using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.LlamaSharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Registry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using DeploySharpApp.Contracts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DeploySharpApp.BackendHost;

internal static class WorkerInferenceAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<WorkerResponse> RunAsync(WorkerRequest request, Action<double>? reportProgress, Action<string>? reportText, CancellationToken cancellationToken)
    {
        string backendId = request.BackendId ?? string.Empty;
        string? modelPath = Value(request.Payload, "modelPath");
        if (string.IsNullOrWhiteSpace(modelPath)) return Error(request, "DSAPP-WORKER-MODEL-PATH-REQUIRED", "A local modelPath is required for native Worker inference.", AppRuntimeState.Unavailable);
        try
        {
            modelPath = Path.GetFullPath(modelPath);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
        {
            return Error(request, "DSAPP-WORKER-MODEL-PATH-INVALID", "The Worker model path is invalid.", AppRuntimeState.Unavailable, exception.Message);
        }
        if (!File.Exists(modelPath)) return Error(request, "DSAPP-WORKER-MODEL-NOT-FOUND", "The Worker model file does not exist.", AppRuntimeState.Unavailable, modelPath);

        WorkerResponse? assetError;
        try
        {
            assetError = await VerifyModelAssetsAsync(request, modelPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Error(request, "DSAPP-WORKER-CANCELLED", "Native Worker model asset verification was cancelled.", AppRuntimeState.Unavailable);
        }
        if (assetError is not null) return assetError;

        reportProgress?.Invoke(0.45);
        try
        {
            WorkerResponse? visual = await VisualReleaseInferenceAdapter.TryRunAsync(request, reportProgress, cancellationToken).ConfigureAwait(false);
            if (visual != null) return visual;
            if (Contains(backendId, "onnxruntime") && string.Equals(Value(request.Payload, "operation"), AppOperationKind.Multimodal.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                string profile = Value(request.Payload, "multimodalProfile") ?? "blip-caption-base";
                return string.Equals(profile, "clip-vit-b-32-image-embedding", StringComparison.OrdinalIgnoreCase)
                    ? await RunClipImageEmbeddingAsync(request, modelPath, reportProgress, cancellationToken).ConfigureAwait(false)
                    : await RunBlipCaptionAsync(request, modelPath, reportProgress, reportText, cancellationToken).ConfigureAwait(false);
            }
            // A ModelPack without a dedicated algorithm adapter still runs its declared
            // primary graph through the provider. Auxiliary files remain verified above
            // and are never silently discarded or represented as an algorithm result.
            if (Contains(backendId, "onnxruntime"))
                return await RunCoreTensorAsync(request, modelPath, new OnnxRuntimeBackendProvider(ParseOnnxRuntimeOptions(request.Payload)), reportProgress, cancellationToken).ConfigureAwait(false);
            if (Contains(backendId, "llamasharp")) return await RunLlamaAsync(request, modelPath, reportProgress, reportText, cancellationToken).ConfigureAwait(false);
            if (Contains(backendId, "openvino")) return await RunCoreTensorAsync(request, modelPath, new OpenVinoBackendProvider(ParseOpenVinoOptions(request.Payload)), reportProgress, cancellationToken).ConfigureAwait(false);
            if (Contains(backendId, "opencv")) return await RunOpenCvAsync(request, modelPath, reportProgress, cancellationToken).ConfigureAwait(false);
            if (Contains(backendId, "tensorrt")) return await RunCoreTensorAsync(request, modelPath, new TensorRtBackendProvider(ParseTensorRtOptions(request.Payload)), reportProgress, cancellationToken).ConfigureAwait(false);
            return Error(request, "DSAPP-WORKER-BACKEND-UNKNOWN", "No native inference adapter is registered for this backend.", AppRuntimeState.Unsupported);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Error(request, "DSAPP-WORKER-CANCELLED", "Native Worker inference was cancelled.", AppRuntimeState.Unavailable);
        }
        catch (DeploySharpException exception)
        {
            return Error(request, exception.ErrorCode, exception.Message, AppRuntimeState.Unavailable, exception.TechnicalDetails);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is JsonException || exception is IOException || exception is UnauthorizedAccessException || exception is OverflowException)
        {
            return Error(request, "DSAPP-WORKER-INPUT-INVALID", "The native Worker input or backend options are invalid.", AppRuntimeState.Unsupported, exception.Message);
        }
        catch (Exception exception)
        {
            return Error(request, "DSAPP-WORKER-NATIVE-EXECUTION-FAILED", "Native Worker inference failed.", AppRuntimeState.Unavailable, exception.GetType().FullName + ": " + exception.Message);
        }
    }

    private static async Task<WorkerResponse?> VerifyModelAssetsAsync(WorkerRequest request, string modelPath, CancellationToken cancellationToken)
    {
        string? json = Value(request.Payload, "modelAssetsJson");
        if (string.IsNullOrWhiteSpace(json)) return null;
        ModelAssetReference[] assets;
        try { assets = JsonSerializer.Deserialize<ModelAssetReference[]>(json, JsonOptions) ?? Array.Empty<ModelAssetReference>(); }
        catch (JsonException exception) { return Error(request, "DSAPP-WORKER-MODEL-ASSETS-INVALID", "The model asset manifest is invalid JSON.", AppRuntimeState.Unsupported, exception.Message); }
        string root = Path.GetDirectoryName(modelPath) ?? modelPath;
        string? declaredRoot = Value(request.Payload, "modelBundleRoot");
        if (!string.IsNullOrWhiteSpace(declaredRoot))
        {
            try { root = Path.GetFullPath(declaredRoot); }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            { return Error(request, "DSAPP-WORKER-MODEL-BUNDLE-ROOT-INVALID", "The model bundle root is invalid.", AppRuntimeState.Unsupported, exception.Message); }
        }
        string rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!modelPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(modelPath, rootPrefix.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            return Error(request, "DSAPP-WORKER-MODEL-OUTSIDE-BUNDLE", "The selected model entrypoint is outside the declared model bundle root.", AppRuntimeState.Unsupported, modelPath);
        foreach (ModelAssetReference asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            try { fullPath = Path.GetFullPath(asset.FullPath); }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            { return Error(request, "DSAPP-WORKER-MODEL-ASSET-PATH-INVALID", "A model asset path is invalid.", AppRuntimeState.Unsupported, exception.Message); }
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(fullPath, rootPrefix.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return Error(request, "DSAPP-WORKER-MODEL-ASSET-OUTSIDE-BUNDLE", "A model asset is outside the selected model bundle directory.", AppRuntimeState.Unsupported, fullPath);
            if (!File.Exists(fullPath)) return Error(request, "DSAPP-WORKER-MODEL-ASSET-NOT-FOUND", "A verified model bundle asset does not exist.", AppRuntimeState.Unavailable, fullPath);
            if (asset.Size > 0 && new FileInfo(fullPath).Length != asset.Size)
                return Error(request, "DSAPP-WORKER-MODEL-ASSET-SIZE-MISMATCH", "A verified model bundle asset has an unexpected size.", AppRuntimeState.Unavailable, fullPath);
            if (!string.IsNullOrWhiteSpace(asset.Sha256) && !await VerifySha256Async(fullPath, asset.Sha256!, cancellationToken).ConfigureAwait(false))
                return Error(request, "DSAPP-WORKER-MODEL-ASSET-SHA256-MISMATCH", "A verified model bundle asset failed SHA256 validation.", AppRuntimeState.Unavailable, fullPath);
        }
        return null;
    }

    private static async Task<bool> VerifySha256Async(string path, string expected, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, useAsync: true);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[128 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0) hash.AppendData(buffer, 0, read);
        return string.Equals(Convert.ToHexString(hash.GetHashAndReset()), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<WorkerResponse> RunBlipCaptionAsync(WorkerRequest request, string visionEncoderPath, Action<double>? reportProgress, Action<string>? reportText, CancellationToken cancellationToken)
    {
        string profileName = Value(request.Payload, "multimodalProfile") ?? "blip-caption-base";
        if (!string.Equals(profileName, "blip-caption-base", StringComparison.OrdinalIgnoreCase))
            return Error(request, "DSAPP-WORKER-MULTIMODAL-PROFILE-UNSUPPORTED", "The Worker currently supports only the audited BLIP caption-base release profile.", AppRuntimeState.Unsupported, profileName);

        string device = Value(request.Payload, "device") ?? "cpu";
        if (!string.Equals(device, "cpu", StringComparison.OrdinalIgnoreCase) && !string.Equals(device, "auto", StringComparison.OrdinalIgnoreCase))
            return Error(request, "DSAPP-WORKER-BLIP-DEVICE-UNAVAILABLE", "The published BLIP caption bundle is enabled only for the ONNX Runtime CPU provider.", AppRuntimeState.Unavailable, device);

        string? decoderValue = Value(request.Payload, "languageDecoderPath");
        string? vocabularyValue = Value(request.Payload, "vocabularyPath");
        string? imageValue = Value(request.Payload, "inputPath");
        if (string.IsNullOrWhiteSpace(decoderValue) || string.IsNullOrWhiteSpace(vocabularyValue) || string.IsNullOrWhiteSpace(imageValue))
            return Error(request, "DSAPP-WORKER-MULTIMODAL-BUNDLE-INCOMPLETE", "BLIP caption inference requires vision encoder, language decoder, vocabulary, and image paths.", AppRuntimeState.Unavailable);

        string decoderPath = Path.GetFullPath(decoderValue);
        string vocabularyPath = Path.GetFullPath(vocabularyValue);
        string imagePath = Path.GetFullPath(imageValue);
        string? missingPath = new[] { decoderPath, vocabularyPath, imagePath }.FirstOrDefault(path => !File.Exists(path));
        if (missingPath != null)
            return Error(request, "DSAPP-WORKER-MULTIMODAL-FILE-NOT-FOUND", "A required BLIP bundle or input image file does not exist.", AppRuntimeState.Unavailable, missingPath);

        GenerativeVisionLanguageProfile profile = GenerativeVisionLanguageProfiles.CreateBlipCaptionBase();
        var tokenizer = new BlipBertTokenizer(vocabularyPath, profile.Tokenizer);
        BackendId backend = OnnxRuntimeBackendProvider.BackendId;
        var bundle = new GenerativeVisionLanguageArtifactBundle(profile, new[]
        {
            new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, visionEncoderPath, backend)),
            new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoderPath, backend))
        });

        Stopwatch preprocess = Stopwatch.StartNew();
        using PreparedVisualInput input = new OpenCvGenerativeVisionLanguageInputFactory().CreateFromFile(imagePath, profile, cancellationToken);
        preprocess.Stop();
        reportProgress?.Invoke(0.55);

        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var session = new GenerativeVisionLanguageSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
        GenerativeVisionLanguageImageState imageState = await session.SetImageAsync(input, cancellationToken: cancellationToken).ConfigureAwait(false);
        reportProgress?.Invoke(0.72);

        int emitted = 0;
        Action<GenerationChunk>? stream = GetBool(request.Payload, "stream", true) && reportText != null
            ? chunk =>
            {
                if (!string.IsNullOrEmpty(chunk.Text)) reportText(chunk.Text);
                emitted++;
                reportProgress?.Invoke(Math.Min(0.96, 0.72 + emitted * 0.006));
            }
            : null;
        GenerativeVisionLanguageResult result = await session.GenerateAsync(GenerativeVisionLanguageRequest.Caption(), tokenizer, stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        reportProgress?.Invoke(0.98);

        string output = JsonSerializer.Serialize(new
        {
            schema = "deploysharp.visual-language.v1",
            task = "image-captioning",
            text = result.Generation.Text,
            finishReason = result.Generation.FinishReason.ToString(),
            promptTokens = result.Generation.Usage.PromptTokens,
            generatedTokens = result.Generation.Usage.GeneratedTokens,
            profileId = profile.ProfileId,
            imageIdentity = imageState.Identity.Identity,
            sourceImageSha256 = imageState.Identity.SourceImageSha256,
            encoderStateSha256 = imageState.ValueSha256,
            encoderShape = imageState.Shape,
            generationIdentity = result.Identity.Identity
        }, JsonOptions);
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["output"] = output,
            ["backendId"] = backend.Value,
            ["device"] = "cpu",
            ["execution"] = "worker",
            ["preprocessMs"] = preprocess.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["inferenceMs"] = (imageState.EncoderTime + result.Timing.DecoderTotal).TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["postprocessMs"] = (result.Timing.PromptTokenize + result.Timing.FinalDecode).TotalMilliseconds.ToString(CultureInfo.InvariantCulture)
        };
        return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, "BLIP image caption completed with ONNX Runtime CPU in the Worker.", payload);
    }

    private static async Task<WorkerResponse> RunClipImageEmbeddingAsync(WorkerRequest request, string imageEncoderPath, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        string device = Value(request.Payload, "device") ?? "cpu";
        if (!string.Equals(device, "cpu", StringComparison.OrdinalIgnoreCase) && !string.Equals(device, "auto", StringComparison.OrdinalIgnoreCase))
            return Error(request, "DSAPP-WORKER-CLIP-DEVICE-UNAVAILABLE", "The published CLIP bundle is enabled only for the ONNX Runtime CPU provider.", AppRuntimeState.Unavailable, device);
        string? textEncoderValue = Value(request.Payload, "textEncoderPath");
        string? imageValue = Value(request.Payload, "inputPath");
        if (string.IsNullOrWhiteSpace(textEncoderValue) || string.IsNullOrWhiteSpace(imageValue))
            return Error(request, "DSAPP-WORKER-MULTIMODAL-BUNDLE-INCOMPLETE", "CLIP image embedding requires image encoder, text encoder, and image paths.", AppRuntimeState.Unavailable);
        string textEncoderPath = Path.GetFullPath(textEncoderValue);
        string imagePath = Path.GetFullPath(imageValue);
        string? missingPath = new[] { imageEncoderPath, textEncoderPath, imagePath }.FirstOrDefault(path => !File.Exists(path));
        if (missingPath != null) return Error(request, "DSAPP-WORKER-MULTIMODAL-FILE-NOT-FOUND", "A required CLIP bundle or input image file does not exist.", AppRuntimeState.Unavailable, missingPath);

        VisionLanguageEmbeddingProfile profile = VisionLanguageProfiles.CreateClipVitB32();
        BackendId backend = OnnxRuntimeBackendProvider.BackendId;
        var bundle = new VisionLanguageArtifactBundle(
            profile,
            profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder, imageEncoderPath, backend),
            profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder, textEncoderPath, backend));
        Stopwatch preprocess = Stopwatch.StartNew();
        using PreparedVisualInput input = new OpenCvVisionLanguageInputFactory().CreateFromFile(imagePath, profile, cancellationToken: cancellationToken);
        preprocess.Stop();
        reportProgress?.Invoke(.58);
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var session = new VisionLanguageEmbeddingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
        VisionLanguageImageEmbedding embedding = await session.EncodeImageAsync(input, cancellationToken: cancellationToken).ConfigureAwait(false);
        reportProgress?.Invoke(.96);
        string output = JsonSerializer.Serialize(new
        {
            schema = "deploysharp.visual-language.v1",
            task = "vision-language-image-embedding",
            profileId = profile.ProfileId,
            dimension = embedding.Dimension,
            batchSize = embedding.BatchSize,
            sourceImageSha256 = embedding.Identity.ContentSha256,
            artifactIdentity = embedding.Identity.ArtifactIdentity,
            embeddingSha256 = embedding.Sha256,
            values = embedding.CopyValues()
        }, JsonOptions);
        return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, "CLIP image embedding completed with ONNX Runtime CPU in the Worker.", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["output"] = output,
            ["backendId"] = backend.Value,
            ["device"] = "cpu",
            ["execution"] = "worker",
            ["preprocessMs"] = preprocess.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["inferenceMs"] = embedding.EncoderTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["postprocessMs"] = "0"
        });
    }

    public static async Task<WorkerResponse> BenchmarkAsync(WorkerRequest request, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        string? suppliedPath = Value(request.Payload, "modelPath");
        if (string.IsNullOrWhiteSpace(suppliedPath)) return Error(request, "DSAPP-WORKER-MODEL-PATH-REQUIRED", "A local modelPath is required for native Worker benchmark.", AppRuntimeState.Unavailable);
        string modelPath;
        try { modelPath = Path.GetFullPath(suppliedPath); }
        catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
        { return Error(request, "DSAPP-WORKER-MODEL-PATH-INVALID", "The Worker benchmark model path is invalid.", AppRuntimeState.Unavailable, exception.Message); }
        if (!File.Exists(modelPath)) return Error(request, "DSAPP-WORKER-MODEL-NOT-FOUND", "The Worker benchmark model file does not exist.", AppRuntimeState.Unavailable, modelPath);

        try
        {
            WorkerResponse? assetError = await VerifyModelAssetsAsync(request, modelPath, cancellationToken).ConfigureAwait(false);
            if (assetError is not null) return assetError;
            string backendId = request.BackendId ?? string.Empty;
            if (Contains(backendId, "llamasharp")) return await BenchmarkLlamaAsync(request, modelPath, reportProgress, cancellationToken).ConfigureAwait(false);
            return await BenchmarkTensorAsync(request, modelPath, reportProgress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return Error(request, "DSAPP-WORKER-CANCELLED", "Native Worker benchmark was cancelled.", AppRuntimeState.Unavailable); }
        catch (DeploySharpException exception)
        { return Error(request, exception.ErrorCode, exception.Message, AppRuntimeState.Unavailable, exception.TechnicalDetails); }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is JsonException || exception is IOException || exception is UnauthorizedAccessException || exception is OverflowException)
        { return Error(request, "DSAPP-WORKER-INPUT-INVALID", "The native benchmark input or backend options are invalid.", AppRuntimeState.Unsupported, exception.Message); }
        catch (Exception exception)
        { return Error(request, "DSAPP-WORKER-NATIVE-BENCHMARK-FAILED", "Native Worker benchmark failed.", AppRuntimeState.Unavailable, exception.GetType().FullName + ": " + exception.Message); }
    }

    private static async Task<WorkerResponse> BenchmarkTensorAsync(WorkerRequest request, string modelPath, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        string backend = request.BackendId ?? string.Empty;
        string format = Value(request.Payload, "modelFormat") ?? Path.GetExtension(modelPath).TrimStart('.');
        IReadOnlyList<WorkerTensorInput> inputs = ParseInputs(request.Payload);
        if (inputs.Count == 0) return Error(request, "DSAPP-WORKER-TENSOR-INPUT-REQUIRED", "Native tensor benchmark requires named tensor inputs.", AppRuntimeState.Unsupported);

        IBackendProvider provider;
        if (Contains(backend, "onnxruntime"))
        {
            if (!string.Equals(format, "onnx", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "ONNX Runtime benchmark requires ONNX.", AppRuntimeState.Unsupported, format);
            string device = Value(request.Payload, "device") ?? "cpu";
            if (!string.Equals(device, "cpu", StringComparison.OrdinalIgnoreCase) && !string.Equals(device, "auto", StringComparison.OrdinalIgnoreCase))
                return Error(request, "DSAPP-WORKER-ORT-DEVICE-UNAVAILABLE", "This Worker packages the ONNX Runtime CPU provider; CUDA is not implicitly downgraded to CPU.", AppRuntimeState.Unavailable, device);
            provider = new OnnxRuntimeBackendProvider(ParseOnnxRuntimeOptions(request.Payload));
        }
        else if (Contains(backend, "opencv"))
        {
            if (!string.Equals(format, "onnx", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "OpenCV DNN benchmark requires ONNX.", AppRuntimeState.Unsupported, format);
            string[] outputNames = (Value(request.Payload, "outputTensorNames") ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();
            if (outputNames.Length == 0) return Error(request, "DSAPP-WORKER-OPENCV-OUTPUT-CONTRACT-REQUIRED", "OpenCV DNN benchmark requires an explicit output contract.", AppRuntimeState.Unsupported);
            var outputShapes = JsonSerializer.Deserialize<Dictionary<string, long[]>>(Value(request.Payload, "outputTensorShapesJson") ?? "{}", JsonOptions) ?? new Dictionary<string, long[]>();
            var outputTypes = JsonSerializer.Deserialize<Dictionary<string, string>>(Value(request.Payload, "outputTensorElementTypesJson") ?? "{}", JsonOptions) ?? new Dictionary<string, string>();
            var modelId = new ModelId(request.ModelId ?? "worker/opencv-benchmark");
            var inputDescriptors = inputs.Select(input => new TensorDescriptor(input.Name, ToElementType(input.ElementType), new TensorShape(input.Shape))).ToArray();
            var outputDescriptors = outputNames.Select(name => new TensorDescriptor(name, ToElementType(outputTypes.TryGetValue(name, out string? type) ? type : "float32"), new TensorShape(outputShapes.TryGetValue(name, out long[]? shape) ? shape : new[] { -1L }))).ToArray();
            provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(new OpenCvDnnModelContract(modelId, inputDescriptors, outputDescriptors, inputs.Where(IsImageTensorInput).Select(input => input.Name)), numThreads: GetNullableInt(request.Payload, "numThreads")));
        }
        else if (Contains(backend, "openvino"))
        {
            if (!string.Equals(format, "onnx", StringComparison.OrdinalIgnoreCase) && !string.Equals(format, "openvino-ir", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "OpenVINO benchmark requires ONNX or OpenVINO IR.", AppRuntimeState.Unsupported, format);
            provider = new OpenVinoBackendProvider(ParseOpenVinoOptions(request.Payload));
        }
        else if (Contains(backend, "tensorrt"))
        {
            if (!string.Equals(format, "tensorrt-engine", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "TensorRT benchmark requires a device-bound engine/plan.", AppRuntimeState.Unsupported, format);
            TensorRtValidationResult validation = TensorRtEngineIdentityValidator.Validate(modelPath, request.Payload);
            if (!validation.Succeeded)
            {
                var details = new Dictionary<string, string>(validation.Details, StringComparer.Ordinal) { ["state"] = AppRuntimeState.Unavailable.ToString(), ["diagnosticCode"] = validation.Code, ["execution"] = "worker" };
                return new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, validation.Message, details);
            }
            provider = new TensorRtBackendProvider(ParseTensorRtOptions(request.Payload));
        }
        else return Error(request, "DSAPP-WORKER-BENCHMARK-BACKEND-UNKNOWN", "No native tensor benchmark adapter is registered for this backend.", AppRuntimeState.Unsupported);

        Stopwatch initialization = Stopwatch.StartNew();
        using (provider)
        {
            BackendId providerId = provider.Descriptor.Id;
            var artifact = new ModelArtifact(new ModelId(request.ModelId ?? "worker/benchmark"), format, modelPath, Value(request.Payload, "modelSha256"), providerId);
            var backendRequest = new BackendRequest(BackendCapabilities.TensorInference, providerId, Value(request.Payload, "device"));
            using IInferenceSession session = provider.CreateSession(artifact, backendRequest, new SessionOptions(GetInt(request.Payload, "maxConcurrency", 1), GetBool(request.Payload, "enableProfiling", false)));
            InferenceInputs inferenceInputs = await CreateInferenceInputsAsync(request, inputs, cancellationToken).ConfigureAwait(false);
            initialization.Stop();
            InferenceOutputs? last = null;
            WorkerResponse report = await RunBenchmarkLoopAsync(request, initialization.Elapsed.TotalMilliseconds, reportProgress, cancellationToken, async token =>
            {
                last = await session.RunAsync(inferenceInputs, token).ConfigureAwait(false);
                return JsonSerializer.Serialize(last.Select(item => new WorkerTensorOutput(item.Name, item.Tensor.ElementType.ToString(), item.Tensor.Shape.ToArray(), item.Tensor.Buffer)).ToArray(), JsonOptions);
            }).ConfigureAwait(false);
            return report;
        }
    }

    private static async Task<WorkerResponse> BenchmarkLlamaAsync(WorkerRequest request, string modelPath, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        string format = Value(request.Payload, "modelFormat") ?? Path.GetExtension(modelPath).TrimStart('.');
        if (!string.Equals(format, "gguf", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "LLamaSharp benchmark requires GGUF.", AppRuntimeState.Unsupported, format);
        string prompt = Value(request.Payload, "prompt") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt)) return Error(request, "DSAPP-WORKER-PROMPT-REQUIRED", "LLamaSharp benchmark requires an explicit prompt.", AppRuntimeState.Unsupported);
        string device = Value(request.Payload, "device") ?? "cpu";
        if (!string.Equals(device, "cpu", StringComparison.OrdinalIgnoreCase) && !string.Equals(device, "auto", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-LLAMA-DEVICE-UNAVAILABLE", "This Worker packages the LLamaSharp CPU provider.", AppRuntimeState.Unavailable, device);

        Stopwatch initialization = Stopwatch.StartNew();
        using var registry = new LanguageModelRegistry();
        registry.UseLlamaSharp(ParseLlamaOptions(request.Payload));
        var artifact = new ModelArtifact(new ModelId(request.ModelId ?? "worker/llama-benchmark"), "gguf", modelPath, Value(request.Payload, "modelSha256"), LlamaSharpBackendProvider.BackendId);
        using ILanguageModelSession session = registry.CreateSession(artifact, new LanguageModelRequest(LanguageModelCapabilities.TextGeneration, LlamaSharpBackendProvider.BackendId, device));
        initialization.Stop();
        var generationOptions = new GenerationOptions(GetInt(request.Payload, "maxTokens", 32), GetFloat(request.Payload, "temperature", 0.8f), GetFloat(request.Payload, "topP", 0.95f), GetInt(request.Payload, "topK", 40), GetNullableInt(request.Payload, "seed"), GetStops(request.Payload), TimeSpan.FromMilliseconds(GetDouble(request.Payload, "timeoutMs", 120000)));
        return await RunBenchmarkLoopAsync(request, initialization.Elapsed.TotalMilliseconds, reportProgress, cancellationToken, async token =>
        {
            GenerationResult result = await session.GenerateAsync(new TextGenerationRequest(prompt, generationOptions), token).ConfigureAwait(false);
            return result.Text;
        }).ConfigureAwait(false);
    }

    private static async Task<WorkerResponse> RunBenchmarkLoopAsync(WorkerRequest request, double initializationMs, Action<double>? reportProgress, CancellationToken cancellationToken, Func<CancellationToken, Task<string>> run)
    {
        int warmup = GetInt(request.Payload, "warmup", 3);
        int iterations = GetInt(request.Payload, "iterations", 20);
        if (warmup < 0 || warmup > 1000 || iterations <= 0 || iterations > 10000) throw new ArgumentOutOfRangeException("iterations", "Warmup must be 0..1000 and iterations 1..10000.");
        reportProgress?.Invoke(0.4);
        for (int index = 0; index < warmup; index++) await run(cancellationToken).ConfigureAwait(false);
        var samples = new List<double>(iterations);
        string lastOutput = string.Empty;
        for (int index = 0; index < iterations; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch timer = Stopwatch.StartNew();
            lastOutput = await run(cancellationToken).ConfigureAwait(false);
            timer.Stop();
            samples.Add(timer.Elapsed.TotalMilliseconds);
            reportProgress?.Invoke(0.4 + ((index + 1d) / iterations * 0.55));
        }
        samples.Sort();
        double p50 = Percentile(samples, 0.50);
        double p95 = Percentile(samples, 0.95);
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["backendId"] = request.BackendId ?? string.Empty,
            ["device"] = Value(request.Payload, "device") ?? "cpu",
            ["execution"] = "worker",
            ["timingScope"] = "session-execution-only",
            ["warmup"] = warmup.ToString(CultureInfo.InvariantCulture),
            ["iterations"] = iterations.ToString(CultureInfo.InvariantCulture),
            ["initializationMs"] = initializationMs.ToString(CultureInfo.InvariantCulture),
            ["p50Ms"] = p50.ToString(CultureInfo.InvariantCulture),
            ["p95Ms"] = p95.ToString(CultureInfo.InvariantCulture),
            ["throughput"] = (1000d / samples.Average()).ToString(CultureInfo.InvariantCulture),
            ["output"] = lastOutput,
            ["runtimeIdentifier"] = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
            ["processArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            ["osDescription"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["frameworkDescription"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["timestampUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, "Native benchmark completed with one reused provider/session; timings cover real execution only.", payload);
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        double position = (values.Count - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        return lower == upper ? values[lower] : values[lower] + ((values[upper] - values[lower]) * (position - lower));
    }

    private static async Task<WorkerResponse> RunLlamaAsync(WorkerRequest request, string modelPath, Action<double>? reportProgress, Action<string>? reportText, CancellationToken cancellationToken)
    {
        string format = Value(request.Payload, "modelFormat") ?? Path.GetExtension(modelPath).TrimStart('.');
        if (!string.Equals(format, "gguf", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "LLamaSharp Worker requires a modelFormat of gguf.", AppRuntimeState.Unsupported, format);
        var artifact = new ModelArtifact(new ModelId(request.ModelId ?? "worker/llamasharp"), "gguf", modelPath, Value(request.Payload, "modelSha256"), LlamaSharpBackendProvider.BackendId);
        string prompt = Value(request.Payload, "prompt") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt)) return Error(request, "DSAPP-WORKER-PROMPT-REQUIRED", "LLamaSharp generation and embedding require non-empty text.", AppRuntimeState.Unsupported);
        string device = Value(request.Payload, "device") ?? "cpu";
        if (!string.Equals(device, "cpu", StringComparison.OrdinalIgnoreCase) && !string.Equals(device, "auto", StringComparison.OrdinalIgnoreCase))
            return Error(request, "DSAPP-WORKER-LLAMA-DEVICE-UNAVAILABLE", "This Worker packages the LLamaSharp CPU provider; GPU device requests are unavailable until a matching provider is installed.", AppRuntimeState.Unavailable, device);

        using var registry = new LanguageModelRegistry();
        registry.UseLlamaSharp(ParseLlamaOptions(request.Payload));
        bool embedding = string.Equals(Value(request.Payload, "operation"), AppOperationKind.Embedding.ToString(), StringComparison.OrdinalIgnoreCase);
        LanguageModelCapabilities capabilities = embedding ? LanguageModelCapabilities.Embeddings : LanguageModelCapabilities.TextGeneration;
        using ILanguageModelSession session = registry.CreateSession(artifact, new LanguageModelRequest(capabilities, LlamaSharpBackendProvider.BackendId, device));
        reportProgress?.Invoke(0.6);
        if (embedding)
        {
            bool normalize = GetBool(request.Payload, "normalize", true);
            EmbeddingResult embeddingResult = await session.EmbedAsync(new TextEmbeddingRequest(prompt, normalize, TimeSpan.FromMilliseconds(GetDouble(request.Payload, "timeoutMs", 120000))), cancellationToken).ConfigureAwait(false);
            reportProgress?.Invoke(0.95);
            var embeddingPayload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["output"] = JsonSerializer.Serialize(new { dimensions = embeddingResult.Dimensions, normalized = embeddingResult.IsNormalized, values = embeddingResult.ToArray() }, JsonOptions),
                ["dimensions"] = embeddingResult.Dimensions.ToString(CultureInfo.InvariantCulture),
                ["normalized"] = embeddingResult.IsNormalized.ToString(CultureInfo.InvariantCulture),
                ["backendId"] = "llamasharp",
                ["execution"] = "worker"
            };
            return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, "LLamaSharp GGUF embedding completed in the Worker.", embeddingPayload);
        }

        int maximumTokens = GetInt(request.Payload, "maxTokens", 256);
        var generationOptions = new GenerationOptions(
            maximumTokens,
            GetFloat(request.Payload, "temperature", 0.8f),
            GetFloat(request.Payload, "topP", 0.95f),
            GetInt(request.Payload, "topK", 40),
            GetNullableInt(request.Payload, "seed"),
            GetStops(request.Payload),
            TimeSpan.FromMilliseconds(GetDouble(request.Payload, "timeoutMs", 120000)));
        bool stream = GetBool(request.Payload, "stream", false) && reportText != null;
        string output;
        string finishReason;
        int generatedTokens;
        int promptTokens;
        if (stream)
        {
            var builder = new StringBuilder();
            GenerationFinishReason finish = GenerationFinishReason.None;
            generatedTokens = 0;
            await foreach (GenerationChunk chunk in session.StreamAsync(new TextGenerationRequest(prompt, generationOptions), cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    builder.Append(chunk.Text);
                    reportText!(chunk.Text);
                }
                if (chunk.TokenId.HasValue) generatedTokens++;
                if (chunk.IsTerminal) finish = chunk.FinishReason;
                reportProgress?.Invoke(0.6 + Math.Min(0.35, generatedTokens / (double)Math.Max(1, maximumTokens) * 0.35));
            }
            output = builder.ToString();
            finishReason = finish.ToString();
            promptTokens = 0;
        }
        else
        {
            GenerationResult result = await session.GenerateAsync(new TextGenerationRequest(prompt, generationOptions), cancellationToken).ConfigureAwait(false);
            output = result.Text;
            finishReason = result.FinishReason.ToString();
            promptTokens = result.Usage.PromptTokens;
            generatedTokens = result.Usage.GeneratedTokens;
        }
        reportProgress?.Invoke(0.95);
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["output"] = output,
            ["finishReason"] = finishReason,
            ["promptTokens"] = promptTokens.ToString(CultureInfo.InvariantCulture),
            ["generatedTokens"] = generatedTokens.ToString(CultureInfo.InvariantCulture),
            ["backendId"] = "llamasharp",
            ["execution"] = "worker",
            ["streamed"] = stream.ToString(CultureInfo.InvariantCulture)
        };
        return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, stream ? "LLamaSharp GGUF streaming generation completed in the Worker." : "LLamaSharp GGUF generation completed in the Worker.", payload);
    }

    private static async Task<WorkerResponse> RunOpenCvAsync(WorkerRequest request, string modelPath, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        string format = Value(request.Payload, "modelFormat") ?? Path.GetExtension(modelPath).TrimStart('.');
        if (!string.Equals(format, "onnx", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "OpenCV DNN Worker requires a modelFormat of onnx.", AppRuntimeState.Unsupported, format);
        IReadOnlyList<WorkerTensorInput> inputs = ParseInputs(request.Payload);
        if (inputs.Count == 0) return Error(request, "DSAPP-WORKER-TENSOR-INPUT-REQUIRED", "OpenCV DNN Worker requires named tensor inputs.", AppRuntimeState.Unsupported);
        string[] outputNames = (Value(request.Payload, "outputTensorNames") ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();
        if (outputNames.Length == 0) return Error(request, "DSAPP-WORKER-OPENCV-OUTPUT-CONTRACT-REQUIRED", "OpenCV DNN requires outputTensorNames and outputTensorShapesJson in backend options because its model contract is explicit.", AppRuntimeState.Unsupported);
        var outputShapes = JsonSerializer.Deserialize<Dictionary<string, long[]>>(Value(request.Payload, "outputTensorShapesJson") ?? "{}", JsonOptions) ?? new Dictionary<string, long[]>();
        var outputTypes = JsonSerializer.Deserialize<Dictionary<string, string>>(Value(request.Payload, "outputTensorElementTypesJson") ?? "{}", JsonOptions) ?? new Dictionary<string, string>();
        var modelId = new ModelId(request.ModelId ?? "worker/opencv");
        var inputDescriptors = inputs.Select(input => new TensorDescriptor(input.Name, ToElementType(input.ElementType), new TensorShape(input.Shape))).ToArray();
        var outputDescriptors = outputNames.Select(name => new TensorDescriptor(name, ToElementType(outputTypes.TryGetValue(name, out string? type) ? type : "float32"), new TensorShape(outputShapes.TryGetValue(name, out long[]? shape) ? shape : new[] { -1L }))).ToArray();
        var contract = new OpenCvDnnModelContract(modelId, inputDescriptors, outputDescriptors, inputs.Where(IsImageTensorInput).Select(input => input.Name));
        using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, numThreads: GetNullableInt(request.Payload, "numThreads")));
        return await RunSessionAsync(request, modelPath, "onnx", OpenCvDnnBackendProvider.BackendId, provider, inputs, reportProgress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WorkerResponse> RunCoreTensorAsync(WorkerRequest request, string modelPath, IBackendProvider provider, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        string format = Value(request.Payload, "modelFormat") ?? Path.GetExtension(modelPath).TrimStart('.');
        if (provider is TensorRtBackendProvider)
        {
            if (!string.Equals(format, "tensorrt-engine", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "TensorRT Worker requires a modelFormat of tensorrt-engine; ONNX-to-engine build is not implicit.", AppRuntimeState.Unsupported, format);
            TensorRtValidationResult validation = TensorRtEngineIdentityValidator.Validate(modelPath, request.Payload);
            if (!validation.Succeeded)
            {
                var payload = new Dictionary<string, string>(validation.Details, StringComparer.Ordinal)
                {
                    ["state"] = AppRuntimeState.Unavailable.ToString(),
                    ["backendId"] = request.BackendId ?? "tensorrt",
                    ["diagnosticCode"] = validation.Code,
                    ["execution"] = "worker"
                };
                return new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, validation.Message, payload);
            }
        }
        if (provider is OpenVinoBackendProvider && !string.Equals(format, "onnx", StringComparison.OrdinalIgnoreCase) && !string.Equals(format, "openvino-ir", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "OpenVINO Worker requires a modelFormat of onnx or openvino-ir.", AppRuntimeState.Unsupported, format);
        IReadOnlyList<WorkerTensorInput> inputs = ParseInputs(request.Payload);
        if (inputs.Count == 0) return Error(request, "DSAPP-WORKER-TENSOR-INPUT-REQUIRED", "Native tensor Worker inference requires named tensor inputs.", AppRuntimeState.Unsupported);
        BackendId backendId = provider.Descriptor.Id;
        using (provider)
            return await RunSessionAsync(request, modelPath, format, backendId, provider, inputs, reportProgress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WorkerResponse> RunSessionAsync(WorkerRequest request, string modelPath, string format, BackendId backendId, IBackendProvider provider, IReadOnlyList<WorkerTensorInput> inputs, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        var artifact = new ModelArtifact(new ModelId(request.ModelId ?? "worker/model"), format, modelPath, Value(request.Payload, "modelSha256"), backendId);
        var backendRequest = new BackendRequest(BackendCapabilities.TensorInference, backendId, Value(request.Payload, "device"));
        using IInferenceSession session = provider.CreateSession(artifact, backendRequest, new SessionOptions(GetInt(request.Payload, "maxConcurrency", 1), GetBool(request.Payload, "enableProfiling", false)));
        Stopwatch preprocess = Stopwatch.StartNew();
        InferenceInputs inferenceInputs = await CreateInferenceInputsAsync(request, inputs, cancellationToken).ConfigureAwait(false);
        preprocess.Stop();
        reportProgress?.Invoke(0.65);
        Stopwatch stopwatch = Stopwatch.StartNew();
        InferenceOutputs outputs = await session.RunAsync(inferenceInputs, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        reportProgress?.Invoke(0.95);
        Stopwatch postprocess = Stopwatch.StartNew();
        string outputJson = JsonSerializer.Serialize(outputs.Select(item => new WorkerTensorOutput(item.Name, item.Tensor.ElementType.ToString(), item.Tensor.Shape.ToArray(), item.Tensor.Buffer)).ToArray(), JsonOptions);
        postprocess.Stop();
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["output"] = outputJson,
            ["backendId"] = backendId.Value,
            ["device"] = Value(request.Payload, "device") ?? "cpu",
            ["execution"] = "worker",
            ["preprocessMs"] = preprocess.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["inferenceMs"] = stopwatch.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            ["postprocessMs"] = postprocess.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)
        };
        return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, backendId.Value + " inference completed in the Worker.", payload);
    }

    private static async Task<InferenceInputs> CreateInferenceInputsAsync(WorkerRequest request, IReadOnlyList<WorkerTensorInput> inputs, CancellationToken cancellationToken)
    {
        var tensors = new List<NamedTensor>(inputs.Count);
        foreach (WorkerTensorInput input in inputs)
        {
            ITensor tensor = input.ImageInput
                ? await CreateImageTensorAsync(request, input, cancellationToken).ConfigureAwait(false)
                : ToTensor(input.ElementType, new TensorShape(input.Shape), input.ValuesJson ?? File.ReadAllText(Path.GetFullPath(input.ValuesFilePath!)));
            tensors.Add(new NamedTensor(input.Name, tensor));
        }
        return new InferenceInputs(tensors);
    }

    private static async Task<ITensor> CreateImageTensorAsync(WorkerRequest request, WorkerTensorInput input, CancellationToken cancellationToken)
    {
        if (!string.Equals(input.ElementType, "float32", StringComparison.OrdinalIgnoreCase) && !string.Equals(input.ElementType, "float", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Image tensor inputs must use float32.");
        var shape = new TensorShape(input.Shape);
        if (shape.Rank != 4 || shape[0] != 1 || (shape[1] != 1 && shape[1] != 3 && shape[1] != 4) || shape[2] <= 0 || shape[3] <= 0)
            throw new ArgumentException("Image tensor shape must be [1,1|3|4,height,width] with positive spatial dimensions.");
        string imagePath = Path.GetFullPath(Value(request.Payload, "inputPath") ?? throw new ArgumentException("An inputPath is required for image tensor input."));
        if (!File.Exists(imagePath)) throw new FileNotFoundException("The Worker image input does not exist.", imagePath);

        int channels = checked((int)shape[1]);
        int height = checked((int)shape[2]);
        int width = checked((int)shape[3]);
        ResizeMode resizeMode = (Value(request.Payload, "imageResizeMode") ?? "stretch").ToLowerInvariant() switch
        {
            "stretch" => ResizeMode.Stretch,
            "pad" => ResizeMode.Pad,
            "crop" => ResizeMode.Crop,
            _ => throw new ArgumentException("imageResizeMode must be 'stretch', 'pad', or 'crop'.")
        };
        string colorOrder = (Value(request.Payload, "imageColorOrder") ?? "rgb").ToLowerInvariant();
        if (colorOrder != "rgb" && colorOrder != "bgr") throw new ArgumentException("imageColorOrder must be 'rgb' or 'bgr'.");
        float scale = GetFloat(request.Payload, "imageScale", 1f / 255f);
        float meanRed = GetFloat(request.Payload, "imageMeanR", 0f);
        float meanGreen = GetFloat(request.Payload, "imageMeanG", 0f);
        float meanBlue = GetFloat(request.Payload, "imageMeanB", 0f);
        float stdRed = GetPositiveFloat(request.Payload, "imageStdR", 1f);
        float stdGreen = GetPositiveFloat(request.Payload, "imageStdG", 1f);
        float stdBlue = GetPositiveFloat(request.Payload, "imageStdB", 1f);
        int padValue = GetInt(request.Payload, "imagePadValue", 0);
        if (padValue is < 0 or > 255) throw new ArgumentOutOfRangeException("imagePadValue", "imagePadValue must be between 0 and 255.");

        using Image<Rgb24> image = await Image.LoadAsync<Rgb24>(imagePath, cancellationToken).ConfigureAwait(false);
        image.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(width, height), Mode = resizeMode, PadColor = Color.FromRgb((byte)padValue, (byte)padValue, (byte)padValue) }));
        var values = new float[checked(channels * height * width)];
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                Rgb24 pixel = image[x, y];
                int offset = y * width + x;
                float red = ((pixel.R * scale) - meanRed) / stdRed;
                float green = ((pixel.G * scale) - meanGreen) / stdGreen;
                float blue = ((pixel.B * scale) - meanBlue) / stdBlue;
                if (colorOrder == "bgr") (red, blue) = (blue, red);
                values[offset] = channels == 1 ? (red + green + blue) / 3f : red;
                if (channels > 1) values[height * width + offset] = green;
                if (channels > 2) values[2 * height * width + offset] = blue;
                if (channels > 3) values[3 * height * width + offset] = 1f;
            }
        }
        return new Tensor<float>(shape, values, TensorBufferOwnership.Transfer);
    }

    private static ITensor ToTensor(string elementType, TensorShape shape, string json)
    {
        switch (elementType.ToLowerInvariant())
        {
            case "float32": case "float": return new Tensor<float>(shape, JsonSerializer.Deserialize<float[]>(json, JsonOptions) ?? throw new FormatException("Tensor values must be an array."), TensorBufferOwnership.Transfer);
            case "float64": case "double": return new Tensor<double>(shape, JsonSerializer.Deserialize<double[]>(json, JsonOptions) ?? throw new FormatException("Tensor values must be an array."), TensorBufferOwnership.Transfer);
            case "int32": return new Tensor<int>(shape, JsonSerializer.Deserialize<int[]>(json, JsonOptions) ?? throw new FormatException("Tensor values must be an array."), TensorBufferOwnership.Transfer);
            case "int64": return new Tensor<long>(shape, JsonSerializer.Deserialize<long[]>(json, JsonOptions) ?? throw new FormatException("Tensor values must be an array."), TensorBufferOwnership.Transfer);
            case "uint8": return new Tensor<byte>(shape, JsonSerializer.Deserialize<byte[]>(json, JsonOptions) ?? throw new FormatException("Tensor values must be an array."), TensorBufferOwnership.Transfer);
            case "int8": return new Tensor<sbyte>(shape, JsonSerializer.Deserialize<sbyte[]>(json, JsonOptions) ?? throw new FormatException("Tensor values must be an array."), TensorBufferOwnership.Transfer);
            case "bool": case "boolean": return new Tensor<bool>(shape, JsonSerializer.Deserialize<bool[]>(json, JsonOptions) ?? throw new FormatException("Tensor values must be an array."), TensorBufferOwnership.Transfer);
            default: throw new NotSupportedException("Unsupported Worker tensor element type: " + elementType);
        }
    }

    private static IReadOnlyList<WorkerTensorInput> ParseInputs(IReadOnlyDictionary<string, string> payload)
    {
        return JsonSerializer.Deserialize<List<WorkerTensorInput>>(Value(payload, "tensorInputsJson") ?? "[]", JsonOptions) ?? new List<WorkerTensorInput>();
    }

    private static bool IsImageTensorInput(WorkerTensorInput input)
    {
        return input.ImageInput || input.Shape.Length == 4 && input.Shape[0] > 0 && (input.Shape[1] == 1 || input.Shape[1] == 3 || input.Shape[1] == 4) && input.Shape[2] > 0 && input.Shape[3] > 0 && (string.Equals(input.ElementType, "float32", StringComparison.OrdinalIgnoreCase) || string.Equals(input.ElementType, "float", StringComparison.OrdinalIgnoreCase));
    }

    private static LlamaSharpOptions ParseLlamaOptions(IReadOnlyDictionary<string, string> payload) => new LlamaSharpOptions(GetNullableUInt(payload, "contextSize"), GetInt(payload, "gpuLayerCount", 0), GetInt(payload, "mainGpu", 0), GetNullableInt(payload, "threads"), GetNullableInt(payload, "batchThreads"), (uint)GetInt(payload, "batchSize", 512), (uint)GetInt(payload, "sequenceCount", 1), GetBool(payload, "useMemoryMap", true), GetBool(payload, "useMemoryLock", false), LlamaEmbeddingPooling.Mean, Value(payload, "device") ?? "cpu");

    private static OnnxRuntimeOptions ParseOnnxRuntimeOptions(IReadOnlyDictionary<string, string> payload) => new OnnxRuntimeOptions(
        GetInt(payload, "intraOpThreads", 0),
        GetInt(payload, "interOpThreads", 0),
        ParseEnum(Value(payload, "graphOptimization"), OnnxRuntimeGraphOptimization.All),
        ParseEnum(Value(payload, "onnxExecutionMode"), OnnxRuntimeExecutionMode.Sequential),
        GetBool(payload, "enableMemoryPattern", true),
        GetBool(payload, "enableCpuMemoryArena", true),
        ParseEnum(Value(payload, "logSeverity"), OnnxRuntimeLogSeverity.Warning),
        Value(payload, "logId"),
        Value(payload, "profilingOutputPathPrefix"),
        OnnxRuntimeExecutionProvider.Cpu);

    private static OpenVinoOptions ParseOpenVinoOptions(IReadOnlyDictionary<string, string> payload) => new OpenVinoOptions(Value(payload, "device") ?? "CPU", ParseEnum(Value(payload, "performanceHint"), OpenVinoPerformanceHint.Default), GetNullableInt(payload, "streams"), GetNullableInt(payload, "inferenceThreads"), Value(payload, "cacheDirectory"), GetBool(payload, "enableProfiling", false), GetNullableInt(payload, "requestCount"), null, GetBool(payload, "allowDynamicShapes", true));

    private static TensorRtBackendOptions ParseTensorRtOptions(IReadOnlyDictionary<string, string> payload) => new TensorRtBackendOptions(ParseTensorRtApiVersion(Value(payload, "apiVersion") ?? Value(payload, "tensorRtApiVersion")), GetInt(payload, "optimizationProfile", 0), GetLong(payload, "maximumEngineBytes", int.MaxValue), Value(payload, "cudaTargetArchitecture"), GetBool(payload, "cacheImmutableHostInputsOnDevice", false));

    private static TensorRtApiVersion ParseTensorRtApiVersion(string? value) => value switch
    {
        "8" => TensorRtApiVersion.TensorRt8,
        "10" => TensorRtApiVersion.TensorRt10,
        "11" => TensorRtApiVersion.TensorRt11,
        _ => ParseEnum(value, TensorRtApiVersion.TensorRt10)
    };

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum => value != null && Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(parsed) ? parsed : fallback;
    private static TensorElementType ToElementType(string value) => value.ToLowerInvariant() switch { "float32" or "float" => TensorElementType.Float32, "float64" or "double" => TensorElementType.Float64, "int32" => TensorElementType.Int32, "int64" => TensorElementType.Int64, "int8" => TensorElementType.Int8, "uint8" => TensorElementType.UInt8, "bool" or "boolean" => TensorElementType.Boolean, _ => throw new NotSupportedException("Unsupported tensor element type: " + value) };
    private static string[] GetStops(IReadOnlyDictionary<string, string> payload) => (Value(payload, "stopSequencesJson") == null ? Array.Empty<string>() : JsonSerializer.Deserialize<string[]>(Value(payload, "stopSequencesJson")!, JsonOptions) ?? Array.Empty<string>());
    private static string? Value(IReadOnlyDictionary<string, string> payload, string key) => payload.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    private static int GetInt(IReadOnlyDictionary<string, string> payload, string key, int fallback) => Value(payload, key) == null ? fallback : int.Parse(Value(payload, key)!, CultureInfo.InvariantCulture);
    private static long GetLong(IReadOnlyDictionary<string, string> payload, string key, long fallback) => Value(payload, key) == null ? fallback : long.Parse(Value(payload, key)!, CultureInfo.InvariantCulture);
    private static double GetDouble(IReadOnlyDictionary<string, string> payload, string key, double fallback) => Value(payload, key) == null ? fallback : double.Parse(Value(payload, key)!, CultureInfo.InvariantCulture);
    private static float GetFloat(IReadOnlyDictionary<string, string> payload, string key, float fallback) => Value(payload, key) == null ? fallback : float.Parse(Value(payload, key)!, CultureInfo.InvariantCulture);
    private static float GetPositiveFloat(IReadOnlyDictionary<string, string> payload, string key, float fallback) { float value = GetFloat(payload, key, fallback); if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(key, key + " must be a positive finite number."); return value; }
    private static int? GetNullableInt(IReadOnlyDictionary<string, string> payload, string key) => Value(payload, key) == null ? null : int.Parse(Value(payload, key)!, CultureInfo.InvariantCulture);
    private static uint? GetNullableUInt(IReadOnlyDictionary<string, string> payload, string key) => Value(payload, key) == null ? null : uint.Parse(Value(payload, key)!, CultureInfo.InvariantCulture);
    private static bool GetBool(IReadOnlyDictionary<string, string> payload, string key, bool fallback) => Value(payload, key) == null ? fallback : bool.Parse(Value(payload, key)!);
    private static bool Contains(string value, string token) => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    private static WorkerResponse Error(WorkerRequest request, string code, string message, AppRuntimeState state, string? technicalDetail = null)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal) { ["state"] = state.ToString(), ["backendId"] = request.BackendId ?? string.Empty, ["diagnosticCode"] = code, ["execution"] = "worker" };
        if (!string.IsNullOrWhiteSpace(technicalDetail)) payload["technicalDetail"] = technicalDetail!;
        return new WorkerResponse(WorkerResponseKind.Error, request.RequestId, false, message, payload);
    }

    private sealed class WorkerTensorInput
    {
        public string Name { get; set; } = string.Empty;
        public string ElementType { get; set; } = "float32";
        public long[] Shape { get; set; } = Array.Empty<long>();
        public string? ValuesJson { get; set; }
        public string? ValuesFilePath { get; set; }
        public bool ImageInput { get; set; }
    }

    private sealed class WorkerTensorOutput
    {
        public WorkerTensorOutput(string name, string elementType, long[] shape, object values) { Name = name; ElementType = elementType; Shape = shape; Values = values; }
        public string Name { get; }
        public string ElementType { get; }
        public long[] Shape { get; }
        public object Values { get; }
    }
}
