using DeploySharp.Data;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DeploySharp.ImageSharp.Tests.Data
{
    public class CvDataProcessorTests
    {
        [Fact]
        public void ProcessToFloat_WithDefaultConfig_ShouldUseRgbSourceAndRgbModel()
        {
            using var image = new Image<Rgb24>(1, 1, new Rgb24(10, 20, 30));

            var result = CvDataProcessor.ProcessToFloat(
                image,
                new DeploySharp.Data.Size(1, 1),
                new DataProcessorConfig());

            result.Should().Equal(10f, 20f, 30f);
        }

        [Fact]
        public void Normalize_WithRgbSourceAndBgrModel_ShouldSwapRedAndBlueChannels()
        {
            using var image = new Image<Rgb24>(1, 1, new Rgb24(10, 20, 30));

            var result = CvDataProcessor.Normalize(
                image,
                ImageNormalizationType.None,
                null,
                ImageColorOrder.Rgb,
                ImageColorOrder.Bgr);

            result.Should().Equal(30f, 20f, 10f);
        }

        [Theory]
        [InlineData(ImageColorOrder.Bgr)]
        [InlineData(ImageColorOrder.Rgb)]
        public void Normalize_WithMatchingColorOrders_ShouldPreserveChannelOrder(
            ImageColorOrder colorOrder)
        {
            using var image = new Image<Rgb24>(1, 1, new Rgb24(10, 20, 30));

            var result = CvDataProcessor.Normalize(
                image,
                ImageNormalizationType.None,
                null,
                colorOrder,
                colorOrder);

            result.Should().Equal(10f, 20f, 30f);
        }
    }
}
