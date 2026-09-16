using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Controls deterministic conversion of an ROI into a promptable-segmentation prompt. / 控制将 ROI 确定性转换为提示式分割提示。</summary>
    public sealed class VisualRoiPromptOptions
    {
        /// <summary>Initializes prompt options. / 初始化提示选项。</summary>
        public VisualRoiPromptOptions(int maximumPoints = 16, bool includeBoundaryNegatives = true, bool returnMultipleMasks = false)
        {
            if (maximumPoints <= 0 || maximumPoints > 256) throw new ArgumentOutOfRangeException(nameof(maximumPoints));
            MaximumPoints = maximumPoints;
            IncludeBoundaryNegatives = includeBoundaryNegatives;
            ReturnMultipleMasks = returnMultipleMasks;
        }

        /// <summary>Gets the maximum number of point prompts. / 获取点提示的最大数量。</summary>
        public int MaximumPoints { get; }
        /// <summary>Gets whether outside boundary samples should be added as negative points. / 获取是否添加边界外负点。</summary>
        public bool IncludeBoundaryNegatives { get; }
        /// <summary>Gets whether the decoder should return every candidate mask. / 获取是否返回全部候选掩码。</summary>
        public bool ReturnMultipleMasks { get; }
    }

    /// <summary>Converts source-space visual ROIs into SAM-compatible point and box prompts. / 将源图空间视觉 ROI 转换为兼容 SAM 的点和框提示。</summary>
    public static class VisualRoiPromptFactory
    {
        /// <summary>Creates a prompt for one enabled Include ROI. / 为一个启用的 Include ROI 创建提示。</summary>
        public static PromptableSegmentationPrompt Create(
            VisualRoiSnapshot snapshot,
            VisualRoi roi,
            VisualRoiPromptOptions? options = null,
            string? promptId = null,
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (roi == null) throw new ArgumentNullException(nameof(roi));
            if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include) throw new VisualException(VisualErrorCodes.InputInvalid, "Prompt ROI must be enabled and use Include mode.", technicalDetails: "roiId=" + roi.Id);
            options ??= new VisualRoiPromptOptions();

            IVisualRoiGeometry geometry = VisualRoiResolution.Resolve(snapshot, roi, coordinateContext);
            RectangleF box = ClipBox(geometry.Bounds, snapshot.SourceSize);
            if (box.Width <= 0 || box.Height <= 0) throw new VisualException(VisualErrorCodes.InputInvalid, "The prompt ROI does not intersect the source image.", technicalDetails: "roiId=" + roi.Id);

            var points = new List<PromptPoint>(Math.Min(options.MaximumPoints, 1 + geometry.Points.Count));
            PointF anchor = FindAnchor(geometry, box);
            points.Add(new PromptPoint(anchor.X, anchor.Y, PromptPointLabel.Foreground));

            if (geometry.Kind != VisualRoiGeometryKind.Mask)
            {
                AddBoundaryPoints(points, geometry.Points, geometry, options.MaximumPoints);
                if (options.IncludeBoundaryNegatives) AddNegativeCorners(points, box, geometry, options.MaximumPoints);
            }
            else if (options.IncludeBoundaryNegatives)
            {
                AddNegativeCorners(points, box, geometry, options.MaximumPoints);
            }

            return new PromptableSegmentationPrompt(points, box, returnMultipleMasks: options.ReturnMultipleMasks, promptId: promptId ?? roi.Id);
        }

        /// <summary>Creates prompts for all enabled Include ROIs that apply to a task. / 为适用任务的所有启用 Include ROI 创建提示。</summary>
        public static IReadOnlyList<PromptableSegmentationPrompt> CreateMany(
            VisualRoiSnapshot snapshot,
            VisualTaskId task,
            VisualRoiPromptOptions? options = null,
            VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (task.IsEmpty) throw new ArgumentException("A visual task is required.", nameof(task));
            var prompts = new List<PromptableSegmentationPrompt>();
            foreach (VisualRoi roi in snapshot.Rois)
            {
                if (!roi.Enabled || roi.InclusionMode != RoiInclusionMode.Include || !roi.AppliesTo(task)) continue;
                prompts.Add(Create(snapshot, roi, options, roi.Id, coordinateContext));
            }
            return new ReadOnlyCollection<PromptableSegmentationPrompt>(prompts);
        }

        private static void AddBoundaryPoints(ICollection<PromptPoint> points, IReadOnlyList<PointF> vertices, IVisualRoiGeometry geometry, int maximum)
        {
            for (int index = 0; index < vertices.Count && points.Count < maximum; index++)
            {
                PointF vertex = vertices[index];
                if (geometry.Contains(vertex) || geometry.Kind == VisualRoiGeometryKind.Polygon || geometry.Kind == VisualRoiGeometryKind.RotatedRectangle || geometry.Kind == VisualRoiGeometryKind.Rectangle)
                {
                    points.Add(new PromptPoint(vertex.X, vertex.Y, PromptPointLabel.Foreground));
                }
            }
        }

        private static void AddNegativeCorners(ICollection<PromptPoint> points, RectangleF box, IVisualRoiGeometry geometry, int maximum)
        {
            float inset = Math.Min(Math.Max(Math.Min(box.Width, box.Height) * .05f, .5f), 4f);
            PointF[] corners =
            {
                new PointF(box.X + inset, box.Y + inset),
                new PointF(box.Right - inset, box.Y + inset),
                new PointF(box.Right - inset, box.Bottom - inset),
                new PointF(box.X + inset, box.Bottom - inset)
            };
            for (int index = 0; index < corners.Length && points.Count < maximum; index++)
            {
                if (!geometry.Contains(corners[index])) points.Add(new PromptPoint(corners[index].X, corners[index].Y, PromptPointLabel.Background));
            }
        }

        private static PointF FindAnchor(IVisualRoiGeometry geometry, RectangleF box)
        {
            if (geometry is MaskRoiGeometry mask)
            {
                byte[] values = mask.ToArray();
                long sumX = 0, sumY = 0, count = 0;
                for (int y = 0; y < mask.SourceSize.Height; y++)
                {
                    for (int x = 0; x < mask.SourceSize.Width; x++)
                    {
                        if (values[(y * mask.SourceSize.Width) + x] == 0) continue;
                        sumX += x;
                        sumY += y;
                        count++;
                    }
                }
                if (count > 0) return new PointF((float)sumX / count + .5f, (float)sumY / count + .5f);
            }

            PointF center = new PointF(box.X + (box.Width / 2f), box.Y + (box.Height / 2f));
            if (geometry.Contains(center)) return center;
            foreach (PointF point in geometry.Points) if (geometry.Contains(point)) return point;
            return center;
        }

        private static RectangleF ClipBox(RectangleF value, VisualSize sourceSize)
        {
            float left = Math.Max(0, Math.Min(sourceSize.Width, value.X));
            float top = Math.Max(0, Math.Min(sourceSize.Height, value.Y));
            float right = Math.Max(left, Math.Min(sourceSize.Width, value.Right));
            float bottom = Math.Max(top, Math.Min(sourceSize.Height, value.Bottom));
            return new RectangleF(left, top, right - left, bottom - top);
        }
    }
}
