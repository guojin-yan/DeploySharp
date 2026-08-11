using System;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Creates pinned Segment Anything model-family profiles without inferring contracts from filenames or ranks. / 创建固定的 Segment Anything 模型族 Profile，不从文件名或 Rank 推断合同。</summary>
    public static class PromptableSegmentationProfiles
    {
        /// <summary>Creates the official SAM v1 ViT image contract: fixed image encoder plus Meta's dynamic-point prompt/mask decoder export. / 创建官方 SAM v1 ViT 图像合同：固定图像 Encoder 加 Meta 动态点 Prompt/Mask Decoder 导出。</summary>
        public static PromptableSegmentationProfile CreateSamV1(
            string profileId,
            ModelId encoderModelId,
            ModelId decoderModelId,
            string encoderSha256,
            string decoderSha256,
            string upstreamCommit,
            string encoderExporter,
            string decoderExporter,
            int imageSize = 1024,
            int embeddingChannels = 256,
            int embeddingSize = 64,
            int lowResolutionMaskSize = 256,
            int opset = 17,
            int maximumPromptPoints = 64,
            int maximumCandidates = 4,
            long maximumSourceMaskPixels = 67108864)
        {
            if (imageSize <= 0 || embeddingChannels <= 0 || embeddingSize <= 0 || lowResolutionMaskSize <= 0) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "SAM tensor dimensions must be positive.");
            const string upstream = "https://github.com/facebookresearch/segment-anything";
            const string license = "Apache-2.0";
            var map = new SamV1TensorMap("images", "image_embeddings", "point_coords", "point_labels", "mask_input", "has_mask_input", "orig_im_size", "masks", "iou_predictions", "low_res_masks");
            var encoder = new PromptableSegmentationArtifactContract(
                PromptableSegmentationArtifactRole.ImageEncoder,
                encoderModelId,
                "onnx",
                encoderSha256,
                opset,
                new[] { new PromptableTensorContract(map.ImageInput, TensorElementType.Float32, new TensorShape(1, 3, imageSize, imageSize)) },
                new[] { new PromptableTensorContract(map.ImageEmbedding, TensorElementType.Float32, new TensorShape(1, embeddingChannels, embeddingSize, embeddingSize)) },
                upstream,
                upstreamCommit,
                encoderExporter,
                license);
            var decoder = new PromptableSegmentationArtifactContract(
                PromptableSegmentationArtifactRole.PromptMaskDecoder,
                decoderModelId,
                "onnx",
                decoderSha256,
                opset,
                new[]
                {
                    new PromptableTensorContract(map.ImageEmbedding, TensorElementType.Float32, new TensorShape(1, embeddingChannels, embeddingSize, embeddingSize)),
                    new PromptableTensorContract(map.PointCoordinates, TensorElementType.Float32, new TensorShape(1, -1, 2)),
                    new PromptableTensorContract(map.PointLabels, TensorElementType.Float32, new TensorShape(1, -1)),
                    new PromptableTensorContract(map.MaskInput, TensorElementType.Float32, new TensorShape(1, 1, lowResolutionMaskSize, lowResolutionMaskSize)),
                    new PromptableTensorContract(map.HasMaskInput, TensorElementType.Float32, new TensorShape(1)),
                    new PromptableTensorContract(map.OriginalImageSize, TensorElementType.Float32, new TensorShape(2))
                },
                new[]
                {
                    new PromptableTensorContract(map.Masks, TensorElementType.Float32, new TensorShape(1, maximumCandidates, -1, -1)),
                    new PromptableTensorContract(map.Quality, TensorElementType.Float32, new TensorShape(1, maximumCandidates)),
                    new PromptableTensorContract(map.LowResolutionMasks, TensorElementType.Float32, new TensorShape(1, maximumCandidates, lowResolutionMaskSize, lowResolutionMaskSize))
                },
                upstream,
                upstreamCommit,
                decoderExporter,
                license);
            return new PromptableSegmentationProfile(
                profileId,
                PromptableSegmentationFamily.Sam,
                "sam-v1-vit-image",
                PromptableSegmentationExecutionKind.SamV1ImageOnnx,
                PromptableSegmentationCapabilities.Points | PromptableSegmentationCapabilities.Boxes | PromptableSegmentationCapabilities.MaskFeedback | PromptableSegmentationCapabilities.Multimask,
                new[] { encoder, decoder },
                new VisualSize(imageSize, imageSize),
                map,
                0f,
                PromptableMaskQualityKind.PredictedIoU,
                maximumPromptPoints,
                maximumCandidates,
                maximumSourceMaskPixels,
                lowResolutionMaskSize,
                "meta-sam-resize-longest-side-pad-bottom-right-rgb-pixelmean-v1",
                "meta-sam-onnx-source-logits-strict-threshold-rle-v1");
        }

        /// <summary>Creates the audited official SAM 2 video contract as a non-executable native-export blocker; it cannot create a backend session. / 将已审计的官方 SAM 2 视频合同创建为不可执行的 native 导出 blocker；不能据此创建 Backend Session。</summary>
        public static PromptableSegmentationProfile CreateSam2VideoBlocker(string profileId, string upstreamCommit, string blocker, int maximumObjects = 16, int maximumFrames = 10000)
        {
            return CreateVideoBlocker(profileId, PromptableSegmentationFamily.Sam2, "sam2-official-video-pytorch", upstreamCommit, PromptableSegmentationCapabilities.Points | PromptableSegmentationCapabilities.Boxes | PromptableSegmentationCapabilities.VideoPropagation, blocker, maximumObjects, maximumFrames);
        }

        /// <summary>Creates the audited official SAM 3 video contract as a non-executable native-export blocker; it cannot create a backend session. / 将已审计的官方 SAM 3 视频合同创建为不可执行的 native 导出 blocker；不能据此创建 Backend Session。</summary>
        public static PromptableSegmentationProfile CreateSam3VideoBlocker(string profileId, string upstreamCommit, string blocker, int maximumObjects = 100, int maximumFrames = 10000)
        {
            return CreateVideoBlocker(profileId, PromptableSegmentationFamily.Sam3, "sam3-official-video-pytorch", upstreamCommit, PromptableSegmentationCapabilities.Text | PromptableSegmentationCapabilities.Boxes | PromptableSegmentationCapabilities.VideoPropagation, blocker, maximumObjects, maximumFrames);
        }

        private static PromptableSegmentationProfile CreateVideoBlocker(string profileId, PromptableSegmentationFamily family, string version, string upstreamCommit, PromptableSegmentationCapabilities capabilities, string blocker, int maximumObjects, int maximumFrames)
        {
            if (string.IsNullOrWhiteSpace(upstreamCommit)) throw new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, "An exact upstream video revision is required.");
            var video = new PromptableVideoStateContract(false, "strict ascending source-frame index", "upstream predictor mutates per-object memory and tracker state", "cancelled propagation must not publish a partially advanced state", maximumObjects, maximumFrames, blocker);
            return new PromptableSegmentationProfile(profileId, family, version + "@" + upstreamCommit.Trim(), PromptableSegmentationExecutionKind.ExternalContractOnly, capabilities, Array.Empty<PromptableSegmentationArtifactContract>(), new VisualSize(1024, 1024), null, video: video);
        }
    }
}
