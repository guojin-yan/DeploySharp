using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Projects an explicitly model-space Detection result into full source coordinates. / 将显式模型空间 Detection 结果投影到完整源图坐标。</summary>
    public sealed class ModelSpaceDetectionRoiProjector : IRoiResultProjector<DetectionResult>
    {
        /// <inheritdoc />
        public DetectionResult Project(DetectionResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            return new DetectionResult(result.Detections.Select(projection.Project));
        }
    }

    /// <summary>Projects an explicitly model-space OBB result into full source coordinates. / 将显式模型空间 OBB 结果投影到完整源图坐标。</summary>
    public sealed class ModelSpaceOrientedDetectionRoiProjector : IRoiResultProjector<OrientedDetectionResult>
    {
        /// <inheritdoc />
        public OrientedDetectionResult Project(OrientedDetectionResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            RequireModelSize(result.SourceSize, projection);
            // A perspective transform does not preserve a rectangle's single angle, even when
            // its affine scale fields happen to be equal. Preserve the exact angle only for
            // non-projective uniform-scale transforms; the canonical quadrilateral remains
            // authoritative for every transform.
            bool preservesAngle = !projection.Transform.IsProjective && Math.Abs(projection.Transform.ScaleX - projection.Transform.ScaleY) <= .000001f;
            var detections = new List<OrientedDetection>(result.Detections.Count);
            foreach (OrientedDetection detection in result.Detections)
            {
                OrientedQuadrilateral quadrilateral = OrientedQuadrilateral.Canonicalize(projection.ToSourcePoints(detection.Quadrilateral.Vertices), OrientedVertexOrder.CounterClockwise);
                bool exact = preservesAngle && detection.HasExactRotatedRectangle;
                detections.Add(new OrientedDetection(detection.SourceIndex, detection.ClassIndex, detection.Label, detection.Score, quadrilateral, exact ? detection.AngleRadiansCounterClockwise : null, exact, detection.ExternalId, detection.Metadata));
            }
            return new OrientedDetectionResult(detections, projection.SourceSize, result.ProfileId, result.ModelId);
        }

        private static void RequireModelSize(VisualSize actual, RoiProjection projection)
        {
            if (actual != projection.ModelSize) throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space OBB result dimensions must match the ROI projection model dimensions.");
        }
    }

    /// <summary>Projects an explicitly model-space Pose result into full source coordinates. / 将显式模型空间 Pose 结果投影到完整源图坐标。</summary>
    public sealed class ModelSpacePoseRoiProjector : IRoiResultProjector<PoseEstimationResult>
    {
        /// <inheritdoc />
        public PoseEstimationResult Project(PoseEstimationResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (result.SourceSize != projection.ModelSize) throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space Pose result dimensions must match the ROI projection model dimensions.");
            var instances = new List<PoseInstance>(result.Instances.Count);
            foreach (PoseInstance instance in result.Instances)
            {
                var keypoints = new List<PoseKeypoint>(instance.Keypoints.Count);
                foreach (PoseKeypoint keypoint in instance.Keypoints) keypoints.Add(new PoseKeypoint(keypoint.Index, projection.ToSource(keypoint.Point), keypoint.Score, keypoint.Visibility, keypoint.IsValid));
                RectangleF? box = instance.BoundingBox.HasValue ? projection.ToSource(instance.BoundingBox.Value) : (RectangleF?)null;
                instances.Add(new PoseInstance(instance.SourceIndex, instance.Score, keypoints, box, instance.ClassIndex, instance.ExternalId));
            }
            return new PoseEstimationResult(result.Topology, instances, projection.SourceSize, result.ProfileId, result.ModelId);
        }
    }

    /// <summary>Projects explicitly model-space OCR detection polygons into full source coordinates. / 将显式模型空间 OCR 检测多边形投影到完整源图坐标。</summary>
    public sealed class ModelSpaceTextDetectionRoiProjector : IRoiResultProjector<TextDetectionResult>
    {
        /// <inheritdoc />
        public TextDetectionResult Project(TextDetectionResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (result.SourceSize != projection.ModelSize) throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space text result dimensions must match the ROI projection model dimensions.");
            var regions = new List<TextRegion>(result.Regions.Count);
            foreach (TextRegion region in result.Regions)
            {
                TextPolygon polygon = TextPolygon.Canonicalize(projection.ToSourcePoints(region.Polygon.Vertices), OrientedVertexOrder.CounterClockwise);
                TextQuadrilateral? quadrilateral = region.CropQuadrilateral == null ? null : new TextQuadrilateral(
                    projection.ToSource(region.CropQuadrilateral.TopLeft),
                    projection.ToSource(region.CropQuadrilateral.TopRight),
                    projection.ToSource(region.CropQuadrilateral.BottomRight),
                    projection.ToSource(region.CropQuadrilateral.BottomLeft),
                    TextCornerOrder.TopLeftClockwise);
                regions.Add(new TextRegion(region.SourceIndex, region.Score, polygon, quadrilateral, region.Orientation, region.AngleRadians, region.Language, region.Script, region.ExternalId, region.Metadata));
            }
            return new TextDetectionResult(regions, projection.SourceSize, result.ProfileId, result.ModelId);
        }
    }

    /// <summary>Projects an explicitly model-space OCR result (regions and recognition) into full source coordinates. / 将显式模型空间完整 OCR 结果（区域与识别文本）投影到完整源图坐标。</summary>
    /// <remarks>Global orientation correction provenance cannot be inferred from an arbitrary ROI transform. Results carrying <see cref="OcrResult.Orientation"/> are therefore rejected instead of silently applying the wrong inverse rotation. Per-region orientation metadata is retained because it describes recognition semantics rather than the crop transform. / 任意 ROI 变换无法推断全局方向校正来源，因此带有 <see cref="OcrResult.Orientation"/> 的结果会显式拒绝，而不是静默应用错误的逆旋转；逐文本区域方向元数据描述识别语义，会被保留。</remarks>
    public sealed class ModelSpaceOcrRoiProjector : IRoiResultProjector<OcrResult>
    {
        /// <inheritdoc />
        public OcrResult Project(OcrResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (result.SourceSize != projection.ModelSize) throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space OCR result dimensions must match the ROI projection model dimensions.");
            if (result.Orientation != null) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "Model-space OCR projection with global orientation provenance requires an orientation-aware projector; refusing to guess the inverse rotation.");

            var regions = new List<OcrRegionResult>(result.Regions.Count);
            foreach (OcrRegionResult item in result.Regions)
            {
                TextRegion source = item.Region;
                TextPolygon polygon;
                TextQuadrilateral? quadrilateral = null;
                if (source.CropQuadrilateral != null)
                {
                    PointF[] points = new[]
                    {
                        projection.ToSource(source.CropQuadrilateral.TopLeft),
                        projection.ToSource(source.CropQuadrilateral.TopRight),
                        projection.ToSource(source.CropQuadrilateral.BottomRight),
                        projection.ToSource(source.CropQuadrilateral.BottomLeft)
                    };
                    quadrilateral = CreateQuadrilateral(points);
                    polygon = quadrilateral.Polygon;
                }
                else
                {
                    polygon = TextPolygon.Canonicalize(projection.ToSourcePoints(source.Polygon.Vertices), OrientedVertexOrder.CounterClockwise);
                }

                var projectedRegion = new TextRegion(
                    source.SourceIndex,
                    source.Score,
                    polygon,
                    quadrilateral,
                    source.Orientation,
                    source.AngleRadians,
                    source.Language,
                    source.Script,
                    source.ExternalId,
                    source.Metadata);
                regions.Add(new OcrRegionResult(projectedRegion, item.Recognition, item.RecognitionWidth));
            }

            return new OcrResult(
                regions,
                projection.SourceSize,
                result.DetectionProfileId,
                result.DetectionModelId,
                result.RecognitionProfileId,
                result.RecognitionModelId,
                result.Timing);
        }

        private static TextQuadrilateral CreateQuadrilateral(IReadOnlyList<PointF> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            double area = 0;
            for (int index = 0; index < points.Count; index++)
            {
                PointF current = points[index];
                PointF next = points[(index + 1) % points.Count];
                area += ((double)current.X * next.Y) - ((double)next.X * current.Y);
            }
            TextCornerOrder order = area >= 0 ? TextCornerOrder.TopLeftClockwise : TextCornerOrder.TopLeftCounterClockwise;
            return new TextQuadrilateral(points[0], points[1], points[2], points[3], order);
        }
    }
}
