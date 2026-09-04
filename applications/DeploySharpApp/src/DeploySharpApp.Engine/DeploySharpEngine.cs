using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DeploySharpApp.Contracts;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using CoreSessionOptions = JYPPX.DeploySharp.Models.SessionOptions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DeploySharpApp.Engine;

public sealed class DeploySharpEngine : IDeploySharpEngine
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IOnnxRuntimeAvailabilityProbe _runtimeProbe;

    public DeploySharpEngine(IOnnxRuntimeAvailabilityProbe? runtimeProbe = null)
    {
        _runtimeProbe = runtimeProbe ?? new OnnxRuntimeAvailabilityProbe();
    }

    public async Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        progress?.Report(0);

        ModelRunResult? boundaryFailure = ValidateBoundary(request);
        if (boundaryFailure != null) return boundaryFailure;

        string modelPath;
        try
        {
            modelPath = Path.GetFullPath(request.ModelPath!);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
        {
            return Failure(AppErrorCode.ModelUnavailable, "The ONNX model path is invalid.", request, "DSAPP-MODEL-PATH-INVALID", exception.Message);
        }

        if (!File.Exists(modelPath))
        {
            return Failure(AppErrorCode.ModelUnavailable, "The ONNX model file does not exist.", request, "DSAPP-MODEL-NOT-FOUND", modelPath);
        }

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancelAfter(request.Timeout);
        BackendRuntimeStatus? runtimeStatus = null;
        var preprocess = Stopwatch.StartNew();
        try
        {
            InferenceInputs inputs = await CreateInputsAsync(request, operation.Token).ConfigureAwait(false);
            OnnxRuntimeOptions backendOptions = CreateBackendOptions(request.BackendOptions);
            CoreSessionOptions sessionOptions = CreateSessionOptions(request.BackendOptions);
            progress?.Report(0.1);

            runtimeStatus = await _runtimeProbe.ProbeAsync(operation.Token).ConfigureAwait(false);
            if (runtimeStatus.State != AppRuntimeState.Available)
            {
                AppErrorCode errorCode = runtimeStatus.State == AppRuntimeState.MissingNative
                    ? AppErrorCode.NativeDependencyMissing
                    : AppErrorCode.BackendUnavailable;
                return new ModelRunResult(false, errorCode, runtimeStatus.Message, diagnostics: runtimeStatus.Diagnostics, runMode: ModelRunMode.RealOnnxRuntime, runtimeStatus: runtimeStatus);
            }
            progress?.Report(0.2);

            var artifact = new ModelArtifact(
                new ModelId(request.ModelId.ToLowerInvariant()),
                "onnx",
                modelPath,
                request.ModelSha256,
                OnnxRuntimeBackendProvider.BackendId);
            var backendRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");

            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime(backendOptions);
            using IInferenceSession session = registry.CreateSession(artifact, backendRequest, sessionOptions);
            preprocess.Stop();
            progress?.Report(0.35);

            var inference = Stopwatch.StartNew();
            InferenceOutputs outputs = await session.RunAsync(inputs, operation.Token).ConfigureAwait(false);
            inference.Stop();
            progress?.Report(0.9);

            var postprocess = Stopwatch.StartNew();
            string output = SerializeOutputs(request, outputs);
            postprocess.Stop();
            progress?.Report(1);

            BackendRuntimeStatus verified = Verified(runtimeStatus);
            return new ModelRunResult(
                true,
                AppErrorCode.None,
                "Real ONNX Runtime CPU inference completed.",
                output,
                preprocess.Elapsed.TotalMilliseconds,
                inference.Elapsed.TotalMilliseconds,
                postprocess.Elapsed.TotalMilliseconds,
                diagnostics: verified.Diagnostics,
                runMode: ModelRunMode.RealOnnxRuntime,
                runtimeStatus: verified);
        }
        catch (OperationCanceledException exception)
        {
            bool timedOut = !cancellationToken.IsCancellationRequested;
            return Failure(timedOut ? AppErrorCode.TimedOut : AppErrorCode.Cancelled, timedOut ? "ONNX Runtime inference timed out." : "ONNX Runtime inference was cancelled.", request, timedOut ? "DSAPP-ORT-TIMED-OUT" : "DSAPP-ORT-CANCELLED", exception.Message, runtimeStatus);
        }
        catch (DeploySharpException exception)
        {
            return MapDeploySharpFailure(exception, request, cancellationToken, runtimeStatus);
        }
        catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is JsonException || exception is IOException || exception is UnauthorizedAccessException || exception is OverflowException)
        {
            return Failure(AppErrorCode.InvalidRequest, "The named tensor input or backend options are invalid.", request, "DSAPP-INPUT-INVALID", exception.Message, runtimeStatus);
        }
        catch (Exception exception)
        {
            return Failure(AppErrorCode.Unknown, "ONNX Runtime inference failed unexpectedly.", request, "DSAPP-ORT-UNEXPECTED", exception.ToString(), runtimeStatus);
        }
    }

    private static ModelRunResult? ValidateBoundary(ModelRunRequest request)
    {
        if (!IsOnnxRuntime(request.BackendId))
        {
            bool worker = request.BackendId.IndexOf("tensorrt", StringComparison.OrdinalIgnoreCase) >= 0
                || request.BackendId.IndexOf("llamasharp", StringComparison.OrdinalIgnoreCase) >= 0;
            return Failure(
                worker ? AppErrorCode.WorkerRequired : AppErrorCode.BackendUnavailable,
                worker ? "The selected backend requires a BackendHost Worker." : "The in-process Engine currently supports only ONNX Runtime CPU.",
                request,
                worker ? "DSAPP-WORKER-REQUIRED" : "DSAPP-BACKEND-UNAVAILABLE",
                request.BackendId,
                runMode: worker ? ModelRunMode.Worker : ModelRunMode.RealOnnxRuntime);
        }

        if (string.Equals(request.Device, "cuda", StringComparison.OrdinalIgnoreCase))
        {
            var status = new BackendRuntimeStatus(
                BackendIds.ApplicationOnnxRuntime,
                AppRuntimeState.Unavailable,
                "ONNX Runtime CUDA is not enabled by the in-process Engine and no CPU fallback was attempted.",
                devices: new[] { "cuda" },
                missingItems: new[] { "onnxruntime-cuda-provider", "cuda-runtime", "cudnn-runtime" },
                suggestedAction: "Configure a dedicated CUDA-capable Worker with matching ONNX Runtime, CUDA and cuDNN versions.",
                diagnostics: new[] { Diagnostic("DSAPP-ORT-CUDA-UNAVAILABLE", DiagnosticSeverity.Warning, "CUDA was requested explicitly; the CPU provider was not selected.", request) });
            return new ModelRunResult(false, AppErrorCode.BackendUnavailable, status.Message, diagnostics: status.Diagnostics, runMode: ModelRunMode.RealOnnxRuntime, runtimeStatus: status);
        }

        if (!string.Equals(request.Device, "cpu", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(AppErrorCode.InvalidRequest, "The ONNX Runtime Engine supports the 'cpu' device only.", request, "DSAPP-DEVICE-INVALID", request.Device);
        }

        if (string.IsNullOrWhiteSpace(request.ModelPath))
        {
            return Failure(AppErrorCode.ModelUnavailable, "A local ONNX model path is required for real inference.", request, "DSAPP-MODEL-PATH-REQUIRED", "modelPath is empty");
        }

        string format;
        try { format = request.ModelFormat ?? Path.GetExtension(request.ModelPath).TrimStart('.'); }
        catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
        {
            return Failure(AppErrorCode.ModelUnavailable, "The ONNX model path is invalid.", request, "DSAPP-MODEL-PATH-INVALID", exception.Message);
        }
        if (!string.Equals(format, "onnx", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(AppErrorCode.InvalidRequest, "The ONNX Runtime Engine accepts only models explicitly identified as ONNX.", request, "DSAPP-MODEL-FORMAT-INVALID", string.IsNullOrWhiteSpace(format) ? "missing" : format);
        }

        if (!string.Equals(request.OutputFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(AppErrorCode.InvalidRequest, "Only JSON tensor output is currently supported.", request, "DSAPP-OUTPUT-FORMAT-INVALID", request.OutputFormat);
        }

        return null;
    }

    private static async Task<InferenceInputs> CreateInputsAsync(ModelRunRequest request, CancellationToken cancellationToken)
    {
        if (request.TensorInputs.Count == 0) throw new ArgumentException("At least one named tensor input is required.", nameof(request));
        var tensors = new List<NamedTensor>(request.TensorInputs.Count);
        foreach (ModelTensorInput input in request.TensorInputs)
        {
            var shape = new TensorShape(input.Shape);
            ITensor tensor;
            if (input.ImageInput)
            {
                if (string.IsNullOrWhiteSpace(request.InputPath)) throw new ArgumentException("An image input path is required for an image tensor.", nameof(request));
                tensor = await CreateImageTensorAsync(request.InputPath!, input, shape, request.Options, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                string json = input.ValuesJson ?? File.ReadAllText(Path.GetFullPath(input.ValuesFilePath!));
                tensor = input.ElementType switch
                {
                    "bool" or "boolean" => Tensor(shape, Deserialize<bool>(json)),
                    "int8" => Tensor(shape, Deserialize<sbyte>(json)),
                    "uint8" => Tensor(shape, Deserialize<byte>(json)),
                    "int16" => Tensor(shape, Deserialize<short>(json)),
                    "uint16" => Tensor(shape, Deserialize<ushort>(json)),
                    "int32" => Tensor(shape, Deserialize<int>(json)),
                    "uint32" => Tensor(shape, Deserialize<uint>(json)),
                    "int64" => Tensor(shape, Deserialize<long>(json)),
                    "uint64" => Tensor(shape, Deserialize<ulong>(json)),
                    "float32" or "float" => Tensor(shape, Deserialize<float>(json)),
                    "float64" or "double" => Tensor(shape, Deserialize<double>(json)),
                    _ => throw new NotSupportedException("Unsupported tensor element type: " + input.ElementType)
                };
            }
            tensors.Add(new NamedTensor(input.Name, tensor));
        }
        return new InferenceInputs(tensors);
    }

    private static async Task<ITensor> CreateImageTensorAsync(string imagePath, ModelTensorInput input, TensorShape shape, IReadOnlyDictionary<string, string> options, CancellationToken cancellationToken)
    {
        if (!string.Equals(input.ElementType, "float32", StringComparison.OrdinalIgnoreCase) && !string.Equals(input.ElementType, "float", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Image tensor inputs must use float32.");
        if (shape.Rank != 4 || shape[0] != 1 || (shape[1] != 1 && shape[1] != 3 && shape[1] != 4) || shape[2] <= 0 || shape[3] <= 0)
            throw new ArgumentException("Image tensor shape must be [1,1|3|4,height,width] with positive spatial dimensions.", nameof(input));

        int channels = checked((int)shape[1]);
        int height = checked((int)shape[2]);
        int width = checked((int)shape[3]);
        string resizeMode = (Get(options, "imageResizeMode") ?? "stretch").ToLowerInvariant();
        ResizeMode imageSharpResizeMode = resizeMode switch
        {
            "stretch" => ResizeMode.Stretch,
            "pad" => ResizeMode.Pad,
            "crop" => ResizeMode.Crop,
            _ => throw new ArgumentException("imageResizeMode must be 'stretch', 'pad', or 'crop'.")
        };
        string colorOrder = (Get(options, "imageColorOrder") ?? "rgb").ToLowerInvariant();
        if (colorOrder != "rgb" && colorOrder != "bgr") throw new ArgumentException("imageColorOrder must be 'rgb' or 'bgr'.");
        float scale = GetFloat(options, "imageScale", 1f / 255f);
        float meanRed = GetFloat(options, "imageMeanR", 0f);
        float meanGreen = GetFloat(options, "imageMeanG", 0f);
        float meanBlue = GetFloat(options, "imageMeanB", 0f);
        float stdRed = GetPositiveFloat(options, "imageStdR", 1f);
        float stdGreen = GetPositiveFloat(options, "imageStdG", 1f);
        float stdBlue = GetPositiveFloat(options, "imageStdB", 1f);
        int padValue = GetInt(options, "imagePadValue", 0);
        if (padValue is < 0 or > 255) throw new ArgumentOutOfRangeException("imagePadValue", "imagePadValue must be between 0 and 255.");
        using Image<Rgb24> image = await Image.LoadAsync<Rgb24>(imagePath, cancellationToken).ConfigureAwait(false);
        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = imageSharpResizeMode,
            PadColor = Color.FromRgb((byte)padValue, (byte)padValue, (byte)padValue)
        }));
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
        return Tensor(new TensorShape(1, channels, height, width), values);
    }

    private static T[] Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T[]>(json) ?? throw new FormatException("Tensor values JSON must be an array.");
    }

    private static Tensor<T> Tensor<T>(TensorShape shape, T[] values)
    {
        return new Tensor<T>(shape, values, TensorBufferOwnership.Transfer);
    }

    private static OnnxRuntimeOptions CreateBackendOptions(IReadOnlyDictionary<string, string> values)
    {
        ValidateKnownOptions(values);
        string provider = Get(values, "executionProvider") ?? "cpu";
        if (!string.Equals(provider, "cpu", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("executionProvider must be 'cpu' for the in-process Engine.");
        return new OnnxRuntimeOptions(
            intraOpThreads: GetInt(values, "intraOpThreads", 0),
            interOpThreads: GetInt(values, "interOpThreads", 0),
            graphOptimization: GetEnum(values, "graphOptimization", OnnxRuntimeGraphOptimization.All),
            executionMode: GetEnum(values, "executionMode", OnnxRuntimeExecutionMode.Sequential),
            enableMemoryPattern: GetBool(values, "enableMemoryPattern", true),
            enableCpuMemoryArena: GetBool(values, "enableCpuMemoryArena", true),
            logSeverity: GetEnum(values, "logSeverity", OnnxRuntimeLogSeverity.Warning),
            logId: Get(values, "logId"),
            profilingOutputPathPrefix: Get(values, "profilingOutputPathPrefix"),
            executionProvider: OnnxRuntimeExecutionProvider.Cpu);
    }

    private static CoreSessionOptions CreateSessionOptions(IReadOnlyDictionary<string, string> values)
    {
        return new CoreSessionOptions(GetInt(values, "maxConcurrency", 1), GetBool(values, "enableProfiling", false));
    }

    private static void ValidateKnownOptions(IReadOnlyDictionary<string, string> values)
    {
        string[] known = { "executionProvider", "intraOpThreads", "interOpThreads", "graphOptimization", "executionMode", "enableMemoryPattern", "enableCpuMemoryArena", "logSeverity", "logId", "profilingOutputPathPrefix", "maxConcurrency", "enableProfiling", "imageResizeMode", "imageColorOrder", "imageScale", "imageMeanR", "imageMeanG", "imageMeanB", "imageStdR", "imageStdG", "imageStdB", "imagePadValue" };
        foreach (string key in values.Keys)
        {
            if (!known.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Unsupported ONNX Runtime option: " + key);
        }
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key)
    {
        foreach (KeyValuePair<string, string> pair in values) if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return null;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        string? value = Get(values, key);
        return value == null ? fallback : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool fallback)
    {
        string? value = Get(values, key);
        return value == null ? fallback : bool.Parse(value);
    }

    private static float GetFloat(IReadOnlyDictionary<string, string> values, string key, float fallback)
    {
        string? value = Get(values, key);
        float result = value == null ? fallback : float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!float.IsFinite(result)) throw new ArgumentOutOfRangeException(key, key + " must be finite.");
        return result;
    }

    private static float GetPositiveFloat(IReadOnlyDictionary<string, string> values, string key, float fallback)
    {
        float result = GetFloat(values, key, fallback);
        if (result <= 0) throw new ArgumentOutOfRangeException(key, key + " must be greater than zero.");
        return result;
    }

    private static T GetEnum<T>(IReadOnlyDictionary<string, string> values, string key, T fallback) where T : struct, Enum
    {
        string? value = Get(values, key);
        if (value == null) return fallback;
        if (!Enum.TryParse(value, true, out T result) || !Enum.IsDefined(result)) throw new ArgumentException("Invalid " + key + " option: " + value);
        return result;
    }

    private static string SerializeOutputs(ModelRunRequest request, InferenceOutputs outputs)
    {
        var values = outputs.Select(item => new TensorOutput(item.Name, item.Tensor.ElementType.ToString(), item.Tensor.Shape.ToArray(), item.Tensor.Buffer)).ToList();
        return JsonSerializer.Serialize(new OutputDocument(request.ModelId, BackendIds.CoreOnnxRuntime, "cpu", values), OutputJsonOptions);
    }

    private static ModelRunResult MapDeploySharpFailure(DeploySharpException exception, ModelRunRequest request, CancellationToken callerToken, BackendRuntimeStatus? probedStatus)
    {
        if (string.Equals(exception.ErrorCode, OnnxRuntimeErrorCodes.Cancelled, StringComparison.Ordinal))
        {
            bool timedOut = !callerToken.IsCancellationRequested;
            return Failure(timedOut ? AppErrorCode.TimedOut : AppErrorCode.Cancelled, timedOut ? "ONNX Runtime inference timed out." : "ONNX Runtime inference was cancelled.", request, timedOut ? "DSAPP-ORT-TIMED-OUT" : "DSAPP-ORT-CANCELLED", exception.TechnicalDetails ?? exception.Message, probedStatus);
        }

        if (string.Equals(exception.ErrorCode, DeploySharpErrorCodes.NativeRuntimeUnavailable, StringComparison.Ordinal))
        {
            string? loadedPath = ExtractNativePath(exception.TechnicalDetails) ?? probedStatus?.LoadedPath;
            var details = FailureDetails(exception, probedStatus);
            var diagnostic = new RuntimeDiagnostic("DSAPP-ORT-NATIVE-MISSING", DiagnosticSeverity.Error, exception.Message, BackendIds.ApplicationOnnxRuntime, request.ModelId, details);
            var status = new BackendRuntimeStatus(
                BackendIds.ApplicationOnnxRuntime,
                AppRuntimeState.MissingNative,
                exception.Message,
                loadedPath,
                rid: probedStatus?.Rid,
                processArchitecture: probedStatus?.ProcessArchitecture,
                devices: new[] { "cpu" },
                missingItems: new[] { "onnxruntime-native-1.28.0" },
                suggestedAction: "Install the official Microsoft.ML.OnnxRuntime 1.28.0 native assets for this RID and remove incompatible machine-wide copies.",
                details: details,
                diagnostics: new[] { diagnostic });
            return new ModelRunResult(false, AppErrorCode.NativeDependencyMissing, exception.Message, diagnostics: status.Diagnostics, runMode: ModelRunMode.RealOnnxRuntime, runtimeStatus: status);
        }

        AppErrorCode code;
        if (string.Equals(exception.ErrorCode, DeploySharpErrorCodes.ModelArtifactInvalid, StringComparison.Ordinal)
            || string.Equals(exception.ErrorCode, OnnxRuntimeErrorCodes.ModelLoadFailed, StringComparison.Ordinal)) code = AppErrorCode.ModelUnavailable;
        else if (string.Equals(exception.ErrorCode, OnnxRuntimeErrorCodes.ConfigurationInvalid, StringComparison.Ordinal)
            || string.Equals(exception.ErrorCode, OnnxRuntimeErrorCodes.TensorInvalid, StringComparison.Ordinal)
            || string.Equals(exception.ErrorCode, OnnxRuntimeErrorCodes.ElementTypeUnsupported, StringComparison.Ordinal)) code = AppErrorCode.InvalidRequest;
        else if (string.Equals(exception.ErrorCode, OnnxRuntimeErrorCodes.ExecutionProviderUnavailable, StringComparison.Ordinal)
            || string.Equals(exception.ErrorCode, DeploySharpErrorCodes.BackendNotCompatible, StringComparison.Ordinal)
            || string.Equals(exception.ErrorCode, DeploySharpErrorCodes.BackendNotFound, StringComparison.Ordinal)) code = AppErrorCode.BackendUnavailable;
        else code = AppErrorCode.Unknown;

        return Failure(code, exception.Message, request, exception.ErrorCode, exception.TechnicalDetails ?? exception.Message, probedStatus);
    }

    private static BackendRuntimeStatus Verified(BackendRuntimeStatus status)
    {
        return new BackendRuntimeStatus(
            status.BackendId,
            AppRuntimeState.Available,
            "ONNX Runtime 1.28.0 was loaded through BackendRegistry and passed native ABI/version preflight.",
            status.LoadedPath,
            "1.28.0",
            status.ApiLine,
            status.Rid,
            status.ProcessArchitecture,
            new[] { "cpu" },
            details: status.Details,
            diagnostics: status.Diagnostics);
    }

    private static ModelRunResult Failure(AppErrorCode code, string message, ModelRunRequest request, string diagnosticCode, string technicalDetail, BackendRuntimeStatus? status = null, ModelRunMode runMode = ModelRunMode.RealOnnxRuntime)
    {
        var details = new Dictionary<string, string> { ["technicalDetail"] = string.IsNullOrWhiteSpace(technicalDetail) ? "not supplied" : technicalDetail };
        var diagnostic = Diagnostic(diagnosticCode, code == AppErrorCode.InvalidRequest ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error, message, request, details);
        return new ModelRunResult(false, code, message, diagnostics: new[] { diagnostic }, runMode: runMode, runtimeStatus: status);
    }

    private static RuntimeDiagnostic Diagnostic(string code, DiagnosticSeverity severity, string message, ModelRunRequest request, IReadOnlyDictionary<string, string>? details = null)
    {
        return new RuntimeDiagnostic(code, severity, message, BackendIds.ApplicationOnnxRuntime, request.ModelId, details);
    }

    private static Dictionary<string, string> FailureDetails(DeploySharpException exception, BackendRuntimeStatus? probedStatus)
    {
        var details = probedStatus == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(probedStatus.Details, StringComparer.Ordinal);
        details["sourceErrorCode"] = exception.ErrorCode;
        if (!string.IsNullOrWhiteSpace(exception.TechnicalDetails)) details["technicalDetails"] = exception.TechnicalDetails!;
        if (exception is OnnxRuntimeBackendException onnx)
        {
            if (!string.IsNullOrWhiteSpace(onnx.Operation)) details["operation"] = onnx.Operation!;
            if (!string.IsNullOrWhiteSpace(onnx.TensorName)) details["tensorName"] = onnx.TensorName!;
        }
        return details;
    }

    private static string? ExtractNativePath(string? technicalDetails)
    {
        if (string.IsNullOrWhiteSpace(technicalDetails)) return null;
        const string marker = "nativePath=";
        int start = technicalDetails.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += marker.Length;
        int end = technicalDetails.IndexOf(';', start);
        string value = (end < 0 ? technicalDetails.Substring(start) : technicalDetails.Substring(start, end - start)).Trim();
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "default-search", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static bool IsOnnxRuntime(string backendId)
    {
        return string.Equals(backendId, BackendIds.ApplicationOnnxRuntime, StringComparison.OrdinalIgnoreCase)
            || string.Equals(backendId, BackendIds.CoreOnnxRuntime, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OutputDocument
    {
        public OutputDocument(string modelId, string backendId, string device, IReadOnlyList<TensorOutput> outputs)
        {
            ModelId = modelId; BackendId = backendId; Device = device; Outputs = outputs;
        }
        public string ModelId { get; }
        public string BackendId { get; }
        public string Device { get; }
        public IReadOnlyList<TensorOutput> Outputs { get; }
    }

    private sealed class TensorOutput
    {
        public TensorOutput(string name, string elementType, long[] shape, object values)
        {
            Name = name; ElementType = elementType; Shape = shape; Values = values;
        }
        public string Name { get; }
        public string ElementType { get; }
        public long[] Shape { get; }
        public object Values { get; }
    }
}
