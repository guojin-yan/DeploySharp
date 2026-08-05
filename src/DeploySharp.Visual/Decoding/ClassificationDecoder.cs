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

    /// <summary>Decodes one-batch classification scores into a canonical Core classification result. / 将单批次分类分数解码为 Core 规范分类结果。</summary>
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
            if (context.Input.BatchSize != 1) throw new VisualException(VisualErrorCodes.DecodeFailed, "Classification decoder currently requires batch size one.", profileId: context.Profile.ProfileId, tensorName: OutputName);
            ITensor tensor;
            try { tensor = context.Outputs.GetRequired(OutputName); }
            catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "Classification output tensor is missing.", exception, context.Profile.ProfileId, OutputName, modelId: context.Profile.ModelId); }
            if (tensor.Shape.Rank != 1 && !(tensor.Shape.Rank == 2 && tensor.Shape[0] == 1)) throw new VisualException(VisualErrorCodes.TensorInvalid, "Classification output shape must be [classes] or [1,classes].", profileId: context.Profile.ProfileId, tensorName: OutputName, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());
            int classCount = checked((int)tensor.Shape[tensor.Shape.Rank - 1]);
            if (classCount <= 0 || tensor.Length != classCount) throw new VisualException(VisualErrorCodes.TensorInvalid, "Classification output contains no classes or an inconsistent element count.", profileId: context.Profile.ProfileId, tensorName: OutputName);
            if (context.Profile.Labels.Count > 0 && context.Profile.Labels.Any(label => label.Index >= classCount)) throw new VisualException(VisualErrorCodes.DecodeFailed, "A profile label index exceeds the classification tensor class count.", profileId: context.Profile.ProfileId, tensorName: OutputName);
            float[] raw = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, OutputName);
            float[] scores = ScoreMode == ClassificationScoreMode.Logits ? Softmax(raw) : ValidateProbabilities(raw, context.Profile.ProfileId, OutputName);
            var candidates = new List<ClassificationCandidate>(scores.Length);
            for (int index = 0; index < scores.Length; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (scores[index] >= Threshold) candidates.Add(new ClassificationCandidate(index, scores[index]));
            }

            IEnumerable<ClassificationCandidate> ordered = candidates.OrderByDescending(value => value.Score).ThenBy(value => value.Index).Take(Math.Min(TopK, candidates.Count));
            var predictions = new List<LabelScore>();
            foreach (ClassificationCandidate candidate in ordered) predictions.Add(new LabelScore(candidate.Index, context.Profile.GetLabel(candidate.Index), candidate.Score));
            return new ClassificationResult(predictions);
        }

        private static float[] Softmax(float[] values)
        {
            float maximum = values[0];
            for (int index = 1; index < values.Length; index++) if (values[index] > maximum) maximum = values[index];
            var exponentials = new double[values.Length];
            double sum = 0;
            for (int index = 0; index < values.Length; index++)
            {
                double exponential = Math.Exp(values[index] - maximum);
                exponentials[index] = exponential;
                sum += exponential;
            }
            if (double.IsNaN(sum) || double.IsInfinity(sum) || sum <= 0) throw new VisualException(VisualErrorCodes.DecodeFailed, "Softmax normalization is not finite.");
            var result = new float[values.Length];
            for (int index = 0; index < result.Length; index++) result[index] = (float)(exponentials[index] / sum);
            return result;
        }

        private static float[] ValidateProbabilities(float[] values, string profileId, string tensorName)
        {
            for (int index = 0; index < values.Length; index++) if (values[index] < 0 || values[index] > 1) throw new VisualException(VisualErrorCodes.DecodeFailed, "Classification probability must be in [0,1].", profileId: profileId, tensorName: tensorName, technicalDetails: "index=" + index);
            return values;
        }

        private sealed class ClassificationCandidate
        {
            public ClassificationCandidate(int index, float score) { Index = index; Score = score; }
            public int Index { get; }
            public float Score { get; }
        }
    }
}
