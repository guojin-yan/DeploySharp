using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents a size with single-precision floating-point dimensions
    /// 表示具有单精度浮点尺寸的大小结构体
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used for representing dimensions of rectangular areas with floating-point precision.
    /// Suitable for graphics operations, UI layout, and other scenarios requiring decimal values.
    /// </para>
    /// <para>
    /// 用于表示需要浮点精度的矩形区域尺寸。
    /// 适用于图形操作、UI布局等需要小数值的场景。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var floatSize = new SizeF(100.5f, 200.75f);
    /// Size intSize = floatSize.ToSize(); // Explicit conversion (truncates)
    /// SizeF fromDouble = (SizeF)new SizeD(150.7, 250.3); // Explicit conversion
    /// </code>
    /// </example>
    /// </remarks>
    public record struct SizeF(float Width, float Height)
    {
        /// <summary>
        /// Gets or sets the horizontal dimension
        /// 获取或设置水平尺寸
        /// </summary>
        /// <value>The width component as single-precision value</value>
        public float Width = Width;

        /// <summary>
        /// Gets or sets the vertical dimension
        /// 获取或设置垂直尺寸
        /// </summary>
        /// <value>The height component as single-precision value</value>
        public float Height = Height;

        /// <summary>
        /// Initializes a new SizeF from double-precision values
        /// 从双精度值初始化新SizeF
        /// </summary>
        /// <param name="width">Width dimension (will be converted to float) 宽度尺寸（将转换为float）</param>
        /// <param name="height">Height dimension (will be converted to float) 高度尺寸（将转换为float）</param>
        /// <remarks>
        /// Values will be converted to single-precision floating-point (may lose precision).
        /// 值将被转换为单精度浮点（可能丢失精度）。
        /// </remarks>
        public SizeF(double width, double height)
            : this((float)width, (float)height)
        {
        }

        /// <summary>
        /// Implicit conversion from integer-based Size
        /// 从整数Size的隐式转换
        /// </summary>
        /// <param name="size">Integer-based size structure 基于整数的大小结构</param>
        /// <returns>New SizeF with converted dimensions</returns>
        public static implicit operator SizeF(Size size)
            => new(size.Width, size.Height);

        /// <summary>
        /// Explicit conversion from double-precision SizeD
        /// 从双精度SizeD的显式转换
        /// </summary>
        /// <param name="size">Double-precision floating-point size 双精度浮点大小</param>
        /// <returns>New SizeF with converted dimensions</returns>
        /// <remarks>
        /// Conversion may lose precision as values are narrowed to single-precision.
        /// 转换可能会丢失精度，因为值会被缩小为单精度。
        /// </remarks>
        public static explicit operator SizeF(SizeD size)
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
        /// Converts to double-precision SizeD
        /// 转换为双精度SizeD
        /// </summary>
        /// <returns>SizeD structure with converted dimensions</returns>
        /// <remarks>
        /// This conversion is lossless as float can be exactly represented in double.
        /// 此转换是无损的，因为float可以精确表示为double。
        /// </remarks>
        public readonly SizeD ToSizeD() => new(Width, Height);

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
