using System;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Models
{
    /// <summary>
    /// Identifies a model definition independently from its file artifacts. / 独立于模型文件工件标识模型定义。
    /// </summary>
    public readonly struct ModelId : IEquatable<ModelId>
    {
        private readonly string? _value;

        /// <summary>
        /// Initializes a model identifier. / 初始化模型标识符。
        /// </summary>
        /// <param name="value">A stable lowercase path-like identifier. / 稳定的小写路径式标识符。</param>
        public ModelId(string value)
        {
            _value = Guard.Identifier(value, nameof(value));
        }

        /// <summary>
        /// Gets the normalized identifier value. / 获取规范化的标识符值。
        /// </summary>
        public string Value => _value ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether this is the default, empty identifier. / 获取一个值，指示当前值是否为默认的空标识符。
        /// </summary>
        public bool IsEmpty => string.IsNullOrEmpty(_value);

        /// <inheritdoc />
        /// <remarks>Uses ordinal identifier equality. / 使用序号标识符相等性。</remarks>
        public bool Equals(ModelId other)
        {
            return StringComparer.Ordinal.Equals(Value, other.Value);
        }

        /// <inheritdoc />
        /// <remarks>Compares with another object by identifier value. / 按标识符值与另一个对象比较。</remarks>
        public override bool Equals(object? obj)
        {
            return obj is ModelId other && Equals(other);
        }

        /// <inheritdoc />
        /// <remarks>Computes an ordinal hash code. / 计算序号哈希码。</remarks>
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <inheritdoc />
        /// <remarks>Returns the normalized identifier. / 返回规范化标识符。</remarks>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Compares two model identifiers for equality. / 比较两个模型标识符是否相等。
        /// </summary>
        public static bool operator ==(ModelId left, ModelId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two model identifiers for inequality. / 比较两个模型标识符是否不相等。
        /// </summary>
        public static bool operator !=(ModelId left, ModelId right)
        {
            return !left.Equals(right);
        }
    }
}
