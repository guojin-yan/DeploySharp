using DeploySharp.Data;
using FluentAssertions;
using OpenCvSharp;
using Xunit;

namespace DeploySharp.OpenCvSharp.Tests.Data
{
    public class CvDataProcessorTests
    {
        [Fact]
        public void ProcessToFloat_WithDefaultConfig_ShouldUseBgrSourceAndRgbModel()
        {
            using var image = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));

            var result = CvDataProcessor.ProcessToFloat(
                image,
                new DeploySharp.Data.Size(1, 1),
                new DataProcessorConfig());

            result.Should().Equal(30f, 20f, 10f);
        }

        [Fact]
        public void Normalize_WithBgrSourceAndRgbModel_ShouldSwapRedAndBlueChannels()
        {
            using var image = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));

            var result = CvDataProcessor.Normalize(
                image,
                ImageNormalizationType.None,
                null,
                ImageColorOrder.Bgr,
                ImageColorOrder.Rgb);

            result.Should().Equal(30f, 20f, 10f);
        }

        [Theory]
        [InlineData(ImageColorOrder.Bgr)]
        [InlineData(ImageColorOrder.Rgb)]
        public void Normalize_WithMatchingColorOrders_ShouldPreserveChannelOrder(
            ImageColorOrder colorOrder)
        {
            using var image = new Mat(1, 1, MatType.CV_8UC3, new Scalar(10, 20, 30));

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
