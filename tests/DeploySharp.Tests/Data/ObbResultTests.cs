using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class ObbResultTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldSetTypeToOrientedBoundingBoxes()
        {
            var result = new ObbResult();

            result.Type.Should().Be(ResultType.OrientedBoundingBoxes);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Bounds_SetValue_ShouldUpdate()
        {
            var result = new ObbResult();
            var bounds = new RotatedRect(
                new PointF(100.5f, 200.5f),
                new SizeF(80, 40),
                30f
            );

            result.Bounds = bounds;

            result.Bounds.Center.Should().Be(new PointF(100.5f, 200.5f));
            result.Bounds.Size.Should().Be(new SizeF(80, 40));
            result.Bounds.Angle.Should().Be(30f);
        }

        [Fact]
        public void InheritedProperties_ShouldWork()
        {
            var result = new ObbResult
            {
                Id = 42,
                Category = "vehicle",
                Confidence = 0.95f
            };

            result.Id.Should().Be(42);
            result.Category.Should().Be("vehicle");
            result.Confidence.Should().Be(0.95f);
        }

        #endregion

        #region GetBoundingRect Tests

        [Fact]
        public void GetBoundingRect_ShouldReturnAxisAlignedBounds()
        {
            var result = new ObbResult
            {
                Bounds = new RotatedRect(
                    new PointF(100, 100),
                    new SizeF(100, 50),
                    0f
                )
            };

            var aabb = result.GetBoundingRect();

            // At 0 degrees, AABB should match OBB dimensions
            aabb.Width.Should().Be(100);
            aabb.Height.Should().Be(50);
        }

        [Fact]
        public void GetBoundingRect_WithRotation_ShouldReturnEnclosingRect()
        {
            var result = new ObbResult
            {
                Bounds = new RotatedRect(
                    new PointF(100, 100),
                    new SizeF(100, 50),
                    45f
                )
            };

            var aabb = result.GetBoundingRect();

            // At 45 degrees, AABB should be larger than original
            aabb.Width.Should().BeGreaterThan(100);
            aabb.Height.Should().BeGreaterThan(50);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldIncludeOBBDetails()
        {
            var result = new ObbResult
            {
                Id = 42,
                Category = "vehicle",
                Confidence = 0.95f,
                Bounds = new RotatedRect(
                    new PointF(100.5f, 200.5f),
                    new SizeF(80, 40),
                    30f
                )
            };

            var str = result.ToString();

            str.Should().Contain("ID: 42");
            str.Should().Contain("Category: vehicle");
            str.Should().Contain("OBB:");
            str.Should().Contain("Center:");
            str.Should().Contain("Size:");
            str.Should().Contain("Angle:");
            str.Should().Contain("°");
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_ShouldCreateDeepCopy()
        {
            var original = new ObbResult
            {
                Id = 42,
                Category = "vehicle",
                Confidence = 0.95f,
                Bounds = new RotatedRect(
                    new PointF(100.5f, 200.5f),
                    new SizeF(80, 40),
                    30f
                )
            };

            var clone = original.Clone();

            clone.Id.Should().Be(original.Id);
            clone.Category.Should().Be(original.Category);
            clone.Confidence.Should().Be(original.Confidence);
            clone.Bounds.Center.Should().Be(original.Bounds.Center);
            clone.Bounds.Size.Should().Be(original.Bounds.Size);
            clone.Bounds.Angle.Should().Be(original.Bounds.Angle);
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new ObbResult
            {
                Bounds = new RotatedRect(
                    new PointF(100, 100),
                    new SizeF(80, 40),
                    30f
                )
            };

            var clone = original.Clone();
            // Note: Since RotatedRect is a struct, it's copied by value
            // Modifying the clone's bounds won't affect original
            clone.Bounds = new RotatedRect(
                new PointF(0, 0),
                new SizeF(10, 10),
                0f
            );

            original.Bounds.Center.Should().Be(new PointF(100, 100));
        }

        #endregion
    }
}
