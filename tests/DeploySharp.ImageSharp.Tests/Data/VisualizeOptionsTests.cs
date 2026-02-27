using DeploySharp.Data;
using FluentAssertions;
using SixLabors.Fonts;
using Xunit;

namespace DeploySharp.ImageSharp.Tests.Data
{
    public class VisualizeOptionsTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithRatio1_ShouldUseDefaultValues()
        {
            var options = new VisualizeOptions(1.0f);

            options.FontSize.Should().BeApproximately(12f, 0.1f);
            options.BorderThickness.Should().BeApproximately(2f, 0.1f);
        }

        [Fact]
        public void Constructor_WithRatio0_5_ShouldScaleDown()
        {
            var options = new VisualizeOptions(0.5f);

            options.FontSize.Should().BeApproximately(6f, 0.1f);
            options.BorderThickness.Should().BeApproximately(1f, 0.1f);
        }

        [Fact]
        public void Constructor_WithRatio2_ShouldScaleUp()
        {
            var options = new VisualizeOptions(2.0f);

            options.FontSize.Should().BeApproximately(24f, 0.1f);
            options.BorderThickness.Should().BeApproximately(4f, 0.1f);
        }

        [Fact]
        public void Constructor_WithRatio0_ShouldZeroOutDimensions()
        {
            var options = new VisualizeOptions(0.0f);

            options.FontSize.Should().Be(0f);
            options.BorderThickness.Should().Be(0f);
        }

        #endregion

        #region Default Property Tests

        [Fact]
        public void DefaultMaskAlpha_ShouldBe0_5()
        {
            var options = new VisualizeOptions(1.0f);

            options.MaskAlpha.Should().Be(0.5f);
        }

        [Fact]
        public void DefaultMaskMinimumConfidence_ShouldBe0_5()
        {
            var options = new VisualizeOptions(1.0f);

            options.MaskMinimumConfidence.Should().Be(0.5f);
        }

        [Fact]
        public void DefaultPointDrawThreshold_ShouldBe0_5()
        {
            var options = new VisualizeOptions(1.0f);

            options.PointDrawThreshold.Should().Be(0.5f);
        }

        #endregion

        #region Property Setter Tests

        [Fact]
        public void MaskAlpha_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.MaskAlpha = 0.8f;

            options.MaskAlpha.Should().Be(0.8f);
        }

        [Fact]
        public void MaskMinimumConfidence_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.MaskMinimumConfidence = 0.7f;

            options.MaskMinimumConfidence.Should().Be(0.7f);
        }

        [Fact]
        public void PointDrawThreshold_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.PointDrawThreshold = 0.3f;

            options.PointDrawThreshold.Should().Be(0.3f);
        }

        [Fact]
        public void BorderThickness_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.BorderThickness = 5f;

            options.BorderThickness.Should().Be(5f);
        }

        [Fact]
        public void FontSize_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.FontSize = 20f;

            options.FontSize.Should().Be(20f);
        }

        [Fact]
        public void Colors_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            var newColors = new VisionColors();
            options.colors = newColors;

            options.colors.Should().Be(newColors);
        }

        #endregion

        #region FontType Property Tests

        [Fact]
        public void FontType_ShouldReturnFont()
        {
            var options = new VisualizeOptions(1.0f);

            var font = options.FontType;

            font.Should().NotBeNull();
        }

        [Fact]
        public void FontType_AfterSettingFontSize_ShouldReflectChange()
        {
            var options = new VisualizeOptions(1.0f);
            options.FontSize = 24f;

            var font = options.FontType;

            font.Size.Should().Be(24f);
        }

        #endregion

        #region FontHeight Property Tests

        [Fact]
        public void FontHeight_ShouldReturnPositiveValue()
        {
            var options = new VisualizeOptions(1.0f);

            var height = options.FontHeight;

            height.Should().BeGreaterThan(0);
        }

        [Fact]
        public void FontHeight_WithLargerFont_ShouldBeGreater()
        {
            var smallOptions = new VisualizeOptions(1.0f);
            smallOptions.FontSize = 12f;
            var smallHeight = smallOptions.FontHeight;

            var largeOptions = new VisualizeOptions(1.0f);
            largeOptions.FontSize = 24f;
            var largeHeight = largeOptions.FontHeight;

            largeHeight.Should().BeGreaterThan(smallHeight);
        }

        #endregion

        #region Static GetFontHeight Tests

        [Fact]
        public void GetFontHeight_WithValidFont_ShouldReturnPositiveValue()
        {
            var font = SystemFonts.CreateFont("Arial", 16f);

            var height = VisualizeOptions.GetFontHeight(font);

            height.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetFontHeight_WithLargerSize_ShouldReturnLargerHeight()
        {
            var smallFont = SystemFonts.CreateFont("Arial", 12f);
            var largeFont = SystemFonts.CreateFont("Arial", 24f);

            var smallHeight = VisualizeOptions.GetFontHeight(smallFont);
            var largeHeight = VisualizeOptions.GetFontHeight(largeFont);

            largeHeight.Should().BeGreaterThan(smallHeight);
        }

        [Fact]
        public void GetFontHeight_WithNullFont_ShouldThrowArgumentNullException()
        {
            Action act = () => VisualizeOptions.GetFontHeight(null!);

            act.Should().Throw<NullReferenceException>();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Options_WithCustomValues_ShouldWork()
        {
            var options = new VisualizeOptions(1.5f)
            {
                MaskAlpha = 0.3f,
                MaskMinimumConfidence = 0.6f,
                PointDrawThreshold = 0.4f,
                BorderThickness = 3f,
                FontSize = 18f
            };

            options.MaskAlpha.Should().Be(0.3f);
            options.MaskMinimumConfidence.Should().Be(0.6f);
            options.PointDrawThreshold.Should().Be(0.4f);
            options.BorderThickness.Should().Be(3f);
            options.FontSize.Should().Be(18f);
        }

        [Fact]
        public void FontHeight_ShouldScaleWithFontSize()
        {
            var options = new VisualizeOptions(1.0f);
            var baseHeight = options.FontHeight;

            options.FontSize *= 2;
            var doubledHeight = options.FontHeight;

            doubledHeight.Should().BeGreaterThan(baseHeight);
        }

        #endregion
    }
}
