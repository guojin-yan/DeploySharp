using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp.Diagnostics;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.LlamaSharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Extensibility;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace DeploySharpApp.BackendHost;

/// <summary>
/// Creates the main-repository backend descriptors and runs their conservative native preflight inside the Worker.
/// The probe deliberately does not load a native library; a file-system success is therefore reported as
/// <c>Unavailable</c> until the provider ABI smoke test and inference adapter are enabled.
/// </summary>
internal static class BackendRuntimeProbeCatalog
{
    private const string AbiSmokePendingCode = "DSAPP-WORKER-ABI-SMOKE-PENDING";

    public static WorkerProbeResult Probe(string? requestedBackendId, IReadOnlyDictionary<string, string>? smokePayload = null)
    {
        string backendId = requestedBackendId ?? string.Empty;
        try
        {
            if (Contains(backendId, "llamasharp"))
                return Run(backendId, new LlamaSharpPluginFactory().Descriptor, new LlamaSharpRuntimeProbe(new BackendPluginContext(AppContext.BaseDirectory)), LlamaEnvironmentVariables);
            if (Contains(backendId, "opencv"))
            {
                var contract = new OpenCvDnnModelContract(
                    new ModelId("deploysharp-worker-probe-opencv"),
                    new[] { new TensorDescriptor("input", TensorElementType.Float32, new TensorShape(1, 3, 224, 224)) },
                    new[] { new TensorDescriptor("output", TensorElementType.Float32, new TensorShape(1)) });
                var factory = new OpenCvDnnPluginFactory(new OpenCvDnnOptions(contract));
                return Run(backendId, factory.Descriptor, new OpenCvRuntimeProbe(new BackendPluginContext(AppContext.BaseDirectory)), OpenCvEnvironmentVariables, smokePayload);
            }
            if (Contains(backendId, "openvino"))
                return Run(backendId, new OpenVinoPluginFactory().Descriptor, new OpenVinoRuntimeProbe(new BackendPluginContext(AppContext.BaseDirectory)), OpenVinoEnvironmentVariables, smokePayload);
            if (Contains(backendId, "tensorrt"))
                return Run(backendId, new TensorRtPluginFactory().Descriptor, new TensorRtRuntimeProbe(new BackendPluginContext(AppContext.BaseDirectory)), TensorRtEnvironmentVariables, smokePayload);

            return WorkerProbeResult.Failed(backendId, "DSAPP-WORKER-BACKEND-UNKNOWN", "Worker does not have a native probe for the requested backend.");
        }
        catch (Exception exception)
        {
            return WorkerProbeResult.Failed(backendId, "DSAPP-WORKER-PROBE-FAILED", "The backend native probe failed before loading a native library.", exception);
        }
    }

