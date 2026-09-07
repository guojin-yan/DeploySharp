#if NET10_0_OR_GREATER
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;
using DeploySharpApp.Engine;

namespace DeploySharpApp.Infrastructure;

public sealed class EngineModelRunner : IModelRunner, IStreamingModelRunner
{
    private readonly IDeploySharpEngine _engine;
    private readonly IModelRunner _demoFallback;
    private readonly IBackendHostWorkerClient _worker;

    public EngineModelRunner(IDeploySharpEngine engine, IModelRunner demoFallback, IBackendHostWorkerClient? worker = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _demoFallback = demoFallback ?? throw new ArgumentNullException(nameof(demoFallback));
        _worker = worker ?? new BackendHostWorkerClient();
    }

    public Task<ModelRunResult> RunAsync(ModelRunRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        bool workerBackend = request.BackendId.IndexOf("tensorrt", StringComparison.OrdinalIgnoreCase) >= 0
            || request.BackendId.IndexOf("llamasharp", StringComparison.OrdinalIgnoreCase) >= 0
            || request.BackendId.IndexOf("opencv", StringComparison.OrdinalIgnoreCase) >= 0
            || request.BackendId.IndexOf("openvino", StringComparison.OrdinalIgnoreCase) >= 0;
        bool releaseVisual = request.Operation == AppOperationKind.Vision
            && (request.ModelId.Equals("segmentation/sam-v1-vit-b", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("yolo/", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("deim/", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("rf-detr/", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("rt-detr/", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("pp-yoloe/", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("paddleocr/", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("mobile-", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("server-", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("anomalib/", StringComparison.OrdinalIgnoreCase)
                || request.ModelId.StartsWith("bria/rmbg-", StringComparison.OrdinalIgnoreCase));
        bool explicitCuda = string.Equals(request.Device, "cuda", StringComparison.OrdinalIgnoreCase);
        bool demo = request.ModelId.StartsWith("demo/", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.ModelPath)
            && !workerBackend
            && !explicitCuda;
        if (workerBackend || releaseVisual || string.Equals(request.Options.TryGetValue("executionMode", out string? mode) ? mode : null, "worker", StringComparison.OrdinalIgnoreCase))
            return _worker.RunAsync(request, progress, cancellationToken);
        return demo
            ? _demoFallback.RunAsync(request, progress, cancellationToken)
            : _engine.RunAsync(request, progress, cancellationToken);
    }

    public async Task<ModelRunResult> RunStreamingAsync(ModelRunRequest request, IProgress<double>? progress, IProgress<string>? textProgress, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        bool workerBackend = IsWorkerBackend(request.BackendId)
            || string.Equals(request.Options.TryGetValue("executionMode", out string? mode) ? mode : null, "worker", StringComparison.OrdinalIgnoreCase);
        if (workerBackend) return await _worker.RunStreamingAsync(request, progress, textProgress, cancellationToken).ConfigureAwait(false);

        ModelRunResult result = await RunAsync(request, progress, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded && !string.IsNullOrEmpty(result.Output)) textProgress?.Report(result.Output!);
        return result;
    }

    public Task<BenchmarkReport> BenchmarkAsync(BenchmarkRequest request, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        bool workerBackend = IsWorkerBackend(request.BackendId)
            || request.BackendId.IndexOf("onnxruntime", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrWhiteSpace(request.ModelPath);
        return workerBackend ? _worker.BenchmarkAsync(request, progress, cancellationToken) : _demoFallback.BenchmarkAsync(request, progress, cancellationToken);
    }

    private static bool IsWorkerBackend(string backendId)
        => backendId.IndexOf("tensorrt", StringComparison.OrdinalIgnoreCase) >= 0
            || backendId.IndexOf("llamasharp", StringComparison.OrdinalIgnoreCase) >= 0
            || backendId.IndexOf("opencv", StringComparison.OrdinalIgnoreCase) >= 0
            || backendId.IndexOf("openvino", StringComparison.OrdinalIgnoreCase) >= 0;
}
#endif
