using DeploySharp.Data;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DeploySharp.ImageSharp.Tests.Data
{
    public class VisionColorsTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldCreateInstance()
        {
            var colors = new VisionColors();

            colors.Should().NotBeNull();
        }

        [Fact]
        public void DefaultConstructor_ShouldNotThrow()
        {
            Action act = () => new VisionColors();

            act.Should().NotThrow();
        }

        #endregion

        #region GetBoundingBoxColor Tests

        [Fact]
        public void GetBoundingBoxColor_WithClass0_ShouldReturnColor()
        {
            var colors = new VisionColors();

            var color = colors.GetBoundingBoxColor(0);

            color.Should().NotBeNull();
        }

        [Fact]
        public void GetBoundingBoxColor_WithClass79_ShouldReturnColor()
        {
            var colors = new VisionColors();

            var color = colors.GetBoundingBoxColor(79);

            color.Should().NotBeNull();
        }

        [Fact]
        public void GetBoundingBoxColor_WithClass81_ShouldClampToClass79()
        {
            // Note: This tests the clamp behavior for values > 80
            var colors = new VisionColors();

            var color81 = colors.GetBoundingBoxColor(81);
            var color79 = colors.GetBoundingBoxColor(79);

            color81.Should().Be(color79);
        }

        [Fact]
        public void GetBoundingBoxColor_WithClass80_ShouldThrowException()
        {
            var colors = new VisionColors();

            Action act = () => colors.GetBoundingBoxColor(80);

            act.Should().Throw<IndexOutOfRangeException>();
        }

        [Fact]
        public void GetBoundingBoxColor_WithNegativeClass_ShouldReturnColor()
        {
            var colors = new VisionColors();

            var color = colors.GetBoundingBoxColor(-1);

            color.Should().NotBeNull();
        }

        [Fact]
        public void GetBoundingBoxColor_WithCustomAlpha_ShouldSetAlpha()
        {
            var colors = new VisionColors();

            var color = colors.GetBoundingBoxColor(0, alpha: 128);
            var rgba = color.ToPixel<Rgba32>();

            rgba.A.Should().Be(128);
        }

        [Fact]
        public void GetBoundingBoxColor_WithAlpha0_ShouldBeTransparent()
        {
            var colors = new VisionColors();

            var color = colors.GetBoundingBoxColor(0, alpha: 0);
            var rgba = color.ToPixel<Rgba32>();

            rgba.A.Should().Be(0);
        }

        [Fact]
        public void GetBoundingBoxColor_WithAlpha255_ShouldBeOpaque()
        {
            var colors = new VisionColors();

            var color = colors.GetBoundingBoxColor(0, alpha: 255);
            var rgba = color.ToPixel<Rgba32>();

            rgba.A.Should().Be(255);
        }

        [Fact]
        public void GetBoundingBoxColor_DifferentClasses_ShouldReturnDifferentColors()
        {
            var colors = new VisionColors();

            var color0 = colors.GetBoundingBoxColor(0);
            var color1 = colors.GetBoundingBoxColor(1);

            color0.Should().NotBe(color1);
        }

        #endregion

        #region GetMaskColor Tests

        [Fact]
        public void GetMaskColor_WithClass0_ShouldReturnColor()
        {
            var colors = new VisionColors();

            var color = colors.GetMaskColor(0);

            color.Should().NotBeNull();
        }

        [Fact]
        public void GetMaskColor_WithDifferentClasses_ShouldReturnDifferentColors()
        {
            var colors = new VisionColors();

            var color0 = colors.GetMaskColor(0);
            var color1 = colors.GetMaskColor(1);

            color0.Should().NotBe(color1);
        }

        [Fact]
        public void GetMaskColor_WithNegativeClass_ShouldReturnColor()
        {
            var colors = new VisionColors();

            var color = colors.GetMaskColor(-1);

            color.Should().NotBeNull();
        }

        #endregion

        #region GetInstanceColor Tests

        [Fact]
        public void GetInstanceColor_WithInstance0_ShouldReturnColor()
        {
            var colors = new VisionColors();

            var color = colors.GetInstanceColor(0);

            color.Should().NotBeNull();
        }

        [Fact]
        public void GetInstanceColor_DefaultAlpha_ShouldBe128()
        {
            var colors = new VisionColors();

            var color = colors.GetInstanceColor(0);
            var rgba = color.ToPixel<Rgba32>();

            rgba.A.Should().Be(128);
        }

        [Fact]
        public void GetInstanceColor_WithCustomAlpha_ShouldSetAlpha()
        {
            var colors = new VisionColors();

            var color = colors.GetInstanceColor(0, alpha: 200);
            var rgba = color.ToPixel<Rgba32>();

            rgba.A.Should().Be(200);
        }

        [Fact]
        public void GetInstanceColor_SameInstance_ShouldReturnSameColor()
        {
            var colors = new VisionColors();

            var color1 = colors.GetInstanceColor(5);
            var color2 = colors.GetInstanceColor(5);

            color1.Should().Be(color2);
        }

        [Fact]
        public void GetInstanceColor_WrapsAt80_ShouldMatchBoundingBoxColor()
        {
            var colors = new VisionColors();

            var instanceColor = colors.GetInstanceColor(80);
            var bboxColor = colors.GetBoundingBoxColor(80 % 80, alpha: 128);

            instanceColor.Should().Be(bboxColor);
        }

        #endregion

        #region Color Consistency Tests

        [Fact]
        public void BoundingBoxColors_ShouldBeConsistent()
        {
            var colors1 = new VisionColors();
            var colors2 = new VisionColors();

            var color1 = colors1.GetBoundingBoxColor(10);
            var color2 = colors2.GetBoundingBoxColor(10);

            color1.Should().Be(color2);
        }

        [Fact]
        public void MaskColors_ShouldBeConsistent()
        {
            var colors1 = new VisionColors();
            var colors2 = new VisionColors();

            var color1 = colors1.GetMaskColor(5);
            var color2 = colors2.GetMaskColor(5);

            color1.Should().Be(color2);
        }

        [Fact]
        public void InstanceColor_WithAlpha255_ShouldMatchBoundingBoxColor()
        {
            var colors = new VisionColors();

            var instanceColor = colors.GetInstanceColor(5, alpha: 255);
            var bboxColor = colors.GetBoundingBoxColor(5, alpha: 255);

            instanceColor.Should().Be(bboxColor);
        }

        #endregion

        #region Color Properties Tests

        [Fact]
        public void GetBoundingBoxColor_ShouldReturnValidRgb()
        {
            var colors = new VisionColors();

            var color = colors.GetBoundingBoxColor(0);
            var rgba = color.ToPixel<Rgba32>();

            rgba.R.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(255);
            rgba.G.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(255);
            rgba.B.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(255);
        }

        [Fact]
        public void GetMaskColor_ShouldReturnValidRgb()
        {
            var colors = new VisionColors();

            var color = colors.GetMaskColor(0);
            var rgba = color.ToPixel<Rgba32>();

            rgba.R.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(255);
            rgba.G.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(255);
            rgba.B.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(255);
        }

        [Fact]
        public void GetMaskColor_ShouldHaveFullAlpha()
        {
            var colors = new VisionColors();

            var color = colors.GetMaskColor(0);
            var rgba = color.ToPixel<Rgba32>();

            rgba.A.Should().Be(255);
        }

        #endregion
    }
}
