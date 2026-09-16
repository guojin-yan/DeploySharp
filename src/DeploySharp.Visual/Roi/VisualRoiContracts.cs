using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the coordinate space used by a visual ROI. / 标识视觉 ROI 使用的坐标空间。</summary>
    public enum RoiCoordinateSpace
    {
        /// <summary>Coordinates are pixels in the source image. / 坐标是源图像素。</summary>
        SourcePixels = 0,
        /// <summary>Coordinates are normalized to the source image in the range [0,1]. / 坐标按源图归一化到 [0,1]。</summary>
        Normalized = 1,
        /// <summary>Coordinates are relative to a model input. / 坐标相对于模型输入。</summary>
        ModelInput = 2,
        /// <summary>Coordinates are relative to a sliding-window tile. / 坐标相对于滑动窗口切片。</summary>
        TileLocal = 3,
        /// <summary>Coordinates are application-defined calibrated world coordinates. / 坐标是应用定义的标定世界坐标。</summary>
        World = 4
    }

    /// <summary>Describes whether an ROI includes or excludes matching results. / 描述 ROI 包含还是排除匹配结果。</summary>
    public enum RoiInclusionMode
    {
        /// <summary>Results matching this ROI are included. / 匹配此 ROI 的结果会被包含。</summary>
        Include = 0,
        /// <summary>Results matching this ROI are excluded. / 匹配此 ROI 的结果会被排除。</summary>
        Exclude = 1
    }

    /// <summary>Describes how a source image is evaluated for one ROI. / 描述源图如何针对一个 ROI 执行。</summary>
    public enum RoiExecutionMode
    {
        /// <summary>Crop the ROI and run a separate inference. / 裁剪 ROI 后单独推理。</summary>
        CropAndInfer = 0,
        /// <summary>Run the source image once and filter decoded results. / 整图推理后过滤结果。</summary>
        FilterResults = 1,
        /// <summary>Run overlapping windows inside the ROI. / 在 ROI 内运行重叠滑窗。</summary>
        SlidingWindow = 2
    }

    /// <summary>Describes the result hit rule used by ROI filtering. / 描述 ROI 过滤使用的命中规则。</summary>
    public enum RoiHitTestMode
    {
        /// <summary>The result center must be inside the ROI. / 结果中心必须位于 ROI 内。</summary>
        CenterPoint = 0,
        /// <summary>Any positive intersection is a hit. / 存在正面积相交即命中。</summary>
        AnyIntersection = 1,
        /// <summary>Intersection over result area must reach a threshold. / 相交面积占结果面积达到阈值。</summary>
        IntersectionOverResult = 2,
        /// <summary>Intersection over ROI area must reach a threshold. / 相交面积占 ROI 面积达到阈值。</summary>
        IntersectionOverRoi = 3,
        /// <summary>Axis-aligned IoU must reach a threshold. / 轴对齐 IoU 达到阈值。</summary>
        IoU = 4
        ,
        /// <summary>For Pose, the fraction of valid keypoints inside the ROI must reach the threshold. Other tasks reject this mode. / 对 Pose，位于 ROI 内的有效关键点比例必须达到阈值；其他任务拒绝此模式。</summary>
        KeypointCoverage = 5,
        /// <summary>Every valid Pose keypoint must be inside the ROI. / 姿态的每个有效关键点都必须位于 ROI 内。</summary>
        AllKeypoints = 6,
        /// <summary>At least one valid Pose keypoint must be inside the ROI. / 姿态至少一个有效关键点必须位于 ROI 内。</summary>
        AnyKeypoint = 7,
        /// <summary>The fraction of explicitly visible Pose keypoints inside the ROI must reach the threshold. / 位于 ROI 内的显式可见姿态关键点比例必须达到阈值。</summary>
        VisibleKeypointRatio = 8,
        /// <summary>The fraction of an instance mask intersecting the ROI must reach the threshold. / 实例掩码与 ROI 相交的比例必须达到阈值。</summary>
        MaskIntersectionRatio = 9
    }

    /// <summary>Describes how ROI execution failures are surfaced. / 描述 ROI 执行失败如何返回。</summary>
    public enum RoiFailureMode
    {
        /// <summary>Stop as soon as one ROI fails. / 任一 ROI 失败立即停止。</summary>
        FailFast = 0,
        /// <summary>Let started work complete and then fail the operation. / 已开始任务收口后失败。</summary>
        CompleteThenFail = 1,
        /// <summary>Return successful results and per-ROI failures. / 返回成功结果及逐 ROI 失败。</summary>
        ReturnPartialResults = 2
    }

    /// <summary>Describes cross-ROI result merging. / 描述跨 ROI 结果合并策略。</summary>
    public enum RoiResultMergeMode
    {
        /// <summary>Keep every result. / 保留全部结果。</summary>
        KeepAll = 0,
        /// <summary>Use class-aware axis-aligned NMS where supported. / 使用按类别轴对齐 NMS。</summary>
        ClassAwareNms = 1,
        /// <summary>Use class-agnostic axis-aligned NMS where supported. / 使用忽略类别轴对齐 NMS。</summary>
        ClassAgnosticNms = 2,
        /// <summary>Keep the result from the highest-priority ROI. / 保留最高优先级 ROI 的结果。</summary>
        RoiPriority = 3,
        /// <summary>Keep the highest-confidence result. / 保留置信度最高的结果。</summary>
        HighestConfidence = 4,
        /// <summary>Delegate to the task-specific merger. / 委托任务专用合并器。</summary>
        TaskSpecific = 5,
        /// <summary>Fuse overlapping axis-aligned detection boxes using score-weighted coordinates. / 使用按分数加权的坐标融合重叠轴对齐检测框。</summary>
        WeightedBoxFusion = 6
    }

    /// <summary>Identifies the supported ROI geometry kinds. / 标识支持的 ROI 几何类型。</summary>
    public enum VisualRoiGeometryKind
    {
        /// <summary>Axis-aligned rectangle. / 轴对齐矩形。</summary>
        Rectangle = 0,
        /// <summary>Rotated rectangle represented by four corners. / 用四个角点表示的旋转矩形。</summary>
        RotatedRectangle = 1,
        /// <summary>Simple non-self-intersecting polygon. / 简单不自交多边形。</summary>
        Polygon = 2
        ,
        /// <summary>Binary pixel mask. / 二值像素掩码。</summary>
        Mask = 3
    }

    /// <summary>Defines geometry operations needed by the backend-neutral ROI runner. / 定义后端无关 ROI 运行器所需的几何操作。</summary>
    public interface IVisualRoiGeometry
    {
        /// <summary>Gets the geometry kind. / 获取几何类型。</summary>
        public VisualRoiGeometryKind Kind { get; }
        /// <summary>Gets the source-space bounding rectangle. / 获取源空间轴对齐边界。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets an immutable ordered point sequence. / 获取不可变有序点序列。</summary>
        public IReadOnlyList<PointF> Points { get; }
        /// <summary>Tests whether a point is inside this geometry. / 测试点是否在几何区域内。</summary>
        public bool Contains(PointF point);
        /// <summary>Returns the geometry area. / 返回几何面积。</summary>
        public float Area { get; }
    }

    /// <summary>Represents an axis-aligned rectangular ROI geometry. / 表示轴对齐矩形 ROI 几何。</summary>
    public sealed class RectangleRoiGeometry : IVisualRoiGeometry
    {
        private readonly IReadOnlyList<PointF> _points;

        /// <summary>Initializes a rectangle geometry. / 初始化矩形几何。</summary>
        public RectangleRoiGeometry(RectangleF rectangle)
        {
            EnsureFinite(rectangle);
            if (rectangle.Width <= 0 || rectangle.Height <= 0) throw new ArgumentOutOfRangeException(nameof(rectangle));
            Rectangle = rectangle;
            _points = new ReadOnlyCollection<PointF>(new[]
            {
                new PointF(rectangle.X, rectangle.Y),
                new PointF(rectangle.Right, rectangle.Y),
                new PointF(rectangle.Right, rectangle.Bottom),
                new PointF(rectangle.X, rectangle.Bottom)
            });
        }

        /// <summary>Gets the rectangle. / 获取矩形。</summary>
        public RectangleF Rectangle { get; }
        /// <inheritdoc />
        public VisualRoiGeometryKind Kind => VisualRoiGeometryKind.Rectangle;
        /// <inheritdoc />
        public RectangleF Bounds => Rectangle;
        /// <inheritdoc />
        public IReadOnlyList<PointF> Points => _points;
        /// <inheritdoc />
        public float Area => Rectangle.Width * Rectangle.Height;
        /// <inheritdoc />
        public bool Contains(PointF point) => point.X >= Rectangle.X && point.X < Rectangle.Right && point.Y >= Rectangle.Y && point.Y < Rectangle.Bottom;

        private static void EnsureFinite(RectangleF value)
        {
            VisualGuard.Finite(value.X, nameof(value));
            VisualGuard.Finite(value.Y, nameof(value));
            VisualGuard.Finite(value.Width, nameof(value));
            VisualGuard.Finite(value.Height, nameof(value));
        }
    }

    /// <summary>Represents a rotated rectangular ROI using four ordered corners. / 使用四个有序角点表示旋转矩形 ROI。</summary>
    public sealed class RotatedRectangleRoiGeometry : IVisualRoiGeometry
    {
        private readonly IReadOnlyList<PointF> _points;

        /// <summary>Initializes a rotated rectangle from center, size, and clockwise angle. / 根据中心、尺寸和顺时针角度初始化旋转矩形。</summary>
        public RotatedRectangleRoiGeometry(PointF center, SizeF size, float angleDegrees)
        {
            EnsureFinite(center);
            EnsureFinite(size);
            VisualGuard.Finite(angleDegrees, nameof(angleDegrees));
            if (size.Width <= 0 || size.Height <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            double radians = angleDegrees * Math.PI / 180d;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            float halfWidth = size.Width / 2f;
            float halfHeight = size.Height / 2f;
            var local = new[] { new PointF(-halfWidth, -halfHeight), new PointF(halfWidth, -halfHeight), new PointF(halfWidth, halfHeight), new PointF(-halfWidth, halfHeight) };
            var corners = new PointF[4];
            for (int index = 0; index < local.Length; index++) corners[index] = new PointF(center.X + local[index].X * cos - local[index].Y * sin, center.Y + local[index].X * sin + local[index].Y * cos);
            _points = new ReadOnlyCollection<PointF>(corners);
            Center = center;
            Size = size;
            AngleDegrees = angleDegrees;
            Bounds = VisualRoiGeometryMath.Bounds(_points);
        }

        /// <summary>Gets the center. / 获取中心。</summary>
        public PointF Center { get; }
        /// <summary>Gets the unrotated size. / 获取未旋转尺寸。</summary>
        public SizeF Size { get; }
        /// <summary>Gets clockwise angle in degrees. / 获取顺时针角度。</summary>
        public float AngleDegrees { get; }
        /// <inheritdoc />
        public VisualRoiGeometryKind Kind => VisualRoiGeometryKind.RotatedRectangle;
        /// <inheritdoc />
        public RectangleF Bounds { get; }
        /// <inheritdoc />
        public IReadOnlyList<PointF> Points => _points;
        /// <inheritdoc />
        public float Area => Size.Width * Size.Height;
        /// <inheritdoc />
        public bool Contains(PointF point) => VisualRoiGeometryMath.Contains(_points, point);

        private static void EnsureFinite(PointF value) { VisualGuard.Finite(value.X, nameof(value)); VisualGuard.Finite(value.Y, nameof(value)); }
        private static void EnsureFinite(SizeF value) { VisualGuard.Finite(value.Width, nameof(value)); VisualGuard.Finite(value.Height, nameof(value)); }
    }

    /// <summary>Represents a simple non-self-intersecting polygon ROI. / 表示简单不自交多边形 ROI。</summary>
    public sealed class PolygonRoiGeometry : IVisualRoiGeometry
    {
        private readonly IReadOnlyList<PointF> _points;

        /// <summary>Initializes a polygon and validates finiteness, area, and self-intersection. / 初始化多边形并验证有限值、面积和自交。</summary>
        public PolygonRoiGeometry(IEnumerable<PointF> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var copy = points.ToList();
            if (copy.Count < 3) throw new ArgumentException("A polygon requires at least three points.", nameof(points));
            for (int index = 0; index < copy.Count; index++) { VisualGuard.Finite(copy[index].X, nameof(points)); VisualGuard.Finite(copy[index].Y, nameof(points)); }
            float area = VisualRoiGeometryMath.SignedArea(copy);
            if (Math.Abs(area) <= 0.000001f) throw new ArgumentException("A polygon must have positive area.", nameof(points));
            if (VisualRoiGeometryMath.HasSelfIntersection(copy)) throw new ArgumentException("A polygon cannot self-intersect.", nameof(points));
            _points = new ReadOnlyCollection<PointF>(copy);
            Bounds = VisualRoiGeometryMath.Bounds(_points);
            Area = Math.Abs(area);
        }

        /// <inheritdoc />
        public VisualRoiGeometryKind Kind => VisualRoiGeometryKind.Polygon;
        /// <inheritdoc />
        public RectangleF Bounds { get; }
        /// <inheritdoc />
        public IReadOnlyList<PointF> Points => _points;
        /// <inheritdoc />
        public float Area { get; }
        /// <inheritdoc />
        public bool Contains(PointF point) => VisualRoiGeometryMath.Contains(_points, point);
    }

    /// <summary>Represents an owned binary pixel-mask ROI. / 表示由 ROI 拥有的二值像素掩码。</summary>
    public sealed class MaskRoiGeometry : IVisualRoiGeometry
    {
        private readonly byte[] _values;
        private readonly IReadOnlyList<PointF> _points;

        /// <summary>Initializes a mask. Non-zero values are inside the ROI. / 初始化掩码；非零值表示 ROI 内部。</summary>
        public MaskRoiGeometry(VisualSize sourceSize, byte[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            long expected = (long)sourceSize.Width * sourceSize.Height;
            if (values.LongLength != expected) throw new ArgumentException("Mask values must match the source size.", nameof(values));
            if (expected > 64L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(values), "ROI masks are limited to 64 million pixels.");
            SourceSize = sourceSize;
            _values = (byte[])values.Clone();
            _points = new ReadOnlyCollection<PointF>(new[]
            {
                new PointF(0, 0), new PointF(sourceSize.Width, 0), new PointF(sourceSize.Width, sourceSize.Height), new PointF(0, sourceSize.Height)
            });
            int count = 0;
            for (int index = 0; index < _values.Length; index++) if (_values[index] != 0) count++;
            PixelCount = count;
        }

        /// <summary>Gets mask source size. / 获取掩码源尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the number of non-zero pixels. / 获取非零像素数。</summary>
        public int PixelCount { get; }
        /// <summary>Gets a defensive row-major copy. / 获取防御性行优先副本。</summary>
        public byte[] ToArray() => (byte[])_values.Clone();

        /// <summary>Tests one integer mask pixel without allocating a copy. / 在不分配副本的情况下测试一个整数掩码像素。</summary>
        internal bool IsSet(int x, int y) => x >= 0 && y >= 0 && x < SourceSize.Width && y < SourceSize.Height && _values[(y * SourceSize.Width) + x] != 0;
        /// <inheritdoc />
        public VisualRoiGeometryKind Kind => VisualRoiGeometryKind.Mask;
        /// <inheritdoc />
        public RectangleF Bounds => new RectangleF(0, 0, SourceSize.Width, SourceSize.Height);
        /// <inheritdoc />
        public IReadOnlyList<PointF> Points => _points;
        /// <inheritdoc />
        public float Area => PixelCount;
        /// <inheritdoc />
        public bool Contains(PointF point)
        {
            if (float.IsNaN(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.X) || float.IsInfinity(point.Y)) return false;
            if (point.X < 0 || point.Y < 0 || point.X >= SourceSize.Width || point.Y >= SourceSize.Height) return false;
            int x = Math.Min(SourceSize.Width - 1, Math.Max(0, (int)Math.Floor(point.X)));
            int y = Math.Min(SourceSize.Height - 1, Math.Max(0, (int)Math.Floor(point.Y)));
            return _values[(y * SourceSize.Width) + x] != 0;
        }
    }

    /// <summary>Describes one immutable visual ROI and its execution policy. / 描述一个不可变视觉 ROI 及其执行策略。</summary>
    public sealed class VisualRoi
    {
        private readonly IReadOnlyList<VisualTaskId> _taskFilter;
        private readonly IReadOnlyList<int> _classFilter;
        private readonly IReadOnlyList<string> _tags;
        private readonly IReadOnlyDictionary<string, string> _metadata;

        /// <summary>Initializes an ROI. / 初始化 ROI。</summary>
        public VisualRoi(string id, IVisualRoiGeometry geometry, RoiCoordinateSpace coordinateSpace = RoiCoordinateSpace.SourcePixels, string? name = null, bool enabled = true, int priority = 0, RoiInclusionMode inclusionMode = RoiInclusionMode.Include, RoiExecutionMode executionMode = RoiExecutionMode.FilterResults, RoiHitTestMode hitTestMode = RoiHitTestMode.CenterPoint, float hitThreshold = 0.5f, float margin = 0, IEnumerable<VisualTaskId>? taskFilter = null, IEnumerable<int>? classFilter = null, IEnumerable<string>? tags = null, IEnumerable<KeyValuePair<string, string>>? metadata = null, float? confidenceOverride = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An ROI id is required.", nameof(id));
            if (id.Length > 128) throw new ArgumentOutOfRangeException(nameof(id));
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (!Enum.IsDefined(typeof(RoiCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if (!Enum.IsDefined(typeof(RoiInclusionMode), inclusionMode)) throw new ArgumentOutOfRangeException(nameof(inclusionMode));
            if (!Enum.IsDefined(typeof(RoiExecutionMode), executionMode)) throw new ArgumentOutOfRangeException(nameof(executionMode));
            if (!Enum.IsDefined(typeof(RoiHitTestMode), hitTestMode)) throw new ArgumentOutOfRangeException(nameof(hitTestMode));
            VisualGuard.Finite(hitThreshold, nameof(hitThreshold));
            VisualGuard.Finite(margin, nameof(margin));
            if (confidenceOverride.HasValue) VisualGuard.Finite(confidenceOverride.Value, nameof(confidenceOverride));
            if (hitThreshold < 0 || hitThreshold > 1) throw new ArgumentOutOfRangeException(nameof(hitThreshold));
            if (margin < 0) throw new ArgumentOutOfRangeException(nameof(margin));
            if (confidenceOverride.HasValue && (confidenceOverride.Value < 0 || confidenceOverride.Value > 1)) throw new ArgumentOutOfRangeException(nameof(confidenceOverride));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name!;
            if (Name.Length > 256) throw new ArgumentOutOfRangeException(nameof(name));
            Geometry = geometry;
            CoordinateSpace = coordinateSpace;
            Enabled = enabled;
            Priority = priority;
            InclusionMode = inclusionMode;
            ExecutionMode = executionMode;
            HitTestMode = hitTestMode;
            HitThreshold = hitThreshold;
            Margin = margin;
            ConfidenceOverride = confidenceOverride;
            _taskFilter = CopyTasks(taskFilter);
            _classFilter = CopyClasses(classFilter);
            _tags = CopyStrings(tags, nameof(tags), 32, 128);
            _metadata = CopyMetadata(metadata);
        }

        /// <summary>Gets the stable identifier. / 获取稳定标识。</summary>
        public string Id { get; }
        /// <summary>Gets the display name. / 获取显示名称。</summary>
        public string Name { get; }
        /// <summary>Gets whether the ROI is enabled. / 获取是否启用。</summary>
        public bool Enabled { get; }
        /// <summary>Gets the conflict priority. / 获取冲突优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets geometry. / 获取几何。</summary>
        public IVisualRoiGeometry Geometry { get; }
        /// <summary>Gets coordinate space. / 获取坐标空间。</summary>
        public RoiCoordinateSpace CoordinateSpace { get; }
        /// <summary>Gets inclusion policy. / 获取包含策略。</summary>
        public RoiInclusionMode InclusionMode { get; }
        /// <summary>Gets execution mode. / 获取执行模式。</summary>
        public RoiExecutionMode ExecutionMode { get; }
        /// <summary>Gets hit-test mode. / 获取命中规则。</summary>
        public RoiHitTestMode HitTestMode { get; }
        /// <summary>Gets hit threshold. / 获取命中阈值。</summary>
        public float HitThreshold { get; }
        /// <summary>Gets the non-negative crop margin in source units. / 获取源单位非负裁剪外扩。</summary>
        public float Margin { get; }
        /// <summary>Gets the optional per-ROI minimum result confidence. / 获取可选的 ROI 级结果最小置信度。</summary>
        /// <remarks>This value is applied by built-in result filters and by custom filters that provide a confidence selector; it does not alter model decoder thresholds. / 内置结果过滤器及提供置信度选择器的自定义过滤器会应用此值；它不会修改模型 decoder 的阈值。</remarks>
        public float? ConfidenceOverride { get; }
        /// <summary>Gets task filters. / 获取任务过滤器。</summary>
        public IReadOnlyList<VisualTaskId> TaskFilter => _taskFilter;
        /// <summary>Gets class-index filters. / 获取类别索引过滤器。</summary>
        public IReadOnlyList<int> ClassFilter => _classFilter;
        /// <summary>Gets application tags. / 获取应用标签。</summary>
        public IReadOnlyList<string> Tags => _tags;
        /// <summary>Gets immutable metadata. / 获取不可变元数据。</summary>
        public IReadOnlyDictionary<string, string> Metadata => _metadata;

        /// <summary>Tests whether this ROI applies to a task and optional class index. / 测试 ROI 是否适用于任务和可选类别。</summary>
        public bool AppliesTo(VisualTaskId task, int? classIndex = null)
        {
            if (!Enabled) return false;
            if (_taskFilter.Count > 0 && !_taskFilter.Contains(task)) return false;
            return !classIndex.HasValue || _classFilter.Count == 0 || _classFilter.Contains(classIndex.Value);
        }

        private static IReadOnlyList<VisualTaskId> CopyTasks(IEnumerable<VisualTaskId>? values)
        {
            if (values == null) return Array.Empty<VisualTaskId>();
            var copy = values.ToList();
            if (copy.Count > 32 || copy.Any(value => value.IsEmpty)) throw new ArgumentException("ROI task filters are invalid.", nameof(values));
            return new ReadOnlyCollection<VisualTaskId>(copy.Distinct().ToList());
        }

        private static IReadOnlyList<int> CopyClasses(IEnumerable<int>? values)
        {
            if (values == null) return Array.Empty<int>();
            var copy = values.ToList();
            if (copy.Count > 256 || copy.Any(value => value < 0)) throw new ArgumentException("ROI class filters are invalid.", nameof(values));
            return new ReadOnlyCollection<int>(copy.Distinct().OrderBy(value => value).ToList());
        }

        private static IReadOnlyList<string> CopyStrings(IEnumerable<string>? values, string name, int maxCount, int maxLength)
        {
            if (values == null) return Array.Empty<string>();
            var copy = values.ToList();
            if (copy.Count > maxCount || copy.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > maxLength)) throw new ArgumentException("ROI strings are invalid.", name);
            return new ReadOnlyCollection<string>(copy.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        private static IReadOnlyDictionary<string, string> CopyMetadata(IEnumerable<KeyValuePair<string, string>>? values)
        {
            var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (KeyValuePair<string, string> item in values)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 128 || item.Value == null || item.Value.Length > 2048) throw new ArgumentException("ROI metadata is invalid.", nameof(values));
                    if (copy.Count >= 64 && !copy.ContainsKey(item.Key)) throw new ArgumentOutOfRangeException(nameof(values));
                    copy[item.Key] = item.Value;
                }
            }
            return new ReadOnlyDictionary<string, string>(copy);
        }
    }

    /// <summary>Contains an immutable ROI snapshot bound to one source size. / 包含绑定到一个源尺寸的不可变 ROI 快照。</summary>
    public sealed class VisualRoiSnapshot
    {
        private readonly IReadOnlyList<VisualRoi> _rois;

        /// <summary>Initializes a snapshot. / 初始化快照。</summary>
        public VisualRoiSnapshot(VisualSize sourceSize, IEnumerable<VisualRoi> rois, long version = 1)
        {
            if (rois == null) throw new ArgumentNullException(nameof(rois));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            var copy = rois.ToList();
            if (copy.Count > 4096) throw new ArgumentOutOfRangeException(nameof(rois));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (VisualRoi roi in copy)
            {
                if (roi == null) throw new ArgumentException("ROI collections cannot contain null values.", nameof(rois));
                if (!ids.Add(roi.Id)) throw new ArgumentException("ROI identifiers must be unique.", nameof(rois));
                if (IsPoseOnlyHitMode(roi.HitTestMode) && (roi.TaskFilter.Count == 0 || roi.TaskFilter.Any(task => task != VisualTaskId.PoseEstimation))) throw new ArgumentException("Pose keypoint hit modes require a PoseEstimation-only task filter.", nameof(rois));
                if (roi.HitTestMode == RoiHitTestMode.MaskIntersectionRatio && (roi.TaskFilter.Count == 0 || roi.TaskFilter.Any(task => task != VisualTaskId.InstanceSegmentation))) throw new ArgumentException("MaskIntersectionRatio requires an InstanceSegmentation-only task filter.", nameof(rois));
            }
            SourceSize = sourceSize;
            Version = version;
            _rois = new ReadOnlyCollection<VisualRoi>(copy.OrderByDescending(value => value.Priority).ThenBy(value => value.Id, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets source image size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets monotonically increasing snapshot version. / 获取单调递增快照版本。</summary>
        public long Version { get; }
        /// <summary>Gets deterministic ROI order. / 获取确定性 ROI 顺序。</summary>
        public IReadOnlyList<VisualRoi> Rois => _rois;

        private static bool IsPoseOnlyHitMode(RoiHitTestMode mode)
        {
            return mode == RoiHitTestMode.KeypointCoverage || mode == RoiHitTestMode.AllKeypoints || mode == RoiHitTestMode.AnyKeypoint || mode == RoiHitTestMode.VisibleKeypointRatio;
        }

        /// <summary>Resolves normalized geometry into source-pixel geometry. / 将归一化几何解析为源像素几何。</summary>
        public IVisualRoiGeometry Resolve(VisualRoi roi)
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            IVisualRoiGeometry resolved = roi.CoordinateSpace == RoiCoordinateSpace.SourcePixels
                ? roi.Geometry
                : roi.CoordinateSpace == RoiCoordinateSpace.Normalized
                    ? VisualRoiGeometryMath.Scale(roi.Geometry, SourceSize.Width, SourceSize.Height)
                    : throw new NotSupportedException("Only SourcePixels and Normalized ROI coordinates can be resolved without a model or calibration context.");
            return roi.Margin <= 0 ? resolved : VisualRoiGeometryMath.Expand(resolved, roi.Margin);
        }

        /// <summary>Resolves an ROI using a concrete model-input frame. ModelInput geometry is inverse-mapped; other non-source spaces still require their own context. / 使用具体模型输入帧解析 ROI；ModelInput 几何执行逆变换，其他非源空间仍需各自上下文。</summary>
        public IVisualRoiGeometry Resolve(VisualRoi roi, VisualInputFrame frame)
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.SourceSize != SourceSize) throw new ArgumentException("The input frame source size must match the ROI snapshot.", nameof(frame));
            if (roi.CoordinateSpace == RoiCoordinateSpace.ModelInput)
            {
                IVisualRoiGeometry modelResolved = VisualRoiGeometryMath.FromModelInput(roi.Geometry, frame.Transform);
                return roi.Margin <= 0 ? modelResolved : VisualRoiGeometryMath.Expand(modelResolved, roi.Margin);
            }
            return Resolve(roi);
        }

        /// <summary>Resolves an ROI using an explicit tile or world coordinate context. / 使用显式切片或世界坐标上下文解析 ROI。</summary>
        public IVisualRoiGeometry Resolve(VisualRoi roi, VisualRoiCoordinateContext context)
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.SourceSize != SourceSize) throw new ArgumentException("The coordinate context source size must match the ROI snapshot.", nameof(context));
            IVisualRoiGeometry resolved;
            if (roi.CoordinateSpace == RoiCoordinateSpace.SourcePixels || roi.CoordinateSpace == RoiCoordinateSpace.Normalized || roi.CoordinateSpace == RoiCoordinateSpace.ModelInput)
            {
                throw new ArgumentException("The explicit coordinate context overload is intended for TileLocal or World ROIs.", nameof(roi));
            }

            resolved = context.Resolve(roi);
            return roi.Margin <= 0 ? resolved : VisualRoiGeometryMath.Expand(resolved, roi.Margin);
        }

        /// <summary>Resolves an ROI against a frame and an explicit tile or world coordinate context. / 使用输入帧和显式切片或世界坐标上下文解析 ROI。</summary>
        public IVisualRoiGeometry Resolve(VisualRoi roi, VisualInputFrame frame, VisualRoiCoordinateContext context)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.SourceSize != SourceSize) throw new ArgumentException("The input frame source size must match the ROI snapshot.", nameof(frame));
            return roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World ? Resolve(roi, context) : Resolve(roi, frame);
        }
    }

    /// <summary>Provides atomic immutable ROI configuration replacement. / 提供不可变 ROI 配置的原子替换。</summary>
    public sealed class VisualRoiManager
    {
        private readonly object _gate = new object();
        private VisualRoiSnapshot _snapshot;

        /// <summary>Initializes a manager. / 初始化管理器。</summary>
        public VisualRoiManager(VisualRoiSnapshot initialSnapshot)
        {
            _snapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
        }

        /// <summary>Gets the current snapshot. / 获取当前快照。</summary>
        public VisualRoiSnapshot Snapshot { get { return System.Threading.Volatile.Read(ref _snapshot); } }

        /// <summary>Atomically replaces the snapshot and increments its version. / 原子替换快照并递增版本。</summary>
        public VisualRoiSnapshot Replace(VisualSize sourceSize, IEnumerable<VisualRoi> rois)
        {
            if (rois == null) throw new ArgumentNullException(nameof(rois));
            lock (_gate)
            {
                VisualRoiSnapshot next = new VisualRoiSnapshot(sourceSize, rois, checked(_snapshot.Version + 1));
                System.Threading.Volatile.Write(ref _snapshot, next);
                return next;
            }
        }
    }

    internal static class VisualRoiGeometryMath
    {
        internal static RectangleF Bounds(IReadOnlyList<PointF> points)
        {
            float minX = points[0].X, minY = points[0].Y, maxX = points[0].X, maxY = points[0].Y;
            for (int index = 1; index < points.Count; index++) { PointF point = points[index]; minX = Math.Min(minX, point.X); minY = Math.Min(minY, point.Y); maxX = Math.Max(maxX, point.X); maxY = Math.Max(maxY, point.Y); }
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        internal static float SignedArea(IReadOnlyList<PointF> points)
        {
            double sum = 0;
            for (int index = 0; index < points.Count; index++) { PointF first = points[index]; PointF second = points[(index + 1) % points.Count]; sum += (first.X * second.Y) - (second.X * first.Y); }
            return (float)(sum / 2d);
        }

        internal static bool Contains(IReadOnlyList<PointF> points, PointF point)
        {
            if (float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y)) return false;
            bool inside = false;
            for (int index = 0, previous = points.Count - 1; index < points.Count; previous = index++)
            {
                PointF current = points[index]; PointF prior = points[previous];
                // Treat polygon boundaries as owned pixels/locations. Without this
                // explicit check the ray-cast result depends on which adjacent edge
                // happens to be visited first, causing ROI flicker at exact borders.
                // 将多边形边界定义为属于 ROI；否则射线算法会因边遍历顺序
                // 在精确边界上产生不稳定的命中结果，导致 ROI 抖动。
                if (PointOnSegment(point, prior, current)) return true;
                bool crosses = ((current.Y > point.Y) != (prior.Y > point.Y)) && point.X < ((prior.X - current.X) * (point.Y - current.Y) / (prior.Y - current.Y)) + current.X;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static bool PointOnSegment(PointF point, PointF start, PointF end)
        {
            const double epsilon = 0.000001;
            double cross = ((double)point.X - start.X) * ((double)end.Y - start.Y) - ((double)point.Y - start.Y) * ((double)end.X - start.X);
            if (Math.Abs(cross) > epsilon) return false;
            return point.X >= Math.Min(start.X, end.X) - epsilon && point.X <= Math.Max(start.X, end.X) + epsilon && point.Y >= Math.Min(start.Y, end.Y) - epsilon && point.Y <= Math.Max(start.Y, end.Y) + epsilon;
        }

        internal static bool HasSelfIntersection(IReadOnlyList<PointF> points)
        {
            for (int first = 0; first < points.Count; first++)
            {
                PointF a1 = points[first], a2 = points[(first + 1) % points.Count];
                for (int second = first + 1; second < points.Count; second++)
                {
                    if (second == first || (second + 1) % points.Count == first || second == (first + 1) % points.Count) continue;
                    if (SegmentsIntersect(a1, a2, points[second], points[(second + 1) % points.Count])) return true;
                }
            }
            return false;
        }

        internal static IVisualRoiGeometry Scale(IVisualRoiGeometry geometry, int width, int height)
        {
            if (geometry.Kind == VisualRoiGeometryKind.Mask) throw new NotSupportedException("A mask ROI must use source-pixel coordinates.");
            var points = geometry.Points.Select(point => new PointF(point.X * width, point.Y * height)).ToList();
            if (geometry.Kind == VisualRoiGeometryKind.Rectangle) return new RectangleRoiGeometry(Bounds(points));
            if (geometry is RotatedRectangleRoiGeometry rotated)
            {
                // Non-uniform normalization changes a rotated rectangle into a
                // general quadrilateral; retaining the original angle would
                // silently change its footprint and hit-test semantics.
                if (width != height) return new PolygonRoiGeometry(points);
                return new RotatedRectangleRoiGeometry(new PointF(rotated.Center.X * width, rotated.Center.Y * height), new SizeF(rotated.Size.Width * width, rotated.Size.Height * height), rotated.AngleDegrees);
            }
            return new PolygonRoiGeometry(points);
        }

        internal static IVisualRoiGeometry FromModelInput(IVisualRoiGeometry geometry, ImageTransform transform)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (geometry.Kind == VisualRoiGeometryKind.Mask) throw new NotSupportedException("A model-input mask ROI requires an explicit mask resampling policy.");
            var points = geometry.Points.Select(transform.ToSource).ToList();
            if (geometry.Kind == VisualRoiGeometryKind.Rectangle) return new RectangleRoiGeometry(Bounds(points));
            if (geometry is RotatedRectangleRoiGeometry rotated && Math.Abs(transform.ScaleX - transform.ScaleY) <= 0.000001f)
            {
                return new RotatedRectangleRoiGeometry(transform.ToSource(rotated.Center), new SizeF(rotated.Size.Width / transform.ScaleX, rotated.Size.Height / transform.ScaleY), rotated.AngleDegrees);
            }
            return new PolygonRoiGeometry(points);
        }

        internal static IVisualRoiGeometry Expand(IVisualRoiGeometry geometry, float margin)
        {
            if (margin < 0 || float.IsNaN(margin) || float.IsInfinity(margin)) throw new ArgumentOutOfRangeException(nameof(margin));
            if (geometry is RectangleRoiGeometry rectangle)
            {
                return new RectangleRoiGeometry(new RectangleF(rectangle.Rectangle.X - margin, rectangle.Rectangle.Y - margin, rectangle.Rectangle.Width + (2 * margin), rectangle.Rectangle.Height + (2 * margin)));
            }
            if (geometry is RotatedRectangleRoiGeometry rotated)
            {
                return new RotatedRectangleRoiGeometry(rotated.Center, new SizeF(rotated.Size.Width + (2 * margin), rotated.Size.Height + (2 * margin)), rotated.AngleDegrees);
            }
            throw new NotSupportedException("Margin expansion is currently supported for rectangle and rotated-rectangle ROIs only.");
        }

        internal static bool SegmentsIntersect(PointF a, PointF b, PointF c, PointF d)
        {
            float first = Orientation(a, b, c), second = Orientation(a, b, d), third = Orientation(c, d, a), fourth = Orientation(c, d, b);
            return ((first > 0 && second < 0) || (first < 0 && second > 0)) && ((third > 0 && fourth < 0) || (third < 0 && fourth > 0));
        }

        private static float Orientation(PointF a, PointF b, PointF c) => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
    }
}
