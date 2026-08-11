using System;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Creates audited BLIP, BLIP-2, and InstructBLIP artifact-bound profiles without inferring graph contracts. / 创建已审计且工件绑定的 BLIP、BLIP-2 与 InstructBLIP Profile，不推测图合同。</summary>
    public static class GenerativeVisionLanguageProfiles
    {
        private const string BlipCommit = "056a169437371659074aa2732649d5de3bffb4a8";
        private const string LavisCommit = "0ef3a2c3603596f88ec5d72bbeabcee4badf0cc7";
        private const string BlipLicense = "BSD-3-Clause";
        private const string BlipCaptionCheckpointUri = "https://storage.googleapis.com/sfr-vision-language-research/BLIP/models/model_base_caption_capfilt_large.pth";
        private const string BlipVqaCheckpointUri = "https://storage.googleapis.com/sfr-vision-language-research/BLIP/models/model_base_vqa_capfilt_large.pth";

        /// <summary>Creates the official BLIP base caption checkpoint split into an opset-17 image encoder and full-prefix no-KV language decoder. / 创建官方 BLIP Base Caption Checkpoint 的 opset-17 图像 Encoder 与 Full-prefix No-KV 语言 Decoder 双图合同。</summary>
        public static GenerativeVisionLanguageProfile CreateBlipCaptionBase(string profileId = "generative-vlm.blip.caption-base.opset17")
        {
            var vision = new GenerativeVisionLanguageArtifactContract(
                GenerativeVisionLanguageArtifactRole.VisionEncoder,
                new ModelId("external/generative-vlm/blip-caption-base/vision-encoder"),
                "onnx",
                "304b912cc437706e2fd34a54cfde1d156134a08a437bc7f46a29d2ea7fa759ef",
                344502972,
                17,
                new[] { Tensor("pixel_values", TensorElementType.Float32, 8_000_000, -1, 3, 384, 384) },
                new[] { Tensor("encoder_hidden_states", TensorElementType.Float32, 8_000_000, -1, 577, 768) },
                BlipCommit,
                "salesforce/BLIP official model + torch 2.9.1 torch.onnx.export(dynamo=false)",
                BlipLicense,
                BlipCaptionCheckpointUri);
            var decoder = new GenerativeVisionLanguageArtifactContract(
                GenerativeVisionLanguageArtifactRole.LanguageDecoder,
                new ModelId("external/generative-vlm/blip-caption-base/language-decoder-full-prefix"),
                "onnx",
                "56fcfa494c561f630b36bdf8ef62adc0597b559588995a73b56bdbab5604e686",
                645794706,
                17,
                new[]
                {
                    Tensor("input_ids", TensorElementType.Int64, 4096, -1, -1),
                    Tensor("attention_mask", TensorElementType.Int64, 4096, -1, -1),
                    Tensor("encoder_hidden_states", TensorElementType.Float32, 8_000_000, -1, 577, 768),
                    Tensor("encoder_attention_mask", TensorElementType.Int64, 100_000, -1, 577)
                },
                new[] { Tensor("logits", TensorElementType.Float32, 10_000_000, -1, -1, 30524) },
                BlipCommit,
                "salesforce/BLIP BertLMHeadModel use_cache=false + torch 2.9.1 torch.onnx.export(dynamo=false)",
                BlipLicense,
                BlipCaptionCheckpointUri);
            var processor = new GenerativeVisionLanguageProcessorContract(
                "salesforce-blip-caption-rgb-fixed-bicubic-384-v1",
                "76aa309c203810ad36f1ce32132dde5dc3b9c89675a19c2933df36d7085c751c",
                new VisualSize(384, 384),
                new[] { 122.7709383f, 116.7460125f, 104.09373615f },
                new[] { 68.5005327f, 66.6321579f, 70.32316305f },
                "PIL.Image.Resampling.BICUBIC",
                "salesforce/BLIP predict.py@" + BlipCommit);
            var tokenizer = new GenerativeVisionLanguageTokenizerContract("bert-base-uncased-blip-dec", "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3", "BertTokenizer plus [DEC]/[ENC]", 30524, 30522, 102, 0, 101, 19, "official uncased BasicTokenizer plus WordPiece");
            var generation = new GenerativeVisionLanguageGenerationContract("blip-caption-greedy-5-20-full-prefix-v1", "640f3443087a7ba7cf4e6819ca9c69379669b8556acc5c178cd0e121b2d74519", GenerativeVisionLanguageGenerationMode.Greedy, GenerativeVisionLanguageCacheMode.NoneFullPrefix, 5, 20);
            return new GenerativeVisionLanguageProfile(profileId, GenerativeVisionLanguageFamily.Blip, "model-base-caption-capfilt-large", GenerativeVisionLanguageTask.ImageCaptioning, processor, tokenizer, generation, "a picture of ", new[] { vision, decoder }, "blip-" + BlipCommit + "-checkpoint-96ac8749-opset17", true);
        }

        /// <summary>Creates the official BLIP base VQA source contract as a blocker because its question encoder, answer decoder/ranker, and exact checkpoint were not exported and verified locally. / 创建官方 BLIP Base VQA 源合同 blocker，因为其 Question Encoder、Answer Decoder/Ranker 与精确 Checkpoint 尚未在本机导出验证。</summary>
        public static GenerativeVisionLanguageProfile CreateBlipVqaBaseBlocker(string profileId = "generative-vlm.blip.vqa-base.external")
        {
            const string configSha = "fe22ae52b5a0af3a63eb8a803ae33ca98da237d6249f07a3c8e652f465f98fa8";
            const string blocker = "The official 1,446,244,375-byte BLIP VQA checkpoint URI and source contract were audited, but the checkpoint SHA256, question encoder, generated-answer decoder, answer-ranking graph, exact native ports, and ORT/OpenVINO evidence are unavailable.";
            var processor = Processor("salesforce-blip-vqa-rgb-fixed-bicubic-480", configSha, 480, "salesforce/BLIP configs/vqa.yaml and train_vqa.py@" + BlipCommit);
            var tokenizer = new GenerativeVisionLanguageTokenizerContract("bert-base-uncased-blip-enc-dec", "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3", "BertTokenizer plus [DEC]/[ENC]", 30524, 30522, 102, 0, 101, 35, "official uncased BasicTokenizer plus WordPiece");
            var generation = new GenerativeVisionLanguageGenerationContract("blip-vqa-generate-beam3-1-10", configSha, GenerativeVisionLanguageGenerationMode.BeamSearch, GenerativeVisionLanguageCacheMode.PastPresent, 1, 10, 3);
            return new GenerativeVisionLanguageProfile(profileId, GenerativeVisionLanguageFamily.Blip, "model-base-vqa-capfilt-large", GenerativeVisionLanguageTask.VisualQuestionAnswering, processor, tokenizer, generation, "{0}", Array.Empty<GenerativeVisionLanguageArtifactContract>(), "blip-" + BlipCommit + "-external-blocker", false, blocker);
        }

        /// <summary>Creates the official LAVIS BLIP-2 COCO caption OPT-2.7B source contract as an explicit multi-component native-export blocker. / 创建官方 LAVIS BLIP-2 COCO Caption OPT-2.7B 源合同，作为显式多组件 Native Export blocker。</summary>
        public static GenerativeVisionLanguageProfile CreateBlip2CaptionOpt27BBlocker(string profileId = "generative-vlm.blip2.caption-opt2.7b.external")
        {
            const string configSha = "c973fa1d05721187355f60bf49ac0d2327b87a86903f197c25acd5ec25d43c37";
            const string blocker = "The official 4,374,813,231-byte BLIP-2 caption checkpoint and 428,705,327-byte pretrained checkpoint URIs were audited, but neither SHA256 nor a complete official ONNX/OpenVINO EVA-CLIP-G, Q-Former/query-token, projection, OPT tokenizer/decoder, and KV-cache bundle is available locally.";
            var processor = Processor("lavis-blip2-caption-opt2.7b-rgb-fixed-bicubic-364", configSha, 364, "LAVIS blip_image_eval plus blip2_caption_opt2.7b.yaml@" + LavisCommit);
            var tokenizer = new GenerativeVisionLanguageTokenizerContract("facebook-opt-2.7b-unresolved", configSha, "AutoTokenizer(use_fast=false)", "The official config names facebook/opt-2.7b, but exact tokenizer files, revision, vocabulary SHA256, and special-token IDs were not acquired.");
            var generation = new GenerativeVisionLanguageGenerationContract("lavis-blip2-opt-beam5-1-30", configSha, GenerativeVisionLanguageGenerationMode.BeamSearch, GenerativeVisionLanguageCacheMode.PastPresent, 1, 30, 5, topP: .9f);
            return new GenerativeVisionLanguageProfile(profileId, GenerativeVisionLanguageFamily.Blip2, "caption-coco-opt2.7b", GenerativeVisionLanguageTask.ImageCaptioning, processor, tokenizer, generation, "a photo of", Array.Empty<GenerativeVisionLanguageArtifactContract>(), "lavis-" + LavisCommit + "-external-blocker", false, blocker);
        }

        /// <summary>Creates the official LAVIS InstructBLIP Flan-T5-XL source contract as an explicit instruction/Q-Former/T5 native-export blocker. / 创建官方 LAVIS InstructBLIP Flan-T5-XL 源合同，作为显式 Instruction/Q-Former/T5 Native Export blocker。</summary>
        public static GenerativeVisionLanguageProfile CreateInstructBlipFlanT5XlBlocker(string profileId = "generative-vlm.instructblip.flant5xl.external")
        {
            const string configSha = "c4c2c819586483006c3fd21b285a19356fe56d5c308f3e3802b95244f4b543df";
            const string blocker = "The official 2,247,595,175-byte InstructBLIP Flan-T5-XL checkpoint URI was audited, but its SHA256 and a complete official ONNX/OpenVINO EVA-CLIP-G, instruction-aware Q-Former/query-token, projection, Flan-T5 tokenizer/encoder/decoder, and KV-cache bundle are unavailable locally.";
            var processor = Processor("lavis-instructblip-flant5xl-rgb-fixed-bicubic-224", configSha, 224, "LAVIS blip_image_eval plus blip2_instruct_flant5xl.yaml@" + LavisCommit);
            var tokenizer = new GenerativeVisionLanguageTokenizerContract("google-flan-t5-xl-unresolved", configSha, "T5TokenizerFast", "The official config names google/flan-t5-xl, but exact tokenizer files, revision, vocabulary SHA256, and special-token IDs were not acquired.");
            var generation = new GenerativeVisionLanguageGenerationContract("lavis-instructblip-flant5xl-beam5-new1-256", configSha, GenerativeVisionLanguageGenerationMode.BeamSearch, GenerativeVisionLanguageCacheMode.PastPresent, 1, 256, 5, topP: .9f, lengthMode: GenerativeVisionLanguageLengthMode.NewTokens);
            return new GenerativeVisionLanguageProfile(profileId, GenerativeVisionLanguageFamily.InstructBlip, "flant5xl", GenerativeVisionLanguageTask.ConditionalTextGeneration, processor, tokenizer, generation, "{0}", Array.Empty<GenerativeVisionLanguageArtifactContract>(), "lavis-" + LavisCommit + "-external-blocker", false, blocker);
        }

        private static GenerativeVisionLanguageProcessorContract Processor(string id, string sha, int size, string implementation) => new GenerativeVisionLanguageProcessorContract(id, sha, new VisualSize(size, size), new[] { 122.7709383f, 116.7460125f, 104.09373615f }, new[] { 68.5005327f, 66.6321579f, 70.32316305f }, "PIL.Image.Resampling.BICUBIC", implementation);
        private static GenerativeVisionLanguageTensorContract Tensor(string name, TensorElementType type, long maximumElements, params long[] shape) => new GenerativeVisionLanguageTensorContract(name, type, new TensorShape(shape), maximumElements);
    }
}
