using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeploySharpApp.Contracts;
using DeploySharpApp.Plugin.Abstractions;

namespace DeploySharpApp.Application
{
    public interface IAppCatalog
    {
        IReadOnlyList<AppBackendInfo> GetBackends();
        IReadOnlyList<AppModelInfo> GetModels();
        IReadOnlyList<BackendRuntimeStatus> GetRuntimeStatuses();
        void Refresh();
    }

    public interface IAppRuntimeProbe
    {
        BackendRuntimeStatus Probe(PluginManifest manifest);
    }

    public interface IModelRunner
    {
        Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken);
        Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken);
    }

    public sealed class DeploySharpAppService
    {
        private readonly IAppCatalog _catalog;
        private readonly IModelRunner _runner;

        public DeploySharpAppService(IAppCatalog catalog, IModelRunner runner)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public IReadOnlyList<AppBackendInfo> Backends => _catalog.GetBackends();
        public IReadOnlyList<AppModelInfo> Models => _catalog.GetModels();
        public IReadOnlyList<BackendRuntimeStatus> RuntimeStatuses => _catalog.GetRuntimeStatuses();
        public void RefreshCatalog() => _catalog.Refresh();
        public Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => _runner.RunAsync(request, progress, cancellationToken);
        public Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => _runner.BenchmarkAsync(request, progress, cancellationToken);
    }

    public sealed class InMemoryAppCatalog : IAppCatalog
    {
        private readonly List<PluginManifest> _manifests;
        private readonly IAppRuntimeProbe _probe;
        private readonly List<AppModelInfo> _models;
        private List<BackendRuntimeStatus> _statuses;

        public InMemoryAppCatalog(IEnumerable<PluginManifest> manifests, IAppRuntimeProbe probe, IEnumerable<AppModelInfo>? models = null)
        {
            _manifests = manifests?.ToList() ?? throw new ArgumentNullException(nameof(manifests));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            _models = models?.ToList() ?? DefaultModels().ToList();
            _statuses = new List<BackendRuntimeStatus>();
            Refresh();
        }

        public IReadOnlyList<AppBackendInfo> GetBackends() => _manifests.Select(manifest => manifest.ToBackendInfo(_statuses.First(status => string.Equals(status.BackendId, manifest.PluginId, StringComparison.OrdinalIgnoreCase)).State, _statuses.First(status => string.Equals(status.BackendId, manifest.PluginId, StringComparison.OrdinalIgnoreCase)).Message)).ToList().AsReadOnly();
        public IReadOnlyList<AppModelInfo> GetModels() => _models.AsReadOnly();
        public IReadOnlyList<BackendRuntimeStatus> GetRuntimeStatuses() => _statuses.AsReadOnly();
        public void Refresh() => _statuses = _manifests.Select(_probe.Probe).ToList();

        private static IEnumerable<AppModelInfo> DefaultModels()
        {
            yield return new AppModelInfo("demo/yolo-v8-n", "YOLOv8 Nano", "vision.detect", "onnx", "13 MB", new[] { "deploysharp.backend.onnxruntime", "deploysharp.backend.opencv" }, "MIT", cached: true, location: "builtin://demo/yolo-v8-n");
            yield return new AppModelInfo("demo/qwen-0.5b", "Qwen 2.5 0.5B Instruct", "text-generation", "gguf", "398 MB", new[] { "deploysharp.backend.llamasharp" }, "Apache-2.0", cached: false);
            yield return new AppModelInfo("demo/vision-language", "Vision Language Demo", "multimodal", "modelpack", "24 MB", new[] { "deploysharp.backend.onnxruntime" }, "Apache-2.0", cached: true, location: "builtin://demo/vision-language");
        }
    }

    public sealed class FakeModelRunner : IModelRunner
    {
        public async Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!string.Equals(request.Device, "cpu", StringComparison.OrdinalIgnoreCase))
            {
                var diagnostic = new RuntimeDiagnostic("DSAPP-DEMO-DEVICE-UNAVAILABLE", DiagnosticSeverity.Warning, "The demo runner does not emulate native device availability.", request.BackendId, request.ModelId);
                return new ModelRunResult(false, AppErrorCode.BackendUnavailable, "Demo mode supports the logical CPU path only; no native device was loaded.", diagnostics: new[] { diagnostic }, runMode: ModelRunMode.Demo);
            }
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operation.CancelAfter(request.Timeout);
            var started = DateTime.UtcNow;
            for (var index = 1; index <= 5; index++) { await Task.Delay(30, operation.Token).ConfigureAwait(false); progress?.Report(index / 5d); }
            var total = (DateTime.UtcNow - started).TotalMilliseconds;
            return new ModelRunResult(true, AppErrorCode.None, "Demo/Fake operation completed. No native backend was loaded.", request.Operation == AppOperationKind.TextGeneration ? "DeploySharpApp demo response: the application contract is ready." : "{\"detections\": [{\"label\": \"demo-object\", \"confidence\": 0.98}]} ", preprocessMs: 1.2, inferenceMs: Math.Max(1, total), postprocessMs: 0.6, runMode: ModelRunMode.Demo);
        }

        public async Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            for (var index = 1; index <= request.Iterations; index++) { await Task.Delay(10, cancellationToken).ConfigureAwait(false); progress?.Report(index / (double)request.Iterations); }
            return new BenchmarkReport(request, true, "Demo benchmark completed; replace FakeModelRunner with the selected in-process or Worker adapter.", 4.8, 5.6, 207, AppExecutionMode.InProcess.ToString());
        }
    }
}
