using System;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Results
{
    /// <summary>
    /// Associates a class index and label with a confidence score. / 将类别索引和标签与置信度分数关联。
    /// </summary>
    public sealed class LabelScore
    {
        /// <summary>Initializes a label score. / 初始化标签分数。</summary>
        public LabelScore(int index, string label, float score)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (float.IsNaN(score) || float.IsInfinity(score))
            {
                throw new ArgumentOutOfRangeException(nameof(score));
            }

            Index = index;
            Label = Guard.NotNullOrWhiteSpace(label, nameof(label));
            Score = score;
        }

        /// <summary>Gets the zero-based class index. / 获取从零开始的类别索引。</summary>
        public int Index { get; }

        /// <summary>Gets the class label. / 获取类别标签。</summary>
        public string Label { get; }

        /// <summary>Gets the model score. / 获取模型分数。</summary>
        public float Score { get; }
    }
}
