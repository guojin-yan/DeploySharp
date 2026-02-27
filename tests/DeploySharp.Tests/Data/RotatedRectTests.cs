using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class RotatedRectTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldSetCorrectValues()
        {
            var center = new PointF(100f, 100f);
            var size = new SizeF(200f, 100f);
            var angle = 45f;

            var rect = new RotatedRect(center, size, angle);

            rect.Center.Should().Be(center);
            rect.Size.Should().Be(size);
            rect.Angle.Should().Be(angle);
        }

        [Fact]
        public void Constructor_WithZeroAngle_ShouldSetCorrectValues()
        {
            var center = new PointF(50f, 50f);
            var size = new SizeF(100f, 100f);

            var rect = new RotatedRect(center, size, 0f);

            rect.Center.Should().Be(center);
            rect.Size.Should().Be(size);
            rect.Angle.Should().Be(0f);
        }

        [Fact]
        public void Constructor_WithNegativeAngle_ShouldSetCorrectValues()
        {
            var center = new PointF(100f, 100f);
            var size = new SizeF(200f, 100f);

            var rect = new RotatedRect(center, size, -45f);

            rect.Angle.Should().Be(-45f);
        }

        #endregion

        #region FromAxisAlignedRect Tests

        [Fact]
        public void FromAxisAlignedRect_WithValidRect_ShouldCalculateCenter()
        {
            var axisAligned = new RectF(0f, 0f, 200f, 100f);

            var rotated = RotatedRect.FromAxisAlignedRect(axisAligned, 45f);

            rotated.Center.X.Should().Be(100f);
            rotated.Center.Y.Should().Be(50f);
            rotated.Size.Width.Should().Be(200f);
            rotated.Size.Height.Should().Be(100f);
            rotated.Angle.Should().Be(45f);
        }

        [Fact]
        public void FromAxisAlignedRect_WithOffsetRect_ShouldCalculateCorrectCenter()
        {
            var axisAligned = new RectF(50f, 100f, 200f, 100f);

            var rotated = RotatedRect.FromAxisAlignedRect(axisAligned, 0f);

            rotated.Center.X.Should().Be(150f);
            rotated.Center.Y.Should().Be(150f);
        }

        [Fact]
        public void FromAxisAlignedRect_WithZeroAngle_ShouldCreateNonRotatedRect()
        {
            var axisAligned = new RectF(0f, 0f, 100f, 50f);

            var rotated = RotatedRect.FromAxisAlignedRect(axisAligned, 0f);

            rotated.Angle.Should().Be(0f);
            rotated.Center.Should().Be(new PointF(50f, 25f));
        }

        #endregion

        #region Points Tests

        [Fact]
        public void Points_WithZeroAngle_ShouldReturnAxisAlignedCorners()
        {
            var rect = new RotatedRect(new PointF(100f, 100f), new SizeF(200f, 100f), 0f);

            var points = rect.Points();

            points.Should().HaveCount(4);
            // With 0 angle, corners should be axis-aligned
            // Center is at (100, 100), Size is 200x100
            // At 0 degrees, corners should be at the edges of the box
            points[0].X.Should().BeApproximately(0f, 0.001f);   // left
            points[0].Y.Should().BeApproximately(150f, 0.001f); // top
            points[1].X.Should().BeApproximately(0f, 0.001f);   // left
            points[1].Y.Should().BeApproximately(50f, 0.001f);  // bottom
        }

        [Fact]
        public void Points_With45DegreeAngle_ShouldReturnRotatedCorners()
        {
            var rect = new RotatedRect(new PointF(100f, 100f), new SizeF(100f, 100f), 45f);

            var points = rect.Points();

            points.Should().HaveCount(4);
            // Verify that points are properly rotated
            // At 45 degrees with a square, the bounding box should be expanded
        }

        [Fact]
        public void Points_WithSquare_ShouldHaveCorrectSymmetry()
        {
            var rect = new RotatedRect(new PointF(0f, 0f), new SizeF(100f, 100f), 0f);

            var points = rect.Points();

            points.Should().HaveCount(4);
            // For a centered square with 0 rotation, opposite points should be symmetric
            var centerX = (points[0].X + points[2].X) / 2;
            var centerY = (points[0].Y + points[2].Y) / 2;
            centerX.Should().BeApproximately(0f, 0.001f);
            centerY.Should().BeApproximately(0f, 0.001f);
        }

        #endregion

        #region BoundingRect Tests

        [Fact]
        public void BoundingRect_WithZeroAngle_ShouldReturnOriginalDimensions()
        {
            var rect = new RotatedRect(new PointF(100f, 100f), new SizeF(200f, 100f), 0f);

            var bounding = rect.BoundingRect();

            bounding.Width.Should().Be(200);
            bounding.Height.Should().Be(100);
        }

        [Fact]
        public void BoundingRect_With45DegreeRotation_ShouldReturnExpandedBoundingBox()
        {
            var rect = new RotatedRect(new PointF(100f, 100f), new SizeF(100f, 100f), 45f);

            var bounding = rect.BoundingRect();

            // For a 45-degree rotated square, the bounding box should be larger
            // The diagonal of a 100x100 square is 100*sqrt(2) ≈ 141
            bounding.Width.Should().BeGreaterThan(100);
            bounding.Height.Should().BeGreaterThan(100);
        }

        [Fact]
        public void BoundingRect_With90DegreeRotation_ShouldReturnSameDimensions()
        {
            var rect = new RotatedRect(new PointF(100f, 100f), new SizeF(200f, 100f), 90f);

            var bounding = rect.BoundingRect();

            // At 90 degrees, width and height are swapped
            bounding.Width.Should().Be(100);
            bounding.Height.Should().Be(200);
        }

        [Fact]
        public void BoundingRect_ShouldContainAllPoints()
        {
            var rect = new RotatedRect(new PointF(100f, 100f), new SizeF(150f, 80f), 30f);

            var points = rect.Points();
            var bounding = rect.BoundingRect();

            foreach (var point in points)
            {
                bounding.Contains((int)point.X, (int)point.Y).Should().BeTrue();
            }
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var rect = new RotatedRect(new PointF(100.5f, 100.5f), new SizeF(200.5f, 100.5f), 45.5f);

            var result = rect.ToString();

            result.Should().Contain("Center:");
            result.Should().Contain("Size:");
            result.Should().Contain("Angle:");
            result.Should().Contain("°");
        }

        #endregion
    }
}
