using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class PointDTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithDoubleCoordinates_ShouldSetCorrectValues()
        {
            var point = new PointD(10.5, 20.7);

            point.X.Should().Be(10.5);
            point.Y.Should().Be(20.7);
        }

        [Fact]
        public void Constructor_WithNegativeValues_ShouldWorkCorrectly()
        {
            var point = new PointD(-10.5, -20.7);

            point.X.Should().Be(-10.5);
            point.Y.Should().Be(-20.7);
        }

        [Fact]
        public void Constructor_WithZeroValues_ShouldWorkCorrectly()
        {
            var point = new PointD(0.0, 0.0);

            point.X.Should().Be(0.0);
            point.Y.Should().Be(0.0);
        }

        [Fact]
        public void Constructor_WithHighPrecisionValues_ShouldPreservePrecision()
        {
            var point = new PointD(1.123456789012345, 2.987654321098765);

            point.X.Should().Be(1.123456789012345);
            point.Y.Should().Be(2.987654321098765);
        }

        #endregion

        #region Operator Tests

        [Fact]
        public void UnaryPlus_ShouldReturnSamePoint()
        {
            var point = new PointD(5.5, 10.5);

            var result = +point;

            result.Should().Be(point);
        }

        [Fact]
        public void UnaryMinus_ShouldNegateCoordinates()
        {
            var point = new PointD(5.5, 10.5);

            var result = -point;

            result.X.Should().Be(-5.5);
            result.Y.Should().Be(-10.5);
        }

        [Fact]
        public void AddOperator_WithTwoPoints_ShouldAddCoordinates()
        {
            var p1 = new PointD(3.5, 4.5);
            var p2 = new PointD(5.5, 6.5);

            var result = p1 + p2;

            result.X.Should().Be(9.0);
            result.Y.Should().Be(11.0);
        }

        [Fact]
        public void SubtractOperator_WithTwoPoints_ShouldSubtractCoordinates()
        {
            var p1 = new PointD(10.5, 15.5);
            var p2 = new PointD(3.5, 5.5);

            var result = p1 - p2;

            result.X.Should().Be(7.0);
            result.Y.Should().Be(10.0);
        }

        [Fact]
        public void MultiplyOperator_WithScalar_ShouldScaleCoordinates()
        {
            var point = new PointD(3.0, 4.0);

            var result = point * 2.0;

            result.X.Should().Be(6.0);
            result.Y.Should().Be(8.0);
        }

        [Fact]
        public void MultiplyOperator_WithZero_ShouldReturnZeroPoint()
        {
            var point = new PointD(3.0, 4.0);

            var result = point * 0.0;

            result.X.Should().Be(0.0);
            result.Y.Should().Be(0.0);
        }

        [Fact]
        public void MultiplyOperator_WithFractional_ShouldScaleCorrectly()
        {
            var point = new PointD(4.0, 6.0);

            var result = point * 0.5;

            result.X.Should().Be(2.0);
            result.Y.Should().Be(3.0);
        }

        #endregion

        #region Geometric Method Tests

        [Fact]
        public void Distance_WithHorizontalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new PointD(0.0, 0.0);
            var p2 = new PointD(3.0, 0.0);

            var distance = PointD.Distance(p1, p2);

            distance.Should().Be(3.0);
        }

        [Fact]
        public void Distance_WithVerticalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new PointD(0.0, 0.0);
            var p2 = new PointD(0.0, 4.0);

            var distance = PointD.Distance(p1, p2);

            distance.Should().Be(4.0);
        }

        [Fact]
        public void Distance_WithDiagonalPoints_ShouldReturnCorrectDistance()
        {
            var p1 = new PointD(0.0, 0.0);
            var p2 = new PointD(3.0, 4.0);

            var distance = PointD.Distance(p1, p2);

            distance.Should().Be(5.0);
        }

        [Fact]
        public void DistanceTo_ShouldCalculateDistanceFromInstance()
        {
            var p1 = new PointD(0.0, 0.0);
            var p2 = new PointD(3.0, 4.0);

            var distance = p1.DistanceTo(p2);

            distance.Should().Be(5.0);
        }

        [Fact]
        public void DotProduct_WithPerpendicularVectors_ShouldReturnZero()
        {
            var p1 = new PointD(1.0, 0.0);
            var p2 = new PointD(0.0, 1.0);

            var dotProduct = PointD.DotProduct(p1, p2);

            dotProduct.Should().Be(0.0);
        }

        [Fact]
        public void DotProduct_WithSameDirectionVectors_ShouldReturnPositive()
        {
            var p1 = new PointD(2.0, 0.0);
            var p2 = new PointD(3.0, 0.0);

            var dotProduct = PointD.DotProduct(p1, p2);

            dotProduct.Should().Be(6.0);
        }

        [Fact]
        public void DotProduct_WithOppositeDirectionVectors_ShouldReturnNegative()
        {
            var p1 = new PointD(2.0, 0.0);
            var p2 = new PointD(-3.0, 0.0);

            var dotProduct = PointD.DotProduct(p1, p2);

            dotProduct.Should().Be(-6.0);
        }

        [Fact]
        public void CrossProduct_WithPerpendicularVectors_ShouldReturnNonZero()
        {
            var p1 = new PointD(1.0, 0.0);
            var p2 = new PointD(0.0, 1.0);

            var crossProduct = PointD.CrossProduct(p1, p2);

            crossProduct.Should().Be(1.0);
        }

        [Fact]
        public void CrossProduct_WithParallelVectors_ShouldReturnZero()
        {
            var p1 = new PointD(2.0, 3.0);
            var p2 = new PointD(4.0, 6.0);

            var crossProduct = PointD.CrossProduct(p1, p2);

            crossProduct.Should().Be(0.0);
        }

        [Fact]
        public void Distance_WithHighPrecision_ShouldMaintainAccuracy()
        {
            var p1 = new PointD(0.0000001, 0.0000001);
            var p2 = new PointD(0.0000002, 0.0000001);

            var distance = PointD.Distance(p1, p2);

            distance.Should().Be(0.0000001);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var point = new PointD(10.5678, 20.9012);

            var result = point.ToString();

            result.Should().Be("PointD(X=10.57, Y=20.90)");
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameCoordinates_ShouldReturnTrue()
        {
            var p1 = new PointD(5.5, 10.5);
            var p2 = new PointD(5.5, 10.5);

            var result = p1.Equals(p2);

            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentCoordinates_ShouldReturnFalse()
        {
            var p1 = new PointD(5.5, 10.5);
            var p2 = new PointD(5.5, 10.6);

            var result = p1.Equals(p2);

            result.Should().BeFalse();
        }

        [Fact]
        public void EqualityOperator_WithSameCoordinates_ShouldReturnTrue()
        {
            var p1 = new PointD(5.5, 10.5);
            var p2 = new PointD(5.5, 10.5);

            (p1 == p2).Should().BeTrue();
        }

        [Fact]
        public void InequalityOperator_WithDifferentCoordinates_ShouldReturnTrue()
        {
            var p1 = new PointD(5.5, 10.5);
            var p2 = new PointD(5.5, 10.6);

            (p1 != p2).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithExactDoubleComparison_ShouldBeExact()
        {
            var p1 = new PointD(1.0000000001, 2.0000000001);
            var p2 = new PointD(1.0000000001, 2.0000000001);
            var p3 = new PointD(1.0000000002, 2.0000000001);

            p1.Equals(p2).Should().BeTrue();
            p1.Equals(p3).Should().BeFalse();
        }

        #endregion
    }
}
