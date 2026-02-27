using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class AnomalySegResultTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldSetTypeToAnomalySegmentation()
        {
            var result = new AnomalySegResult();

            result.Type.Should().Be(ResultType.AnomalySegmentation);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void RawMask_SetValue_ShouldUpdate()
        {
            var result = new AnomalySegResult();
            // ImageDataF would need proper initialization in real scenarios
            result.RawMask = null;

            result.RawMask.Should().BeNull();
        }

        [Fact]
        public void InheritedProperties_ShouldWork()
        {
            var result = new AnomalySegResult
            {
                Id = 42,
                Category = "defect",
                Confidence = 0.92f,
                Bounds = new Rect(100, 150, 200, 200)
            };

            result.Id.Should().Be(42);
            result.Category.Should().Be("defect");
            result.Confidence.Should().Be(0.92f);
            result.Bounds.Should().Be(new Rect(100, 150, 200, 200));
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_WithNullMasks_ShouldCreateDeepCopy()
        {
            var original = new AnomalySegResult
            {
                Id = 42,
                Category = "defect",
                Confidence = 0.92f,
                Bounds = new Rect(100, 150, 200, 200),
                Mask = null,
                RawMask = null
            };

            var clone = original.Clone();

            clone.Id.Should().Be(original.Id);
            clone.Category.Should().Be(original.Category);
            clone.Confidence.Should().Be(original.Confidence);
            clone.Bounds.Should().Be(original.Bounds);
            clone.Mask.Should().BeNull();
            clone.RawMask.Should().BeNull();
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new AnomalySegResult
            {
                Id = 42,
                Bounds = new Rect(100, 150, 200, 200)
            };

            var clone = original.Clone();
            clone.Bounds = new Rect(0, 0, 10, 10);

            original.Bounds.Should().Be(new Rect(100, 150, 200, 200));
        }

        #endregion
    }
}
