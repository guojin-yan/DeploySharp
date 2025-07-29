using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct PointD(double X, double Y)
    {

        public double X = X;

        public double Y = Y;


        #region Operators


        public readonly PointD Plus() => this;


        public static PointD operator +(PointD pt) => pt;


        public readonly PointD Negate() => new(-X, -Y);

        public static PointD operator -(PointD pt) => pt.Negate();

        public readonly PointD Add(PointD p) => new(X + p.X, Y + p.Y);


        public static PointD operator +(PointD p1, PointD p2) => p1.Add(p2);

        public readonly PointD Subtract(PointD p) => new(X - p.X, Y - p.Y);
        public static PointD operator -(PointD p1, PointD p2) => p1.Subtract(p2);
        public readonly PointD Multiply(double scale) => new(X * scale, Y * scale);
        public static PointD operator *(PointD pt, double scale) => pt.Multiply(scale);

        #endregion

        #region Methods
        public static double Distance(PointD p1, PointD p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        public readonly double DistanceTo(PointD p)
        {
            return Distance(this, p);
        }
        public static double DotProduct(PointD p1, PointD p2)
        {
            return p1.X * p2.X + p1.Y * p2.Y;
        }
        public readonly double DotProduct(PointD p)
        {
            return DotProduct(this, p);
        }

        public static double CrossProduct(PointD p1, PointD p2)
        {
            return p1.X * p2.Y - p2.X * p1.Y;
        }
        public readonly double CrossProduct(PointD p)
        {
            return CrossProduct(this, p);
        }

        #endregion
    }
}
