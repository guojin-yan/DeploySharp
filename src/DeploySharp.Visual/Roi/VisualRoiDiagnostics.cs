using System;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Describes bounded work performed by one ROI execution. / 描述一次 ROI 执行实际完成的有界工作量。</summary>
    public sealed class RoiExecutionDiagnostics
    {
        /// <summary>Initializes execution diagnostics. / 初始化执行诊断。</summary>
        public RoiExecutionDiagnostics(
            RoiExecutionMode executionMode,
            int selectedRoiCount,
            int windowCount,
            int preparedInputCount,
            int inferenceCallCount,
            int succeededResultCount,
            int failedResultCount,
            long preparedPixelCount,
            TimeSpan elapsed,
            bool usedTrueBatch,
            string? correlationId = null)
        {
            if (!Enum.IsDefined(typeof(RoiExecutionMode), executionMode)) throw new ArgumentOutOfRangeException(nameof(executionMode));
            if (selectedRoiCount < 0) throw new ArgumentOutOfRangeException(nameof(selectedRoiCount));
            if (windowCount < 0) throw new ArgumentOutOfRangeException(nameof(windowCount));
            if (preparedInputCount < 0) throw new ArgumentOutOfRangeException(nameof(preparedInputCount));
            if (inferenceCallCount < 0) throw new ArgumentOutOfRangeException(nameof(inferenceCallCount));
            if (succeededResultCount < 0) throw new ArgumentOutOfRangeException(nameof(succeededResultCount));
            if (failedResultCount < 0) throw new ArgumentOutOfRangeException(nameof(failedResultCount));
            if (preparedPixelCount < 0) throw new ArgumentOutOfRangeException(nameof(preparedPixelCount));
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (usedTrueBatch && executionMode != RoiExecutionMode.CropAndInfer) throw new ArgumentException("True Batch diagnostics are only valid for CropAndInfer.", nameof(usedTrueBatch));
            int resultCapacity = executionMode == RoiExecutionMode.SlidingWindow ? windowCount : selectedRoiCount;
            if (succeededResultCount > resultCapacity || failedResultCount > resultCapacity - succeededResultCount) throw new ArgumentException("The diagnostic result counts exceed the selected ROI or window count.");
            ExecutionMode = executionMode;
            SelectedRoiCount = selectedRoiCount;
            WindowCount = windowCount;
            PreparedInputCount = preparedInputCount;
            InferenceCallCount = inferenceCallCount;
            SucceededResultCount = succeededResultCount;
            FailedResultCount = failedResultCount;
            PreparedPixelCount = preparedPixelCount;
            Elapsed = elapsed;
            UsedTrueBatch = usedTrueBatch;
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId;
        }

        /// <summary>Gets the execution mode. / 获取执行模式。</summary>
        public RoiExecutionMode ExecutionMode { get; }
        /// <summary>Gets the number of selected ROIs. / 获取选中的 ROI 数量。</summary>
        public int SelectedRoiCount { get; }
        /// <summary>Gets the number of planned sliding windows. / 获取规划的滑窗数量。</summary>
        public int WindowCount { get; }
        /// <summary>Gets the number of prepared input objects. / 获取已准备输入对象数量。</summary>
        public int PreparedInputCount { get; }
        /// <summary>Gets the number of backend pipeline calls. / 获取后端 Pipeline 调用次数。</summary>
        public int InferenceCallCount { get; }
        /// <summary>Gets the number of successful decoded results. / 获取成功解码结果数量。</summary>
        public int SucceededResultCount { get; }
        /// <summary>Gets the number of per-ROI or per-window failures. / 获取逐 ROI 或逐窗口失败数量。</summary>
        public int FailedResultCount { get; }
        /// <summary>Gets the estimated source pixels prepared before inference. / 获取推理前准备的估算源像素数。</summary>
        public long PreparedPixelCount { get; }
        /// <summary>Gets elapsed wall-clock time for the ROI operation. / 获取 ROI 操作的墙钟耗时。</summary>
        public TimeSpan Elapsed { get; }
        /// <summary>Gets whether one model call contained multiple ROI rows. / 获取是否一次模型调用包含多个 ROI 行。</summary>
        public bool UsedTrueBatch { get; }
        /// <summary>Gets the optional correlation identifier. / 获取可选关联标识。</summary>
        public string? CorrelationId { get; }
    }
}
