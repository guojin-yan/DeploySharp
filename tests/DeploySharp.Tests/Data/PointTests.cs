using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class PointTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithIntCoordinates_ShouldSetCorrectValues()
        {
            var point = new Point(10, 20);

            point.X.Should().Be(10);
            point.Y.Should().Be(20);
        }

        [Fact]
        public void Constructor_WithDoubleCoordinates_ShouldTruncateToInt()
        {
            var point = new Point(10.7, 20.3);

            point.X.Should().Be(10);
            point.Y.Should().Be(20);
        }

        [Fact]
        public void Constructor_WithNegativeValues_ShouldWorkCorrectly()
        {
            var point = new Point(-10, -20);

            point.X.Should().Be(-10);
            point.Y.Should().Be(-20);
        }

        #endregion

        #region Operator Tests

        [Fact]
        public void UnaryPlus_ShouldReturnSamePoint()
        {
            var point = new Point(5, 10);

            var result = +point;

            result.Should().Be(point);
        }

        [Fact]
        public void UnaryMinus_ShouldNegateCoordinates()
        {
            var point = new Point(5, 10);

            var result = -point;

            result.X.Should().Be(-5);
            result.Y.Should().Be(-10);
        }

        [Fact]
        public void AddOperator_WithTwoPoints_ShouldAddCoordinates()
        {
            var p1 = new Point(3, 4);
            var p2 = new Point(5, 6);

            var result = p1 + p2;

            result.X.Should().Be(8);
            result.Y.Should().Be(10);
        }

        [Fact]
        public void SubtractOperator_WithTwoPoints_ShouldSubtractCoordinates()
        {
            var p1 = new Point(10, 15);
            var p2 = new Point(3, 5);

            var result = p1 - p2;

            result.X.Should().Be(7);
            result.Y.Should().Be(10);
        }

        [Fact]
        public void MultiplyOperator_WithScalar_ShouldScaleCoordinates()
        {
            var point = new Point(3, 4);

            var result = point * 2.0;

            result.X.Should().Be(6);
            result.Y.Should().Be(8);
        }

        [Fact]
        public void MultiplyOperator_WithZero_ShouldReturnZeroPoint()
        {
            var point = new Point(3, 4);

            var result = point * 0;

            result.X.Should().Be(0);
            result.Y.Should().Be(0);
        }

        #endregion

        #region Geometric Method Tests

        [Fact]
        public void Distance_WithHorizontalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new Point(0, 0);
            var p2 = new Point(3, 0);

            var distance = Point.Distance(p1, p2);

            distance.Should().Be(3.0);
        }

        [Fact]
        public void Distance_WithVerticalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new Point(0, 0);
            var p2 = new Point(0, 4);

            var distance = Point.Distance(p1, p2);

            distance.Should().Be(4.0);
        }

        [Fact]
        public void Distance_WithDiagonalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new Point(0, 0);
            var p2 = new Point(3, 4);

            var distance = Point.Distance(p1, p2);

            distance.Should().Be(5.0);
        }

        [Fact]
        public void DistanceTo_ShouldCalculateDistanceFromInstance()
        {
            var p1 = new Point(0, 0);
            var p2 = new Point(3, 4);

            var distance = p1.DistanceTo(p2);

            distance.Should().Be(5.0);
        }

        [Fact]
        public void DotProduct_WithPerpendicularVectors_ShouldReturnZero()
        {
            var p1 = new Point(1, 0);
            var p2 = new Point(0, 1);

            var dotProduct = Point.DotProduct(p1, p2);

            dotProduct.Should().Be(0.0);
        }

        [Fact]
        public void DotProduct_WithSameDirectionVectors_ShouldReturnPositive()
        {
            var p1 = new Point(2, 0);
            var p2 = new Point(3, 0);

            var dotProduct = Point.DotProduct(p1, p2);

            dotProduct.Should().Be(6.0);
        }

        [Fact]
        public void DotProduct_WithOppositeDirectionVectors_ShouldReturnNegative()
        {
            var p1 = new Point(2, 0);
            var p2 = new Point(-3, 0);

            var dotProduct = Point.DotProduct(p1, p2);

            dotProduct.Should().Be(-6.0);
        }

        [Fact]
        public void CrossProduct_WithPerpendicularVectors_ShouldReturnNonZero()
        {
            var p1 = new Point(1, 0);
            var p2 = new Point(0, 1);

            var crossProduct = Point.CrossProduct(p1, p2);

            crossProduct.Should().Be(1.0);
        }

        [Fact]
        public void CrossProduct_WithParallelVectors_ShouldReturnZero()
        {
            var p1 = new Point(2, 3);
            var p2 = new Point(4, 6);

            var crossProduct = Point.CrossProduct(p1, p2);

            crossProduct.Should().Be(0.0);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var point = new Point(10, 20);

            var result = point.ToString();

            result.Should().Be("Point(X=10, Y=20)");
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameCoordinates_ShouldReturnTrue()
        {
            var p1 = new Point(5, 10);
            var p2 = new Point(5, 10);

            var result = p1.Equals(p2);

            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentCoordinates_ShouldReturnFalse()
        {
            var p1 = new Point(5, 10);
            var p2 = new Point(5, 11);

            var result = p1.Equals(p2);

            result.Should().BeFalse();
        }

        [Fact]
        public void EqualityOperator_WithSameCoordinates_ShouldReturnTrue()
        {
            var p1 = new Point(5, 10);
            var p2 = new Point(5, 10);

            (p1 == p2).Should().BeTrue();
        }

        [Fact]
        public void InequalityOperator_WithDifferentCoordinates_ShouldReturnTrue()
        {
            var p1 = new Point(5, 10);
            var p2 = new Point(5, 11);

            (p1 != p2).Should().BeTrue();
        }

        #endregion
    }
}
