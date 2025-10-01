using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    //public record struct PointD(double X, double Y)
    //{

    //    public double X = X;

    //    public double Y = Y;


    //    #region Operators


    //    public readonly PointD Plus() => this;


    //    public static PointD operator +(PointD pt) => pt;


    //    public readonly PointD Negate() => new(-X, -Y);

    //    public static PointD operator -(PointD pt) => pt.Negate();

    //    public readonly PointD Add(PointD p) => new(X + p.X, Y + p.Y);


    //    public static PointD operator +(PointD p1, PointD p2) => p1.Add(p2);

    //    public readonly PointD Subtract(PointD p) => new(X - p.X, Y - p.Y);
    //    public static PointD operator -(PointD p1, PointD p2) => p1.Subtract(p2);
    //    public readonly PointD Multiply(double scale) => new(X * scale, Y * scale);
    //    public static PointD operator *(PointD pt, double scale) => pt.Multiply(scale);

    //    #endregion

    //    #region Methods
    //    public static double Distance(PointD p1, PointD p2)
    //    {
    //        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
    //    }

    //    public readonly double DistanceTo(PointD p)
    //    {
    //        return Distance(this, p);
    //    }
    //    public static double DotProduct(PointD p1, PointD p2)
    //    {
    //        return p1.X * p2.X + p1.Y * p2.Y;
    //    }
    //    public readonly double DotProduct(PointD p)
    //    {
    //        return DotProduct(this, p);
    //    }

    //    public static double CrossProduct(PointD p1, PointD p2)
    //    {
    //        return p1.X * p2.Y - p2.X * p1.Y;
    //    }
    //    public readonly double CrossProduct(PointD p)
    //    {
    //        return CrossProduct(this, p);
    //    }

    //    #endregion
    //}

    /// <summary>
    /// Represents a high-precision 2D point with double-precision coordinates and advanced geometric operations
    /// 表示具有双精度坐标和高级几何运算的高精度二维点
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides high-precision geometric operations essential for computational geometry, 
    /// graphics transformations, and scientific calculations.
    /// </para>
    /// <para>
    /// 为计算几何、图形变换和科学计算提供必要的高精度几何运算。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var p1 = new PointD(1.5, 2.5);
    /// var p2 = new PointD(3.0, 4.0);
    /// double distance = p1.DistanceTo(p2);
    /// PointD sum = p1 + p2;
    /// </code>
    /// </example>
    /// </remarks>
    public record struct PointD(double X, double Y)
    {
        /// <summary>
        /// The X-coordinate with double precision
        /// 双精度的X坐标
        /// </summary>
        /// <value>A double value representing horizontal position 表示水平位置的双精度值</value>
        public double X = X;

        /// <summary>
        /// The Y-coordinate with double precision
        /// 双精度的Y坐标
        /// </summary>
        /// <value>A double value representing vertical position 表示垂直位置的双精度值</value>
        public double Y = Y;

        #region Operators

        /// <summary>
        /// Returns the point unchanged (identity operation)
        /// 返回未更改的点(恒等运算)
        /// </summary>
        /// <returns>The original point 原始点</returns>
        public readonly PointD Plus() => this;

        /// <summary>
        /// Unary plus operator (identity operation)
        /// 一元加运算符(恒等运算)
        /// </summary>
        public static PointD operator +(PointD pt) => pt;

        /// <summary>
        /// Negates both coordinates (returns additive inverse)
        /// 取反两个坐标(返回加法逆元)
        /// </summary>
        /// <returns>New point with coordinates (-X, -Y) 坐标为(-X, -Y)的新点</returns>
        public readonly PointD Negate() => new(-X, -Y);

        /// <summary>
        /// Unary negation operator
        /// 一元取反运算符
        /// </summary>
        public static PointD operator -(PointD pt) => pt.Negate();

        /// <summary>
        /// Performs vector addition (component-wise)
        /// 执行向量加法(分量相加)
        /// </summary>
        /// <param name="p">Point to add 要相加的点</param>
        /// <returns>Resulting point 结果点</returns>
        public readonly PointD Add(PointD p) => new(X + p.X, Y + p.Y);

        /// <summary>
        /// Vector addition operator
        /// 向量加法运算符
        /// </summary>
        public static PointD operator +(PointD p1, PointD p2) => p1.Add(p2);

        /// <summary>
        /// Performs vector subtraction (component-wise)
        /// 执行向量减法(分量相减)
        /// </summary>
        /// <param name="p">Point to subtract 要减去的点</param>
        /// <returns>Resulting point 结果点</returns>
        public readonly PointD Subtract(PointD p) => new(X - p.X, Y - p.Y);

        /// <summary>
        /// Vector subtraction operator
        /// 向量减法运算符
        /// </summary>
        public static PointD operator -(PointD p1, PointD p2) => p1.Subtract(p2);

        /// <summary>
        /// Scales the point by a multiplicative factor
        /// 按乘数因子缩放点
        /// </summary>
        /// <param name="scale">Scaling factor 缩放因子</param>
        /// <returns>Scaled point 缩放后的点</returns>
        public readonly PointD Multiply(double scale) => new(X * scale, Y * scale);

        /// <summary>
        /// Scalar multiplication operator
        /// 标量乘法运算符
        /// </summary>
        public static PointD operator *(PointD pt, double scale) => pt.Multiply(scale);

        #endregion

        #region Geometric Methods

        /// <summary>
        /// Calculates the Euclidean distance between two points
        /// 计算两点之间的欧几里得距离
        /// </summary>
        /// <param name="p1">First point 第一个点</param>
        /// <param name="p2">Second point 第二个点</param>
        /// <returns>Distance between points 点之间的距离</returns>
        public static double Distance(PointD p1, PointD p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        /// <summary>
        /// Calculates distance from this point to another
        /// 计算从该点到另一点的距离
        /// </summary>
        /// <param name="p">Target point 目标点</param>
        /// <returns>Calculated distance 计算的距离</returns>
        public readonly double DistanceTo(PointD p)
        {
            return Distance(this, p);
        }

        /// <summary>
        /// Calculates the dot product of two vectors
        /// 计算两个向量的点积
        /// </summary>
        /// <param name="p1">First vector 第一个向量</param>
        /// <param name="p2">Second vector 第二个向量</param>
        /// <returns>Dot product 点积</returns>
        public static double DotProduct(PointD p1, PointD p2)
        {
            return p1.X * p2.X + p1.Y * p2.Y;
        }

        /// <summary>
        /// Calculates dot product with another vector
        /// 计算与另一个向量的点积
        /// </summary>
        /// <param name="p">Other vector 另一个向量</param>
        /// <returns>Dot product 点积</returns>
        public readonly double DotProduct(PointD p)
        {
            return DotProduct(this, p);
        }

        /// <summary>
        /// Calculates the 2D cross product magnitude
        /// 计算二维叉积大小
        /// </summary>
        /// <param name="p1">First vector 第一个向量</param>
        /// <param name="p2">Second vector 第二个向量</param>
        /// <returns>Cross product magnitude 叉积大小</returns>
        /// <remarks>
        /// The cross product in 2D returns a scalar representing the signed area
        /// of the parallelogram spanned by the two vectors.
        /// 二维叉积返回一个标量，表示两个向量所张成的平行四边形的有符号面积。
        /// </remarks>
        public static double CrossProduct(PointD p1, PointD p2)
        {
            return p1.X * p2.Y - p2.X * p1.Y;
        }

        /// <summary>
        /// Calculates cross product magnitude with another vector
        /// 计算与另一个向量的叉积大小
        /// </summary>
        /// <param name="p">Other vector 另一个向量</param>
        /// <returns>Cross product magnitude 叉积大小</returns>
        public readonly double CrossProduct(PointD p)
        {
            return CrossProduct(this, p);
        }

        #endregion

        /// <summary>
        /// Returns a string representation of the point with high precision
        /// 返回点的高精度字符串表示
        /// </summary>
        /// <returns>Formatted string 格式化字符串</returns>
        public override string ToString() => $"PointD(X={X:F2}, Y={Y:F2})";

        /// <summary>
        /// Compares two points for exact equality (bitwise comparison)
        /// 比较两点是否完全相等(按位比较)
        /// </summary>
        /// <param name="other">Other point to compare 要比较的另一个点</param>
        /// <returns>True if exactly equal 如果完全相同则为true</returns>
        /// <remarks>
        /// For floating-point comparisons considering precision tolerance,
        /// implement an additional ApproximatelyEquals method.
        /// 对于考虑精度容差的浮点比较，应额外实现ApproximatelyEquals方法。
        /// </remarks>
        public bool Equals(PointD other) => X == other.X && Y == other.Y;
    }

}
