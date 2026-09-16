using System;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates exact profile-bound BLIP-family image tensors from one decoded file or byte source. / 从单次解码的文件或字节源创建精确 Profile 绑定的 BLIP 家族图像张量。</summary>
    public sealed class OpenCvGenerativeVisionLanguageInputFactory
    {
        private readonly OpenCvVisualInputFactory _inner = new OpenCvVisualInputFactory();

        /// <summary>Decodes once, applies fixed RGB bicubic resize/normalization, and preserves exact encoded-source SHA identity. / 单次解码，应用固定 RGB 双三次缩放/归一化，并保留精确编码源 SHA Identity。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, GenerativeVisionLanguageProfile profile, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, profile.Blocker ?? "The BLIP-family profile is not executable.", profileId: profile.ProfileId);
            if (source.Length > profile.Processor.MaximumImageBytes) throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageLimitExceeded, "The encoded image exceeds the profile capacity.", profileId: profile.ProfileId);
            GenerativeVisionLanguageArtifactContract encoder = profile.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder);
            VisualPreprocessingOptions selected = preprocessing ?? new VisualPreprocessingOptions(
                profile.Processor.ImageSize,
                VisualResizeMode.Resize,
                VisualColorOrder.Rgb,
                VisualNormalizationOptions.MeanStandardDeviation(profile.Processor.Mean, profile.Processor.StandardDeviation),
                VisualTensorLayout.Nchw,
                1,
                interpolation: VisualInterpolationMode.PillowBicubic,
                contractId: profile.ProfileId + ":official-processor");
            if (!selected.ModelSize.HasValue || selected.ModelSize.Value != profile.Processor.ImageSize || selected.Layout != VisualTensorLayout.Nchw || selected.ColorOrder != VisualColorOrder.Rgb || selected.OutputType != VisualPreprocessingOutputType.Float32 || selected.BatchSize != 1)
                throw new VisualException(VisualErrorCodes.GenerativeVisionLanguageContractInvalid, "A generative vision-language preprocessing override must preserve the processor size, RGB/NCHW layout, Float32 output, and batch 1.", profileId: profile.ProfileId);
            var options = selected.ToOpenCvOptions();
            return _inner.Create(source, encoder.Inputs[0].Name, options, source.Sha256, cancellationToken);
        }

        /// <summary>Creates an owned prepared tensor from an absolute PNG/JPEG path. / 从绝对 PNG/JPEG 路径创建自有已准备张量。</summary>
        public PreparedVisualInput CreateFromFile(string path, GenerativeVisionLanguageProfile profile, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null) => Create(OpenCvImageSource.FromFile(path), profile, cancellationToken, preprocessing);

        /// <summary>Creates an owned prepared tensor from copied encoded bytes. / 从复制的编码字节创建自有已准备张量。</summary>
        public PreparedVisualInput CreateFromBytes(byte[] bytes, GenerativeVisionLanguageProfile profile, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null) => Create(OpenCvImageSource.FromBytes(bytes), profile, cancellationToken, preprocessing);
    }
}
