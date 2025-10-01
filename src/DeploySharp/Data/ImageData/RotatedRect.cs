using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    //public record struct RotatedRect
    //{

    //    public PointF Center;

    //    public SizeF Size;

    //    public float Angle;

    //    public RotatedRect(PointF center, SizeF size, float angle)
    //    {
    //        Center = center;
    //        Size = size;
    //        Angle = angle;
    //    }
    //    public static RotatedRect FromAxisAlignedRect(RectF rect, float angle)
    //    {
    //        var center = new PointF(
    //            X: rect.X + rect.Width / 2f,
    //            Y: rect.Y + rect.Height / 2f);

    //        return new RotatedRect(
    //            center: center,
    //            size: new SizeF(rect.Width, rect.Height),
    //            angle: angle);
    //    }


    //    public readonly PointF[] Points()
    //    {
    //        var angle = Angle * Math.PI / 180.0;
    //        var b = (float)Math.Cos(angle) * 0.5f;
    //        var a = (float)Math.Sin(angle) * 0.5f;

    //        var pt = new PointF[4];
    //        pt[0].X = Center.X - a * Size.Height - b * Size.Width;
    //        pt[0].Y = Center.Y + b * Size.Height - a * Size.Width;
    //        pt[1].X = Center.X + a * Size.Height - b * Size.Width;
    //        pt[1].Y = Center.Y - b * Size.Height - a * Size.Width;
    //        pt[2].X = 2 * Center.X - pt[0].X;
    //        pt[2].Y = 2 * Center.Y - pt[0].Y;
    //        pt[3].X = 2 * Center.X - pt[1].X;
    //        pt[3].Y = 2 * Center.Y - pt[1].Y;
    //        return pt;
    //    }

    //    public readonly Rect BoundingRect()
    //    {
    //        var pt = Points();
    //        var r = new Rect
    //        {
    //            X = (int)Math.Floor(Math.Min(Math.Min(Math.Min(pt[0].X, pt[1].X), pt[2].X), pt[3].X)),
    //            Y = (int)Math.Floor(Math.Min(Math.Min(Math.Min(pt[0].Y, pt[1].Y), pt[2].Y), pt[3].Y)),
    //            Width = (int)Math.Ceiling(Math.Max(Math.Max(Math.Max(pt[0].X, pt[1].X), pt[2].X), pt[3].X)),
    //            Height = (int)Math.Ceiling(Math.Max(Math.Max(Math.Max(pt[0].Y, pt[1].Y), pt[2].Y), pt[3].Y))
    //        };
    //        r.Width -= r.X - 1;
    //        r.Height -= r.Y - 1;
    //        return r;
    //    }

    //    public override string ToString()
    //    {
    //        return $"Center: [{Center.X:F1}, {Center.Y:F1}] " + $"Size: {Size.Width:F1}x{Size.Height:F1} " + $"Angle: {Angle:F1}°";
    //    }
    //}

    /// <summary>
    /// Represents a rectangle that can be rotated around its center point
    /// 表示可围绕其中心点旋转的矩形
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stores rotation information and provides methods to calculate rotated corners and bounding boxes.
    /// Rotation is performed clockwise around the center point.
    /// </para>
    /// <para>
    /// 存储旋转信息并提供计算旋转角和边界框的方法。
    /// 旋转围绕中心点顺时针进行。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var rect = new RotatedRect(new PointF(100, 100), new SizeF(200, 100), 45);
    /// PointF[] corners = rect.Points();
    /// Rect boundingBox = rect.BoundingRect();
    /// </code>
    /// </example>
    /// </remarks>
    public record struct RotatedRect
    {
        /// <summary>
        /// The center point of the rectangle
        /// 矩形的中心点
        /// </summary>
        /// <value>Floating-point coordinates of the center</value>
        public PointF Center;

        /// <summary>
        /// The dimensions (width and height) of the rectangle
        /// 矩形的尺寸（宽度和高度）
        /// </summary>
        /// <value>Floating-point width and height</value>
        public SizeF Size;

        /// <summary>
        /// The rotation angle in degrees
        /// 旋转角度（以度为单位）
        /// </summary>
        /// <value>Rotation angle measured clockwise from horizontal</value>
        public float Angle;

        /// <summary>
        /// Initializes a new rotated rectangle
        /// 初始化一个新的旋转矩形
        /// </summary>
        /// <param name="center">Center point of rectangle 矩形中心点</param>
        /// <param name="size">Dimensions of rectangle 矩形尺寸</param>
        /// <param name="angle">Rotation angle in degrees 旋转角度（度）</param>
        public RotatedRect(PointF center, SizeF size, float angle)
        {
            Center = center;
            Size = size;
            Angle = angle;
        }

        /// <summary>
        /// Creates a rotated rectangle from an axis-aligned rectangle
        /// 从轴对齐矩形创建旋转矩形
        /// </summary>
        /// <param name="rect">Axis-aligned source rectangle 轴对齐的源矩形</param>
        /// <param name="angle">Rotation angle in degrees 旋转角度（度）</param>
        /// <returns>New RotatedRect instance</returns>
        public static RotatedRect FromAxisAlignedRect(RectF rect, float angle)
        {
            var center = new PointF(
                X: rect.X + rect.Width / 2f,
                Y: rect.Y + rect.Height / 2f);

            return new RotatedRect(
                center: center,
                size: new SizeF(rect.Width, rect.Height),
                angle: angle);
        }

        /// <summary>
        /// Computes the four corner points of the rotated rectangle
        /// 计算旋转矩形的四个角点
        /// </summary>
        /// <returns>
        /// Array of 4 points in clockwise order:
        /// [0] top-left, [1] top-right, [2] bottom-right, [3] bottom-left
        /// 
        /// 顺时针顺序的4个点数组：
        /// [0] 左上, [1] 右上, [2] 右下, [3] 左下
        /// </returns>
        public readonly PointF[] Points()
        {
            var angle = Angle * Math.PI / 180.0;
            var cosAngle = (float)Math.Cos(angle) * 0.5f;
            var sinAngle = (float)Math.Sin(angle) * 0.5f;

            var pt = new PointF[4];

            // Calculate coordinates using rotation matrix transformation
            // 使用旋转矩阵变换计算坐标
            pt[0].X = Center.X - sinAngle * Size.Height - cosAngle * Size.Width;
            pt[0].Y = Center.Y + cosAngle * Size.Height - sinAngle * Size.Width;
            pt[1].X = Center.X + sinAngle * Size.Height - cosAngle * Size.Width;
            pt[1].Y = Center.Y - cosAngle * Size.Height - sinAngle * Size.Width;
            pt[2].X = 2 * Center.X - pt[0].X;
            pt[2].Y = 2 * Center.Y - pt[0].Y;
            pt[3].X = 2 * Center.X - pt[1].X;
            pt[3].Y = 2 * Center.Y - pt[1].Y;

            return pt;
        }

        /// <summary>
        /// Calculates the axis-aligned bounding rectangle that encompasses the rotated rectangle
        /// 计算包含旋转矩形的最小轴对齐边界矩形
        /// </summary>
        /// <returns>
        /// A Rect structure that bounds the rotated rectangle.
        /// Coordinates are snapped to integer pixel boundaries (floor for min, ceiling for max).
        /// 
        /// 包含旋转矩形的最小矩形边界。
        /// 坐标被对齐到整数像素边界（下限为最小，上限为最大）。
        /// </returns>
        public readonly Rect BoundingRect()
        {
            var pt = Points();
            var r = new Rect
            {
                // Find minimum and maximum coordinates
                // 查找最小和最大坐标
                X = (int)Math.Floor(Math.Min(Math.Min(Math.Min(pt[0].X, pt[1].X), pt[2].X), pt[3].X)),
                Y = (int)Math.Floor(Math.Min(Math.Min(Math.Min(pt[0].Y, pt[1].Y), pt[2].Y), pt[3].Y)),
                Width = (int)Math.Ceiling(Math.Max(Math.Max(Math.Max(pt[0].X, pt[1].X), pt[2].X), pt[3].X)),
                Height = (int)Math.Ceiling(Math.Max(Math.Max(Math.Max(pt[0].Y, pt[1].Y), pt[2].Y), pt[3].Y))
            };

            // Convert max points to width/height
            // 将最大点转换为宽高
            r.Width -= r.X;
            r.Height -= r.Y;

            return r;
        }

        /// <summary>
        /// Returns a formatted string representation of the rotated rectangle
        /// 返回旋转矩形的格式化字符串表示
        /// </summary>
        /// <returns>String showing center, size, and angle</returns>
        public override string ToString()
        {
            return $"Center: [{Center.X:F1}, {Center.Y:F1}] " +
                   $"Size: {Size.Width:F1}x{Size.Height:F1} " +
                   $"Angle: {Angle:F1}°";
        }
    }

}
