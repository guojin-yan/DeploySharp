using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Provides immutable audited CLIP, SigLIP, and SigLIP 2 contracts. / 提供不可变、已审计的 CLIP、SigLIP 与 SigLIP 2 合同。</summary>
    public static class VisionLanguageProfiles
    {
        private const string ClipCommit = "d05afc436d78f1c48dc0dbf8e5980a9d471f35f6";
        private const string ClipRevision = "3d74acf9a28c67741b2f4f2ea7635f0aaf6f0268";
        private const string SigLipCommit = "0127fb6b337ee2a27bf4e54dea79cff176527356";
        private const string SigLipRevision = "7fd15f0689c79d79e38b1c2e2e2370a7bf2761ed";
        private const string SigLip2Revision = "75de2d55ec2d0b4efc50b3e9ad70dba96a7b2fa2";

        /// <summary>Creates the official OpenAI CLIP ViT-B/32 opset-17 split encoder contract. / 创建官方 OpenAI CLIP ViT-B/32 opset-17 双 Encoder 合同。</summary>
        public static VisionLanguageEmbeddingProfile CreateClipVitB32(string profileId = "vision-language.clip-vit-b-32.opset17")
        {
            var image = new VisionLanguageArtifactContract(VisionLanguageArtifactRole.ImageEncoder, new ModelId("external/vlm/clip-vit-b-32/image"), "onnx", "51e6e8f7c1d0f43c9434751d55238bba6cd6fde02865a4839683f82928e30963", 351593168, 17,
                new[] { Tensor("pixel_values", TensorElementType.Float32, -1, 3, 224, 224) }, new[] { Tensor("image_embedding", TensorElementType.Float32, -1, 512) },
                ClipCommit + ";hf:" + ClipRevision, "transformers 4.57.3 CLIPModel official get_image_features + torch.onnx.export(dynamo=false)", "MIT", "https://huggingface.co/openai/clip-vit-base-patch32/tree/" + ClipRevision);
            var text = new VisionLanguageArtifactContract(VisionLanguageArtifactRole.TextEncoder, new ModelId("external/vlm/clip-vit-b-32/text"), "onnx", "e167dd8f5510fb1bf6cdf6458f0582e69f27abacd123fe660b93ca90db4be3a8", 253943687, 17,
                new[] { Tensor("input_ids", TensorElementType.Int64, -1, 77), Tensor("attention_mask", TensorElementType.Int64, -1, 77) }, new[] { Tensor("text_embedding", TensorElementType.Float32, -1, 512) },
                ClipCommit + ";hf:" + ClipRevision, "transformers 4.57.3 CLIPModel official get_text_features + torch.onnx.export(dynamo=false)", "MIT", "https://huggingface.co/openai/clip-vit-base-patch32/tree/" + ClipRevision);
            return new VisionLanguageEmbeddingProfile(profileId, VisionLanguageModelFamily.Clip, "vit-base-patch32", new VisionLanguageTokenizerContract("openai-clip-bpe-77", "b556ac8c99757ffb677208af34bc8c6721572114111a6e0aaf5fa69ff0b8d842", 77, 49406, 49407, 49407, true, "official CLIP lowercase byte-BPE; RGB bicubic shortest-edge 224 center crop", "CLIPTokenizerFast"), new[] { image, text }, 512, VisionLanguagePooling.EndOfText, VisionLanguageScoreSemantics.ClipSoftmax, 100.0000076f, 0f, new VisualSize(224, 224), new[] { 122.7709383f, 116.7460125f, 104.09373615f }, new[] { 68.5005327f, 66.6321579f, 70.32316305f }, VisionLanguageImageResizeMode.ShortestEdgeCenterCrop, "hf-" + ClipRevision + "-opset17", true, maximumTextBatch: 64, maximumImageBatch: 16);
        }

        /// <summary>Creates the official Google SigLIP base patch-16/224 opset-17 split encoder contract. / 创建官方 Google SigLIP base patch-16/224 opset-17 双 Encoder 合同。</summary>
        public static VisionLanguageEmbeddingProfile CreateSigLipBase(string profileId = "vision-language.siglip-base-patch16-224.opset17")
        {
            var image = new VisionLanguageArtifactContract(VisionLanguageArtifactRole.ImageEncoder, new ModelId("external/vlm/siglip-base-patch16-224/image"), "onnx", "6f6d699bee2f2978675a3aa5e3d47c2933df0a9e68ea4ad854c77cdde9174155", 371784017, 17,
                new[] { Tensor("pixel_values", TensorElementType.Float32, -1, 3, 224, 224) }, new[] { Tensor("image_embedding", TensorElementType.Float32, -1, 768) },
                SigLipCommit + ";hf:" + SigLipRevision, "transformers 4.57.3 SiglipModel official get_image_features + torch.onnx.export(dynamo=false)", "Apache-2.0", "https://huggingface.co/google/siglip-base-patch16-224/tree/" + SigLipRevision);
            var text = new VisionLanguageArtifactContract(VisionLanguageArtifactRole.TextEncoder, new ModelId("external/vlm/siglip-base-patch16-224/text"), "onnx", "da30eb3ed3fc15add817d4c24ebcd53bfd4525cae833b3f91e82a04fe1d9c9c9", 441298653, 17,
                new[] { Tensor("input_ids", TensorElementType.Int64, -1, 64) }, new[] { Tensor("text_embedding", TensorElementType.Float32, -1, 768) },
                SigLipCommit + ";hf:" + SigLipRevision, "transformers 4.57.3 SiglipModel official get_text_features + torch.onnx.export(dynamo=false)", "Apache-2.0", "https://huggingface.co/google/siglip-base-patch16-224/tree/" + SigLipRevision);
            return new VisionLanguageEmbeddingProfile(profileId, VisionLanguageModelFamily.SigLip, "base-patch16-224", new VisionLanguageTokenizerContract("google-siglip-spiece-64", "c6e405cb7c670d56636a9402c81023a55bc6c3c53d89cf02b92f5c5005bfe920", 64, -1, 1, 1, false, "official SigLIP lowercase SentencePiece; RGB bicubic fixed 224 resize; mean/std 0.5", "SiglipTokenizer"), new[] { image, text }, 768, VisionLanguagePooling.ModelPooler, VisionLanguageScoreSemantics.SigLipIndependentSigmoid, 117.3307648f, -12.93243694f, new VisualSize(224, 224), new[] { 127.5f, 127.5f, 127.5f }, new[] { 127.5f, 127.5f, 127.5f }, VisionLanguageImageResizeMode.FixedResize, "hf-" + SigLipRevision + "-opset17", true, maximumTextBatch: 64, maximumImageBatch: 16);
        }

        /// <summary>Creates the official SigLIP 2 profile as an external-only blocker until a reproducible native dual-encoder export is audited. / 创建官方 SigLIP 2 外部阻断 Profile，直到完成可复现本机双 Encoder 导出审计。</summary>
        public static VisionLanguageEmbeddingProfile CreateSigLip2BaseBlocker(string profileId = "vision-language.siglip2-base-patch16-224.external")
        {
            var tokenizer = new VisionLanguageTokenizerContract("google-siglip2-gemma-tokenizer", "61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2", 64, 2, 1, 0, false, "official SigLIP2 Gemma SentencePiece; add_bos_token=false", "GemmaTokenizer");
            return new VisionLanguageEmbeddingProfile(profileId, VisionLanguageModelFamily.SigLip2, "base-patch16-224", tokenizer, System.Array.Empty<VisionLanguageArtifactContract>(), 768, VisionLanguagePooling.ModelPooler, VisionLanguageScoreSemantics.SigLipIndependentSigmoid, 1f, 0f, new VisualSize(224, 224), new[] { 127.5f, 127.5f, 127.5f }, new[] { 127.5f, 127.5f, 127.5f }, VisionLanguageImageResizeMode.FixedResize, "hf-" + SigLip2Revision + "-external-blocker", false, "Official SigLIP2 checkpoint/revision is available, but no local official ONNX/OpenVINO dual-encoder export was audited; conversion and processor contract remain unverified; no resident Python fallback.", maximumTextBatch: 64, maximumImageBatch: 16);
        }

        private static VisionLanguageTensorContract Tensor(string name, TensorElementType type, params long[] shape) => new VisionLanguageTensorContract(name, type, new TensorShape(shape));
    }
}
