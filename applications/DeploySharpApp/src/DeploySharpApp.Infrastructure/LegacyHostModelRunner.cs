using System;
using System.Threading;
using System.Threading.Tasks;
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.Infrastructure
{
    public sealed class LegacyHostModelRunner : IModelRunner
    {
        private readonly IModelRunner _demoRunner;

        public LegacyHostModelRunner(IModelRunner demoRunner)
        {
            _demoRunner = demoRunner ?? throw new ArgumentNullException(nameof(demoRunner));
        }

        public Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            bool workerBackend = request.BackendId.IndexOf("tensorrt", StringComparison.OrdinalIgnoreCase) >= 0
                || request.BackendId.IndexOf("llamasharp", StringComparison.OrdinalIgnoreCase) >= 0
                || request.BackendId.IndexOf("onnxruntime", StringComparison.OrdinalIgnoreCase) >= 0 && !request.ModelId.StartsWith("demo/", StringComparison.OrdinalIgnoreCase);
            if (request.ModelId.StartsWith("demo/", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(request.ModelPath)
                && string.Equals(request.Device, "cpu", StringComparison.OrdinalIgnoreCase)
                && !workerBackend)
            {
                return _demoRunner.RunAsync(request, progress, cancellationToken);
            }

            var diagnostic = new RuntimeDiagnostic(
                "DSAPP-WORKER-REQUIRED",
                DiagnosticSeverity.Warning,
                "Real native inference from the .NET Framework host requires the net10 BackendHost Worker.",
                request.BackendId,
                request.ModelId);
            return Task.FromResult(new ModelRunResult(
                false,
                AppErrorCode.WorkerRequired,
                "This operation requires the net10 BackendHost Worker; no in-process native backend or Fake fallback was used.",
                diagnostics: new[] { diagnostic },
                runMode: ModelRunMode.Worker));
        }

        public Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            return _demoRunner.BenchmarkAsync(request, progress, cancellationToken);
        }
    }
}
