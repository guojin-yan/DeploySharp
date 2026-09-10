using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeploySharpApp.Contracts;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace DeploySharpApp.BackendHost;

internal sealed class TensorRtPreparedEngine
{
    public TensorRtPreparedEngine(bool succeeded, string code, string message, string? enginePath, string? identityPath, string? engineSha256, bool built, IReadOnlyDictionary<string, string> details)
    {
        Succeeded = succeeded;
        Code = code;
        Message = message;
        EnginePath = enginePath;
        IdentityPath = identityPath;
        EngineSha256 = engineSha256;
        Built = built;
        Details = details;
    }

    public bool Succeeded { get; }
    public string Code { get; }
    public string Message { get; }
    public string? EnginePath { get; }
    public string? IdentityPath { get; }
    public string? EngineSha256 { get; }
    public bool Built { get; }
    public IReadOnlyDictionary<string, string> Details { get; }
}

internal static class TensorRtOnnxEngineAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static TensorRtPreparedEngine ResolveOrBuild(WorkerRequest request, string onnxPath, Action<double>? progress, CancellationToken cancellationToken)
    {
        TensorRtValidationResult runtime = TensorRtEngineIdentityValidator.ValidateRuntime(request.Payload);
        if (!runtime.Succeeded) return Failure(runtime);

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Invoke(.48);
        TensorRtOnnxEngineBuildOptions options = ParseOptions(request.Payload);
        string tensorRtVersion = Required(runtime.Details, "runtime.tensorRtVersion");
        string expectedApiLine = ((int)options.ApiVersion).ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(tensorRtVersion.Split('.')[0], expectedApiLine, StringComparison.Ordinal))
        {
            var details = new Dictionary<string, string>(runtime.Details, StringComparer.Ordinal)
            {
                ["requestedApiLine"] = expectedApiLine,
                ["detectedTensorRtVersion"] = tensorRtVersion
            };
            return new TensorRtPreparedEngine(false, "DSAPP-TENSORRT-API-LINE-MISMATCH", "The selected TensorRT API line does not match the detected TensorRT runtime major version.", null, null, null, false, details);
        }
        string onnxSha256 = HashFile(onnxPath);
        string? declaredSha256 = Value(request.Payload, "modelSha256");
        if (!string.IsNullOrWhiteSpace(declaredSha256) && !string.Equals(declaredSha256, onnxSha256, StringComparison.OrdinalIgnoreCase))
        {
            var details = new Dictionary<string, string>(runtime.Details, StringComparer.Ordinal)
            {
                ["sourceOnnxPath"] = onnxPath,
                ["expectedOnnxSha256"] = declaredSha256!,
                ["actualOnnxSha256"] = onnxSha256
            };
            return new TensorRtPreparedEngine(false, "DSAPP-TENSORRT-ONNX-SHA256-MISMATCH", "The ONNX source SHA256 does not match the selected model manifest.", null, null, null, false, details);
        }

        string buildInputsSha256 = TensorRtOnnxEngineBuilder.GetBuildInputsSha256(onnxSha256, options);
        string cacheKey = HashText(string.Join("|", new[]
        {
            buildInputsSha256,
            Required(runtime.Details, "runtime.tensorRtIdentity"),
            Required(runtime.Details, "runtime.cudaIdentity"),
            Required(runtime.Details, "runtime.cudnnIdentity"),
            Required(runtime.Details, "runtime.driverIdentity"),
            Required(runtime.Details, "runtime.bridgeIdentity"),
            Required(runtime.Details, "runtime.driverVersion"),
            Required(runtime.Details, "gpuIdentity"),
            Required(runtime.Details, "runtime.gpuCompatibility"),
            Required(runtime.Details, "runtimeIdentifier"),
            Required(runtime.Details, "processArchitecture")
        }));
        string? requestedOutput = Value(request.Payload, "tensorRtEngineOutputPath");
        bool explicitOutput = !string.IsNullOrWhiteSpace(requestedOutput);
        string enginePath = explicitOutput
            ? Path.GetFullPath(requestedOutput!)
            : Path.Combine(ResolveCacheRoot(request.Payload), onnxSha256[..16], cacheKey + ".engine");
        string extension = Path.GetExtension(enginePath);
        if (!string.Equals(extension, ".engine", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".plan", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The TensorRT engine output path must end with .engine or .plan.", "tensorRtEngineOutputPath");
        string identityPath = Path.GetFullPath(Value(request.Payload, "engineIdentityPath") ?? enginePath + ".identity.json");
        Directory.CreateDirectory(Path.GetDirectoryName(enginePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(identityPath)!);

        var validationPayload = new Dictionary<string, string>(request.Payload, StringComparer.Ordinal)
        {
            ["engineIdentityPath"] = identityPath,
            ["sourceOnnxPath"] = onnxPath
        };
        bool forceRebuild = BoolValue(request.Payload, "tensorRtForceRebuild", false);
        if (!forceRebuild && File.Exists(enginePath) && File.Exists(identityPath))
        {
            TensorRtValidationResult cached = TensorRtEngineIdentityValidator.Validate(enginePath, validationPayload);
            if (cached.Succeeded)
            {
                var details = new Dictionary<string, string>(cached.Details, StringComparer.Ordinal)
                {
                    ["engineBuildState"] = "cache-hit",
                    ["engineBuildInputsSha256"] = buildInputsSha256,
                    ["engineCacheKey"] = cacheKey
                };
                return new TensorRtPreparedEngine(true, "DSAPP-TENSORRT-ENGINE-CACHE-HIT", "A compatible device-bound TensorRT engine was reused from the local cache.", enginePath, identityPath, HashFile(enginePath), false, details);
            }
            if (explicitOutput)
            {
                var details = new Dictionary<string, string>(cached.Details, StringComparer.Ordinal)
                {
                    ["engineBuildState"] = "explicit-output-conflict",
                    ["suggestedAction"] = "Enable force rebuild or choose another TensorRT engine output path."
                };
                return new TensorRtPreparedEngine(false, "DSAPP-TENSORRT-ENGINE-OUTPUT-CONFLICT", "The selected engine output exists but its identity does not match this ONNX/runtime/GPU build.", null, identityPath, null, false, details);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Invoke(.52);
        var artifact = new ModelArtifact(new ModelId(request.ModelId ?? "worker/tensorrt-onnx"), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId);
        TensorRtOnnxEngineBuildOptions buildOptions = WithOverwrite(options);
        TensorRtOnnxEngineBuildResult built = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, buildOptions, cancellationToken);
        WriteIdentity(identityPath, runtime.Details, built, buildOptions, cacheKey);
        cancellationToken.ThrowIfCancellationRequested();

        TensorRtValidationResult validation = TensorRtEngineIdentityValidator.Validate(enginePath, validationPayload);
        if (!validation.Succeeded) return Failure(validation);
        progress?.Invoke(.62);
        var builtDetails = new Dictionary<string, string>(validation.Details, StringComparer.Ordinal)
        {
            ["engineBuildState"] = "built",
            ["engineBuildInputsSha256"] = built.BuildInputsSha256,
            ["engineCacheKey"] = cacheKey,
            ["engineBytes"] = built.EngineBytes.ToString(CultureInfo.InvariantCulture),
            ["enginePrecision"] = built.Precision.ToString(),
            ["engineApiVersion"] = ((int)built.ApiVersion).ToString(CultureInfo.InvariantCulture),
            ["optimizationProfileCount"] = built.OptimizationProfileCount.ToString(CultureInfo.InvariantCulture)
        };
        return new TensorRtPreparedEngine(true, "DSAPP-TENSORRT-ENGINE-BUILT", "ONNX was converted to a device-bound TensorRT engine and its identity sidecar was verified.", enginePath, identityPath, built.EngineSha256, true, builtDetails);
    }

    private static TensorRtPreparedEngine Failure(TensorRtValidationResult validation)
        => new(false, validation.Code, validation.Message, null, null, null, false, validation.Details);

    private static TensorRtOnnxEngineBuildOptions ParseOptions(IReadOnlyDictionary<string, string> payload)
    {
        TensorRtApiVersion apiVersion = ParseApiVersion(Value(payload, "apiVersion") ?? Value(payload, "tensorRtApiVersion"));
        TensorRtOnnxEnginePrecision precision = ParsePrecision(Value(payload, "tensorRtPrecision"));
        long maximumOnnxBytes = LongValue(payload, "maximumOnnxBytes", int.MaxValue);
        long maximumEngineBytes = LongValue(payload, "maximumEngineBytes", int.MaxValue);
        long workspaceMiB = LongValue(payload, "tensorRtWorkspaceMiB", 1024);
        if (workspaceMiB <= 0 || workspaceMiB > 1048576) throw new ArgumentOutOfRangeException("tensorRtWorkspaceMiB", "TensorRT workspace must be between 1 and 1048576 MiB.");
        int optimizationLevel = IntValue(payload, "tensorRtBuilderOptimizationLevel", -1);
        bool stronglyTyped = BoolValue(payload, "tensorRtStronglyTypedNetwork", false);
        TensorRtOnnxInputProfile[] profiles = ParseProfiles(Value(payload, "tensorRtInputProfilesJson"));
        return new TensorRtOnnxEngineBuildOptions(apiVersion, precision, maximumOnnxBytes, maximumEngineBytes, checked((ulong)workspaceMiB * 1024UL * 1024UL), optimizationLevel, stronglyTyped, overwrite: false, profiles);
    }

    private static TensorRtOnnxEngineBuildOptions WithOverwrite(TensorRtOnnxEngineBuildOptions value)
        => new(value.ApiVersion, value.Precision, value.MaximumOnnxBytes, value.MaximumEngineBytes, value.WorkspaceBytes, value.OptimizationLevel, value.StronglyTypedNetwork, overwrite: true, value.InputProfiles);

    private static TensorRtOnnxInputProfile[] ParseProfiles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<TensorRtOnnxInputProfile>();
        TensorRtInputProfileContract[] contracts = JsonSerializer.Deserialize<TensorRtInputProfileContract[]>(json, JsonOptions) ?? Array.Empty<TensorRtInputProfileContract>();
        return contracts.Select(profile => new TensorRtOnnxInputProfile(
            profile.InputName ?? throw new FormatException("Every TensorRT input profile requires inputName."),
            new TensorShape(profile.Minimum ?? throw new FormatException("Every TensorRT input profile requires minimum.")),
            new TensorShape(profile.Optimum ?? throw new FormatException("Every TensorRT input profile requires optimum.")),
            new TensorShape(profile.Maximum ?? throw new FormatException("Every TensorRT input profile requires maximum.")))).ToArray();
    }

    private static void WriteIdentity(string path, IReadOnlyDictionary<string, string> runtime, TensorRtOnnxEngineBuildResult result, TensorRtOnnxEngineBuildOptions options, string cacheKey)
    {
        var document = new
        {
            schema = "deploysharp.app.tensorrt-engine-identity.v1",
            engineSha256 = result.EngineSha256,
            onnxSha256 = result.OnnxSha256,
            engineSerializationVersion = Required(runtime, "runtime.tensorRtVersion"),
            tensorRtVersion = Required(runtime, "runtime.tensorRtVersion"),
            cudaVersion = Required(runtime, "runtime.cudaVersion"),
            cudnnVersion = Required(runtime, "runtime.cudnnVersion"),
            driverVersion = Required(runtime, "runtime.driverVersion"),
            gpuCompatibility = Required(runtime, "runtime.gpuCompatibility"),
            gpuIdentity = Required(runtime, "gpuIdentity"),
            bridgeIdentity = Required(runtime, "runtime.bridgeIdentity"),
            tensorRtIdentity = Required(runtime, "runtime.tensorRtIdentity"),
            cudaIdentity = Required(runtime, "runtime.cudaIdentity"),
            cudnnIdentity = Required(runtime, "runtime.cudnnIdentity"),
            driverIdentity = Required(runtime, "runtime.driverIdentity"),
            result.BuildInputsSha256,
            cacheKey,
            result.OnnxPath,
            result.EnginePath,
            result.OnnxBytes,
            result.EngineBytes,
            apiVersion = (int)result.ApiVersion,
            precision = result.Precision.ToString(),
            options.WorkspaceBytes,
            options.OptimizationLevel,
            options.StronglyTypedNetwork,
            inputProfiles = options.InputProfiles.Select(profile => new { profile.InputName, minimum = profile.Minimum.ToArray(), optimum = profile.Optimum.ToArray(), maximum = profile.Maximum.ToArray() }),
            runtimeIdentifier = Required(runtime, "runtimeIdentifier"),
            processArchitecture = Required(runtime, "processArchitecture"),
            createdUtc = DateTimeOffset.UtcNow
        };
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string ResolveCacheRoot(IReadOnlyDictionary<string, string> payload)
    {
        string? configured = Value(payload, "tensorRtEngineCacheRoot");
        return Path.GetFullPath(configured ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharpApp", "tensorrt-engines"));
    }

    private static TensorRtApiVersion ParseApiVersion(string? value) => value switch
    {
        "8" => TensorRtApiVersion.TensorRt8,
        "11" => TensorRtApiVersion.TensorRt11,
        null or "" or "10" => TensorRtApiVersion.TensorRt10,
        _ => Enum.Parse<TensorRtApiVersion>(value, true)
    };

    private static TensorRtOnnxEnginePrecision ParsePrecision(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "runtime-default" or "runtimedefault" => TensorRtOnnxEnginePrecision.RuntimeDefault,
        "fp32" or "float32" => TensorRtOnnxEnginePrecision.Float32,
        "fp16" or "float16" => TensorRtOnnxEnginePrecision.Float16,
        "int8" or "int8-explicit" or "int8explicitquantization" => TensorRtOnnxEnginePrecision.Int8ExplicitQuantization,
        _ => Enum.Parse<TensorRtOnnxEnginePrecision>(value!, true)
    };

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string HashText(string value)
    {
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException("Missing TensorRT runtime identity field: " + key);
    private static string? Value(IReadOnlyDictionary<string, string> payload, string key) => payload.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    private static int IntValue(IReadOnlyDictionary<string, string> payload, string key, int fallback) => Value(payload, key) is string value ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;
    private static long LongValue(IReadOnlyDictionary<string, string> payload, string key, long fallback) => Value(payload, key) is string value ? long.Parse(value, CultureInfo.InvariantCulture) : fallback;
    private static bool BoolValue(IReadOnlyDictionary<string, string> payload, string key, bool fallback) => Value(payload, key) is string value ? bool.Parse(value) : fallback;

    private sealed class TensorRtInputProfileContract
    {
        public string? InputName { get; set; }
        public long[]? Minimum { get; set; }
        public long[]? Optimum { get; set; }
        public long[]? Maximum { get; set; }
    }
}
