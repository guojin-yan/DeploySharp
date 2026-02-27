using DeploySharp.Data;
using FluentAssertions;
using System;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class ImageAdjustmentParamTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParams_ShouldSetProperties()
        {
            var padding = new Pair<int, int>(10, 20);
            var ratio = new Pair<float, float>(0.5f, 0.6f);
            var rowSize = new Size(400, 300);
            var targetSize = new Size(800, 600);

            var param = new ImageAdjustmentParam(padding, ratio, rowSize, targetSize);

            param.Padding.Equals(padding).Should().BeTrue();
            param.Ratio.Equals(ratio).Should().BeTrue();
            param.RowImgSize.Should().Be(rowSize);
            param.TargetImgSize.Should().Be(targetSize);
        }

        [Fact]
        public void Constructor_WithDefaultValues_ShouldWork()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(0, 0),
                new Pair<float, float>(1f, 1f),
                new Size(100, 100),
                new Size(100, 100));

            param.Padding.First.Should().Be(0);
            param.Ratio.First.Should().Be(1f);
        }

        #endregion

        #region Deconstruct Tests

        [Fact]
        public void Deconstruct_ShouldReturnAllValues()
        {
            var padding = new Pair<int, int>(5, 10);
            var ratio = new Pair<float, float>(0.8f, 0.9f);
            var rowSize = new Size(640, 480);
            var targetSize = new Size(320, 240);
            var param = new ImageAdjustmentParam(padding, ratio, rowSize, targetSize);

            var (outPadding, outRatio, outRow, outTarget) = param;

            outPadding.Equals(padding).Should().BeTrue();
            outRatio.Equals(ratio).Should().BeTrue();
            outRow.Should().Be(rowSize);
            outTarget.Should().Be(targetSize);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ShouldContainAllProperties()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(0.5f, 0.6f),
                new Size(400, 300),
                new Size(800, 600));

            var str = param.ToString();

            str.Should().Contain("Padding");
            str.Should().Contain("Ratio");
            str.Should().Contain("RowImgSize");
            str.Should().Contain("TargetImgSize");
            str.Should().Contain("10");
            str.Should().Contain("0.5");
        }

        #endregion

        #region AdjustRect Tests

        [Fact]
        public void AdjustRect_WithPaddingAndRatio_ShouldAdjust()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(2f, 2f),
                new Size(100, 100),
                new Size(200, 200));
            var rect = new RectF(50, 60, 40, 30);

            var adjusted = param.AdjustRect(rect);

            adjusted.X.Should().Be((50 - 10) / 2);  // = 20
            adjusted.Y.Should().Be((60 - 20) / 2);  // = 20
            adjusted.Width.Should().Be(40 / 2);     // = 20
            adjusted.Height.Should().Be(30 / 2);    // = 15
        }

        [Fact]
        public void AdjustRect_WithoutPadding_ShouldOnlyApplyRatio()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(0, 0),
                new Pair<float, float>(0.5f, 0.5f),
                new Size(100, 100),
                new Size(200, 200));
            var rect = new RectF(100, 100, 50, 40);

            var adjusted = param.AdjustRect(rect);

            adjusted.X.Should().Be(200);
            adjusted.Y.Should().Be(200);
            adjusted.Width.Should().Be(100);
            adjusted.Height.Should().Be(80);
        }

        #endregion

        #region AdjustRectF Tests

        [Fact]
        public void AdjustRectF_WithPaddingAndRatio_ShouldAdjust()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(2f, 2f),
                new Size(100, 100),
                new Size(200, 200));
            var rect = new RectF(50, 60, 40, 30);

            var adjusted = param.AdjustRectF(rect);

            adjusted.X.Should().Be(20);
            adjusted.Y.Should().Be(20);
            adjusted.Width.Should().Be(20);
            adjusted.Height.Should().Be(15);
        }

        [Fact]
        public void AdjustRectF_ShouldReturnFloatPrecision()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(0, 0),
                new Pair<float, float>(0.3f, 0.7f),
                new Size(100, 100),
                new Size(300, 700));
            var rect = new RectF(10.5f, 20.7f, 30.3f, 40.9f);

            var adjusted = param.AdjustRectF(rect);

            adjusted.X.Should().BeApproximately(35f, 0.1f);
            adjusted.Y.Should().BeApproximately(29.57f, 0.1f);
        }

        #endregion

        #region AdjustPoint Tests

        [Fact]
        public void AdjustPoint_WithPaddingAndRatio_ShouldAdjust()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(2f, 2f),
                new Size(100, 100),
                new Size(200, 200));
            var point = new Point(50, 60);

            var adjusted = param.AdjustPoint(point);

            adjusted.X.Should().Be(20);
            adjusted.Y.Should().Be(20);
        }

        [Fact]
        public void AdjustPoint_WithoutPadding_ShouldOnlyApplyRatio()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(0, 0),
                new Pair<float, float>(0.5f, 0.5f),
                new Size(100, 100),
                new Size(200, 200));
            var point = new Point(100, 100);

            var adjusted = param.AdjustPoint(point);

            adjusted.X.Should().Be(200);
            adjusted.Y.Should().Be(200);
        }

        #endregion

        #region AdjustRotatedRect Tests

        [Fact]
        public void AdjustRotatedRect_WithNoRotation_ShouldAdjust()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(0, 0),
                new Pair<float, float>(0.5f, 0.5f),
                new Size(100, 100),
                new Size(200, 200));
            var rect = new RotatedRect(new PointF(100, 100), new SizeF(50, 30), 0);

            var adjusted = param.AdjustRotatedRect(rect);

            adjusted.Center.X.Should().BeApproximately(200, 1);
            adjusted.Center.Y.Should().BeApproximately(200, 1);
            adjusted.Size.Width.Should().BeApproximately(100, 1);
            adjusted.Size.Height.Should().BeApproximately(60, 1);
        }

        [Fact]
        public void AdjustRotatedRect_WithPadding_ShouldAdjust()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(2f, 2f),
                new Size(100, 100),
                new Size(200, 200));
            var rect = new RotatedRect(new PointF(50, 60), new SizeF(40, 30), 0);

            var adjusted = param.AdjustRotatedRect(rect);

            // Point adjusted: (50-10)/2=20, (60-20)/2=20
            // Size adjusted: 40/2=20, 30/2=15
            adjusted.Center.X.Should().BeApproximately(20, 1);
            adjusted.Center.Y.Should().BeApproximately(20, 1);
        }

        #endregion

        #region CreateFromImageInfo Tests

        [Theory]
        [InlineData(0, 100)]
        [InlineData(100, 0)]
        [InlineData(-1, 100)]
        [InlineData(100, -1)]
        public void CreateFromImageInfo_WithInvalidDimensions_ShouldThrow(int inputWidth, int inputHeight)
        {
            Action act = () => ImageAdjustmentParam.CreateFromImageInfo(
                new Size(inputWidth, inputHeight),
                new Size(100, 100),
                ImageResizeMode.Stretch);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateFromImageInfo_WithStretchMode_ShouldCalculateIndependentRatios()
        {
            var param = ImageAdjustmentParam.CreateFromImageInfo(
                new Size(800, 600),
                new Size(400, 300),
                ImageResizeMode.Stretch);

            param.Padding.First.Should().Be(0);
            param.Padding.Second.Should().Be(0);
            param.Ratio.First.Should().Be(2f);  // 800/400
            param.Ratio.Second.Should().Be(2f); // 600/300
        }

        [Fact]
        public void CreateFromImageInfo_WithPadMode_ShouldCalculateUniformRatioAndPadding()
        {
            var param = ImageAdjustmentParam.CreateFromImageInfo(
                new Size(640, 640),
                new Size(800, 600),
                ImageResizeMode.Pad);

            // Scale = min(640/800, 640/600) = 0.8
            // Final size = 800*0.8=640, 600*0.8=480
            // Pad = (640-640)/2=0, (640-480)/2=80
            param.Ratio.First.Should().Be(0.8f);
            param.Ratio.Second.Should().Be(0.8f);
            param.Padding.First.Should().Be(0);
            param.Padding.Second.Should().Be(80);
        }

        [Fact]
        public void CreateFromImageInfo_WithMaxMode_ShouldCalculateUniformRatio()
        {
            var param = ImageAdjustmentParam.CreateFromImageInfo(
                new Size(640, 640),
                new Size(800, 600),
                ImageResizeMode.Max);

            // Same calculation as Pad, but no padding
            param.Ratio.First.Should().Be(0.8f);
            param.Ratio.Second.Should().Be(0.8f);
            param.Padding.First.Should().Be(0);
            param.Padding.Second.Should().Be(0);
        }

        [Fact]
        public void CreateFromImageInfo_WithCropMode_ShouldCalculateCropRatio()
        {
            var param = ImageAdjustmentParam.CreateFromImageInfo(
                new Size(640, 640),
                new Size(800, 600),
                ImageResizeMode.Crop);

            // Scale = max(640/800, 640/600) = 1.066...
            param.Ratio.First.Should().BeApproximately(640f / 600f, 0.001f);
            param.Ratio.Second.Should().BeApproximately(640f / 600f, 0.001f);
            param.Padding.First.Should().Be(0);
            param.Padding.Second.Should().Be(0);
        }

        [Fact]
        public void CreateFromImageInfo_WithCrnnPadMode_ShouldCalculateIndependentRatios()
        {
            var param = ImageAdjustmentParam.CreateFromImageInfo(
                new Size(800, 32),
                new Size(100, 16),
                ImageResizeMode.CrnnPad);

            // CrnnPad is like Stretch - independent ratios
            param.Padding.First.Should().Be(0);
            param.Padding.Second.Should().Be(0);
            param.Ratio.First.Should().Be(8f);  // 800/100
            param.Ratio.Second.Should().Be(2f); // 32/16
        }

        [Fact]
        public void CreateFromImageInfo_WithInvalidResizeMode_ShouldThrow()
        {
            Action act = () => ImageAdjustmentParam.CreateFromImageInfo(
                new Size(100, 100),
                new Size(100, 100),
                (ImageResizeMode)999);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void CreateFromImageInfo_ShouldStoreSizes()
        {
            var imgSize = new Size(400, 300);
            var targetSize = new Size(800, 600);

            var param = ImageAdjustmentParam.CreateFromImageInfo(
                targetSize, imgSize, ImageResizeMode.Stretch);

            param.RowImgSize.Should().Be(imgSize);
            param.TargetImgSize.Should().Be(targetSize);
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_WithSameValues_ShouldReturnTrue()
        {
            var p1 = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(0.5f, 0.6f),
                new Size(400, 300),
                new Size(800, 600));
            var p2 = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(0.5f, 0.6f),
                new Size(400, 300),
                new Size(800, 600));

            p1.Equals(p2).Should().BeTrue();
            (p1 == p2).Should().BeTrue();
            (p1 != p2).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentValues_ShouldReturnFalse()
        {
            var p1 = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(0.5f, 0.6f),
                new Size(400, 300),
                new Size(800, 600));
            var p2 = new ImageAdjustmentParam(
                new Pair<int, int>(5, 10),
                new Pair<float, float>(0.5f, 0.6f),
                new Size(400, 300),
                new Size(800, 600));

            p1.Equals(p2).Should().BeFalse();
            (p1 == p2).Should().BeFalse();
            (p1 != p2).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithObject_WhenImageAdjustmentParam_ShouldWork()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(0.5f, 0.6f),
                new Size(400, 300),
                new Size(800, 600));
            object obj = param;

            param.Equals(obj).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithObject_WhenNotImageAdjustmentParam_ShouldReturnFalse()
        {
            var param = new ImageAdjustmentParam(
                new Pair<int, int>(10, 20),
                new Pair<float, float>(0.5f, 0.6f),
                new Size(400, 300),
                new Size(800, 600));

            param.Equals("not a param").Should().BeFalse();
        }

        #endregion
    }
}
