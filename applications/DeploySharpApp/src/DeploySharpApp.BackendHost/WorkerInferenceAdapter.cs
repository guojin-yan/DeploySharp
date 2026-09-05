using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.LlamaSharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Registry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using DeploySharpApp.Contracts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DeploySharpApp.BackendHost;

internal static class WorkerInferenceAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<WorkerResponse> RunAsync(WorkerRequest request, Action<double>? reportProgress, CancellationToken cancellationToken)
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

        reportProgress?.Invoke(0.45);
        try
        {
            if (Contains(backendId, "llamasharp")) return await RunLlamaAsync(request, modelPath, reportProgress, cancellationToken).ConfigureAwait(false);
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

    private static async Task<WorkerResponse> RunLlamaAsync(WorkerRequest request, string modelPath, Action<double>? reportProgress, CancellationToken cancellationToken)
    {
        string format = Value(request.Payload, "modelFormat") ?? Path.GetExtension(modelPath).TrimStart('.');
        if (!string.Equals(format, "gguf", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "LLamaSharp Worker requires a modelFormat of gguf.", AppRuntimeState.Unsupported, format);
        var artifact = new ModelArtifact(new ModelId(request.ModelId ?? "worker/llamasharp"), "gguf", modelPath, Value(request.Payload, "modelSha256"), LlamaSharpBackendProvider.BackendId);
        var generationOptions = new GenerationOptions(
            GetInt(request.Payload, "maxTokens", 256),
            GetFloat(request.Payload, "temperature", 0.8f),
            GetFloat(request.Payload, "topP", 0.95f),
            GetInt(request.Payload, "topK", 40),
            GetNullableInt(request.Payload, "seed"),
            GetStops(request.Payload),
            TimeSpan.FromMilliseconds(GetDouble(request.Payload, "timeoutMs", 120000)));
        string prompt = Value(request.Payload, "prompt") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt)) return Error(request, "DSAPP-WORKER-PROMPT-REQUIRED", "LLamaSharp text generation requires a prompt.", AppRuntimeState.Unsupported);
        string device = Value(request.Payload, "device") ?? "cpu";
        if (!string.Equals(device, "cpu", StringComparison.OrdinalIgnoreCase) && !string.Equals(device, "auto", StringComparison.OrdinalIgnoreCase))
            return Error(request, "DSAPP-WORKER-LLAMA-DEVICE-UNAVAILABLE", "This Worker packages the LLamaSharp CPU provider; GPU device requests are unavailable until a matching provider is installed.", AppRuntimeState.Unavailable, device);

        using var registry = new LanguageModelRegistry();
        registry.UseLlamaSharp(ParseLlamaOptions(request.Payload));
        using ILanguageModelSession session = registry.CreateSession(artifact, new LanguageModelRequest(LanguageModelCapabilities.TextGeneration, LlamaSharpBackendProvider.BackendId, device));
        reportProgress?.Invoke(0.65);
        GenerationResult result = await session.GenerateAsync(new TextGenerationRequest(prompt, generationOptions), cancellationToken).ConfigureAwait(false);
        reportProgress?.Invoke(0.95);
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["output"] = result.Text,
            ["finishReason"] = result.FinishReason.ToString(),
            ["promptTokens"] = result.Usage.PromptTokens.ToString(CultureInfo.InvariantCulture),
            ["generatedTokens"] = result.Usage.GeneratedTokens.ToString(CultureInfo.InvariantCulture),
            ["backendId"] = "llamasharp",
            ["execution"] = "worker"
        };
        return new WorkerResponse(WorkerResponseKind.Result, request.RequestId, true, "LLamaSharp GGUF generation completed in the Worker.", payload);
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
        if (provider is TensorRtBackendProvider && !string.Equals(format, "tensorrt-engine", StringComparison.OrdinalIgnoreCase)) return Error(request, "DSAPP-WORKER-MODEL-FORMAT-INVALID", "TensorRT Worker requires a modelFormat of tensorrt-engine; ONNX-to-engine build is not implicit.", AppRuntimeState.Unsupported, format);
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

    private static OpenVinoOptions ParseOpenVinoOptions(IReadOnlyDictionary<string, string> payload) => new OpenVinoOptions(Value(payload, "device") ?? "CPU", ParseEnum(Value(payload, "performanceHint"), OpenVinoPerformanceHint.Default), GetNullableInt(payload, "streams"), GetNullableInt(payload, "inferenceThreads"), Value(payload, "cacheDirectory"), GetBool(payload, "enableProfiling", false), GetNullableInt(payload, "requestCount"), null, GetBool(payload, "allowDynamicShapes", true));

    private static TensorRtBackendOptions ParseTensorRtOptions(IReadOnlyDictionary<string, string> payload) => new TensorRtBackendOptions(ParseEnum(Value(payload, "apiVersion"), TensorRtApiVersion.TensorRt10), GetInt(payload, "optimizationProfile", 0), GetLong(payload, "maximumEngineBytes", int.MaxValue), Value(payload, "cudaTargetArchitecture"), GetBool(payload, "cacheImmutableHostInputsOnDevice", false));

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
