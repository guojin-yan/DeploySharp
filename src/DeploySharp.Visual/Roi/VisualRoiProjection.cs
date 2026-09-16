using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Describes the reversible geometry used by one ROI input. / 描述一个 ROI 输入使用的可逆几何。</summary>
    public sealed class RoiProjection
    {
        /// <summary>Initializes a projection. / 初始化投影。</summary>
        public RoiProjection(string roiId, VisualSize sourceSize, VisualSize modelSize, ImageTransform transform, RectangleF? roiBounds = null, IVisualRoiGeometry? sourceGeometry = null)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (transform.SourceSize != sourceSize || transform.ModelSize != modelSize) throw new ArgumentException("The transform sizes must match the projection sizes.", nameof(transform));
            RoiId = roiId;
            SourceSize = sourceSize;
            ModelSize = modelSize;
            Transform = transform;
            RectangleF transformBounds = transform.ClipToSource(transform.ToSource(new RectangleF(0, 0, modelSize.Width, modelSize.Height)));
            SourceGeometry = sourceGeometry;
            SourceBounds = Intersect(transformBounds, sourceGeometry?.Bounds ?? roiBounds ?? transformBounds);
        }

        /// <summary>Creates a projection from a prepared input. / 根据已准备输入创建投影。</summary>
        public static RoiProjection FromPreparedInput(VisualRoi roi, PreparedVisualInput input)
        {
            return FromPreparedInput(roi, input, 0);
        }

        /// <summary>Creates a projection from one prepared-input batch row. / 根据已准备输入的一个 Batch 行创建投影。</summary>
        public static RoiProjection FromPreparedInput(VisualRoi roi, PreparedVisualInput input, int batchIndex)
        {
            return FromPreparedInput(roi, input, batchIndex, null);
        }

        /// <summary>Creates a projection from one batch row and an explicit tile/world coordinate context. / 根据一个 Batch 行和显式切片/世界坐标上下文创建投影。</summary>
        public static RoiProjection FromPreparedInput(VisualRoi roi, PreparedVisualInput input, int batchIndex, VisualRoiCoordinateContext? coordinateContext)
        {
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (batchIndex < 0 || batchIndex >= input.BatchFrames.Count) throw new ArgumentOutOfRangeException(nameof(batchIndex));
            VisualInputFrame frame = input.BatchFrames[batchIndex];
            bool contextResolved = roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World;
            IVisualRoiGeometry geometry = roi.CoordinateSpace == RoiCoordinateSpace.SourcePixels
                ? roi.Geometry
                : roi.CoordinateSpace == RoiCoordinateSpace.Normalized
                    ? VisualRoiGeometryMath.Scale(roi.Geometry, frame.SourceSize.Width, frame.SourceSize.Height)
                    : roi.CoordinateSpace == RoiCoordinateSpace.ModelInput
                        ? VisualRoiGeometryMath.FromModelInput(roi.Geometry, frame.Transform)
                        : coordinateContext == null
                            ? throw new NotSupportedException("TileLocal and World ROI coordinates require an explicit projection context.")
                            : new VisualRoiSnapshot(frame.SourceSize, new[] { roi }).Resolve(roi, coordinateContext);
            if (coordinateContext != null && (roi.CoordinateSpace == RoiCoordinateSpace.TileLocal || roi.CoordinateSpace == RoiCoordinateSpace.World) && coordinateContext.SourceSize != frame.SourceSize) throw new ArgumentException("The coordinate context source size must match the prepared frame.", nameof(coordinateContext));
            if (!contextResolved && roi.Margin > 0) geometry = VisualRoiGeometryMath.Expand(geometry, roi.Margin);
            return new RoiProjection(roi.Id, frame.SourceSize, frame.ModelSize, frame.Transform, geometry.Bounds, geometry);
        }

        /// <summary>Gets the ROI identifier. / 获取 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets the source size. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the model size. / 获取模型尺寸。</summary>
        public VisualSize ModelSize { get; }
        /// <summary>Gets the source/model transform. / 获取源图与模型变换。</summary>
        public ImageTransform Transform { get; }
        /// <summary>Gets the source-space bounds of the ROI crop. / 获取 ROI 裁剪的源图边界。</summary>
        public RectangleF SourceBounds { get; }
        /// <summary>Gets the optional resolved source-space ROI geometry. / 获取可选的已解析源图 ROI 几何。</summary>
        /// <remarks>When present, dense result projectors use this geometry for per-pixel ownership rather than treating the whole bounding rectangle as part of the ROI. / 存在时，稠密结果投影器按逐像素几何归属处理，而不是把整个外接矩形都视为 ROI。</remarks>
        public IVisualRoiGeometry? SourceGeometry { get; }

        /// <summary>Maps a model-space point to source pixels. / 将模型空间点映射到源像素。</summary>
        public PointF ToSource(PointF modelPoint) => Transform.ToSource(modelPoint);
        /// <summary>Maps an ordered model-space point sequence to source pixels. / 将有序模型空间点序列映射到源像素。</summary>
        public IReadOnlyList<PointF> ToSourcePoints(IEnumerable<PointF> modelPoints)
        {
            if (modelPoints == null) throw new ArgumentNullException(nameof(modelPoints));
            return new ReadOnlyCollection<PointF>(modelPoints.Select(ToSource).ToList());
        }
        /// <summary>Maps a model-space rectangle to a clipped source rectangle. / 将模型空间矩形映射并裁切到源图。</summary>
        public RectangleF ToSource(RectangleF modelRectangle)
        {
            RectangleF source = Transform.ClipToSource(Transform.ToSource(modelRectangle));
            float left = Math.Max(SourceBounds.X, source.X);
            float top = Math.Max(SourceBounds.Y, source.Y);
            float right = Math.Min(SourceBounds.Right, source.Right);
            float bottom = Math.Min(SourceBounds.Bottom, source.Bottom);
            return new RectangleF(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }
        /// <summary>Maps a source-space point to model pixels. / 将源像素点映射到模型空间。</summary>
        public PointF ToModel(PointF sourcePoint) => Transform.ToModel(sourcePoint);
        /// <summary>Maps an ordered source-space point sequence to model pixels. / 将有序源图空间点序列映射到模型像素。</summary>
        public IReadOnlyList<PointF> ToModelPoints(IEnumerable<PointF> sourcePoints)
        {
            if (sourcePoints == null) throw new ArgumentNullException(nameof(sourcePoints));
            return new ReadOnlyCollection<PointF>(sourcePoints.Select(ToModel).ToList());
        }
        /// <summary>Maps a source-space rectangle to model pixels. / 将源矩形映射到模型空间。</summary>
        public RectangleF ToModel(RectangleF sourceRectangle) => Transform.ToModel(sourceRectangle);

        /// <summary>Tests whether an integer source pixel belongs to the resolved ROI geometry. / 测试整数源像素是否属于已解析 ROI 几何。</summary>
        /// <remarks>The check uses pixel-center semantics for polygons, rotated rectangles and masks, and returns false outside the source image. Custom dense-result projectors should use this method before writing a projected pixel. / 多边形、旋转矩形和掩码使用像素中心语义；源图外始终返回 false。自定义稠密结果投影器应在写入投影像素前调用此方法。</remarks>
        public bool ContainsSourcePixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= SourceSize.Width || y >= SourceSize.Height) return false;
            return OwnsSourcePixel(x, y);
        }

        internal bool OwnsSourcePixel(int x, int y)
        {
            PointF center = new PointF(x + .5f, y + .5f);
            if (SourceGeometry == null)
            {
                return center.X >= SourceBounds.X && center.X < SourceBounds.Right && center.Y >= SourceBounds.Y && center.Y < SourceBounds.Bottom;
            }
            return SourceGeometry.Contains(center);
        }

        /// <summary>Projects an axis-aligned detection decoded in model space. / 投影一个在模型空间解码的轴对齐检测。</summary>
        public Detection Project(Detection detection)
        {
            if (detection == null) throw new ArgumentNullException(nameof(detection));
            return new Detection(ToSource(detection.Box), detection.Label);
        }

        private static RectangleF Intersect(RectangleF first, RectangleF second)
        {
            float left = Math.Max(first.X, second.X);
            float top = Math.Max(first.Y, second.Y);
            float right = Math.Min(first.Right, second.Right);
            float bottom = Math.Min(first.Bottom, second.Bottom);
            return new RectangleF(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }
    }

    /// <summary>Associates a projected detection with its ROI source. / 将投影检测与 ROI 来源关联。</summary>
    public sealed class RoiProjectedDetection
    {
        private readonly IReadOnlyList<string> _contributingRoiIds;

        /// <summary>Initializes a projected detection. / 初始化投影检测。</summary>
        public RoiProjectedDetection(string roiId, Detection detection, int? windowIndex = null, int priority = 0, IEnumerable<string>? contributingRoiIds = null)
        {
            if (string.IsNullOrWhiteSpace(roiId)) throw new ArgumentException("An ROI id is required.", nameof(roiId));
            Detection = detection ?? throw new ArgumentNullException(nameof(detection));
            RoiId = roiId;
            WindowIndex = windowIndex;
            Priority = priority;
            var ids = contributingRoiIds == null ? new List<string>() : contributingRoiIds.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            ids.Add(roiId);
            _contributingRoiIds = new ReadOnlyCollection<string>(ids.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets ROI identifier. / 获取 ROI 标识。</summary>
        public string RoiId { get; }
        /// <summary>Gets projected source-space detection. / 获取投影后的源图检测。</summary>
        public Detection Detection { get; }
        /// <summary>Gets optional window index. / 获取可选窗口索引。</summary>
        public int? WindowIndex { get; }
        /// <summary>Gets the ROI priority used by deterministic merging. / 获取确定性合并使用的 ROI 优先级。</summary>
        public int Priority { get; }
        /// <summary>Gets every ROI that contributed an overlapping result. / 获取贡献重叠结果的全部 ROI。</summary>
        public IReadOnlyList<string> ContributingRoiIds => _contributingRoiIds;
    }
}
