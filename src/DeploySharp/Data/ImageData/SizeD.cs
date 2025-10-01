using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents a size with double-precision floating-point dimensions
    /// 表示具有双精度浮点尺寸的大小结构体
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used for representing dimensions of rectangular areas with high precision.
    /// Suitable for calculations requiring decimal precision and gradual transformations.
    /// </para>
    /// <para>
    /// 用于表示需要高精度的矩形区域尺寸。
    /// 适合需要小数精度和渐变变换的计算。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var preciseSize = new SizeD(1920.5678, 1080.1234);
    /// Size intSize = preciseSize.ToSize(); // Explicit conversion
    /// SizeD fromFloat = new SizeF(100.5f, 200.5f); // Implicit conversion
    /// </code>
    /// </example>
    /// </remarks>
    public record struct SizeD(double Width, double Height)
    {
        /// <summary>
        /// Gets or sets the horizontal dimension
        /// 获取或设置水平尺寸
        /// </summary>
        /// <value>The width component as double-precision value</value>
        public double Width = Width;

        /// <summary>
        /// Gets or sets the vertical dimension
        /// 获取或设置垂直尺寸
        /// </summary>
        /// <value>The height component as double-precision value</value>
        public double Height = Height;

        /// <summary>
        /// Implicit conversion from integer-based Size
        /// 从整数Size的隐式转换
        /// </summary>
        /// <param name="size">Integer-based size structure 基于整数的大小结构</param>
        /// <returns>New SizeD with converted dimensions</returns>
        public static implicit operator SizeD(Size size)
            => new(size.Width, size.Height);

        /// <summary>
        /// Implicit conversion from single-precision SizeF
        /// 从单精度SizeF的隐式转换
        /// </summary>
        /// <param name="size">Single-precision floating-point size 单精度浮点大小</param>
        /// <returns>New SizeD with converted dimensions</returns>
        public static implicit operator SizeD(SizeF size)
            => new(size.Width, size.Height);

        /// <summary>
        /// Converts to integer-based Size (truncates decimal portion)
        /// 转换为整数Size（截断小数部分）
        /// </summary>
        /// <returns>Size structure with truncated dimensions</returns>
        /// <exception cref="OverflowException">
        /// Thrown if dimensions exceed integer range
        /// 当尺寸超过整数范围时抛出
        /// </exception>
        public readonly Size ToSize() => new(Width, Height);

        /// <summary>
        /// Converts to single-precision SizeF (may lose precision)
        /// 转换为单精度SizeF（可能丢失精度）
        /// </summary>
        /// <returns>SizeF structure with converted dimensions</returns>
        public readonly SizeF ToSizeF() => new((float)Width, (float)Height);
        /// <summary>
        /// Returns a string representation of the size
        /// 返回大小的字符串表示形式
        /// </summary>
        /// <returns>Formatted string showing width and height</returns>
        public override string ToString()
        {
            return $"(Width: {Width:F2}, Height: {Height:F2})";
        }
    }

}
