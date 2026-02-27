using DeploySharp.Data;
using FluentAssertions;
using Xunit;

namespace DeploySharp.Tests.Data
{
    public class DataProcessorConfigTests
    {
        #region Constructor Tests

        [Fact]
        public void DefaultConstructor_ShouldSetDefaultValues()
        {
            var config = new DataProcessorConfig();

            config.ResizeMode.Should().Be(ImageResizeMode.Stretch);
            config.NormalizationType.Should().Be(ImageNormalizationType.None);
            config.CustomNormalizationParams.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithParameters_ShouldSetValues()
        {
            var config = new DataProcessorConfig(
                ImageResizeMode.Pad,
                ImageNormalizationType.ImageNetStandard
            );

            config.ResizeMode.Should().Be(ImageResizeMode.Pad);
            config.NormalizationType.Should().Be(ImageNormalizationType.ImageNetStandard);
        }

        [Fact]
        public void Constructor_WithCustomParams_ShouldSetValues()
        {
            var customParams = new NormalizationParams
            {
                Mean = new[] { 0.5f, 0.5f, 0.5f },
                Std = new[] { 0.5f, 0.5f, 0.5f }
            };
            var config = new DataProcessorConfig(
                ImageResizeMode.Crop,
                ImageNormalizationType.CustomStandard,
                customParams
            );

            config.ResizeMode.Should().Be(ImageResizeMode.Crop);
            config.NormalizationType.Should().Be(ImageNormalizationType.CustomStandard);
            config.CustomNormalizationParams.Should().Be(customParams);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void NormalizationType_SetValue_ShouldUpdate()
        {
            var config = new DataProcessorConfig();

            config.NormalizationType = ImageNormalizationType.ImageNetStandard;

            config.NormalizationType.Should().Be(ImageNormalizationType.ImageNetStandard);
        }

        [Fact]
        public void ResizeMode_SetValue_ShouldUpdate()
        {
            var config = new DataProcessorConfig();

            config.ResizeMode = ImageResizeMode.CrnnPad;

            config.ResizeMode.Should().Be(ImageResizeMode.CrnnPad);
        }

        [Fact]
        public void CustomNormalizationParams_SetValue_ShouldUpdate()
        {
            var config = new DataProcessorConfig();
            var customParams = new NormalizationParams
            {
                Mean = new[] { 0.485f, 0.456f, 0.406f },
                Std = new[] { 0.229f, 0.224f, 0.225f }
            };

            config.CustomNormalizationParams = customParams;

            config.CustomNormalizationParams.Should().Be(customParams);
        }

        #endregion
    }

    public class NormalizationParamsTests
    {
        #region Property Tests

        [Fact]
        public void Mean_SetValue_ShouldUpdate()
        {
            var mean = new[] { 0.485f, 0.456f, 0.406f };
            var params_ = new NormalizationParams { Mean = mean };

            params_.Mean.Should().BeEquivalentTo(mean);
        }

        [Fact]
        public void Std_SetValue_ShouldUpdate()
        {
            var std = new[] { 0.229f, 0.224f, 0.225f };
            var params_ = new NormalizationParams { Std = std };

            params_.Std.Should().BeEquivalentTo(std);
        }

        [Fact]
        public void MaxPixelValue_DefaultValue_ShouldBe255()
        {
            var params_ = new NormalizationParams();

            params_.MaxPixelValue.Should().Be(255f);
        }

        [Fact]
        public void MaxPixelValue_SetValue_ShouldUpdate()
        {
            var params_ = new NormalizationParams { MaxPixelValue = 1f };

            params_.MaxPixelValue.Should().Be(1f);
        }

        [Fact]
        public void Epsilon_DefaultValue_ShouldBe1eMinus5()
        {
            var params_ = new NormalizationParams();

            params_.Epsilon.Should().Be(1e-5f);
        }

        [Fact]
        public void Epsilon_SetValue_ShouldUpdate()
        {
            var params_ = new NormalizationParams { Epsilon = 1e-6f };

            params_.Epsilon.Should().Be(1e-6f);
        }

        #endregion

        #region Clone Tests

        [Fact]
        public void Clone_ShouldCreateDeepCopy()
        {
            var original = new NormalizationParams
            {
                Mean = new[] { 0.485f, 0.456f, 0.406f },
                Std = new[] { 0.229f, 0.224f, 0.225f },
                MaxPixelValue = 255f,
                Epsilon = 1e-5f
            };

            var clone = original.Clone();

            clone.Mean.Should().BeEquivalentTo(original.Mean);
            clone.Std.Should().BeEquivalentTo(original.Std);
            clone.MaxPixelValue.Should().Be(original.MaxPixelValue);
            clone.Epsilon.Should().Be(original.Epsilon);
        }

        [Fact]
        public void Clone_ModifyClone_ShouldNotAffectOriginal()
        {
            var original = new NormalizationParams
            {
                Mean = new[] { 0.485f, 0.456f, 0.406f },
                Std = new[] { 0.229f, 0.224f, 0.225f }
            };

            var clone = original.Clone();
            clone.Mean[0] = 0.5f;
            clone.Std[0] = 0.5f;

            original.Mean[0].Should().Be(0.485f);
            original.Std[0].Should().Be(0.229f);
        }

        #endregion
    }

    public class NormalizationParamsFactoryTests
    {
        #region GetParams Tests

        [Fact]
        public void GetParams_WithImageNetStandard_ShouldReturnImageNetParams()
        {
            var params_ = NormalizationParamsFactory.GetParams(ImageNormalizationType.ImageNetStandard);

            params_.Mean.Should().BeEquivalentTo(new[] { 0.485f, 0.456f, 0.406f });
            params_.Std.Should().BeEquivalentTo(new[] { 0.229f, 0.224f, 0.225f });
        }

        [Fact]
        public void GetParams_WithScale0To1_ShouldReturnCorrectParams()
        {
            var params_ = NormalizationParamsFactory.GetParams(ImageNormalizationType.Scale_0_1);

            params_.MaxPixelValue.Should().Be(255f);
        }

        [Fact]
        public void GetParams_WithScaleNeg1To1_ShouldReturnCorrectParams()
        {
            var params_ = NormalizationParamsFactory.GetParams(ImageNormalizationType.Scale_Neg1_1);

            params_.Mean.Should().BeEquivalentTo(new[] { 0.5f, 0.5f, 0.5f });
            params_.Std.Should().BeEquivalentTo(new[] { 0.5f, 0.5f, 0.5f });
        }

        [Fact]
        public void GetParams_WithCustomStandard_ShouldReturnCustomParams()
        {
            var customMean = new[] { 0.1f, 0.2f, 0.3f };
            var customStd = new[] { 0.4f, 0.5f, 0.6f };

            var params_ = NormalizationParamsFactory.GetParams(
                ImageNormalizationType.CustomStandard,
                customMean,
                customStd
            );

            params_.Mean.Should().BeEquivalentTo(customMean);
            params_.Std.Should().BeEquivalentTo(customStd);
        }

        [Fact]
        public void GetParams_WithCustomStandardAndNullMean_ShouldThrowArgumentException()
        {
            Action act = () => NormalizationParamsFactory.GetParams(
                ImageNormalizationType.CustomStandard,
                null,
                new[] { 0.5f, 0.5f, 0.5f }
            );

            act.Should().Throw<ArgumentException>()
                .WithMessage("*Custom normalization requires both mean and std parameters*");
        }

        [Fact]
        public void GetParams_WithCustomStandardAndNullStd_ShouldThrowArgumentException()
        {
            Action act = () => NormalizationParamsFactory.GetParams(
                ImageNormalizationType.CustomStandard,
                new[] { 0.5f, 0.5f, 0.5f },
                null
            );

            act.Should().Throw<ArgumentException>()
                .WithMessage("*Custom normalization requires both mean and std parameters*");
        }

        [Fact]
        public void GetParams_WithNone_ShouldReturnDefaultParams()
        {
            var params_ = NormalizationParamsFactory.GetParams(ImageNormalizationType.None);

            params_.Should().NotBeNull();
        }

        #endregion
    }
}
