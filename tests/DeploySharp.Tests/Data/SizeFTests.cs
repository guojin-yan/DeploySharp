using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class SizeFTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithFloatParameters_ShouldSetCorrectValues()
        {
            var size = new SizeF(100.5f, 50.5f);

            size.Width.Should().Be(100.5f);
            size.Height.Should().Be(50.5f);
        }

        [Fact]
        public void Constructor_WithDoubleParameters_ShouldTruncateToFloat()
        {
            var size = new SizeF(100.7, 50.3);

            size.Width.Should().Be(100.7f);
            size.Height.Should().Be(50.3f);
        }

        [Fact]
        public void Constructor_WithNegativeValues_ShouldWorkCorrectly()
        {
            var size = new SizeF(-100.5f, -50.5f);

            size.Width.Should().Be(-100.5f);
            size.Height.Should().Be(-50.5f);
        }

        [Fact]
        public void Constructor_WithZeroValues_ShouldWorkCorrectly()
        {
            var size = new SizeF(0f, 0f);

            size.Width.Should().Be(0f);
            size.Height.Should().Be(0f);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var size = new SizeF(100.5f, 50.5f);

            var result = size.ToString();

            result.Should().Be("(Width: 100.50, Height: 50.50)");
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameValues_ShouldReturnTrue()
        {
            var s1 = new SizeF(100.5f, 50.5f);
            var s2 = new SizeF(100.5f, 50.5f);

            var result = s1.Equals(s2);

            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentValues_ShouldReturnFalse()
        {
            var s1 = new SizeF(100.5f, 50.5f);
            var s2 = new SizeF(100.5f, 51.5f);

            var result = s1.Equals(s2);

            result.Should().BeFalse();
        }

        [Fact]
        public void EqualityOperator_WithSameValues_ShouldReturnTrue()
        {
            var s1 = new SizeF(100.5f, 50.5f);
            var s2 = new SizeF(100.5f, 50.5f);

            (s1 == s2).Should().BeTrue();
        }

        [Fact]
        public void InequalityOperator_WithDifferentValues_ShouldReturnTrue()
        {
            var s1 = new SizeF(100.5f, 50.5f);
            var s2 = new SizeF(100.5f, 51.5f);

            (s1 != s2).Should().BeTrue();
        }

        #endregion
    }
}
