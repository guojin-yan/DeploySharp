using DeploySharp.Data;
using DeploySharp.Common;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class ResultTests
    {
        #region Property Tests

        [Fact]
        public void DefaultConstructor_ShouldSetDefaultValues()
        {
            var result = new Result();

            result.Type.Should().Be(ResultType.Classification);
            result.Id.Should().Be(0);
            result.Confidence.Should().Be(0f);
        }

        [Fact]
        public void Type_SetValue_ShouldUpdate()
        {
            var result = new Result();

            result.Type = ResultType.Detection;

            result.Type.Should().Be(ResultType.Detection);
        }

        [Fact]
        public void Id_SetValue_ShouldUpdate()
        {
            var result = new Result();

            result.Id = 42;

            result.Id.Should().Be(42);
        }

        [Fact]
        public void ImageSize_SetValue_ShouldUpdate()
        {
            var result = new Result();
            var size = new Size(1920, 1080);

            result.ImageSize = size;

            result.ImageSize.Should().Be(size);
        }

        [Fact]
        public void Confidence_SetValue_ShouldUpdate()
        {
            var result = new Result();

            result.Confidence = 0.95f;

            result.Confidence.Should().Be(0.95f);
        }

        #endregion

        #region Category Tests

        [Fact]
        public void Category_WithValueSet_ShouldReturnValue()
        {
            var result = new Result { Category = "dog" };

            result.Category.Should().Be("dog");
        }

        [Fact]
        public void Category_WithNullOrEmpty_ShouldReturnIdAsString()
        {
            var result = new Result { Id = 42 };

            result.Category.Should().Be("42");
        }

        [Fact]
        public void Category_SetNull_ShouldReturnIdAsString()
        {
            var result = new Result { Id = 42, Category = null };

            result.Category.Should().Be("42");
        }

        [Fact]
        public void Category_SetEmptyString_ShouldReturnIdAsString()
        {
            var result = new Result { Id = 42, Category = "" };

            result.Category.Should().Be("42");
        }

        #endregion

        #region UpdateCategory Tests

        [Fact]
        public void UpdateCategory_WithValidCategories_ShouldSetCategory()
        {
            var result = new Result { Id = 1 };
            var categories = new[] { "cat", "dog", "bird" };

            result.UpdateCategory(categories);

            result.Category.Should().Be("dog");
        }

        [Fact]
        public void UpdateCategory_WithEmptyCategories_ShouldSetNullCategory()
        {
            var result = new Result { Id = 0 };
            var categories = Array.Empty<string>();

            // When categories is empty, Category is set to null
            // But Category getter returns Id.ToString() when _category is null
            result.UpdateCategory(categories);
            
            // The internal _category is null, but getter returns "0"
            result.Category.Should().Be("0");
        }

        [Fact]
        public void UpdateCategory_WithIdExceedingLength_ShouldThrowException()
        {
            var result = new Result { Id = 5 };
            var categories = new[] { "cat", "dog" };

            Action act = () => result.UpdateCategory(categories);

            act.Should().Throw<DeploySharpException>()
                .WithMessage("*categories.Length(2) < Result.Id(5)*");
        }

        [Fact]
        public void UpdateCategory_WithZeroIdAndEmptyCategories_ShouldNotThrow()
        {
            var result = new Result { Id = 0 };
            var categories = Array.Empty<string>();

            // Empty categories array with Id=0 is valid (0 < 0 is false)
            // Category will be set to null, but getter returns "0"
            result.UpdateCategory(categories);
            result.Category.Should().Be("0");
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_ShouldCreateDeepCopy()
        {
            var original = new Result
            {
                Type = ResultType.Classification,
                Id = 42,
                Category = "dog",
                Confidence = 0.95f,
                ImageSize = new Size(100, 100)
            };

            var clone = original.Clone();

            clone.Type.Should().Be(original.Type);
            clone.Id.Should().Be(original.Id);
            clone.Category.Should().Be(original.Category);
            clone.Confidence.Should().Be(original.Confidence);
            clone.ImageSize.Should().Be(original.ImageSize);
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new Result
            {
                Id = 42,
                Category = "dog",
                Confidence = 0.95f
            };

            var clone = original.Clone();
            clone.Id = 100;
            clone.Category = "cat";

            original.Id.Should().Be(42);
            original.Category.Should().Be("dog");
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var result = new Result
            {
                Id = 42,
                Category = "dog",
                Confidence = 0.95f,
                ImageSize = new Size(100, 100)
            };

            var str = result.ToString();

            str.Should().Contain("ID: 42");
            str.Should().Contain("Category: dog");
            str.Should().Contain("Confidence:");
            str.Should().Contain("Image Size:");
        }

        #endregion
    }

    public class DetResultTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldSetTypeToDetection()
        {
            var result = new DetResult();

            result.Type.Should().Be(ResultType.Detection);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Bounds_SetValue_ShouldUpdate()
        {
            var result = new DetResult();
            var bounds = new Rect(10, 20, 100, 200);

            result.Bounds = bounds;

            result.Bounds.Should().Be(bounds);
        }

        [Fact]
        public void InheritedProperties_ShouldWork()
        {
            var result = new DetResult
            {
                Id = 42,
                Category = "person",
                Confidence = 0.87f,
                Bounds = new Rect(100, 200, 50, 80)
            };

            result.Id.Should().Be(42);
            result.Category.Should().Be("person");
            result.Confidence.Should().Be(0.87f);
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_ShouldCreateDeepCopy()
        {
            var original = new DetResult
            {
                Id = 42,
                Category = "person",
                Confidence = 0.87f,
                Bounds = new Rect(100, 200, 50, 80)
            };

            var clone = original.Clone();

            clone.Id.Should().Be(original.Id);
            clone.Category.Should().Be(original.Category);
            clone.Confidence.Should().Be(original.Confidence);
            clone.Bounds.Should().Be(original.Bounds);
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new DetResult
            {
                Id = 42,
                Bounds = new Rect(100, 200, 50, 80)
            };

            var clone = original.Clone();
            clone.Bounds = new Rect(0, 0, 10, 10);

            original.Bounds.Should().Be(new Rect(100, 200, 50, 80));
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldIncludeBounds()
        {
            var result = new DetResult
            {
                Id = 42,
                Category = "person",
                Confidence = 0.87f,
                Bounds = new Rect(100, 200, 50, 80)
            };

            var str = result.ToString();

            str.Should().Contain("ID: 42");
            str.Should().Contain("Category: person");
            str.Should().Contain("Bounds:");
        }

        #endregion
    }

    public class ResultTypeTests
    {
        [Theory]
        [InlineData(ResultType.Classification, 0)]
        [InlineData(ResultType.Detection, 1)]
        [InlineData(ResultType.OrientedBoundingBoxes, 2)]
        [InlineData(ResultType.Segmentation, 3)]
        [InlineData(ResultType.KeyPoints, 4)]
        [InlineData(ResultType.AnomalySegmentation, 5)]
        [InlineData(ResultType.TextRecResult, 6)]
        [InlineData(ResultType.OcrResult, 7)]
        public void ResultType_ShouldHaveExpectedValue(ResultType type, int expectedValue)
        {
            ((int)type).Should().Be(expectedValue);
        }
    }
}
