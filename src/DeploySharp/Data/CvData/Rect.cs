using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct Rect(int X, int Y, int Width, int Height)
    {

        public int X = X;
        public int Y = Y;

        public int Width = Width;

        public int Height = Height;


        #region Properties


        public int Top
        {
            readonly get => Y;
            set => Y = value;
        }

        public int Bottom => Y + Height;


        public int Left
        {
            get => X;
            set => X = value;
        }

        public int Right => X + Width;


        public Point Location
        {
            get => new(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }


        public Size Size
        {
            get => new(Width, Height);
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        public Point TopLeft => new(X, Y);

        public Point BottomRight => new(X + Width, Y + Height);

        #endregion

        public Rect(Point location, Size size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
        }


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
                throw new ArgumentException("right > left");
            if (r.Height < 0)
                throw new ArgumentException("bottom > top");
            return r;
        }

        #region Operators


       
        public static Rect operator +(Rect rect, Point pt) => rect.Add(pt);


        public readonly Rect Add(Point pt)
            => this with
            {
                X = X + pt.X,
                Y = Y + pt.Y
            };

        public static Rect operator -(Rect rect, Point pt)
            => rect with
            {
                X = rect.X - pt.X,
                Y = rect.Y - pt.Y
            };

        public readonly Rect Subtract(Point pt)
            => this with
            {
                X = X - pt.X,
                Y = Y - pt.Y
            };


        public static Rect operator +(Rect rect, Size size) =>
            rect with
            {
                Width = rect.Width + size.Width,
                Height = rect.Height + size.Height
            };


        public readonly Rect Add(Size size) => this with
        {
            Width = Width + size.Width,
            Height = Height + size.Height
        };

        public static Rect operator -(Rect rect, Size size) =>
            rect with
            {
                Width = rect.Width - size.Width,
                Height = rect.Height - size.Height
            };

        public readonly Rect Subtract(Size size)
            => this with
            {
                Width = Width - size.Width,
                Height = Height - size.Height
            };




        public static Rect operator &(Rect a, Rect b)
            => Intersect(a, b);


        public static Rect operator |(Rect a, Rect b)
            => Union(a, b);



        #endregion

       

        #region Methods


        public readonly bool Contains(int x, int y)
            => (X <= x && Y <= y && X + Width > x && Y + Height > y);

        public readonly bool Contains(Point pt)
            => Contains(pt.X, pt.Y);

        public readonly bool Contains(Rect rect) =>
            X <= rect.X &&
            (rect.X + rect.Width) <= (X + Width) &&
            Y <= rect.Y &&
            (rect.Y + rect.Height) <= (Y + Height);


        public void Inflate(int width, int height)
        {
            X -= width;
            Y -= height;
            Width += (2 * width);
            Height += (2 * height);
        }


        public void Inflate(Size size) => Inflate(size.Width, size.Height);

        public static Rect Inflate(Rect rect, int x, int y)
        {
            rect.Inflate(x, y);
            return rect;
        }

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

        
        public readonly Rect Intersect(Rect rect) => Intersect(this, rect);

     
        public readonly bool IntersectsWith(Rect rect) =>
            (X < rect.X + rect.Width) &&
            (X + Width > rect.X) &&
            (Y < rect.Y + rect.Height) &&
            (Y + Height > rect.Y);

        public readonly Rect Union(Rect rect) => Union(this, rect);
        public static Rect Union(Rect a, Rect b)
        {
            var x1 = Math.Min(a.X, b.X);
            var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Min(a.Y, b.Y);
            var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        #endregion
    }
}
