using System;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Runs one anomaly profile through a selected backend and returns a typed owned result. / 通过选定后端运行一个异常 Profile 并返回类型化自有结果。</summary>
    public sealed class AnomalyPipeline : IDisposable
    {
        private readonly VisualPipeline _inner;

        /// <summary>Initializes and owns one anomaly inference session; the registry remains caller-owned. / 初始化并拥有一个异常推理会话；注册中心仍由调用方拥有。</summary>
        public AnomalyPipeline(BackendRegistry backendRegistry, VisualProfileSelection selection, BackendRequest request, SessionOptions? sessionOptions = null)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (selection.Profile.Task != VisualTaskId.AnomalyDetection) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An anomaly pipeline requires an anomaly-detection profile.", profileId: selection.Profile.ProfileId, backendId: selection.Backend.Id, modelId: selection.Profile.ModelId);
            if (!(selection.Profile.Decoder is IAnomalyPostprocessor)) throw new VisualException(VisualErrorCodes.ProfileInvalid, "An anomaly profile requires an anomaly postprocessor.", profileId: selection.Profile.ProfileId, backendId: selection.Backend.Id, modelId: selection.Profile.ModelId);
            _inner = new VisualPipeline(backendRegistry ?? throw new ArgumentNullException(nameof(backendRegistry)), selection, request ?? throw new ArgumentNullException(nameof(request)), sessionOptions);
        }

        /// <summary>Gets the selected profile, artifact, and backend. / 获取已选择的 Profile、工件与后端。</summary>
        public VisualProfileSelection Selection => _inner.Selection;

        /// <summary>Runs synchronous anomaly inference. / 运行同步异常推理。</summary>
        public AnomalyDetectionResult Run(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            VisualInferenceResult result = _inner.Run(input, options, cancellationToken);
            return AttachTiming(result);
        }

        /// <summary>Runs backend asynchronous inference or its documented fallback without using Task.Run. / 运行后端异步推理或其已记录回退，不使用 Task.Run。</summary>
        public async Task<AnomalyDetectionResult> RunAsync(PreparedVisualInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            VisualInferenceResult result = await _inner.RunAsync(input, options, cancellationToken).ConfigureAwait(false);
            return AttachTiming(result);
        }

        /// <inheritdoc />
        /// <remarks>Delegates idempotent active-call-aware disposal to the owned Visual pipeline. / 将幂等且感知活动调用的释放委托给自有 Visual Pipeline。</remarks>
        public void Dispose() => _inner.Dispose();

        private static AnomalyDetectionResult AttachTiming(VisualInferenceResult result) => result.GetValue<AnomalyDetectionResult>().WithTiming(result.Timing);
    }
}
