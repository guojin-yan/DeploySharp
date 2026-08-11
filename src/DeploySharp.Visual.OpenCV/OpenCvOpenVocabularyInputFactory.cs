using System;
using System.Threading;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates exact image inputs for executable open-vocabulary detectors and Grounded-SAM composition. / 为可执行开放词汇检测器与 Grounded-SAM 组合创建精确图像输入。</summary>
    public sealed class OpenCvOpenVocabularyInputFactory
    {
        private readonly OpenCvVisualInputFactory _inner = new OpenCvVisualInputFactory();

        /// <summary>Creates one detector tensor from PNG/JPEG/bytes using the artifact-bound RGB letterbox contract. / 使用工件绑定的 RGB Letterbox 合同从 PNG/JPEG/字节创建一个检测器张量。</summary>
        public PreparedVisualInput Create(OpenCvImageSource source, OpenVocabularyDetectionProfile profile, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            YoloDetectionProfile detector = profile.DetectorProfile ?? throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The open-vocabulary profile has no executable image graph: " + profile.Blocker + ".", profileId: profile.ProfileId);
            return _inner.Create(source, detector.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(detector), source.Sha256, cancellationToken);
        }

        /// <summary>Creates one detector tensor from an absolute PNG or JPEG path. / 从绝对 PNG 或 JPEG 路径创建一个检测器张量。</summary>
        public PreparedVisualInput CreateFromFile(string path, OpenVocabularyDetectionProfile profile, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromFile(path), profile, cancellationToken);

        /// <summary>Creates one detector tensor from copied encoded bytes. / 从复制的编码字节创建一个检测器张量。</summary>
        public PreparedVisualInput CreateFromBytes(byte[] bytes, OpenVocabularyDetectionProfile profile, CancellationToken cancellationToken = default(CancellationToken)) => Create(OpenCvImageSource.FromBytes(bytes), profile, cancellationToken);

        /// <summary>Decodes the image exactly once, then prepares detector and SAM encoder tensors with the same encoded-image identity. / 图像仅解码一次，随后以相同编码图像 Identity 准备检测器与 SAM Encoder 张量。</summary>
        public GroundedSamPreparedInput CreateGroundedSam(OpenCvImageSource source, OpenVocabularyDetectionProfile detectorProfile, PromptableSegmentationProfile segmentationProfile, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (detectorProfile == null) throw new ArgumentNullException(nameof(detectorProfile));
            if (segmentationProfile == null) throw new ArgumentNullException(nameof(segmentationProfile));
            YoloDetectionProfile detector = detectorProfile.DetectorProfile ?? throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The open-vocabulary profile has no executable image graph: " + detectorProfile.Blocker + ".", profileId: detectorProfile.ProfileId);
            if (segmentationProfile.ExecutionKind != PromptableSegmentationExecutionKind.SamV1ImageOnnx || segmentationProfile.SamV1TensorMap == null) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "The segmentation profile has no supported SAM v1 native image contract.", profileId: segmentationProfile.ProfileId);
            OpenCvRuntimePreflight.Check();
            cancellationToken.ThrowIfCancellationRequested();
            PreparedVisualInput? detectorInput = null;
            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    detectorInput = OpenCvVisualInputFactory.CreateFromDecoded(decoded, detector.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(detector), source.Sha256, cancellationToken);
                    PreparedVisualInput segmentationInput = OpenCvVisualInputFactory.CreateFromDecoded(decoded, segmentationProfile.SamV1TensorMap.ImageInput, OpenCvPromptableSegmentationInputFactory.CreateSamV1Options(segmentationProfile.ImageInputSize.Width), source.Sha256, cancellationToken);
                    return new GroundedSamPreparedInput(detectorInput, segmentationInput);
                }
            }
            catch
            {
                detectorInput?.Dispose();
                throw;
            }
        }

        /// <summary>Creates a single-decode Grounded-SAM input from an absolute image path. / 从绝对图像路径创建单次解码 Grounded-SAM 输入。</summary>
        public GroundedSamPreparedInput CreateGroundedSamFromFile(string path, OpenVocabularyDetectionProfile detectorProfile, PromptableSegmentationProfile segmentationProfile, CancellationToken cancellationToken = default(CancellationToken)) => CreateGroundedSam(OpenCvImageSource.FromFile(path), detectorProfile, segmentationProfile, cancellationToken);

        /// <summary>Creates a single-decode Grounded-SAM input from copied encoded bytes. / 从复制的编码字节创建单次解码 Grounded-SAM 输入。</summary>
        public GroundedSamPreparedInput CreateGroundedSamFromBytes(byte[] bytes, OpenVocabularyDetectionProfile detectorProfile, PromptableSegmentationProfile segmentationProfile, CancellationToken cancellationToken = default(CancellationToken)) => CreateGroundedSam(OpenCvImageSource.FromBytes(bytes), detectorProfile, segmentationProfile, cancellationToken);
    }
}
