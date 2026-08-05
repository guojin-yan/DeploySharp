using System;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp
{
    /// <summary>
    /// Identifies an inference backend without coupling Core to a closed enumeration. / 在不让 Core 依赖封闭枚举的情况下标识推理后端。
    /// </summary>
    public readonly struct BackendId : IEquatable<BackendId>
    {
        private readonly string? _value;

        /// <summary>
        /// Initializes a backend identifier. / 初始化后端标识符。
        /// </summary>
        /// <param name="value">A stable lowercase identifier such as <c>openvino</c>. / 稳定的小写标识符，例如 <c>openvino</c>。</param>
        public BackendId(string value)
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
        public bool Equals(BackendId other)
        {
            return StringComparer.Ordinal.Equals(Value, other.Value);
        }

        /// <inheritdoc />
        /// <remarks>Compares with another object by identifier value. / 按标识符值与另一个对象比较。</remarks>
        public override bool Equals(object? obj)
        {
            return obj is BackendId other && Equals(other);
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
        /// Compares two backend identifiers for equality. / 比较两个后端标识符是否相等。
        /// </summary>
        public static bool operator ==(BackendId left, BackendId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two backend identifiers for inequality. / 比较两个后端标识符是否不相等。
        /// </summary>
        public static bool operator !=(BackendId left, BackendId right)
        {
            return !left.Equals(right);
        }
    }
}
