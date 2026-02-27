using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class NonMaxSuppressionTests
    {
        #region RectNonMaxSuppression Tests

        [Fact]
        public void RectNonMaxSuppression_WithEmptyList_ShouldReturnEmptyArray()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>();

            var result = nms.Run(boxes, 0.5f);

            result.Should().BeEmpty();
        }

        [Fact]
        public void RectNonMaxSuppression_WithSingleBox_ShouldReturnThatBox()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                }
            };

            var result = nms.Run(boxes, 0.5f);

            result.Should().HaveCount(1);
            result[0].Confidence.Should().Be(0.9f);
        }

        [Fact]
        public void RectNonMaxSuppression_WithNonOverlappingBoxes_ShouldKeepAll()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 50, 50),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(100, 100, 50, 50),
                    NameIndex = 0
                }
            };

            var result = nms.Run(boxes, 0.5f);

            result.Should().HaveCount(2);
        }

        [Fact]
        public void RectNonMaxSuppression_WithHighlyOverlappingSameClass_ShouldKeepHighestConfidence()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(10, 10, 100, 100),
                    NameIndex = 0
                }
            };

            var result = nms.Run(boxes, 0.5f);

            result.Should().HaveCount(1);
            result[0].Confidence.Should().Be(0.9f);
        }

        [Fact]
        public void RectNonMaxSuppression_WithOverlappingDifferentClass_ShouldKeepBoth()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(10, 10, 100, 100),
                    NameIndex = 1
                }
            };

            var result = nms.Run(boxes, 0.5f);

            result.Should().HaveCount(2);
        }

        [Fact]
        public void RectNonMaxSuppression_WithLowIouThreshold_ShouldSuppressMore()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(20, 20, 100, 100),
                    NameIndex = 0
                }
            };

            // With low IoU threshold, even small overlap will suppress
            var result = nms.Run(boxes, 0.1f);

            result.Should().HaveCount(1);
        }

        [Fact]
        public void RectNonMaxSuppression_WithHighIouThreshold_ShouldKeepMore()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(20, 20, 100, 100),
                    NameIndex = 0
                }
            };

            // With high IoU threshold, need large overlap to suppress
            var result = nms.Run(boxes, 0.9f);

            result.Should().HaveCount(2);
        }

        [Fact]
        public void RectNonMaxSuppression_WithArrayInput_ShouldWork()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new BoundingBox[]
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 50, 50),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(100, 100, 50, 50),
                    NameIndex = 0
                }
            };

            var result = nms.Run(boxes, 0.5f);

            result.Should().HaveCount(2);
        }

        [Fact]
        public void RectNonMaxSuppression_WithMultipleBoxesSameClass_ShouldSuppressCorrectly()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.95f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.90f,
                    Box = new RectF(5, 5, 100, 100),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.85f,
                    Box = new RectF(200, 200, 100, 100),
                    NameIndex = 0
                }
            };

            var result = nms.Run(boxes, 0.5f);

            // First and third should be kept, second should be suppressed
            result.Should().HaveCount(2);
            result[0].Confidence.Should().Be(0.95f);
            result[1].Confidence.Should().Be(0.85f);
        }

        [Fact]
        public void RectNonMaxSuppression_WithNullBox_ShouldRemoveNull()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                },
                null!,
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(200, 200, 100, 100),
                    NameIndex = 0
                }
            };

            var result = nms.Run(boxes, 0.5f);

            result.Should().HaveCount(2);
        }

        [Fact]
        public void RectNonMaxSuppression_WithZeroAreaBox_ShouldReturnZeroIou()
        {
            var nms = new RectNonMaxSuppression();
            var boxes = new List<BoundingBox>
            {
                new BoundingBox
                {
                    Confidence = 0.9f,
                    Box = new RectF(0, 0, 0, 0),
                    NameIndex = 0
                },
                new BoundingBox
                {
                    Confidence = 0.8f,
                    Box = new RectF(0, 0, 100, 100),
                    NameIndex = 0
                }
            };

            var result = nms.Run(boxes, 0.5f);

            // Both should be kept since zero area box has no overlap
            result.Should().HaveCount(2);
        }

        #endregion
    }
}
