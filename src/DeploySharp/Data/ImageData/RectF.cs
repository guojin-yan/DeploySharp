using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents a rectangle structure with single-precision floating-point coordinates and dimensions
    /// 表示具有单精度浮点坐标和尺寸的矩形结构
    /// </summary>
    /// <remarks>
    /// <para>
    /// This structure is immutable and provides various operations for rectangle manipulation.
    /// 该结构是不可变的，并提供各种矩形操作功能。
    /// </para>
    /// <para>
    /// The coordinate system assumes Y increases downward and X increases rightward.
    /// 坐标系假定Y向下增加，X向右增加。
    /// </para>
    /// </remarks>
    public record struct RectF(float X, float Y, float Width, float Height)
    {
        /// <summary>
        /// The x-coordinate of the rectangle's origin (left edge)
        /// 矩形原点的x坐标（左边缘）
        /// </summary>
        public float X = X;

        /// <summary>
        /// The y-coordinate of the rectangle's origin (top edge)
        /// 矩形原点的y坐标（上边缘）
        /// </summary>
        public float Y = Y;

        /// <summary>
        /// The width of the rectangle
        /// 矩形的宽度
        /// </summary>
        public float Width = Width;

        /// <summary>
        /// The height of the rectangle
        /// 矩形的高度
        /// </summary>
        public float Height = Height;

        /// <summary>
        /// Initializes a new instance of the RectF structure from location and size
        /// 从位置和大小初始化RectF结构的新实例
        /// </summary>
        /// <param name="location">The top-left corner coordinates/左上角坐标</param>
        /// <param name="size">The dimensions of the rectangle/矩形的尺寸</param>
        /// <example>
        /// <code>
        /// var location = new PointF(10f, 20f);
        /// var size = new SizeF(30f, 40f);
        /// var rect = new RectF(location, size);
        /// </code>
        /// </example>
        public RectF(PointF location, SizeF size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
            X = location.X;
            Y = location.Y;
            Width = size.Width;
            Height = size.Height;
        }

        /// <summary>
        /// Creates a RectF from edge coordinates (left, top, right, bottom)
        /// 从边缘坐标（左、上、右、下）创建RectF
        /// </summary>
        /// <param name="left">The left edge coordinate/左边缘坐标</param>
        /// <param name="top">The top edge coordinate/上边缘坐标</param>
        /// <param name="right">The right edge coordinate/右边缘坐标</param>
        /// <param name="bottom">The bottom edge coordinate/下边缘坐标</param>
        /// <returns>A new RectF instance/新的RectF实例</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when right is less than left or bottom is less than top
        /// 当右边缘小于左边缘或下边缘小于上边缘时抛出
        /// </exception>
        public static RectF FromLTRB(float left, float top, float right, float bottom)
        {
            var r = new RectF
            {
                X = left,
                Y = top,
                Width = right - left,
                Height = bottom - top
            };

            if (r.Width < 0)
                throw new ArgumentException("Right must be greater than left");
            if (r.Height < 0)
                throw new ArgumentException("Bottom must be greater than top");
            return r;
        }

        #region Operators

        /// <summary>
        /// Translates a rectangle by the specified point
        /// 通过指定点平移矩形
        /// </summary>
        /// <param name="rect">The rectangle to translate/要平移的矩形</param>
        /// <param name="pt">The translation amount/平移量</param>
        /// <returns>A new translated rectangle/平移后的新矩形</returns>
        public static RectF operator +(RectF rect, PointF pt)
            => rect.Add(pt);

        /// <summary>
        /// Translates this rectangle by the specified point
        /// 将此矩形平移指定点
        /// </summary>
        /// <param name="pt">The point to add/要添加的点</param>
        /// <returns>A new translated rectangle/平移后的新矩形</returns>
        public readonly RectF Add(PointF pt)
            => this with
            {
                X = X + pt.X,
                Y = Y + pt.Y
            };

        /// <summary>
        /// Translates a rectangle inversely by the specified point
        /// 通过指定点反向平移矩形
        /// </summary>
        /// <param name="rect">The rectangle to translate/要平移的矩形</param>
        /// <param name="pt">The translation amount/平移量</param>
        /// <returns>A new translated rectangle/平移后的新矩形</returns>
        public static RectF operator -(RectF rect, PointF pt)
            => rect.Subtract(pt);

        /// <summary>
        /// Translates this rectangle inversely by the specified point
        /// 将此矩形反向平移指定点
        /// </summary>
        /// <param name="pt">The point to subtract/要减去的点</param>
        /// <returns>A new translated rectangle/平移后的新矩形</returns>
        public readonly RectF Subtract(PointF pt)
            => this with
            {
                X = X - pt.X,
                Y = Y - pt.Y
            };

        /// <summary>
        /// Adds the specified size to the rectangle
        /// 将指定大小添加到矩形
        /// </summary>
        /// <param name="rect">The rectangle to add to/要添加的矩形</param>
        /// <param name="size">The size to add/要添加的大小</param>
        /// <returns>A new rectangle with adjusted dimensions/调整尺寸后的新矩形</returns>
        public static RectF operator +(RectF rect, SizeF size)
            => rect.Add(size);

        /// <summary>
        /// Adds the specified size to this rectangle
        /// 将指定大小添加到此矩形
        /// </summary>
        /// <param name="size">The size to add/要添加的大小</param>
        /// <returns>A new rectangle with adjusted dimensions/调整尺寸后的新矩形</returns>
        public readonly RectF Add(SizeF size)
            => this with
            {
                Width = Width + size.Width,
                Height = Height + size.Height
            };

        /// <summary>
        /// Subtracts the specified size from the rectangle
        /// 从矩形中减去指定大小
        /// </summary>
        /// <param name="rect">The rectangle to subtract from/要减去的矩形</param>
        /// <param name="size">The size to subtract/要减去的大小</param>
        /// <returns>A new rectangle with adjusted dimensions/调整尺寸后的新矩形</returns>
        public static RectF operator -(RectF rect, SizeF size)
            => rect.Subtract(size);

        /// <summary>
        /// Subtracts the specified size from this rectangle
        /// 从此矩形中减去指定大小
        /// </summary>
        /// <param name="size">The size to subtract/要减去的大小</param>
        /// <returns>A new rectangle with adjusted dimensions/调整尺寸后的新矩形</returns>
        public readonly RectF Subtract(SizeF size)
            => this with
            {
                Width = Width - size.Width,
                Height = Height - size.Height
            };

        /// <summary>
        /// Computes the intersection of two rectangles using bitwise AND operator (&)
        /// 使用按位AND运算符(&)计算两个矩形的交集
        /// </summary>
        /// <param name="a">First rectangle/第一个矩形</param>
        /// <param name="b">Second rectangle/第二个矩形</param>
        /// <returns>The intersection rectangle/相交矩形</returns>
        public static RectF operator &(RectF a, RectF b)
            => Intersect(a, b);

        /// <summary>
        /// Computes the union of two rectangles using bitwise OR operator (|)
        /// 使用按位OR运算符(|)计算两个矩形的并集
        /// </summary>
        /// <param name="a">First rectangle/第一个矩形</param>
        /// <param name="b">Second rectangle/第二个矩形</param>
        /// <returns>The union rectangle/并集矩形</returns>
        public static RectF operator |(RectF a, RectF b)
            => Union(a, b);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the y-coordinate of the top edge
        /// 获取或设置顶部边缘的y坐标
        /// </summary>
        public float Top
        {
            readonly get => Y;
            set => Y = value;
        }

        /// <summary>
        /// Gets the y-coordinate of the bottom edge (readonly)
        /// 获取底部边缘的y坐标（只读）
        /// </summary>
        public readonly float Bottom => Y + Height;

        /// <summary>
        /// Gets or sets the x-coordinate of the left edge
        /// 获取或设置左侧边缘的x坐标
        /// </summary>
        public float Left
        {
            readonly get => X;
            set => X = value;
        }

        /// <summary>
        /// Gets the x-coordinate of the right edge (readonly)
        /// 获取右侧边缘的x坐标（只读）
        /// </summary>
        public readonly float Right => X + Width;

        /// <summary>
        /// Gets or sets the location (top-left corner) of the rectangle
        /// 获取或设置矩形的位置（左上角）
        /// </summary>
        public PointF Location
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
        public SizeF Size
        {
            readonly get => new(Width, Height);
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        /// <summary>
        /// Gets the coordinates of the top-left corner (readonly)
        /// 获取左上角的坐标（只读）
        /// </summary>
        public readonly PointF TopLeft => new(X, Y);

        /// <summary>
        /// Gets the coordinates of the bottom-right corner (readonly)
        /// 获取右下角的坐标（只读）
        /// </summary>
        public readonly PointF BottomRight => new(X + Width, Y + Height);

        #endregion

        #region Methods

        /// <summary>
        /// Determines if the specified point is contained within this rectangle
        /// 确定指定点是否包含在此矩形内
        /// </summary>
        /// <param name="x">The x-coordinate of the point/点的x坐标</param>
        /// <param name="y">The y-coordinate of the point/点的y坐标</param>
        /// <returns>true if contained; otherwise false/如果包含则返回true；否则返回false</returns>
        public readonly bool Contains(float x, float y)
            => (X <= x && Y <= y && X + Width > x && Y + Height > y);

        /// <summary>
        /// Determines if the specified point is contained within this rectangle
        /// 确定指定点是否包含在此矩形内
        /// </summary>
        /// <param name="pt">The point to test/要测试的点</param>
        /// <returns>true if contained; otherwise false/如果包含则返回true；否则返回false</returns>
        public readonly bool Contains(PointF pt)
            => Contains(pt.X, pt.Y);

        /// <summary>
        /// Determines if the specified rectangle is entirely contained within this rectangle
        /// 确定指定矩形是否完全包含在此矩形内
        /// </summary>
        /// <param name="rect">The rectangle to test/要测试的矩形</param>
        /// <returns>true if fully contained; otherwise false/如果完全包含则返回true；否则返回false</returns>
        public readonly bool Contains(RectF rect) =>
            X <= rect.X &&
            (rect.X + rect.Width) <= (X + Width) &&
            Y <= rect.Y &&
            (rect.Y + rect.Height) <= (Y + Height);

        /// <summary>
        /// Expands or shrinks the rectangle by the specified amount
        /// 按指定量扩展或收缩矩形
        /// </summary>
        /// <param name="width">The horizontal expansion amount/水平扩展量</param>
        /// <param name="height">The vertical expansion amount/垂直扩展量</param>
        public void Inflate(float width, float height)
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
        public void Inflate(SizeF size)
            => Inflate(size.Width, size.Height);

        /// <summary>
        /// Creates a rectangle that results from expanding the specified rectangle
        /// 创建通过扩展指定矩形而产生的新矩形
        /// </summary>
        /// <param name="rect">The rectangle to inflate/要扩展的矩形</param>
        /// <param name="x">The horizontal expansion amount/水平扩展量</param>
        /// <param name="y">The vertical expansion amount/垂直扩展量</param>
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
        /// <returns>The intersection rectangle; empty if no intersection/相交矩形；如果没有相交则返回空矩形</returns>
        public static RectF Intersect(RectF a, RectF b)
        {
            var x1 = Math.Max(a.X, b.X);
            var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Max(a.Y, b.Y);
            var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            if (x2 >= x1 && y2 >= y1)
                return new RectF(x1, y1, x2 - x1, y2 - y1);
            return default;
        }

        /// <summary>
        /// Computes the intersection between this rectangle and another
        /// 计算此矩形与另一个矩形的交集
        /// </summary>
        /// <param name="rect">The rectangle to intersect with/要相交的矩形</param>
        /// <returns>The intersection rectangle; empty if no intersection/相交矩形；如果没有相交则返回空矩形</returns>
        public readonly RectF Intersect(RectF rect)
            => Intersect(this, rect);

        /// <summary>
        /// Determines if this rectangle intersects with another rectangle
        /// 确定此矩形是否与另一个矩形相交
        /// </summary>
        /// <param name="rect">The rectangle to test/要测试的矩形</param>
        /// <returns>true if intersecting; otherwise false/如果相交则返回true；否则返回false</returns>
        public readonly bool IntersectsWith(RectF rect) =>
            (X < rect.X + rect.Width) &&
            (X + Width > rect.X) &&
            (Y < rect.Y + rect.Height) &&
            (Y + Height > rect.Y);

        /// <summary>
        /// Computes the smallest rectangle that contains both this rectangle and another
        /// 计算包含此矩形和另一个矩形的最小矩形
        /// </summary>
        /// <param name="rect">The rectangle to union with/要合并的矩形</param>
        /// <returns>The union rectangle/并集矩形</returns>
        public readonly RectF Union(RectF rect)
            => Union(this, rect);

        /// <summary>
        /// Computes the smallest rectangle that contains both rectangles
        /// 计算包含两个矩形的最小矩形
        /// </summary>
        /// <param name="a">First rectangle/第一个矩形</param>
        /// <param name="b">Second rectangle/第二个矩形</param>
        /// <returns>The union rectangle/并集矩形</returns>
        public static RectF Union(RectF a, RectF b)
        {
            var x1 = Math.Min(a.X, b.X);
            var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Min(a.Y, b.Y);
            var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

            return new RectF(x1, y1, x2 - x1, y2 - y1);
        }

        #endregion

        /// <summary>
        /// Returns a string representation of the rectangle
        /// 返回矩形的字符串表示形式
        /// </summary>
        /// <returns>A formatted string showing coordinates and dimensions/显示坐标和尺寸的格式化字符串</returns>
        public override string ToString()
        {
            return $"Rect(X={X:F2}, Y={Y:F2}, Width={Width:F2}, Height={Height:F2})";
        }

        /// <summary>
        /// Determines if this rectangle equals another rectangle
        /// 确定此矩形是否等于另一个矩形
        /// </summary>
        /// <param name="other">The rectangle to compare with/要比较的矩形</param>
        /// <returns>true if equal; otherwise false/如果相等则返回true；否则返回false</returns>
        public bool Equals(Rect other) =>
            X == other.X &&
            Y == other.Y &&
            Width == other.Width &&
            Height == other.Height;
    }

}
