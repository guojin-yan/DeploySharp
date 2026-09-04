using DeploySharpApp.Contracts;

namespace DeploySharpApp.Engine;

public interface IDeploySharpEngine
{
    Task<ModelRunResult> RunAsync(
        ModelRunRequest request,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

public interface IOnnxRuntimeAvailabilityProbe
{
    Task<BackendRuntimeStatus> ProbeAsync(CancellationToken cancellationToken);
}
