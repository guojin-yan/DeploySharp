using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class ImageDataBTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithDimensions_ShouldCreateInstance()
        {
            var image = new ImageDataB(100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        [Fact]
        public void Constructor_WithData_ShouldCreateInstance()
        {
            var data = new byte[100 * 200 * 3];
            var image = new ImageDataB(data, 100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        #endregion

        #region GetRawByteData Tests

        [Fact]
        public void GetRawByteData_ShouldReturnData()
        {
            var data = new byte[100 * 200 * 3];
            data[0] = 255;
            var image = new ImageDataB(data, 100, 200, 3);

            var rawData = image.GetRawByteData();

            rawData[0].Should().Be(255);
        }

        [Fact]
        public void GetRawByteData_ShouldReturnEquivalentData()
        {
            var data = new byte[100 * 200 * 3];
            data[0] = 255;
            var image = new ImageDataB(data, 100, 200, 3);

            var rawData = image.GetRawByteData();

            rawData[0].Should().Be(255);
            rawData.Should().Equal(data); // Returns copy, not same reference
        }

        #endregion

        #region Properties Tests

        [Fact]
        public void Width_ShouldReturnCorrectValue()
        {
            var image = new ImageDataB(640, 480, 3);

            image.Width.Should().Be(640);
        }

        [Fact]
        public void Height_ShouldReturnCorrectValue()
        {
            var image = new ImageDataB(640, 480, 3);

            image.Height.Should().Be(480);
        }

        [Fact]
        public void Channels_ShouldReturnCorrectValue()
        {
            var image = new ImageDataB(640, 480, 4);

            image.Channels.Should().Be(4);
        }

        #endregion

        #region Buffer Size Tests

        [Fact]
        public void Constructor_WithSmallImage_ShouldCreateCorrectBuffer()
        {
            var image = new ImageDataB(10, 10, 1);
            var data = image.GetRawByteData();

            data.Length.Should().Be(100); // 10*10*1
        }

        [Fact]
        public void Constructor_WithRGBImage_ShouldCreateCorrectBuffer()
        {
            var image = new ImageDataB(224, 224, 3);
            var data = image.GetRawByteData();

            data.Length.Should().Be(224 * 224 * 3);
        }

        [Fact]
        public void Constructor_WithRGBAImage_ShouldCreateCorrectBuffer()
        {
            var image = new ImageDataB(1920, 1080, 4);
            var data = image.GetRawByteData();

            data.Length.Should().Be(1920 * 1080 * 4);
        }

        #endregion
    }

    public class ImageDataFTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithDimensions_ShouldCreateInstance()
        {
            var image = new ImageDataF(100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        [Fact]
        public void Constructor_WithData_ShouldCreateInstance()
        {
            var data = new float[100 * 200 * 3];
            var image = new ImageDataF(data, 100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        #endregion

        #region GetRawFloatData Tests

        [Fact]
        public void GetRawFloatData_ShouldReturnData()
        {
            var data = new float[100 * 200 * 3];
            data[0] = 1.5f;
            var image = new ImageDataF(data, 100, 200, 3);

            var rawData = image.GetRawFloatData();

            rawData[0].Should().Be(1.5f);
        }

        [Fact]
        public void GetRawFloatData_ShouldReturnEquivalentData()
        {
            var data = new float[100 * 200 * 3];
            data[0] = 3.14f;
            var image = new ImageDataF(data, 100, 200, 3);

            var rawData = image.GetRawFloatData();

            rawData[0].Should().Be(3.14f);
            rawData.Should().Equal(data); // Returns copy, not same reference
        }

        #endregion

        #region Properties Tests

        [Fact]
        public void Width_ShouldReturnCorrectValue()
        {
            var image = new ImageDataF(640, 480, 3);

            image.Width.Should().Be(640);
        }

        [Fact]
        public void Height_ShouldReturnCorrectValue()
        {
            var image = new ImageDataF(640, 480, 3);

            image.Height.Should().Be(480);
        }

        [Fact]
        public void Channels_ShouldReturnCorrectValue()
        {
            var image = new ImageDataF(640, 480, 4);

            image.Channels.Should().Be(4);
        }

        #endregion

        #region Buffer Size Tests

        [Fact]
        public void Constructor_WithSmallImage_ShouldCreateCorrectBuffer()
        {
            var image = new ImageDataF(10, 10, 1);
            var data = image.GetRawFloatData();

            data.Length.Should().Be(100); // 10*10*1
        }

        [Fact]
        public void Constructor_WithRGBImage_ShouldCreateCorrectBuffer()
        {
            var image = new ImageDataF(224, 224, 3);
            var data = image.GetRawFloatData();

            data.Length.Should().Be(224 * 224 * 3);
        }

        [Fact]
        public void Constructor_WithRGBAImage_ShouldCreateCorrectBuffer()
        {
            var image = new ImageDataF(1920, 1080, 4);
            var data = image.GetRawFloatData();

            data.Length.Should().Be(1920 * 1080 * 4);
        }

        #endregion
    }

    public class ImageDataGenericTests
    {
        #region Constructor Tests - Byte

        [Fact]
        public void Constructor_Byte_WithDimensions_ShouldCreateInstance()
        {
            var image = new ImageData<byte>(100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        [Fact]
        public void Constructor_Byte_WithData_ShouldCreateInstance()
        {
            var data = new byte[100 * 200 * 3];
            var image = new ImageData<byte>(data, 100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        [Fact]
        public void Constructor_Byte_GetRawData_ShouldReturnData()
        {
            var data = new byte[100 * 200 * 3];
            data[0] = 255;
            var image = new ImageData<byte>(data, 100, 200, 3);

            image.GetRawData()[0].Should().Be(255);
        }

        #endregion

        #region Constructor Tests - Float

        [Fact]
        public void Constructor_Float_WithDimensions_ShouldCreateInstance()
        {
            var image = new ImageData<float>(100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        [Fact]
        public void Constructor_Float_WithData_ShouldCreateInstance()
        {
            var data = new float[100 * 200 * 3];
            var image = new ImageData<float>(data, 100, 200, 3);

            image.Width.Should().Be(100);
            image.Height.Should().Be(200);
            image.Channels.Should().Be(3);
        }

        [Fact]
        public void Constructor_Float_GetRawData_ShouldReturnData()
        {
            var data = new float[100 * 200 * 3];
            data[0] = 3.14f;
            var image = new ImageData<float>(data, 100, 200, 3);

            image.GetRawData()[0].Should().Be(3.14f);
        }

        #endregion

        #region Constructor Tests - Double

        [Fact]
        public void Constructor_Double_WithDimensions_ShouldCreateInstance()
        {
            var image = new ImageData<double>(64, 64, 1);

            image.Width.Should().Be(64);
            image.Height.Should().Be(64);
            image.Channels.Should().Be(1);
        }

        [Fact]
        public void Constructor_Double_WithData_ShouldCreateInstance()
        {
            var data = new double[64 * 64];
            var image = new ImageData<double>(data, 64, 64, 1);

            image.Width.Should().Be(64);
            image.Height.Should().Be(64);
            image.Channels.Should().Be(1);
        }

        #endregion

        #region Properties Tests

        [Fact]
        public void Width_ShouldReturnCorrectValue()
        {
            var image = new ImageData<byte>(640, 480, 3);

            image.Width.Should().Be(640);
        }

        [Fact]
        public void Height_ShouldReturnCorrectValue()
        {
            var image = new ImageData<byte>(640, 480, 3);

            image.Height.Should().Be(480);
        }

        [Fact]
        public void Channels_ShouldReturnCorrectValue()
        {
            var image = new ImageData<byte>(640, 480, 4);

            image.Channels.Should().Be(4);
        }

        #endregion

        #region Buffer Tests

        [Fact]
        public void Constructor_Byte_ShouldCreateCorrectBuffer()
        {
            var image = new ImageData<byte>(224, 224, 3);
            var data = image.GetRawData();

            data.Length.Should().Be(224 * 224 * 3);
        }

        [Fact]
        public void Constructor_Float_ShouldCreateCorrectBuffer()
        {
            var image = new ImageData<float>(224, 224, 3);
            var data = image.GetRawData();

            data.Length.Should().Be(224 * 224 * 3);
        }

        [Fact]
        public void GetRawData_Byte_ShouldReturnEquivalentData()
        {
            var data = new byte[100 * 200 * 3];
            data[0] = 255;
            var image = new ImageData<byte>(data, 100, 200, 3);

            var rawData = image.GetRawData();
            rawData[0].Should().Be(255);
            rawData.Should().Equal(data); // Returns copy, not same reference
        }

        [Fact]
        public void GetRawData_Float_ShouldReturnEquivalentData()
        {
            var data = new float[100 * 200 * 3];
            data[0] = 3.14f;
            var image = new ImageData<float>(data, 100, 200, 3);

            var rawData = image.GetRawData();
            rawData[0].Should().Be(3.14f);
            rawData.Should().Equal(data); // Returns copy, not same reference
        }

        #endregion
    }
}
