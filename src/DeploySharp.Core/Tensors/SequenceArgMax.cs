using System;

namespace JYPPX.DeploySharp.Tensors
{
    /// <summary>Identifies the dimension order of a batched sequence tensor. / 标识批序列 Tensor 的维度顺序。</summary>
    public enum SequenceTensorLayout
    {
        /// <summary>Batch, time, classes. / 批次、时间、类别。</summary>
        BatchTimeClasses = 0,
        /// <summary>Time, batch, classes. / 时间、批次、类别。</summary>
        TimeBatchClasses = 1
    }

    /// <summary>Describes an optional backend-side sequence argmax reduction. / 描述可选的后端侧序列 argmax 归约。</summary>
    public sealed class SequenceArgMaxRequest
    {
        /// <summary>Initializes a bounded sequence argmax request. / 初始化有界序列 argmax 请求。</summary>
        public SequenceArgMaxRequest(
            string outputName,
            SequenceTensorLayout layout,
            int expectedClasses,
            bool applySoftmax,
            bool requireUnitInterval,
            int maximumBatch,
            int maximumTime)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output tensor name is required.", nameof(outputName));
            if (!Enum.IsDefined(typeof(SequenceTensorLayout), layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            if (expectedClasses <= 0) throw new ArgumentOutOfRangeException(nameof(expectedClasses));
            if (maximumBatch <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBatch));
            if (maximumTime <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTime));
            OutputName = outputName.Trim();
            Layout = layout;
            ExpectedClasses = expectedClasses;
            ApplySoftmax = applySoftmax;
            RequireUnitInterval = requireUnitInterval;
            MaximumBatch = maximumBatch;
            MaximumTime = maximumTime;
        }

        /// <summary>Gets the output tensor to reduce. / 获取要归约的输出 Tensor。</summary>
        public string OutputName { get; }
        /// <summary>Gets the output dimension order. / 获取输出维度顺序。</summary>
        public SequenceTensorLayout Layout { get; }
        /// <summary>Gets the exact expected class count. / 获取精确的预期类别数。</summary>
        public int ExpectedClasses { get; }
        /// <summary>Gets whether confidence requires a stable softmax. / 获取置信度是否需要稳定 softmax。</summary>
        public bool ApplySoftmax { get; }
        /// <summary>Gets whether every source value must be finite and within [0,1]. / 获取是否要求每个源值有限且位于 [0,1]。</summary>
        public bool RequireUnitInterval { get; }
        /// <summary>Gets the maximum accepted batch. / 获取允许的最大批次。</summary>
        public int MaximumBatch { get; }
        /// <summary>Gets the maximum accepted time dimension. / 获取允许的最大时间维度。</summary>
        public int MaximumTime { get; }
    }

    /// <summary>Owns compact class and confidence traces produced by a backend-side sequence reduction. / 拥有后端侧序列归约生成的紧凑类别与置信度轨迹。</summary>
    public sealed class SequenceArgMaxResult
    {
        private readonly int[] _classIndices;
        private readonly float[] _confidences;
        private readonly int[] _invalidOffsets;

        /// <summary>Initializes a validated, defensively copied sequence result. / 初始化经过验证且防御性复制的序列结果。</summary>
        public SequenceArgMaxResult(int batch, int time, int classes, int[] classIndices, float[] confidences, int[] invalidOffsets)
        {
            if (batch <= 0) throw new ArgumentOutOfRangeException(nameof(batch));
            if (time <= 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (classes <= 0) throw new ArgumentOutOfRangeException(nameof(classes));
            if (classIndices == null) throw new ArgumentNullException(nameof(classIndices));
            if (confidences == null) throw new ArgumentNullException(nameof(confidences));
            if (invalidOffsets == null) throw new ArgumentNullException(nameof(invalidOffsets));
            int traceLength = checked(batch * time);
            if (classIndices.Length != traceLength) throw new ArgumentException("Class trace length does not match batch and time.", nameof(classIndices));
            if (confidences.Length != traceLength) throw new ArgumentException("Confidence trace length does not match batch and time.", nameof(confidences));
            if (invalidOffsets.Length != batch) throw new ArgumentException("Invalid-offset trace length does not match batch.", nameof(invalidOffsets));
            Batch = batch;
            Time = time;
            Classes = classes;
            _classIndices = (int[])classIndices.Clone();
            _confidences = (float[])confidences.Clone();
            _invalidOffsets = (int[])invalidOffsets.Clone();
        }

        /// <summary>Gets the batch dimension. / 获取批次维度。</summary>
        public int Batch { get; }
        /// <summary>Gets the time dimension. / 获取时间维度。</summary>
        public int Time { get; }
        /// <summary>Gets the source class dimension. / 获取源类别维度。</summary>
        public int Classes { get; }

        /// <summary>Gets one selected class index. / 获取一个选中的类别索引。</summary>
        public int GetClassIndex(int batchIndex, int timestep) => _classIndices[Offset(batchIndex, timestep)];

        /// <summary>Gets one selected class confidence. / 获取一个选中类别的置信度。</summary>
        public float GetConfidence(int batchIndex, int timestep) => _confidences[Offset(batchIndex, timestep)];

        /// <summary>Gets the first invalid flattened source offset for a sequence, or -1 when all values passed validation. / 获取序列中首个无效源扁平偏移；全部通过验证时为 -1。</summary>
        public int GetInvalidOffset(int batchIndex)
        {
            if (batchIndex < 0 || batchIndex >= Batch) throw new ArgumentOutOfRangeException(nameof(batchIndex));
            return _invalidOffsets[batchIndex];
        }

        private int Offset(int batchIndex, int timestep)
        {
            if (batchIndex < 0 || batchIndex >= Batch) throw new ArgumentOutOfRangeException(nameof(batchIndex));
            if (timestep < 0 || timestep >= Time) throw new ArgumentOutOfRangeException(nameof(timestep));
            return checked(batchIndex * Time + timestep);
        }
    }

    /// <summary>Exposes an optional backend-side sequence argmax path that avoids materializing the full output on the host. / 暴露可选的后端侧序列 argmax 路径，避免在主机端物化完整输出。</summary>
    public interface ISequenceArgMaxInferenceSession
    {
        /// <summary>Gets whether this session can execute sequence argmax reductions. / 获取此 Session 是否可执行序列 argmax 归约。</summary>
        public bool IsSequenceArgMaxSupported { get; }

        /// <summary>Runs inference and returns only the compact sequence trace. / 运行推理并仅返回紧凑序列轨迹。</summary>
        public SequenceArgMaxResult RunSequenceArgMax(InferenceInputs inputs, SequenceArgMaxRequest request, System.Threading.CancellationToken cancellationToken);
    }
}
