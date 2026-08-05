using System;
using System.Threading;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines a stateless, reusable decoder for one visual task. / 定义一个视觉任务的无状态可复用解码器。</summary>
    public interface IVisualDecoder
    {
        /// <summary>Gets the task produced by this decoder. / 获取此解码器生成的任务。</summary>
        public VisualTaskId Task { get; }

        /// <summary>Decodes validated backend outputs into a canonical result object. / 将已验证后端输出解码为规范结果对象。</summary>
        public object Decode(VisualDecodeContext context);
    }

    /// <summary>Provides immutable input, profile, output, and cancellation state to a decoder. / 向解码器提供不可变输入、Profile、输出和取消状态。</summary>
    public sealed class VisualDecodeContext
    {
        /// <summary>Initializes a decoder context. / 初始化解码上下文。</summary>
        public VisualDecodeContext(PreparedVisualInput input, VisualModelProfile profile, InferenceOutputs outputs, CancellationToken cancellationToken)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
            CancellationToken = cancellationToken;
        }

        /// <summary>Gets the prepared visual input. / 获取已准备视觉输入。</summary>
        public PreparedVisualInput Input { get; }
        /// <summary>Gets the selected immutable model profile. / 获取选中的不可变模型 Profile。</summary>
        public VisualModelProfile Profile { get; }
        /// <summary>Gets named backend outputs. / 获取命名后端输出。</summary>
        public InferenceOutputs Outputs { get; }
        /// <summary>Gets the operation cancellation token. / 获取操作取消令牌。</summary>
        public CancellationToken CancellationToken { get; }
    }

    /// <summary>Wraps a decoded visual result with model, backend, timing, task, and correlation metadata. / 使用模型、后端、时长、任务和关联元数据包装解码后的视觉结果。</summary>
    public sealed class VisualInferenceResult
    {
        /// <summary>Initializes a Visual inference result. / 初始化 Visual 推理结果。</summary>
        public VisualInferenceResult(object value, VisualTaskId task, ModelId modelId, BackendId backendId, InferenceTiming timing, string? correlationId = null)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            if (task.IsEmpty) throw new ArgumentException("A visual task is required.", nameof(task));
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            if (backendId.IsEmpty) throw new ArgumentException("A backend identifier is required.", nameof(backendId));
            Task = task;
            ModelId = modelId;
            BackendId = backendId;
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
            CorrelationId = correlationId;
        }

        /// <summary>Gets the canonical decoded payload, such as a classification or detection result. / 获取规范解码载荷，例如分类或检测结果。</summary>
        public object Value { get; }
        /// <summary>Gets the visual task. / 获取视觉任务。</summary>
        public VisualTaskId Task { get; }
        /// <summary>Gets the model identifier. / 获取模型标识符。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the explicitly selected backend identifier. / 获取显式选择的后端标识符。</summary>
        public BackendId BackendId { get; }
        /// <summary>Gets measured inference and decoding durations. / 获取测得的推理和解码时长。</summary>
        public InferenceTiming Timing { get; }
        /// <summary>Gets the optional correlation identifier. / 获取可选关联标识符。</summary>
        public string? CorrelationId { get; }

        /// <summary>Returns the decoded payload as the requested canonical type. / 将解码载荷作为请求的规范类型返回。</summary>
        public T GetValue<T>() where T : class
        {
            T? result = Value as T;
            if (result == null) throw new InvalidCastException("The visual result payload is not of the requested type.");
            return result;
        }
    }

    internal static class VisualTensorReader
    {
        public static float[] ReadFiniteScores(ITensor tensor, string profileId, string tensorName)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            float[] values;
            if (tensor.ElementType == TensorElementType.Float32 && tensor.Buffer is float[] floats)
            {
                // Decoders borrow the managed output only for this synchronous call and never mutate or retain it. / 解码器仅在本次同步调用中借用托管输出，绝不修改或保留它。
                values = floats;
            }
            else if (tensor.ElementType == TensorElementType.Float64 && tensor.Buffer is double[] doubles)
            {
                values = new float[doubles.Length];
                for (int index = 0; index < doubles.Length; index++) values[index] = checked((float)doubles[index]);
            }
            else
            {
                throw new VisualException(VisualErrorCodes.TensorInvalid, "Decoder requires a Float32 or Float64 tensor.", profileId: profileId, tensorName: tensorName);
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw new VisualException(VisualErrorCodes.DecodeFailed, "Tensor contains NaN or infinity.", profileId: profileId, tensorName: tensorName, technicalDetails: "index=" + index);
            }

            return values;
        }
    }
}
