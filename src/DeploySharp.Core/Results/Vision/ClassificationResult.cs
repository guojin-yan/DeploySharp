using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>
    /// Contains ordered classification predictions. / 包含有序的分类预测结果。
    /// </summary>
    public sealed class ClassificationResult
    {
        private readonly IReadOnlyList<LabelScore> _predictions;

        /// <summary>Initializes classification predictions in descending application-defined order. / 按应用定义的降序初始化分类预测结果。</summary>
        public ClassificationResult(IEnumerable<LabelScore> predictions)
        {
            if (predictions == null) throw new ArgumentNullException(nameof(predictions));
            var values = new List<LabelScore>();
            foreach (LabelScore prediction in predictions)
            {
                if (prediction == null)
                {
                    throw new ArgumentException("Predictions cannot contain null values.", nameof(predictions));
                }

                values.Add(prediction);
            }

            _predictions = values.AsReadOnly();
        }

        /// <summary>Gets ordered classification predictions. / 获取有序的分类预测结果。</summary>
        public IReadOnlyList<LabelScore> Predictions => _predictions;

        /// <summary>Gets the first prediction or <see langword="null"/> when the result is empty. / 获取第一项预测；结果为空时返回 <see langword="null"/>。</summary>
        public LabelScore? TopPrediction => _predictions.Count == 0 ? null : _predictions[0];
    }
}
