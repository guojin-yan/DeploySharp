using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public record struct Point(int X, int Y)
    {
        /// <summary>
        /// 
        /// </summary>
        public int X = X;

        /// <summary>
        /// 
        /// </summary>
        public int Y = Y;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public Point(double x, double y)
            : this((int)x, (int)y)
        {
        }


        #region Operators

        public readonly Point Plus() => this;


        public static Point operator +(Point pt) => pt;


        public readonly Point Negate() => new(-X, -Y);


        public static Point operator -(Point pt) => pt.Negate();


        public readonly Point Add(Point p) => new(X + p.X, Y + p.Y);


        public static Point operator +(Point p1, Point p2) => p1.Add(p2);

        public readonly Point Subtract(Point p) => new(X - p.X, Y - p.Y);


        public static Point operator -(Point p1, Point p2) => p1.Subtract(p2);


        public readonly Point Multiply(double scale) => new(X * scale, Y * scale);

 
        public static Point operator *(Point pt, double scale) => pt.Multiply(scale);

        #endregion

        #region Methods


        public static double Distance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }


        public readonly double DistanceTo(Point p)
        {
            return Distance(this, p);
        }


        public static double DotProduct(Point p1, Point p2)
        {
            return p1.X * p2.X + p1.Y * p2.Y;
        }

        public readonly double DotProduct(Point p)
        {
            return DotProduct(this, p);
        }


        public static double CrossProduct(Point p1, Point p2)
        {
            return p1.X * p2.Y - p2.X * p1.Y;
        }


        public readonly double CrossProduct(Point p)
        {
            return CrossProduct(this, p);
        }

        #endregion

        public override string ToString() => $"Point(X={X}, Y={Y})";
    }
}
