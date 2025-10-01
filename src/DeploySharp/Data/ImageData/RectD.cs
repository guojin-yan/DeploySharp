using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents a rectangle defined by X/Y coordinates, width and height (double precision)
    /// 表示由X/Y坐标、宽度和高度定义的双精度矩形结构
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides geometric operations for rectangle manipulation including intersection,
    /// union, containment checks, and arithmetic operations with points/sizes.
    /// 提供矩形几何操作，包括相交、并集、包含检查以及与点/大小的算术运算
    /// </para>
    /// </remarks>
    public record struct RectD(double X, double Y, double Width, double Height)
    {

        /// <summary>
        /// X coordinate of the rectangle's origin point
        /// 矩形原点X坐标
        /// </summary>
        public double X = X;

        /// <summary>
        /// Y coordinate of the rectangle's origin point
        /// 矩形原点Y坐标
        /// </summary>
        public double Y = Y;

        /// <summary>
        /// Width of the rectangle
        /// 矩形的宽度
        /// </summary>
        public double Width = Width;

        /// <summary>
        /// Height of the rectangle
        /// 矩形的高度
        /// </summary>
        public double Height = Height;

        /// <summary>
        /// Creates a rectangle from location point and size
        /// 通过位置点和大小创建矩形
        /// </summary>
        /// <param name="location">Top-left corner location/左上角位置</param>
        /// <param name="size">Rectangle dimensions/矩形尺寸</param>
        public RectD(PointD location, SizeD size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
            X = location.X;
            Y = location.Y;
            Width = size.Width;
            Height = size.Height;
        }

        /// <summary>
        /// Creates a rectangle from left, top, right and bottom coordinates
        /// 通过左、上、右、下坐标创建矩形
        /// </summary>
        /// <param name="left">Left coordinate/左侧坐标</param>
        /// <param name="top">Top coordinate/顶部坐标</param>
        /// <param name="right">Right coordinate/右侧坐标</param>
        /// <param name="bottom">Bottom coordinate/底部坐标</param>
        /// <returns>New RectangleD instance/新的RectangleD实例</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when right < left or bottom < top
        /// 当right < left 或 bottom < top时抛出
        /// </exception>
        public static RectD FromLTRB(double left, double top, double right, double bottom)
        {
            var r = new RectD
            {
                X = left,
                Y = top,
                Width = right - left,
                Height = bottom - top
            };

            if (r.Width < 0)
                throw new ArgumentException("right > left");
            if (r.Height < 0)
                throw new ArgumentException("bottom > top");
            return r;
        }

        #region Operators

        /// <summary>
        /// Translates rectangle by specified point
        /// 通过指定点平移矩形
        /// </summary>
        public static RectD operator +(RectD rect, PointD pt)
            => rect.Add(pt);

        /// <summary>
        /// Translates rectangle by specified point
        /// 通过指定点平移矩形
        /// </summary>
        public readonly RectD Add(PointD pt)
            => this with
            {
                X = X + pt.X,
                Y = Y + pt.Y
            };

        /// <summary>
        /// Translates rectangle inversely by specified point
        /// 通过指定点反向平移矩形
        /// </summary>
        public static RectD operator -(RectD rect, PointD pt)
            => rect.Subtract(pt);

        /// <summary>
        /// Translates rectangle inversely by specified point
        /// 通过指定点反向平移矩形
        /// </summary>
        public readonly RectD Subtract(PointD pt)
            => this with
            {
                X = X - pt.X,
                Y = Y - pt.Y
            };


        /// <summary>
        /// Computes the intersection of two rectangles using & operator
        /// 使用 & 运算符计算两个矩形的交集
        /// </summary>
        /// <param name="a">First rectangle/第一个矩形</param>
        /// <param name="b">Second rectangle/第二个矩形</param>
        /// <returns>
        /// A rectangle representing the intersection area, or empty if no intersection
        /// 表示相交区域的矩形，若无交集则返回空矩形
        /// </returns>
        /// <example>
        /// <code>
        /// var rect1 = new RectD(0, 0, 100, 100);
        /// var rect2 = new RectD(50, 50, 100, 100);
        /// var intersection = rect1 & rect2; // RectD(50, 50, 50, 50)
        /// </code>
        /// </example>
        public static RectD operator &(RectD a, RectD b)
            => Intersect(a, b);

        /// <summary>
        /// Computes the union of two rectangles using | operator
        /// 使用 | 运算符计算两个矩形的并集
        /// </summary>
        /// <param name="a">First rectangle/第一个矩形</param>
        /// <param name="b">Second rectangle/第二个矩形</param>
        /// <returns>
        /// The smallest rectangle that contains both rectangles
        /// 包含两个矩形的最小矩形
        /// </returns>
        /// <example>
        /// <code>
        /// var rect1 = new RectD(0, 0, 100, 100);
        /// var rect2 = new RectD(150, 150, 100, 100);
        /// var union = rect1 | rect2; // RectD(0, 0, 250, 250)
        /// </code>
        /// </example>
        public static RectD operator |(RectD a, RectD b)
            => Union(a, b);


        #endregion


        #region Properties

        /// <summary>
        /// Gets or sets the y-coordinate of the top edge
        /// 获取或设置顶部边缘的Y坐标
        /// </summary>
        /// <value>
        /// The y-coordinate of the top edge of the rectangle
        /// 矩形顶部边缘的Y坐标值
        /// </value>
        public double Top
        {
            readonly get => Y;
            set => Y = value;
        }

        /// <summary>
        /// Gets the y-coordinate of the bottom edge (read-only)
        /// 获取底部边缘的Y坐标（只读）
        /// </summary>
        /// <value>
        /// The y-coordinate of the bottom edge (Y + Height)
        /// 底部边缘的Y坐标值（Y + Height）
        /// </value>
        public readonly double Bottom => Y + Height;

        /// <summary>
        /// Gets or sets the x-coordinate of the left edge
        /// 获取或设置左侧边缘的X坐标
        /// </summary>
        /// <value>
        /// The x-coordinate of the left edge of the rectangle
        /// 矩形左侧边缘的X坐标值
        /// </value>
        public double Left
        {
            readonly get => X;
            set => X = value;
        }

        /// <summary>
        /// Gets the x-coordinate of the right edge (read-only)
        /// 获取右侧边缘的X坐标（只读）
        /// </summary>
        /// <value>
        /// The x-coordinate of the right edge (X + Width)
        /// 右侧边缘的X坐标值（X + Width）
        /// </value>
        public readonly double Right => X + Width;

        /// <summary>
        /// Gets or sets the location of the upper-left corner
        /// 获取或设置左上角的位置
        /// </summary>
        /// <value>
        /// A PointD representing the upper-left corner coordinates
        /// 表示左上角坐标的PointD值
        /// </value>
        public PointD Location
        {
            readonly get => new(X, Y);
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
        /// <value>
        /// A SizeD representing the width and height of the rectangle
        /// 表示矩形宽度和高度的SizeD值
        /// </value>
        public SizeD Size
        {
            readonly get => new(Width, Height);
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        /// <summary>
        /// Gets the coordinates of the upper-left corner (read-only)
        /// 获取左上角的坐标（只读）
        /// </summary>
        /// <value>
        /// A PointD representing the upper-left corner
        /// 表示左上角的PointD值
        /// </value>
        public readonly PointD TopLeft => new(X, Y);

        /// <summary>
        /// Gets the coordinates of the bottom-right corner (read-only)
        /// 获取右下角的坐标（只读）
        /// </summary>
        /// <value>
        /// A PointD representing the bottom-right corner
        /// 表示右下角的PointD值
        /// </value>
        public readonly PointD BottomRight => new(X + Width, Y + Height);

        #endregion

        #region Methods

        /// <summary>
        /// Converts the RectD to a Rect by truncating coordinates
        /// 通过截断坐标将RectD转换为Rect
        /// </summary>
        /// <returns>
        /// A Rect with truncated coordinates
        /// 带有截断坐标的Rect
        /// </returns>
        /// <remarks>
        /// Note that this conversion performs truncation (rounds toward zero)
        /// rather than rounding.
        /// 注意此转换执行截断（向零舍入）而非四舍五入。
        /// </remarks>
        public readonly Rect ToRect()
            => new((int)X, (int)Y, (int)Width, (int)Height);

        /// <summary>
        /// Determines if the specified point is contained within this rectangle
        /// 确定指定点是否包含在此矩形内
        /// </summary>
        /// <param name="x">The x-coordinate of the point/点的X坐标</param>
        /// <param name="y">The y-coordinate of the point/点的Y坐标</param>
        /// <returns>
        /// true if the point is contained; otherwise false
        /// 如果点包含在内则为true，否则为false
        /// </returns>
        public readonly bool Contains(double x, double y)
            => (X <= x && Y <= y && X + Width > x && Y + Height > y);

        /// <summary>
        /// Determines if the specified point is contained within this rectangle
        /// 确定指定点是否包含在此矩形内
        /// </summary>
        /// <param name="pt">The point to check/要检查的点</param>
        /// <returns>
        /// true if the point is contained; otherwise false
        /// 如果点包含在内则为true，否则为false
        /// </returns>
        public readonly bool Contains(PointD pt)
            => Contains(pt.X, pt.Y);

        /// <summary>
        /// Determines if the specified rectangle is entirely contained within this rectangle
        /// 确定指定矩形是否完全包含在此矩形内
        /// </summary>
        /// <param name="rect">The rectangle to check/要检查的矩形</param>
        /// <returns>
        /// true if the rectangle is fully contained; otherwise false
        /// 如果矩形完全包含则为true，否则为false
        /// </returns>
        public readonly bool Contains(RectD rect) =>
            X <= rect.X &&
            (rect.X + rect.Width) <= (X + Width) &&
            Y <= rect.Y &&
            (rect.Y + rect.Height) <= (Y + Height);

        /// <summary>
        /// Expands or shrinks the rectangle by the specified amount
        /// 按指定数量扩展或收缩矩形
        /// </summary>
        /// <param name="width">Horizontal expansion amount/水平扩展量</param>
        /// <param name="height">Vertical expansion amount/垂直扩展量</param>
        /// <remarks>
        /// Positive values expand the rectangle while negative values shrink it.
        /// The rectangle expands/shrinks symmetrically in both directions.
        /// 正值扩展矩形，负值收缩矩形。矩形在两个方向上对称地扩展/收缩。
        /// </remarks>
        public void Inflate(double width, double height)
        {
            X -= width;
            Y -= height;
            Width += (2 * width);
            Height += (2 * height);
        }

        /// <summary>
        /// Expands or shrinks the rectangle by the specified size
        /// 按指定大小扩展或收缩矩形
        /// </summary>
        /// <param name="size">The size to inflate by/要扩展的大小</param>
        public void Inflate(SizeD size) => Inflate(size.Width, size.Height);

        /// <summary>
        /// Expands the specified Rect by the given amounts
        /// 按给定数量扩展指定的Rect
        /// </summary>
        /// <param name="rect">The rectangle to inflate/要扩展的矩形</param>
        /// <param name="x">Horizontal expansion amount/水平扩展量</param>
        /// <param name="y">Vertical expansion amount/垂直扩展量</param>
        /// <returns>The inflated rectangle/扩展后的矩形</returns>
        public static Rect Inflate(Rect rect, int x, int y)
        {
            rect.Inflate(x, y);
            return rect;
        }

        /// <summary>
        /// Computes the intersection of two rectangles
        /// 计算两个矩形的交集
        /// </summary>
        /// <param name="a">First rectangle/第一个矩形</param>
        /// <param name="b">Second rectangle/第二个矩形</param>
        /// <returns>
        /// A rectangle representing the intersection area, or empty if no intersection
        /// 表示相交区域的矩形，若无交集则返回空矩形
        /// </returns>
        public static RectD Intersect(RectD a, RectD b)
        {
            var x1 = Math.Max(a.X, b.X);
            var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Max(a.Y, b.Y);
            var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            if (x2 >= x1 && y2 >= y1)
                return new RectD(x1, y1, x2 - x1, y2 - y1);
            return default;
        }

        /// <summary>
        /// Computes the intersection between this rectangle and another
        /// 计算此矩形与另一个矩形的交集
        /// </summary>
        /// <param name="rect">The rectangle to intersect with/要相交的矩形</param>
        /// <returns>
        /// A rectangle representing the intersection area, or empty if no intersection
        /// 表示相交区域的矩形，若无交集则返回空矩形
        /// </returns>
        public readonly RectD Intersect(RectD rect) => Intersect(this, rect);

        /// <summary>
        /// Determines if this rectangle intersects with another rectangle
        /// 确定此矩形是否与另一个矩形相交
        /// </summary>
        /// <param name="rect">The rectangle to test/要测试的矩形</param>
        /// <returns>
        /// true if the rectangles intersect; otherwise false
        /// 如果矩形相交则为true，否则为false
        /// </returns>
        public readonly bool IntersectsWith(RectD rect) =>
            (X < rect.X + rect.Width) &&
            (X + Width > rect.X) &&
            (Y < rect.Y + rect.Height) &&
            (Y + Height > rect.Y);

        /// <summary>
        /// Computes the union between this rectangle and another
        /// 计算此矩形与另一个矩形的并集
        /// </summary>
        /// <param name="rect">The rectangle to union with/要合并的矩形</param>
        /// <returns>
        /// The smallest rectangle that contains both rectangles
        /// 包含两个矩形的最小矩形
        /// </returns>
        public readonly RectD Union(RectD rect) => Union(this, rect);

        /// <summary>
        /// Computes the union of two rectangles
        /// 计算两个矩形的并集
        /// </summary>
        /// <param name="a">First rectangle/第一个矩形</param>
        /// <param name="b">Second rectangle/第二个矩形</param>
        /// <returns>
        /// The smallest rectangle that contains both rectangles
        /// 包含两个矩形的最小矩形
        /// </returns>
        public static RectD Union(RectD a, RectD b)
        {
            var x1 = Math.Min(a.X, b.X);
            var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Min(a.Y, b.Y);
            var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

            return new RectD(x1, y1, x2 - x1, y2 - y1);
        }

        #endregion

        /// <summary>
        /// Returns a string representation of the rectangle
        /// 返回矩形的字符串表示形式
        /// </summary>
        /// <returns>Formatted string showing coordinates and dimensions</returns>
        public override string ToString()
        {
            return $"Rect(X={X:F2}, Y={Y:F2}, Width={Width:F2}, Height={Height:F2})";
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
