using System;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates image-encoder inputs for official Segment Anything preprocessing contracts. / 为官方 Segment Anything 前处理合同创建图像 Encoder 输入。</summary>
    public sealed class OpenCvPromptableSegmentationInputFactory
    {
        private static readonly float[] SamMeans = { 123.675f, 116.28f, 103.53f };
        private static readonly float[] SamStandardDeviations = { 58.395f, 57.12f, 57.375f };
        private readonly OpenCvVisualInputFactory _inner = new OpenCvVisualInputFactory();

        /// <summary>Creates a SAM v1 input using RGB, longest-side resize, bottom/right zero padding, and official pixel normalization; the encoded-byte SHA becomes the image identity. / 使用 RGB、最长边缩放、底/右补零及官方像素归一化创建 SAM v1 输入；编码字节 SHA 作为图像 identity。</summary>
        public PreparedVisualInput CreateSamV1(OpenCvImageSource source, string inputName = "images", int imageSize = 1024, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (imageSize <= 0) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The SAM image size must be positive.");
            OpenCvPreprocessOptions options = CreateSamV1Options(imageSize, preprocessing);
            return _inner.Create(source, inputName, options, source.Sha256, cancellationToken);
        }

        internal static OpenCvPreprocessOptions CreateSamV1Options(int imageSize, VisualPreprocessingOptions? preprocessing = null)
        {
            VisualSize modelSize = new VisualSize(imageSize, imageSize);
            VisualPreprocessingOptions selected = preprocessing ?? CreateSamV1Preprocessing(imageSize);
            if (!selected.ModelSize.HasValue || selected.ModelSize.Value != modelSize || selected.Layout != VisualTensorLayout.Nchw || selected.ColorOrder != VisualColorOrder.Rgb || selected.OutputType != VisualPreprocessingOutputType.Float32 || selected.BatchSize != 1)
                throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "A SAM preprocessing override must preserve the encoder size, RGB/NCHW layout, Float32 output, and batch 1.");
            return selected.ToOpenCvOptions();
        }

        private static VisualPreprocessingOptions CreateSamV1Preprocessing(int imageSize)
        {
            if (imageSize <= 0) throw new OpenCvVisualException(OpenCvErrorCodes.PreprocessInvalid, "The SAM image size must be positive.");
            return new VisualPreprocessingOptions(
                new VisualSize(imageSize, imageSize),
                VisualResizeMode.LongestSidePadBottomRight,
                VisualColorOrder.Rgb,
                VisualNormalizationOptions.DivideByStandardDeviation(SamStandardDeviations, SamMeans),
                VisualTensorLayout.Nchw,
                1,
                VisualPreprocessingOutputType.Float32,
                VisualRgbColor.Black,
                dimensionRounding: VisualDimensionRounding.HalfUp,
                contractId: "sam-v1-longest-side-rgb-imagenet-v1");
        }

        /// <summary>Creates a SAM v1 input from an absolute PNG or JPEG path. / 从绝对 PNG 或 JPEG 路径创建 SAM v1 输入。</summary>
        public PreparedVisualInput CreateSamV1FromFile(string path, string inputName = "images", int imageSize = 1024, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null)
        {
            return CreateSamV1(OpenCvImageSource.FromFile(path), inputName, imageSize, cancellationToken, preprocessing);
        }

        /// <summary>Creates a SAM v1 input from copied encoded bytes. / 从已复制的编码字节创建 SAM v1 输入。</summary>
        public PreparedVisualInput CreateSamV1FromBytes(byte[] bytes, string inputName = "images", int imageSize = 1024, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null)
        {
            return CreateSamV1(OpenCvImageSource.FromBytes(bytes), inputName, imageSize, cancellationToken, preprocessing);
        }
    }
}
