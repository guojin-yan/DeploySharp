using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct RectF(float X, float Y, float Width, float Height)
    {

        public float X = X;


        public float Y = Y;

        public float Width = Width;

        public float Height = Height;

        public RectF(PointF location, SizeF size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
            X = location.X;
            Y = location.Y;
            Width = size.Width;
            Height = size.Height;
        }


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
                throw new ArgumentException("right > left");
            if (r.Height < 0)
                throw new ArgumentException("bottom > top");
            return r;
        }

        #region Operators

        public readonly RectF Add(PointF pt)
            => this with
            {
                X = X + pt.X,
                Y = Y + pt.Y
            };


        public static RectF operator +(RectF rect, PointF pt)
            => rect.Add(pt);
        public readonly RectF Subtract(PointF pt)
            => this with
            {
                X = X - pt.X,
                Y = Y - pt.Y
            };

        public static RectF operator -(RectF rect, PointF pt)
            => rect.Subtract(pt);

        public readonly RectF Add(SizeF size)
            => this with
            {
                Width = Width + size.Width,
                Height = Height + size.Height
            };

        public static RectF operator +(RectF rect, SizeF size)
            => rect.Add(size);

        public readonly RectF Subtract(SizeF size)
            => this with
            {
                Width = Width - size.Width,
                Height = Height - size.Height
            };

        public static RectF operator -(RectF rect, SizeF size)
            => rect.Subtract(size);


        public static RectF operator &(RectF a, RectF b)
            => Intersect(a, b);


        public static RectF operator |(RectF a, RectF b)
            => Union(a, b);


        #endregion

        #region Properties

        public float Top
        {
            readonly get => Y;
            set => Y = value;
        }

        public readonly float Bottom => Y + Height;


        public float Left
        {
            readonly get => X;
            set => X = value;
        }


        public readonly float Right => X + Width;

        public PointF Location
        {
            readonly get => new(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        public SizeF Size
        {
            readonly get => new(Width, Height);
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        public readonly PointF TopLeft => new(X, Y);

        public readonly PointF BottomRight => new(X + Width, Y + Height);

        #endregion

        #region Methods


        public readonly bool Contains(float x, float y)
            => (X <= x && Y <= y && X + Width > x && Y + Height > y);

        public readonly bool Contains(PointF pt)
            => Contains(pt.X, pt.Y);

        public readonly bool Contains(RectF rect) =>
            X <= rect.X &&
            (rect.X + rect.Width) <= (X + Width) &&
            Y <= rect.Y &&
            (rect.Y + rect.Height) <= (Y + Height);


        public void Inflate(float width, float height)
        {
            X -= width;
            Y -= height;
            Width += (2 * width);
            Height += (2 * height);
        }


        public void Inflate(SizeF size)
            => Inflate(size.Width, size.Height);


        public static Rect Inflate(Rect rect, int x, int y)
        {
            rect.Inflate(x, y);
            return rect;
        }

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

        public readonly RectF Intersect(RectF rect)
            => Intersect(this, rect);


        public readonly bool IntersectsWith(RectF rect) =>
            (X < rect.X + rect.Width) &&
            (X + Width > rect.X) &&
            (Y < rect.Y + rect.Height) &&
            (Y + Height > rect.Y);


        public readonly RectF Union(RectF rect)
            => Union(this, rect);

        public static RectF Union(RectF a, RectF b)
        {
            var x1 = Math.Min(a.X, b.X);
            var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Min(a.Y, b.Y);
            var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

            return new RectF(x1, y1, x2 - x1, y2 - y1);
        }

        #endregion
    }
}
