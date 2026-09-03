using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies whether classification output contains logits or probabilities. / 标识分类输出包含 logits 还是 probabilities。</summary>
    public enum ClassificationScoreMode
    {
        /// <summary>Apply a numerically stable softmax to logits. / 对 logits 应用数值稳定的 softmax。</summary>
        Logits = 0,
        /// <summary>Use finite scores already constrained to [0,1]. / 使用已经限制在 [0,1] 的有限分数。</summary>
        Probabilities = 1
    }

    /// <summary>Decodes classification scores into a canonical result; dynamic [batch,classes] outputs return <see cref="ClassificationBatchResult"/>. / 将分类分数解码为规范结果；动态 [batch,classes] 输出返回 ClassificationBatchResult。</summary>
    public sealed class ClassificationDecoder : IVisualDecoder
    {
        /// <summary>Initializes a classification decoder. / 初始化分类解码器。</summary>
        public ClassificationDecoder(string outputName, ClassificationScoreMode scoreMode = ClassificationScoreMode.Logits, int topK = 5, float threshold = 0)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output tensor name is required.", nameof(outputName));
            if (!Enum.IsDefined(typeof(ClassificationScoreMode), scoreMode)) throw new ArgumentOutOfRangeException(nameof(scoreMode));
            if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));
            if (float.IsNaN(threshold) || float.IsInfinity(threshold) || threshold < 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            OutputName = outputName;
            ScoreMode = scoreMode;
            TopK = topK;
            Threshold = threshold;
        }

        /// <inheritdoc />
        /// <remarks>Classification decoder task is immutable. / 分类解码器任务不可变。</remarks>
        public VisualTaskId Task => VisualTaskId.ImageClassification;
        /// <summary>Gets the bound output tensor name. / 获取绑定的输出张量名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets score interpretation mode. / 获取分数解释模式。</summary>
        public ClassificationScoreMode ScoreMode { get; }
        /// <summary>Gets the maximum returned prediction count. / 获取最大返回预测数量。</summary>
        public int TopK { get; }
        /// <summary>Gets the inclusive score threshold. / 获取包含边界的分数阈值。</summary>
        public float Threshold { get; }

        /// <inheritdoc />
        /// <remarks>Uses max-subtraction before exponentiation and deterministic class-index tie breaking. / 指数运算前减去最大值，并使用确定性的类别索引打破同分。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(OutputName); }
            catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "Classification output tensor is missing.", exception, context.Profile.ProfileId, OutputName, modelId: context.Profile.ModelId); }
            int batch;
            int classCount;
            if (tensor.Shape.Rank == 1)
            {
                if (context.Input.BatchSize != 1) throw InvalidShape(context, tensor, "A batched classification input requires a [batch,classes] output.");
                batch = 1;
                classCount = checked((int)tensor.Shape[0]);
            }
            else if (tensor.Shape.Rank == 2)
            {
                batch = checked((int)tensor.Shape[0]);
                classCount = checked((int)tensor.Shape[1]);
                if (batch != context.Input.BatchSize) throw InvalidShape(context, tensor, "Classification output batch does not match the input batch.");
            }
            else throw InvalidShape(context, tensor, "Classification output shape must be [classes] or [batch,classes].");
            if (batch <= 0 || classCount <= 0 || tensor.Length != (long)batch * classCount) throw InvalidShape(context, tensor, "Classification output contains no classes or an inconsistent element count.");
            if (context.Profile.Labels.Count > 0 && context.Profile.Labels.Any(label => label.Index >= classCount)) throw new VisualException(VisualErrorCodes.DecodeFailed, "A profile label index exceeds the classification tensor class count.", profileId: context.Profile.ProfileId, tensorName: OutputName);
            float[] raw = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, OutputName);
            var results = new List<ClassificationResult>(batch);
            for (int row = 0; row < batch; row++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                results.Add(DecodeRow(raw, row * classCount, classCount, context));
            }
            return batch == 1 ? results[0] : new ClassificationBatchResult(results);
        }

        private ClassificationResult DecodeRow(float[] raw, int offset, int classCount, VisualDecodeContext context)
        {
            float[] scores = ScoreMode == ClassificationScoreMode.Logits ? Softmax(raw, offset, classCount) : ValidateProbabilities(raw, offset, classCount, context.Profile.ProfileId, OutputName);
            var candidates = new List<ClassificationCandidate>(classCount);
            for (int index = 0; index < classCount; index++)
            {
                if (scores[index] >= Threshold) candidates.Add(new ClassificationCandidate(index, scores[index]));
            }

            candidates.Sort((left, right) =>
            {
                int score = right.Score.CompareTo(left.Score);
                return score != 0 ? score : left.Index.CompareTo(right.Index);
            });
            int count = Math.Min(TopK, candidates.Count);
            if (candidates.Count > count) candidates.RemoveRange(count, candidates.Count - count);
            var predictions = new List<LabelScore>(count);
            for (int index = 0; index < count; index++)
            {
                ClassificationCandidate candidate = candidates[index];
                predictions.Add(new LabelScore(candidate.Index, context.Profile.GetLabel(candidate.Index), candidate.Score));
            }
            return new ClassificationResult(predictions);
        }

        private static float[] Softmax(float[] values, int offset, int count)
        {
            float maximum = values[offset];
            for (int index = 1; index < count; index++) if (values[offset + index] > maximum) maximum = values[offset + index];
            double sum = 0;
            for (int index = 0; index < count; index++) sum += Math.Exp(values[offset + index] - maximum);
            if (double.IsNaN(sum) || double.IsInfinity(sum) || sum <= 0) throw new VisualException(VisualErrorCodes.DecodeFailed, "Softmax normalization is not finite.");
            var result = new float[count];
            // Recompute the exponentials into the single retained Float32 result
            // buffer. This removes the temporary Double[] while preserving the
            // stable max-subtraction and accumulation order.
            for (int index = 0; index < result.Length; index++) result[index] = (float)(Math.Exp(values[offset + index] - maximum) / sum);
            return result;
        }

        private static float[] ValidateProbabilities(float[] values, int offset, int count, string profileId, string tensorName)
        {
            var result = new float[count];
            for (int index = 0; index < count; index++)
            {
                float value = values[offset + index];
                if (value < 0 || value > 1) throw new VisualException(VisualErrorCodes.DecodeFailed, "Classification probability must be in [0,1].", profileId: profileId, tensorName: tensorName, technicalDetails: "index=" + (offset + index));
                result[index] = value;
            }
            return result;
        }

        private VisualException InvalidShape(VisualDecodeContext context, ITensor tensor, string message) => new VisualException(VisualErrorCodes.TensorInvalid, message, profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());

        private sealed class ClassificationCandidate
        {
            public ClassificationCandidate(int index, float score) { Index = index; Score = score; }
            public int Index { get; }
            public float Score { get; }
        }
    }
}
