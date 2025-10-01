using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    //public record struct PointF(float X, float Y)
    //{

    //    public float X = X;


    //    public float Y = Y;


    //    #region Operators

    //    public readonly PointF Plus() => this;


    //    public static PointF operator +(PointF pt)
    //    {
    //        return pt;
    //    }

    //    public readonly PointF Negate() => new(-X, -Y);

    //    public static PointF operator -(PointF pt) => pt.Negate();


    //    public readonly PointF Add(PointF p) => new(X + p.X, Y + p.Y);


    //    public static PointF operator +(PointF p1, PointF p2) => p1.Add(p2);


    //    public readonly PointF Subtract(PointF p) => new(X - p.X, Y - p.Y);


    //    public static PointF operator -(PointF p1, PointF p2) => p1.Subtract(p2);


    //    public readonly PointF Multiply(double scale) => new((float)(X * scale), (float)(Y * scale));


    //    public static PointF operator *(PointF pt, double scale) => pt.Multiply(scale);

    //    #endregion

    //    #region Methods


    //    public static double Distance(PointF p1, PointF p2)
    //    {
    //        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
    //    }


    //    public readonly double DistanceTo(PointF p)
    //    {
    //        return Distance(this, p);
    //    }


    //    public static double DotProduct(PointF p1, PointF p2)
    //    {
    //        return p1.X * p2.X + p1.Y * p2.Y;
    //    }


    //    public readonly double DotProduct(PointF p)
    //    {
    //        return DotProduct(this, p);
    //    }


    //    public static double CrossProduct(PointF p1, PointF p2)
    //    {
    //        return p1.X * p2.Y - p2.X * p1.Y;
    //    }

    //    public readonly double CrossProduct(PointF p)
    //    {
    //        return CrossProduct(this, p);
    //    }

    //    #endregion
    //}
    /// <summary>
    /// Represents a single-precision floating-point 2D point with geometric operations
    /// 表示具有几何运算的单精度浮点二维点
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optimized for graphics programming where high performance with moderate precision is required.
    /// Provides all essential vector operations while maintaining memory efficiency.
    /// </para>
    /// <para>
    /// 针对需要中等精度高性能的图形编程而优化，
    /// 提供了所有必要的向量运算，同时保持内存效率。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var pointA = new PointF(1.5f, 2.5f);
    /// var pointB = new PointF(3.0f, 4.0f);
    /// float distance = (float)pointA.DistanceTo(pointB);
    /// PointF sum = pointA + pointB;
    /// </code>
    /// </example>
    /// </remarks>
    public record struct PointF(float X, float Y)
    {
        /// <summary>
        /// The X-coordinate (single-precision floating-point)
        /// X坐标（单精度浮点）
        /// </summary>
        /// <value>The horizontal position ranging from ±1.5 x 10^-45 to ±3.4 x 10^38</value>
        public float X = X;

        /// <summary>
        /// The Y-coordinate (single-precision floating-point)
        /// Y坐标（单精度浮点）
        /// </summary>
        /// <value>The vertical position ranging from ±1.5 x 10^-45 to ±3.4 x 10^38</value>
        public float Y = Y;

        #region Operators

        /// <summary>
        /// Identity operation - returns the point unchanged
        /// 恒等运算 - 返回未改变的点
        /// </summary>
        /// <returns>The original point</returns>
        public readonly PointF Plus() => this;

        /// <summary>
        /// Unary plus operator - returns the point unchanged
        /// 一元加运算符 - 返回未改变的点
        /// </summary>
        public static PointF operator +(PointF pt) => pt;

        /// <summary>
        /// Negates both coordinates (additive inverse)
        /// 对两个坐标取反（加法逆元）
        /// </summary>
        /// <returns>New point with coordinates (-X, -Y)</returns>
        public readonly PointF Negate() => new(-X, -Y);

        /// <summary>
        /// Unary negation operator
        /// 一元取反运算符
        /// </summary>
        public static PointF operator -(PointF pt) => pt.Negate();

        /// <summary>
        /// Vector addition (component-wise)
        /// 向量加法（分量相加）
        /// </summary>
        /// <param name="p">The point to add</param>
        /// <returns>Resulting point</returns>
        public readonly PointF Add(PointF p) => new(X + p.X, Y + p.Y);

        /// <summary>
        /// Vector addition operator
        /// 向量加法运算符
        /// </summary>
        public static PointF operator +(PointF p1, PointF p2) => p1.Add(p2);

        /// <summary>
        /// Vector subtraction (component-wise)
        /// 向量减法（分量相减）
        /// </summary>
        /// <param name="p">The point to subtract</param>
        /// <returns>Resulting point</returns>
        public readonly PointF Subtract(PointF p) => new(X - p.X, Y - p.Y);

        /// <summary>
        /// Vector subtraction operator
        /// 向量减法运算符
        /// </summary>
        public static PointF operator -(PointF p1, PointF p2) => p1.Subtract(p2);

        /// <summary>
        /// Scales the point by a double-precision factor (returns float)
        /// 使用双精度因子缩放点（返回浮点）
        /// </summary>
        /// <param name="scale">The scaling factor</param>
        /// <returns>Scaled point</returns>
        /// <remarks>
        /// Note: Input is double precision but result is single precision.
        /// This allows high precision intermediate calculations while maintaining memory efficiency.
        /// </remarks>
        public readonly PointF Multiply(double scale) => new((float)(X * scale), (float)(Y * scale));

        /// <summary>
        /// Scalar multiplication operator
        /// 标量乘法运算符
        /// </summary>
        public static PointF operator *(PointF pt, double scale) => pt.Multiply(scale);

        #endregion

        #region Geometric Methods

        /// <summary>
        /// Calculates Euclidean distance between two points (returns double precision)
        /// 计算两点之间的欧几里得距离（返回双精度）
        /// </summary>
        /// <param name="p1">First point</param>
        /// <param name="p2">Second point</param>
        /// <returns>The distance between points</returns>
        public static double Distance(PointF p1, PointF p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculates distance to another point (returns double precision)
        /// 计算到另一个点的距离（返回双精度）
        /// </summary>
        /// <param name="p">The target point</param>
        /// <returns>The distance to target point</returns>
        public readonly double DistanceTo(PointF p) => Distance(this, p);

        /// <summary>
        /// Calculates dot product of two vectors (returns double precision)
        /// 计算两个向量的点积（返回双精度）
        /// </summary>
        /// <param name="p1">First vector</param>
        /// <param name="p2">Second vector</param>
        /// <returns>The dot product</returns>
        public static double DotProduct(PointF p1, PointF p2) => p1.X * p2.X + p1.Y * p2.Y;

        /// <summary>
        /// Calculates dot product with another vector (returns double precision)
        /// 计算与另一个向量的点积（返回双精度）
        /// </summary>
        /// <param name="p">The other vector</param>
        /// <returns>The dot product</returns>
        public readonly double DotProduct(PointF p) => DotProduct(this, p);

        /// <summary>
        /// Calculates 2D cross product magnitude (returns double precision)
        /// 计算二维叉积大小（返回双精度）
        /// </summary>
        /// <param name="p1">First vector</param>
        /// <param name="p2">Second vector</param>
        /// <returns>The cross product magnitude</returns>
        /// <remarks>
        /// In 2D, cross product returns a scalar representing the signed area
        /// of the parallelogram spanned by the two vectors.
        /// </remarks>
        public static double CrossProduct(PointF p1, PointF p2) => p1.X * p2.Y - p2.X * p1.Y;

        /// <summary>
        /// Calculates cross product with another vector (returns double precision)
        /// 计算与另一个向量的叉积（返回双精度）
        /// </summary>
        /// <param name="p">The other vector</param>
        /// <returns>The cross product magnitude</returns>
        public readonly double CrossProduct(PointF p) => CrossProduct(this, p);

        #endregion

        /// <summary>
        /// Returns a string representation of the point
        /// 返回点的字符串表示形式
        /// </summary>
        /// <returns>Formatted point coordinates</returns>
        public override string ToString() => $"PointF(X={X:F2}, Y={Y:F2})";

        /// <summary>
        /// Determines if two points are exactly equal
        /// 确定两个点是否完全相等
        /// </summary>
        /// <param name="other">The point to compare</param>
        /// <returns>True if exactly equal</returns>
        public bool Equals(PointF other) => X == other.X && Y == other.Y;
    }

}
