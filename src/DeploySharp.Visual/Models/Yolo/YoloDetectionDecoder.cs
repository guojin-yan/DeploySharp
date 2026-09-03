using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Yolo
{
    /// <summary>Decodes versioned YOLO raw-head and end-to-end detection exports. / 解码版本化 YOLO 原始 Head 与端到端检测导出。</summary>
    public sealed class YoloDetectionDecoder : IVisualDecoder
    {
        /// <summary>Initializes a reusable YOLO detection decoder. / 初始化可复用 YOLO 检测解码器。</summary>
        public YoloDetectionDecoder(YoloDetectionOutputContract contract, YoloDetectionDecoderOptions? options = null)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            Options = options ?? new YoloDetectionDecoderOptions();
        }

        /// <summary>Gets the object-detection task implemented by this decoder. / 获取此 Decoder 实现的目标检测任务。</summary>
        public VisualTaskId Task => VisualTaskId.ObjectDetection;
        /// <summary>Gets the exact export output contract. / 获取精确导出输出合同。</summary>
        public YoloDetectionOutputContract Contract { get; }
        /// <summary>Gets filtering and NMS options. / 获取筛选与 NMS 选项。</summary>
        public YoloDetectionDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Raw-head NMS runs in model coordinates before source restoration; end-to-end row order is preserved. / 原始 Head 在源图恢复前使用模型坐标执行 NMS；端到端行顺序保持不变。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(Contract.OutputName); }
            catch (KeyNotFoundException exception) { throw Invalid("The required YOLO output tensor is missing.", context, exception); }
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Contract.OutputName);
            int batch = ResolveBatch(context, tensor.Shape);
            var results = new List<DetectionResult>(batch);
            for (int batchIndex = 0; batchIndex < batch; batchIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                results.Add(Contract.IsEndToEnd
                    ? DecodeEndToEnd(context, tensor.Shape, values, batchIndex, batch)
                    : DecodeRaw(context, tensor.Shape, values, batchIndex, batch));
            }
            return batch == 1 ? results[0] : new DetectionBatchResult(results);
        }

        private DetectionResult DecodeRaw(VisualDecodeContext context, TensorShape shape, float[] values, int batchIndex, int batch)
        {
            int candidates;
            int fields;
            bool attributeMajor = Contract.Kind == YoloDetectionOutputKind.RawAttributeMajor;
            int baseOffset = 0;
            if (shape.Rank == 3 && shape[0] == batch)
            {
                fields = checked((int)shape[attributeMajor ? 1 : 2]);
                candidates = checked((int)shape[attributeMajor ? 2 : 1]);
                baseOffset = checked(batchIndex * candidates * fields);
            }
            else if (shape.Rank == 2 && batch == 1)
            {
                fields = checked((int)shape[attributeMajor ? 0 : 1]);
                candidates = checked((int)shape[attributeMajor ? 1 : 0]);
            }
            else throw Invalid("A raw YOLO output must be [1,N,F], [N,F], [1,F,N], or [F,N] according to its explicit contract.", context, details: shape.ToString());
            if (fields != Contract.FieldCount || candidates <= 0 || values.LongLength != checked((long)batch * candidates * fields)) throw Invalid("The raw YOLO tensor shape does not match its explicit field contract.", context, details: shape.ToString());

            var decoded = new List<VisualDetectionCandidate>(Math.Min(candidates, Options.MaximumCandidates));
            for (int candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
            {
                if ((candidateIndex & 255) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                float objectness = Contract.HasObjectness ? Score(Read(values, baseOffset, candidates, fields, candidateIndex, 4, attributeMajor), context, candidateIndex, "objectness") : 1f;
                if (objectness <= Options.ScoreThreshold && Contract.HasObjectness) continue;
                int classOffset = Contract.HasObjectness ? 5 : 4;
                if (Options.ClassSelection == YoloClassSelectionMode.BestClassOnly)
                {
                    int bestClass = 0;
                    float bestClassScore = Score(Read(values, baseOffset, candidates, fields, candidateIndex, classOffset, attributeMajor), context, candidateIndex, "class-score");
                    for (int classIndex = 1; classIndex < Contract.ClassCount; classIndex++)
                    {
                        float classScore = Score(Read(values, baseOffset, candidates, fields, candidateIndex, classOffset + classIndex, attributeMajor), context, candidateIndex, "class-score");
                        if (classScore > bestClassScore) { bestClassScore = classScore; bestClass = classIndex; }
                    }
                    AddRawCandidate(decoded, context, values, baseOffset, candidates, fields, candidateIndex, attributeMajor, bestClass, objectness * bestClassScore, context.Input.BatchFrames[batchIndex]);
                }
                else
                {
                    for (int classIndex = 0; classIndex < Contract.ClassCount; classIndex++)
                    {
                        float classScore = Score(Read(values, baseOffset, candidates, fields, candidateIndex, classOffset + classIndex, attributeMajor), context, candidateIndex, "class-score");
                        AddRawCandidate(decoded, context, values, baseOffset, candidates, fields, candidateIndex, attributeMajor, classIndex, objectness * classScore, context.Input.BatchFrames[batchIndex]);
                    }
                }
            }

            decoded.Sort(CompareCandidates);
            if (decoded.Count > Options.MaximumCandidates) decoded.RemoveRange(Options.MaximumCandidates, decoded.Count - Options.MaximumCandidates);
            // Official YOLO NMS is performed before scale_boxes; model-coordinate IoU avoids non-uniform restoration changing suppression. / 官方 YOLO 在 scale_boxes 前执行 NMS；模型坐标 IoU 可避免非均匀恢复改变抑制结果。
            List<VisualDetectionCandidate> kept = DetectionPostprocessing.Suppress(decoded, Options.IouThreshold, Options.NmsMode, Options.MaximumDetections, context.CancellationToken, true);
            return ToResult(kept, context);
        }

        private DetectionResult DecodeEndToEnd(VisualDecodeContext context, TensorShape shape, float[] values, int batchIndex, int batch)
        {
            int fields = Contract.FieldCount;
            int rows;
            int baseOffset = 0;
            if (shape.Rank == 3 && shape[0] == batch && shape[2] == fields)
            {
                rows = checked((int)shape[1]);
                baseOffset = checked(batchIndex * rows * fields);
            }
            else if (shape.Rank == 2 && shape[1] == fields) rows = checked((int)shape[0]);
            else throw Invalid("The end-to-end YOLO tensor shape does not match its explicit row contract.", context, details: shape.ToString());
            if (rows < 0 || values.LongLength != checked((long)batch * rows * fields)) throw Invalid("The end-to-end YOLO tensor element count is inconsistent.", context, details: shape.ToString());
            var results = new List<Detection>(Math.Min(rows, Options.MaximumDetections));
            for (int row = 0; row < rows && results.Count < Options.MaximumDetections; row++)
            {
                if ((row & 255) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                int offset = checked(baseOffset + (row * fields));
                int boxOffset;
                int scoreIndex;
                int classIndex;
                if (Contract.Kind == YoloDetectionOutputKind.BatchedEndToEnd)
                {
                    int embeddedBatch = Integer(values[offset], context, row, "batch-index");
                    if (embeddedBatch != 0) continue;
                    boxOffset = offset + 1;
                    classIndex = Integer(values[offset + 5], context, row, "class-index");
                    scoreIndex = offset + 6;
                }
                else
                {
                    boxOffset = offset;
                    scoreIndex = offset + 4;
                    classIndex = Integer(values[offset + 5], context, row, "class-index");
                }
                if (classIndex < 0 || classIndex >= Contract.ClassCount) throw Invalid("An end-to-end YOLO class index is outside the configured class range.", context, details: "row=" + row + ";class=" + classIndex);
                float score = Score(values[scoreIndex], context, row, "score");
                if (score <= Options.ScoreThreshold) continue;
                RectangleF modelBox = DecodeXyxy(values[boxOffset], values[boxOffset + 1], values[boxOffset + 2], values[boxOffset + 3], context, row);
                VisualInputFrame frame = context.Input.BatchFrames[batchIndex];
                RectangleF sourceBox = frame.Transform.ClipToSource(frame.Transform.ToSource(modelBox));
                if (sourceBox.Width <= 0f || sourceBox.Height <= 0f) continue;
                results.Add(new Detection(sourceBox, new LabelScore(classIndex, context.Profile.GetLabel(classIndex), score)));
            }
            return new DetectionResult(results);
        }

        private void AddRawCandidate(List<VisualDetectionCandidate> decoded, VisualDecodeContext context, float[] values, int baseOffset, int candidates, int fields, int candidateIndex, bool attributeMajor, int classIndex, float score, VisualInputFrame frame)
        {
            if (score <= Options.ScoreThreshold) return;
            RectangleF modelBox;
            try
            {
                modelBox = DetectionPostprocessing.DecodeModelBox(
                    DetectionBoxFormat.Cxcywh,
                    false,
                    frame.ModelSize,
                    Read(values, baseOffset, candidates, fields, candidateIndex, 0, attributeMajor),
                    Read(values, baseOffset, candidates, fields, candidateIndex, 1, attributeMajor),
                    Read(values, baseOffset, candidates, fields, candidateIndex, 2, attributeMajor),
                    Read(values, baseOffset, candidates, fields, candidateIndex, 3, attributeMajor));
            }
            catch (ArgumentOutOfRangeException exception) { throw Invalid("A raw YOLO box has negative width or height.", context, exception, "candidate=" + candidateIndex); }
            RectangleF sourceBox = frame.Transform.ClipToSource(frame.Transform.ToSource(modelBox));
            if (sourceBox.Width <= 0f || sourceBox.Height <= 0f) return;
            decoded.Add(new VisualDetectionCandidate(candidateIndex, classIndex, score, modelBox, sourceBox));
        }

        private float Score(float value, VisualDecodeContext context, int candidate, string field)
        {
            float score = Contract.ScoreActivation == YoloScoreActivation.Sigmoid ? Sigmoid(value) : value;
            if (score < 0f || score > 1f) throw Invalid("A YOLO probability must be in [0,1].", context, details: "candidate=" + candidate + ";field=" + field + ";value=" + score);
            return score;
        }

        private static float Read(float[] values, int baseOffset, int candidates, int fields, int candidate, int field, bool attributeMajor)
        {
            return attributeMajor ? values[baseOffset + (field * candidates) + candidate] : values[baseOffset + (candidate * fields) + field];
        }

        private int ResolveBatch(VisualDecodeContext context, TensorShape shape)
        {
            int expected = context.Input.BatchSize;
            if (expected == 1) return 1;
            if (Contract.Kind == YoloDetectionOutputKind.BatchedEndToEnd) throw Invalid("The embedded-batch YOLO output is single-item only.", context, details: shape.ToString());
            if (shape.Rank != 3 || shape[0] != expected) throw Invalid("A true YOLO batch requires a rank-three output whose first dimension matches the input batch.", context, details: shape.ToString());
            return expected;
        }

        private static float Sigmoid(float value)
        {
            if (value >= 0f) return (float)(1d / (1d + Math.Exp(-value)));
            double exponential = Math.Exp(value);
            return (float)(exponential / (1d + exponential));
        }

        private static int Integer(float value, VisualDecodeContext context, int row, string field)
        {
            int integer = checked((int)Math.Round(value));
            if (Math.Abs(value - integer) > 0.001f) throw Invalid("A YOLO integer field contains a fractional value.", context, details: "row=" + row + ";field=" + field + ";value=" + value);
            return integer;
        }

        private static RectangleF DecodeXyxy(float left, float top, float right, float bottom, VisualDecodeContext context, int row)
        {
            if (right < left || bottom < top) throw Invalid("An end-to-end YOLO box has inverted coordinates.", context, details: "row=" + row);
            return new RectangleF(left, top, right - left, bottom - top);
        }

        private static int CompareCandidates(VisualDetectionCandidate left, VisualDetectionCandidate right)
        {
            int score = right.Score.CompareTo(left.Score);
            if (score != 0) return score;
            int classIndex = left.ClassIndex.CompareTo(right.ClassIndex);
            return classIndex != 0 ? classIndex : left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static DetectionResult ToResult(IReadOnlyList<VisualDetectionCandidate> candidates, VisualDecodeContext context)
        {
            var results = new List<Detection>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++)
            {
                VisualDetectionCandidate candidate = candidates[index];
                results.Add(new Detection(candidate.SourceBox, new LabelScore(candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score)));
            }
            return new DetectionResult(results);
        }

        private static VisualException Invalid(string message, VisualDecodeContext context, Exception? exception = null, string? details = null)
        {
            return new VisualException(VisualErrorCodes.YoloContractInvalid, message, exception, context.Profile.ProfileId, context.Profile.Decoder is YoloDetectionDecoder decoder ? decoder.Contract.OutputName : null, modelId: context.Profile.ModelId, technicalDetails: details);
        }
    }
}
