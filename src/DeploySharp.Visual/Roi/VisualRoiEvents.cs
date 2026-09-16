using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies an event emitted by the ROI video state machine. / 标识 ROI 视频状态机发出的事件。</summary>
    public enum VisualRoiEventKind
    {
        /// <summary>A track entered an Include ROI. / 跟踪目标进入 Include ROI。</summary>
        Entered = 0,
        /// <summary>A track left an Include ROI. / 跟踪目标离开 Include ROI。</summary>
        Exited = 1,
        /// <summary>A track remained inside an ROI for the configured dwell duration. / 跟踪目标在 ROI 内达到配置的停留时长。</summary>
        Dwell = 2,
        /// <summary>A track crossed a configured line. / 跟踪目标穿过配置的线。</summary>
        LineCrossed = 3
    }

    /// <summary>Controls line-crossing direction. / 控制穿线方向。</summary>
    public enum VisualRoiLineDirection
    {
        /// <summary>Accept either direction. / 接受任意方向。</summary>
        Both = 0,
        /// <summary>Accept movement from the line's left side to its right side. / 接受从线左侧到右侧的运动。</summary>
        Positive = 1,
        /// <summary>Accept movement from the line's right side to its left side. / 接受从线右侧到左侧的运动。</summary>
        Negative = 2
    }

    /// <summary>Controls observations whose timestamps are older than the last observation for a track. / 控制早于目标上次观测时间的观测。</summary>
    public enum VisualRoiOutOfOrderMode
    {
        /// <summary>Ignore the observation and keep state unchanged. / 忽略观测并保持状态不变。</summary>
        Ignore = 0,
        /// <summary>Reject the observation with an exception. / 以异常拒绝观测。</summary>
        Reject = 1
    }

    /// <summary>Controls whether existing track state is retained when the ROI snapshot is replaced. / 控制替换 ROI 快照时是否保留已有跟踪状态。</summary>
    public enum VisualRoiSnapshotUpdateMode
    {
        /// <summary>Keep state for ROI IDs that still exist. / 保留仍存在 ROI ID 的状态。</summary>
        PreserveTrackState = 0,
        /// <summary>Clear every track state and require fresh observations. / 清除全部跟踪状态并要求重新观测。</summary>
        ResetTrackState = 1,
        /// <summary>Keep state only for ROI IDs whose event-relevant definition is unchanged. / 仅保留事件相关定义未变化的 ROI ID 状态。</summary>
        ResetChangedRoiState = 2,
        /// <summary>Re-evaluate changed ROIs from each track's last source point without emitting a synthetic transition. / 根据目标最近源图位置重新评估已变化 ROI，且不合成虚假进出事件。</summary>
        ReevaluateTrackState = 3
    }

    /// <summary>Defines one directed or undirected line associated with an ROI. / 定义一条关联到 ROI 的有向或无向线。</summary>
    public sealed class VisualRoiLine
    {
        /// <summary>Initializes a line. / 初始化线。</summary>
        public VisualRoiLine(string id, string roiId, PointF start, PointF end, VisualRoiLineDirection direction = VisualRoiLineDirection.Both)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A line id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            Validate(start, nameof(start));
            Validate(end, nameof(end));
            if (Math.Abs(end.X - start.X) <= 0.000001f && Math.Abs(end.Y - start.Y) <= 0.000001f) throw new ArgumentException("A line must have positive length.", nameof(end));
            if (!Enum.IsDefined(typeof(VisualRoiLineDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            Id = id;
            RoiId = roiId;
            Start = start;
            End = end;
            Direction = direction;
        }

        /// <summary>Gets the stable line identifier. / 获取稳定线标识。</summary>
        public string Id { get; }
        /// <summary>Gets the associated ROI identifier. / 获取关联 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets the line start point. / 获取线起点。</summary>
        public PointF Start { get; }
        /// <summary>Gets the line end point. / 获取线终点。</summary>
        public PointF End { get; }
        /// <summary>Gets accepted crossing direction. / 获取接受的穿越方向。</summary>
        public VisualRoiLineDirection Direction { get; }

        private static void Validate(PointF point, string parameterName)
        {
            VisualGuard.Finite(point.X, parameterName);
            VisualGuard.Finite(point.Y, parameterName);
        }
    }

    /// <summary>Provides an external tracker observation for one video frame. / 为一个视频帧提供外部跟踪器观测。</summary>
    public sealed class VisualRoiTrackObservation
    {
        /// <summary>Initializes a track observation. / 初始化跟踪观测。</summary>
        public VisualRoiTrackObservation(string trackId, RectangleF bounds, DateTimeOffset timestamp, long frameIndex = -1, PointF? centroid = null)
        {
            if (string.IsNullOrWhiteSpace(trackId)) throw new ArgumentException("A track id is required.", nameof(trackId));
            if (trackId.Length > 256) throw new ArgumentOutOfRangeException(nameof(trackId));
            Validate(bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentOutOfRangeException(nameof(bounds));
            if (centroid.HasValue)
            {
                VisualGuard.Finite(centroid.Value.X, nameof(centroid));
                VisualGuard.Finite(centroid.Value.Y, nameof(centroid));
            }
            TrackId = trackId;
            Bounds = bounds;
            Timestamp = timestamp;
            FrameIndex = frameIndex;
            Centroid = centroid ?? new PointF(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
        }

        /// <summary>Gets the external tracker identifier. / 获取外部跟踪器标识。</summary>
        public string TrackId { get; }
        /// <summary>Gets the source-space target bounds. / 获取源图目标边界。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets the observation timestamp. / 获取观测时间戳。</summary>
        public DateTimeOffset Timestamp { get; }
        /// <summary>Gets the optional source frame index. / 获取可选源帧索引。</summary>
        public long FrameIndex { get; }
        /// <summary>Gets the point used for ROI and line tests. / 获取用于 ROI 和穿线判断的点。</summary>
        public PointF Centroid { get; }

        private static void Validate(RectangleF value)
        {
            VisualGuard.Finite(value.X, nameof(value));
            VisualGuard.Finite(value.Y, nameof(value));
            VisualGuard.Finite(value.Width, nameof(value));
            VisualGuard.Finite(value.Height, nameof(value));
        }
    }

    /// <summary>Configures bounded video ROI event processing. / 配置有界视频 ROI 事件处理。</summary>
    public sealed class VisualRoiEventOptions
    {
        /// <summary>Initializes event options. / 初始化事件选项。</summary>
        public VisualRoiEventOptions(TimeSpan? dwellDuration = null, TimeSpan? debounceDuration = null, TimeSpan? cooldownDuration = null, TimeSpan? missingTrackTimeout = null, int maximumTracks = 10000, VisualRoiOutOfOrderMode outOfOrderMode = VisualRoiOutOfOrderMode.Ignore, int minimumStableObservations = 1, float hysteresisPixels = 0)
        {
            DwellDuration = ValidateDuration(dwellDuration ?? TimeSpan.Zero, nameof(dwellDuration));
            DebounceDuration = ValidateDuration(debounceDuration ?? TimeSpan.Zero, nameof(debounceDuration));
            CooldownDuration = ValidateDuration(cooldownDuration ?? TimeSpan.Zero, nameof(cooldownDuration));
            MissingTrackTimeout = ValidatePositiveDuration(missingTrackTimeout ?? TimeSpan.FromSeconds(2), nameof(missingTrackTimeout));
            if (maximumTracks <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTracks));
            if (!Enum.IsDefined(typeof(VisualRoiOutOfOrderMode), outOfOrderMode)) throw new ArgumentOutOfRangeException(nameof(outOfOrderMode));
            if (minimumStableObservations <= 0) throw new ArgumentOutOfRangeException(nameof(minimumStableObservations));
            VisualGuard.Finite(hysteresisPixels, nameof(hysteresisPixels));
            if (hysteresisPixels < 0) throw new ArgumentOutOfRangeException(nameof(hysteresisPixels));
            MaximumTracks = maximumTracks;
            OutOfOrderMode = outOfOrderMode;
            MinimumStableObservations = minimumStableObservations;
            HysteresisPixels = hysteresisPixels;
        }

        /// <summary>Gets the time required before a Dwell event. / 获取触发停留事件所需时长。</summary>
        public TimeSpan DwellDuration { get; }
        /// <summary>Gets the stability duration required for enter/exit transitions. / 获取进入/离开状态稳定所需时长。</summary>
        public TimeSpan DebounceDuration { get; }
        /// <summary>Gets the minimum interval between same-key events. / 获取相同事件键之间的最小间隔。</summary>
        public TimeSpan CooldownDuration { get; }
        /// <summary>Gets the time after which a missing track is expired. / 获取目标丢失后的过期时长。</summary>
        public TimeSpan MissingTrackTimeout { get; }
        /// <summary>Gets the maximum tracked IDs retained by the processor. / 获取处理器保留的最大跟踪 ID 数。</summary>
        public int MaximumTracks { get; }
        /// <summary>Gets the out-of-order timestamp policy. / 获取乱序时间戳策略。</summary>
        public VisualRoiOutOfOrderMode OutOfOrderMode { get; }
        /// <summary>Gets the minimum consecutive observations required for an enter/exit transition. / 获取进入或离开状态所需的最少连续观测次数。</summary>
        public int MinimumStableObservations { get; }
        /// <summary>Gets the exit-only spatial hysteresis distance in source pixels. Entry still requires the point inside the ROI. / 获取仅用于离开判定的源像素空间滞回距离；进入仍要求点位于 ROI 内。</summary>
        public float HysteresisPixels { get; }

        private static TimeSpan ValidateDuration(TimeSpan value, string parameterName)
        {
            if (value < TimeSpan.Zero || value == Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        private static TimeSpan ValidatePositiveDuration(TimeSpan value, string parameterName)
        {
            if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    /// <summary>Represents one deterministic ROI video event. / 表示一个确定性的 ROI 视频事件。</summary>
    public sealed class VisualRoiEvent
    {
        internal VisualRoiEvent(VisualRoiEventKind kind, string trackId, string roiId, DateTimeOffset timestamp, long frameIndex, PointF point, TimeSpan insideDuration, string? lineId, long count)
        {
            Kind = kind;
            TrackId = trackId;
            RoiId = roiId;
            Timestamp = timestamp;
            FrameIndex = frameIndex;
            Point = point;
            InsideDuration = insideDuration;
            LineId = lineId;
            Count = count;
        }

        /// <summary>Gets event kind. / 获取事件类型。</summary>
        public VisualRoiEventKind Kind { get; }
        /// <summary>Gets external track ID. / 获取外部跟踪 ID。</summary>
        public string TrackId { get; }
        /// <summary>Gets ROI identifier. / 获取 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets event timestamp. / 获取事件时间戳。</summary>
        public DateTimeOffset Timestamp { get; }
        /// <summary>Gets source frame index. / 获取源帧索引。</summary>
        public long FrameIndex { get; }
        /// <summary>Gets event point in source pixels. / 获取源像素事件点。</summary>
        public PointF Point { get; }
        /// <summary>Gets stable inside duration at event time. / 获取事件时刻的稳定区域内时长。</summary>
        public TimeSpan InsideDuration { get; }
        /// <summary>Gets crossed line ID, or null for area events. / 获取穿越线标识；区域事件为空。</summary>
        public string? LineId { get; }
        /// <summary>Gets the cumulative count for this ROI and event kind. / 获取此 ROI 和事件类型的累计计数。</summary>
        public long Count { get; }
    }

    /// <summary>Processes externally tracked observations against immutable ROI geometry. / 根据不可变 ROI 几何处理外部跟踪观测。</summary>
    public sealed class VisualRoiEventProcessor
    {
        private readonly object _gate = new object();
        private VisualRoiSnapshot _snapshot;
        private readonly VisualTaskId _task;
        private readonly VisualRoiEventOptions _options;
        private VisualRoiCoordinateContext? _coordinateContext;
        private IReadOnlyList<Region> _regions;
        private IReadOnlyList<VisualRoiLine> _lines;
        private readonly Dictionary<string, TrackState> _tracks = new Dictionary<string, TrackState>(StringComparer.Ordinal);
        private readonly Dictionary<EventKey, DateTimeOffset> _lastEmitted = new Dictionary<EventKey, DateTimeOffset>();
        private readonly Dictionary<CountKey, long> _counts = new Dictionary<CountKey, long>();

        /// <summary>Initializes a processor for one Visual task. / 为一个 Visual 任务初始化处理器。</summary>
        public VisualRoiEventProcessor(VisualRoiSnapshot snapshot, VisualTaskId task, IEnumerable<VisualRoiLine>? lines = null, VisualRoiEventOptions? options = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (task.IsEmpty) throw new ArgumentException("A visual task is required.", nameof(task));
            _task = task;
            _options = options ?? new VisualRoiEventOptions();
            ValidateCoordinateContext(snapshot, coordinateContext);
            _coordinateContext = coordinateContext;
            _regions = BuildRegions(snapshot, coordinateContext);
            _lines = BuildLines(lines, _regions);
        }

        /// <summary>Gets the immutable snapshot version currently used by the processor. / 获取处理器当前使用的不可变快照版本。</summary>
        public long SnapshotVersion
        {
            get { lock (_gate) return _snapshot.Version; }
        }

        /// <summary>Gets the current immutable ROI snapshot. / 获取当前不可变 ROI 快照。</summary>
        public VisualRoiSnapshot Snapshot
        {
            get { lock (_gate) return _snapshot; }
        }

        /// <summary>Gets the number of live track states retained by the processor. / 获取处理器当前保留的活动跟踪状态数。</summary>
        /// <remarks>This value is bounded by <see cref="VisualRoiEventOptions.MaximumTracks"/> and is useful for long-running video health checks. / 该值受 MaximumTracks 限制，可用于长时间视频运行健康检查。</remarks>
        public int ActiveTrackCount
        {
            get { lock (_gate) return _tracks.Count; }
        }

        /// <summary>Gets the number of per-track cooldown entries retained by the processor. / 获取处理器当前保留的逐跟踪冷却条目数。</summary>
        /// <remarks>Expired tracks remove their cooldown entries, preventing unbounded growth when external track IDs rotate. / 目标过期时会删除对应冷却条目，避免外部跟踪 ID 轮换造成无界增长。</remarks>
        public int CooldownEntryCount
        {
            get { lock (_gate) return _lastEmitted.Count; }
        }

        /// <summary>Gets the number of cumulative counter keys. / 获取累计计数键数量。</summary>
        /// <remarks>This is bounded by configured ROI and event-kind definitions. / 该值受 ROI 和事件类型定义数量限制。</remarks>
        public int CountKeyCount
        {
            get { lock (_gate) return _counts.Count; }
        }

        /// <summary>Atomically replaces the ROI snapshot and line definitions. Existing states are retained by ROI ID by default. / 原子替换 ROI 快照和线定义；默认按 ROI ID 保留已有状态。</summary>
        public long UpdateSnapshot(VisualRoiSnapshot snapshot, IEnumerable<VisualRoiLine>? lines = null, VisualRoiSnapshotUpdateMode updateMode = VisualRoiSnapshotUpdateMode.PreserveTrackState, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!Enum.IsDefined(typeof(VisualRoiSnapshotUpdateMode), updateMode)) throw new ArgumentOutOfRangeException(nameof(updateMode));
            lock (_gate)
            {
                if (snapshot.SourceSize != _snapshot.SourceSize) throw new ArgumentException("The replacement ROI snapshot must use the same source size.", nameof(snapshot));
                VisualRoiCoordinateContext? effectiveContext = coordinateContext ?? _coordinateContext;
                ValidateCoordinateContext(snapshot, effectiveContext);
                IReadOnlyList<Region> regions = BuildRegions(snapshot, effectiveContext);
                IReadOnlyList<VisualRoiLine> copiedLines = BuildLines(lines, regions);
                bool linesChanged = (updateMode == VisualRoiSnapshotUpdateMode.ResetChangedRoiState || updateMode == VisualRoiSnapshotUpdateMode.ReevaluateTrackState) && !LinesEqual(_lines, copiedLines);
                if (updateMode == VisualRoiSnapshotUpdateMode.ResetTrackState)
                {
                    _tracks.Clear();
                    _lastEmitted.Clear();
                }
                else
                {
                    var validIds = new HashSet<string>(regions.Select(value => value.Roi.Id), StringComparer.Ordinal);
                    var changedIds = updateMode == VisualRoiSnapshotUpdateMode.ResetChangedRoiState || updateMode == VisualRoiSnapshotUpdateMode.ReevaluateTrackState
                        ? GetChangedRoiIds(_snapshot, snapshot)
                        : new HashSet<string>(StringComparer.Ordinal);
                    if ((updateMode == VisualRoiSnapshotUpdateMode.ResetChangedRoiState || updateMode == VisualRoiSnapshotUpdateMode.ReevaluateTrackState) && !CoordinateContextsEqual(_coordinateContext, effectiveContext))
                    {
                        // TileLocal and World geometries are interpreted through the external context.
                        // A context change therefore invalidates their state even when the ROI JSON itself
                        // is unchanged; SourcePixels/Normalized ROIs remain eligible for state retention.
                        foreach (VisualRoi roi in snapshot.Rois.Where(value => value.CoordinateSpace == RoiCoordinateSpace.TileLocal || value.CoordinateSpace == RoiCoordinateSpace.World)) changedIds.Add(roi.Id);
                    }
                    foreach (TrackState state in _tracks.Values)
                    {
                        foreach (string roiId in state.RegionStates.Keys.Where(value => !validIds.Contains(value) || updateMode != VisualRoiSnapshotUpdateMode.ReevaluateTrackState && changedIds.Contains(value)).ToList()) state.RegionStates.Remove(roiId);
                        if (updateMode == VisualRoiSnapshotUpdateMode.ReevaluateTrackState)
                        {
                            foreach (string roiId in changedIds)
                            {
                                Region? region = regions.FirstOrDefault(value => string.Equals(value.Roi.Id, roiId, StringComparison.Ordinal));
                                if (region == null || !state.RegionStates.TryGetValue(roiId, out RegionState? current)) continue;
                                current.Rebase(IsInside(region.Geometry, state.LastPoint, stableInside: false), state.LastTimestamp);
                            }
                        }
                        if (linesChanged) state.ResetLineHistory();
                    }
                    foreach (EventKey key in _lastEmitted.Keys.Where(value => !validIds.Contains(value.RoiId) || changedIds.Contains(value.RoiId) || linesChanged && value.Kind == VisualRoiEventKind.LineCrossed).ToList()) _lastEmitted.Remove(key);
                }
                _snapshot = snapshot;
                _coordinateContext = effectiveContext;
                _regions = regions;
                _lines = copiedLines;
                return snapshot.Version;
            }
        }

        /// <summary>Processes one frame observation and returns newly emitted events. / 处理一帧观测并返回新发出的事件。</summary>
        public IReadOnlyList<VisualRoiEvent> Process(VisualRoiTrackObservation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            lock (_gate)
            {
                return ProcessCore(observation);
            }
        }

        /// <summary>
        /// Processes all observations belonging to one video frame under one lock and returns events in deterministic input order.
        /// The method validates duplicate IDs, timestamp ordering and track capacity before mutating state, so a malformed frame
        /// cannot partially update the processor. / 在一次锁内处理同一视频帧的全部观测，并按确定性输入顺序返回事件；
        /// 方法会在修改状态前校验重复 ID、时间戳顺序和目标容量，格式错误的帧不会部分更新处理器。
        /// </summary>
        public IReadOnlyList<VisualRoiEvent> ProcessFrame(IReadOnlyList<VisualRoiTrackObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            lock (_gate)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                int newTracks = 0;
                DateTimeOffset? frameTimestamp = null;
                long frameIndex = long.MinValue;
                for (int index = 0; index < observations.Count; index++)
                {
                    VisualRoiTrackObservation observation = observations[index] ?? throw new ArgumentException("Frame observations cannot contain null values.", nameof(observations));
                    if (!ids.Add(observation.TrackId)) throw new VisualException(VisualErrorCodes.InputInvalid, "A frame cannot contain duplicate ROI track IDs.", technicalDetails: "trackId=" + observation.TrackId);
                    if (!frameTimestamp.HasValue) frameTimestamp = observation.Timestamp;
                    else if (observation.Timestamp != frameTimestamp.Value) throw new VisualException(VisualErrorCodes.InputInvalid, "All observations in one ROI frame must use the same timestamp.", technicalDetails: "trackId=" + observation.TrackId);
                    if (index == 0) frameIndex = observation.FrameIndex;
                    else if (observation.FrameIndex >= 0 && frameIndex >= 0 && observation.FrameIndex != frameIndex) throw new VisualException(VisualErrorCodes.InputInvalid, "All observations in one ROI frame must use the same frame index.", technicalDetails: "trackId=" + observation.TrackId);
                    if (_tracks.TryGetValue(observation.TrackId, out TrackState? state) && observation.Timestamp < state.LastTimestamp && _options.OutOfOrderMode == VisualRoiOutOfOrderMode.Reject)
                    {
                        throw new VisualException(VisualErrorCodes.InputInvalid, "ROI track observations must be monotonic per TrackId.", technicalDetails: "trackId=" + observation.TrackId);
                    }
                    if (!_tracks.ContainsKey(observation.TrackId)) newTracks++;
                }
                if (_tracks.Count > _options.MaximumTracks - newTracks) throw new VisualException(VisualErrorCodes.InputInvalid, "The ROI event track limit has been reached.", technicalDetails: "maximumTracks=" + _options.MaximumTracks);

                var events = new List<VisualRoiEvent>();
                for (int index = 0; index < observations.Count; index++)
                {
                    VisualRoiTrackObservation observation = observations[index];
                    if (_tracks.TryGetValue(observation.TrackId, out TrackState? state) && observation.Timestamp < state.LastTimestamp)
                    {
                        // Ignore mode was validated above and deliberately keeps the existing state unchanged.
                        continue;
                    }
                    events.AddRange(ProcessCore(observation));
                }
                return new ReadOnlyCollection<VisualRoiEvent>(events);
            }
        }

        private IReadOnlyList<VisualRoiEvent> ProcessCore(VisualRoiTrackObservation observation)
        {
            if (_tracks.TryGetValue(observation.TrackId, out TrackState? existing) && observation.Timestamp < existing.LastTimestamp)
            {
                if (_options.OutOfOrderMode == VisualRoiOutOfOrderMode.Reject) throw new VisualException(VisualErrorCodes.InputInvalid, "ROI track observations must be monotonic per TrackId.", technicalDetails: "trackId=" + observation.TrackId);
                return Array.Empty<VisualRoiEvent>();
            }
            if (!_tracks.TryGetValue(observation.TrackId, out TrackState? state))
            {
                if (_tracks.Count >= _options.MaximumTracks) throw new VisualException(VisualErrorCodes.InputInvalid, "The ROI event track limit has been reached.", technicalDetails: "maximumTracks=" + _options.MaximumTracks);
                state = new TrackState(observation.TrackId, observation.Timestamp, observation.Centroid);
                _tracks.Add(observation.TrackId, state);
            }
            var events = new List<VisualRoiEvent>();
            foreach (Region region in _regions)
            {
                bool stableInside = state.RegionStates.TryGetValue(region.Roi.Id, out RegionState? previousRegionState) && previousRegionState.StableInside;
                bool inside = IsInside(region.Geometry, observation.Centroid, stableInside);
                RegionState regionState = state.GetRegionState(region.Roi.Id, observation.Timestamp, inside);
                if (inside != regionState.CandidateInside)
                {
                    regionState.CandidateInside = inside;
                    regionState.CandidateSince = observation.Timestamp;
                    regionState.CandidateObservations = 1;
                }
                else
                {
                    regionState.CandidateObservations = checked(regionState.CandidateObservations + 1);
                }
                if (regionState.StableInside != regionState.CandidateInside && regionState.CandidateObservations >= _options.MinimumStableObservations && observation.Timestamp - regionState.CandidateSince >= _options.DebounceDuration)
                {
                    regionState.StableInside = regionState.CandidateInside;
                    regionState.StableSince = observation.Timestamp;
                    regionState.DwellRaised = false;
                    AddIfEmitted(events, Emit(regionState.StableInside ? VisualRoiEventKind.Entered : VisualRoiEventKind.Exited, observation, region.Roi.Id, regionState.StableInside ? TimeSpan.Zero : observation.Timestamp - regionState.InsideSince, null));
                    if (regionState.StableInside) regionState.InsideSince = observation.Timestamp;
                }
                if (regionState.StableInside && _options.DwellDuration > TimeSpan.Zero && !regionState.DwellRaised && observation.Timestamp - regionState.InsideSince >= _options.DwellDuration)
                {
                    regionState.DwellRaised = true;
                    AddIfEmitted(events, Emit(VisualRoiEventKind.Dwell, observation, region.Roi.Id, observation.Timestamp - regionState.InsideSince, null));
                }
            }
            // Evaluate every configured line against the same segment for this frame. Updating
            // PreviousPoint inside the loop would make later lines compare the current point with itself.
            bool hadPreviousPoint = state.HasPreviousPoint;
            PointF previousPoint = state.PreviousPoint;
            foreach (VisualRoiLine line in _lines)
            {
                if (!hadPreviousPoint) continue;
                int previousSide = Side(line.Start, line.End, previousPoint);
                int currentSide = Side(line.Start, line.End, observation.Centroid);
                if (previousSide != 0 && currentSide != 0 && previousSide != currentSide && Accepts(line.Direction, previousSide, currentSide) && TryIntersectSegment(line.Start, line.End, previousPoint, observation.Centroid, out PointF intersection))
                {
                    AddIfEmitted(events, Emit(VisualRoiEventKind.LineCrossed, observation, line.RoiId, TimeSpan.Zero, line.Id, intersection));
                }
            }
            state.PreviousPoint = observation.Centroid;
            state.HasPreviousPoint = true;
            state.LastTimestamp = observation.Timestamp;
            state.LastSeen = observation.Timestamp;
            state.LastFrameIndex = observation.FrameIndex;
            state.LastPoint = observation.Centroid;
            return new ReadOnlyCollection<VisualRoiEvent>(events);
        }

        /// <summary>Expires tracks missing beyond the configured timeout. / 使超过配置超时未出现的目标过期。</summary>
        public IReadOnlyList<VisualRoiEvent> Advance(DateTimeOffset timestamp, long frameIndex = -1)
        {
            lock (_gate)
            {
                var events = new List<VisualRoiEvent>();
                foreach (TrackState state in _tracks.Values.ToList())
                {
                    if (timestamp < state.LastTimestamp)
                    {
                        if (_options.OutOfOrderMode == VisualRoiOutOfOrderMode.Reject) throw new VisualException(VisualErrorCodes.InputInvalid, "ROI event time cannot move backwards.");
                        continue;
                    }
                    if (timestamp - state.LastSeen < _options.MissingTrackTimeout) continue;
                    foreach (Region region in _regions)
                    {
                        if (!state.RegionStates.TryGetValue(region.Roi.Id, out RegionState? regionState) || !regionState.StableInside) continue;
                        regionState.StableInside = false;
                        regionState.CandidateInside = false;
                        AddIfEmitted(events, Emit(VisualRoiEventKind.Exited, new VisualRoiTrackObservation(state.TrackId, new RectangleF(state.LastPoint.X, state.LastPoint.Y, 1, 1), timestamp, frameIndex, state.LastPoint), region.Roi.Id, timestamp - regionState.InsideSince, null));
                    }
                    foreach (EventKey key in _lastEmitted.Keys.Where(value => string.Equals(value.TrackId, state.TrackId, StringComparison.Ordinal)).ToList()) _lastEmitted.Remove(key);
                    _tracks.Remove(state.TrackId);
                }
                return new ReadOnlyCollection<VisualRoiEvent>(events);
            }
        }

        /// <summary>Gets a stable copy of cumulative event counts. / 获取累计事件计数的稳定副本。</summary>
        public IReadOnlyDictionary<string, long> GetCounts()
        {
            lock (_gate)
            {
                var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
                foreach (Region region in _regions)
                {
                    foreach (VisualRoiEventKind kind in Enum.GetValues(typeof(VisualRoiEventKind)))
                    {
                        var key = new CountKey(region.Roi.Id, kind);
                        result[key.ToString()] = CurrentCount(key);
                    }
                }
                foreach (KeyValuePair<CountKey, long> pair in _counts) result[pair.Key.ToString()] = pair.Value;
                return new ReadOnlyDictionary<string, long>(result);
            }
        }

        private static void AddIfEmitted(ICollection<VisualRoiEvent> events, VisualRoiEvent? value)
        {
            if (value != null) events.Add(value);
        }

        private VisualRoiEvent? Emit(VisualRoiEventKind kind, VisualRoiTrackObservation observation, string roiId, TimeSpan insideDuration, string? lineId, PointF? point = null)
        {
            var eventKey = new EventKey(observation.TrackId, roiId, kind, lineId);
            if (_options.CooldownDuration > TimeSpan.Zero && _lastEmitted.TryGetValue(eventKey, out DateTimeOffset last) && observation.Timestamp - last < _options.CooldownDuration) return null;
            _lastEmitted[eventKey] = observation.Timestamp;
            var countKey = new CountKey(roiId, kind);
            long count = CurrentCount(countKey) + 1;
            _counts[countKey] = count;
            return new VisualRoiEvent(kind, observation.TrackId, roiId, observation.Timestamp, observation.FrameIndex, point ?? observation.Centroid, insideDuration, lineId, count);
        }

        private long CurrentCount(CountKey key) => _counts.TryGetValue(key, out long value) ? value : 0;

        private bool IsInside(IVisualRoiGeometry geometry, PointF point, bool stableInside)
        {
            if (geometry.Contains(point)) return true;
            if (!stableInside || _options.HysteresisPixels <= 0 || geometry.Kind == VisualRoiGeometryKind.Mask) return false;
            return DistanceToBoundary(geometry.Points, point) <= _options.HysteresisPixels;
        }

        private static float DistanceToBoundary(IReadOnlyList<PointF> points, PointF point)
        {
            if (points == null || points.Count < 2) return float.PositiveInfinity;
            float minimum = float.PositiveInfinity;
            for (int index = 0; index < points.Count; index++)
            {
                PointF start = points[index];
                PointF end = points[(index + 1) % points.Count];
                float dx = end.X - start.X;
                float dy = end.Y - start.Y;
                float denominator = (dx * dx) + (dy * dy);
                float parameter = denominator <= 0 ? 0 : (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / denominator;
                parameter = Math.Max(0, Math.Min(1, parameter));
                float nearestX = start.X + (parameter * dx);
                float nearestY = start.Y + (parameter * dy);
                float distance = (float)Math.Sqrt(((point.X - nearestX) * (point.X - nearestX)) + ((point.Y - nearestY) * (point.Y - nearestY)));
                if (distance < minimum) minimum = distance;
            }
            return minimum;
        }

        private IReadOnlyList<Region> BuildRegions(VisualRoiSnapshot snapshot, VisualRoiCoordinateContext? coordinateContext)
        {
            var regions = new List<Region>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include || !roi.AppliesTo(_task)) continue;
                if (roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World)
                {
                    if (coordinateContext == null) throw new NotSupportedException("TileLocal and World video ROIs require an explicit projection context.");
                    regions.Add(new Region(roi, snapshot.Resolve(roi, coordinateContext)));
                    continue;
                }
                if (roi.CoordinateSpace == RoiCoordinateSpace.ModelInput) throw new NotSupportedException("Video ROI events do not support ModelInput coordinates; resolve them against a concrete frame before constructing the processor.");
                regions.Add(new Region(roi, snapshot.Resolve(roi)));
            }
            return new ReadOnlyCollection<Region>(regions);
        }

        private static IReadOnlyList<VisualRoiLine> BuildLines(IEnumerable<VisualRoiLine>? lines, IReadOnlyList<Region> regions)
        {
            var copiedLines = new List<VisualRoiLine>();
            if (lines != null)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (VisualRoiLine line in lines)
                {
                    if (line == null) throw new ArgumentException("Line collections cannot contain null values.", nameof(lines));
                    if (!ids.Add(line.Id)) throw new ArgumentException("Line identifiers must be unique.", nameof(lines));
                    if (!regions.Any(value => string.Equals(value.Roi.Id, line.RoiId, StringComparison.Ordinal))) throw new ArgumentException("A line must reference an enabled Include ROI that applies to the selected task.", nameof(lines));
                    copiedLines.Add(line);
                }
            }
            return new ReadOnlyCollection<VisualRoiLine>(copiedLines);
        }

        private static void ValidateCoordinateContext(VisualRoiSnapshot snapshot, VisualRoiCoordinateContext? context)
        {
            if (context != null && context.SourceSize != snapshot.SourceSize) throw new ArgumentException("The ROI coordinate context source size must match the snapshot source size.", nameof(context));
        }

        private static bool CoordinateContextsEqual(VisualRoiCoordinateContext? before, VisualRoiCoordinateContext? after)
        {
            if (before == null || after == null) return before == null && after == null;
            return before.EquivalentTo(after);
        }

        private static bool LinesEqual(IReadOnlyList<VisualRoiLine> before, IReadOnlyList<VisualRoiLine> after)
        {
            if (before.Count != after.Count) return false;
            var current = after.ToDictionary(value => value.Id, StringComparer.Ordinal);
            foreach (VisualRoiLine line in before)
            {
                if (!current.TryGetValue(line.Id, out VisualRoiLine? other)) return false;
                if (!string.Equals(line.RoiId, other.RoiId, StringComparison.Ordinal) || line.Direction != other.Direction || line.Start != other.Start || line.End != other.End) return false;
            }
            return true;
        }

        private static HashSet<string> GetChangedRoiIds(VisualRoiSnapshot before, VisualRoiSnapshot after)
        {
            var changed = new HashSet<string>(StringComparer.Ordinal);
            var previous = before.Rois.ToDictionary(value => value.Id, StringComparer.Ordinal);
            foreach (VisualRoi current in after.Rois)
            {
                if (!previous.TryGetValue(current.Id, out VisualRoi? old) || !EventDefinitionEquals(old, current)) changed.Add(current.Id);
            }
            return changed;
        }

        private static bool EventDefinitionEquals(VisualRoi before, VisualRoi after)
        {
            if (before.Enabled != after.Enabled || before.InclusionMode != after.InclusionMode || before.ExecutionMode != after.ExecutionMode || before.CoordinateSpace != after.CoordinateSpace || before.HitTestMode != after.HitTestMode || before.HitThreshold != after.HitThreshold || before.Margin != after.Margin || before.ConfidenceOverride != after.ConfidenceOverride) return false;
            if (!before.TaskFilter.Select(value => value.Value).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(after.TaskFilter.Select(value => value.Value).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)) return false;
            if (!before.ClassFilter.OrderBy(value => value).SequenceEqual(after.ClassFilter.OrderBy(value => value))) return false;
            if (before.Geometry.Kind != after.Geometry.Kind || before.Geometry.Points.Count != after.Geometry.Points.Count) return false;
            for (int index = 0; index < before.Geometry.Points.Count; index++) if (before.Geometry.Points[index] != after.Geometry.Points[index]) return false;
            if (before.Geometry is MaskRoiGeometry beforeMask && after.Geometry is MaskRoiGeometry afterMask && (beforeMask.SourceSize != afterMask.SourceSize || !beforeMask.ToArray().SequenceEqual(afterMask.ToArray()))) return false;
            return before.Geometry is not MaskRoiGeometry && after.Geometry is not MaskRoiGeometry || before.Geometry is MaskRoiGeometry && after.Geometry is MaskRoiGeometry;
        }

        private static bool Accepts(VisualRoiLineDirection direction, int previous, int current)
        {
            if (direction == VisualRoiLineDirection.Both) return true;
            bool positive = previous < current;
            return direction == VisualRoiLineDirection.Positive ? positive : !positive;
        }

        private static int Side(PointF start, PointF end, PointF point)
        {
            float cross = ((end.X - start.X) * (point.Y - start.Y)) - ((end.Y - start.Y) * (point.X - start.X));
            return cross > .000001f ? 1 : cross < -.000001f ? -1 : 0;
        }

        private static bool TryIntersectSegment(PointF lineStart, PointF lineEnd, PointF movementStart, PointF movementEnd, out PointF intersection)
        {
            float lineX = lineEnd.X - lineStart.X;
            float lineY = lineEnd.Y - lineStart.Y;
            float movementX = movementEnd.X - movementStart.X;
            float movementY = movementEnd.Y - movementStart.Y;
            float offsetX = movementStart.X - lineStart.X;
            float offsetY = movementStart.Y - lineStart.Y;
            float determinant = (lineX * movementY) - (lineY * movementX);
            if (Math.Abs(determinant) <= .000001f)
            {
                intersection = default(PointF);
                return false;
            }

            float lineParameter = ((offsetX * movementY) - (offsetY * movementX)) / determinant;
            float movementParameter = ((offsetX * lineY) - (offsetY * lineX)) / determinant;
            const float tolerance = .000001f;
            if (lineParameter < -tolerance || lineParameter > 1 + tolerance || movementParameter < -tolerance || movementParameter > 1 + tolerance)
            {
                intersection = default(PointF);
                return false;
            }

            intersection = new PointF(lineStart.X + (lineParameter * lineX), lineStart.Y + (lineParameter * lineY));
            return true;
        }

        private sealed class Region
        {
            public Region(VisualRoi roi, IVisualRoiGeometry geometry) { Roi = roi; Geometry = geometry; }
            public VisualRoi Roi { get; }
            public IVisualRoiGeometry Geometry { get; }
        }

        private sealed class TrackState
        {
            public TrackState(string trackId, DateTimeOffset timestamp, PointF point) { TrackId = trackId; LastTimestamp = timestamp; LastSeen = timestamp; LastPoint = point; }
            public string TrackId { get; }
            public DateTimeOffset LastTimestamp { get; set; }
            public DateTimeOffset LastSeen { get; set; }
            public long LastFrameIndex { get; set; }
            public PointF LastPoint { get; set; }
            public PointF PreviousPoint { get; set; }
            public bool HasPreviousPoint { get; set; }
            public Dictionary<string, RegionState> RegionStates { get; } = new Dictionary<string, RegionState>(StringComparer.Ordinal);
            public RegionState GetRegionState(string roiId, DateTimeOffset timestamp, bool inside)
            {
                if (!RegionStates.TryGetValue(roiId, out RegionState? state))
                {
                    state = new RegionState(inside, timestamp);
                    RegionStates.Add(roiId, state);
                }
                return state;
            }
            public void ResetLineHistory()
            {
                HasPreviousPoint = false;
                PreviousPoint = default(PointF);
            }
        }

        private sealed class RegionState
        {
            public RegionState(bool inside, DateTimeOffset timestamp) { CandidateInside = inside; CandidateObservations = 0; StableInside = false; CandidateSince = timestamp; StableSince = timestamp; InsideSince = timestamp; }
            public bool CandidateInside { get; set; }
            public int CandidateObservations { get; set; }
            public bool StableInside { get; set; }
            public DateTimeOffset CandidateSince { get; set; }
            public DateTimeOffset StableSince { get; set; }
            public DateTimeOffset InsideSince { get; set; }
            public bool DwellRaised { get; set; }
            public void Rebase(bool inside, DateTimeOffset timestamp)
            {
                CandidateInside = inside;
                CandidateObservations = 0;
                StableInside = inside;
                CandidateSince = timestamp;
                StableSince = timestamp;
                InsideSince = timestamp;
                DwellRaised = false;
            }
        }

        private readonly struct EventKey : IEquatable<EventKey>
        {
            public EventKey(string trackId, string roiId, VisualRoiEventKind kind, string? lineId) { TrackId = trackId; RoiId = roiId; Kind = kind; LineId = lineId ?? string.Empty; }
            internal string TrackId { get; }
            internal string RoiId { get; }
            internal VisualRoiEventKind Kind { get; }
            private string LineId { get; }
            public bool Equals(EventKey other) => string.Equals(TrackId, other.TrackId, StringComparison.Ordinal) && string.Equals(RoiId, other.RoiId, StringComparison.Ordinal) && Kind == other.Kind && string.Equals(LineId, other.LineId, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is EventKey other && Equals(other);
            public override int GetHashCode() => unchecked((((TrackId.GetHashCode() * 397) ^ RoiId.GetHashCode()) * 397 ^ (int)Kind) * 397 ^ LineId.GetHashCode());
        }

        private readonly struct CountKey : IEquatable<CountKey>
        {
            public CountKey(string roiId, VisualRoiEventKind kind) { RoiId = roiId; Kind = kind; }
            private string RoiId { get; }
            private VisualRoiEventKind Kind { get; }
            public bool Equals(CountKey other) => string.Equals(RoiId, other.RoiId, StringComparison.Ordinal) && Kind == other.Kind;
            public override bool Equals(object? obj) => obj is CountKey other && Equals(other);
            public override int GetHashCode() => unchecked((RoiId.GetHashCode() * 397) ^ (int)Kind);
            public override string ToString() => RoiId + ":" + Kind;
        }
    }
}
