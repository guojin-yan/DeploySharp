using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Describes the coordinate space emitted by a sliding-window prepare callback. / 描述滑动窗口准备回调输出的坐标空间。</summary>
    public enum SlidingWindowCoordinateMode
    {
        /// <summary>Infer local versus source coordinates from the prepared input source size. / 根据已准备输入源图尺寸自动判断局部或原图坐标。</summary>
        Auto = 0,
        /// <summary>Boxes are already in the full source-image coordinate space. / 边界框已经处于完整源图坐标系。</summary>
        Source = 1,
        /// <summary>Boxes are relative to the current tile and receive the tile origin offset. / 边界框相对于当前切片，运行器会加上切片原点偏移。</summary>
        TileLocal = 2
    }

    /// <summary>Describes one source-image tile evaluated by a sliding-window detector. / 描述滑动窗口检测器评估的一个源图切片。</summary>
    public sealed class SlidingWindow
    {
        internal SlidingWindow(int index, RectangleF bounds)
            : this(index, bounds, null)
        {
        }

        internal SlidingWindow(int index, RectangleF bounds, string? roiId)
        {
            Index = index;
            Bounds = bounds;
            RoiId = string.IsNullOrWhiteSpace(roiId) ? null : roiId;
        }

        /// <summary>Gets the deterministic zero-based tile index. / 获取确定性的从零开始切片索引。</summary>
        public int Index { get; }
        /// <summary>Gets the half-open source-image tile bounds. / 获取半开区间源图切片边界。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets the originating ROI identifier, or null for an unrestricted sliding-window run. / 获取来源 ROI 标识；非 ROI 限定运行时为 null。</summary>
        public string? RoiId { get; }
    }

    /// <summary>Associates a source-space detection with its originating sliding window and ROI. / 将源图检测与其来源滑动窗口和 ROI 关联。</summary>
    public sealed class SlidingWindowDetectionItem
    {
        internal SlidingWindowDetectionItem(Detection detection, SlidingWindow window)
        {
            Detection = detection ?? throw new ArgumentNullException(nameof(detection));
            Window = window ?? throw new ArgumentNullException(nameof(window));
        }

        /// <summary>Gets the mapped source-space detection. / 获取映射到源图空间的检测。</summary>
        public Detection Detection { get; }
        /// <summary>Gets the source window that produced the detection. / 获取产生检测的源图窗口。</summary>
        public SlidingWindow Window { get; }
        /// <summary>Gets the source ROI identifier, when this was a ROI-limited run. / 获取来源 ROI 标识；ROI 限定运行时存在。</summary>
        public string? RoiId => Window.RoiId;
    }

    /// <summary>Controls tile geometry, overlap, and global suppression for large-image detection. / 控制大图检测的切片几何、重叠和全局抑制。</summary>
    public sealed class SlidingWindowDetectionOptions
    {
        /// <summary>Initializes sliding-window options. Overlap is a fraction of the tile dimension. / 初始化滑动窗口选项；重叠是切片尺寸的比例。</summary>
        public SlidingWindowDetectionOptions(
            VisualSize windowSize,
            float overlap = 0.2f,
            float globalIouThreshold = 0.45f,
            DetectionNmsMode nmsMode = DetectionNmsMode.ClassAware,
            int maximumWindows = 4096,
            int maximumDetections = 300,
            bool includeFullImagePass = false,
            float? horizontalOverlap = null,
            float? verticalOverlap = null,
            SlidingWindowCoordinateMode coordinateMode = SlidingWindowCoordinateMode.Auto,
            float minimumRoiCoverage = 0f,
            long maximumPreparedPixels = 1L * 1024 * 1024 * 1024)
        {
            if (!Enum.IsDefined(typeof(DetectionNmsMode), nmsMode)) throw new ArgumentOutOfRangeException(nameof(nmsMode));
            if (!Enum.IsDefined(typeof(SlidingWindowCoordinateMode), coordinateMode)) throw new ArgumentOutOfRangeException(nameof(coordinateMode));
            if (windowSize.Width <= 0 || windowSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(windowSize));
            if (float.IsNaN(overlap) || float.IsInfinity(overlap) || overlap < 0 || overlap >= 1) throw new ArgumentOutOfRangeException(nameof(overlap));
            ValidateOverlap(horizontalOverlap, nameof(horizontalOverlap));
            ValidateOverlap(verticalOverlap, nameof(verticalOverlap));
            if (float.IsNaN(globalIouThreshold) || float.IsInfinity(globalIouThreshold) || globalIouThreshold < 0 || globalIouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(globalIouThreshold));
            if (maximumWindows <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWindows));
            if (maximumDetections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDetections));
            if (float.IsNaN(minimumRoiCoverage) || float.IsInfinity(minimumRoiCoverage) || minimumRoiCoverage < 0 || minimumRoiCoverage > 1) throw new ArgumentOutOfRangeException(nameof(minimumRoiCoverage));
            if (maximumPreparedPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPreparedPixels));
            WindowSize = windowSize;
            Overlap = overlap;
            HorizontalOverlap = horizontalOverlap ?? overlap;
            VerticalOverlap = verticalOverlap ?? overlap;
            GlobalIouThreshold = globalIouThreshold;
            NmsMode = nmsMode;
            MaximumWindows = maximumWindows;
            MaximumDetections = maximumDetections;
            IncludeFullImagePass = includeFullImagePass;
            CoordinateMode = coordinateMode;
            MinimumRoiCoverage = minimumRoiCoverage;
            MaximumPreparedPixels = maximumPreparedPixels;
        }

        /// <summary>Gets the requested tile width and height. / 获取请求的切片宽高。</summary>
        public VisualSize WindowSize { get; }
        /// <summary>Gets the fractional overlap between adjacent tiles. / 获取相邻切片之间的比例重叠。</summary>
        public float Overlap { get; }
        /// <summary>Gets the fractional overlap between horizontally adjacent tiles. / 获取水平方向相邻切片之间的比例重叠。</summary>
        public float HorizontalOverlap { get; }
        /// <summary>Gets the fractional overlap between vertically adjacent tiles. / 获取垂直方向相邻切片之间的比例重叠。</summary>
        public float VerticalOverlap { get; }
        /// <summary>Gets the IoU threshold used after all tiles are mapped to source coordinates. / 获取所有切片映射到源坐标后使用的 IoU 阈值。</summary>
        public float GlobalIouThreshold { get; }
        /// <summary>Gets class-aware or class-agnostic global suppression mode. / 获取按类别或忽略类别的全局抑制模式。</summary>
        public DetectionNmsMode NmsMode { get; }
        /// <summary>Gets the maximum number of generated tiles. / 获取生成切片的最大数量。</summary>
        public int MaximumWindows { get; }
        /// <summary>Gets the maximum detections returned after global suppression. / 获取全局抑制后返回的最大检测数。</summary>
        public int MaximumDetections { get; }
        /// <summary>Gets whether the unsliced full image is also evaluated to retain large-object context. / 获取是否同时评估未切片完整图像以保留大目标上下文。</summary>
        public bool IncludeFullImagePass { get; }
        /// <summary>Gets how callback detection coordinates are interpreted. / 获取回调检测坐标的解释方式。</summary>
        public SlidingWindowCoordinateMode CoordinateMode { get; }
        /// <summary>Gets the minimum fraction of each tile that must be covered by the ROI. / 获取每个切片必须被 ROI 覆盖的最小比例。</summary>
        public float MinimumRoiCoverage { get; }
        /// <summary>Gets the maximum sum of source pixels across all windows in one run. / 获取一次运行所有窗口源像素面积之和的上限。</summary>
        /// <remarks>This bounds preprocessing allocations before the first callback starts; it is independent of the per-window model input size. / 该限制在第一个准备回调启动前生效，且独立于每个窗口的模型输入尺寸。</remarks>
        public long MaximumPreparedPixels { get; }

        private static void ValidateOverlap(float? value, string parameterName)
        {
            if (!value.HasValue) return;
            if (float.IsNaN(value.Value) || float.IsInfinity(value.Value) || value.Value < 0 || value.Value >= 1) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>Contains full-image detections and the tiles evaluated to produce them. / 包含完整图像检测结果及生成结果所评估的切片。</summary>
    public sealed class SlidingWindowDetectionResult
    {
        private readonly IReadOnlyList<SlidingWindow> _windows;

        internal SlidingWindowDetectionResult(DetectionResult detections, IReadOnlyList<SlidingWindow> windows, IReadOnlyList<SlidingWindowDetectionItem>? detectionsWithProvenance = null)
        {
            Detections = detections ?? throw new ArgumentNullException(nameof(detections));
            _windows = windows ?? throw new ArgumentNullException(nameof(windows));
            DetectionsWithProvenance = detectionsWithProvenance ?? Array.Empty<SlidingWindowDetectionItem>();
        }

        /// <summary>Gets globally suppressed detections in full source-image coordinates. / 获取全局抑制后的完整源图坐标检测结果。</summary>
        public DetectionResult Detections { get; }
        /// <summary>Gets generated windows in deterministic evaluation order. / 按确定性评估顺序获取生成的切片。</summary>
        public IReadOnlyList<SlidingWindow> Windows => _windows;
        /// <summary>Gets the number of evaluated windows. / 获取评估的窗口数量。</summary>
        public int WindowCount => _windows.Count;
        /// <summary>Gets retained detections with deterministic source-window provenance. / 获取带确定性来源窗口追溯的保留检测。</summary>
        public IReadOnlyList<SlidingWindowDetectionItem> DetectionsWithProvenance { get; }
    }

    /// <summary>Runs batch-one detection over overlapping tiles and merges them in source coordinates. / 在重叠切片上运行 batch-one 检测并在源坐标中合并结果。</summary>
    public sealed class SlidingWindowDetectionRunner
    {
        private readonly VisualPipeline _pipeline;

        /// <summary>Initializes a runner over an object-detection Visual pipeline. / 使用目标检测 Visual Pipeline 初始化运行器。</summary>
        public SlidingWindowDetectionRunner(VisualPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            if (pipeline.Selection.Profile.Task != VisualTaskId.ObjectDetection) throw new ArgumentException("The selected visual pipeline must produce object detections.", nameof(pipeline));
        }

        /// <summary>Runs tiled detection while preparing the next bounded tile group during the current inference. / 在当前推理期间准备下一组有界切片并运行切片检测。</summary>
        public async Task<SlidingWindowDetectionResult> RunAsync(
            VisualSize sourceSize,
            SlidingWindowDetectionOptions options,
            Func<SlidingWindow, CancellationToken, PreparedVisualInput> prepare,
            VisualExecutionOptions? executionOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            return await RunCoreAsync(
                sourceSize,
                options,
                (window, token) => Task.Factory.StartNew(
                    () => prepare(window, token),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default),
                executionOptions,
                cancellationToken,
                null,
                null).ConfigureAwait(false);
        }

        /// <summary>Runs tiled detection with asynchronous tile preparation. / 使用异步切片准备运行切片检测。</summary>
        /// <remarks>The callback is started on the thread pool and is bounded by the runner's preparation window; explicitly owned prepared inputs are released after each group. / 回调在线程池启动并受运行器准备窗口限制；显式拥有的已准备输入在每组完成后释放。</remarks>
        public async Task<SlidingWindowDetectionResult> RunAsync(
            VisualSize sourceSize,
            SlidingWindowDetectionOptions options,
            Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            VisualExecutionOptions? executionOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            return await RunCoreAsync(
                sourceSize,
                options,
                (window, token) => Task.Factory.StartNew(
                    () => prepareAsync(window, token),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default).Unwrap(),
                executionOptions,
                cancellationToken,
                null,
                null).ConfigureAwait(false);
        }

        /// <summary>Runs overlapping windows restricted to one resolved ROI geometry. / 在一个已解析 ROI 几何范围内运行重叠窗口。</summary>
        /// <remarks>Windows are generated from the ROI bounds and retained only when they touch the ROI. The callback must crop the requested window and remains responsible for exact polygon masking. / 窗口根据 ROI 边界生成并仅保留与 ROI 相交的窗口；回调负责精确裁剪和多边形 mask。</remarks>
        public Task<SlidingWindowDetectionResult> RunRoiAsync(
            VisualSize sourceSize,
            VisualRoi roi,
            IVisualRoiGeometry geometry,
            SlidingWindowDetectionOptions options,
            Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            VisualExecutionOptions? executionOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            return RunCoreAsync(
                sourceSize,
                options,
                (window, token) => Task.Factory.StartNew(
                    () => prepareAsync(window, token),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default).Unwrap(),
                executionOptions,
                cancellationToken,
                geometry,
                roi.Id);
        }

        /// <summary>Runs every enabled sliding-window ROI in one snapshot and performs cross-ROI global suppression. / 运行快照中所有启用的滑窗 ROI，并执行跨 ROI 全局抑制。</summary>
        public async Task<SlidingWindowDetectionResult> RunRoisAsync(
            VisualRoiSnapshot snapshot,
            SlidingWindowDetectionOptions options,
            Func<VisualRoi, IVisualRoiGeometry, SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            VisualExecutionOptions? executionOptions = null,
            int maximumRois = 4096,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (maximumRois <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRois));

            var selected = new List<(VisualRoi Roi, IVisualRoiGeometry Geometry)>();
            int totalWindows = 0;
            long totalPreparedPixels = 0;
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include || roi.ExecutionMode != RoiExecutionMode.SlidingWindow || !roi.AppliesTo(VisualTaskId.ObjectDetection)) continue;
                if (selected.Count >= maximumRois) throw new VisualException(VisualErrorCodes.InputInvalid, "The sliding-window ROI count exceeds the configured limit.", technicalDetails: "maximumRois=" + maximumRois);
                IVisualRoiGeometry geometry = snapshot.Resolve(roi);
                List<SlidingWindow> planned = CreateWindows(snapshot.SourceSize, options, geometry, roi.Id);
                int windowCount = planned.Count;
                totalWindows = checked(totalWindows + windowCount);
                if (totalWindows > options.MaximumWindows) throw new VisualException(VisualErrorCodes.DecodeFailed, "Sliding-window generation exceeded the configured cross-ROI window limit.", technicalDetails: "maximumWindows=" + options.MaximumWindows);
                totalPreparedPixels = checked(totalPreparedPixels + EstimatePreparedPixels(planned));
                if (totalPreparedPixels > options.MaximumPreparedPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "Sliding-window preparation exceeds the configured cross-ROI pixel budget.", technicalDetails: "pixels=" + totalPreparedPixels + ";limit=" + options.MaximumPreparedPixels);
                selected.Add((roi, geometry));
            }

            var allWindows = new List<SlidingWindow>(totalWindows);
            var allDetections = new List<SlidingWindowDetectionItem>();
            foreach ((VisualRoi Roi, IVisualRoiGeometry Geometry) entry in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SlidingWindowDetectionResult result = await RunRoiAsync(
                    snapshot.SourceSize,
                    entry.Roi,
                    entry.Geometry,
                    options,
                    (window, token) => prepareAsync(entry.Roi, entry.Geometry, window, token),
                    executionOptions,
                    cancellationToken).ConfigureAwait(false);
                allWindows.AddRange(result.Windows);
                allDetections.AddRange(result.DetectionsWithProvenance);
            }

            IReadOnlyList<SlidingWindowDetectionItem> suppressed = SuppressGlobally(allDetections, options, cancellationToken);
            return new SlidingWindowDetectionResult(new DetectionResult(suppressed.Select(value => value.Detection)), allWindows.AsReadOnly(), suppressed);
        }

        private async Task<SlidingWindowDetectionResult> RunCoreAsync(
            VisualSize sourceSize,
            SlidingWindowDetectionOptions options,
            Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync,
            VisualExecutionOptions? executionOptions,
            CancellationToken cancellationToken,
            IVisualRoiGeometry? roiGeometry,
            string? roiId)
        {
            List<SlidingWindow> windows = CreateWindows(sourceSize, options, roiGeometry, roiId);
            EnsurePreparedPixelBudget(windows, options);
            int chunkSize = Math.Max(1, _pipeline.MaximumConcurrency * 2);
            var allDetections = new List<SlidingWindowDetectionItem>();
            Task<PreparedVisualInput[]>? prefetched = null;
            VisualExecutionOptions requested = executionOptions ?? VisualExecutionOptions.Default;
            // The runner owns only explicitly owned prepared resources. Borrowed tensors
            // remain with the caller, while owned tiles are released after each group.
            var runOptions = new VisualExecutionOptions(requested.Timeout, disposeOwnedInputOnCompletion: true, requested.CorrelationId);
            try
            {
                for (int offset = 0; offset < windows.Count; offset += chunkSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = Math.Min(chunkSize, windows.Count - offset);
                    PreparedVisualInput[] prepared = prefetched == null
                        ? await PrepareChunkAsync(windows, offset, count, prepareAsync, cancellationToken).ConfigureAwait(false)
                        : await prefetched.ConfigureAwait(false);
                    int nextOffset = offset + count;
                    if (nextOffset < windows.Count)
                    {
                        int nextCount = Math.Min(chunkSize, windows.Count - nextOffset);
                        int capturedOffset = nextOffset;
                        prefetched = PrepareChunkAsync(windows, capturedOffset, nextCount, prepareAsync, cancellationToken);
                    }
                    else prefetched = null;

                    try
                    {
                        IReadOnlyList<VisualInferenceResult> results = await _pipeline.RunManyAsync(prepared, runOptions, cancellationToken).ConfigureAwait(false);
                        if (results.Count != count) throw new VisualException(VisualErrorCodes.DecodeFailed, "The Visual pipeline returned a result count different from the sliding-window batch.", profileId: _pipeline.Selection.Profile.ProfileId, modelId: _pipeline.Selection.Profile.ModelId, technicalDetails: "expected=" + count + ";actual=" + results.Count);
                        for (int index = 0; index < results.Count; index++)
                        {
                            object value = results[index].Value;
                            if (value is not DetectionResult detection) throw new VisualException(VisualErrorCodes.DecodeFailed, "Sliding-window detection requires a batch-one DetectionResult for each tile.", profileId: _pipeline.Selection.Profile.ProfileId, modelId: _pipeline.Selection.Profile.ModelId);
                            allDetections.AddRange(MapDetections(detection.Detections, windows[offset + index], prepared[index], sourceSize, options.CoordinateMode));
                        }
                    }
                    finally
                    {
                        DisposeOwned(prepared);
                    }
                }
            }
            catch
            {
                if (prefetched != null)
                {
                    try { DisposeOwned(await prefetched.ConfigureAwait(false)); } catch { }
                }
                throw;
            }

            IReadOnlyList<SlidingWindowDetectionItem> suppressed = SuppressGlobally(allDetections, options, cancellationToken);
            return new SlidingWindowDetectionResult(new DetectionResult(suppressed.Select(value => value.Detection)), windows.AsReadOnly(), suppressed);
        }

        private static void DisposeOwned(IReadOnlyList<PreparedVisualInput> prepared)
        {
            for (int index = 0; index < prepared.Count; index++) if (prepared[index].Ownership == PreparedInputOwnership.Owned) prepared[index].Dispose();
        }

        private static IReadOnlyList<SlidingWindowDetectionItem> MapDetections(
            IReadOnlyList<Detection> detections,
            SlidingWindow window,
            PreparedVisualInput prepared,
            VisualSize sourceSize,
            SlidingWindowCoordinateMode coordinateMode)
        {
            VisualSize tileSize = new VisualSize((int)Math.Round(window.Bounds.Width), (int)Math.Round(window.Bounds.Height));
            if (coordinateMode == SlidingWindowCoordinateMode.TileLocal && prepared.SourceSize != tileSize)
            {
                throw new VisualException(
                    VisualErrorCodes.InputInvalid,
                    "Tile-local sliding-window coordinates require a prepared source size equal to the tile size.",
                    tensorName: prepared.InputName,
                    technicalDetails: "prepared=" + prepared.SourceSize.Width + "x" + prepared.SourceSize.Height + "; tile=" + tileSize.Width + "x" + tileSize.Height);
            }
            bool local = coordinateMode == SlidingWindowCoordinateMode.TileLocal;
            if (coordinateMode == SlidingWindowCoordinateMode.Auto)
            {
                // A local crop normally advertises the tile dimensions as its source
                // size; ImageTransform.Crop over the original image advertises the
                // full source size and is therefore already globally mapped.
                local = prepared.SourceSize == tileSize;
            }
            var mapped = new List<SlidingWindowDetectionItem>(detections.Count);
            RectangleF sourceBounds = new RectangleF(0, 0, sourceSize.Width, sourceSize.Height);
            for (int index = 0; index < detections.Count; index++)
            {
                Detection detection = detections[index];
                RectangleF box = detection.Box;
                RectangleF translated = new RectangleF(
                    local ? box.X + window.Bounds.X : box.X,
                    local ? box.Y + window.Bounds.Y : box.Y,
                    box.Width,
                    box.Height);
                float left = Math.Max(sourceBounds.X, Math.Min(sourceBounds.Right, translated.X));
                float top = Math.Max(sourceBounds.Y, Math.Min(sourceBounds.Bottom, translated.Y));
                float right = Math.Max(left, Math.Min(sourceBounds.Right, translated.Right));
                float bottom = Math.Max(top, Math.Min(sourceBounds.Bottom, translated.Bottom));
                if (right > left && bottom > top)
                {
                    mapped.Add(new SlidingWindowDetectionItem(new Detection(new RectangleF(left, top, right - left, bottom - top), detection.Label), window));
                }
            }
            return mapped;
        }

        private static Task<PreparedVisualInput[]> PrepareChunkAsync(IReadOnlyList<SlidingWindow> windows, int offset, int count, Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                () => PrepareChunkCoreAsync(windows, offset, count, prepareAsync, cancellationToken),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default).Unwrap();
        }

        private static async Task<PreparedVisualInput[]> PrepareChunkCoreAsync(IReadOnlyList<SlidingWindow> windows, int offset, int count, Func<SlidingWindow, CancellationToken, Task<PreparedVisualInput>> prepareAsync, CancellationToken cancellationToken)
        {
            var tasks = new Task<PreparedVisualInput>[count];
            int started = 0;
            try
            {
                for (int index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tasks[index] = prepareAsync(windows[offset + index], cancellationToken);
                    if (tasks[index] == null) throw new ArgumentException("The sliding-window prepare callback returned a null task.", nameof(prepareAsync));
                    started++;
                }

                PreparedVisualInput[] result = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (int index = 0; index < result.Length; index++)
                {
                    if (result[index] == null) throw new ArgumentException("The sliding-window prepare callback returned null.", nameof(prepareAsync));
                    if (result[index].BatchSize != 1) throw new VisualException(VisualErrorCodes.InputInvalid, "Sliding-window detection requires one image per prepared tile.", tensorName: result[index].InputName);
                }
                return result;
            }
            catch
            {
                // Await every started preparation before releasing its owned input;
                // this also observes faults from callbacks that completed after the
                // first failure and prevents an unobserved task exception.
                var completed = new PreparedVisualInput[started];
                for (int index = 0; index < started; index++)
                {
                    try { completed[index] = await tasks[index].ConfigureAwait(false); } catch { }
                }
                for (int index = 0; index < completed.Length; index++) if (completed[index]?.Ownership == PreparedInputOwnership.Owned) completed[index].Dispose();
                throw;
            }
        }

        private static List<SlidingWindow> CreateWindows(VisualSize sourceSize, SlidingWindowDetectionOptions options, IVisualRoiGeometry? roiGeometry, string? roiId)
        {
            RectangleF region = roiGeometry?.Bounds ?? new RectangleF(0, 0, sourceSize.Width, sourceSize.Height);
            int regionLeft = Math.Max(0, (int)Math.Floor(region.X));
            int regionTop = Math.Max(0, (int)Math.Floor(region.Y));
            int regionRight = Math.Min(sourceSize.Width, (int)Math.Ceiling(region.Right));
            int regionBottom = Math.Min(sourceSize.Height, (int)Math.Ceiling(region.Bottom));
            if (regionRight <= regionLeft || regionBottom <= regionTop) throw new VisualException(VisualErrorCodes.InputInvalid, "The ROI does not intersect the source image.");
            int width = Math.Min(regionRight - regionLeft, options.WindowSize.Width);
            int height = Math.Min(regionBottom - regionTop, options.WindowSize.Height);
            int stepX = Math.Max(1, (int)Math.Round(width * (1f - options.HorizontalOverlap), MidpointRounding.AwayFromZero));
            int stepY = Math.Max(1, (int)Math.Round(height * (1f - options.VerticalOverlap), MidpointRounding.AwayFromZero));
            int lastX = regionRight - width;
            int lastY = regionBottom - height;
            var windows = new List<SlidingWindow>();
            if (options.IncludeFullImagePass && (lastX != regionLeft || lastY != regionTop))
            {
                windows.Add(new SlidingWindow(windows.Count, new RectangleF(regionLeft, regionTop, regionRight - regionLeft, regionBottom - regionTop), roiId));
            }
            for (int y = regionTop; ; y = Math.Min(lastY, checked(y + stepY)))
            {
                for (int x = regionLeft; ; x = Math.Min(lastX, checked(x + stepX)))
                {
                    if (windows.Count >= options.MaximumWindows) throw new VisualException(VisualErrorCodes.DecodeFailed, "Sliding-window generation exceeded the configured maximum window count.", technicalDetails: "maximumWindows=" + options.MaximumWindows);
                    var candidate = new SlidingWindow(windows.Count, new RectangleF(x, y, width, height), roiId);
                    if (roiGeometry == null || MeetsRoiCoverage(candidate.Bounds, roiGeometry, options.MinimumRoiCoverage)) windows.Add(candidate);
                    if (x == lastX) break;
                }
                if (y == lastY) break;
            }
            for (int index = 0; index < windows.Count; index++) windows[index] = new SlidingWindow(index, windows[index].Bounds, roiId);
            return windows;
        }

        private static void EnsurePreparedPixelBudget(IReadOnlyList<SlidingWindow> windows, SlidingWindowDetectionOptions options)
        {
            long pixels = EstimatePreparedPixels(windows);
            if (pixels > options.MaximumPreparedPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "Sliding-window preparation exceeds the configured pixel budget.", technicalDetails: "pixels=" + pixels + ";limit=" + options.MaximumPreparedPixels);
        }

        internal static long EstimatePreparedPixels(IReadOnlyList<SlidingWindow> windows)
        {
            long total = 0;
            for (int index = 0; index < windows.Count; index++)
            {
                SlidingWindow window = windows[index];
                long pixels = checked((long)Math.Ceiling(window.Bounds.Width) * (long)Math.Ceiling(window.Bounds.Height));
                total = checked(total + pixels);
            }
            return total;
        }

        // Shared by task-specific ROI sliding-window runners. The detection runner
        // remains the owner of the geometry rules so other tasks cannot silently
        // diverge on overlap, bounds, coverage, or window-budget semantics.
        internal static IReadOnlyList<SlidingWindow> PlanWindows(VisualSize sourceSize, SlidingWindowDetectionOptions options, IVisualRoiGeometry? roiGeometry, string? roiId)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            List<SlidingWindow> windows = CreateWindows(sourceSize, options, roiGeometry, roiId);
            EnsurePreparedPixelBudget(windows, options);
            return windows;
        }

        private static IReadOnlyList<SlidingWindowDetectionItem> SuppressGlobally(List<SlidingWindowDetectionItem> detections, SlidingWindowDetectionOptions options, CancellationToken cancellationToken)
        {
            detections.Sort((left, right) =>
            {
                int score = right.Detection.Label.Score.CompareTo(left.Detection.Label.Score);
                if (score != 0) return score;
                int label = left.Detection.Label.Index.CompareTo(right.Detection.Label.Index);
                if (label != 0) return label;
                int x = left.Detection.Box.X.CompareTo(right.Detection.Box.X);
                if (x != 0) return x;
                int y = left.Detection.Box.Y.CompareTo(right.Detection.Box.Y);
                if (y != 0) return y;
                int width = left.Detection.Box.Width.CompareTo(right.Detection.Box.Width);
                if (width != 0) return width;
                int height = left.Detection.Box.Height.CompareTo(right.Detection.Box.Height);
                if (height != 0) return height;
                int roi = string.Compare(left.RoiId, right.RoiId, StringComparison.Ordinal);
                return roi != 0 ? roi : left.Window.Index.CompareTo(right.Window.Index);
            });
            var kept = new List<SlidingWindowDetectionItem>(Math.Min(detections.Count, options.MaximumDetections));
            for (int index = 0; index < detections.Count && kept.Count < options.MaximumDetections; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SlidingWindowDetectionItem candidate = detections[index];
                bool suppressed = false;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    SlidingWindowDetectionItem existing = kept[keptIndex];
                    if (options.NmsMode == DetectionNmsMode.ClassAware && existing.Detection.Label.Index != candidate.Detection.Label.Index) continue;
                    if (DetectionDecoder.IntersectionOverUnion(existing.Detection.Box, candidate.Detection.Box) > options.GlobalIouThreshold)
                    {
                        suppressed = true;
                        break;
                    }
                }
                if (!suppressed) kept.Add(candidate);
            }
            return kept;
        }

        private static bool MeetsRoiCoverage(RectangleF window, IVisualRoiGeometry geometry, float minimumCoverage)
        {
            float area = window.Width * window.Height;
            if (area <= 0) return false;
            float intersection = VisualRoiDetectionFilter.CalculateIntersectionArea(window, geometry);
            return intersection > 0 && intersection / area >= minimumCoverage;
        }
    }
}
