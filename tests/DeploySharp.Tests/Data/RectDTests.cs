using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class RectDTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithDoubleParameters_ShouldSetCorrectValues()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.X.Should().Be(10.5);
            rect.Y.Should().Be(20.5);
            rect.Width.Should().Be(100.5);
            rect.Height.Should().Be(50.5);
        }

        [Fact]
        public void Constructor_WithPointDAndSizeD_ShouldSetCorrectValues()
        {
            var location = new PointD(10.5, 20.5);
            var size = new SizeD(100.5, 50.5);

            var rect = new RectD(location, size);

            rect.X.Should().Be(10.5);
            rect.Y.Should().Be(20.5);
            rect.Width.Should().Be(100.5);
            rect.Height.Should().Be(50.5);
        }

        [Fact]
        public void FromLTRB_WithValidCoordinates_ShouldCreateRect()
        {
            var rect = RectD.FromLTRB(10.5, 20.5, 110.5, 70.5);

            rect.X.Should().Be(10.5);
            rect.Y.Should().Be(20.5);
            rect.Width.Should().Be(100.0);
            rect.Height.Should().Be(50.0);
        }

        [Fact]
        public void FromLTRB_WithRightLessThanLeft_ShouldThrowArgumentException()
        {
            Action act = () => RectD.FromLTRB(110.5, 20.5, 10.5, 70.5);

            act.Should().Throw<ArgumentException>().WithMessage("right > left");
        }

        [Fact]
        public void FromLTRB_WithBottomLessThanTop_ShouldThrowArgumentException()
        {
            Action act = () => RectD.FromLTRB(10.5, 70.5, 110.5, 20.5);

            act.Should().Throw<ArgumentException>().WithMessage("bottom > top");
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Top_ShouldReturnY()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Top.Should().Be(20.5);
        }

        [Fact]
        public void Bottom_ShouldReturnYPlusHeight()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Bottom.Should().Be(71.0);
        }

        [Fact]
        public void Left_ShouldReturnX()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Left.Should().Be(10.5);
        }

        [Fact]
        public void Right_ShouldReturnXPlusWidth()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Right.Should().Be(111.0);
        }

        [Fact]
        public void Location_ShouldReturnTopLeftPoint()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Location.Should().Be(new PointD(10.5, 20.5));
        }

        [Fact]
        public void Location_Set_ShouldUpdateXAndY()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Location = new PointD(30.5, 40.5);

            rect.X.Should().Be(30.5);
            rect.Y.Should().Be(40.5);
        }

        [Fact]
        public void Size_ShouldReturnSizeD()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Size.Should().Be(new SizeD(100.5, 50.5));
        }

        [Fact]
        public void Size_Set_ShouldUpdateWidthAndHeight()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Size = new SizeD(200.5, 100.5);

            rect.Width.Should().Be(200.5);
            rect.Height.Should().Be(100.5);
        }

        [Fact]
        public void TopLeft_ShouldReturnTopLeftPoint()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.TopLeft.Should().Be(new PointD(10.5, 20.5));
        }

        [Fact]
        public void BottomRight_ShouldReturnBottomRightPoint()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.BottomRight.Should().Be(new PointD(111.0, 71.0));
        }

        #endregion

        #region Operator Tests

        [Fact]
        public void AddOperator_WithPointD_ShouldTranslateRect()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);
            var point = new PointD(5.5, 10.5);

            var result = rect + point;

            result.X.Should().Be(16.0);
            result.Y.Should().Be(31.0);
            result.Width.Should().Be(100.5);
            result.Height.Should().Be(50.5);
        }

        [Fact]
        public void SubtractOperator_WithPointD_ShouldTranslateRect()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);
            var point = new PointD(5.5, 10.5);

            var result = rect - point;

            result.X.Should().Be(5.0);
            result.Y.Should().Be(10.0);
            result.Width.Should().Be(100.5);
            result.Height.Should().Be(50.5);
        }

        [Fact]
        public void IntersectionOperator_WithOverlappingRects_ShouldReturnIntersection()
        {
            var r1 = new RectD(0.0, 0.0, 100.0, 100.0);
            var r2 = new RectD(50.0, 50.0, 100.0, 100.0);

            var result = r1 & r2;

            result.X.Should().Be(50.0);
            result.Y.Should().Be(50.0);
            result.Width.Should().Be(50.0);
            result.Height.Should().Be(50.0);
        }

        [Fact]
        public void UnionOperator_WithTwoRects_ShouldReturnBoundingRect()
        {
            var r1 = new RectD(0.0, 0.0, 50.0, 50.0);
            var r2 = new RectD(100.0, 100.0, 50.0, 50.0);

            var result = r1 | r2;

            result.X.Should().Be(0.0);
            result.Y.Should().Be(0.0);
            result.Width.Should().Be(150.0);
            result.Height.Should().Be(150.0);
        }

        #endregion

        #region Method Tests

        [Fact]
        public void Contains_WithPointInside_ShouldReturnTrue()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);
            var point = new PointD(50.5, 40.5);

            var result = rect.Contains(point);

            result.Should().BeTrue();
        }

        [Fact]
        public void Contains_WithPointOutside_ShouldReturnFalse()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);
            var point = new PointD(200.0, 200.0);

            var result = rect.Contains(point);

            result.Should().BeFalse();
        }

        [Fact]
        public void Contains_WithRectInside_ShouldReturnTrue()
        {
            var outer = new RectD(0.0, 0.0, 100.0, 100.0);
            var inner = new RectD(10.0, 10.0, 50.0, 50.0);

            var result = outer.Contains(inner);

            result.Should().BeTrue();
        }

        [Fact]
        public void Contains_WithRectOutside_ShouldReturnFalse()
        {
            var outer = new RectD(0.0, 0.0, 100.0, 100.0);
            var inner = new RectD(200.0, 200.0, 50.0, 50.0);

            var result = outer.Contains(inner);

            result.Should().BeFalse();
        }

        [Fact]
        public void Inflate_ShouldExpandRect()
        {
            var rect = new RectD(10.5, 20.5, 100.5, 50.5);

            rect.Inflate(5.5, 10.5);

            rect.X.Should().Be(5.0);
            rect.Y.Should().Be(10.0);
            rect.Width.Should().Be(111.5);
            rect.Height.Should().Be(71.5);
        }

        [Fact]
        public void Intersect_WithOverlappingRects_ShouldReturnIntersection()
        {
            var r1 = new RectD(0.0, 0.0, 100.0, 100.0);
            var r2 = new RectD(50.0, 50.0, 100.0, 100.0);

            var result = RectD.Intersect(r1, r2);

            result.X.Should().Be(50.0);
            result.Y.Should().Be(50.0);
            result.Width.Should().Be(50.0);
            result.Height.Should().Be(50.0);
        }

        [Fact]
        public void Intersect_WithNonOverlappingRects_ShouldReturnEmptyRect()
        {
            var r1 = new RectD(0.0, 0.0, 50.0, 50.0);
            var r2 = new RectD(100.0, 100.0, 50.0, 50.0);

            var result = RectD.Intersect(r1, r2);

            result.Should().Be(default(RectD));
        }

        [Fact]
        public void IntersectsWith_WithOverlappingRects_ShouldReturnTrue()
        {
            var r1 = new RectD(0.0, 0.0, 100.0, 100.0);
            var r2 = new RectD(50.0, 50.0, 100.0, 100.0);

            var result = r1.IntersectsWith(r2);

            result.Should().BeTrue();
        }

        [Fact]
        public void IntersectsWith_WithNonOverlappingRects_ShouldReturnFalse()
        {
            var r1 = new RectD(0.0, 0.0, 50.0, 50.0);
            var r2 = new RectD(100.0, 100.0, 50.0, 50.0);

            var result = r1.IntersectsWith(r2);

            result.Should().BeFalse();
        }

        [Fact]
        public void Union_WithTwoRects_ShouldReturnBoundingRect()
        {
            var r1 = new RectD(0.0, 0.0, 50.0, 50.0);
            var r2 = new RectD(100.0, 100.0, 50.0, 50.0);

            var result = RectD.Union(r1, r2);

            result.X.Should().Be(0.0);
            result.Y.Should().Be(0.0);
            result.Width.Should().Be(150.0);
            result.Height.Should().Be(150.0);
        }

        [Fact]
        public void ToRect_ShouldTruncateToInt()
        {
            var rectD = new RectD(10.9, 20.9, 100.9, 50.9);

            var result = rectD.ToRect();

            result.X.Should().Be(10);
            result.Y.Should().Be(20);
            result.Width.Should().Be(100);
            result.Height.Should().Be(50);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var rect = new RectD(10.5678, 20.9012, 100.3456, 50.7890);

            var result = rect.ToString();

            result.Should().Be("Rect(X=10.57, Y=20.90, Width=100.35, Height=50.79)");
        }

        #endregion
    }
}
