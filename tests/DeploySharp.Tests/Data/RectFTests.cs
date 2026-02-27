using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class RectFTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithFloatParameters_ShouldSetCorrectValues()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.X.Should().Be(10.5f);
            rect.Y.Should().Be(20.5f);
            rect.Width.Should().Be(100.5f);
            rect.Height.Should().Be(50.5f);
        }

        [Fact]
        public void Constructor_WithPointFAndSizeF_ShouldSetCorrectValues()
        {
            var location = new PointF(10.5f, 20.5f);
            var size = new SizeF(100.5f, 50.5f);

            var rect = new RectF(location, size);

            rect.X.Should().Be(10.5f);
            rect.Y.Should().Be(20.5f);
            rect.Width.Should().Be(100.5f);
            rect.Height.Should().Be(50.5f);
        }

        [Fact]
        public void FromLTRB_WithValidCoordinates_ShouldCreateRect()
        {
            var rect = RectF.FromLTRB(10.5f, 20.5f, 110.5f, 70.5f);

            rect.X.Should().Be(10.5f);
            rect.Y.Should().Be(20.5f);
            rect.Width.Should().Be(100.0f);
            rect.Height.Should().Be(50.0f);
        }

        [Fact]
        public void FromLTRB_WithRightLessThanLeft_ShouldThrowArgumentException()
        {
            Action act = () => RectF.FromLTRB(110.5f, 20.5f, 10.5f, 70.5f);

            act.Should().Throw<ArgumentException>().WithMessage("Right must be greater than left");
        }

        [Fact]
        public void FromLTRB_WithBottomLessThanTop_ShouldThrowArgumentException()
        {
            Action act = () => RectF.FromLTRB(10.5f, 70.5f, 110.5f, 20.5f);

            act.Should().Throw<ArgumentException>().WithMessage("Bottom must be greater than top");
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Top_ShouldReturnY()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.Top.Should().Be(20.5f);
        }

        [Fact]
        public void Bottom_ShouldReturnYPlusHeight()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.Bottom.Should().Be(71.0f);
        }

        [Fact]
        public void Left_ShouldReturnX()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.Left.Should().Be(10.5f);
        }

        [Fact]
        public void Right_ShouldReturnXPlusWidth()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.Right.Should().Be(111.0f);
        }

        [Fact]
        public void Location_ShouldReturnTopLeftPoint()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.Location.Should().Be(new PointF(10.5f, 20.5f));
        }

        [Fact]
        public void Size_ShouldReturnSize()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.Size.Should().Be(new SizeF(100.5f, 50.5f));
        }

        [Fact]
        public void TopLeft_ShouldReturnTopLeftPoint()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.TopLeft.Should().Be(new PointF(10.5f, 20.5f));
        }

        [Fact]
        public void BottomRight_ShouldReturnBottomRightPoint()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.BottomRight.Should().Be(new PointF(111.0f, 71.0f));
        }

        #endregion

        #region Operator Tests

        [Fact]
        public void AddOperator_WithPointF_ShouldTranslateRect()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);
            var point = new PointF(5.5f, 10.5f);

            var result = rect + point;

            result.X.Should().Be(16.0f);
            result.Y.Should().Be(31.0f);
            result.Width.Should().Be(100.5f);
            result.Height.Should().Be(50.5f);
        }

        [Fact]
        public void SubtractOperator_WithPointF_ShouldTranslateRect()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);
            var point = new PointF(5.5f, 10.5f);

            var result = rect - point;

            result.X.Should().Be(5.0f);
            result.Y.Should().Be(10.0f);
            result.Width.Should().Be(100.5f);
            result.Height.Should().Be(50.5f);
        }

        [Fact]
        public void AddOperator_WithSizeF_ShouldExpandRect()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);
            var size = new SizeF(20.5f, 10.5f);

            var result = rect + size;

            result.X.Should().Be(10.5f);
            result.Y.Should().Be(20.5f);
            result.Width.Should().Be(121.0f);
            result.Height.Should().Be(61.0f);
        }

        [Fact]
        public void SubtractOperator_WithSizeF_ShouldShrinkRect()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);
            var size = new SizeF(20.5f, 10.5f);

            var result = rect - size;

            result.X.Should().Be(10.5f);
            result.Y.Should().Be(20.5f);
            result.Width.Should().Be(80.0f);
            result.Height.Should().Be(40.0f);
        }

        [Fact]
        public void IntersectionOperator_WithOverlappingRects_ShouldReturnIntersection()
        {
            var r1 = new RectF(0f, 0f, 100f, 100f);
            var r2 = new RectF(50f, 50f, 100f, 100f);

            var result = r1 & r2;

            result.X.Should().Be(50f);
            result.Y.Should().Be(50f);
            result.Width.Should().Be(50f);
            result.Height.Should().Be(50f);
        }

        [Fact]
        public void UnionOperator_WithTwoRects_ShouldReturnBoundingRect()
        {
            var r1 = new RectF(0f, 0f, 50f, 50f);
            var r2 = new RectF(100f, 100f, 50f, 50f);

            var result = r1 | r2;

            result.X.Should().Be(0f);
            result.Y.Should().Be(0f);
            result.Width.Should().Be(150f);
            result.Height.Should().Be(150f);
        }

        #endregion

        #region Method Tests

        [Fact]
        public void Contains_WithPointInside_ShouldReturnTrue()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);
            var point = new PointF(50.5f, 40.5f);

            var result = rect.Contains(point);

            result.Should().BeTrue();
        }

        [Fact]
        public void Contains_WithPointOutside_ShouldReturnFalse()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);
            var point = new PointF(200f, 200f);

            var result = rect.Contains(point);

            result.Should().BeFalse();
        }

        [Fact]
        public void Contains_WithRectInside_ShouldReturnTrue()
        {
            var outer = new RectF(0f, 0f, 100f, 100f);
            var inner = new RectF(10f, 10f, 50f, 50f);

            var result = outer.Contains(inner);

            result.Should().BeTrue();
        }

        [Fact]
        public void Contains_WithRectOutside_ShouldReturnFalse()
        {
            var outer = new RectF(0f, 0f, 100f, 100f);
            var inner = new RectF(200f, 200f, 50f, 50f);

            var result = outer.Contains(inner);

            result.Should().BeFalse();
        }

        [Fact]
        public void Inflate_ShouldExpandRect()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            rect.Inflate(5.5f, 10.5f);

            rect.X.Should().Be(5.0f);
            rect.Y.Should().Be(10.0f);
            rect.Width.Should().BeApproximately(111.5f, 0.001f);
            rect.Height.Should().BeApproximately(71.5f, 0.001f);
        }

        [Fact]
        public void Intersect_WithOverlappingRects_ShouldReturnIntersection()
        {
            var r1 = new RectF(0f, 0f, 100f, 100f);
            var r2 = new RectF(50f, 50f, 100f, 100f);

            var result = RectF.Intersect(r1, r2);

            result.X.Should().Be(50f);
            result.Y.Should().Be(50f);
            result.Width.Should().Be(50f);
            result.Height.Should().Be(50f);
        }

        [Fact]
        public void Intersect_WithNonOverlappingRects_ShouldReturnEmptyRect()
        {
            var r1 = new RectF(0f, 0f, 50f, 50f);
            var r2 = new RectF(100f, 100f, 50f, 50f);

            var result = RectF.Intersect(r1, r2);

            result.Should().Be(default(RectF));
        }

        [Fact]
        public void IntersectsWith_WithOverlappingRects_ShouldReturnTrue()
        {
            var r1 = new RectF(0f, 0f, 100f, 100f);
            var r2 = new RectF(50f, 50f, 100f, 100f);

            var result = r1.IntersectsWith(r2);

            result.Should().BeTrue();
        }

        [Fact]
        public void IntersectsWith_WithNonOverlappingRects_ShouldReturnFalse()
        {
            var r1 = new RectF(0f, 0f, 50f, 50f);
            var r2 = new RectF(100f, 100f, 50f, 50f);

            var result = r1.IntersectsWith(r2);

            result.Should().BeFalse();
        }

        [Fact]
        public void Union_WithTwoRects_ShouldReturnBoundingRect()
        {
            var r1 = new RectF(0f, 0f, 50f, 50f);
            var r2 = new RectF(100f, 100f, 50f, 50f);

            var result = RectF.Union(r1, r2);

            result.X.Should().Be(0f);
            result.Y.Should().Be(0f);
            result.Width.Should().Be(150f);
            result.Height.Should().Be(150f);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var rect = new RectF(10.5f, 20.5f, 100.5f, 50.5f);

            var result = rect.ToString();

            result.Should().Be("Rect(X=10.50, Y=20.50, Width=100.50, Height=50.50)");
        }

        #endregion
    }
}
