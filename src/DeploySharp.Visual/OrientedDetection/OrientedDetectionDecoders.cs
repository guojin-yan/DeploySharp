using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Decodes named center-size-angle OBB outputs. / 解码命名的中心宽高角 OBB 输出。</summary>
    public sealed class DirectOrientedDetectionDecoder : IVisualDecoder
    {
        /// <summary>Initializes a direct OBB decoder. / 初始化直接 OBB 解码器。</summary>
        public DirectOrientedDetectionDecoder(CenterSizeAngleOutputSchema schema, OrientedDetectionDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? new OrientedDetectionDecoderOptions();
        }

        /// <summary>Gets the oriented-object-detection task identifier. / 获取旋转目标检测任务标识符。</summary>
        public VisualTaskId Task => VisualTaskId.OrientedObjectDetection;
        /// <summary>Gets the strict center-size-angle schema. / 获取严格的中心宽高角 Schema。</summary>
        public CenterSizeAngleOutputSchema Schema { get; }
        /// <summary>Gets bounded decoder options. / 获取有界解码选项。</summary>
        public OrientedDetectionDecoderOptions Options { get; }

        /// <summary>Decodes strict named center-size-angle outputs into owned source-space detections. / 将严格命名的中心宽高角输出解码为自有源图空间检测结果。</summary>
        /// <remarks>Only named [1,N,5], [1,N], and [1,N] outputs are accepted. / 只接受命名的 [1,N,5]、[1,N] 和 [1,N] 输出。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB decoding currently requires batch size one.", Schema.BoxesOutputName);
            if (context.Outputs.Count != 3) throw Failure(context, VisualErrorCodes.TensorInvalid, "Center-size-angle OBB requires exactly three declared outputs.", Schema.BoxesOutputName);
            ITensor boxesTensor = Required(context, Schema.BoxesOutputName);
            int candidates = ValidateBoxShape(boxesTensor, 5, context, Schema.BoxesOutputName);
            if (candidates > Options.MaximumCandidates) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB candidate count exceeds its configured bound.", Schema.BoxesOutputName, "candidates=" + candidates);
            ITensor scoresTensor = Required(context, Schema.ScoresOutputName);
            ITensor classesTensor = Required(context, Schema.ClassesOutputName);
            ValidateVector(scoresTensor, candidates, context, Schema.ScoresOutputName);
            ValidateVector(classesTensor, candidates, context, Schema.ClassesOutputName);
            EnsureWorkspace(context, Options, boxesTensor, scoresTensor, classesTensor);
            float[] boxes = VisualTensorReader.ReadFiniteScores(boxesTensor, context.Profile.ProfileId, Schema.BoxesOutputName);
            float[] scores = VisualTensorReader.ReadFiniteScores(scoresTensor, context.Profile.ProfileId, Schema.ScoresOutputName);
            float[] classes = VisualTensorReader.ReadFiniteScores(classesTensor, context.Profile.ProfileId, Schema.ClassesOutputName);
            var decoded = new List<OrientedCandidate>(candidates);
            for (int candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                float score = ValidateScore(scores[candidateIndex], context, candidateIndex);
                if (score < Options.ScoreThreshold) continue;
                int classIndex = ValidateClass(classes[candidateIndex], context, candidateIndex);
                int offset = checked(candidateIndex * 5);
                float centerX = boxes[offset + Schema.BoxOrder.CenterXIndex];
                float centerY = boxes[offset + Schema.BoxOrder.CenterYIndex];
                float width = boxes[offset + Schema.BoxOrder.WidthIndex];
                float height = boxes[offset + Schema.BoxOrder.HeightIndex];
                float angle = boxes[offset + Schema.BoxOrder.AngleIndex];
                if (width <= 0 || height <= 0) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB width and height must be positive.", Schema.BoxesOutputName, "candidate=" + candidateIndex);
                OrientedQuadrilateral modelQuadrilateral;
                try { modelQuadrilateral = OrientedGeometry.CreateCenterSizeAngleCorners(centerX, centerY, width, height, angle, Schema, context.Input.ModelSize); }
                catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
                { throw Failure(context, VisualErrorCodes.DecodeFailed, "Center-size-angle OBB values violate the declared schema.", Schema.BoxesOutputName, "candidate=" + candidateIndex, exception); }
                OrientedQuadrilateral sourceQuadrilateral = Restore(modelQuadrilateral, context);
                decoded.Add(new OrientedCandidate(candidateIndex, classIndex, score, sourceQuadrilateral, GetSourceAngle(angle, width, height, context)));
            }

            decoded.Sort(CompareCandidates);
            List<OrientedCandidate> kept = Suppress(decoded, context);
            var results = new List<OrientedDetection>(kept.Count);
            for (int index = 0; index < kept.Count; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                OrientedCandidate candidate = kept[index];
                results.Add(new OrientedDetection(candidate.SourceIndex, candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score, candidate.Quadrilateral, candidate.AngleRadiansCounterClockwise, IsUniformTransform(context.Input.Transform)));
            }

            return new OrientedDetectionResult(results, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private OrientedQuadrilateral Restore(OrientedQuadrilateral model, VisualDecodeContext context)
        {
            var points = new PointF[4];
            for (int index = 0; index < 4; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                PointF modelPoint = model.Vertices[index];
                points[index] = context.Input.Transform.ToSource(modelPoint);
                if (Schema.BoundaryMode == OrientedDetectionBoundaryMode.RejectOutsideSource && (points[index].X < 0 || points[index].X > context.Input.SourceSize.Width || points[index].Y < 0 || points[index].Y > context.Input.SourceSize.Height)) throw Failure(context, VisualErrorCodes.DecodeFailed, "A restored OBB vertex is outside the source image.", Schema.BoxesOutputName);
            }

            try { return OrientedQuadrilateral.Canonicalize(points, OrientedVertexOrder.CounterClockwise, OrientedStartVertexRule.MinimumYThenX, Schema.Epsilon); }
            catch (ArgumentException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Restored OBB vertices are not a strict convex quadrilateral.", Schema.BoxesOutputName, null, exception); }
        }

        private float GetSourceAngle(float inputAngle, float width, float height, VisualDecodeContext context)
        {
            float normalized = OrientedGeometry.NormalizeCenterMathAngle(inputAngle, width, height, Schema, context.Input.ModelSize);
            return IsUniformTransform(context.Input.Transform) ? normalized : float.NaN;
        }

        private List<OrientedCandidate> Suppress(List<OrientedCandidate> ordered, VisualDecodeContext context)
        {
            var kept = new List<OrientedCandidate>(Math.Min(ordered.Count, Options.MaximumDetections));
            for (int candidateIndex = 0; candidateIndex < ordered.Count && kept.Count < Options.MaximumDetections; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                OrientedCandidate candidate = ordered[candidateIndex];
                bool suppressed = false;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    if ((keptIndex & 15) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                    OrientedCandidate existing = kept[keptIndex];
                    if (Options.NmsMode == DetectionNmsMode.ClassAware && existing.ClassIndex != candidate.ClassIndex) continue;
                    if (OrientedGeometry.IntersectionOverUnion(existing.Quadrilateral, candidate.Quadrilateral, Schema.Epsilon, context.CancellationToken) > Options.IouThreshold) { suppressed = true; break; }
                }

                if (!suppressed) kept.Add(candidate);
            }

            return kept;
        }

        private static bool IsUniformTransform(ImageTransform transform) => Math.Abs(transform.ScaleX - transform.ScaleY) <= 0.000001f;
        private static int CompareCandidates(OrientedCandidate left, OrientedCandidate right) { int score = right.Score.CompareTo(left.Score); return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex); }
        private static int ValidateBoxShape(ITensor tensor, int fields, VisualDecodeContext context, string name)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 3 || shape[0] != 1 || shape[2] != fields || tensor.Length != (long)shape[1] * fields) throw Failure(context, VisualErrorCodes.TensorInvalid, "OBB boxes must have shape [1,N,5].", name, shape.ToString());
            try { return checked((int)shape[1]); } catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "OBB candidate count exceeds Int32 bounds.", name, shape.ToString(), exception); }
        }
        private static void ValidateVector(ITensor tensor, int candidates, VisualDecodeContext context, string name)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 2 || shape[0] != 1 || shape[1] != candidates || tensor.Length != candidates) throw Failure(context, VisualErrorCodes.TensorInvalid, "OBB scores and classes must have shape [1,N].", name, shape.ToString());
        }
        private static float ValidateScore(float score, VisualDecodeContext context, int index) { if (score < 0) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB score must be non-negative.", "scores", "candidate=" + index); return score; }
        private static int ValidateClass(float value, VisualDecodeContext context, int index) { if (value < 0 || value > int.MaxValue || value != (float)Math.Floor(value)) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB class values must be non-negative integers.", "classes", "candidate=" + index); return checked((int)value); }
        private static void EnsureWorkspace(VisualDecodeContext context, OrientedDetectionDecoderOptions options, params ITensor[] tensors)
        {
            long bytes = 0;
            try { for (int index = 0; index < tensors.Length; index++) if (tensors[index].ElementType == TensorElementType.Float64) bytes = checked(bytes + checked(tensors[index].Length * sizeof(float))); }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB conversion workspace size overflowed.", details: exception.ToString(), exception: exception); }
            if (bytes > options.MaximumWorkspaceBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB conversion workspace exceeds its configured bound.", details: "workspaceBytes=" + bytes);
        }
        private static ITensor Required(VisualDecodeContext context, string name) { try { return context.Outputs.GetRequired(name); } catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "A required OBB output is missing.", name, null, exception); } }
        private static VisualException Failure(VisualDecodeContext context, string code, string message, string? tensorName = null, string? details = null, Exception? exception = null) => new VisualException(code, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
        private sealed class OrientedCandidate
        {
            public OrientedCandidate(int sourceIndex, int classIndex, float score, OrientedQuadrilateral quadrilateral, float angleRadiansCounterClockwise) { SourceIndex = sourceIndex; ClassIndex = classIndex; Score = score; Quadrilateral = quadrilateral; AngleRadiansCounterClockwise = float.IsNaN(angleRadiansCounterClockwise) ? (float?)null : angleRadiansCounterClockwise; }
            public int SourceIndex { get; }
            public int ClassIndex { get; }
            public float Score { get; }
            public OrientedQuadrilateral Quadrilateral { get; }
            public float? AngleRadiansCounterClockwise { get; }
        }
    }

    /// <summary>Decodes named four-corner OBB outputs. / 解码命名的四角点 OBB 输出。</summary>
    public sealed class FourCornerOrientedDetectionDecoder : IVisualDecoder
    {
        /// <summary>Initializes a four-corner OBB decoder. / 初始化四角点 OBB 解码器。</summary>
        public FourCornerOrientedDetectionDecoder(FourCornerOutputSchema schema, OrientedDetectionDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? new OrientedDetectionDecoderOptions();
        }
        /// <summary>Gets the oriented-object-detection task identifier. / 获取旋转目标检测任务标识符。</summary>
        public VisualTaskId Task => VisualTaskId.OrientedObjectDetection;
        /// <summary>Gets the strict four-corner schema. / 获取严格的四角点 Schema。</summary>
        public FourCornerOutputSchema Schema { get; }
        /// <summary>Gets bounded decoder options. / 获取有界解码选项。</summary>
        public OrientedDetectionDecoderOptions Options { get; }

        /// <summary>Decodes strict named four-corner outputs into owned source-space detections. / 将严格命名的四角点输出解码为自有源图空间检测结果。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB decoding currently requires batch size one.", Schema.CornersOutputName);
            if (context.Outputs.Count != 3) throw Failure(context, VisualErrorCodes.TensorInvalid, "Four-corner OBB requires exactly three declared outputs.", Schema.CornersOutputName);
            ITensor cornersTensor = Required(context, Schema.CornersOutputName);
            int candidates = ValidateShape(cornersTensor, context, Schema.CornersOutputName);
            if (candidates > Options.MaximumCandidates) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB candidate count exceeds its configured bound.", Schema.CornersOutputName, "candidates=" + candidates);
            ITensor scoresTensor = Required(context, Schema.ScoresOutputName);
            ITensor classesTensor = Required(context, Schema.ClassesOutputName);
            ValidateVector(scoresTensor, candidates, context, Schema.ScoresOutputName);
            ValidateVector(classesTensor, candidates, context, Schema.ClassesOutputName);
            OrientedDecoderShared.EnsureWorkspace(context, Options, cornersTensor, scoresTensor, classesTensor);
            float[] corners = VisualTensorReader.ReadFiniteScores(cornersTensor, context.Profile.ProfileId, Schema.CornersOutputName);
            float[] scores = VisualTensorReader.ReadFiniteScores(scoresTensor, context.Profile.ProfileId, Schema.ScoresOutputName);
            float[] classes = VisualTensorReader.ReadFiniteScores(classesTensor, context.Profile.ProfileId, Schema.ClassesOutputName);
            var decoded = new List<OrientedCandidate>(candidates);
            for (int candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                float score = scores[candidateIndex];
                if (score < 0) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB score must be non-negative.", Schema.ScoresOutputName, "candidate=" + candidateIndex);
                if (score < Options.ScoreThreshold) continue;
                float classValue = classes[candidateIndex];
                if (classValue < 0 || classValue > int.MaxValue || classValue != (float)Math.Floor(classValue)) throw Failure(context, VisualErrorCodes.DecodeFailed, "OBB class values must be non-negative integers.", Schema.ClassesOutputName, "candidate=" + candidateIndex);
                int classIndex = checked((int)classValue);
                int offset = checked(candidateIndex * 8);
                var modelPoints = new PointF[4];
                for (int pointIndex = 0; pointIndex < 4; pointIndex++)
                {
                    float x = corners[offset + (pointIndex * 2)];
                    float y = corners[offset + (pointIndex * 2) + 1];
                    if (Schema.CoordinateSpace == OrientedCoordinateSpace.Normalized) { x *= context.Input.ModelSize.Width; y *= context.Input.ModelSize.Height; }
                    modelPoints[pointIndex] = new PointF(x, y);
                }
                OrientedQuadrilateral model;
                try { model = OrientedQuadrilateral.Canonicalize(modelPoints, Schema.InputVertexOrder, Schema.StartVertexRule, Schema.Epsilon); }
                catch (ArgumentException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Four-corner OBB values are not a valid strict convex quadrilateral.", Schema.CornersOutputName, "candidate=" + candidateIndex, exception); }
                var sourcePoints = new PointF[4];
                for (int pointIndex = 0; pointIndex < 4; pointIndex++)
                {
                    sourcePoints[pointIndex] = context.Input.Transform.ToSource(model.Vertices[pointIndex]);
                    if (Schema.BoundaryMode == OrientedDetectionBoundaryMode.RejectOutsideSource && (sourcePoints[pointIndex].X < 0 || sourcePoints[pointIndex].X > context.Input.SourceSize.Width || sourcePoints[pointIndex].Y < 0 || sourcePoints[pointIndex].Y > context.Input.SourceSize.Height)) throw Failure(context, VisualErrorCodes.DecodeFailed, "A restored OBB vertex is outside the source image.", Schema.CornersOutputName);
                }
                OrientedQuadrilateral source;
                try { source = OrientedQuadrilateral.Canonicalize(sourcePoints, OrientedVertexOrder.CounterClockwise, OrientedStartVertexRule.MinimumYThenX, Schema.Epsilon); }
                catch (ArgumentException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Restored OBB vertices are not a strict convex quadrilateral.", Schema.CornersOutputName, null, exception); }
                decoded.Add(new OrientedCandidate(candidateIndex, classIndex, score, source, TryDeriveAngle(source, Schema.Epsilon)));
            }

            decoded.Sort((left, right) => { int score = right.Score.CompareTo(left.Score); return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex); });
            var kept = new List<OrientedCandidate>(Math.Min(decoded.Count, Options.MaximumDetections));
            for (int candidateIndex = 0; candidateIndex < decoded.Count && kept.Count < Options.MaximumDetections; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                OrientedCandidate candidate = decoded[candidateIndex];
                bool suppressed = false;
                for (int keptIndex = 0; keptIndex < kept.Count; keptIndex++)
                {
                    if (Options.NmsMode == DetectionNmsMode.ClassAware && kept[keptIndex].ClassIndex != candidate.ClassIndex) continue;
                    if (OrientedGeometry.IntersectionOverUnion(kept[keptIndex].Quadrilateral, candidate.Quadrilateral, Schema.Epsilon, context.CancellationToken) > Options.IouThreshold) { suppressed = true; break; }
                }
                if (!suppressed) kept.Add(candidate);
            }
            var results = new List<OrientedDetection>(kept.Count);
            for (int index = 0; index < kept.Count; index++) { OrientedCandidate candidate = kept[index]; results.Add(new OrientedDetection(candidate.SourceIndex, candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score, candidate.Quadrilateral, candidate.AngleRadiansCounterClockwise, false)); }
            return new OrientedDetectionResult(results, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private static float TryDeriveAngle(OrientedQuadrilateral quadrilateral, float epsilon)
        {
            PointF first = quadrilateral.First;
            PointF second = quadrilateral.Second;
            PointF third = quadrilateral.Third;
            double abX = second.X - first.X;
            double abY = second.Y - first.Y;
            double bcX = third.X - second.X;
            double bcY = third.Y - second.Y;
            double dot = (abX * bcX) + (abY * bcY);
            double length = Math.Sqrt((abX * abX + abY * abY) * (bcX * bcX + bcY * bcY));
            if (length <= epsilon || Math.Abs(dot) > length * 0.0001d) return float.NaN;
            return (float)Math.Atan2(-abY, abX);
        }
        private static int ValidateShape(ITensor tensor, VisualDecodeContext context, string name)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 3 || shape[0] != 1 || shape[2] != 8 || tensor.Length != (long)shape[1] * 8) throw Failure(context, VisualErrorCodes.TensorInvalid, "OBB corners must have shape [1,N,8].", name, shape.ToString());
            try { return checked((int)shape[1]); } catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "OBB candidate count exceeds Int32 bounds.", name, shape.ToString(), exception); }
        }
        private static void ValidateVector(ITensor tensor, int candidates, VisualDecodeContext context, string name) { TensorShape shape = tensor.Shape; if (shape.Rank != 2 || shape[0] != 1 || shape[1] != candidates || tensor.Length != candidates) throw Failure(context, VisualErrorCodes.TensorInvalid, "OBB scores and classes must have shape [1,N].", name, shape.ToString()); }
        private static ITensor Required(VisualDecodeContext context, string name) { try { return context.Outputs.GetRequired(name); } catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "A required OBB output is missing.", name, null, exception); } }
        private static VisualException Failure(VisualDecodeContext context, string code, string message, string? tensorName = null, string? details = null, Exception? exception = null) => new VisualException(code, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
        private sealed class OrientedCandidate
        {
            public OrientedCandidate(int sourceIndex, int classIndex, float score, OrientedQuadrilateral quadrilateral, float angle) { SourceIndex = sourceIndex; ClassIndex = classIndex; Score = score; Quadrilateral = quadrilateral; AngleRadiansCounterClockwise = float.IsNaN(angle) ? (float?)null : angle; }
            public int SourceIndex { get; }
            public int ClassIndex { get; }
            public float Score { get; }
            public OrientedQuadrilateral Quadrilateral { get; }
            public float? AngleRadiansCounterClockwise { get; }
        }
    }

    internal static class OrientedDecoderShared
    {
        public static void EnsureWorkspace(VisualDecodeContext context, OrientedDetectionDecoderOptions options, params ITensor[] tensors)
        {
            long bytes = 0;
            try { for (int index = 0; index < tensors.Length; index++) if (tensors[index].ElementType == TensorElementType.Float64) bytes = checked(bytes + checked(tensors[index].Length * sizeof(float))); }
            catch (OverflowException exception) { throw new VisualException(VisualErrorCodes.DecodeFailed, "OBB conversion workspace size overflowed.", exception, modelId: context.Profile.ModelId, profileId: context.Profile.ProfileId, technicalDetails: exception.ToString()); }
            if (bytes > options.MaximumWorkspaceBytes) throw new VisualException(VisualErrorCodes.DecodeFailed, "OBB conversion workspace exceeds its configured bound.", profileId: context.Profile.ProfileId, modelId: context.Profile.ModelId, technicalDetails: "workspaceBytes=" + bytes);
        }
    }
}
