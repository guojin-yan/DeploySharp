using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies how a stateful SAM 2/SAM 3 video predictor should consume one frame. / 标识有状态 SAM 2/SAM 3 视频 Predictor 应如何消费一帧。</summary>
    public enum VisualRoiVideoFrameMode
    {
        /// <summary>Installs the first-frame ROI prompts and starts predictor state. / 安装首帧 ROI 提示并启动 Predictor 状态。</summary>
        Initialize = 1,
        /// <summary>Advances the predictor without adding prompts. / 推进 Predictor，但不增加提示。</summary>
        Propagate = 2,
        /// <summary>Advances the predictor and re-applies ROI prompts as a correction frame. / 推进 Predictor，并在修正帧重新应用 ROI 提示。</summary>
        Correct = 3
    }

    /// <summary>Contains one ROI binding for a video frame; a null prompt means predictor propagation. / 包含一帧的一个 ROI 绑定；空 Prompt 表示由 Predictor 传播。</summary>
    public sealed class VisualRoiVideoPromptPlanItem
    {
        internal VisualRoiVideoPromptPlanItem(VisualRoi roi, int objectIndex, PromptableSegmentationPrompt? prompt, bool propagate)
        {
            Roi = roi ?? throw new ArgumentNullException(nameof(roi));
            if (objectIndex < 0) throw new ArgumentOutOfRangeException(nameof(objectIndex));
            if (prompt == null && !propagate) throw new ArgumentException("A video ROI item without a prompt must be a propagation item.");
            ObjectIndex = objectIndex;
            Prompt = prompt;
            Propagate = propagate;
        }

        /// <summary>Gets the immutable ROI definition. / 获取不可变 ROI 定义。</summary>
        public VisualRoi Roi { get; }
        /// <summary>Gets the stable zero-based object slot assigned to this ROI. / 获取分配给此 ROI 的稳定零基对象槽位。</summary>
        public int ObjectIndex { get; }
        /// <summary>Gets the source-space prompt on initialize/correction frames. / 获取初始化/修正帧上的源图空间 Prompt。</summary>
        public PromptableSegmentationPrompt? Prompt { get; }
        /// <summary>Gets whether the external predictor should propagate this object state. / 获取外部 Predictor 是否应传播该对象状态。</summary>
        public bool Propagate { get; }
    }

    /// <summary>Describes one ordered, bounded video ROI prompt plan for an external SAM 2/SAM 3 predictor. / 描述一个供外部 SAM 2/SAM 3 Predictor 使用的有序有界视频 ROI Prompt 计划。</summary>
    public sealed class VisualRoiVideoPromptPlan
    {
        private readonly IReadOnlyList<VisualRoiVideoPromptPlanItem> _items;

        internal VisualRoiVideoPromptPlan(
            PromptableSegmentationProfile profile,
            VisualRoiSnapshot snapshot,
            long frameIndex,
            VisualRoiVideoFrameMode mode,
            IEnumerable<VisualRoiVideoPromptPlanItem> items)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (frameIndex < 0) throw new ArgumentOutOfRangeException(nameof(frameIndex));
            if (!Enum.IsDefined(typeof(VisualRoiVideoFrameMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = new ReadOnlyCollection<VisualRoiVideoPromptPlanItem>(items.ToList());
            FrameIndex = frameIndex;
            Mode = mode;
        }

        /// <summary>Gets the exact profile whose video state contract governs this plan. / 获取约束此计划的视频状态合同的精确 Profile。</summary>
        public PromptableSegmentationProfile Profile { get; }
        /// <summary>Gets the immutable ROI snapshot used for this frame. / 获取本帧使用的不可变 ROI 快照。</summary>
        public VisualRoiSnapshot Snapshot { get; }
        /// <summary>Gets the zero-based source frame index. / 获取从零开始的源帧索引。</summary>
        public long FrameIndex { get; }
        /// <summary>Gets the frame operation mode. / 获取帧操作模式。</summary>
        public VisualRoiVideoFrameMode Mode { get; }
        /// <summary>Gets the ROI bindings in deterministic snapshot order. / 获取按快照确定性顺序排列的 ROI 绑定。</summary>
        public IReadOnlyList<VisualRoiVideoPromptPlanItem> Items => _items;
        /// <summary>Gets only prompts that must be sent to the predictor on this frame. / 仅获取本帧必须发送给 Predictor 的 Prompt。</summary>
        public IReadOnlyList<PromptableSegmentationPrompt> Prompts => _items.Where(value => value.Prompt != null).Select(value => value.Prompt!).ToList().AsReadOnly();
        /// <summary>Gets whether a native predictor implementation is still required. / 获取是否仍需要 native Predictor 实现。</summary>
        public bool RequiresExternalPredictor => !Profile.Video!.Executable;
        /// <summary>Gets the audited blocker when the profile is contract-only. / 获取合同专用 Profile 的已审计 blocker。</summary>
        public string? Blocker => Profile.Video!.Blocker;
    }

    /// <summary>Builds ordered, state-safe ROI plans for SAM 2/SAM 3 video propagation without pretending that a native predictor exists. / 为 SAM 2/SAM 3 视频传播构建有序且状态安全的 ROI 计划，但不伪称已有 native Predictor。</summary>
    /// <remarks>The planner owns frame-order and snapshot invariants. Use <see cref="CreatePlan"/> followed by the external predictor and <see cref="Commit"/> so a cancelled or failed predictor call does not advance state; <see cref="PlanFrame"/> is the convenience all-in-one form. / Planner 负责帧顺序和快照不变量；使用 CreatePlan、外部 Predictor、Commit 三步可确保取消或失败不推进状态；PlanFrame 是一体化便利形式。</remarks>
    public sealed class VisualRoiVideoPromptPlanner
    {
        private readonly object _gate = new object();
        private readonly PromptableSegmentationProfile _profile;
        private long _lastFrameIndex = -1;
        private VisualSize? _sourceSize;
        private long? _snapshotVersion;
        private bool _initialized;

        /// <summary>Initializes a planner from an explicit SAM 2/SAM 3 video contract. / 根据显式 SAM 2/SAM 3 视频合同初始化 Planner。</summary>
        public VisualRoiVideoPromptPlanner(PromptableSegmentationProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (profile.Video == null || (profile.Capabilities & PromptableSegmentationCapabilities.VideoPropagation) == 0)
                throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "The profile does not declare a video propagation contract.", profileId: profile.ProfileId);
            if (profile.Family != PromptableSegmentationFamily.Sam2 && profile.Family != PromptableSegmentationFamily.Sam3)
                throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "The video ROI planner is reserved for SAM 2 and SAM 3 contracts.", profileId: profile.ProfileId);
        }

        /// <summary>Gets the exact profile bound to this planner. / 获取绑定到此 Planner 的精确 Profile。</summary>
        public PromptableSegmentationProfile Profile => _profile;
        /// <summary>Gets the last committed frame index, or -1 before initialization. / 获取最后提交的帧索引；初始化前为 -1。</summary>
        public long LastFrameIndex { get { lock (_gate) return _lastFrameIndex; } }
        /// <summary>Gets whether an initialize plan has been committed. / 获取是否已提交初始化计划。</summary>
        public bool IsInitialized { get { lock (_gate) return _initialized; } }

        /// <summary>Builds one deterministic frame plan without mutating state; call <see cref="Commit"/> after the external predictor succeeds. / 构建一帧确定性计划但不修改状态；外部 Predictor 成功后调用 Commit。</summary>
        public VisualRoiVideoPromptPlan CreatePlan(
            VisualRoiSnapshot snapshot,
            long frameIndex,
            VisualRoiVideoFrameMode mode,
            VisualRoiPromptOptions? promptOptions = null,
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (frameIndex < 0) throw new ArgumentOutOfRangeException(nameof(frameIndex));
            if (!Enum.IsDefined(typeof(VisualRoiVideoFrameMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (coordinateContext != null && coordinateContext.SourceSize != snapshot.SourceSize) throw new ArgumentException("The ROI coordinate context source size must match the snapshot source size.", nameof(coordinateContext));

            lock (_gate)
            {
                if (mode == VisualRoiVideoFrameMode.Initialize && _initialized) throw InvalidState("The video planner is already initialized; call Reset before starting a new video.");
                if (mode != VisualRoiVideoFrameMode.Initialize && !_initialized) throw InvalidState("An initialize plan must be committed before propagation or correction.");
                if (frameIndex <= _lastFrameIndex) throw InvalidState("Video frame indices must be strictly ascending.", "last=" + _lastFrameIndex + ";actual=" + frameIndex);
                if (frameIndex >= _profile.Video!.MaximumFrames) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "The video frame index exceeds the profile capacity.", profileId: _profile.ProfileId, technicalDetails: "maximumFrames=" + _profile.Video.MaximumFrames + ";frame=" + frameIndex);
                if (_sourceSize.HasValue && _sourceSize.Value != snapshot.SourceSize) throw InvalidState("Video ROI source size cannot change without Reset.", "expected=" + _sourceSize.Value.Width + "x" + _sourceSize.Value.Height + ";actual=" + snapshot.SourceSize.Width + "x" + snapshot.SourceSize.Height);
                if (_snapshotVersion.HasValue && _snapshotVersion.Value != snapshot.Version) throw InvalidState("Video ROI snapshot cannot change while predictor state is active; call Reset and reinitialize.", "expected=" + _snapshotVersion.Value + ";actual=" + snapshot.Version);

                var selected = snapshot.Rois.Where(roi => roi.Enabled && roi.InclusionMode == RoiInclusionMode.Include && roi.AppliesTo(VisualTaskId.PromptableVideoSegmentation)).ToList();
                if (selected.Count > _profile.Video.MaximumObjects) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "The applicable ROI count exceeds the video object capacity.", profileId: _profile.ProfileId, technicalDetails: "maximumObjects=" + _profile.Video.MaximumObjects + ";actual=" + selected.Count);
                var items = new List<VisualRoiVideoPromptPlanItem>(selected.Count);
                for (int index = 0; index < selected.Count; index++)
                {
                    VisualRoi roi = selected[index];
                    if (roi.ClassFilter.Count != 0) throw new VisualException(VisualErrorCodes.InputInvalid, "Video prompt ROIs cannot use class filters.", technicalDetails: "roiId=" + roi.Id);
                    PromptableSegmentationPrompt? prompt = mode == VisualRoiVideoFrameMode.Propagate ? null : VisualRoiPromptFactory.Create(snapshot, roi, promptOptions, roi.Id, coordinateContext);
                    items.Add(new VisualRoiVideoPromptPlanItem(roi, index, prompt, mode == VisualRoiVideoFrameMode.Propagate));
                }

                return new VisualRoiVideoPromptPlan(_profile, snapshot, frameIndex, mode, items);
            }
        }

        /// <summary>Commits a plan after the external SAM predictor has accepted the frame. / 外部 SAM Predictor 接受本帧后提交计划。</summary>
        public void Commit(VisualRoiVideoPromptPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Profile, _profile)) throw InvalidState("The video plan belongs to a different profile.");
            lock (_gate)
            {
                if (plan.Mode == VisualRoiVideoFrameMode.Initialize && _initialized) throw InvalidState("The video planner is already initialized; call Reset before starting a new video.");
                if (plan.Mode != VisualRoiVideoFrameMode.Initialize && !_initialized) throw InvalidState("An initialize plan must be committed before propagation or correction.");
                if (plan.FrameIndex <= _lastFrameIndex) throw InvalidState("Video frame indices must be strictly ascending.", "last=" + _lastFrameIndex + ";actual=" + plan.FrameIndex);
                if (plan.FrameIndex >= _profile.Video!.MaximumFrames) throw new VisualException(VisualErrorCodes.PromptableSegmentationLimitExceeded, "The video frame index exceeds the profile capacity.", profileId: _profile.ProfileId, technicalDetails: "maximumFrames=" + _profile.Video.MaximumFrames + ";frame=" + plan.FrameIndex);
                if (_sourceSize.HasValue && _sourceSize.Value != plan.Snapshot.SourceSize) throw InvalidState("Video ROI source size cannot change without Reset.");
                if (_snapshotVersion.HasValue && _snapshotVersion.Value != plan.Snapshot.Version) throw InvalidState("Video ROI snapshot cannot change while predictor state is active; call Reset and reinitialize.");
                _lastFrameIndex = plan.FrameIndex;
                _sourceSize = plan.Snapshot.SourceSize;
                _snapshotVersion = plan.Snapshot.Version;
                _initialized = true;
            }
        }

        /// <summary>Builds and commits one plan in one call for callers whose predictor operation is already serialized. / 为已自行串行化 Predictor 操作的调用方一次性构建并提交计划。</summary>
        public VisualRoiVideoPromptPlan PlanFrame(
            VisualRoiSnapshot snapshot,
            long frameIndex,
            VisualRoiVideoFrameMode mode,
            VisualRoiPromptOptions? promptOptions = null,
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            VisualRoiVideoPromptPlan plan = CreatePlan(snapshot, frameIndex, mode, promptOptions, coordinateContext);
            Commit(plan);
            return plan;
        }

        /// <summary>Clears frame-order and snapshot state before starting another video or ROI configuration. / 在开始另一个视频或 ROI 配置前清除帧序和快照状态。</summary>
        public void Reset()
        {
            lock (_gate)
            {
                _lastFrameIndex = -1;
                _sourceSize = null;
                _snapshotVersion = null;
                _initialized = false;
            }
        }

        private VisualException InvalidState(string message, string? details = null) => new VisualException(VisualErrorCodes.PromptableSegmentationStateInvalid, message, profileId: _profile.ProfileId, technicalDetails: details);
    }
}
