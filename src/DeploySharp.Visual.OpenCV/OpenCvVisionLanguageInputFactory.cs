using System;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates exact CLIP/SigLIP image tensors from one decoded PNG, JPEG, file, or byte source. / 从单次解码的 PNG、JPEG、文件或字节源创建精确 CLIP/SigLIP 图像张量。</summary>
    public sealed class OpenCvVisionLanguageInputFactory
    {
        private readonly OpenCvVisualInputFactory _inner = new OpenCvVisualInputFactory();

        /// <summary>Decodes once, applies the profile-bound RGB geometry/normalization, and preserves the encoded-source SHA identity. / 单次解码，应用 Profile 绑定的 RGB 几何与归一化，并保留编码源 SHA Identity。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, VisionLanguageEmbeddingProfile profile, int batchSize = 1, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, profile.Blocker ?? "The VLM profile has no native image encoder.", profileId: profile.ProfileId);
            if (preprocessing != null && batchSize != 1 && batchSize != preprocessing.BatchSize) throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "The requested image batch and preprocessing batch differ.", profileId: profile.ProfileId);
            int effectiveBatch = preprocessing?.BatchSize ?? batchSize;
            if (effectiveBatch <= 0 || effectiveBatch > profile.MaximumImageBatch) throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "The image batch exceeds the profile capacity.", profileId: profile.ProfileId);
            VisionLanguageArtifactContract artifact = profile.GetArtifact(VisionLanguageArtifactRole.ImageEncoder);
            OpenCvResizeMode resize = profile.ImageResizeMode == VisionLanguageImageResizeMode.ShortestEdgeCenterCrop ? OpenCvResizeMode.ShortestEdgeCenterCrop : OpenCvResizeMode.Resize;
            VisualPreprocessingOptions selected = preprocessing ?? new VisualPreprocessingOptions(
                profile.ImageSize,
                resize == OpenCvResizeMode.ShortestEdgeCenterCrop ? VisualResizeMode.ShortestEdgeCenterCrop : VisualResizeMode.Resize,
                VisualColorOrder.Rgb,
                VisualNormalizationOptions.MeanStandardDeviation(profile.ImageMean, profile.ImageStandardDeviation),
                VisualTensorLayout.Nchw,
                effectiveBatch,
                interpolation: VisualInterpolationMode.Cubic,
                contractId: profile.ProfileId + ":official-processor");
            ValidateOverride(selected, profile.ImageSize, effectiveBatch, profile.ProfileId);
            var options = selected.ToOpenCvOptions();
            return _inner.Create(source, artifact.Inputs[0].Name, options, source.Sha256, cancellationToken);
        }

        /// <summary>Creates a profile-bound image tensor from an absolute PNG or JPEG path. / 从绝对 PNG 或 JPEG 路径创建 Profile 绑定的图像张量。</summary>
        public PreparedVisualInput CreateFromFile(string path, VisionLanguageEmbeddingProfile profile, int batchSize = 1, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null) => Create(OpenCvImageSource.FromFile(path), profile, batchSize, cancellationToken, preprocessing);

        /// <summary>Creates a profile-bound image tensor from copied encoded bytes. / 从复制的编码字节创建 Profile 绑定的图像张量。</summary>
        public PreparedVisualInput CreateFromBytes(byte[] bytes, VisionLanguageEmbeddingProfile profile, int batchSize = 1, CancellationToken cancellationToken = default(CancellationToken), VisualPreprocessingOptions? preprocessing = null) => Create(OpenCvImageSource.FromBytes(bytes), profile, batchSize, cancellationToken, preprocessing);

        private static void ValidateOverride(VisualPreprocessingOptions options, VisualSize modelSize, int requestedBatch, string profileId)
        {
            if (!options.ModelSize.HasValue || options.ModelSize.Value != modelSize || options.Layout != VisualTensorLayout.Nchw || options.ColorOrder != VisualColorOrder.Rgb || options.OutputType != VisualPreprocessingOutputType.Float32)
                throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A vision-language preprocessing override must preserve the encoder size, RGB/NCHW layout, and Float32 output.", profileId: profileId);
            if (options.BatchSize != requestedBatch)
                throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "The preprocessing batch and requested image batch differ.", profileId: profileId);
        }
    }
}
