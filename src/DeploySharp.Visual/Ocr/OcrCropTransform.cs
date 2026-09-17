using System;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Controls the geometric transform used for OCR crops. / 控制 OCR 裁剪使用的几何变换。</summary>
    public enum OcrCropTransformMode
    {
        /// <summary>Always map all four corners with a projective transform. / 始终使用四角透视变换。</summary>
        Perspective = 0,
        /// <summary>Use affine mapping only when the quadrilateral is demonstrably a near-parallelogram. / 仅在四边形明确接近平行四边形时使用仿射映射。</summary>
        AffineWhenEquivalent = 1
    }

    /// <summary>Reports whether a quadrilateral is safe for the optional affine fast path. / 判断四边形是否适合可选仿射快速路径。</summary>
    public static class OcrCropTransformPolicy
    {
        /// <summary>Gets the normalized closure error without allocating a diagnostic object. / 获取归一化闭合误差，不分配诊断对象。</summary>
        public static double GetParallelogramError(TextQuadrilateral quadrilateral)
        {
            if (quadrilateral == null) throw new ArgumentNullException(nameof(quadrilateral));
            PointF topLeft = quadrilateral.TopLeft;
            PointF topRight = quadrilateral.TopRight;
            PointF bottomRight = quadrilateral.BottomRight;
            PointF bottomLeft = quadrilateral.BottomLeft;
            double maxEdge = Math.Max(Distance(topLeft, topRight), Distance(topRight, bottomRight));
            maxEdge = Math.Max(maxEdge, Distance(bottomRight, bottomLeft));
            maxEdge = Math.Max(maxEdge, Distance(bottomLeft, topLeft));
            if (maxEdge <= 0) return double.PositiveInfinity;
            double closureX = (bottomRight.X - topRight.X) - (bottomLeft.X - topLeft.X);
            double closureY = (bottomRight.Y - topRight.Y) - (bottomLeft.Y - topLeft.Y);
            return Math.Sqrt(closureX * closureX + closureY * closureY) / maxEdge;
        }

        /// <summary>Checks closure and the normalized affine condition; angle alone is never used. / 检查闭合误差和归一化仿射条件数；绝不只按角度判断。</summary>
        public static bool IsAffineEquivalent(TextQuadrilateral quadrilateral, double maximumParallelogramError = 0.0001, double maximumAffineCondition = 10000)
        {
            if (quadrilateral == null) throw new ArgumentNullException(nameof(quadrilateral));
            if (double.IsNaN(maximumParallelogramError) || double.IsInfinity(maximumParallelogramError) || maximumParallelogramError < 0) throw new ArgumentOutOfRangeException(nameof(maximumParallelogramError));
            if (double.IsNaN(maximumAffineCondition) || double.IsInfinity(maximumAffineCondition) || maximumAffineCondition < 2) throw new ArgumentOutOfRangeException(nameof(maximumAffineCondition));
            PointF topLeft = quadrilateral.TopLeft;
            PointF topRight = quadrilateral.TopRight;
            PointF bottomRight = quadrilateral.BottomRight;
            PointF bottomLeft = quadrilateral.BottomLeft;
            double x1 = topRight.X - topLeft.X, y1 = topRight.Y - topLeft.Y;
            double x2 = bottomLeft.X - topLeft.X, y2 = bottomLeft.Y - topLeft.Y;
            double oppositeX = (bottomRight.X - topRight.X) - x2;
            double oppositeY = (bottomRight.Y - topRight.Y) - y2;
            double maxEdge = Math.Max(Distance(topLeft, topRight), Distance(topRight, bottomRight));
            maxEdge = Math.Max(maxEdge, Distance(bottomRight, bottomLeft));
            maxEdge = Math.Max(maxEdge, Distance(bottomLeft, topLeft));
            if (maxEdge <= 0) return false;
            double error = Math.Sqrt(oppositeX * oppositeX + oppositeY * oppositeY) / maxEdge;
            if (error > maximumParallelogramError) return false;
            double determinant = (x1 * y2) - (y1 * x2);
            if (Math.Abs(determinant) <= maxEdge * maxEdge * 1e-12) return false;
            // For A=[edgeTop edgeLeft], ||A||F * ||A^-1||F simplifies to
            // (sum(Aij²))/|det(A)|. It is invariant to translation and scale.
            double condition = (x1 * x1) + (y1 * y1) + (x2 * x2) + (y2 * y2);
            condition /= Math.Abs(determinant);
            return condition <= maximumAffineCondition;
        }

        private static double Distance(PointF first, PointF second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
