using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct RotatedRect
    {

        public PointF Center;

        public SizeF Size;

        public float Angle;

        public RotatedRect(PointF center, SizeF size, float angle)
        {
            Center = center;
            Size = size;
            Angle = angle;
        }
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


        public readonly PointF[] Points()
        {
            var angle = Angle * Math.PI / 180.0;
            var b = (float)Math.Cos(angle) * 0.5f;
            var a = (float)Math.Sin(angle) * 0.5f;

            var pt = new PointF[4];
            pt[0].X = Center.X - a * Size.Height - b * Size.Width;
            pt[0].Y = Center.Y + b * Size.Height - a * Size.Width;
            pt[1].X = Center.X + a * Size.Height - b * Size.Width;
            pt[1].Y = Center.Y - b * Size.Height - a * Size.Width;
            pt[2].X = 2 * Center.X - pt[0].X;
            pt[2].Y = 2 * Center.Y - pt[0].Y;
            pt[3].X = 2 * Center.X - pt[1].X;
            pt[3].Y = 2 * Center.Y - pt[1].Y;
            return pt;
        }

        public readonly Rect BoundingRect()
        {
            var pt = Points();
            var r = new Rect
            {
                X = (int)Math.Floor(Math.Min(Math.Min(Math.Min(pt[0].X, pt[1].X), pt[2].X), pt[3].X)),
                Y = (int)Math.Floor(Math.Min(Math.Min(Math.Min(pt[0].Y, pt[1].Y), pt[2].Y), pt[3].Y)),
                Width = (int)Math.Ceiling(Math.Max(Math.Max(Math.Max(pt[0].X, pt[1].X), pt[2].X), pt[3].X)),
                Height = (int)Math.Ceiling(Math.Max(Math.Max(Math.Max(pt[0].Y, pt[1].Y), pt[2].Y), pt[3].Y))
            };
            r.Width -= r.X - 1;
            r.Height -= r.Y - 1;
            return r;
        }
    }
}