    private static WorkerProbeResult Run(string appBackendId, BackendPluginDescriptor descriptor, IBackendRuntimeProbe probe, IReadOnlyList<string> environmentVariables, IReadOnlyDictionary<string, string>? smokePayload = null)
    {
        descriptor = MakeWorkerCompatible(descriptor);
        BackendRuntimeStatus status = probe.ProbeAsync(descriptor, default).GetAwaiter().GetResult();
        status = NormalizePlatformStatus(descriptor, status);
        status = AugmentPackagedNativeStatus(descriptor, status);
        status = ApplyAbiSmoke(appBackendId, status, smokePayload);
        string missingItems = string.Join(",", status.MissingItems.Select(MapMissingItem));
        string probedPaths = string.Join(";", ProbeRoots(environmentVariables)
            .Concat(status.Details.Where(pair => pair.Key.EndsWith(".root", StringComparison.OrdinalIgnoreCase) || pair.Key.EndsWith(".packaged", StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["backendId"] = appBackendId,
            ["providerBackendId"] = descriptor.Backend.Id.ToString(),
            ["pluginId"] = descriptor.PluginId,
            ["execution"] = "worker",
            ["preflightState"] = status.State.ToString(),
            ["state"] = status.State == BackendRuntimeState.MissingNative ? "MissingNative" : status.State == BackendRuntimeState.Available ? "Unavailable" : status.State.ToString(),
            ["diagnosticCode"] = status.State == BackendRuntimeState.MissingNative ? "DSAPP-WORKER-NATIVE-MISSING" : status.State == BackendRuntimeState.Available ? (status.Details.TryGetValue("abiSmokeState", out string? smokeState) && smokeState == "passed" ? "DSAPP-WORKER-ABI-SMOKE-PASSED" : AbiSmokePendingCode) : status.Details.TryGetValue("abiSmokeCode", out string? smokeCode) ? smokeCode : "DSAPP-WORKER-NATIVE-PREFLIGHT",
            ["missingItems"] = missingItems,
            ["preflightMissingItems"] = string.Join(",", status.MissingItems),
            ["probedPaths"] = probedPaths,
            ["loadedPath"] = status.LoadedPath ?? string.Empty,
            ["runtimeIdentifier"] = status.RuntimeIdentifier ?? string.Empty,
            ["processArchitecture"] = status.ProcessArchitecture ?? string.Empty,
            ["suggestedAction"] = status.SuggestedAction ?? (status.State == BackendRuntimeState.Available ? "Run the Worker ABI smoke test before enabling inference." : "Install the matching runtime and select its application-owned root, then probe again.")
        };

        foreach (KeyValuePair<string, string> detail in status.Details)
            payload["probe." + detail.Key] = detail.Value;

        bool success = false;
        string message = status.State == BackendRuntimeState.MissingNative
            ? "The Worker could not find every declared native runtime requirement."
            : status.State == BackendRuntimeState.Available
                ? "Native files were found, but ABI smoke test and inference adapter execution are still required."
                : "The Worker native preflight did not report an executable runtime.";
        return new WorkerProbeResult(success, message, payload);
    }

    private static BackendRuntimeStatus ApplyAbiSmoke(string backendId, BackendRuntimeStatus status, IReadOnlyDictionary<string, string>? payload)
    {
        var details = new Dictionary<string, string>(status.Details, StringComparer.Ordinal);
        string? modelPath = payload != null && payload.TryGetValue("smokeModelPath", out string? supplied) ? supplied : null;
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            details["abiSmokeState"] = "required";
            details["abiSmokeCode"] = AbiSmokePendingCode;
            return Rebuild(status, status.State, details, status.SuggestedAction ?? "Provide a verified smoke model to exercise the native ABI before enabling this backend.");
        }
        if (status.State != BackendRuntimeState.Available)
        {
            details["abiSmokeState"] = "blocked-by-preflight";
            details["abiSmokeCode"] = "DSAPP-WORKER-ABI-SMOKE-PREFLIGHT-FAILED";
            return Rebuild(status, status.State, details, status.SuggestedAction);
        }
        try
        {
            string path = Path.GetFullPath(modelPath);
            if (!File.Exists(path)) throw new FileNotFoundException("The ABI smoke model does not exist.", path);
            if (Contains(backendId, "opencv")) SmokeOpenCv(path, payload!);
            else if (Contains(backendId, "openvino")) SmokeOpenVino(path);
            else if (Contains(backendId, "tensorrt")) SmokeTensorRt(path);
            else throw new NotSupportedException("ABI smoke is not defined for this backend.");
            details["abiSmokeState"] = "passed";
            details["abiSmokeCode"] = "DSAPP-WORKER-ABI-SMOKE-PASSED";
            details["abiSmokePath"] = path;
            return Rebuild(status, BackendRuntimeState.Available, details, "Native ABI smoke passed; inference still validates the requested model contract.");
        }
        catch (DllNotFoundException exception)
        {
            return SmokeFailure(status, details, "DSAPP-WORKER-ABI-SMOKE-MISSING-NATIVE", "Missing native runtime while loading the ABI smoke model.", exception);
        }
        catch (BadImageFormatException exception)
        {
            return SmokeFailure(status, details, "DSAPP-WORKER-ABI-SMOKE-ABI-MISMATCH", "Native ABI or process architecture mismatch while loading the smoke model.", exception);
        }
        catch (Exception exception)
        {
            return SmokeFailure(status, details, "DSAPP-WORKER-ABI-SMOKE-FAILED", "Native ABI smoke failed; the backend remains unavailable.", exception);
        }
    }

    private static BackendRuntimeStatus SmokeFailure(BackendRuntimeStatus status, Dictionary<string, string> details, string code, string message, Exception exception)
    {
        details["abiSmokeState"] = "failed";
        details["abiSmokeCode"] = code;
        details["abiSmokeError"] = exception.GetType().FullName + ": " + exception.Message;
        var diagnostic = new RuntimeDiagnostic(code, DiagnosticSeverity.Warning, message, details: details);
        return Rebuild(status, BackendRuntimeState.Unavailable, details, "Install matching native assets and rerun the ABI smoke test with a model contract valid for this backend.", status.Diagnostics.Concat(new[] { diagnostic }));
    }

    private static BackendRuntimeStatus Rebuild(BackendRuntimeStatus status, BackendRuntimeState state, IReadOnlyDictionary<string, string> details, string? suggestedAction, IEnumerable<RuntimeDiagnostic>? diagnostics = null)
        => new BackendRuntimeStatus(state, status.LoadedPath, status.Version, status.AbiApiLine, status.RuntimeIdentifier, status.ProcessArchitecture, status.Device, status.MissingItems, suggestedAction, details, diagnostics ?? status.Diagnostics);

    private static void SmokeOpenVino(string path)
    {
        var artifact = new ModelArtifact(new ModelId("worker/abi-smoke-openvino"), string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase) ? "openvino-ir" : "onnx", path, null, OpenVinoBackendProvider.BackendId);
        using var provider = new OpenVinoBackendProvider();
        using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"), new SessionOptions(1, false));
    }

    private static void SmokeOpenCv(string path, IReadOnlyDictionary<string, string> payload)
    {
        var input = new TensorDescriptor(Value(payload, "smokeInputName", "input"), TensorElementType.Float32, new TensorShape(ParseShape(payload, "smokeInputShape", new[] { 1L, 3L, 224L, 224L })));
        var output = new TensorDescriptor(Value(payload, "smokeOutputName", "output"), TensorElementType.Float32, new TensorShape(ParseShape(payload, "smokeOutputShape", new[] { -1L })));
        var contract = new OpenCvDnnModelContract(new ModelId("worker/abi-smoke-opencv"), new[] { input }, new[] { output }, new[] { input.Name });
        var artifact = new ModelArtifact(new ModelId("worker/abi-smoke-opencv"), "onnx", path, null, OpenCvDnnBackendProvider.BackendId);
        using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract));
        using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), new SessionOptions(1, false));
    }

    private static void SmokeTensorRt(string path)
    {
        var artifact = new ModelArtifact(new ModelId("worker/abi-smoke-tensorrt"), "tensorrt-engine", path, null, TensorRtBackendProvider.BackendId);
        using var provider = new TensorRtBackendProvider();
        using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"), new SessionOptions(1, false));
    }

    private static long[] ParseShape(IReadOnlyDictionary<string, string> payload, string key, long[] fallback)
    {
        if (!payload.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value)) return fallback;
        return JsonSerializer.Deserialize<long[]>(value) ?? throw new FormatException(key + " must be a JSON array of dimensions.");
    }

    private static string Value(IReadOnlyDictionary<string, string> payload, string key, string fallback) => payload.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static BackendRuntimeStatus NormalizePlatformStatus(BackendPluginDescriptor descriptor, BackendRuntimeStatus status)
    {
        if (status.LoadedPath == null || IsCompatiblePath(status.LoadedPath, status.RuntimeIdentifier)) return status;
        var details = new Dictionary<string, string>(status.Details, StringComparer.Ordinal);
        foreach (string key in details.Keys.Where(key => key.EndsWith(".root", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (!IsCompatiblePath(details[key], status.RuntimeIdentifier)) details.Remove(key);
        }
        var missing = descriptor.NativeRequirements.Select(requirement => "native." + requirement.Kind.ToString().ToLowerInvariant()).ToArray();
        return new BackendRuntimeStatus(BackendRuntimeState.MissingNative, runtimeIdentifier: status.RuntimeIdentifier, processArchitecture: status.ProcessArchitecture, missingItems: missing, suggestedAction: "Install the native assets matching the Worker RID " + status.RuntimeIdentifier + ".", details: details, diagnostics: status.Diagnostics);
    }

    private static bool IsCompatiblePath(string path, string? rid)
    {
        if (string.Equals(rid, "win-x64", StringComparison.OrdinalIgnoreCase))
            return path.IndexOf("runtimes\\win-x64\\", StringComparison.OrdinalIgnoreCase) >= 0 || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && path.IndexOf("linux", StringComparison.OrdinalIgnoreCase) < 0;
        if (string.Equals(rid, "linux-x64", StringComparison.OrdinalIgnoreCase)) return path.IndexOf("linux-x64", StringComparison.OrdinalIgnoreCase) >= 0 || path.EndsWith(".so", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static BackendRuntimeStatus AugmentPackagedNativeStatus(BackendPluginDescriptor descriptor, BackendRuntimeStatus status)
    {
        if (status.State != BackendRuntimeState.MissingNative || status.MissingItems.Count == 0) return status;
        var details = new Dictionary<string, string>(status.Details, StringComparer.Ordinal);
        var missing = new List<string>();
        string? loadedPath = status.LoadedPath;
        for (int index = 0; index < descriptor.NativeRequirements.Count; index++)
        {
            NativeRuntimeRequirement requirement = descriptor.NativeRequirements[index];
            string key = "native." + requirement.Kind.ToString().ToLowerInvariant();
            if (!status.MissingItems.Contains(key, StringComparer.Ordinal)) continue;
            string? packagedPath = FindPackagedNative(descriptor.PluginId, requirement.Kind);
            if (packagedPath == null) missing.Add(key);
            else
            {
                details[key + ".packaged"] = packagedPath;
                loadedPath ??= packagedPath;
            }
        }
        if (missing.Count > 0) return status;
        details["probeSource"] = "worker-output-package";
        return new BackendRuntimeStatus(BackendRuntimeState.Available, loadedPath, status.Version, status.AbiApiLine, status.RuntimeIdentifier, status.ProcessArchitecture, status.Device, missingItems: Array.Empty<string>(), suggestedAction: null, details: details, diagnostics: status.Diagnostics);
    }

    private static string? FindPackagedNative(string pluginId, NativeRuntimeKind kind)
    {
        string rid = "win-x64";
        string nativeRoot = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");
        if (!Directory.Exists(nativeRoot)) return null;
        string[] names = kind switch
        {
            NativeRuntimeKind.LlamaSharpNative => new[] { "llama.dll", "ggml.dll" },
            NativeRuntimeKind.OpenCV => new[] { "JYPPX.OpenCV.Native.dll", "opencv_core500.dll", "opencv_dnn500.dll" },
            NativeRuntimeKind.OpenVINO => new[] { "openvino_c.dll", "openvino.dll" },
            _ => Array.Empty<string>()
        };
        foreach (string name in names)
        {
            string? match = Directory.EnumerateFiles(nativeRoot, name, SearchOption.AllDirectories).FirstOrDefault();
            if (match != null) return Path.GetFullPath(match);
        }
        return null;
    }

    private static BackendPluginDescriptor MakeWorkerCompatible(BackendPluginDescriptor descriptor)
    {
        if (descriptor.TargetFrameworks.Any(value => string.Equals(value, "net10.0", StringComparison.OrdinalIgnoreCase))) return descriptor;
        return new BackendPluginDescriptor(
            descriptor.PluginId,
            descriptor.DisplayName,
            descriptor.Version,
            descriptor.Backend,
            descriptor.TargetFrameworks.Concat(new[] { "net10.0" }),
            descriptor.RuntimeIdentifiers,
            descriptor.ExecutionMode,
            descriptor.Capabilities,
            descriptor.Formats,
            descriptor.ProviderPackageId,
            descriptor.ProviderPackageVersion,
            descriptor.RuntimeDependencies,
            descriptor.NativeRequirements,
            descriptor.OptionsSchema,
            descriptor.ProbeId,
            descriptor.EntryPoint);
    }

    private static string MapMissingItem(string value)
    {
        if (value.EndsWith("llamasharpnative", StringComparison.OrdinalIgnoreCase)) return "llama.dll/ggml.dll";
        if (value.EndsWith("opencv", StringComparison.OrdinalIgnoreCase)) return "opencv_world*.dll";
        if (value.EndsWith("openvino", StringComparison.OrdinalIgnoreCase)) return "openvino_c.dll";
        if (value.EndsWith("cuda", StringComparison.OrdinalIgnoreCase)) return "cudart64_*.dll";
        if (value.EndsWith("cudnn", StringComparison.OrdinalIgnoreCase)) return "cudnn*.dll";
        if (value.EndsWith("tensorrt", StringComparison.OrdinalIgnoreCase)) return "nvinfer*.dll";
        if (value.EndsWith("nvrtc", StringComparison.OrdinalIgnoreCase)) return "nvrtc*.dll";
        if (value.EndsWith("driver", StringComparison.OrdinalIgnoreCase)) return "nvcuda.dll";
        if (value.EndsWith("unknown", StringComparison.OrdinalIgnoreCase)) return "jyppxtrtbridge.dll";
        return value;
    }

    private static IEnumerable<string> ProbeRoots(IEnumerable<string> variables)
    {
        foreach (string variable in variables)
        {
            string? value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value)) continue;
            foreach (string root in value.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
                yield return variable + "=" + root.Trim();
        }
    }

    private static bool Contains(string value, string token) => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    private static readonly string[] LlamaEnvironmentVariables = { "LLAMASHARP_BACKEND_PATH", "DEPLOYSHARP_LLAMASHARP_ROOT" };
    private static readonly string[] OpenCvEnvironmentVariables = { "DEPLOYSHARP_OPENCV_ROOT" };
    private static readonly string[] OpenVinoEnvironmentVariables = { "DEPLOYSHARP_OPENVINO_ROOT" };
    private static readonly string[] TensorRtEnvironmentVariables = { "JYPPX_CUDA_ROOT", "CUDA_PATH", "JYPPX_CUDNN_ROOT", "JYPPX_TENSORRT_ROOT", "JYPPX_NATIVE_BRIDGE_PATH" };
}

internal sealed class WorkerProbeResult
{
    public WorkerProbeResult(bool succeeded, string message, IReadOnlyDictionary<string, string> payload)
    {
        Succeeded = succeeded;
        Message = message;
        Payload = payload;
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public IReadOnlyDictionary<string, string> Payload { get; }

    public static WorkerProbeResult Failed(string backendId, string code, string message, Exception? exception = null)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["backendId"] = backendId,
            ["execution"] = "worker",
            ["state"] = "ProbeFailed",
            ["diagnosticCode"] = code,
            ["missingItems"] = "backend-probe",
            ["suggestedAction"] = "Inspect the Worker diagnostic and install a compatible backend adapter."
        };
        if (exception != null) payload["technicalDetail"] = exception.GetType().FullName + ": " + exception.Message;
        return new WorkerProbeResult(false, message, payload);
    }
}
