using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>Contains one ordered detection result for every input in a true model batch. / 包含真正模型 Batch 中每个输入的有序检测结果。</summary>
    public sealed class DetectionBatchResult
    {
        private readonly IReadOnlyList<DetectionResult> _results;

        /// <summary>Initializes a detection batch result and preserves input order. / 初始化检测 Batch 结果并保留输入顺序。</summary>
        public DetectionBatchResult(IEnumerable<DetectionResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var values = new List<DetectionResult>();
            foreach (DetectionResult result in results)
            {
                if (result == null) throw new ArgumentException("Detection results cannot contain null values.", nameof(results));
                values.Add(result);
            }
            if (values.Count == 0) throw new ArgumentException("A detection batch must contain at least one result.", nameof(results));
            _results = values.AsReadOnly();
        }

        /// <summary>Gets one detection result per batch row in input order. / 获取按输入顺序排列的每个 Batch 行检测结果。</summary>
        public IReadOnlyList<DetectionResult> Results => _results;
        /// <summary>Gets the number of decoded batch rows. / 获取已解码 Batch 行数。</summary>
        public int Count => _results.Count;
        /// <summary>Gets one decoded row by zero-based batch index. / 按从零开始的 Batch 索引获取一行结果。</summary>
        public DetectionResult this[int index] => _results[index];
    }
}
