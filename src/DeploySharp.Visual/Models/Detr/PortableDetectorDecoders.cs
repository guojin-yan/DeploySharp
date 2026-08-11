using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Detr
{
    /// <summary>Stores one bounded RF-DETR query/class candidate. / 存储一个有界的 RF-DETR Query/类别候选。</summary>
    internal readonly struct PortableRfCandidate
    {
        public PortableRfCandidate(int query, int classIndex, int sourceIndex, float score)
        {
            Query = query;
            ClassIndex = classIndex;
            SourceIndex = sourceIndex;
            Score = score;
        }

        public int Query { get; }
        public int ClassIndex { get; }
        public int SourceIndex { get; }
        public float Score { get; }
    }

    /// <summary>Decodes DEIMv2, RF-DETR, Paddle RT-DETR, RT-DETRv2, and PP-YOLOE detection tensors with one cancellation-aware decode per backend result. / 对 DEIMv2、RF-DETR、Paddle RT-DETR、RT-DETRv2 与 PP-YOLOE 检测张量执行每组后端结果一次且可取消的解码。</summary>
    public sealed class PortableDetectorDecoder : IVisualDecoder
    {
        /// <summary>Initializes a strict detector decoder. / 初始化严格检测解码器。</summary>
        public PortableDetectorDecoder(PortableDetectorOutputContract contract, float scoreThreshold = 0.4f, int maximumCandidates = 3000, int maximumResults = 300, int topK = 300)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            if (scoreThreshold < 0 || scoreThreshold > 1 || float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold)) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (maximumCandidates <= 0 || maximumResults <= 0 || maximumResults > maximumCandidates) throw new ArgumentOutOfRangeException(nameof(maximumResults));
            if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));
            if (topK < maximumResults) throw new ArgumentException("Top-k cannot be smaller than the result bound.", nameof(topK));
            ScoreThreshold = scoreThreshold;
            MaximumCandidates = maximumCandidates;
            MaximumResults = maximumResults;
            TopK = topK;
        }

        /// <summary>Gets the object-detection task handled by this decoder. / 获取此 Decoder 处理的目标检测任务。</summary>
        public VisualTaskId Task => VisualTaskId.ObjectDetection;
        /// <summary>Gets exact output semantics. / 获取精确输出语义。</summary>
        public PortableDetectorOutputContract Contract { get; }
        /// <summary>Gets the strict score threshold. / 获取严格分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets the candidate bound. / 获取候选上限。</summary>
        public int MaximumCandidates { get; }
        /// <summary>Gets the returned-result bound. / 获取返回结果上限。</summary>
        public int MaximumResults { get; }
        /// <summary>Gets RF-DETR global top-k. / 获取 RF-DETR 全局 top-k。</summary>
        public int TopK { get; }

        /// <summary>Decodes one named portable-detector output set into owned detections. / 将一组具名便携检测器输出解码为自有检测结果。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (Contract.Kind == PortableDetectorOutputKind.DeimDecoded || Contract.Kind == PortableDetectorOutputKind.RtDetrV2Decoded) return DecodeTriplet(context);
            if (Contract.Kind == PortableDetectorOutputKind.PaddleDecoded) return DecodePaddle(context);
            if (Contract.Kind == PortableDetectorOutputKind.RfDetrRaw || Contract.Kind == PortableDetectorOutputKind.RtDetrRaw) return DecodeRawQueries(context);
            throw Failure(context, "The detector decoder contract is not a detection contract.");
        }

        private object DecodeTriplet(VisualDecodeContext context)
        {
            ITensor labelTensor = Required(context, Contract.LabelsName);
            ITensor boxTensor = Required(context, Contract.BoxesName);
            ITensor scoreTensor = Required(context, Contract.ScoresName);
            long[] labels = ReadIntegers(labelTensor, context, Contract.LabelsName);
            float[] boxes = ReadFloats(boxTensor, context, Contract.BoxesName);
            float[] scores = ReadFloats(scoreTensor, context, Contract.ScoresName);
            int count = ResolveBatchVectorCount(labelTensor, context, Contract.LabelsName);
            if (count > MaximumCandidates) throw Failure(context, "Decoded detector candidate count exceeds the configured bound.", Contract.LabelsName);
            ValidateBatchVector(scoreTensor, count, context, Contract.ScoresName);
            ValidateBoxes(boxTensor, count, context, Contract.BoxesName, "Decoded detector boxes must have shape [1,N,4].");
            if (labels.Length != count || scores.Length != count || boxes.Length != checked(count * 4)) throw Failure(context, "Decoded detector output tensors have inconsistent element counts.");
            var result = new List<Detection>();
            for (int index = 0; index < count && result.Count < MaximumResults; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                float score = scores[index];
                ValidateFinite(score, context, Contract.ScoresName);
                // Decoded triplet exports own query selection; DeploySharp applies one strict consumer threshold and never adds NMS. / 已解码三张量导出拥有 Query 选择；DeploySharp 仅应用一次严格消费者阈值且绝不追加 NMS。
                if (score <= ScoreThreshold) continue;
                int classIndex = ToClassIndex(labels[index], context, Contract.LabelsName);
                RectangleF box = SourceBox(context, boxes, index * 4, Contract.CoordinateSpace == PortableDetectorCoordinateSpace.NormalizedSource, Contract.CoordinateSpace == PortableDetectorCoordinateSpace.SourcePixels);
                if (box.Width <= 0 || box.Height <= 0) continue;
                result.Add(new Detection(box, new LabelScore(classIndex, context.Profile.GetLabel(classIndex), score)));
            }

            return new DetectionResult(result);
        }

        private object DecodePaddle(VisualDecodeContext context)
        {
            ITensor rowTensor = Required(context, Contract.BoxesName);
            ITensor countTensor = Required(context, Contract.CountName!);
            float[] rows = ReadFloats(rowTensor, context, Contract.BoxesName);
            long[] counts = ReadIntegers(countTensor, context, Contract.CountName!);
            int count = ResolvePaddleCount(countTensor, counts, context, Contract.CountName!);
            TensorShape rowShape = rowTensor.Shape;
            if (rowShape.Rank != 2 || rowShape[0] < count || rowShape[1] != 6 || rows.Length != rowShape.GetElementCount()) throw Failure(context, "Paddle decoded rows must have shape [N,6] and include every declared result.", Contract.BoxesName, technicalDetails: rowShape.ToString());
            if (count > MaximumCandidates) throw Failure(context, "Paddle decoded output count is outside the configured candidate bound.", Contract.CountName!);
            var result = new List<Detection>();
            for (int index = 0; index < count && result.Count < MaximumResults; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                int offset = index * 6;
                int classIndex = ToClassIndex(rows[offset], context, Contract.BoxesName);
                float score = rows[offset + 1];
                ValidateFinite(score, context, Contract.BoxesName);
                if (classIndex < 0 || score <= ScoreThreshold) continue;
                RectangleF box = SourceBox(context, rows, offset + 2, false, true);
                if (box.Width <= 0 || box.Height <= 0) continue;
                result.Add(new Detection(box, new LabelScore(classIndex, context.Profile.GetLabel(classIndex), score)));
            }

            return new DetectionResult(result);
        }

        private object DecodeRawQueries(VisualDecodeContext context)
        {
            float[] boxes = ReadFloats(Required(context, Contract.BoxesName), context, Contract.BoxesName);
            float[] logits = ReadFloats(Required(context, Contract.LabelsName), context, Contract.LabelsName);
            TensorShape boxShape = Required(context, Contract.BoxesName).Shape;
            TensorShape logitShape = Required(context, Contract.LabelsName).Shape;
            int queries = ResolveQueryCount(boxShape, 4, context, Contract.BoxesName);
            int fields = ResolveQueryFields(logitShape, context, Contract.LabelsName);
            int expectedFields = checked(Contract.ClassCount + (Contract.IncludesNoObjectClass ? 1 : 0));
            if (Contract.QueryCount >= 0 && queries != Contract.QueryCount) throw Failure(context, "Detector query count does not match the artifact-bound profile.", Contract.BoxesName, technicalDetails: "expected=" + Contract.QueryCount + ";actual=" + queries);
            if (queries > MaximumCandidates || logitShape[1] != queries || fields != expectedFields || boxes.Length != checked(queries * 4) || logits.Length != checked(queries * fields)) throw Failure(context, "Raw detector outputs are inconsistent with the artifact-bound query/class contract.");
            List<PortableRfCandidate> ordered = SelectRfCandidates(logits, queries, fields, Contract, ScoreThreshold, TopK, MaximumResults, context, Contract.LabelsName);
            var result = new List<Detection>(ordered.Count);
            foreach (PortableRfCandidate candidate in ordered)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                RectangleF box = SourceBox(context, boxes, candidate.Query * 4, true, false);
                if (box.Width <= 0 || box.Height <= 0) continue;
                result.Add(new Detection(box, new LabelScore(candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score)));
            }

            return new DetectionResult(result);
        }

        private RectangleF SourceBox(VisualDecodeContext context, float[] values, int offset, bool normalizedSource, bool sourceCoordinates)
        {
            float x1 = values[offset];
            float y1 = values[offset + 1];
            float x2 = values[offset + 2];
            float y2 = values[offset + 3];
            for (int index = 0; index < 4; index++) ValidateFinite(values[offset + index], context, Contract.BoxesName);
            if (normalizedSource)
            {
                // RF-DETR emits normalized cxcywh, unlike the decoded xyxy contracts. / RF-DETR 输出归一化 cxcywh，不同于已解码 xyxy 合同。
                float cx = x1 * context.Input.SourceSize.Width;
                float cy = y1 * context.Input.SourceSize.Height;
                float width = x2 * context.Input.SourceSize.Width;
                float height = y2 * context.Input.SourceSize.Height;
                x1 = cx - (width / 2f); y1 = cy - (height / 2f); x2 = cx + (width / 2f); y2 = cy + (height / 2f);
            }
            RectangleF box = new RectangleF(x1, y1, x2 - x1, y2 - y1);
            if (!sourceCoordinates && !normalizedSource) box = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(box));
            else box = ClipSource(box, context.Input.SourceSize);
            return box;
        }

        internal static RectangleF ClipSource(RectangleF box, VisualSize size)
        {
            float left = Math.Max(0, Math.Min(size.Width, box.X));
            float top = Math.Max(0, Math.Min(size.Height, box.Y));
            float right = Math.Max(0, Math.Min(size.Width, box.Right));
            float bottom = Math.Max(0, Math.Min(size.Height, box.Bottom));
            return new RectangleF(left, top, right - left, bottom - top);
        }

        internal static ITensor Required(VisualDecodeContext context, string name)
        {
            try { return context.Outputs.GetRequired(name); }
            catch (KeyNotFoundException exception) { throw Failure(context, "A required model output is missing.", name, exception); }
        }

        internal static float[] ReadFloats(ITensor tensor, VisualDecodeContext context, string name)
        {
            if (tensor.ElementType == TensorElementType.Float32 && tensor.Buffer is float[] values) return values;
            if (tensor.ElementType == TensorElementType.Float64 && tensor.Buffer is double[] doubles)
            {
                var result = new float[doubles.Length];
                for (int index = 0; index < doubles.Length; index++) result[index] = checked((float)doubles[index]);
                return result;
            }

            throw Failure(context, "A detector output must be Float32 or Float64.", name);
        }

        internal static long[] ReadIntegers(ITensor tensor, VisualDecodeContext context, string name)
        {
            if (tensor.ElementType == TensorElementType.Int64 && tensor.Buffer is long[] longs) return longs;
            if (tensor.ElementType == TensorElementType.Int32 && tensor.Buffer is int[] ints)
            {
                var result = new long[ints.Length];
                for (int index = 0; index < ints.Length; index++) result[index] = ints[index];
                return result;
            }

            throw Failure(context, "A detector integer output must be Int32 or Int64.", name);
        }

        private static int ResolveBatchVectorCount(ITensor tensor, VisualDecodeContext context, string name)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 2 || shape[0] != 1 || shape[1] < 0) throw Failure(context, "A decoded vector output must have shape [1,N].", name, technicalDetails: shape.ToString());
            return checked((int)shape[1]);
        }

        private static void ValidateBatchVector(ITensor tensor, int count, VisualDecodeContext context, string name)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 2 || shape[0] != 1 || shape[1] != count || tensor.Length != count) throw Failure(context, "Decoded vector outputs must use the same [1,N] shape.", name, technicalDetails: shape.ToString());
        }

        private static void ValidateBoxes(ITensor tensor, int count, VisualDecodeContext context, string name, string message)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 3 || shape[0] != 1 || shape[1] != count || shape[2] != 4 || tensor.Length != checked(count * 4)) throw Failure(context, message, name, technicalDetails: shape.ToString());
        }

        private static int ResolvePaddleCount(ITensor tensor, long[] values, VisualDecodeContext context, string name)
        {
            TensorShape shape = tensor.Shape;
            if (!((shape.Rank == 0 && tensor.Length == 1) || (shape.Rank == 1 && shape[0] == 1 && tensor.Length == 1)) || values.Length != 1) throw Failure(context, "Paddle bbox_num must be a scalar or a one-element vector for batch one.", name, technicalDetails: shape.ToString());
            return ToCount(values[0], context, name);
        }

        private static int ToCount(long value, VisualDecodeContext context, string name)
        {
            if (value < 0 || value > int.MaxValue) throw Failure(context, "A detector count must be a non-negative Int32 value.", name);
            return (int)value;
        }

        private static int ToClassIndex(long value, VisualDecodeContext context, string name)
        {
            if (value < 0 || value > int.MaxValue) throw Failure(context, "A detector class index must be a non-negative Int32 value.", name);
            return (int)value;
        }

        private static int ToClassIndex(float value, VisualDecodeContext context, string name)
        {
            ValidateFinite(value, context, name);
            if (value < 0 || value > int.MaxValue || value != (float)Math.Truncate(value)) throw Failure(context, "A decoded detector class value must be a non-negative integer.", name);
            return (int)value;
        }

        private static int ResolveQueryCount(TensorShape shape, int fields, VisualDecodeContext context, string name)
        {
            if (shape.Rank != 3 || shape[0] != 1 || shape[2] != fields) throw Failure(context, "RF-DETR boxes must have shape [1,Q,4].", name, technicalDetails: shape.ToString());
            return checked((int)shape[1]);
        }

        private static int ResolveQueryFields(TensorShape shape, VisualDecodeContext context, string name)
        {
            if (shape.Rank != 3 || shape[0] != 1) throw Failure(context, "RF-DETR logits must have shape [1,Q,C].", name, technicalDetails: shape.ToString());
            return checked((int)shape[2]);
        }

        internal static float Sigmoid(float value)
        {
            if (value >= 0) return 1f / (1f + (float)Math.Exp(-value));
            float e = (float)Math.Exp(value);
            return e / (1f + e);
        }

        internal static void ValidateFinite(float value, VisualDecodeContext context, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw Failure(context, "A detector tensor contains NaN or infinity.", name);
        }

        internal static VisualException Failure(VisualDecodeContext context, string message, string? tensorName = null, Exception? inner = null, string? technicalDetails = null)
        {
            return new VisualException(VisualErrorCodes.DecodeFailed, message, inner, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: technicalDetails);
        }

        internal static List<PortableRfCandidate> SelectRfCandidates(float[] logits, int queries, int fields, PortableDetectorOutputContract contract, float scoreThreshold, int topK, int maximumResults, VisualDecodeContext context, string tensorName)
        {
            int limit = Math.Min(topK, maximumResults);
            var selected = new List<PortableRfCandidate>(limit);
            for (int query = 0; query < queries; query++)
            {
                int offset = checked(query * fields);
                for (int column = 0; column < fields; column++)
                {
                    float logit = logits[offset + column];
                    ValidateFinite(logit, context, tensorName);
                    if (column >= contract.ClassCount) continue;
                    float score = Sigmoid(logit);
                    if (score <= scoreThreshold) continue;
                    InsertCandidate(selected, new PortableRfCandidate(query, column, offset + column, score), limit);
                }
            }

            return selected;
        }

        private static void InsertCandidate(List<PortableRfCandidate> selected, PortableRfCandidate candidate, int limit)
        {
            if (selected.Count == limit && CompareCandidates(candidate, selected[selected.Count - 1]) >= 0) return;
            int position = selected.Count;
            while (position > 0 && CompareCandidates(candidate, selected[position - 1]) < 0) position--;
            selected.Insert(position, candidate);
            if (selected.Count > limit) selected.RemoveAt(selected.Count - 1);
        }

        internal static int CompareCandidates(PortableRfCandidate left, PortableRfCandidate right)
        {
            int score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex);
        }
    }

    /// <summary>Decodes RF-DETR raw mask logits with the official global top-k and bilinear restoration. / 使用官方全局 top-k 与双线性恢复解码 RF-DETR 原始掩码 Logit。</summary>
    public sealed class RFDETRInstanceSegmentationDecoder : IVisualDecoder
    {
        /// <summary>Initializes the RF-DETR segmentation decoder. / 初始化 RF-DETR 分割解码器。</summary>
        public RFDETRInstanceSegmentationDecoder(PortableDetectorOutputContract contract, float scoreThreshold = 0.4f, int topK = 300, int maximumResults = 300, long maximumMaskPixels = 64L * 1024 * 1024, int maximumQueries = 3000)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            if (contract.Kind != PortableDetectorOutputKind.RfDetrSegmentation) throw new ArgumentException("The contract must describe RF-DETR segmentation.", nameof(contract));
            if (scoreThreshold < 0 || scoreThreshold > 1 || float.IsNaN(scoreThreshold) || float.IsInfinity(scoreThreshold)) throw new ArgumentOutOfRangeException(nameof(scoreThreshold));
            if (topK <= 0 || maximumResults <= 0 || maximumResults > topK) throw new ArgumentOutOfRangeException(nameof(maximumResults));
            if (maximumMaskPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumMaskPixels));
            if (maximumQueries <= 0) throw new ArgumentOutOfRangeException(nameof(maximumQueries));
            ScoreThreshold = scoreThreshold;
            TopK = topK;
            MaximumResults = maximumResults;
            MaximumMaskPixels = maximumMaskPixels;
            MaximumQueries = maximumQueries;
        }

        /// <summary>Gets the instance-segmentation task handled by this decoder. / 获取此 Decoder 处理的实例分割任务。</summary>
        public VisualTaskId Task => VisualTaskId.InstanceSegmentation;
        /// <summary>Gets the exact output contract. / 获取精确输出合同。</summary>
        public PortableDetectorOutputContract Contract { get; }
        /// <summary>Gets the strict score threshold. / 获取严格分数阈值。</summary>
        public float ScoreThreshold { get; }
        /// <summary>Gets global top-k. / 获取全局 top-k。</summary>
        public int TopK { get; }
        /// <summary>Gets the result bound. / 获取结果上限。</summary>
        public int MaximumResults { get; }
        /// <summary>Gets the total source-mask pixel budget. / 获取源图掩码像素总预算。</summary>
        public long MaximumMaskPixels { get; }
        /// <summary>Gets the maximum accepted raw query count. / 获取允许的原始 Query 数上限。</summary>
        public int MaximumQueries { get; }

        /// <summary>Decodes one RF-DETR query and mask output set into owned instances. / 将一组 RF-DETR Query 与 mask 输出解码为自有实例。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            ITensor boxTensor = PortableDetectorDecoder.Required(context, Contract.BoxesName);
            ITensor logitTensor = PortableDetectorDecoder.Required(context, Contract.LabelsName);
            ITensor maskTensor = PortableDetectorDecoder.Required(context, Contract.MasksName!);
            float[] boxes = PortableDetectorDecoder.ReadFloats(boxTensor, context, Contract.BoxesName);
            float[] logits = PortableDetectorDecoder.ReadFloats(logitTensor, context, Contract.LabelsName);
            float[] masks = PortableDetectorDecoder.ReadFloats(maskTensor, context, Contract.MasksName!);
            int queries = ResolveQueryCount(boxTensor.Shape, context, Contract.BoxesName);
            int fields = ResolveLogitFields(logitTensor.Shape, context, Contract.LabelsName);
            MaskShape maskShape = ResolveMaskShape(maskTensor.Shape, queries, context);
            int expectedFields = checked(Contract.ClassCount + (Contract.IncludesNoObjectClass ? 1 : 0));
            if (Contract.QueryCount >= 0 && queries != Contract.QueryCount) throw PortableDetectorDecoder.Failure(context, "RF-DETR segmentation query count does not match the artifact-bound profile.", Contract.BoxesName, technicalDetails: "expected=" + Contract.QueryCount + ";actual=" + queries);
            if (queries > MaximumQueries || logitTensor.Shape[1] != queries || fields != expectedFields || boxes.Length != checked(queries * 4) || logits.Length != checked(queries * fields) || masks.Length != checked(queries * maskShape.Width * maskShape.Height)) throw PortableDetectorDecoder.Failure(context, "RF-DETR segmentation tensors are inconsistent with the artifact-bound query/class contract.");
            List<PortableRfCandidate> selected = PortableDetectorDecoder.SelectRfCandidates(logits, queries, fields, Contract, ScoreThreshold, TopK, MaximumResults, context, Contract.LabelsName);
            long materializedPixels;
            try { materializedPixels = checked((long)context.Input.SourceSize.Width * context.Input.SourceSize.Height * selected.Count); }
            catch (OverflowException exception) { throw PortableDetectorDecoder.Failure(context, "RF-DETR segmentation source-mask size overflowed.", Contract.MasksName, exception); }
            if (materializedPixels > MaximumMaskPixels) throw PortableDetectorDecoder.Failure(context, "RF-DETR segmentation output exceeds the configured source-mask pixel budget.");
            var instances = new List<InstanceSegmentationInstance>(selected.Count);
            int plane = checked(maskShape.Width * maskShape.Height);
            for (int resultIndex = 0; resultIndex < selected.Count; resultIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                PortableRfCandidate candidate = selected[resultIndex];
                RectangleF sourceBox = DecodeBox(boxes, candidate.Query * 4, context);
                RectangleF modelBox = context.Input.Transform.ToModel(sourceBox);
                float[] grid = masks;
                int maskOffset = checked(candidate.Query * plane);
                ValidateMaskPlane(grid, maskOffset, plane, context, Contract.MasksName!);
                // RF-DETR restores full-source raw mask logits with bilinear align_corners=false and a strict > 0 threshold. / RF-DETR 以 bilinear align_corners=false 恢复全源图原始掩码 Logit，并使用严格的 > 0 阈值。
                InstanceBinaryMask mask = InstanceMaskRestorer.Restore(grid, maskOffset, maskShape.Width, maskShape.Height, context.Input, modelBox, InstanceMaskValueKind.Logits, InstanceMaskActivation.None, InstanceMaskInterpolationMode.BilinearHalfPixel, InstanceMaskThresholdOrder.AfterResize, InstanceMaskCropSpace.None, InstanceMaskCropOrder.AfterResize, 0f, context.CancellationToken, thresholdIsStrict: true);
                InstanceMaskRle rle = InstanceMaskRle.Encode(mask, int.MaxValue, context.CancellationToken);
                instances.Add(new InstanceSegmentationInstance(candidate.SourceIndex, candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score, sourceBox, mask, rle));
            }

            return new InstanceSegmentationResult(instances, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private static RectangleF DecodeBox(float[] values, int offset, VisualDecodeContext context)
        {
            for (int index = 0; index < 4; index++) PortableDetectorDecoder.ValidateFinite(values[offset + index], context, context.Profile.ProfileId);
            float cx = values[offset] * context.Input.SourceSize.Width;
            float cy = values[offset + 1] * context.Input.SourceSize.Height;
            float width = values[offset + 2] * context.Input.SourceSize.Width;
            float height = values[offset + 3] * context.Input.SourceSize.Height;
            return PortableDetectorDecoder.ClipSource(new RectangleF(cx - width / 2f, cy - height / 2f, width, height), context.Input.SourceSize);
        }

        private static int ResolveQueryCount(TensorShape shape, VisualDecodeContext context, string name)
        {
            if (shape.Rank != 3 || shape[0] != 1 || shape[2] != 4) throw PortableDetectorDecoder.Failure(context, "RF-DETR segmentation boxes must have shape [1,Q,4].", name, technicalDetails: shape.ToString());
            return checked((int)shape[1]);
        }

        private static int ResolveLogitFields(TensorShape shape, VisualDecodeContext context, string name)
        {
            if (shape.Rank != 3 || shape[0] != 1 || shape[1] <= 0 || shape[2] <= 0) throw PortableDetectorDecoder.Failure(context, "RF-DETR segmentation labels must have shape [1,Q,C].", name, technicalDetails: shape.ToString());
            return checked((int)shape[2]);
        }

        private static void ValidateMaskPlane(float[] values, int offset, int length, VisualDecodeContext context, string name)
        {
            for (int index = 0; index < length; index++) PortableDetectorDecoder.ValidateFinite(values[offset + index], context, name);
        }

        private static MaskShape ResolveMaskShape(TensorShape shape, int queries, VisualDecodeContext context)
        {
            if (shape.Rank != 4 || shape[0] != 1 || shape[1] != queries || shape[2] <= 0 || shape[3] <= 0) throw PortableDetectorDecoder.Failure(context, "RF-DETR masks must have shape [1,Q,H,W].", context.Profile.ProfileId, technicalDetails: shape.ToString());
            return new MaskShape(checked((int)shape[3]), checked((int)shape[2]));
        }

        private readonly struct MaskShape
        {
            public MaskShape(int width, int height) { Width = width; Height = height; }
            public int Width { get; }
            public int Height { get; }
        }
    }
}
