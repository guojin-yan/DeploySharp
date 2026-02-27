using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class UtilTests
    {
        #region FindMaxInRange Tests

        [Fact]
        public void FindMaxInRange_WithValidArray_ShouldReturnMaxValueAndIndex()
        {
            var array = new float[] { 0.1f, 0.5f, 0.9f, 0.3f, 0.7f };

            var (maxValue, index) = Util.FindMaxInRange(array, 0, 4);

            maxValue.Should().Be(0.9f);
            index.Should().Be(2);
        }

        [Fact]
        public void FindMaxInRange_WithPartialRange_ShouldReturnMaxInRange()
        {
            var array = new float[] { 0.9f, 0.5f, 0.8f, 0.3f, 0.7f };

            var (maxValue, index) = Util.FindMaxInRange(array, 1, 3);

            maxValue.Should().Be(0.8f);
            index.Should().Be(1);
        }

        [Fact]
        public void FindMaxInRange_WithSingleElement_ShouldReturnThatElement()
        {
            var array = new float[] { 0.5f };

            var (maxValue, index) = Util.FindMaxInRange(array, 0, 0);

            maxValue.Should().Be(0.5f);
            index.Should().Be(0);
        }

        [Fact]
        public void FindMaxInRange_WithNullArray_ShouldThrowArgumentException()
        {
            float[]? array = null;

            Action act = () => Util.FindMaxInRange(array!, 0, 0);

            act.Should().Throw<ArgumentException>().WithParameterName("array");
        }

        [Fact]
        public void FindMaxInRange_WithEmptyArray_ShouldThrowArgumentException()
        {
            var array = Array.Empty<float>();

            Action act = () => Util.FindMaxInRange(array, 0, 0);

            act.Should().Throw<ArgumentException>().WithParameterName("array");
        }

        [Fact]
        public void FindMaxInRange_WithNegativeStartIndex_ShouldThrowArgumentOutOfRangeException()
        {
            var array = new float[] { 0.5f, 0.6f };

            Action act = () => Util.FindMaxInRange(array, -1, 1);

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Invalid start or end index.*");
        }

        [Fact]
        public void FindMaxInRange_WithEndIndexOutOfRange_ShouldThrowArgumentOutOfRangeException()
        {
            var array = new float[] { 0.5f, 0.6f };

            Action act = () => Util.FindMaxInRange(array, 0, 10);

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Invalid start or end index.*");
        }

        [Fact]
        public void FindMaxInRange_WithStartIndexGreaterThanEndIndex_ShouldThrowArgumentOutOfRangeException()
        {
            var array = new float[] { 0.5f, 0.6f };

            Action act = () => Util.FindMaxInRange(array, 1, 0);

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Invalid start or end index.*");
        }

        [Fact]
        public void FindMaxInRange_WithIntArray_ShouldWork()
        {
            var array = new int[] { 5, 3, 8, 1, 9 };

            var (maxValue, index) = Util.FindMaxInRange(array, 0, 5);

            maxValue.Should().Be(9);
            index.Should().Be(4);
        }

        [Fact]
        public void FindMaxInRange_WithDoubleArray_ShouldWork()
        {
            var array = new double[] { 5.5, 3.3, 8.8, 1.1, 9.9 };

            var (maxValue, index) = Util.FindMaxInRange(array, 0, 5);

            maxValue.Should().Be(9.9);
            index.Should().Be(4);
        }

        [Fact]
        public void FindMaxInRange_IndexShouldBeRelativeToStartIndex()
        {
            var array = new float[] { 0.1f, 0.5f, 0.9f, 0.3f, 0.7f };

            var (maxValue, index) = Util.FindMaxInRange(array, 1, 3);

            // 0.9f is at absolute index 2, relative to start index 1, it should be 1
            maxValue.Should().Be(0.9f);
            index.Should().Be(1);
        }

        #endregion

        #region FindMinInRange Tests

        [Fact]
        public void FindMinInRange_WithValidArray_ShouldReturnMinValueAndIndex()
        {
            var array = new float[] { 0.9f, 0.5f, 0.1f, 0.3f, 0.7f };

            var (minValue, index) = Util.FindMinInRange(array, 0, 4);

            minValue.Should().Be(0.1f);
            index.Should().Be(2);
        }

        [Fact]
        public void FindMinInRange_WithPartialRange_ShouldReturnMinInRange()
        {
            var array = new float[] { 0.1f, 0.5f, 0.2f, 0.3f, 0.7f };

            var (minValue, index) = Util.FindMinInRange(array, 1, 3);

            minValue.Should().Be(0.2f);
            index.Should().Be(1);
        }

        [Fact]
        public void FindMinInRange_WithSingleElement_ShouldReturnThatElement()
        {
            var array = new float[] { 0.5f };

            var (minValue, index) = Util.FindMinInRange(array, 0, 0);

            minValue.Should().Be(0.5f);
            index.Should().Be(0);
        }

        [Fact]
        public void FindMinInRange_WithNullArray_ShouldThrowArgumentException()
        {
            float[]? array = null;

            Action act = () => Util.FindMinInRange(array!, 0, 0);

            act.Should().Throw<ArgumentException>().WithParameterName("array");
        }

        [Fact]
        public void FindMinInRange_WithEmptyArray_ShouldThrowArgumentException()
        {
            var array = Array.Empty<float>();

            Action act = () => Util.FindMinInRange(array, 0, 0);

            act.Should().Throw<ArgumentException>().WithParameterName("array");
        }

        [Fact]
        public void FindMinInRange_WithNegativeStartIndex_ShouldThrowArgumentOutOfRangeException()
        {
            var array = new float[] { 0.5f, 0.6f };

            Action act = () => Util.FindMinInRange(array, -1, 1);

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Invalid start or end index.*");
        }

        [Fact]
        public void FindMinInRange_WithEndIndexOutOfRange_ShouldThrowArgumentOutOfRangeException()
        {
            var array = new float[] { 0.5f, 0.6f };

            Action act = () => Util.FindMinInRange(array, 0, 10);

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Invalid start or end index.*");
        }

        [Fact]
        public void FindMinInRange_WithStartIndexGreaterThanEndIndex_ShouldThrowArgumentOutOfRangeException()
        {
            var array = new float[] { 0.5f, 0.6f };

            Action act = () => Util.FindMinInRange(array, 1, 0);

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Invalid start or end index.*");
        }

        [Fact]
        public void FindMinInRange_WithIntArray_ShouldWork()
        {
            var array = new int[] { 5, 3, 8, 1, 9 };

            var (minValue, index) = Util.FindMinInRange(array, 0, 4);

            minValue.Should().Be(1);
            index.Should().Be(3);
        }

        [Fact]
        public void FindMinInRange_WithDoubleArray_ShouldWork()
        {
            var array = new double[] { 5.5, 3.3, 8.8, 1.1, 9.9 };

            var (minValue, index) = Util.FindMinInRange(array, 0, 4);

            minValue.Should().Be(1.1);
            index.Should().Be(3);
        }

        [Fact]
        public void FindMinInRange_IndexShouldBeRelativeToStartIndex()
        {
            var array = new float[] { 0.9f, 0.5f, 0.1f, 0.3f, 0.7f };

            var (minValue, index) = Util.FindMinInRange(array, 1, 3);

            // 0.1f is at absolute index 2, relative to start index 1, it should be 1
            minValue.Should().Be(0.1f);
            index.Should().Be(1);
        }

        #endregion
    }
}
