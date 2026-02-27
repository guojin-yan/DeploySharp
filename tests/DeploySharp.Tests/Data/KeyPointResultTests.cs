using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class KeyPointTests
    {
        #region Property Tests

        [Fact]
        public void Confidence_SetValue_ShouldUpdate()
        {
            var kp = new KeyPoint { Confidence = 0.95f };

            kp.Confidence.Should().Be(0.95f);
        }

        [Fact]
        public void Point_SetValue_ShouldUpdate()
        {
            var kp = new KeyPoint { Point = new Point(120, 130) };

            kp.Point.Should().Be(new Point(120, 130));
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var kp = new KeyPoint 
            { 
                Confidence = 0.95f, 
                Point = new Point(120, 130) 
            };

            var str = kp.ToString();

            str.Should().Contain("Confidence:");
            str.Should().Contain("0.95");
            str.Should().Contain("Point:");
        }

        #endregion

        #region DistanceTo Tests

        [Fact]
        public void DistanceTo_SamePoint_ShouldReturnZero()
        {
            var kp1 = new KeyPoint { Point = new Point(100, 100) };
            var kp2 = new KeyPoint { Point = new Point(100, 100) };

            var distance = kp1.DistanceTo(kp2);

            distance.Should().Be(0f);
        }

        [Fact]
        public void DistanceTo_HorizontalPoints_ShouldReturnCorrectDistance()
        {
            var kp1 = new KeyPoint { Point = new Point(0, 0) };
            var kp2 = new KeyPoint { Point = new Point(3, 0) };

            var distance = kp1.DistanceTo(kp2);

            distance.Should().Be(3f);
        }

        [Fact]
        public void DistanceTo_VerticalPoints_ShouldReturnCorrectDistance()
        {
            var kp1 = new KeyPoint { Point = new Point(0, 0) };
            var kp2 = new KeyPoint { Point = new Point(0, 4) };

            var distance = kp1.DistanceTo(kp2);

            distance.Should().Be(4f);
        }

        [Fact]
        public void DistanceTo_DiagonalPoints_ShouldReturnCorrectDistance()
        {
            var kp1 = new KeyPoint { Point = new Point(0, 0) };
            var kp2 = new KeyPoint { Point = new Point(3, 4) };

            var distance = kp1.DistanceTo(kp2);

            distance.Should().Be(5f);
        }

        #endregion
    }

    public class KeyPointResultTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldSetTypeToKeyPoints()
        {
            var result = new KeyPointResult();

            result.Type.Should().Be(ResultType.KeyPoints);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void KeyPoints_SetValue_ShouldUpdate()
        {
            var result = new KeyPointResult();
            var keyPoints = new[]
            {
                new KeyPoint { Point = new Point(120, 130), Confidence = 0.95f },
                new KeyPoint { Point = new Point(150, 140), Confidence = 0.92f }
            };

            result.KeyPoints = keyPoints;

            result.KeyPoints.Should().HaveCount(2);
        }

        [Fact]
        public void Indexer_WithValidIndex_ShouldReturnKeyPoint()
        {
            var result = new KeyPointResult
            {
                KeyPoints = new[]
                {
                    new KeyPoint { Point = new Point(120, 130), Confidence = 0.95f }
                }
            };

            var kp = result[0];

            kp.Point.Should().Be(new Point(120, 130));
            kp.Confidence.Should().Be(0.95f);
        }

        [Fact]
        public void Count_WithKeyPoints_ShouldReturnLength()
        {
            var result = new KeyPointResult
            {
                KeyPoints = new[]
                {
                    new KeyPoint { Point = new Point(120, 130) },
                    new KeyPoint { Point = new Point(150, 140) }
                }
            };

            result.Count.Should().Be(2);
        }

        [Fact]
        public void Count_WithNullKeyPoints_ShouldReturnZero()
        {
            var result = new KeyPointResult();

            result.Count.Should().Be(0);
        }

        #endregion

        #region Enumeration Tests

        [Fact]
        public void GetEnumerator_WithKeyPoints_ShouldIterate()
        {
            var result = new KeyPointResult
            {
                KeyPoints = new[]
                {
                    new KeyPoint { Point = new Point(120, 130) },
                    new KeyPoint { Point = new Point(150, 140) }
                }
            };

            var count = 0;
            foreach (var kp in result)
            {
                count++;
            }

            count.Should().Be(2);
        }

        [Fact]
        public void GetEnumerator_WithNullKeyPoints_ShouldReturnEmpty()
        {
            var result = new KeyPointResult();

            var items = result.ToList();

            items.Should().BeEmpty();
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_WithKeyPoints_ShouldReturnFormattedString()
        {
            var result = new KeyPointResult
            {
                Id = 1,
                Category = "person",
                Confidence = 0.95f,
                KeyPoints = new[]
                {
                    new KeyPoint { Point = new Point(120, 130), Confidence = 0.95f }
                }
            };

            var str = result.ToString();

            str.Should().Contain("ID: 1");
            str.Should().Contain("Category: person");
            str.Should().Contain("KeyPoints:");
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_ShouldCreateDeepCopy()
        {
            var original = new KeyPointResult
            {
                Id = 42,
                Category = "person",
                Confidence = 0.87f,
                Bounds = new Rect(100, 200, 50, 80),
                KeyPoints = new[]
                {
                    new KeyPoint { Point = new Point(120, 130), Confidence = 0.95f }
                }
            };

            var clone = original.Clone();

            clone.Id.Should().Be(original.Id);
            clone.Category.Should().Be(original.Category);
            clone.Confidence.Should().Be(original.Confidence);
            clone.Bounds.Should().Be(original.Bounds);
            clone.KeyPoints.Should().HaveCount(1);
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new KeyPointResult
            {
                KeyPoints = new[]
                {
                    new KeyPoint { Point = new Point(120, 130), Confidence = 0.95f }
                }
            };

            var clone = original.Clone();
            // Modify clone's keypoints
            clone.KeyPoints[0] = new KeyPoint { Point = new Point(0, 0), Confidence = 0f };

            // Original should remain unchanged
            original.KeyPoints[0].Point.Should().Be(new Point(120, 130));
            original.KeyPoints[0].Confidence.Should().Be(0.95f);
        }

        #endregion

        #region GetAverageConfidence Tests

        [Fact]
        public void GetAverageConfidence_WithKeyPoints_ShouldReturnAverage()
        {
            var result = new KeyPointResult
            {
                KeyPoints = new[]
                {
                    new KeyPoint { Confidence = 0.8f },
                    new KeyPoint { Confidence = 0.9f },
                    new KeyPoint { Confidence = 1.0f }
                }
            };

            var avg = result.GetAverageConfidence();

            avg.Should().BeApproximately(0.9f, 0.001f);
        }

        [Fact]
        public void GetAverageConfidence_WithNullKeyPoints_ShouldReturnZero()
        {
            var result = new KeyPointResult();

            var avg = result.GetAverageConfidence();

            avg.Should().Be(0f);
        }

        [Fact]
        public void GetAverageConfidence_WithEmptyKeyPoints_ShouldReturnZero()
        {
            var result = new KeyPointResult
            {
                KeyPoints = Array.Empty<KeyPoint>()
            };

            var avg = result.GetAverageConfidence();

            avg.Should().Be(0f);
        }

        #endregion
    }
}
