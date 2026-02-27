using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class SizeDTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithDoubleParameters_ShouldSetCorrectValues()
        {
            var size = new SizeD(100.5, 50.5);

            size.Width.Should().Be(100.5);
            size.Height.Should().Be(50.5);
        }

        [Fact]
        public void Constructor_WithNegativeValues_ShouldWorkCorrectly()
        {
            var size = new SizeD(-100.5, -50.5);

            size.Width.Should().Be(-100.5);
            size.Height.Should().Be(-50.5);
        }

        [Fact]
        public void Constructor_WithZeroValues_ShouldWorkCorrectly()
        {
            var size = new SizeD(0.0, 0.0);

            size.Width.Should().Be(0.0);
            size.Height.Should().Be(0.0);
        }

        [Fact]
        public void Constructor_WithHighPrecisionValues_ShouldPreservePrecision()
        {
            var size = new SizeD(100.123456789, 50.987654321);

            size.Width.Should().Be(100.123456789);
            size.Height.Should().Be(50.987654321);
        }

        #endregion

        #region Implicit Conversion Tests

        [Fact]
        public void ImplicitConversion_FromSize_ShouldConvertCorrectly()
        {
            var size = new Size(100, 50);

            SizeD sizeD = size;

            sizeD.Width.Should().Be(100.0);
            sizeD.Height.Should().Be(50.0);
        }

        [Fact]
        public void ImplicitConversion_FromSizeF_ShouldConvertCorrectly()
        {
            var sizeF = new SizeF(100.5f, 50.5f);

            SizeD sizeD = sizeF;

            sizeD.Width.Should().Be(100.5f);
            sizeD.Height.Should().Be(50.5f);
        }

        #endregion

        #region ToSize Tests

        [Fact]
        public void ToSize_ShouldTruncateToInt()
        {
            var sizeD = new SizeD(100.9, 50.9);

            var result = sizeD.ToSize();

            result.Width.Should().Be(100);
            result.Height.Should().Be(50);
        }

        [Fact]
        public void ToSize_WithNegativeValues_ShouldTruncateCorrectly()
        {
            var sizeD = new SizeD(-100.9, -50.9);

            var result = sizeD.ToSize();

            result.Width.Should().Be(-100);
            result.Height.Should().Be(-50);
        }

        #endregion

        #region ToSizeF Tests

        [Fact]
        public void ToSizeF_ShouldConvertToFloat()
        {
            var sizeD = new SizeD(100.5, 50.5);

            var result = sizeD.ToSizeF();

            result.Width.Should().Be(100.5f);
            result.Height.Should().Be(50.5f);
        }

        [Fact]
        public void ToSizeF_WithHighPrecision_ShouldLosePrecision()
        {
            var sizeD = new SizeD(100.123456789, 50.987654321);

            var result = sizeD.ToSizeF();

            result.Width.Should().BeApproximately(100.123456789f, 0.0001f);
            result.Height.Should().BeApproximately(50.987654321f, 0.0001f);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            var size = new SizeD(100.5678, 50.9012);

            var result = size.ToString();

            result.Should().Be("(Width: 100.57, Height: 50.90)");
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameValues_ShouldReturnTrue()
        {
            var s1 = new SizeD(100.5, 50.5);
            var s2 = new SizeD(100.5, 50.5);

            var result = s1.Equals(s2);

            result.Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentValues_ShouldReturnFalse()
        {
            var s1 = new SizeD(100.5, 50.5);
            var s2 = new SizeD(100.5, 50.6);

            var result = s1.Equals(s2);

            result.Should().BeFalse();
        }

        [Fact]
        public void EqualityOperator_WithSameValues_ShouldReturnTrue()
        {
            var s1 = new SizeD(100.5, 50.5);
            var s2 = new SizeD(100.5, 50.5);

            (s1 == s2).Should().BeTrue();
        }

        [Fact]
        public void InequalityOperator_WithDifferentValues_ShouldReturnTrue()
        {
            var s1 = new SizeD(100.5, 50.5);
            var s2 = new SizeD(100.5, 50.6);

            (s1 != s2).Should().BeTrue();
        }

        #endregion
    }
}
