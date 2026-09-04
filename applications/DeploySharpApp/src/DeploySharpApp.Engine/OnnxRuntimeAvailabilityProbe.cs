using System.Runtime.InteropServices;
using AppDiagnosticSeverity = DeploySharpApp.Contracts.DiagnosticSeverity;
using AppRuntimeDiagnostic = DeploySharpApp.Contracts.RuntimeDiagnostic;
using AppRuntimeState = DeploySharpApp.Contracts.AppRuntimeState;
using AppRuntimeStatus = DeploySharpApp.Contracts.BackendRuntimeStatus;
using CoreRuntimeState = JYPPX.DeploySharp.Extensibility.BackendRuntimeState;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Extensibility;

namespace DeploySharpApp.Engine;

public sealed class OnnxRuntimeAvailabilityProbe : IOnnxRuntimeAvailabilityProbe
{
    public async Task<AppRuntimeStatus> ProbeAsync(CancellationToken cancellationToken)
    {
        var factory = new OnnxRuntimePluginFactory();
        BackendPluginDescriptor plugin = ForNet10(factory.Descriptor);
        string root = ResolveRuntimeRoot();
        var probe = new OnnxRuntimeRuntimeProbe(new BackendPluginContext(root));
        JYPPX.DeploySharp.Extensibility.BackendRuntimeStatus status = await probe.ProbeAsync(plugin, cancellationToken).ConfigureAwait(false);

        var details = new Dictionary<string, string>(status.Details, StringComparer.Ordinal);
        IReadOnlyList<string> paths = CandidatePaths(root);
        for (var index = 0; index < paths.Count; index++) details["probe.path." + index] = paths[index];
        string? localCandidate = paths.FirstOrDefault(File.Exists);
        bool candidateFound = localCandidate != null;
        details["nativePreflight"] = candidateFound ? "pending-abi-version-check" : "not-run";

        var diagnostics = (candidateFound ? Enumerable.Empty<JYPPX.DeploySharp.Diagnostics.RuntimeDiagnostic>() : status.Diagnostics).Select(item => new AppRuntimeDiagnostic(
            item.Code,
            (AppDiagnosticSeverity)(int)item.Severity,
            item.Message,
            BackendIds.ApplicationOnnxRuntime,
            item.ModelId?.Value,
            item.Details)).ToList();

        if (!candidateFound && diagnostics.Count == 0)
        {
            diagnostics.Add(new AppRuntimeDiagnostic(
                "DSAPP-ORT-NATIVE-MISSING",
                AppDiagnosticSeverity.Warning,
                "No application-local ONNX Runtime native library was found in the probed paths.",
                BackendIds.ApplicationOnnxRuntime,
                details: new Dictionary<string, string> { ["probedPaths"] = string.Join(Path.PathSeparator.ToString(), paths) }));
        }

        return new AppRuntimeStatus(
            BackendIds.ApplicationOnnxRuntime,
            candidateFound ? AppRuntimeState.Available : Map(status.State),
            candidateFound
                ? "An application-local ONNX Runtime library was found; session creation will perform the ABI and version smoke test."
                : "No compatible application-local ONNX Runtime native candidate is discoverable.",
            localCandidate ?? status.LoadedPath,
            status.Version,
            status.AbiApiLine,
            status.RuntimeIdentifier,
            status.ProcessArchitecture,
            new[] { "cpu" },
            candidateFound ? Array.Empty<string>() : status.MissingItems,
            candidateFound ? null : status.SuggestedAction ?? "Install Microsoft.ML.OnnxRuntime 1.28.0 for this RID or set DEPLOYSHARP_ONNXRUNTIME_NATIVE_PATH.",
            details,
            diagnostics);
    }

    private static BackendPluginDescriptor ForNet10(BackendPluginDescriptor source)
    {
        return new BackendPluginDescriptor(
            source.PluginId,
            source.DisplayName,
            source.Version,
            source.Backend,
            targetFrameworks: new[] { "net10.0" },
            runtimeIdentifiers: source.RuntimeIdentifiers,
            executionMode: source.ExecutionMode,
            capabilities: source.Capabilities,
            formats: source.Formats,
            providerPackageId: source.ProviderPackageId,
            providerPackageVersion: source.ProviderPackageVersion,
            runtimeDependencies: source.RuntimeDependencies,
            nativeRequirements: source.NativeRequirements,
            optionsSchema: source.OptionsSchema,
            probeId: source.ProbeId,
            entryPoint: source.EntryPoint);
    }

    private static string ResolveRuntimeRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_ONNXRUNTIME_NATIVE_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string path = Path.GetFullPath(configured.Trim());
            return Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
        }

        configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_ORT_ROOT");
        return string.IsNullOrWhiteSpace(configured) ? AppContext.BaseDirectory : Path.GetFullPath(configured.Trim());
    }

    private static IReadOnlyList<string> CandidatePaths(string root)
    {
        string library = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "onnxruntime.dll" : "libonnxruntime.so";
        string rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "win-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
            : "linux-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        return new[]
        {
            Path.GetFullPath(Path.Combine(root, library)),
            Path.GetFullPath(Path.Combine(root, "runtimes", rid, "native", library))
        };
    }

    private static AppRuntimeState Map(CoreRuntimeState state)
    {
        return state switch
        {
            CoreRuntimeState.Available => AppRuntimeState.Available,
            CoreRuntimeState.MissingPackage => AppRuntimeState.MissingPackage,
            CoreRuntimeState.MissingNative => AppRuntimeState.MissingNative,
            CoreRuntimeState.Incompatible => AppRuntimeState.Incompatible,
            CoreRuntimeState.Unavailable => AppRuntimeState.Unavailable,
            CoreRuntimeState.ProbeFailed => AppRuntimeState.ProbeFailed,
            CoreRuntimeState.Unsupported => AppRuntimeState.Unsupported,
            _ => AppRuntimeState.ProbeFailed
        };
    }
}

internal static class BackendIds
{
    public const string ApplicationOnnxRuntime = "deploysharp.backend.onnxruntime";
    public const string CoreOnnxRuntime = "onnxruntime";
}
