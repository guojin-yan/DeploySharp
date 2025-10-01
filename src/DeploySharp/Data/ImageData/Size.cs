using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{

    /// <summary>
    /// Represents a size with integer width and height dimensions
    /// 表示具有整数宽度和高度尺寸的大小
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used for representing dimensions of rectangular areas in integer units.
    /// Suitable for pixel-based operations and discrete coordinate systems.
    /// </para>
    /// <para>
    /// 用于表示矩形区域的整数单位尺寸。
    /// 适用于基于像素的操作和离散坐标系。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var size = new Size(1920, 1080); // HD resolution
    /// var fromDouble = new Size(1920.7, 1080.3); // Will truncate to (1920, 1080)
    /// </code>
    /// </example>
    /// </remarks>
    public record struct Size(int Width, int Height)
    {
        /// <summary>
        /// Gets or sets the horizontal dimension
        /// 获取或设置水平尺寸
        /// </summary>
        /// <value>The width component</value>
        public int Width = Width;

        /// <summary>
        /// Gets or sets the vertical dimension
        /// 获取或设置垂直尺寸
        /// </summary>
        /// <value>The height component</value>
        public int Height = Height;

        /// <summary>
        /// Initializes a new size from floating-point dimensions
        /// 从浮点尺寸初始化新大小
        /// </summary>
        /// <param name="width">Width dimension (will be truncated) 宽度尺寸（将被截断）</param>
        /// <param name="height">Height dimension (will be truncated) 高度尺寸（将被截断）</param>
        /// <remarks>
        /// Values are truncated (not rounded) to integer values.
        /// 值将被截断（非四舍五入）为整数值。
        /// </remarks>
        public Size(double width, double height)
            : this((int)width, (int)height)
        {
        }

        /// <summary>
        /// Explicit conversion from SizeD to Size
        /// 从SizeD到Size的显式转换
        /// </summary>
        /// <param name="size">Double-precision floating-point size 双精度浮点大小</param>
        /// <returns>New Size with truncated dimensions</returns>
        /// <exception cref="OverflowException">
        /// Thrown if either dimension exceeds integer range
        /// 如果任一维度超出整数范围则抛出
        /// </exception>
        public static explicit operator Size(SizeD size)
            => new(size.Width, size.Height);

        /// <summary>
        /// Explicit conversion from SizeF to Size
        /// 从SizeF到Size的显式转换
        /// </summary>
        /// <param name="size">Single-precision floating-point size 单精度浮点大小</param>
        /// <returns>New Size with truncated dimensions</returns>
        /// <exception cref="OverflowException">
        /// Thrown if either dimension exceeds integer range
        /// 如果任一维度超出整数范围则抛出
        /// </exception>
        public static explicit operator Size(SizeF size)
            => new(size.Width, size.Height);

        /// <summary>
        /// Returns a string representation of the size
        /// 返回大小的字符串表示形式
        /// </summary>
        /// <returns>Formatted string showing width and height</returns>
        public override string ToString()
        {
            return $"(Width: {Width}, Height: {Height})";
        }
    }

}
