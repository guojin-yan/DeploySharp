using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class BoundingBoxTests
    {
        #region Property Tests

        [Fact]
        public void Properties_ShouldSetAndGetCorrectly()
        {
            var box = new BoundingBox
            {
                Index = 1,
                NameIndex = 42,
                Confidence = 0.95f,
                Box = new RectF(10.5f, 20.5f, 100.5f, 50.5f),
                Angle = 45.0f
            };

            box.Index.Should().Be(1);
            box.NameIndex.Should().Be(42);
            box.Confidence.Should().Be(0.95f);
            box.Box.Should().Be(new RectF(10.5f, 20.5f, 100.5f, 50.5f));
            box.Angle.Should().Be(45.0f);
        }

        [Fact]
        public void Confidence_SetToValidValue_ShouldWork()
        {
            var box = new BoundingBox
            {
                Confidence = 0.5f
            };

            box.Confidence.Should().Be(0.5f);
        }

        [Fact]
        public void Box_SetToValidValue_ShouldWork()
        {
            var box = new BoundingBox
            {
                Box = new RectF(0f, 0f, 100f, 100f)
            };

            box.Box.X.Should().Be(0f);
            box.Box.Y.Should().Be(0f);
            box.Box.Width.Should().Be(100f);
            box.Box.Height.Should().Be(100f);
        }

        #endregion

        #region CompareTo Tests

        [Fact]
        public void CompareTo_WithHigherConfidence_ShouldReturnPositive()
        {
            var box1 = new BoundingBox { Confidence = 0.9f };
            var box2 = new BoundingBox { Confidence = 0.5f };

            var result = box1.CompareTo(box2);

            result.Should().BeGreaterThan(0);
        }

        [Fact]
        public void CompareTo_WithLowerConfidence_ShouldReturnNegative()
        {
            var box1 = new BoundingBox { Confidence = 0.5f };
            var box2 = new BoundingBox { Confidence = 0.9f };

            var result = box1.CompareTo(box2);

            result.Should().BeLessThan(0);
        }

        [Fact]
        public void CompareTo_WithEqualConfidence_ShouldReturnZero()
        {
            var box1 = new BoundingBox { Confidence = 0.5f };
            var box2 = new BoundingBox { Confidence = 0.5f };

            var result = box1.CompareTo(box2);

            result.Should().Be(0);
        }

        [Fact]
        public void CompareTo_WithZeroConfidence_ShouldWork()
        {
            var box1 = new BoundingBox { Confidence = 0f };
            var box2 = new BoundingBox { Confidence = 0.5f };

            var result = box1.CompareTo(box2);

            result.Should().BeLessThan(0);
        }

        [Fact]
        public void CompareTo_WithMaxConfidence_ShouldWork()
        {
            var box1 = new BoundingBox { Confidence = 1f };
            var box2 = new BoundingBox { Confidence = 0.5f };

            var result = box1.CompareTo(box2);

            result.Should().BeGreaterThan(0);
        }

        #endregion

        #region Sorting Tests

        [Fact]
        public void Sort_WithMultipleBoxes_ShouldSortByConfidenceDescending()
        {
            var boxes = new List<BoundingBox>
            {
                new BoundingBox { Confidence = 0.5f },
                new BoundingBox { Confidence = 0.9f },
                new BoundingBox { Confidence = 0.7f },
                new BoundingBox { Confidence = 0.3f }
            };

            boxes.Sort();
            boxes.Reverse(); // Reverse to get descending order

            boxes[0].Confidence.Should().Be(0.9f);
            boxes[1].Confidence.Should().Be(0.7f);
            boxes[2].Confidence.Should().Be(0.5f);
            boxes[3].Confidence.Should().Be(0.3f);
        }

        #endregion
    }
}
