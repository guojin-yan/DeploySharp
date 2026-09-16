using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Projects a model-space anomaly map and thresholded mask into the full source image. / 将模型空间异常图和阈值掩码投影到完整源图。</summary>
    public sealed class ModelSpaceAnomalyRoiProjector : IRoiResultProjector<AnomalyDetectionResult>
    {
        private readonly long _maximumOutputPixels;

        /// <summary>Initializes a bounded anomaly projector. / 初始化有界异常投影器。</summary>
        public ModelSpaceAnomalyRoiProjector(long maximumOutputPixels = 64L * 1024L * 1024L)
        {
            if (maximumOutputPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputPixels));
            _maximumOutputPixels = maximumOutputPixels;
        }

        /// <inheritdoc />
        public AnomalyDetectionResult Project(AnomalyDetectionResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (result.NormalizedMap.Width != projection.ModelSize.Width || result.NormalizedMap.Height != projection.ModelSize.Height || result.Mask.Width != projection.ModelSize.Width || result.Mask.Height != projection.ModelSize.Height)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space anomaly dimensions must match the ROI projection model dimensions.");
            }
            long outputPixels = checked((long)projection.SourceSize.Width * projection.SourceSize.Height);
            if (outputPixels > _maximumOutputPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "Projected anomaly output exceeds the configured dense-pixel budget.", technicalDetails: "pixels=" + outputPixels + ";limit=" + _maximumOutputPixels);

            int width = projection.SourceSize.Width;
            int height = projection.SourceSize.Height;
            float[] normalizedValues = ProjectValues(result.NormalizedMap.DangerousGetReadOnlyBuffer(), result.NormalizedMap.Width, projection, width, height);
            byte[] maskValues = new byte[checked(width * height)];
            int anomalous = 0;
            for (int index = 0; index < normalizedValues.Length; index++) if (normalizedValues[index] >= result.Threshold) { maskValues[index] = 1; anomalous++; }
            AnomalyScoreMap normalized = new AnomalyScoreMap(projection.SourceSize, width, height, normalizedValues, result.NormalizedMap.ValueMode, result.NormalizedMap.Normalization, true);
            AnomalyScoreMap? raw = null;
            if (result.RawMap != null && result.RawMap.Width == projection.ModelSize.Width && result.RawMap.Height == projection.ModelSize.Height)
            {
                float[] rawValues = ProjectValues(result.RawMap.DangerousGetReadOnlyBuffer(), result.RawMap.Width, projection, width, height);
                raw = new AnomalyScoreMap(projection.SourceSize, width, height, rawValues, result.RawMap.ValueMode, result.RawMap.Normalization, true);
            }
            var mask = new AnomalyBinaryMask(width, height, maskValues, true);
            return new AnomalyDetectionResult(result.ImageScore, raw, normalized, mask, result.Threshold, ImageTransform.Resize(projection.SourceSize, projection.SourceSize), anomalous, result.Timing, result.Warnings);
        }

        private static float[] ProjectValues(float[] modelValues, int modelWidth, RoiProjection projection, int sourceWidth, int sourceHeight)
        {
            var projected = new float[checked(sourceWidth * sourceHeight)];
            PixelBounds bounds = PixelBounds.From(projection.SourceBounds, sourceWidth, sourceHeight);
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int destinationRow = y * sourceWidth;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (!projection.OwnsSourcePixel(x, y)) continue;
                    PointF model = projection.ToModel(new PointF(x + .5f, y + .5f));
                    int modelX = (int)Math.Floor(model.X);
                    int modelY = (int)Math.Floor(model.Y);
                    if (modelX >= 0 && modelX < modelWidth && modelY >= 0 && modelY < projection.ModelSize.Height) projected[destinationRow + x] = modelValues[(modelY * modelWidth) + modelX];
                }
            }
            return projected;
        }
    }

    /// <summary>Projects a model-space RMBG Alpha plane into the full source image using nearest-neighbor sampling. / 使用最近邻采样将模型空间 RMBG Alpha 平面投影到完整源图。</summary>
    public sealed class ModelSpaceAlphaRoiProjector : IRoiResultProjector<BackgroundRemovalResult>
    {
        private readonly long _maximumOutputPixels;

        /// <summary>Initializes a bounded Alpha projector. / 初始化有界 Alpha 投影器。</summary>
        public ModelSpaceAlphaRoiProjector(long maximumOutputPixels = 64L * 1024L * 1024L)
        {
            if (maximumOutputPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputPixels));
            _maximumOutputPixels = maximumOutputPixels;
        }

        /// <inheritdoc />
        public BackgroundRemovalResult Project(BackgroundRemovalResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (result.SourceSize != projection.ModelSize || result.Alpha.Width != projection.ModelSize.Width || result.Alpha.Height != projection.ModelSize.Height)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space Alpha dimensions must match the ROI projection model dimensions.");
            }
            long outputPixels = checked((long)projection.SourceSize.Width * projection.SourceSize.Height);
            if (outputPixels > _maximumOutputPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "Projected Alpha exceeds the configured dense-pixel budget.", technicalDetails: "pixels=" + outputPixels + ";limit=" + _maximumOutputPixels);

            int width = projection.SourceSize.Width;
            int height = projection.SourceSize.Height;
            var values = new float[checked(width * height)];
            float[] source = result.Alpha.DangerousGetReadOnlyBuffer();
            PixelBounds bounds = PixelBounds.From(projection.SourceBounds, width, height);
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int row = y * width;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (!projection.OwnsSourcePixel(x, y)) continue;
                    PointF model = projection.ToModel(new PointF(x + .5f, y + .5f));
                    int modelX = (int)Math.Floor(model.X);
                    int modelY = (int)Math.Floor(model.Y);
                    if (modelX >= 0 && modelX < result.Alpha.Width && modelY >= 0 && modelY < result.Alpha.Height) values[row + x] = source[(modelY * result.Alpha.Width) + modelX];
                }
            }
            return new BackgroundRemovalResult(new AlphaMask(width, height, values, true), projection.SourceSize, ImageTransform.Resize(projection.SourceSize, projection.SourceSize), result.ProfileId, result.ModelId);
        }
    }

    /// <summary>Projects model-space instance masks into full source-image masks using nearest-neighbor label sampling. / 使用最近邻标签采样将模型空间实例掩码投影为完整源图掩码。</summary>
    public sealed class ModelSpaceInstanceSegmentationRoiProjector : IRoiResultProjector<InstanceSegmentationResult>
    {
        private readonly long _maximumTotalMaskPixels;

        /// <summary>Initializes a bounded dense-mask projector. / 初始化有界稠密掩码投影器。</summary>
        public ModelSpaceInstanceSegmentationRoiProjector(long maximumTotalMaskPixels = 256L * 1024L * 1024L)
        {
            if (maximumTotalMaskPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTotalMaskPixels));
            _maximumTotalMaskPixels = maximumTotalMaskPixels;
        }

        /// <inheritdoc />
        public InstanceSegmentationResult Project(InstanceSegmentationResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (result.SourceSize != projection.ModelSize) throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space instance segmentation dimensions must match the ROI projection model dimensions.");
            long outputPixels = checked((long)projection.SourceSize.Width * projection.SourceSize.Height * result.Instances.Count);
            if (outputPixels > _maximumTotalMaskPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "Projected instance masks exceed the configured dense-pixel budget.", technicalDetails: "pixels=" + outputPixels + ";limit=" + _maximumTotalMaskPixels);

            var instances = new List<InstanceSegmentationInstance>(result.Instances.Count);
            foreach (InstanceSegmentationInstance instance in result.Instances)
            {
                InstanceBinaryMask mask = ProjectMask(instance.Mask, projection);
                RectangleF? bounds = mask.GetForegroundBounds();
                if (!bounds.HasValue) continue;
                instances.Add(new InstanceSegmentationInstance(instance.SourceIndex, instance.ClassIndex, instance.Label, instance.Score, bounds.Value, mask, null, instance.ExternalId, instance.Metadata));
            }

            return new InstanceSegmentationResult(instances, projection.SourceSize, result.ProfileId, result.ModelId);
        }

        private static InstanceBinaryMask ProjectMask(InstanceBinaryMask mask, RoiProjection projection)
        {
            if (mask.Width != projection.ModelSize.Width || mask.Height != projection.ModelSize.Height || mask.OriginX != 0 || mask.OriginY != 0)
            {
                throw new VisualException(VisualErrorCodes.InputInvalid, "A model-space instance mask must occupy the complete model canvas with a zero origin.");
            }

            int sourceWidth = projection.SourceSize.Width;
            int sourceHeight = projection.SourceSize.Height;
            var projected = new byte[checked(sourceWidth * sourceHeight)];
            byte[] source = mask.GetPixelsUnsafe();
            int sourceOffset = mask.PixelOffset;
            int foreground = 0;
            PixelBounds bounds = PixelBounds.From(projection.SourceBounds, sourceWidth, sourceHeight);
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int destinationRow = y * sourceWidth;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (!projection.OwnsSourcePixel(x, y)) continue;
                    PointF model = projection.ToModel(new PointF(x + .5f, y + .5f));
                    int modelX = (int)Math.Floor(model.X);
                    int modelY = (int)Math.Floor(model.Y);
                    if (modelX < 0 || modelX >= mask.Width || modelY < 0 || modelY >= mask.Height || source[sourceOffset + (modelY * mask.Width) + modelX] == 0) continue;
                    projected[destinationRow + x] = 1;
                    foreground++;
                }
            }

            return new InstanceBinaryMask(sourceWidth, sourceHeight, projected, InstanceMaskCoordinateSpace.SourceImage, 0, 0, foreground);
        }
    }

    /// <summary>Projects a model-space semantic label mask into the full source image. / 将模型空间语义标签掩码投影到完整源图。</summary>
    public sealed class ModelSpaceSemanticSegmentationRoiProjector : IRoiResultProjector<SemanticSegmentationResult>
    {
        private readonly ushort _backgroundClassIndex;
        private readonly long _maximumOutputPixels;

        /// <summary>Initializes a nearest-neighbor semantic label projector. / 初始化最近邻语义标签投影器。</summary>
        public ModelSpaceSemanticSegmentationRoiProjector(int backgroundClassIndex, long maximumOutputPixels = 64L * 1024L * 1024L)
        {
            if (backgroundClassIndex < 0 || backgroundClassIndex > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(backgroundClassIndex));
            if (maximumOutputPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputPixels));
            _backgroundClassIndex = (ushort)backgroundClassIndex;
            _maximumOutputPixels = maximumOutputPixels;
        }

        /// <inheritdoc />
        public SemanticSegmentationResult Project(SemanticSegmentationResult result, RoiProjection projection)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (result.Mask.Width != projection.ModelSize.Width || result.Mask.Height != projection.ModelSize.Height) throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space semantic mask dimensions must match the ROI projection model dimensions.");
            if (!result.Classes.Any(value => value.Index == _backgroundClassIndex)) throw new VisualException(VisualErrorCodes.InputInvalid, "The configured semantic background class is absent from the result palette.");
            long outputPixelCount = checked((long)projection.SourceSize.Width * projection.SourceSize.Height);
            if (outputPixelCount > _maximumOutputPixels) throw new VisualException(VisualErrorCodes.InputInvalid, "Projected semantic mask exceeds the configured dense-pixel budget.", technicalDetails: "pixels=" + outputPixelCount + ";limit=" + _maximumOutputPixels);

            int sourceWidth = projection.SourceSize.Width;
            int sourceHeight = projection.SourceSize.Height;
            var projected = Enumerable.Repeat(_backgroundClassIndex, checked(sourceWidth * sourceHeight)).ToArray();
            ushort[] modelLabels = result.Mask.DangerousGetReadOnlyBuffer();
            PixelBounds bounds = PixelBounds.From(projection.SourceBounds, sourceWidth, sourceHeight);
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int destinationRow = y * sourceWidth;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (!projection.OwnsSourcePixel(x, y)) continue;
                    PointF model = projection.ToModel(new PointF(x + .5f, y + .5f));
                    int modelX = (int)Math.Floor(model.X);
                    int modelY = (int)Math.Floor(model.Y);
                    if (modelX < 0 || modelX >= projection.ModelSize.Width || modelY < 0 || modelY >= projection.ModelSize.Height) continue;
                    projected[destinationRow + x] = modelLabels[(modelY * projection.ModelSize.Width) + modelX];
                }
            }

            var counts = new SortedDictionary<int, long>();
            for (int index = 0; index < projected.Length; index++) counts[projected[index]] = counts.TryGetValue(projected[index], out long current) ? current + 1 : 1;
            var statistics = counts.Select(value => new SegmentationClassStatistics(value.Key, value.Value, (double)value.Value / projected.Length));
            SegmentationProbabilityMap? probabilityMap = ProjectProbabilityMap(result.ProbabilityMap, projection, bounds, sourceWidth, sourceHeight);
            return new SemanticSegmentationResult(new SemanticSegmentationMask(sourceWidth, sourceHeight, projected, true), result.Classes, statistics, probabilityMap: probabilityMap);
        }

        private static SegmentationProbabilityMap? ProjectProbabilityMap(SegmentationProbabilityMap? map, RoiProjection projection, PixelBounds bounds, int sourceWidth, int sourceHeight)
        {
            if (map == null) return null;
            if (map.Width != projection.ModelSize.Width || map.Height != projection.ModelSize.Height) throw new VisualException(VisualErrorCodes.InputInvalid, "Model-space semantic probability-map dimensions must match the ROI projection model dimensions.");
            int classCount = map.ClassCount;
            var projected = new float[checked(sourceWidth * sourceHeight * classCount)];
            float[] source = map.DangerousGetReadOnlyBuffer();
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int destinationRow = y * sourceWidth;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (!projection.OwnsSourcePixel(x, y)) continue;
                    PointF model = projection.ToModel(new PointF(x + .5f, y + .5f));
                    int modelX = (int)Math.Floor(model.X);
                    int modelY = (int)Math.Floor(model.Y);
                    if (modelX < 0 || modelX >= map.Width || modelY < 0 || modelY >= map.Height) continue;
                    int sourceOffset = ((modelY * map.Width) + modelX) * classCount;
                    int destinationOffset = (destinationRow + x) * classCount;
                    Array.Copy(source, sourceOffset, projected, destinationOffset, classCount);
                }
            }
            return new SegmentationProbabilityMap(sourceWidth, sourceHeight, classCount, projected, true);
        }
    }

    internal readonly struct PixelBounds
    {
        private PixelBounds(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        internal int Left { get; }
        internal int Top { get; }
        internal int Right { get; }
        internal int Bottom { get; }

        internal static PixelBounds From(RectangleF rectangle, int width, int height)
        {
            int left = Math.Max(0, Math.Min(width, (int)Math.Floor(rectangle.X)));
            int top = Math.Max(0, Math.Min(height, (int)Math.Floor(rectangle.Y)));
            int right = Math.Max(left, Math.Min(width, (int)Math.Ceiling(rectangle.Right)));
            int bottom = Math.Max(top, Math.Min(height, (int)Math.Ceiling(rectangle.Bottom)));
            return new PixelBounds(left, top, right, bottom);
        }
    }
}
