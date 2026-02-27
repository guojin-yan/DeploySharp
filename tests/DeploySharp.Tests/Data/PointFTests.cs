using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class PointFTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithFloatCoordinates_ShouldSetCorrectValues()
        {
            var point = new PointF(10.5f, 20.7f);

            point.X.Should().Be(10.5f);
            point.Y.Should().Be(20.7f);
        }

        [Fact]
        public void Constructor_WithNegativeValues_ShouldWorkCorrectly()
        {
            var point = new PointF(-10.5f, -20.7f);

            point.X.Should().Be(-10.5f);
            point.Y.Should().Be(-20.7f);
        }

        [Fact]
        public void Constructor_WithZeroValues_ShouldWorkCorrectly()
        {
            var point = new PointF(0f, 0f);

            point.X.Should().Be(0f);
            point.Y.Should().Be(0f);
        }

        #endregion

        #region Operator Tests

        [Fact]
        public void UnaryPlus_ShouldReturnSamePoint()
        {
            var point = new PointF(5.5f, 10.5f);

            var result = +point;

            result.Should().Be(point);
        }

        [Fact]
        public void UnaryMinus_ShouldNegateCoordinates()
        {
            var point = new PointF(5.5f, 10.5f);

            var result = -point;

            result.X.Should().Be(-5.5f);
            result.Y.Should().Be(-10.5f);
        }

        [Fact]
        public void AddOperator_WithTwoPoints_ShouldAddCoordinates()
        {
            var p1 = new PointF(3.5f, 4.5f);
            var p2 = new PointF(5.5f, 6.5f);

            var result = p1 + p2;

            result.X.Should().Be(9.0f);
            result.Y.Should().Be(11.0f);
        }

        [Fact]
        public void SubtractOperator_WithTwoPoints_ShouldSubtractCoordinates()
        {
            var p1 = new PointF(10.5f, 15.5f);
            var p2 = new PointF(3.5f, 5.5f);

            var result = p1 - p2;

            result.X.Should().Be(7.0f);
            result.Y.Should().Be(10.0f);
        }

        [Fact]
        public void MultiplyOperator_WithScalar_ShouldScaleCoordinates()
        {
            var point = new PointF(3.0f, 4.0f);

            var result = point * 2.0;

            result.X.Should().Be(6.0f);
            result.Y.Should().Be(8.0f);
        }

        [Fact]
        public void MultiplyOperator_WithZero_ShouldReturnZeroPoint()
        {
            var point = new PointF(3.0f, 4.0f);

            var result = point * 0.0;

            result.X.Should().Be(0.0f);
            result.Y.Should().Be(0.0f);
        }

        [Fact]
        public void MultiplyOperator_WithFractional_ShouldScaleCorrectly()
        {
            var point = new PointF(4.0f, 6.0f);

            var result = point * 0.5;

            result.X.Should().Be(2.0f);
            result.Y.Should().Be(3.0f);
        }

        #endregion

        #region Geometric Method Tests

        [Fact]
        public void Distance_WithHorizontalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new PointF(0f, 0f);
            var p2 = new PointF(3f, 0f);

            var distance = PointF.Distance(p1, p2);

            distance.Should().Be(3.0);
        }

        [Fact]
        public void Distance_WithVerticalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new PointF(0f, 0f);
            var p2 = new PointF(0f, 4f);

            var distance = PointF.Distance(p1, p2);

            distance.Should().Be(4.0);
        }

        [Fact]
        public void Distance_WithDiagonalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new PointF(0f, 0f);
            var p2 = new PointF(3f, 4f);

            var distance = PointF.Distance(p1, p2);

            distance.Should().Be(5.0);
        }

        [Fact]
        public void DistanceTo_ShouldCalculateDistanceFromInstance()
        {
            var p1 = new PointF(0f, 0f);
            var p2 = new PointF(3f, 4f);

            var distance = p1.DistanceTo(p2);

            distance.Should().Be(5.0);
        }

        [Fact]
        public void DotProduct_WithPerpendicularVectors_ShouldReturnZero()
        {
            var p1 = new PointF(1f, 0f);
            var p2 = new PointF(0f, 1f);

            var dotProduct = PointF.DotProduct(p1, p2);

            dotProduct.Should().Be(0.0);
        }

        [Fact]
        public void DotProduct_WithSameDirectionVectors_ShouldReturnPositive()
        {
            var p1 = new PointF(2f, 0f);
            var p2 = new PointF(3f, 0f);

            var dotProduct = PointF.DotProduct(p1, p2);

            dotProduct.Should().Be(6.0);
        }

        [Fact]
        public void DotProduct_WithOppositeDirectionVectors_ShouldReturnNegative()
        {
            var p1 = new PointF(2f, 0f);
            var p2 = new PointF(-3f, 0f);

            var dotProduct = PointF.DotProduct(p1, p2);

            dotProduct.Should().Be(-6.0);
        }

        [Fact]
        public void CrossProduct_WithPerpendicularVectors_ShouldReturnNonZero()
        {
            var p1 = new PointF(1f, 0f);
            var p2 = new PointF(0f, 1f);

            var crossProduct = PointF.CrossProduct(p1, p2);

            crossProduct.Should().Be(1.0);
        }

        [Fact]
        public void CrossProduct_WithParallelVectors_ShouldReturnZero()
        {
            var p1 = new PointF(2f, 3f);
            var p2 = new PointF(4f, 6f);

            var crossProduct = PointF.CrossProduct(p1, p2);

            crossProduct.Should().Be(0.0);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var point = new PointF(10.5f, 20.7f);

            var result = point.ToString();

            result.Should().Be("PointF(X=10.50, Y=20.70)");
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameCoordinates_ShouldReturnTrue()
        {
            var p1 = new PointF(5.5f, 10.5f);
            var p2 = new PointF(5.5f, 10.5f);

            var result = p1.Equals(p2);

            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentCoordinates_ShouldReturnFalse()
        {
            var p1 = new PointF(5.5f, 10.5f);
            var p2 = new PointF(5.5f, 11.5f);

            var result = p1.Equals(p2);

            result.Should().BeFalse();
        }

        [Fact]
        public void EqualityOperator_WithSameCoordinates_ShouldReturnTrue()
        {
            var p1 = new PointF(5.5f, 10.5f);
            var p2 = new PointF(5.5f, 10.5f);

            (p1 == p2).Should().BeTrue();
        }

        [Fact]
        public void InequalityOperator_WithDifferentCoordinates_ShouldReturnTrue()
        {
            var p1 = new PointF(5.5f, 10.5f);
            var p2 = new PointF(5.5f, 11.5f);

            (p1 != p2).Should().BeTrue();
        }

        #endregion
    }
}
