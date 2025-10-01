using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    //public record struct Rect(int X, int Y, int Width, int Height)
    //{

    //    public int X = X;
    //    public int Y = Y;

    //    public int Width = Width;

    //    public int Height = Height;


    //    #region Properties


    //    public int Top
    //    {
    //        readonly get => Y;
    //        set => Y = value;
    //    }

    //    public int Bottom => Y + Height;


    //    public int Left
    //    {
    //        get => X;
    //        set => X = value;
    //    }

    //    public int Right => X + Width;


    //    public Point Location
    //    {
    //        get => new(X, Y);
    //        set
    //        {
    //            X = value.X;
    //            Y = value.Y;
    //        }
    //    }


    //    public Size Size
    //    {
    //        get => new(Width, Height);
    //        set
    //        {
    //            Width = value.Width;
    //            Height = value.Height;
    //        }
    //    }

    //    public Point TopLeft => new(X, Y);

    //    public Point BottomRight => new(X + Width, Y + Height);

    //    #endregion

    //    public Rect(Point location, Size size)
    //        : this(location.X, location.Y, size.Width, size.Height)
    //    {
    //    }


    //    public static Rect FromLTRB(int left, int top, int right, int bottom)
    //    {
    //        var r = new Rect
    //        {
    //            X = left,
    //            Y = top,
    //            Width = right - left,
    //            Height = bottom - top
    //        };

    //        if (r.Width < 0)
    //            throw new ArgumentException("right > left");
    //        if (r.Height < 0)
    //            throw new ArgumentException("bottom > top");
    //        return r;
    //    }

    //    #region Operators



    //    public static Rect operator +(Rect rect, Point pt) => rect.Add(pt);


    //    public readonly Rect Add(Point pt)
    //        => this with
    //        {
    //            X = X + pt.X,
    //            Y = Y + pt.Y
    //        };

    //    public static Rect operator -(Rect rect, Point pt)
    //        => rect with
    //        {
    //            X = rect.X - pt.X,
    //            Y = rect.Y - pt.Y
    //        };

    //    public readonly Rect Subtract(Point pt)
    //        => this with
    //        {
    //            X = X - pt.X,
    //            Y = Y - pt.Y
    //        };


    //    public static Rect operator +(Rect rect, Size size) =>
    //        rect with
    //        {
    //            Width = rect.Width + size.Width,
    //            Height = rect.Height + size.Height
    //        };


    //    public readonly Rect Add(Size size) => this with
    //    {
    //        Width = Width + size.Width,
    //        Height = Height + size.Height
    //    };

    //    public static Rect operator -(Rect rect, Size size) =>
    //        rect with
    //        {
    //            Width = rect.Width - size.Width,
    //            Height = rect.Height - size.Height
    //        };

    //    public readonly Rect Subtract(Size size)
    //        => this with
    //        {
    //            Width = Width - size.Width,
    //            Height = Height - size.Height
    //        };




    //    public static Rect operator &(Rect a, Rect b)
    //        => Intersect(a, b);


    //    public static Rect operator |(Rect a, Rect b)
    //        => Union(a, b);



    //    #endregion



    //    #region Methods


    //    public readonly bool Contains(int x, int y)
    //        => (X <= x && Y <= y && X + Width > x && Y + Height > y);

    //    public readonly bool Contains(Point pt)
    //        => Contains(pt.X, pt.Y);

    //    public readonly bool Contains(Rect rect) =>
    //        X <= rect.X &&
    //        (rect.X + rect.Width) <= (X + Width) &&
    //        Y <= rect.Y &&
    //        (rect.Y + rect.Height) <= (Y + Height);


    //    public void Inflate(int width, int height)
    //    {
    //        X -= width;
    //        Y -= height;
    //        Width += (2 * width);
    //        Height += (2 * height);
    //    }


    //    public void Inflate(Size size) => Inflate(size.Width, size.Height);

    //    public static Rect Inflate(Rect rect, int x, int y)
    //    {
    //        rect.Inflate(x, y);
    //        return rect;
    //    }

    //    public static Rect Intersect(Rect a, Rect b)
    //    {
    //        var x1 = Math.Max(a.X, b.X);
    //        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
    //        var y1 = Math.Max(a.Y, b.Y);
    //        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

    //        if (x2 >= x1 && y2 >= y1)
    //            return new Rect(x1, y1, x2 - x1, y2 - y1);
    //        return default;
    //    }


    //    public readonly Rect Intersect(Rect rect) => Intersect(this, rect);


    //    public readonly bool IntersectsWith(Rect rect) =>
    //        (X < rect.X + rect.Width) &&
    //        (X + Width > rect.X) &&
    //        (Y < rect.Y + rect.Height) &&
    //        (Y + Height > rect.Y);

    //    public readonly Rect Union(Rect rect) => Union(this, rect);
    //    public static Rect Union(Rect a, Rect b)
    //    {
    //        var x1 = Math.Min(a.X, b.X);
    //        var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
    //        var y1 = Math.Min(a.Y, b.Y);
    //        var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

    //        return new Rect(x1, y1, x2 - x1, y2 - y1);
    //    }

    //    #endregion

    //    public override string ToString()
    //    {
    //        return $"Rect(X={X}, Y={Y}, Width={Width}, Height={Height})";
    //    }
    //}

   
    /// <summary>
    /// Represents a rectangular region with integer coordinates and dimensions
    /// 表示具有整数坐标和尺寸的矩形区域
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides functionality for geometric operations including intersection,
    /// union, containment checks, and coordinate transformations.
    /// </para>
    /// <para>
    /// 提供几何运算功能，包括交集、并集、包含检查和坐标转换。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var rect = new Rect(10, 20, 100, 50);
    /// var point = new Point(15, 25);
    /// bool contains = rect.Contains(point);
    /// </code>
    /// </example>
    /// </remarks>
    public record struct Rect(int X, int Y, int Width, int Height)
        {
            /// <summary>
            /// Gets or sets the X-coordinate of the rectangle's left edge
            /// 获取或设置矩形左边缘的X坐标
            /// </summary>
            /// <value>The horizontal position of the rectangle's origin</value>
            public int X = X;

            /// <summary>
            /// Gets or sets the Y-coordinate of the rectangle's top edge
            /// 获取或设置矩形上边缘的Y坐标
            /// </summary>
            /// <value>The vertical position of the rectangle's origin</value>
            public int Y = Y;

            ///summary>
            /// Gets or sets the width of the rectangle
            /// 获取或设置矩形的宽度
            /// </summary>
            /// <value>The horizontal extent of the rectangle, must be non-negative</value>
            public int Width = Width;

            ///summary>
            /// Gets or sets the height of the rectangle
            /// 获取或设置矩形的高度
            /// </summary>
            /// <value>The vertical extent of the rectangle, must be non-negative</value>
            public int Height = Height;

            #region Properties

            /// <summary>
            /// Gets or sets the top edge Y-coordinate
            /// 获取或设置上边缘Y坐标
            /// </summary>
            public int Top
            {
                readonly get => Y;
                set => Y = value;
            }

            /// <summary>
            /// Gets the bottom edge Y-coordinate (Y + Height)
            /// 获取下边缘Y坐标(Y + Height)
            /// </summary>
            public int Bottom => Y + Height;

            /// <summary>
            /// Gets or sets the left edge X-coordinate
            /// 获取或设置左边缘X坐标
            /// </summary>
            public int Left
            {
                get => X;
                set => X = value;
            }

            /// <summary>
            /// Gets the right edge X-coordinate (X + Width)
            /// 获取右边缘X坐标(X + Width)
            /// </summary>
            public int Right => X + Width;

            /// <summary>
            /// Gets or sets the top-left location of the rectangle
            /// 获取或设置矩形的左上角位置
            /// </summary>
            public Point Location
            {
                get => new(X, Y);
                set
                {
                    X = value.X;
                    Y = value.Y;
                }
            }

            /// <summary>
            /// Gets or sets the size of the rectangle
            /// 获取或设置矩形的大小
            /// </summary>
            public Size Size
            {
                get => new(Width, Height);
                set
                {
                    Width = value.Width;
                    Height = value.Height;
                }
            }

            /// <summary>
            /// Gets the top-left corner point
            /// 获取左上角点
            /// </summary>
            public Point TopLeft => new(X, Y);

            /// <summary>
            /// Gets the bottom-right corner point
            /// 获取右下角点
            /// </summary>
            public Point BottomRight => new(X + Width, Y + Height);

            #endregion

            /// <summary>
            /// Initializes a new rectangle from location and size
            /// 从位置和大小初始化新矩形
            /// </summary>
            /// <param name="location">Top-left corner location 左上角位置</param>
            /// <param name="size">Dimensions of the rectangle 矩形尺寸</param>
            public Rect(Point location, Size size)
                : this(location.X, location.Y, size.Width, size.Height)
            {
            }

            /// <summary>
            /// Creates a rectangle from left, top, right, and bottom coordinates
            /// 根据左、上、右、下坐标创建矩形
            /// </summary>
            /// <param name="left">Left edge coordinate 左边缘坐标</param>
            /// <param name="top">Top edge coordinate 上边缘坐标</param>
            /// <param name="right">Right edge coordinate 右边缘坐标</param>
            /// <param name="bottom">Bottom edge coordinate 下边缘坐标</param>
            /// <returns>The constructed rectangle 构造的矩形</returns>
            /// <exception cref="ArgumentException">
            /// Thrown when right < left or bottom < top
            /// 当右 < 左或下 < 上时抛出
            /// </exception>
            public static Rect FromLTRB(int left, int top, int right, int bottom)
            {
                var r = new Rect
                {
                    X = left,
                    Y = top,
                    Width = right - left,
                    Height = bottom - top
                };

                if (r.Width < 0)
                    throw new ArgumentException("right < left");
                if (r.Height < 0)
                    throw new ArgumentException("bottom < top");
                return r;
            }

            #region Operators

            /// <summary>
            /// Translates a rectangle by adding point coordinates
            /// 通过添加点坐标平移矩形
            /// </summary>
            public static Rect operator +(Rect rect, Point pt) => rect.Add(pt);

            /// <summary>
            /// Translates the rectangle by adding point coordinates
            /// 通过添加点坐标平移矩形
            /// </summary>
            public readonly Rect Add(Point pt)
                => this with
                {
                    X = X + pt.X,
                    Y = Y + pt.Y
                };

            /// <summary>
            /// Translates a rectangle by subtracting point coordinates
            /// 通过减去点坐标平移矩形
            /// </summary>
            public static Rect operator -(Rect rect, Point pt)
                => rect with
                {
                    X = rect.X - pt.X,
                    Y = rect.Y - pt.Y
                };

            /// <summary>
            /// Translates the rectangle by subtracting point coordinates
            /// 通过减去点坐标平移矩形
            /// </summary>
            public readonly Rect Subtract(Point pt)
                => this with
                {
                    X = X - pt.X,
                    Y = Y - pt.Y
                };

            /// <summary>
            /// Expands a rectangle by adding size dimensions
            /// 通过添加尺寸扩展矩形
            /// </summary>
            public static Rect operator +(Rect rect, Size size) =>
                rect with
                {
                    Width = rect.Width + size.Width,
                    Height = rect.Height + size.Height
                };

            /// <summary>
            /// Expands the rectangle by adding size dimensions
            /// 通过添加尺寸扩展矩形
            /// </summary>
            public readonly Rect Add(Size size) => this with
            {
                Width = Width + size.Width,
                Height = Height + size.Height
            };

            /// <summary>
            /// Shrinks a rectangle by subtracting size dimensions
            /// 通过减去尺寸缩小矩形
            /// </summary>
            public static Rect operator -(Rect rect, Size size) =>
                rect with
                {
                    Width = rect.Width - size.Width,
                    Height = rect.Height - size.Height
                };

            /// <summary>
            /// Shrinks the rectangle by subtracting size dimensions
            /// 通过减去尺寸缩小矩形
            /// </summary>
            public readonly Rect Subtract(Size size)
                => this with
                {
                    Width = Width - size.Width,
                    Height = Height - size.Height
                };

            /// <summary>
            /// Computes the intersection of two rectangles
            /// 计算两个矩形的交集
            /// </summary>
            public static Rect operator &(Rect a, Rect b)
                => Intersect(a, b);

            /// <summary>
            /// Computes the union of two rectangles
            /// 计算两个矩形的并集
            /// </summary>
            public static Rect operator |(Rect a, Rect b)
                => Union(a, b);

            #endregion

            #region Methods

            /// <summary>
            /// Determines if the rectangle contains a point with specified coordinates
            /// 确定矩形是否包含具有指定坐标的点
            /// </summary>
            /// <param name="x">The X-coordinate to test X坐标</param>
            /// <param name="y">The Y-coordinate to test Y坐标</param>
            /// <returns>True if the point is within the rectangle</returns>
            public readonly bool Contains(int x, int y)
                => (X <= x && Y <= y && X + Width > x && Y + Height > y);

            /// <summary>
            /// Determines if the rectangle contains a point
            /// 确定矩形是否包含点
            /// </summary>
            /// <param name="pt">The point to test 要测试的点</param>
            /// <returns>True if the point is within the rectangle</returns>
            public readonly bool Contains(Point pt)
                => Contains(pt.X, pt.Y);

            /// <summary>
            /// Determines if the rectangle fully contains another rectangle
            /// 确定矩形是否完全包含另一个矩形
            /// </summary>
            /// <param name="rect">The rectangle to test 要测试的矩形</param>
            /// <returns>True if fully contained</returns>
            public readonly bool Contains(Rect rect) =>
                X <= rect.X &&
                (rect.X + rect.Width) <= (X + Width) &&
                Y <= rect.Y &&
                (rect.Y + rect.Height) <= (Y + Height);

            /// <summary>
            /// Expands the rectangle by the specified amount
            /// 按指定量扩展矩形
            /// </summary>
            /// <param name="width">Horizontal expansion amount 水平扩展量</param>
            /// <param name="height">Vertical expansion amount 垂直扩展量</param>
            public void Inflate(int width, int height)
            {
                X -= width;
                Y -= height;
                Width += (2 * width);
                Height += (2 * height);
            }

            /// <summary>
            /// Expands the rectangle by the specified size
            /// 按指定尺寸扩展矩形
            /// </summary>


            /// <param name="size">The expansion size 扩展尺寸</param>
            public void Inflate(Size size) => Inflate(size.Width, size.Height);

            /// <summary>
            /// Returns a new rectangle expanded by specified amounts
            /// 返回按指定量扩展的新矩形
            /// </summary>
            /// <param name="rect">The source rectangle 源矩形</param>
            /// <param name="x">Horizontal expansion amount 水平扩展量</param>
            /// <param name="y">Vertical expansion amount 垂直扩展量</param>
            /// <returns>The expanded rectangle 扩展后的矩形</returns>
            public static Rect Inflate(Rect rect, int x, int y)
            {
                rect.Inflate(x, y);
                return rect;
            }

            /// <summary>
            /// Computes the intersection of two rectangles
            /// 计算两个矩形的交集
            /// </summary>
            /// <param name="a">First rectangle 第一个矩形</param>
            /// <param name="b">Second rectangle 第二个矩形</param>
            /// <returns>
            /// The intersection rectangle, or empty rectangle if no intersection
            /// 交集矩形，若无交集则返回空矩形
            /// </returns>
            public static Rect Intersect(Rect a, Rect b)
            {
                var x1 = Math.Max(a.X, b.X);
                var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
                var y1 = Math.Max(a.Y, b.Y);
                var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

                if (x2 >= x1 && y2 >= y1)
                    return new Rect(x1, y1, x2 - x1, y2 - y1);
                return default;
            }

            /// <summary>
            /// Computes intersection with another rectangle
            /// 计算与另一个矩形的交集
            /// </summary>
            /// <param name="rect">The rectangle to intersect with 要计算交集的矩形</param>
            /// <returns>The intersection rectangle</returns>
            public readonly Rect Intersect(Rect rect) => Intersect(this, rect);

            /// <summary>
            /// Determines if this rectangle intersects with another
            /// 确定此矩形是否与另一个矩形相交
            /// </summary>
            /// <param name="rect">The rectangle to test 要测试的矩形</param>
            /// <returns>True if rectangles intersect</returns>
            public readonly bool IntersectsWith(Rect rect) =>
                (X < rect.X + rect.Width) &&
                (X + Width > rect.X) &&
                (Y < rect.Y + rect.Height) &&
                (Y + Height > rect.Y);

            /// <summary>
            /// Computes union with another rectangle
            /// 计算与另一个矩形的并集
            /// </summary>
            /// <param name="rect">The rectangle to union with 要计算并集的矩形</param>
            /// <returns>The bounding rectangle containing both rectangles</returns>
            public readonly Rect Union(Rect rect) => Union(this, rect);

            /// <summary>
            /// Computes the union of two rectangles
            /// 计算两个矩形的并集
            /// </summary>
            /// <param name="a">First rectangle 第一个矩形</param>
            /// <param name="b">Second rectangle 第二个矩形</param>
            /// <returns>The bounding rectangle containing both rectangles</returns>
            public static Rect Union(Rect a, Rect b)
            {
                var x1 = Math.Min(a.X, b.X);
                var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
                var y1 = Math.Min(a.Y, b.Y);
                var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

                return new Rect(x1, y1, x2 - x1, y2 - y1);
            }

            #endregion

            /// <summary>
            /// Returns a string representation of the rectangle
            /// 返回矩形的字符串表示形式
            /// </summary>
            /// <returns>Formatted string showing coordinates and dimensions</returns>
            public override string ToString()
            {
                return $"Rect(X={X}, Y={Y}, Width={Width}, Height={Height})";
            }

            /// <summary>
            /// Determines if two rectangles are equal
            /// 确定两个矩形是否相等
            /// </summary>
            public bool Equals(Rect other) =>
                X == other.X &&
                Y == other.Y &&
                Width == other.Width &&
                Height == other.Height;
    }
} 
