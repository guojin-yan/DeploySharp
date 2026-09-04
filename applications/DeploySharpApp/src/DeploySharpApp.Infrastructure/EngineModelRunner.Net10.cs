#if NET10_0_OR_GREATER
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;
using DeploySharpApp.Engine;

namespace DeploySharpApp.Infrastructure;

public sealed class EngineModelRunner : IModelRunner
{
    private readonly IDeploySharpEngine _engine;
    private readonly IModelRunner _demoFallback;

    public EngineModelRunner(IDeploySharpEngine engine, IModelRunner demoFallback)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _demoFallback = demoFallback ?? throw new ArgumentNullException(nameof(demoFallback));
    }

    public Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        bool workerBackend = request.BackendId.IndexOf("tensorrt", StringComparison.OrdinalIgnoreCase) >= 0
            || request.BackendId.IndexOf("llamasharp", StringComparison.OrdinalIgnoreCase) >= 0;
        bool explicitCuda = string.Equals(request.Device, "cuda", StringComparison.OrdinalIgnoreCase);
        bool demo = request.ModelId.StartsWith("demo/", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.ModelPath)
            && !workerBackend
            && !explicitCuda;
        return demo
            ? _demoFallback.RunAsync(request, progress, cancellationToken)
            : _engine.RunAsync(request, progress, cancellationToken);
    }

    public Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        return _demoFallback.BenchmarkAsync(request, progress, cancellationToken);
    }
}
#endif
