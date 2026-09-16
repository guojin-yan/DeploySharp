using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Controls one bounded, ordered video ROI run over an application-owned decoder and tracker. / 配置一次有界有序的视频 ROI 运行。</summary>
    public sealed class VisualRoiVideoRunOptions
    {
        /// <summary>Initializes video run options. / 初始化视频运行选项。</summary>
        public VisualRoiVideoRunOptions(int maximumFrames = 1_000_000, bool continueOnFrameFailure = false, int maximumRecordedFailures = 256)
        {
            if (maximumFrames <= 0) throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            if (maximumRecordedFailures < 0) throw new ArgumentOutOfRangeException(nameof(maximumRecordedFailures));
            MaximumFrames = maximumFrames;
            ContinueOnFrameFailure = continueOnFrameFailure;
            MaximumRecordedFailures = maximumRecordedFailures;
        }

        /// <summary>Gets the maximum number of frames accepted by one run. / 获取一次运行接受的最大帧数。</summary>
        public int MaximumFrames { get; }
        /// <summary>Gets whether tracker failures are recorded and skipped instead of aborting the run. / 获取跟踪器失败是否记录并跳过，而不是终止运行。</summary>
        public bool ContinueOnFrameFailure { get; }
        /// <summary>Gets the maximum number of exception objects retained in the report. / 获取报告中保留的异常对象上限。</summary>
        public int MaximumRecordedFailures { get; }
    }

    /// <summary>Contains bounded video ROI health and event counters. / 包含有界视频 ROI 健康度和事件计数。</summary>
    public sealed class VisualRoiVideoRunReport
    {
        internal VisualRoiVideoRunReport(int processedFrames, int failedFrames, long observations, long emittedEvents, int peakActiveTracks, int peakCooldownEntries, int peakCountKeys, TimeSpan elapsed, IReadOnlyList<Exception> failures, int droppedFailures)
        {
            ProcessedFrames = processedFrames;
            FailedFrames = failedFrames;
            ObservationCount = observations;
            EmittedEventCount = emittedEvents;
            PeakActiveTrackCount = peakActiveTracks;
            PeakCooldownEntryCount = peakCooldownEntries;
            PeakCountKeyCount = peakCountKeys;
            Elapsed = elapsed;
            Failures = new ReadOnlyCollection<Exception>(new List<Exception>(failures));
            DroppedFailureCount = droppedFailures;
        }

        /// <summary>Gets the successfully enumerated frame count. / 获取成功枚举的帧数。</summary>
        public int ProcessedFrames { get; }
        /// <summary>Gets the number of frames whose external tracker callback failed. / 获取外部跟踪器回调失败的帧数。</summary>
        public int FailedFrames { get; }
        /// <summary>Gets the total tracker observations submitted to the ROI processor. / 获取提交给 ROI 处理器的跟踪观测总数。</summary>
        public long ObservationCount { get; }
        /// <summary>Gets the number of emitted ROI events. / 获取发出的 ROI 事件数。</summary>
        public long EmittedEventCount { get; }
        /// <summary>Gets the peak number of active TrackId states. / 获取活动 TrackId 状态峰值。</summary>
        public int PeakActiveTrackCount { get; }
        /// <summary>Gets the peak number of cooldown entries. / 获取冷却条目峰值。</summary>
        public int PeakCooldownEntryCount { get; }
        /// <summary>Gets the peak number of cumulative count keys. / 获取累计计数键峰值。</summary>
        public int PeakCountKeyCount { get; }
        /// <summary>Gets wall-clock run duration. / 获取墙钟运行时长。</summary>
        public TimeSpan Elapsed { get; }
        /// <summary>Gets frame failures retained when ContinueOnFrameFailure is enabled. / 获取启用 ContinueOnFrameFailure 时保留的帧失败。</summary>
        public IReadOnlyList<Exception> Failures { get; }
        /// <summary>Gets the number of additional failures not retained because the report cap was reached. / 获取因达到报告上限而未保留的其他失败数。</summary>
        public int DroppedFailureCount { get; }
    }

    /// <summary>
    /// Runs a real decoder/tracker stream through <see cref="VisualRoiEventProcessor"/> without buffering the video.
    /// The processor remains the single owner of ROI state, so a failed or cancelled frame cannot partially update it.
    /// / 将真实解码器/跟踪器流逐帧送入 VisualRoiEventProcessor，不缓存整段视频；处理器是 ROI 状态的唯一所有者，失败或取消帧不会部分更新状态。
    /// </summary>
    /// <typeparam name="TFrame">Application-owned decoded frame type. / 应用拥有的解码帧类型。</typeparam>
    public sealed class VisualRoiVideoRunner<TFrame>
    {
        /// <summary>Runs frames in source order with bounded frame count and no unbounded queue. / 按源顺序、有界帧数、无无界队列地运行帧。</summary>
        /// <param name="frames">Lazy frame sequence; it is never materialized by this runner. / 延迟帧序列；本运行器不会将其全部物化。</param>
        /// <param name="trackAsync">Application-owned detector/tracker callback. / 应用拥有的检测器/跟踪器回调。</param>
        /// <param name="timestamp">Returns the timestamp used to expire missing TrackIds, including empty frames. / 返回用于过期丢失 TrackId（包括空帧）的时间戳。</param>
        /// <param name="processor">ROI event state machine. / ROI 事件状态机。</param>
        /// <param name="options">Optional frame bound and failure policy. / 可选的帧数上限和失败策略。</param>
        /// <param name="releaseFrame">Optional frame release callback, always called after the frame callback finishes. / 可选帧释放回调，在帧回调结束后始终调用。</param>
        /// <param name="cancellationToken">Cancellation token observed before each frame and by the tracker callback. / 每帧开始前以及跟踪器回调使用的取消令牌。</param>
        public async Task<VisualRoiVideoRunReport> RunAsync(
            IEnumerable<TFrame> frames,
            Func<TFrame, CancellationToken, Task<IReadOnlyList<VisualRoiTrackObservation>>> trackAsync,
            Func<TFrame, DateTimeOffset> timestamp,
            VisualRoiEventProcessor processor,
            VisualRoiVideoRunOptions? options = null,
            Action<TFrame>? releaseFrame = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (trackAsync == null) throw new ArgumentNullException(nameof(trackAsync));
            if (timestamp == null) throw new ArgumentNullException(nameof(timestamp));
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            VisualRoiVideoRunOptions effective = options ?? new VisualRoiVideoRunOptions();
            var failures = new List<Exception>();
            int processed = 0;
            int failed = 0;
            int droppedFailures = 0;
            long observations = 0;
            long emitted = 0;
            int peakTracks = 0;
            int peakCooldown = 0;
            int peakKeys = 0;
            Stopwatch watch = Stopwatch.StartNew();
            foreach (TFrame frame in frames)
            {
                DateTimeOffset frameTimestamp = default(DateTimeOffset);
                bool timestampAvailable = false;
                bool frameLimitExceeded = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (processed >= effective.MaximumFrames)
                    {
                        frameLimitExceeded = true;
                        throw new VisualException(VisualErrorCodes.InputInvalid, "The video ROI run exceeded its configured maximum frame count.", technicalDetails: "maximumFrames=" + effective.MaximumFrames);
                    }
                    frameTimestamp = timestamp(frame);
                    timestampAvailable = true;
                    IReadOnlyList<VisualRoiTrackObservation> result = await (trackAsync(frame, cancellationToken) ?? throw new InvalidOperationException("The tracker callback returned null observations.")).ConfigureAwait(false);
                    IReadOnlyList<VisualRoiEvent> events = result.Count == 0 ? Array.Empty<VisualRoiEvent>() : processor.ProcessFrame(result);
                    observations = checked(observations + result.Count);
                    emitted = checked(emitted + events.Count);
                    IReadOnlyList<VisualRoiEvent> expired = processor.Advance(frameTimestamp, processed);
                    emitted = checked(emitted + expired.Count);
                    processed = checked(processed + 1);
                }
                catch (Exception exception) when (effective.ContinueOnFrameFailure && !frameLimitExceeded && !(exception is OperationCanceledException))
                {
                    failed = checked(failed + 1);
                    if (failures.Count < effective.MaximumRecordedFailures) failures.Add(exception);
                    else droppedFailures = checked(droppedFailures + 1);
                    if (timestampAvailable)
                    {
                        IReadOnlyList<VisualRoiEvent> expired = processor.Advance(frameTimestamp, processed);
                        emitted = checked(emitted + expired.Count);
                    }
                    processed = checked(processed + 1);
                }
                finally
                {
                    releaseFrame?.Invoke(frame);
                    peakTracks = Math.Max(peakTracks, processor.ActiveTrackCount);
                    peakCooldown = Math.Max(peakCooldown, processor.CooldownEntryCount);
                    peakKeys = Math.Max(peakKeys, processor.CountKeyCount);
                }
            }
            watch.Stop();
            return new VisualRoiVideoRunReport(processed, failed, observations, emitted, peakTracks, peakCooldown, peakKeys, watch.Elapsed, failures, droppedFailures);
        }
    }
}
