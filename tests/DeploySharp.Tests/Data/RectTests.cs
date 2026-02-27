using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class RectTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithIntParameters_ShouldSetCorrectValues()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.X.Should().Be(10);
            rect.Y.Should().Be(20);
            rect.Width.Should().Be(100);
            rect.Height.Should().Be(50);
        }

        [Fact]
        public void Constructor_WithPointAndSize_ShouldSetCorrectValues()
        {
            var location = new Point(10, 20);
            var size = new Size(100, 50);

            var rect = new Rect(location, size);

            rect.X.Should().Be(10);
            rect.Y.Should().Be(20);
            rect.Width.Should().Be(100);
            rect.Height.Should().Be(50);
        }

        [Fact]
        public void FromLTRB_WithValidCoordinates_ShouldCreateRect()
        {
            var rect = Rect.FromLTRB(10, 20, 110, 70);

            rect.X.Should().Be(10);
            rect.Y.Should().Be(20);
            rect.Width.Should().Be(100);
            rect.Height.Should().Be(50);
        }

        [Fact]
        public void FromLTRB_WithRightLessThanLeft_ShouldThrowArgumentException()
        {
            Action act = () => Rect.FromLTRB(110, 20, 10, 70);

            act.Should().Throw<ArgumentException>().WithMessage("right < left");
        }

        [Fact]
        public void FromLTRB_WithBottomLessThanTop_ShouldThrowArgumentException()
        {
            Action act = () => Rect.FromLTRB(10, 70, 110, 20);

            act.Should().Throw<ArgumentException>().WithMessage("bottom < top");
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Top_ShouldReturnY()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Top.Should().Be(20);
        }

        [Fact]
        public void Bottom_ShouldReturnYPlusHeight()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Bottom.Should().Be(70);
        }

        [Fact]
        public void Left_ShouldReturnX()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Left.Should().Be(10);
        }

        [Fact]
        public void Right_ShouldReturnXPlusWidth()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Right.Should().Be(110);
        }

        [Fact]
        public void Location_ShouldReturnTopLeftPoint()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Location.Should().Be(new Point(10, 20));
        }

        [Fact]
        public void Location_Set_ShouldUpdateXAndY()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Location = new Point(30, 40);

            rect.X.Should().Be(30);
            rect.Y.Should().Be(40);
        }

        [Fact]
        public void Size_ShouldReturnSize()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Size.Should().Be(new Size(100, 50));
        }

        [Fact]
        public void Size_Set_ShouldUpdateWidthAndHeight()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Size = new Size(200, 100);

            rect.Width.Should().Be(200);
            rect.Height.Should().Be(100);
        }

        [Fact]
        public void TopLeft_ShouldReturnTopLeftPoint()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.TopLeft.Should().Be(new Point(10, 20));
        }

        [Fact]
        public void BottomRight_ShouldReturnBottomRightPoint()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.BottomRight.Should().Be(new Point(110, 70));
        }

        #endregion

        #region Operator Tests

        [Fact]
        public void AddOperator_WithPoint_ShouldTranslateRect()
        {
            var rect = new Rect(10, 20, 100, 50);
            var point = new Point(5, 10);

            var result = rect + point;

            result.X.Should().Be(15);
            result.Y.Should().Be(30);
            result.Width.Should().Be(100);
            result.Height.Should().Be(50);
        }

        [Fact]
        public void SubtractOperator_WithPoint_ShouldTranslateRect()
        {
            var rect = new Rect(10, 20, 100, 50);
            var point = new Point(5, 10);

            var result = rect - point;

            result.X.Should().Be(5);
            result.Y.Should().Be(10);
            result.Width.Should().Be(100);
            result.Height.Should().Be(50);
        }

        [Fact]
        public void AddOperator_WithSize_ShouldExpandRect()
        {
            var rect = new Rect(10, 20, 100, 50);
            var size = new Size(20, 10);

            var result = rect + size;

            result.X.Should().Be(10);
            result.Y.Should().Be(20);
            result.Width.Should().Be(120);
            result.Height.Should().Be(60);
        }

        [Fact]
        public void SubtractOperator_WithSize_ShouldShrinkRect()
        {
            var rect = new Rect(10, 20, 100, 50);
            var size = new Size(20, 10);

            var result = rect - size;

            result.X.Should().Be(10);
            result.Y.Should().Be(20);
            result.Width.Should().Be(80);
            result.Height.Should().Be(40);
        }

        [Fact]
        public void IntersectionOperator_WithOverlappingRects_ShouldReturnIntersection()
        {
            var r1 = new Rect(0, 0, 100, 100);
            var r2 = new Rect(50, 50, 100, 100);

            var result = r1 & r2;

            result.X.Should().Be(50);
            result.Y.Should().Be(50);
            result.Width.Should().Be(50);
            result.Height.Should().Be(50);
        }

        [Fact]
        public void UnionOperator_WithTwoRects_ShouldReturnBoundingRect()
        {
            var r1 = new Rect(0, 0, 50, 50);
            var r2 = new Rect(100, 100, 50, 50);

            var result = r1 | r2;

            result.X.Should().Be(0);
            result.Y.Should().Be(0);
            result.Width.Should().Be(150);
            result.Height.Should().Be(150);
        }

        #endregion

        #region Method Tests

        [Fact]
        public void Contains_WithPointInside_ShouldReturnTrue()
        {
            var rect = new Rect(10, 20, 100, 50);
            var point = new Point(50, 40);

            var result = rect.Contains(point);

            result.Should().BeTrue();
        }

        [Fact]
        public void Contains_WithPointOutside_ShouldReturnFalse()
        {
            var rect = new Rect(10, 20, 100, 50);
            var point = new Point(200, 200);

            var result = rect.Contains(point);

            result.Should().BeFalse();
        }

        [Fact]
        public void Contains_WithPointOnEdge_ShouldReturnTrue()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Contains(10, 20).Should().BeTrue(); // Top-left corner
            rect.Contains(109, 69).Should().BeTrue(); // Bottom-right corner (exclusive)
        }

        [Fact]
        public void Contains_WithRectInside_ShouldReturnTrue()
        {
            var outer = new Rect(0, 0, 100, 100);
            var inner = new Rect(10, 10, 50, 50);

            var result = outer.Contains(inner);

            result.Should().BeTrue();
        }

        [Fact]
        public void Contains_WithRectOutside_ShouldReturnFalse()
        {
            var outer = new Rect(0, 0, 100, 100);
            var inner = new Rect(200, 200, 50, 50);

            var result = outer.Contains(inner);

            result.Should().BeFalse();
        }

        [Fact]
        public void Inflate_ShouldExpandRect()
        {
            var rect = new Rect(10, 20, 100, 50);

            rect.Inflate(5, 10);

            rect.X.Should().Be(5);
            rect.Y.Should().Be(10);
            rect.Width.Should().Be(110);
            rect.Height.Should().Be(70);
        }

        [Fact]
        public void Intersect_WithOverlappingRects_ShouldReturnIntersection()
        {
            var r1 = new Rect(0, 0, 100, 100);
            var r2 = new Rect(50, 50, 100, 100);

            var result = Rect.Intersect(r1, r2);

            result.X.Should().Be(50);
            result.Y.Should().Be(50);
            result.Width.Should().Be(50);
            result.Height.Should().Be(50);
        }

        [Fact]
        public void Intersect_WithNonOverlappingRects_ShouldReturnEmptyRect()
        {
            var r1 = new Rect(0, 0, 50, 50);
            var r2 = new Rect(100, 100, 50, 50);

            var result = Rect.Intersect(r1, r2);

            result.Should().Be(default(Rect));
        }

        [Fact]
        public void IntersectsWith_WithOverlappingRects_ShouldReturnTrue()
        {
            var r1 = new Rect(0, 0, 100, 100);
            var r2 = new Rect(50, 50, 100, 100);

            var result = r1.IntersectsWith(r2);

            result.Should().BeTrue();
        }

        [Fact]
        public void IntersectsWith_WithNonOverlappingRects_ShouldReturnFalse()
        {
            var r1 = new Rect(0, 0, 50, 50);
            var r2 = new Rect(100, 100, 50, 50);

            var result = r1.IntersectsWith(r2);

            result.Should().BeFalse();
        }

        [Fact]
        public void Union_WithTwoRects_ShouldReturnBoundingRect()
        {
            var r1 = new Rect(0, 0, 50, 50);
            var r2 = new Rect(100, 100, 50, 50);

            var result = Rect.Union(r1, r2);

            result.X.Should().Be(0);
            result.Y.Should().Be(0);
            result.Width.Should().Be(150);
            result.Height.Should().Be(150);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var rect = new Rect(10, 20, 100, 50);

            var result = rect.ToString();

            result.Should().Be("Rect(X=10, Y=20, Width=100, Height=50)");
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameValues_ShouldReturnTrue()
        {
            var r1 = new Rect(10, 20, 100, 50);
            var r2 = new Rect(10, 20, 100, 50);

            var result = r1.Equals(r2);

            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentValues_ShouldReturnFalse()
        {
            var r1 = new Rect(10, 20, 100, 50);
            var r2 = new Rect(10, 20, 100, 51);

            var result = r1.Equals(r2);

            result.Should().BeFalse();
        }

        #endregion
    }
}
