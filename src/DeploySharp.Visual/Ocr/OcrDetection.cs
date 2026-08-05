using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines treatment of restored text vertices outside the source image. / 定义恢复后的文本顶点超出源图时的处理。</summary>
    public enum TextDetectionBoundaryMode
    {
        /// <summary>Preserve exact vertices, including out-of-bounds coordinates. / 保留精确顶点，包括越界坐标。</summary>
        Preserve = 0,
        /// <summary>Reject any region with an out-of-bounds vertex. / 拒绝含越界顶点的区域。</summary>
        RejectOutsideSource = 1
    }

    /// <summary>Defines strict explicit polygon and score outputs for text detection. / 定义文本检测的严格显式多边形和分数输出。</summary>
    public sealed class ExplicitTextDetectionSchema
    {
        /// <summary>Initializes an explicit polygon schema. / 初始化显式多边形 Schema。</summary>
        public ExplicitTextDetectionSchema(string polygonsOutputName, string scoresOutputName, int pointsPerRegion, OrientedCoordinateSpace coordinateSpace = OrientedCoordinateSpace.ModelPixels, OrientedVertexOrder vertexOrder = OrientedVertexOrder.CounterClockwise, TextCornerOrder? quadrilateralCornerOrder = null, TextOrientation orientation = TextOrientation.Degrees0, TextDetectionBoundaryMode boundaryMode = TextDetectionBoundaryMode.Preserve, float epsilon = 0.000001f)
        {
            if (string.IsNullOrWhiteSpace(polygonsOutputName)) throw new ArgumentException("A polygon output name is required.", nameof(polygonsOutputName));
            if (string.IsNullOrWhiteSpace(scoresOutputName)) throw new ArgumentException("A score output name is required.", nameof(scoresOutputName));
            if (string.Equals(polygonsOutputName, scoresOutputName, StringComparison.Ordinal)) throw new ArgumentException("Text detection output names must be unique.");
            if (pointsPerRegion < 3 || pointsPerRegion > TextPolygon.MaximumVertices) throw new ArgumentOutOfRangeException(nameof(pointsPerRegion));
            if (quadrilateralCornerOrder.HasValue && pointsPerRegion != 4) throw new ArgumentException("Explicit crop corner roles require four points.", nameof(quadrilateralCornerOrder));
            if (!Enum.IsDefined(typeof(OrientedCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if (!Enum.IsDefined(typeof(OrientedVertexOrder), vertexOrder)) throw new ArgumentOutOfRangeException(nameof(vertexOrder));
            if (quadrilateralCornerOrder.HasValue && !Enum.IsDefined(typeof(TextCornerOrder), quadrilateralCornerOrder.Value)) throw new ArgumentOutOfRangeException(nameof(quadrilateralCornerOrder));
            if (!Enum.IsDefined(typeof(TextOrientation), orientation)) throw new ArgumentOutOfRangeException(nameof(orientation));
            if (!Enum.IsDefined(typeof(TextDetectionBoundaryMode), boundaryMode)) throw new ArgumentOutOfRangeException(nameof(boundaryMode));
            if (float.IsNaN(epsilon) || float.IsInfinity(epsilon) || epsilon <= 0) throw new ArgumentOutOfRangeException(nameof(epsilon));
            PolygonsOutputName = polygonsOutputName;
            ScoresOutputName = scoresOutputName;
            PointsPerRegion = pointsPerRegion;
            CoordinateSpace = coordinateSpace;
            VertexOrder = vertexOrder;
            QuadrilateralCornerOrder = quadrilateralCornerOrder;
            Orientation = orientation;
            BoundaryMode = boundaryMode;
            Epsilon = epsilon;
        }

        /// <summary>Gets polygon output name with shape [1,N,P,2]. / 获取形状为 [1,N,P,2] 的多边形输出名称。</summary>
        public string PolygonsOutputName { get; }
        /// <summary>Gets score output name with shape [1,N]. / 获取形状为 [1,N] 的分数输出名称。</summary>
        public string ScoresOutputName { get; }
        /// <summary>Gets exact points per region. / 获取每个区域的精确点数。</summary>
        public int PointsPerRegion { get; }
        /// <summary>Gets coordinate space. / 获取坐标空间。</summary>
        public OrientedCoordinateSpace CoordinateSpace { get; }
        /// <summary>Gets declared polygon vertex order. / 获取声明的多边形顶点顺序。</summary>
        public OrientedVertexOrder VertexOrder { get; }
        /// <summary>Gets optional explicit four-corner roles. / 获取可选的显式四角角色。</summary>
        public TextCornerOrder? QuadrilateralCornerOrder { get; }
        /// <summary>Gets configured right-angle orientation. / 获取配置的直角方向。</summary>
        public TextOrientation Orientation { get; }
        /// <summary>Gets source boundary behavior. / 获取源图边界行为。</summary>
        public TextDetectionBoundaryMode BoundaryMode { get; }
        /// <summary>Gets geometric epsilon. / 获取几何 epsilon。</summary>
        public float Epsilon { get; }
    }

    /// <summary>Controls bounded text detection filtering, polygon NMS, and reading order. / 控制有界文本检测筛选、多边形 NMS 和阅读顺序。</summary>
    public sealed class TextDetectionDecoderOptions
    {
        /// <summary>Initializes text detection options. / 初始化文本检测选项。</summary>
        public TextDetectionDecoderOptions(float scoreThreshold = 0.5f, float polygonIouThreshold = 0.3f, bool applyPolygonNms = true, TextReadingOrder readingOrder = TextReadingOrder.TopToBottomThenLeftToRight, float rowToleranceRatio = 0.5f, int maximumCandidates = 1024, int maximumRegions = 128, long maximumWorkspaceBytes = 64L * 1024L * 1024L)
        {
            if (float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold) || scoreThreshold < 0 || scoreThreshold > 1) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (float.IsNaN(polygonIouThreshold) || float.IsInfinity(polygonIouThreshold) || polygonIouThreshold < 0 || polygonIouThreshold > 1) throw new ArgumentOutOfRangeException(nameof(polygonIouThreshold));
            if (!Enum.IsDefined(typeof(TextReadingOrder), readingOrder)) throw new ArgumentOutOfRangeException(nameof(readingOrder));
            if (float.IsNaN(rowToleranceRatio) || float.IsInfinity(rowToleranceRatio) || rowToleranceRatio < 0 || rowToleranceRatio > 2) throw new ArgumentOutOfRangeException(nameof(rowToleranceRatio));
            if (maximumCandidates <= 0 || maximumRegions <= 0 || maximumRegions > maximumCandidates) throw new ArgumentOutOfRangeException(nameof(maximumCandidates));
            if (maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkspaceBytes));
            ScoreThreshold = scoreThreshold;
            PolygonIouThreshold = polygonIouThreshold;
            ApplyPolygonNms = applyPolygonNms;
            ReadingOrder = readingOrder;
            RowToleranceRatio = rowToleranceRatio;
            MaximumCandidates = maximumCandidates;
            MaximumRegions = maximumRegions;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
        }

        /// <summary>Gets inclusive score threshold. / 获取包含边界的分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets polygon IoU suppression threshold. / 获取多边形 IoU 抑制阈值。</summary>
        public float PolygonIouThreshold { get; }
        /// <summary>Gets whether exact polygon NMS is applied. / 获取是否应用精确多边形 NMS。</summary>
        public bool ApplyPolygonNms { get; }
        /// <summary>Gets reading order. / 获取阅读顺序。</summary>
        public TextReadingOrder ReadingOrder { get; }
        /// <summary>Gets row grouping tolerance relative to the smaller region height. / 获取相对于较小区域高度的行分组容差。</summary>
        public float RowToleranceRatio { get; }
        /// <summary>Gets maximum input candidates. / 获取最大输入候选数。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets maximum retained regions. / 获取最大保留区域数。</summary>
        public int MaximumRegions { get; }
        /// <summary>Gets maximum numeric conversion workspace. / 获取最大数值转换工作区。</summary>
        public long MaximumWorkspaceBytes { get; }
    }

    /// <summary>Decodes strict named explicit text polygons into source-space regions. / 将严格命名的显式文本多边形解码为源图区域。</summary>
    public sealed class ExplicitTextDetectionDecoder : IVisualDecoder
    {
        /// <summary>Initializes a text detection decoder. / 初始化文本检测解码器。</summary>
        public ExplicitTextDetectionDecoder(ExplicitTextDetectionSchema schema, TextDetectionDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? new TextDetectionDecoderOptions();
        }

        /// <summary>Gets text-detection task. / 获取文本检测任务。</summary>
        public VisualTaskId Task => VisualTaskId.TextDetection;
        /// <summary>Gets strict output schema. / 获取严格输出 Schema。</summary>
        public ExplicitTextDetectionSchema Schema { get; }
        /// <summary>Gets bounded decoder options. / 获取有界解码选项。</summary>
        public TextDetectionDecoderOptions Options { get; }

        /// <summary>Decodes [1,N,P,2] polygons and [1,N] scores with exact polygon NMS and deterministic reading order. / 使用精确多边形 NMS 和确定阅读顺序解码 [1,N,P,2] 多边形及 [1,N] 分数。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, VisualErrorCodes.DecodeFailed, "Text detection requires batch size one.");
            if (context.Outputs.Count != 2) throw Failure(context, VisualErrorCodes.TensorInvalid, "Explicit text detection requires exactly two outputs.");
            ITensor polygonsTensor = Required(context, Schema.PolygonsOutputName);
            ITensor scoresTensor = Required(context, Schema.ScoresOutputName);
            int candidates = ValidateShapes(polygonsTensor, scoresTensor, context);
            if (candidates > Options.MaximumCandidates) throw Failure(context, VisualErrorCodes.DecodeFailed, "Text candidate count exceeds its configured bound.", Schema.PolygonsOutputName, "candidates=" + candidates);
            long workspace = 0;
            if (polygonsTensor.ElementType == TensorElementType.Float64) workspace = checked(workspace + polygonsTensor.Length * sizeof(float));
            if (scoresTensor.ElementType == TensorElementType.Float64) workspace = checked(workspace + scoresTensor.Length * sizeof(float));
            if (workspace > Options.MaximumWorkspaceBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "Text decoder workspace exceeds its configured bound.", details: "workspaceBytes=" + workspace);
            float[] polygonValues = VisualTensorReader.ReadFiniteScores(polygonsTensor, context.Profile.ProfileId, Schema.PolygonsOutputName);
            float[] scoreValues = VisualTensorReader.ReadFiniteScores(scoresTensor, context.Profile.ProfileId, Schema.ScoresOutputName);
            var candidatesList = new List<TextRegion>(Math.Min(candidates, Options.MaximumRegions));
            for (int candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                float score = scoreValues[candidateIndex];
                if (score < 0 || score > 1) throw Failure(context, VisualErrorCodes.DecodeFailed, "Text score must be in [0,1].", Schema.ScoresOutputName, "candidate=" + candidateIndex);
                if (score < Options.ScoreThreshold) continue;
                PointF[] sourcePoints = DecodePoints(polygonValues, candidateIndex, context);
                TextPolygon polygon;
                try { polygon = TextPolygon.Canonicalize(sourcePoints, Schema.VertexOrder, Schema.Epsilon); }
                catch (ArgumentException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Text polygon violates the declared convex polygon contract.", Schema.PolygonsOutputName, "candidate=" + candidateIndex, exception); }
                TextQuadrilateral? quadrilateral = CreateQuadrilateral(sourcePoints, context, candidateIndex);
                candidatesList.Add(new TextRegion(candidateIndex, score, polygon, quadrilateral, Schema.Orientation));
            }

            candidatesList.Sort(ScoreOrder);
            List<TextRegion> kept = Options.ApplyPolygonNms ? ApplyNms(candidatesList, context) : TakeBounded(candidatesList);
            List<TextRegion> ordered = OrderForReading(kept, context.CancellationToken);
            return new TextDetectionResult(ordered, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private PointF[] DecodePoints(float[] values, int candidateIndex, VisualDecodeContext context)
        {
            var result = new PointF[Schema.PointsPerRegion];
            int offset = checked(candidateIndex * Schema.PointsPerRegion * 2);
            for (int pointIndex = 0; pointIndex < result.Length; pointIndex++)
            {
                float x = values[offset + (pointIndex * 2)];
                float y = values[offset + (pointIndex * 2) + 1];
                if (Schema.CoordinateSpace == OrientedCoordinateSpace.Normalized) { x *= context.Input.ModelSize.Width; y *= context.Input.ModelSize.Height; }
                PointF source = context.Input.Transform.ToSource(new PointF(x, y));
                if (Schema.BoundaryMode == TextDetectionBoundaryMode.RejectOutsideSource && (source.X < 0 || source.X > context.Input.SourceSize.Width || source.Y < 0 || source.Y > context.Input.SourceSize.Height)) throw Failure(context, VisualErrorCodes.DecodeFailed, "A restored text vertex is outside the source image.", Schema.PolygonsOutputName, "candidate=" + candidateIndex + ";point=" + pointIndex);
                result[pointIndex] = source;
            }
            return result;
        }

        private TextQuadrilateral? CreateQuadrilateral(PointF[] points, VisualDecodeContext context, int candidateIndex)
        {
            if (!Schema.QuadrilateralCornerOrder.HasValue) return null;
            try { return new TextQuadrilateral(points[0], points[1], points[2], points[3], Schema.QuadrilateralCornerOrder.Value, Schema.Epsilon); }
            catch (ArgumentException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Text quadrilateral corner roles are inconsistent with the declared order.", Schema.PolygonsOutputName, "candidate=" + candidateIndex, exception); }
        }

        private List<TextRegion> ApplyNms(List<TextRegion> ordered, VisualDecodeContext context)
        {
            var kept = new List<TextRegion>(Math.Min(ordered.Count, Options.MaximumRegions));
            for (int index = 0; index < ordered.Count && kept.Count < Options.MaximumRegions; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                bool suppressed = false;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    if ((keptIndex & 15) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                    if (OcrGeometry.IntersectionOverUnion(kept[keptIndex].Polygon, ordered[index].Polygon, Schema.Epsilon, context.CancellationToken) > Options.PolygonIouThreshold) { suppressed = true; break; }
                }
                if (!suppressed) kept.Add(ordered[index]);
            }
            return kept;
        }

        private List<TextRegion> TakeBounded(List<TextRegion> ordered)
        {
            int count = Math.Min(ordered.Count, Options.MaximumRegions);
            var result = new List<TextRegion>(count);
            for (int index = 0; index < count; index++) result.Add(ordered[index]);
            return result;
        }

        private List<TextRegion> OrderForReading(List<TextRegion> regions, System.Threading.CancellationToken cancellationToken)
        {
            regions.Sort((left, right) =>
            {
                RectangleF a = left.AxisAlignedBounds;
                RectangleF b = right.AxisAlignedBounds;
                int primary = Options.ReadingOrder == TextReadingOrder.LeftToRightThenTopToBottom ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
                if (primary != 0) return primary;
                int secondary = Options.ReadingOrder == TextReadingOrder.LeftToRightThenTopToBottom ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
                return secondary != 0 ? secondary : left.SourceIndex.CompareTo(right.SourceIndex);
            });
            if (Options.ReadingOrder == TextReadingOrder.LeftToRightThenTopToBottom) return regions;

            var result = new List<TextRegion>(regions.Count);
            int rowStart = 0;
            while (rowStart < regions.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int rowEnd = rowStart + 1;
                RectangleF anchor = regions[rowStart].AxisAlignedBounds;
                float anchorCenter = anchor.Y + (anchor.Height * 0.5f);
                while (rowEnd < regions.Count)
                {
                    RectangleF candidate = regions[rowEnd].AxisAlignedBounds;
                    float tolerance = Math.Min(anchor.Height, candidate.Height) * Options.RowToleranceRatio;
                    float center = candidate.Y + (candidate.Height * 0.5f);
                    if (Math.Abs(center - anchorCenter) > tolerance) break;
                    rowEnd++;
                }
                regions.Sort(rowStart, rowEnd - rowStart, Comparer<TextRegion>.Create((left, right) =>
                {
                    int x = left.AxisAlignedBounds.X.CompareTo(right.AxisAlignedBounds.X);
                    return x != 0 ? x : left.SourceIndex.CompareTo(right.SourceIndex);
                }));
                for (int index = rowStart; index < rowEnd; index++) result.Add(regions[index]);
                rowStart = rowEnd;
            }
            return result;
        }

        private int ValidateShapes(ITensor polygons, ITensor scores, VisualDecodeContext context)
        {
            TensorShape polygonShape = polygons.Shape;
            if (polygonShape.Rank != 4 || polygonShape[0] != 1 || polygonShape[2] != Schema.PointsPerRegion || polygonShape[3] != 2 || polygons.Length != checked((long)polygonShape[1] * Schema.PointsPerRegion * 2)) throw Failure(context, VisualErrorCodes.TensorInvalid, "Text polygons must have shape [1,N,P,2].", Schema.PolygonsOutputName, polygonShape.ToString());
            int candidates = checked((int)polygonShape[1]);
            TensorShape scoreShape = scores.Shape;
            if (scoreShape.Rank != 2 || scoreShape[0] != 1 || scoreShape[1] != candidates || scores.Length != candidates) throw Failure(context, VisualErrorCodes.TensorInvalid, "Text scores must have shape [1,N].", Schema.ScoresOutputName, scoreShape.ToString());
            return candidates;
        }

        private static int ScoreOrder(TextRegion left, TextRegion right) { int score = right.Score.CompareTo(left.Score); return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex); }
        private static ITensor Required(VisualDecodeContext context, string name) { try { return context.Outputs.GetRequired(name); } catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "A required text output is missing.", name, null, exception); } }
        private static VisualException Failure(VisualDecodeContext context, string code, string message, string? tensorName = null, string? details = null, Exception? exception = null) => new VisualException(code, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
    }
}
