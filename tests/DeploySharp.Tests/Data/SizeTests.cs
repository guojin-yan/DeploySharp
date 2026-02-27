using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class SizeTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithIntParameters_ShouldSetCorrectValues()
        {
            var size = new Size(100, 50);

            size.Width.Should().Be(100);
            size.Height.Should().Be(50);
        }

        [Fact]
        public void Constructor_WithDoubleParameters_ShouldTruncateToInt()
        {
            var size = new Size(100.7, 50.3);

            size.Width.Should().Be(100);
            size.Height.Should().Be(50);
        }

        [Fact]
        public void Constructor_WithNegativeValues_ShouldWorkCorrectly()
        {
            var size = new Size(-100, -50);

            size.Width.Should().Be(-100);
            size.Height.Should().Be(-50);
        }

        [Fact]
        public void Constructor_WithZeroValues_ShouldWorkCorrectly()
        {
            var size = new Size(0, 0);

            size.Width.Should().Be(0);
            size.Height.Should().Be(0);
        }

        #endregion

        #region Explicit Conversion Tests

        [Fact]
        public void ExplicitConversion_FromSizeD_ShouldTruncateToInt()
        {
            var sizeD = new SizeD(100.7, 50.3);

            var size = (Size)sizeD;

            size.Width.Should().Be(100);
            size.Height.Should().Be(50);
        }

        [Fact]
        public void ExplicitConversion_FromSizeF_ShouldTruncateToInt()
        {
            var sizeF = new SizeF(100.7f, 50.3f);

            var size = (Size)sizeF;

            size.Width.Should().Be(100);
            size.Height.Should().Be(50);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var size = new Size(100, 50);

            var result = size.ToString();

            result.Should().Be("(Width: 100, Height: 50)");
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameValues_ShouldReturnTrue()
        {
            var s1 = new Size(100, 50);
            var s2 = new Size(100, 50);

            var result = s1.Equals(s2);

            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentValues_ShouldReturnFalse()
        {
            var s1 = new Size(100, 50);
            var s2 = new Size(100, 51);

            var result = s1.Equals(s2);

            result.Should().BeFalse();
        }

        [Fact]
        public void EqualityOperator_WithSameValues_ShouldReturnTrue()
        {
            var s1 = new Size(100, 50);
            var s2 = new Size(100, 50);

            (s1 == s2).Should().BeTrue();
        }

        [Fact]
        public void InequalityOperator_WithDifferentValues_ShouldReturnTrue()
        {
            var s1 = new Size(100, 50);
            var s2 = new Size(100, 51);

            (s1 != s2).Should().BeTrue();
        }

        #endregion
    }
}
