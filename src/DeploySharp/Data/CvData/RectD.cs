using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data.CvData
{
    public record struct RectD(double X, double Y, double Width, double Height)
    {

        public double X = X;

        public double Y = Y;


        public double Width = Width;

        public double Height = Height;


        public RectD(PointD location, SizeD size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
            X = location.X;
            Y = location.Y;
            Width = size.Width;
            Height = size.Height;
        }

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

        public static RectD operator +(RectD rect, PointD pt)
            => rect.Add(pt);


        public readonly RectD Add(PointD pt)
            => this with
            {
                X = X + pt.X,
                Y = Y + pt.Y
            };

        public static RectD operator -(RectD rect, PointD pt)
            => rect.Subtract(pt);


        public readonly RectD Subtract(PointD pt)
            => this with
            {
                X = X - pt.X,
                Y = Y - pt.Y
            };

        public static RectD operator +(RectD rect, SizeD size)
            => rect with
            {
                Width = rect.Width + size.Width,
                Height = rect.Height + size.Height
            };

        public readonly RectD Add(SizeD size)
            => this with
            {
                Width = Width + size.Width,
                Height = Height + size.Height
            };

        public static RectD operator -(RectD rect, SizeD size)
            => rect with
            {
                Width = rect.Width - size.Width,
                Height = rect.Height - size.Height
            };

        public readonly RectD Subtract(SizeD size)
            => this with
            {
                Width = Width - size.Width,
                Height = Height - size.Height
            };

        public static RectD operator &(RectD a, RectD b)
            => Intersect(a, b);


        public static RectD operator |(RectD a, RectD b)
            => Union(a, b);

        #endregion


        #region Properties

        public double Top
        {
            readonly get => Y;
            set => Y = value;
        }


        public readonly double Bottom => Y + Height;


        public double Left
        {
            readonly get => X;
            set => X = value;
        }

        public readonly double Right => X + Width;


        public PointD Location
        {
            readonly get => new(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }


        public SizeD Size
        {
            readonly get => new(Width, Height);
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }


        public readonly PointD TopLeft => new(X, Y);


        public readonly PointD BottomRight => new(X + Width, Y + Height);

        #endregion

        #region Methods

        public readonly Rect ToRect()
            => new((int)X, (int)Y, (int)Width, (int)Height);


        public readonly bool Contains(double x, double y)
            => (X <= x && Y <= y && X + Width > x && Y + Height > y);


        public readonly bool Contains(PointD pt)
            => Contains(pt.X, pt.Y);

        public readonly bool Contains(RectD rect) =>
            X <= rect.X &&
            (rect.X + rect.Width) <= (X + Width) &&
            Y <= rect.Y &&
            (rect.Y + rect.Height) <= (Y + Height);


        public void Inflate(double width, double height)
        {
            X -= width;
            Y -= height;
            Width += (2 * width);
            Height += (2 * height);
        }


        public void Inflate(SizeD size) => Inflate(size.Width, size.Height);


        public static Rect Inflate(Rect rect, int x, int y)
        {
            rect.Inflate(x, y);
            return rect;
        }


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

        public readonly RectD Intersect(RectD rect) => Intersect(this, rect);


        public readonly bool IntersectsWith(RectD rect) =>
            (X < rect.X + rect.Width) &&
            (X + Width > rect.X) &&
            (Y < rect.Y + rect.Height) &&
            (Y + Height > rect.Y);

        public readonly RectD Union(RectD rect) => Union(this, rect);


        public static RectD Union(RectD a, RectD b)
        {
            var x1 = Math.Min(a.X, b.X);
            var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Min(a.Y, b.Y);
            var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

            return new RectD(x1, y1, x2 - x1, y2 - y1);
        }

        #endregion
    }

}
