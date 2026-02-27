using DeploySharp.Data;
using FluentAssertions;
using OpenCvSharp;
using Xunit;

namespace DeploySharp.OpenCvSharp.Tests.Data
{
    public class VisualizeOptionsTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithRatio1_ShouldUseDefaultValues()
        {
            var options = new VisualizeOptions(1.0f);

            options.FontSize.Should().BeApproximately(0.5f, 0.1f);
            options.BorderThickness.Should().Be(2);
        }

        [Fact]
        public void Constructor_WithRatio0_5_ShouldScaleDown()
        {
            var options = new VisualizeOptions(0.5f);

            options.FontSize.Should().BeApproximately(0.25f, 0.05f);
            options.BorderThickness.Should().Be(1);
        }

        [Fact]
        public void Constructor_WithRatio2_ShouldScaleUp()
        {
            var options = new VisualizeOptions(2.0f);

            options.FontSize.Should().BeApproximately(1.0f, 0.1f);
            options.BorderThickness.Should().Be(4);
        }

        [Fact]
        public void Constructor_WithRatio0_ShouldZeroOutFontSize()
        {
            var options = new VisualizeOptions(0.0f);

            options.FontSize.Should().Be(0f);
            options.BorderThickness.Should().Be(0);
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
        public void DefaultMaskMinConfidence_ShouldBe0_5()
        {
            var options = new VisualizeOptions(1.0f);

            options.MaskMinConfidence.Should().Be(0.5f);
        }

        [Fact]
        public void DefaultKeyPointMinConfidence_ShouldBe0_5()
        {
            var options = new VisualizeOptions(1.0f);

            options.KeyPointMinConfidence.Should().Be(0.5f);
        }

        [Fact]
        public void DefaultFontType_ShouldBeHersheySimplex()
        {
            var options = new VisualizeOptions(1.0f);

            options.FontType.Should().Be(HersheyFonts.HersheySimplex);
        }

        [Fact]
        public void DefaultColors_ShouldNotBeNull()
        {
            var options = new VisualizeOptions(1.0f);

            options.Colors.Should().NotBeNull();
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
        public void MaskMinConfidence_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.MaskMinConfidence = 0.7f;

            options.MaskMinConfidence.Should().Be(0.7f);
        }

        [Fact]
        public void KeyPointMinConfidence_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.KeyPointMinConfidence = 0.3f;

            options.KeyPointMinConfidence.Should().Be(0.3f);
        }

        [Fact]
        public void BorderThickness_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.BorderThickness = 5;

            options.BorderThickness.Should().Be(5);
        }

        [Fact]
        public void FontSize_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.FontSize = 1.0f;

            options.FontSize.Should().Be(1.0f);
        }

        [Fact]
        public void FontType_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            options.FontType = HersheyFonts.HersheyTriplex;

            options.FontType.Should().Be(HersheyFonts.HersheyTriplex);
        }

        [Fact]
        public void Colors_SetValue_ShouldUpdate()
        {
            var options = new VisualizeOptions(1.0f);
            var newColors = new VisionColors();
            options.Colors = newColors;

            options.Colors.Should().Be(newColors);
        }

        #endregion

        #region FontHeight Tests

        [Fact]
        public void FontHeight_ShouldCalculateCorrectly()
        {
            var options = new VisualizeOptions(1.0f);
            options.FontSize = 1.0f;

            options.FontHeight.Should().Be(40);
        }

        [Fact]
        public void FontHeight_WithDefaultFontSize_ShouldBe20()
        {
            var options = new VisualizeOptions(1.0f);

            options.FontHeight.Should().Be(20);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Options_WithCustomValues_ShouldWork()
        {
            var options = new VisualizeOptions(1.5f)
            {
                MaskAlpha = 0.3f,
                MaskMinConfidence = 0.6f,
                KeyPointMinConfidence = 0.4f,
                BorderThickness = 3,
                FontSize = 0.8f,
                FontType = HersheyFonts.HersheyScriptSimplex
            };

            options.MaskAlpha.Should().Be(0.3f);
            options.MaskMinConfidence.Should().Be(0.6f);
            options.KeyPointMinConfidence.Should().Be(0.4f);
            options.BorderThickness.Should().Be(3);
            options.FontSize.Should().Be(0.8f);
            options.FontType.Should().Be(HersheyFonts.HersheyScriptSimplex);
        }

        #endregion
    }
}
