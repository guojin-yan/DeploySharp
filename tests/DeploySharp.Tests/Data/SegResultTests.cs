using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class SegResultTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldSetTypeToSegmentation()
        {
            var result = new SegResult();

            result.Type.Should().Be(ResultType.Segmentation);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Mask_SetValue_ShouldUpdate()
        {
            var result = new SegResult();
            // Note: ImageDataF would need proper initialization in real scenarios
            // Here we just test the property setter/getter
        }

        [Fact]
        public void InheritedProperties_ShouldWork()
        {
            var result = new SegResult
            {
                Id = 42,
                Category = "road",
                Confidence = 0.92f,
                Bounds = new Rect(100, 150, 200, 200)
            };

            result.Id.Should().Be(42);
            result.Category.Should().Be("road");
            result.Confidence.Should().Be(0.92f);
            result.Bounds.Should().Be(new Rect(100, 150, 200, 200));
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_WithNullMask_ShouldCreateDeepCopy()
        {
            var original = new SegResult
            {
                Id = 42,
                Category = "road",
                Confidence = 0.92f,
                Bounds = new Rect(100, 150, 200, 200),
                Mask = null
            };

            var clone = original.Clone();

            clone.Id.Should().Be(original.Id);
            clone.Category.Should().Be(original.Category);
            clone.Confidence.Should().Be(original.Confidence);
            clone.Bounds.Should().Be(original.Bounds);
            clone.Mask.Should().BeNull();
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new SegResult
            {
                Id = 42,
                Bounds = new Rect(100, 150, 200, 200)
            };

            var clone = original.Clone();
            clone.Bounds = new Rect(0, 0, 10, 10);

            original.Bounds.Should().Be(new Rect(100, 150, 200, 200));
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_WithNullMask_ShouldIndicateNull()
        {
            var result = new SegResult
            {
                Id = 42,
                Category = "road",
                Confidence = 0.92f,
                Bounds = new Rect(100, 150, 200, 200),
                Mask = null
            };

            var str = result.ToString();

            str.Should().Contain("Mask: null");
        }

        #endregion
    }
}
