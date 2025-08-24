using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct PointF(float X, float Y)
    {
  
        public float X = X;


        public float Y = Y;


        #region Operators

        public readonly PointF Plus() => this;


        public static PointF operator +(PointF pt)
        {
            return pt;
        }

        public readonly PointF Negate() => new(-X, -Y);

        public static PointF operator -(PointF pt) => pt.Negate();


        public readonly PointF Add(PointF p) => new(X + p.X, Y + p.Y);


        public static PointF operator +(PointF p1, PointF p2) => p1.Add(p2);


        public readonly PointF Subtract(PointF p) => new(X - p.X, Y - p.Y);


        public static PointF operator -(PointF p1, PointF p2) => p1.Subtract(p2);

     
        public readonly PointF Multiply(double scale) => new((float)(X * scale), (float)(Y * scale));


        public static PointF operator *(PointF pt, double scale) => pt.Multiply(scale);

        #endregion

        #region Methods


        public static double Distance(PointF p1, PointF p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }


        public readonly double DistanceTo(PointF p)
        {
            return Distance(this, p);
        }


        public static double DotProduct(PointF p1, PointF p2)
        {
            return p1.X * p2.X + p1.Y * p2.Y;
        }


        public readonly double DotProduct(PointF p)
        {
            return DotProduct(this, p);
        }


        public static double CrossProduct(PointF p1, PointF p2)
        {
            return p1.X * p2.Y - p2.X * p1.Y;
        }

        public readonly double CrossProduct(PointF p)
        {
            return CrossProduct(this, p);
        }

        #endregion
    }
}
