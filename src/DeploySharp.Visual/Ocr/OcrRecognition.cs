using System;
using System.Collections.Generic;
using System.Text;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    internal interface ISequenceArgMaxVisualDecoder
    {
        public SequenceArgMaxRequest CreateSequenceArgMaxRequest();
        public object DecodeSequenceArgMax(SequenceArgMaxResult result, PreparedVisualInput input, VisualModelProfile profile, System.Threading.CancellationToken cancellationToken);
    }

    /// <summary>Identifies CTC output dimension order. / 标识 CTC 输出维度顺序。</summary>
    public enum CtcTensorLayout
    {
        /// <summary>Batch, time, classes. / 批次、时间、类别。</summary>
        BatchTimeClasses = 0,
        /// <summary>Time, batch, classes. / 时间、批次、类别。</summary>
        TimeBatchClasses = 1
    }

    /// <summary>Identifies handling of an explicitly reserved unknown class. / 标识显式保留未知类别的处理。</summary>
    public enum CtcUnknownTokenBehavior
    {
        /// <summary>Reject the sequence. / 拒绝序列。</summary>
        Throw = 0,
        /// <summary>Skip the unknown token while retaining its trace. / 跳过未知 token，同时保留追踪。</summary>
        Skip = 1,
        /// <summary>Emit the configured replacement scalar. / 发射配置的替换标量。</summary>
        Replace = 2
    }

    /// <summary>Identifies aggregate confidence for emitted CTC tokens. / 标识已发射 CTC token 的聚合置信度。</summary>
    public enum CtcConfidenceAggregation
    {
        /// <summary>Arithmetic mean. / 算术平均值。</summary>
        Mean = 0,
        /// <summary>Minimum emitted-token probability. / 已发射 token 的最小概率。</summary>
        Minimum = 1,
        /// <summary>Geometric mean. / 几何平均值。</summary>
        GeometricMean = 2
    }

    /// <summary>Defines one strict CTC logits or probability output. / 定义一个严格的 CTC logits 或概率输出。</summary>
    public sealed class CtcOutputSchema
    {
        /// <summary>Initializes a CTC output schema. / 初始化 CTC 输出 Schema。</summary>
        public CtcOutputSchema(string outputName, CtcTensorLayout layout)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("A CTC output name is required.", nameof(outputName));
            if (!Enum.IsDefined(typeof(CtcTensorLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            OutputName = outputName;
            Layout = layout;
        }

        /// <summary>Gets output name. / 获取输出名称。</summary>
        public string OutputName { get; }
        /// <summary>Gets dimension order. / 获取维度顺序。</summary>
        public CtcTensorLayout Layout { get; }
    }

    /// <summary>Controls deterministic greedy CTC decoding and work bounds. / 控制确定性贪心 CTC 解码和工作边界。</summary>
    public sealed class CtcDecoderOptions
    {
        /// <summary>Initializes greedy CTC options. / 初始化贪心 CTC 选项。</summary>
        public CtcDecoderOptions(int blankIndex, bool applySoftmax = true, bool collapseRepeats = true, bool removeBlank = true, string? blankText = null, int? unknownClassIndex = null, CtcUnknownTokenBehavior unknownBehavior = CtcUnknownTokenBehavior.Throw, string unknownReplacement = "\uFFFD", CtcConfidenceAggregation confidenceAggregation = CtcConfidenceAggregation.Mean, int maximumBatch = 64, int maximumSequenceLength = 4096, int maximumCharacters = 4096, long maximumWorkspaceBytes = 64L * 1024L * 1024L)
        {
            if (blankIndex < 0) throw new ArgumentOutOfRangeException(nameof(blankIndex));
            if (unknownClassIndex.HasValue && unknownClassIndex.Value < 0) throw new ArgumentOutOfRangeException(nameof(unknownClassIndex));
            if (unknownClassIndex == blankIndex) throw new ArgumentException("Blank and unknown class indexes must differ.", nameof(unknownClassIndex));
            if (!Enum.IsDefined(typeof(CtcUnknownTokenBehavior), unknownBehavior)) throw new ArgumentOutOfRangeException(nameof(unknownBehavior));
            if (!Enum.IsDefined(typeof(CtcConfidenceAggregation), confidenceAggregation)) throw new ArgumentOutOfRangeException(nameof(confidenceAggregation));
            if (!removeBlank && string.IsNullOrEmpty(blankText)) throw new ArgumentException("Retaining blank requires explicit blank text.", nameof(blankText));
            if (unknownBehavior == CtcUnknownTokenBehavior.Replace && !IsSingleScalar(unknownReplacement)) throw new ArgumentException("Unknown replacement must contain exactly one Unicode scalar.", nameof(unknownReplacement));
            if (maximumBatch <= 0 || maximumSequenceLength <= 0 || maximumCharacters <= 0 || maximumWorkspaceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            BlankIndex = blankIndex;
            ApplySoftmax = applySoftmax;
            CollapseRepeats = collapseRepeats;
            RemoveBlank = removeBlank;
            BlankText = blankText;
            UnknownClassIndex = unknownClassIndex;
            UnknownBehavior = unknownBehavior;
            UnknownReplacement = unknownReplacement;
            ConfidenceAggregation = confidenceAggregation;
            MaximumBatch = maximumBatch;
            MaximumSequenceLength = maximumSequenceLength;
            MaximumCharacters = maximumCharacters;
            MaximumWorkspaceBytes = maximumWorkspaceBytes;
        }

        /// <summary>Gets explicit blank class index. / 获取显式 blank 类别索引。</summary>
        public int BlankIndex { get; }
        /// <summary>Gets whether stable softmax is applied. / 获取是否应用稳定 softmax。</summary>
        public bool ApplySoftmax { get; }
        /// <summary>Gets whether adjacent equal non-blank classes collapse. / 获取是否折叠相邻相同非 blank 类别。</summary>
        public bool CollapseRepeats { get; }
        /// <summary>Gets whether blank is omitted from text. / 获取是否从文本中移除 blank。</summary>
        public bool RemoveBlank { get; }
        /// <summary>Gets explicit text emitted for retained blank. / 获取保留 blank 时发射的显式文本。</summary>
        public string? BlankText { get; }
        /// <summary>Gets optional explicit unknown class index. / 获取可选显式未知类别索引。</summary>
        public int? UnknownClassIndex { get; }
        /// <summary>Gets unknown handling. / 获取未知类别处理。</summary>
        public CtcUnknownTokenBehavior UnknownBehavior { get; }
        /// <summary>Gets unknown replacement scalar. / 获取未知类别替换标量。</summary>
        public string UnknownReplacement { get; }
        /// <summary>Gets confidence aggregation. / 获取置信度聚合方式。</summary>
        public CtcConfidenceAggregation ConfidenceAggregation { get; }
        /// <summary>Gets maximum batch. / 获取最大批次。</summary>
        public int MaximumBatch { get; }
        /// <summary>Gets maximum time dimension. / 获取最大时间维度。</summary>
        public int MaximumSequenceLength { get; }
        /// <summary>Gets maximum emitted characters. / 获取最大发射字符数。</summary>
        public int MaximumCharacters { get; }
        /// <summary>Gets maximum conversion and softmax workspace. / 获取最大转换和 softmax 工作区。</summary>
        public long MaximumWorkspaceBytes { get; }

        private static bool IsSingleScalar(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value.Length == 1) return !char.IsSurrogate(value[0]);
            return value.Length == 2 && char.IsSurrogatePair(value[0], value[1]);
        }
    }

    /// <summary>Decodes strict named CTC tensors using deterministic greedy selection. / 使用确定性贪心选择解码严格命名的 CTC 张量。</summary>
    public sealed class GreedyCtcDecoder : IVisualDecoder, ISequenceArgMaxVisualDecoder
    {
        /// <summary>Initializes a greedy CTC decoder. / 初始化贪心 CTC 解码器。</summary>
        public GreedyCtcDecoder(CtcOutputSchema schema, OcrCharacterSet characterSet, CtcDecoderOptions options)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            CharacterSet = characterSet ?? throw new ArgumentNullException(nameof(characterSet));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            int expectedClasses = checked(characterSet.Count + 1 + (options.UnknownClassIndex.HasValue ? 1 : 0));
            if (options.BlankIndex >= expectedClasses) throw new ArgumentOutOfRangeException(nameof(options), "Blank index exceeds the declared class mapping.");
            if (options.UnknownClassIndex.HasValue && options.UnknownClassIndex.Value >= expectedClasses) throw new ArgumentOutOfRangeException(nameof(options), "Unknown index exceeds the declared class mapping.");
            ExpectedClassCount = expectedClasses;
        }

        /// <summary>Gets text-recognition task. / 获取文本识别任务。</summary>
        public VisualTaskId Task => VisualTaskId.TextRecognition;
        /// <summary>Gets strict output schema. / 获取严格输出 Schema。</summary>
        public CtcOutputSchema Schema { get; }
        /// <summary>Gets immutable character set. / 获取不可变字符表。</summary>
        public OcrCharacterSet CharacterSet { get; }
        /// <summary>Gets deterministic options. / 获取确定性选项。</summary>
        public CtcDecoderOptions Options { get; }
        /// <summary>Gets exact required class count, including blank and optional unknown. / 获取包括 blank 和可选 unknown 的精确所需类别数。</summary>
        public int ExpectedClassCount { get; }

        /// <summary>Decodes Float32/Float64 [B,T,C] or [T,B,C] tensors with lowest-index tie breaking. / 使用最小索引同分决策解码 Float32/Float64 [B,T,C] 或 [T,B,C] 张量。</summary>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Outputs.Count != 1) throw Failure(context, VisualErrorCodes.TensorInvalid, "CTC recognition requires exactly one output.");
            ITensor tensor = Required(context, Schema.OutputName);
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 3) throw Failure(context, VisualErrorCodes.TensorInvalid, "CTC output must have rank three.", Schema.OutputName, shape.ToString());
            int batch = checked((int)(Schema.Layout == CtcTensorLayout.BatchTimeClasses ? shape[0] : shape[1]));
            int time = checked((int)(Schema.Layout == CtcTensorLayout.BatchTimeClasses ? shape[1] : shape[0]));
            int classes = checked((int)shape[2]);
            if (batch != context.Input.BatchSize) throw Failure(context, VisualErrorCodes.TensorInvalid, "CTC output batch does not match prepared input batch.", Schema.OutputName, "output=" + batch + ";input=" + context.Input.BatchSize);
            if (batch <= 0 || batch > Options.MaximumBatch) throw Failure(context, VisualErrorCodes.DecodeFailed, "CTC batch exceeds its configured bound.", Schema.OutputName, "batch=" + batch);
            if (time <= 0 || time > Options.MaximumSequenceLength) throw Failure(context, VisualErrorCodes.DecodeFailed, "CTC sequence length exceeds its configured bound.", Schema.OutputName, "time=" + time);
            if (classes != ExpectedClassCount) throw Failure(context, VisualErrorCodes.TensorInvalid, "CTC class dimension does not match character set plus reserved classes.", Schema.OutputName, "classes=" + classes + ";expected=" + ExpectedClassCount);
            if (tensor.Length != checked((long)batch * time * classes)) throw Failure(context, VisualErrorCodes.TensorInvalid, "CTC tensor element count is inconsistent with shape.", Schema.OutputName, shape.ToString());
            long workspace = tensor.ElementType == TensorElementType.Float64 ? checked(tensor.Length * sizeof(float)) : 0;
            if (Options.ApplySoftmax) workspace = checked(workspace + checked((long)classes * sizeof(double)));
            if (workspace > Options.MaximumWorkspaceBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "CTC workspace exceeds its configured bound.", Schema.OutputName, "workspaceBytes=" + workspace);
            float[] values = Options.ApplySoftmax
                ? VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Schema.OutputName)
                : VisualTensorReader.ReadScoresForFusedValidation(tensor, context.Profile.ProfileId, Schema.OutputName);
            var results = new List<RecognizedText>(batch);
            double[] probabilities = Options.ApplySoftmax ? new double[classes] : Array.Empty<double>();
            for (int batchIndex = 0; batchIndex < batch; batchIndex++) results.Add(DecodeSequence(values, batchIndex, batch, time, classes, probabilities, context));
            return TextRecognitionBatchResult.CreateDecoded(results);
        }

        SequenceArgMaxRequest ISequenceArgMaxVisualDecoder.CreateSequenceArgMaxRequest()
        {
            SequenceTensorLayout layout = Schema.Layout == CtcTensorLayout.BatchTimeClasses
                ? SequenceTensorLayout.BatchTimeClasses
                : SequenceTensorLayout.TimeBatchClasses;
            return new SequenceArgMaxRequest(
                Schema.OutputName,
                layout,
                ExpectedClassCount,
                Options.ApplySoftmax,
                requireUnitInterval: !Options.ApplySoftmax,
                Options.MaximumBatch,
                Options.MaximumSequenceLength);
        }

        object ISequenceArgMaxVisualDecoder.DecodeSequenceArgMax(SequenceArgMaxResult result, PreparedVisualInput input, VisualModelProfile profile, System.Threading.CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Batch != input.BatchSize) throw ReducedFailure(profile, VisualErrorCodes.TensorInvalid, "CTC output batch does not match prepared input batch.", "output=" + result.Batch + ";input=" + input.BatchSize);
            if (result.Batch > Options.MaximumBatch) throw ReducedFailure(profile, VisualErrorCodes.DecodeFailed, "CTC batch exceeds its configured bound.", "batch=" + result.Batch);
            if (result.Time > Options.MaximumSequenceLength) throw ReducedFailure(profile, VisualErrorCodes.DecodeFailed, "CTC sequence length exceeds its configured bound.", "time=" + result.Time);
            if (result.Classes != ExpectedClassCount) throw ReducedFailure(profile, VisualErrorCodes.TensorInvalid, "CTC class dimension does not match character set plus reserved classes.", "classes=" + result.Classes + ";expected=" + ExpectedClassCount);
            var results = new List<RecognizedText>(result.Batch);
            for (int batchIndex = 0; batchIndex < result.Batch; batchIndex++)
            {
                int invalidOffset = result.GetInvalidOffset(batchIndex);
                if (invalidOffset >= 0)
                {
                    int timestep = invalidOffset / result.Classes;
                    int classIndex = invalidOffset % result.Classes;
                    string message = Options.ApplySoftmax
                        ? "CTC logits must be finite."
                        : "CTC probabilities must be finite and in [0,1] when softmax is disabled.";
                    throw ReducedFailure(profile, VisualErrorCodes.DecodeFailed, message, "batch=" + batchIndex + ";time=" + timestep + ";class=" + classIndex);
                }
                results.Add(DecodeReducedSequence(result, batchIndex, profile, cancellationToken));
            }
            return TextRecognitionBatchResult.CreateDecoded(results);
        }

        private RecognizedText DecodeReducedSequence(SequenceArgMaxResult result, int batchIndex, VisualModelProfile profile, System.Threading.CancellationToken cancellationToken)
        {
            var tokens = new List<OcrToken>(result.Time);
            var text = new StringBuilder(Math.Min(result.Time, Options.MaximumCharacters));
            int previousClass = -1;
            int emittedCount = 0;
            double confidenceSum = 0;
            double confidenceLogSum = 0;
            float minimum = 1;
            for (int timestep = 0; timestep < result.Time; timestep++)
            {
                if ((timestep & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                int selected = result.GetClassIndex(batchIndex, timestep);
                float confidence = result.GetConfidence(batchIndex, timestep);
                if (selected < 0 || selected >= result.Classes) throw ReducedFailure(profile, VisualErrorCodes.DecodeFailed, "Backend sequence argmax returned an invalid class index.", "batch=" + batchIndex + ";time=" + timestep + ";class=" + selected);
                if (!(confidence >= 0 && confidence <= 1)) throw ReducedFailure(profile, VisualErrorCodes.DecodeFailed, "Backend sequence argmax returned an invalid confidence.", "batch=" + batchIndex + ";time=" + timestep + ";confidence=" + confidence.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                bool blank = selected == Options.BlankIndex;
                bool unknown = Options.UnknownClassIndex.HasValue && selected == Options.UnknownClassIndex.Value;
                bool repeated = Options.CollapseRepeats && !blank && selected == previousClass;
                bool emitted = false;
                string? tokenText = null;
                if (blank)
                {
                    if (!Options.RemoveBlank) { tokenText = Options.BlankText; emitted = true; }
                }
                else if (!repeated && unknown)
                {
                    if (Options.UnknownBehavior == CtcUnknownTokenBehavior.Throw) throw ReducedFailure(profile, VisualErrorCodes.DecodeFailed, "CTC selected the explicitly reserved unknown class.", "batch=" + batchIndex + ";time=" + timestep);
                    if (Options.UnknownBehavior == CtcUnknownTokenBehavior.Replace) { tokenText = Options.UnknownReplacement; emitted = true; }
                }
                else if (!repeated)
                {
                    tokenText = CharacterSet.GetCharacter(CharacterIndex(selected));
                    emitted = true;
                }

                if (emitted)
                {
                    emittedCount++;
                    if (emittedCount > Options.MaximumCharacters) throw ReducedFailure(profile, VisualErrorCodes.DecodeFailed, "CTC emitted character count exceeds its configured bound.", "batch=" + batchIndex);
                    text.Append(tokenText);
                    confidenceSum += confidence;
                    confidenceLogSum += Math.Log(Math.Max(confidence, 1e-30f));
                    minimum = Math.Min(minimum, confidence);
                }
                tokens.Add(new OcrToken(timestep, selected, confidence, tokenText, blank, repeated, unknown, emitted));
                previousClass = selected;
            }
            float aggregate = emittedCount == 0 ? 0 : Options.ConfidenceAggregation == CtcConfidenceAggregation.Minimum
                ? minimum
                : Options.ConfidenceAggregation == CtcConfidenceAggregation.GeometricMean
                    ? checked((float)Math.Exp(confidenceLogSum / emittedCount))
                    : checked((float)(confidenceSum / emittedCount));
            return RecognizedText.CreateDecoded(batchIndex, text.ToString(), aggregate, tokens, CharacterSet.Id, CharacterSet.Version, CharacterSet.Sha256);
        }

        private RecognizedText DecodeSequence(float[] values, int batchIndex, int batch, int time, int classes, double[] probabilities, VisualDecodeContext context)
        {
            var tokens = new List<OcrToken>(time);
            var text = new StringBuilder(Math.Min(time, Options.MaximumCharacters));
            int previousClass = -1;
            int emittedCount = 0;
            double confidenceSum = 0;
            double confidenceLogSum = 0;
            float minimum = 1;
            for (int timestep = 0; timestep < time; timestep++)
            {
                if ((timestep & 31) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                int rowOffset = Schema.Layout == CtcTensorLayout.BatchTimeClasses
                    ? checked(((batchIndex * time + timestep) * classes))
                    : checked(((timestep * batch + batchIndex) * classes));
                int selected = 0;
                float selectedRaw = values[rowOffset];
                if (!Options.ApplySoftmax) ValidateProbability(selectedRaw, batchIndex, timestep, 0, context);
                for (int classIndex = 1; classIndex < classes; classIndex++)
                {
                    float candidate = values[rowOffset + classIndex];
                    if (!Options.ApplySoftmax) ValidateProbability(candidate, batchIndex, timestep, classIndex, context);
                    if (candidate > selectedRaw) { selectedRaw = candidate; selected = classIndex; }
                }
                float confidence = Options.ApplySoftmax ? SoftmaxConfidence(values, rowOffset, selected, classes, probabilities) : selectedRaw;
                bool blank = selected == Options.BlankIndex;
                bool unknown = Options.UnknownClassIndex.HasValue && selected == Options.UnknownClassIndex.Value;
                bool repeated = Options.CollapseRepeats && !blank && selected == previousClass;
                bool emitted = false;
                string? tokenText = null;
                if (blank)
                {
                    if (!Options.RemoveBlank) { tokenText = Options.BlankText; emitted = true; }
                }
                else if (!repeated && unknown)
                {
                    if (Options.UnknownBehavior == CtcUnknownTokenBehavior.Throw) throw Failure(context, VisualErrorCodes.DecodeFailed, "CTC selected the explicitly reserved unknown class.", Schema.OutputName, "batch=" + batchIndex + ";time=" + timestep);
                    if (Options.UnknownBehavior == CtcUnknownTokenBehavior.Replace) { tokenText = Options.UnknownReplacement; emitted = true; }
                }
                else if (!repeated)
                {
                    tokenText = CharacterSet.GetCharacter(CharacterIndex(selected));
                    emitted = true;
                }

                if (emitted)
                {
                    emittedCount++;
                    if (emittedCount > Options.MaximumCharacters) throw Failure(context, VisualErrorCodes.DecodeFailed, "CTC emitted character count exceeds its configured bound.", Schema.OutputName, "batch=" + batchIndex);
                    text.Append(tokenText);
                    confidenceSum += confidence;
                    confidenceLogSum += Math.Log(Math.Max(confidence, 1e-30f));
                    minimum = Math.Min(minimum, confidence);
                }
                tokens.Add(new OcrToken(timestep, selected, confidence, tokenText, blank, repeated, unknown, emitted));
                previousClass = selected;
            }
            float aggregate = emittedCount == 0 ? 0 : Options.ConfidenceAggregation == CtcConfidenceAggregation.Minimum
                ? minimum
                : Options.ConfidenceAggregation == CtcConfidenceAggregation.GeometricMean
                    ? checked((float)Math.Exp(confidenceLogSum / emittedCount))
                    : checked((float)(confidenceSum / emittedCount));
            return RecognizedText.CreateDecoded(batchIndex, text.ToString(), aggregate, tokens, CharacterSet.Id, CharacterSet.Version, CharacterSet.Sha256);
        }

        private float SoftmaxConfidence(float[] values, int rowOffset, int selected, int classes, double[] probabilities)
        {
            double maximum = values[rowOffset];
            for (int classIndex = 1; classIndex < classes; classIndex++) maximum = Math.Max(maximum, values[rowOffset + classIndex]);
            double sum = 0;
            for (int classIndex = 0; classIndex < classes; classIndex++) { double value = Math.Exp(values[rowOffset + classIndex] - maximum); probabilities[classIndex] = value; sum += value; }
            return checked((float)(probabilities[selected] / sum));
        }

        private void ValidateProbability(float value, int batchIndex, int timestep, int classIndex, VisualDecodeContext context)
        {
            if (!(value >= 0 && value <= 1)) throw Failure(context, VisualErrorCodes.DecodeFailed, "CTC probabilities must be finite and in [0,1] when softmax is disabled.", Schema.OutputName, "batch=" + batchIndex + ";time=" + timestep + ";class=" + classIndex);
        }

        private int CharacterIndex(int classIndex)
        {
            int index = classIndex;
            if (Options.BlankIndex < classIndex) index--;
            if (Options.UnknownClassIndex.HasValue && Options.UnknownClassIndex.Value < classIndex) index--;
            if (index < 0 || index >= CharacterSet.Count) throw new InvalidOperationException("The validated CTC class map is inconsistent.");
            return index;
        }

        private static ITensor Required(VisualDecodeContext context, string name) { try { return context.Outputs.GetRequired(name); } catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "The required CTC output is missing.", name, null, exception); } }
        private static VisualException Failure(VisualDecodeContext context, string code, string message, string? tensorName = null, string? details = null, Exception? exception = null) => new VisualException(code, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: details);
        private VisualException ReducedFailure(VisualModelProfile profile, string code, string message, string? details = null) => new VisualException(code, message, profileId: profile.ProfileId, tensorName: Schema.OutputName, modelId: profile.ModelId, technicalDetails: details);
    }
}
