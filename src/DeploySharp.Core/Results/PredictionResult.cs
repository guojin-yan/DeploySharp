using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Results
{
    /// <summary>
    /// Wraps a canonical result payload with model, backend, timing, warning, and correlation metadata. / 使用模型、后端、时长、警告和关联元数据封装标准结果载荷。
    /// </summary>
    public sealed class PredictionResult<T>
    {
        private readonly IReadOnlyList<PredictionWarning> _warnings;

        /// <summary>Initializes a prediction result. / 初始化预测结果。</summary>
        public PredictionResult(
            T value,
            ModelId modelId,
            BackendId backendId,
            InferenceTiming? timing = null,
            IEnumerable<PredictionWarning>? warnings = null,
            string? correlationId = null)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            if (backendId.IsEmpty) throw new ArgumentException("A backend identifier is required.", nameof(backendId));
            Value = value;
            ModelId = modelId;
            BackendId = backendId;
            Timing = timing ?? InferenceTiming.Zero;
            CorrelationId = correlationId;

            var warningList = new List<PredictionWarning>();
            if (warnings != null)
            {
                foreach (PredictionWarning warning in warnings)
                {
                    if (warning == null)
                    {
                        throw new ArgumentException("Warnings cannot contain null values.", nameof(warnings));
                    }

                    warningList.Add(warning);
                }
            }

            _warnings = warningList.AsReadOnly();
        }

        /// <summary>Gets the canonical result payload. / 获取标准结果载荷。</summary>
        public T Value { get; }

        /// <summary>Gets the model identifier. / 获取模型标识符。</summary>
        public ModelId ModelId { get; }

        /// <summary>Gets the selected backend identifier. / 获取选定的后端标识符。</summary>
        public BackendId BackendId { get; }

        /// <summary>Gets measured phase durations. / 获取测量的阶段时长。</summary>
        public InferenceTiming Timing { get; }

        /// <summary>Gets non-fatal warnings. / 获取非致命警告。</summary>
        public IReadOnlyList<PredictionWarning> Warnings => _warnings;

        /// <summary>Gets the optional operation correlation identifier. / 获取可选的操作关联标识符。</summary>
        public string? CorrelationId { get; }
    }
}
