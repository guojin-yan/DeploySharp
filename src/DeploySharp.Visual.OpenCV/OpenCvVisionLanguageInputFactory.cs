using System;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates exact CLIP/SigLIP image tensors from one decoded PNG, JPEG, file, or byte source. / 从单次解码的 PNG、JPEG、文件或字节源创建精确 CLIP/SigLIP 图像张量。</summary>
    public sealed class OpenCvVisionLanguageInputFactory
    {
        private readonly OpenCvVisualInputFactory _inner = new OpenCvVisualInputFactory();

        /// <summary>Decodes once, applies the profile-bound RGB geometry/normalization, and preserves the encoded-source SHA identity. / 单次解码，应用 Profile 绑定的 RGB 几何与归一化，并保留编码源 SHA Identity。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, VisionLanguageEmbeddingProfile profile, int batchSize = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, profile.Blocker ?? "The VLM profile has no native image encoder.", profileId: profile.ProfileId);
            if (batchSize <= 0 || batchSize > profile.MaximumImageBatch) throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "The image batch exceeds the profile capacity.", profileId: profile.ProfileId);
            VisionLanguageArtifactContract artifact = profile.GetArtifact(VisionLanguageArtifactRole.ImageEncoder);
            OpenCvResizeMode resize = profile.ImageResizeMode == VisionLanguageImageResizeMode.ShortestEdgeCenterCrop ? OpenCvResizeMode.ShortestEdgeCenterCrop : OpenCvResizeMode.Resize;
            var options = new OpenCvPreprocessOptions(profile.ImageSize, resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, profile.ImageMean, profile.ImageStandardDeviation, VisualTensorLayout.Nchw, batchSize, OpenCvOutputType.Float32, interpolation: OpenCvInterpolation.Cubic);
            return _inner.Create(source, artifact.Inputs[0].Name, options, source.Sha256, cancellationToken);
        }

        /// <summary>Creates a profile-bound image tensor from an absolute PNG or JPEG path. / 从绝对 PNG 或 JPEG 路径创建 Profile 绑定的图像张量。</summary>
        public PreparedVisualInput CreateFromFile(string path, VisionLanguageEmbeddingProfile profile, int batchSize = 1, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromFile(path), profile, batchSize, cancellationToken);

        /// <summary>Creates a profile-bound image tensor from copied encoded bytes. / 从复制的编码字节创建 Profile 绑定的图像张量。</summary>
        public PreparedVisualInput CreateFromBytes(byte[] bytes, VisionLanguageEmbeddingProfile profile, int batchSize = 1, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromBytes(bytes), profile, batchSize, cancellationToken);
    }
}
