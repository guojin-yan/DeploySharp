using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    //public record struct Point(int X, int Y)
    //{
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    public int X = X;

    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    public int Y = Y;

    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <param name="x"></param>
    //    /// <param name="y"></param>
    //    public Point(double x, double y)
    //        : this((int)x, (int)y)
    //    {
    //    }


    //    #region Operators

    //    public readonly Point Plus() => this;


    //    public static Point operator +(Point pt) => pt;


    //    public readonly Point Negate() => new(-X, -Y);


    //    public static Point operator -(Point pt) => pt.Negate();


    //    public readonly Point Add(Point p) => new(X + p.X, Y + p.Y);


    //    public static Point operator +(Point p1, Point p2) => p1.Add(p2);

    //    public readonly Point Subtract(Point p) => new(X - p.X, Y - p.Y);


    //    public static Point operator -(Point p1, Point p2) => p1.Subtract(p2);


    //    public readonly Point Multiply(double scale) => new(X * scale, Y * scale);


    //    public static Point operator *(Point pt, double scale) => pt.Multiply(scale);

    //    #endregion

    //    #region Methods


    //    public static double Distance(Point p1, Point p2)
    //    {
    //        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
    //    }


    //    public readonly double DistanceTo(Point p)
    //    {
    //        return Distance(this, p);
    //    }


    //    public static double DotProduct(Point p1, Point p2)
    //    {
    //        return p1.X * p2.X + p1.Y * p2.Y;
    //    }

    //    public readonly double DotProduct(Point p)
    //    {
    //        return DotProduct(this, p);
    //    }


    //    public static double CrossProduct(Point p1, Point p2)
    //    {
    //        return p1.X * p2.Y - p2.X * p1.Y;
    //    }


    //    public readonly double CrossProduct(Point p)
    //    {
    //        return CrossProduct(this, p);
    //    }

    //    #endregion

    //    public override string ToString() => $"Point(X={X}, Y={Y})";
    //}

    /// <summary>
    /// Represents a 2D point with integer coordinates and geometric operations
    /// 表示具有整数坐标和几何运算的二维点
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides common geometric operations like distance calculation, vector products,
    /// and basic arithmetic operations optimized for performance.
    /// </para>
    /// <para>
    /// 提供常见的几何运算，如距离计算、向量积和为性能优化的基本算术运算。
    /// </para>
    /// </remarks>
    public record struct Point(int X, int Y)
    {
        /// <summary>
        /// The X-coordinate of the point
        /// 点的X坐标
        /// </summary>
        public int X = X;

        /// <summary>
        /// The Y-coordinate of the point
        /// 点的Y坐标
        /// </summary>
        public int Y = Y;

        /// <summary>
        /// Initializes a new point from double values (truncates to integers)
        /// 从双精度值初始化新点(截断为整数)
        /// </summary>
        /// <param name="x">X-coordinate X坐标</param>
        /// <param name="y">Y-coordinate Y坐标</param>
        public Point(double x, double y)
            : this((int)x, (int)y)
        {
        }

        #region Operators

        /// <summary>
        /// Returns the point unchanged (identity operation)
        /// 返回未更改的点(恒等运算)
        /// </summary>
        /// <returns>The same point 相同的点</returns>
        public readonly Point Plus() => this;

        /// <summary>
        /// Returns the point unchanged (identity operation)
        /// 返回未更改的点(恒等运算)
        /// </summary>
        public static Point operator +(Point pt) => pt;

        /// <summary>
        /// Negates both coordinates of the point
        /// 取反点的两个坐标
        /// </summary>
        /// <returns>New point with negated coordinates 坐标取反的新点</returns>
        public readonly Point Negate() => new(-X, -Y);

        /// <summary>
        /// Negates both coordinates of the point
        /// 取反点的两个坐标
        /// </summary>
        public static Point operator -(Point pt) => pt.Negate();

        /// <summary>
        /// Adds two points component-wise
        /// 分量相加两个点
        /// </summary>
        /// <param name="p">Point to add 要相加的点</param>
        /// <returns>New point representing the sum 表示和的新点</returns>
        public readonly Point Add(Point p) => new(X + p.X, Y + p.Y);

        /// <summary>
        /// Adds two points component-wise
        /// 分量相加两个点
        /// </summary>
        public static Point operator +(Point p1, Point p2) => p1.Add(p2);

        /// <summary>
        /// Subtracts one point from another component-wise
        /// 分量相减两个点
        /// </summary>
        /// <param name="p">Point to subtract 要相减的点</param>
        /// <returns>New point representing the difference 表示差的新点</returns>
        public readonly Point Subtract(Point p) => new(X - p.X, Y - p.Y);

        /// <summary>
        /// Subtracts one point from another component-wise
        /// 分量相减两个点
        /// </summary>
        public static Point operator -(Point p1, Point p2) => p1.Subtract(p2);

        /// <summary>
        /// Multiplies both coordinates by a scalar value
        /// 将两个坐标乘以标量值
        /// </summary>
        /// <param name="scale">Scaling factor 缩放因子</param>
        /// <returns>Scaled point 缩放后的点</returns>
        public readonly Point Multiply(double scale) => new((int)(X * scale), (int)(Y * scale));

        /// <summary>
        /// Multiplies both coordinates by a scalar value
        /// 将两个坐标乘以标量值
        /// </summary>
        public static Point operator *(Point pt, double scale) => pt.Multiply(scale);

        #endregion

        #region Geometric Methods

        /// <summary>
        /// Calculates Euclidean distance between two points
        /// 计算两点之间的欧几里得距离
        /// </summary>
        /// <param name="p1">First point 第一个点</param>
        /// <param name="p2">Second point 第二个点</param>
        /// <returns>Distance between points 点之间的距离</returns>
        public static double Distance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        /// <summary>
        /// Calculates Euclidean distance to another point
        /// 计算到另一点的欧几里得距离
        /// </summary>
        /// <param name="p">Target point 目标点</param>
        /// <returns>Distance to target point 到目标点的距离</returns>
        public readonly double DistanceTo(Point p)
        {
            return Distance(this, p);
        }

        /// <summary>
        /// Calculates dot product between two points (vectors)
        /// 计算两点(向量)之间的点积
        /// </summary>
        /// <param name="p1">First point/vector 第一个点/向量</param>
        /// <param name="p2">Second point/vector 第二个点/向量</param>
        /// <returns>Dot product value 点积值</returns>
        public static double DotProduct(Point p1, Point p2)
        {
            return p1.X * p2.X + p1.Y * p2.Y;
        }

        /// <summary>
        /// Calculates dot product with another point (vector)
        /// 计算与另一点(向量)的点积
        /// </summary>
        /// <param name="p">Target point/vector 目标点/向量</param>
        /// <returns>Dot product value 点积值</returns>
        public readonly double DotProduct(Point p)
        {
            return DotProduct(this, p);
        }

        /// <summary>
        /// Calculates cross product between two points (vectors)
        /// 计算两点(向量)之间的叉积
        /// </summary>
        /// <param name="p1">First point/vector 第一个点/向量</param>
        /// <param name="p2">Second point/vector 第二个点/向量</param>
        /// <returns>Cross product value 叉积值</returns>
        public static double CrossProduct(Point p1, Point p2)
        {
            return p1.X * p2.Y - p2.X * p1.Y;
        }

        /// <summary>
        /// Calculates cross product with another point (vector)
        /// 计算与另一点(向量)的叉积
        /// </summary>
        /// <param name="p">Target point/vector 目标点/向量</param>
        /// <returns>Cross product value 叉积值</returns>
        public readonly double CrossProduct(Point p)
        {
            return CrossProduct(this, p);
        }

        #endregion

        /// <summary>
        /// Returns string representation of the point
        /// 返回点的字符串表示形式
        /// </summary>
        /// <returns>Formatted point coordinates 格式化后的点坐标</returns>
        public override string ToString() => $"Point(X={X}, Y={Y})";

        /// <summary>
        /// Compares two points for equality
        /// 比较两点是否相等
        /// </summary>
        public bool Equals(Point other) => X == other.X && Y == other.Y;
    }

}
