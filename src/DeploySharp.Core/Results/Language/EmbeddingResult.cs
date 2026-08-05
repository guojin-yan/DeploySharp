using System;

namespace JYPPX.DeploySharp.Results.Language
{
    /// <summary>
    /// Contains a copied embedding vector and its normalization state. / 包含嵌入向量副本及其归一化状态。
    /// </summary>
    public sealed class EmbeddingResult
    {
        private readonly float[] _values;

        /// <summary>Initializes an embedding result. / 初始化嵌入结果。</summary>
        public EmbeddingResult(float[] values, bool isNormalized)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length == 0) throw new ArgumentException("An embedding cannot be empty.", nameof(values));
            _values = (float[])values.Clone();
            IsNormalized = isNormalized;
        }

        /// <summary>Gets embedding dimensionality. / 获取嵌入维度。</summary>
        public int Dimensions => _values.Length;

        /// <summary>Gets whether the vector is declared to have unit length. / 获取向量是否声明为单位长度。</summary>
        public bool IsNormalized { get; }

        /// <summary>Returns a defensive copy of embedding values. / 返回嵌入值的防御性副本。</summary>
        public float[] ToArray()
        {
            return (float[])_values.Clone();
        }
    }
}
