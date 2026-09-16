using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Adapts an application-owned SAM 2/SAM 3 video predictor to the DeploySharp ROI state contract. / 将应用拥有的 SAM 2/SAM 3 视频 Predictor 适配到 DeploySharp ROI 状态合同。</summary>
    /// <typeparam name="TFrame">Application frame type. / 应用帧类型。</typeparam>
    /// <typeparam name="TResult">Predictor result type. / Predictor 结果类型。</typeparam>
    public interface IVisualRoiVideoPromptPredictor<TFrame, TResult>
    {
        /// <summary>Gets the exact immutable profile implemented by the predictor. / 获取 Predictor 实现的精确不可变 Profile。</summary>
        public PromptableSegmentationProfile Profile { get; }

        /// <summary>Processes one ordered frame and its ROI plan without committing planner state. / 处理一帧及其 ROI 计划，但不提交 Planner 状态。</summary>
        public Task<TResult> ProcessFrameAsync(TFrame frame, VisualRoiVideoPromptPlan plan, CancellationToken cancellationToken);

        /// <summary>Clears native memory, object slots, and propagation state before another video starts. / 在另一段视频开始前清理 native memory、对象槽位和传播状态。</summary>
        public Task ResetAsync(CancellationToken cancellationToken);
    }

    /// <summary>Contains one successfully committed SAM 2/SAM 3 video ROI frame. / 包含一个成功提交的 SAM 2/SAM 3 视频 ROI 帧。</summary>
    /// <typeparam name="TResult">Predictor result type. / Predictor 结果类型。</typeparam>
    public sealed class VisualRoiVideoFrameResult<TResult>
    {
        internal VisualRoiVideoFrameResult(VisualRoiVideoPromptPlan plan, TResult result, TimeSpan elapsed)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Result = result;
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
            Elapsed = elapsed;
        }

        /// <summary>Gets the exact committed plan. / 获取已提交的精确计划。</summary>
        public VisualRoiVideoPromptPlan Plan { get; }
        /// <summary>Gets the application predictor result. / 获取应用 Predictor 结果。</summary>
        public TResult Result { get; }
        /// <summary>Gets predictor wall-clock duration, excluding plan creation. / 获取 Predictor 墙钟耗时，不含计划创建。</summary>
        public TimeSpan Elapsed { get; }
    }

    /// <summary>Serializes stateful video prediction and commits ROI frame state only after predictor success. / 串行化有状态视频预测，并仅在 Predictor 成功后提交 ROI 帧状态。</summary>
    /// <typeparam name="TFrame">Application frame type. / 应用帧类型。</typeparam>
    /// <typeparam name="TResult">Predictor result type. / Predictor 结果类型。</typeparam>
    public sealed class VisualRoiVideoPromptRunner<TFrame, TResult>
    {
        private readonly VisualRoiVideoPromptPlanner _planner;
        private readonly IVisualRoiVideoPromptPredictor<TFrame, TResult> _predictor;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// <summary>Initializes a state-safe runner over an application-owned predictor. / 基于应用拥有的 Predictor 初始化状态安全运行器。</summary>
        public VisualRoiVideoPromptRunner(VisualRoiVideoPromptPlanner planner, IVisualRoiVideoPromptPredictor<TFrame, TResult> predictor)
        {
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _predictor = predictor ?? throw new ArgumentNullException(nameof(predictor));
            if (!ReferenceEquals(planner.Profile, predictor.Profile))
            {
                throw new VisualException(
                    VisualErrorCodes.PromptableSegmentationIdentityMismatch,
                    "The video predictor must implement the exact profile instance used by the ROI planner.",
                    profileId: planner.Profile.ProfileId,
                    technicalDetails: "predictorProfile=" + predictor.Profile.ProfileId);
            }
        }

        /// <summary>Gets the planner whose committed state is owned by this runner. / 获取由本运行器拥有提交状态的 Planner。</summary>
        public VisualRoiVideoPromptPlanner Planner => _planner;

        /// <summary>Plans, predicts, and atomically commits one ordered frame. / 对一帧依次执行规划、预测和原子提交。</summary>
        /// <remarks>A predictor exception or cancellation leaves <see cref="VisualRoiVideoPromptPlanner.LastFrameIndex"/> unchanged. / Predictor 异常或取消不会改变 Planner.LastFrameIndex。</remarks>
        public async Task<VisualRoiVideoFrameResult<TResult>> RunAsync(
            TFrame frame,
            VisualRoiSnapshot snapshot,
            long frameIndex,
            VisualRoiVideoFrameMode mode,
            VisualRoiPromptOptions? promptOptions = null,
            VisualRoiCoordinateContext? coordinateContext = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (frame is null) throw new ArgumentNullException(nameof(frame));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                VisualRoiVideoPromptPlan plan = _planner.CreatePlan(snapshot, frameIndex, mode, promptOptions, coordinateContext);
                var stopwatch = Stopwatch.StartNew();
                TResult result = await _predictor.ProcessFrameAsync(frame, plan, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                _planner.Commit(plan);
                return new VisualRoiVideoFrameResult<TResult>(plan, result, stopwatch.Elapsed);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Resets the external predictor first, then clears planner state only after reset succeeds. / 先重置外部 Predictor，仅在成功后清理 Planner 状态。</summary>
        public async Task ResetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _predictor.ResetAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _planner.Reset();
            }
            finally
            {
                _gate.Release();
            }
        }

    }
}
