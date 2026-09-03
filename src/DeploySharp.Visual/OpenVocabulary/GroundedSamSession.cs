using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Owns two prepared tensors derived from one decoded image for detector and SAM encoder execution. / 拥有由同一次图像解码派生、分别用于检测器与 SAM Encoder 的两个已准备张量。</summary>
    public sealed class GroundedSamPreparedInput : IDisposable
    {
        private bool _disposed;

        /// <summary>Initializes an identity-checked dual input. / 初始化经过 Identity 检查的双路输入。</summary>
        public GroundedSamPreparedInput(PreparedVisualInput detectorInput, PreparedVisualInput segmentationInput)
        {
            DetectorInput = detectorInput ?? throw new ArgumentNullException(nameof(detectorInput));
            SegmentationInput = segmentationInput ?? throw new ArgumentNullException(nameof(segmentationInput));
            if (detectorInput.InputId == null || !string.Equals(detectorInput.InputId, segmentationInput.InputId, StringComparison.Ordinal) || detectorInput.SourceSize != segmentationInput.SourceSize) throw new VisualException(VisualErrorCodes.OpenVocabularyIdentityMismatch, "Detector and segmentation inputs must originate from the same encoded image and source geometry.");
            SourceSha256 = detectorInput.InputId;
            SourceSize = detectorInput.SourceSize;
        }

        /// <summary>Gets the detector input. / 获取检测器输入。</summary>
        public PreparedVisualInput DetectorInput { get; }
        /// <summary>Gets the SAM image-encoder input. / 获取 SAM 图像 Encoder 输入。</summary>
        public PreparedVisualInput SegmentationInput { get; }
        /// <summary>Gets exact encoded-image SHA256. / 获取精确编码图像 SHA256。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets whether both inputs have been released. / 获取两路输入是否已释放。</summary>
        public bool IsDisposed => _disposed;

        /// <inheritdoc />
        /// <remarks>Idempotently releases only resources owned by the two prepared inputs. / 仅幂等释放两路已准备输入拥有的资源。</remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DetectorInput.Dispose();
            SegmentationInput.Dispose();
        }

        internal void EnsureUsable()
        {
            if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The Grounded-SAM prepared input has been disposed.");
        }
    }

    /// <summary>Describes one successfully installed Grounded-SAM image state. / 描述一个已成功安装的 Grounded-SAM 图像状态。</summary>
    public sealed class GroundedSamImageState
    {
        internal GroundedSamImageState(string sourceSha256, VisualSize sourceSize, OpenVocabularyDetectionResult detections, PromptableImageEmbedding embedding, TimeSpan detectorTime)
        {
            SourceSha256 = sourceSha256;
            SourceSize = sourceSize;
            Detections = detections;
            Embedding = embedding;
            DetectorTime = detectorTime;
        }

        /// <summary>Gets encoded-image identity shared by both sub-pipelines. / 获取两个子 Pipeline 共享的编码图像 Identity。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets cached canonical detections and phrase provenance. / 获取缓存的规范检测与短语来源。</summary>
        public OpenVocabularyDetectionResult Detections { get; }
        /// <summary>Gets cached SAM embedding summary and identity. / 获取缓存的 SAM Embedding 摘要与 Identity。</summary>
        public PromptableImageEmbedding Embedding { get; }
        /// <summary>Gets one detector execution observation. / 获取一次检测器执行观测。</summary>
        public TimeSpan DetectorTime { get; }
    }

    /// <summary>Associates one detector box and phrase with the SAM result produced from that exact source-space box prompt. / 将一个检测框及短语与由该精确源图框提示生成的 SAM 结果关联。</summary>
    public sealed class GroundedSamInstance
    {
        internal GroundedSamInstance(int detectionIndex, Detection detection, OpenVocabularyDetectionMatch match, PromptableSegmentationResult segmentation)
        {
            DetectionIndex = detectionIndex;
            Detection = detection;
            Match = match;
            Segmentation = segmentation;
        }

        /// <summary>Gets canonical detection index. / 获取规范检测索引。</summary>
        public int DetectionIndex { get; }
        /// <summary>Gets reused canonical source-space detection. / 获取复用的规范源图检测。</summary>
        public Detection Detection { get; }
        /// <summary>Gets phrase/token provenance. / 获取短语/Token 来源。</summary>
        public OpenVocabularyDetectionMatch Match { get; }
        /// <summary>Gets owned canonical masks, RLE, quality, and low-resolution feedback. / 获取自有规范 Mask、RLE、质量与低分辨率反馈。</summary>
        public PromptableSegmentationResult Segmentation { get; }
    }

    /// <summary>Contains one deterministic detector-to-SAM composition result. / 包含一个确定性的检测器到 SAM 组合结果。</summary>
    public sealed class GroundedSamResult
    {
        private readonly IReadOnlyList<GroundedSamInstance> _instances;

        internal GroundedSamResult(GroundedSamImageState image, IEnumerable<GroundedSamInstance> instances, TimeSpan compositionTime)
        {
            Image = image;
            _instances = new ReadOnlyCollection<GroundedSamInstance>(new List<GroundedSamInstance>(instances));
            CompositionTime = compositionTime;
        }

        /// <summary>Gets exact installed image, profile, artifact, vocabulary, and embedding state. / 获取精确安装的图像、Profile、工件、词汇与 Embedding 状态。</summary>
        public GroundedSamImageState Image { get; }
        /// <summary>Gets instances in selected detector order. / 获取按所选检测器顺序排列的实例。</summary>
        public IReadOnlyList<GroundedSamInstance> Instances => _instances;
        /// <summary>Gets one complete box-prompt composition observation. / 获取一次完整框提示组合观测。</summary>
        public TimeSpan CompositionTime { get; }
    }

    /// <summary>Owns one open-vocabulary detector session, one SAM image session, and one atomically installed shared-image state. / 拥有一个开放词汇检测器 Session、一个 SAM 图像 Session 及一个原子安装的共享图像状态。</summary>
    /// <remarks>State-changing and prompt operations are single-writer and reject overlap; the backend registry and prepared inputs remain caller-owned. / 状态变更与提示操作为单写者并拒绝重叠；Backend Registry 与已准备输入仍由调用方拥有。</remarks>
    public sealed class GroundedSamImageSession : IDisposable
    {
        private readonly object _lifetimeGate = new object();
        private readonly VisualPipeline _detector;
        private readonly PromptableSegmentationImageSession _segmentation;
        private readonly CancellationTokenSource _disposeSource = new CancellationTokenSource();
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private GroundedSamImageState? _state;
        private int _operationActive;
        private bool _disposed;

        /// <summary>Creates exact detector and SAM sessions; both are owned and disposed by this composition session. / 创建精确检测器与 SAM Session；两者均由本组合 Session 拥有并释放。</summary>
        public GroundedSamImageSession(BackendRegistry backendRegistry, OpenVocabularyDetectionProfile detectorProfile, ModelArtifact detectorArtifact, BackendRequest detectorRequest, PromptableSegmentationArtifactBundle segmentationBundle, BackendRequest segmentationRequest, SessionOptions? sessionOptions = null)
        {
            if (backendRegistry == null) throw new ArgumentNullException(nameof(backendRegistry));
            DetectorProfile = detectorProfile ?? throw new ArgumentNullException(nameof(detectorProfile));
            if (!detectorProfile.Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The selected detector profile is contract-only: " + detectorProfile.Blocker + ".", profileId: detectorProfile.ProfileId);
            if (detectorArtifact == null) throw new ArgumentNullException(nameof(detectorArtifact));
            if (detectorRequest == null) throw new ArgumentNullException(nameof(detectorRequest));
            if (segmentationBundle == null) throw new ArgumentNullException(nameof(segmentationBundle));
            if (segmentationRequest == null) throw new ArgumentNullException(nameof(segmentationRequest));
            var profiles = new VisualProfileRegistry();
            profiles.Register(detectorProfile.VisualProfile);
            profiles.Freeze();
            VisualPipeline? detector = null;
            try
            {
                detector = new VisualPipeline(backendRegistry, profiles.Select(detectorArtifact, backendRegistry, detectorRequest, VisualTaskId.ObjectDetection), detectorRequest, sessionOptions);
                _segmentation = new PromptableSegmentationImageSession(backendRegistry, segmentationBundle, segmentationRequest, sessionOptions);
                _detector = detector;
            }
            catch
            {
                detector?.Dispose();
                _disposeSource.Dispose();
                _idle.Dispose();
                throw;
            }
        }

        /// <summary>Gets immutable detector profile. / 获取不可变检测器 Profile。</summary>
        public OpenVocabularyDetectionProfile DetectorProfile { get; }
        /// <summary>Gets current shared image state or null before set-image/after clear. / 获取当前共享图像状态；set-image 前或 clear 后为 null。</summary>
        public GroundedSamImageState? CurrentImage { get { lock (_lifetimeGate) { EnsureUsableLocked(); return _state; } } }

        /// <summary>Runs detector and SAM encoder once each and atomically replaces state only after both succeed. / 检测器与 SAM Encoder 各运行一次，并仅在两者均成功后原子替换状态。</summary>
        public GroundedSamImageState SetImage(GroundedSamPreparedInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetImageCoreAsync(input, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously installs a shared image; cancellation never installs a partial detector/embedding state. / 异步安装共享图像；取消不会安装部分检测器/Embedding 状态。</summary>
        public Task<GroundedSamImageState> SetImageAsync(GroundedSamPreparedInput input, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SetImageCoreAsync(input, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Segments the first capacity-bounded detections using their existing source-space boxes and deterministic order. / 使用现有源图空间框与确定性顺序分割前若干个容量受限的检测。</summary>
        public GroundedSamResult SegmentDetections(int maximumDetections = 10, float minimumScore = 0f, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SegmentCoreAsync(maximumDetections, minimumScore, options ?? VisualExecutionOptions.Default, false, cancellationToken).GetAwaiter().GetResult();

        /// <summary>Asynchronously segments selected detections; each returned mask is fully owned and cancellation returns no partial list. / 异步分割所选检测；每个返回 Mask 均完全自有，取消时不返回部分列表。</summary>
        public Task<GroundedSamResult> SegmentDetectionsAsync(int maximumDetections = 10, float minimumScore = 0f, VisualExecutionOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) => SegmentCoreAsync(maximumDetections, minimumScore, options ?? VisualExecutionOptions.Default, true, cancellationToken);

        /// <summary>Clears detector and SAM embedding state together. / 同时清除检测器与 SAM Embedding 状态。</summary>
        public void ClearImage()
        {
            EnterOperation();
            try { _segmentation.ClearImage(); lock (_lifetimeGate) { EnsureUsableLocked(); _state = null; } }
            finally { ExitOperation(); }
        }

        /// <inheritdoc />
        /// <remarks>Cancels active work, waits for consistent unwind, clears state, and disposes both owned sub-pipelines exactly once. / 取消活动工作、等待一致退出、清除状态，并仅一次释放两个自有子 Pipeline。</remarks>
        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                _disposeSource.Cancel();
            }
            _idle.Wait();
            try
            {
                _state = null;
                _segmentation.Dispose();
                _detector.Dispose();
            }
            finally
            {
                _disposeSource.Dispose();
                _idle.Dispose();
            }
        }

        private async Task<GroundedSamImageState> SetImageCoreAsync(GroundedSamPreparedInput input, VisualExecutionOptions options, bool asynchronous, CancellationToken callerToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            CancellationToken disposeToken = EnterOperation();
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            CancellationToken operationToken = disposeToken;
            try
            {
                if (options.Timeout.HasValue) timeoutSource = new CancellationTokenSource(options.Timeout.Value);
                if (callerToken.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(callerToken, disposeToken)
                        : CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutSource.Token, disposeToken);
                    operationToken = linked.Token;
                }
                input.EnsureUsable();
                VisualInferenceResult detectorResult = asynchronous ? await _detector.RunAsync(input.DetectorInput, options, operationToken).ConfigureAwait(false) : _detector.Run(input.DetectorInput, options, operationToken);
                OpenVocabularyDetectionResult detections = detectorResult.GetValue<OpenVocabularyDetectionResult>();
                PromptableImageEmbedding embedding = asynchronous ? await _segmentation.SetImageAsync(input.SegmentationInput, options, operationToken).ConfigureAwait(false) : _segmentation.SetImage(input.SegmentationInput, options, operationToken);
                if (!string.Equals(embedding.Identity.ContentSha256, input.SourceSha256, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.OpenVocabularyIdentityMismatch, "SAM installed an embedding for a different encoded image.", profileId: DetectorProfile.ProfileId);
                var state = new GroundedSamImageState(input.SourceSha256, input.SourceSize, detections, embedding, detectorResult.Timing.Inference + detectorResult.Timing.Postprocessing);
                lock (_lifetimeGate) { EnsureUsableLocked(); _state = state; }
                return state;
            }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                if (options.DisposeOwnedInputOnCompletion) input.Dispose();
                ExitOperation();
            }
        }

        private async Task<GroundedSamResult> SegmentCoreAsync(int maximumDetections, float minimumScore, VisualExecutionOptions options, bool asynchronous, CancellationToken callerToken)
        {
            if (maximumDetections <= 0 || maximumDetections > DetectorProfile.MaximumDetections) throw new VisualException(VisualErrorCodes.OpenVocabularyLimitExceeded, "The Grounded-SAM detection capacity is invalid.", profileId: DetectorProfile.ProfileId);
            if (float.IsNaN(minimumScore) || float.IsInfinity(minimumScore) || minimumScore < 0f || minimumScore > 1f) throw new VisualException(VisualErrorCodes.OpenVocabularyContractInvalid, "The minimum score must be finite and between zero and one.", profileId: DetectorProfile.ProfileId);
            CancellationToken disposeToken = EnterOperation();
            CancellationTokenSource? timeoutSource = null;
            CancellationTokenSource? linked = null;
            CancellationToken operationToken = disposeToken;
            try
            {
                if (options.Timeout.HasValue) timeoutSource = new CancellationTokenSource(options.Timeout.Value);
                if (callerToken.CanBeCanceled || timeoutSource != null)
                {
                    linked = timeoutSource == null
                        ? CancellationTokenSource.CreateLinkedTokenSource(callerToken, disposeToken)
                        : CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutSource.Token, disposeToken);
                    operationToken = linked.Token;
                }
                GroundedSamImageState state;
                lock (_lifetimeGate) { EnsureUsableLocked(); state = _state ?? throw new VisualException(VisualErrorCodes.OpenVocabularyStateInvalid, "Set-image must succeed before Grounded-SAM composition.", profileId: DetectorProfile.ProfileId); }
                var watch = Stopwatch.StartNew();
                var instances = new List<GroundedSamInstance>();
                for (int index = 0; index < state.Detections.Detections.Detections.Count && instances.Count < maximumDetections; index++)
                {
                    operationToken.ThrowIfCancellationRequested();
                    Detection detection = state.Detections.Detections.Detections[index];
                    if (detection.Label.Score < minimumScore) continue;
                    var prompt = new PromptableSegmentationPrompt(box: detection.Box, returnMultipleMasks: false, promptId: "grounded-detection-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    PromptableSegmentationResult segmentation = asynchronous ? await _segmentation.PredictAsync(prompt, options, operationToken).ConfigureAwait(false) : _segmentation.Predict(prompt, options, operationToken);
                    instances.Add(new GroundedSamInstance(index, detection, state.Detections.Matches[index], segmentation));
                }
                watch.Stop();
                return new GroundedSamResult(state, instances, watch.Elapsed);
            }
            finally
            {
                linked?.Dispose();
                timeoutSource?.Dispose();
                ExitOperation();
            }
        }

        private CancellationToken EnterOperation()
        {
            lock (_lifetimeGate) EnsureUsableLocked();
            if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0) throw new VisualException(VisualErrorCodes.OpenVocabularyConcurrentOperation, "A Grounded-SAM operation is already active.", profileId: DetectorProfile.ProfileId);
            _idle.Reset();
            lock (_lifetimeGate)
            {
                if (_disposed) { ExitOperation(); throw new VisualException(VisualErrorCodes.ObjectDisposed, "The Grounded-SAM session has been disposed.", profileId: DetectorProfile.ProfileId); }
                return _disposeSource.Token;
            }
        }

        private void ExitOperation()
        {
            Volatile.Write(ref _operationActive, 0);
            _idle.Set();
        }

        private void EnsureUsableLocked()
        {
            if (_disposed) throw new VisualException(VisualErrorCodes.ObjectDisposed, "The Grounded-SAM session has been disposed.", profileId: DetectorProfile.ProfileId);
        }
    }
}
