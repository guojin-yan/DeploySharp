using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual.Models.Yolo;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Creates audited open-vocabulary family profiles without guessing contracts from filenames or ranks. / 创建已审计的开放词汇模型族 Profile，不从文件名或 Rank 猜测合同。</summary>
    public static class OpenVocabularyDetectionProfiles
    {
        private const string UltralyticsRepository = "https://github.com/ultralytics/ultralytics";

        /// <summary>Creates the exact two-class Ultralytics YOLO-Worldv2 artifact exported after official CLIP reparameterization. / 创建经官方 CLIP 重参数化后导出的精确二类 Ultralytics YOLO-Worldv2 工件。</summary>
        public static OpenVocabularyDetectionProfile CreateUltralyticsYoloWorldV2PersonBus()
        {
            const string profileId = "open-vocabulary.yolo-world-v2.ultralytics-8.2.2.person-bus.onnx";
            const string detectorSha = "42f9d408c0ba8f941fa5efd503c8d4faa175fff1705686174684ae5e6de29bdd";
            const string sourceSha = "7c951d3baac2de906e3deda859b2ff965350fbfa14f51b81ea52f0e3d98519c8";
            const string tokenizerSha = "924691ac288e54409236115652ad4aa250f48203de50a9e4722a6ecd48d6804a";
            const string clipSha = "40d365715913c9da98579312b702a82c18be219cc2a73407c4526f58eba950af";
            const string embeddingSha = "e047a003ac4cf14a051aadef984378198c1237c6677a4dff0069cd6da6d74753";
            const string upstreamCommit = "1110258d379bed8d623068ff7ceda8c9290f0774";
            var vocabulary = new VocabularyPrompt(new[] { "person", "bus" }, VocabularyNormalization.Exact, 2, 64);
            var person = new int[77];
            person[0] = 49406; person[1] = 2533; person[2] = 49407;
            var bus = new int[77];
            bus[0] = 49406; bus[1] = 2840; bus[2] = 49407;
            var tokenization = new[]
            {
                new OpenVocabularyTokenizationEntry(0, person),
                new OpenVocabularyTokenizationEntry(1, bus)
            };
            var embedding = new OpenVocabularyEmbeddingIdentity(vocabulary.Sha256, "clip-bpe-77", tokenizerSha, "clip-vit-b-32", clipSha, embeddingSha, 2, 512);
            var modelId = new ModelId("external/yolov8s-worldv2-person-bus-fixed");
            var decoderOptions = new YoloDetectionDecoderOptions(.25f, .7f, DetectionNmsMode.ClassAware, YoloClassSelectionMode.BestClassOnly, 30000, 300);
            var baseProfile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV8,
                modelId,
                detectorSha,
                new[] { "person", "bus" },
                upstreamCommit,
                "Ultralytics 8.2.2 set_classes then ONNX export; opset17; batch1; imgsz640",
                new YoloDetectionProfileOptions(17, new VisualSize(640, 640), "images", "output0", 32, 114, true, false, YoloScoreActivation.Identity, decoderOptions, profileId, "ultralytics-letterbox-rgb-nchw-v1", "ultralytics-raw-head-managed-class-aware-nms-v1"));
            var decoder = new OpenVocabularyDetectionDecoder(baseProfile.VisualProfile.Decoder, profileId, vocabulary, tokenization, OpenVocabularyPromptMode.FixedVocabulary);
            VisualModelProfile sourceVisual = baseProfile.VisualProfile;
            var visual = new VisualModelProfile(
                sourceVisual.ProfileId,
                sourceVisual.ModelId,
                sourceVisual.Task,
                sourceVisual.Version,
                sourceVisual.ModelFormat,
                sourceVisual.Input,
                sourceVisual.Outputs,
                sourceVisual.Labels,
                decoder,
                sourceVisual.RequiredCapabilities,
                sourceVisual.MinimumBackendVersion,
                sourceVisual.AuxiliaryInputs);
            var detectorProfile = new YoloDetectionProfile(
                baseProfile.Family,
                baseProfile.UpstreamRepository,
                baseProfile.UpstreamCommit,
                baseProfile.ExporterVersion,
                baseProfile.ArtifactSha256,
                baseProfile.Opset,
                baseProfile.DynamicShapes,
                baseProfile.PreprocessingVersion,
                baseProfile.PostprocessingVersion,
                baseProfile.Preprocessing,
                baseProfile.Output,
                visual);
            var artifacts = new[]
            {
                new OpenVocabularyArtifactContract(OpenVocabularyArtifactRole.Detector, modelId, "onnx", detectorSha, 51252911, 17, new[] { "images" }, new[] { "output0" }, UltralyticsRepository, upstreamCommit, "Ultralytics 8.2.2 ONNX exporter after YOLOWorld.set_classes", "AGPL-3.0", true),
                new OpenVocabularyArtifactContract(OpenVocabularyArtifactRole.SourceCheckpoint, new ModelId("external/yolov8s-worldv2-source"), "pytorch", sourceSha, 25920600, 0, null, null, UltralyticsRepository, "v8.1.0 / 808984c6cf32f4ac9cb28f52fd74d13b9d6ad6a0", "official Ultralytics asset", "AGPL-3.0", false, "Source checkpoint is not a DeploySharp native runtime graph."),
                new OpenVocabularyArtifactContract(OpenVocabularyArtifactRole.TextEncoder, new ModelId("external/clip-vit-b-32"), "pytorch", clipSha, 353976522, 0, null, null, "https://github.com/ultralytics/CLIP", "488e81a6711eea7346872b46ea928b367da8889d", "official CLIP load path used by Ultralytics set_classes", "AGPL-3.0", false, "Text encoding runs before fixed-vocabulary export and is not a detector runtime input."),
                new OpenVocabularyArtifactContract(OpenVocabularyArtifactRole.Tokenizer, new ModelId("external/clip-bpe-77"), "gzip", tokenizerSha, 1356917, 0, null, null, "https://github.com/ultralytics/CLIP", "488e81a6711eea7346872b46ea928b367da8889d", "official CLIP BPE tokenizer data", "AGPL-3.0", false, "Tokenizer data is audit evidence for the pre-export fixed vocabulary, not a runtime detector input.")
            };
            return new OpenVocabularyDetectionProfile(profileId, OpenVocabularyModelFamily.YoloWorld, "YOLO-Worldv2 / Ultralytics 8.2.2", OpenVocabularyPromptMode.FixedVocabulary, artifacts, vocabulary, tokenization, embedding, detectorProfile, 2, 77, 300, baseProfile.PreprocessingVersion, baseProfile.PostprocessingVersion);
        }

        /// <summary>Creates the official Grounding DINO Swin-T source contract and records the absent audited native export. / 创建官方 Grounding DINO Swin-T 源合同并记录缺失的已审计 native 导出。</summary>
        public static OpenVocabularyDetectionProfile CreateGroundingDinoSwinTBlocker()
        {
            const string blocker = "No local official Grounding DINO ONNX/IR artifact is configured, and the audited official repository does not provide a complete native ONNX export contract for BERT tokenization, text/image fusion, phrase decoding, and boxes.";
            var artifact = new OpenVocabularyArtifactContract(OpenVocabularyArtifactRole.SourceCheckpoint, new ModelId("external/groundingdino-swint-ogc"), "pytorch", "3b3ca2563c77c69f651d7bd133e97139c186df06231157a64c507099c52bc799", 693997677, 0, null, null, "https://github.com/IDEA-Research/GroundingDINO", "856dde20aee659246248e20734ef9ba5214f5e44", "official v0.1.0-alpha checkpoint; text encoder bert-base-uncased", "Apache-2.0", false, blocker);
            return new OpenVocabularyDetectionProfile("open-vocabulary.grounding-dino.swint-ogc.contract", OpenVocabularyModelFamily.GroundingDino, "v0.1.0-alpha", OpenVocabularyPromptMode.RuntimeText, new[] { artifact }, null, null, null, null, preprocessingVersion: "official-random-resize-800-max1333-imagenet-normalize", postprocessingVersion: "official-sigmoid-box-threshold-token-threshold-cxcywh-source-v1", blocker: blocker);
        }

        /// <summary>Creates the official YOLOE family contract and records absent local text, visual, and prompt-free native artifacts. / 创建官方 YOLOE 模型族合同并记录缺失的本机文本、视觉与无提示 native 工件。</summary>
        public static OpenVocabularyDetectionProfile CreateYoloEBlocker()
        {
            const string blocker = "No local official YOLOE text-prompt, visual-prompt, or prompt-free ONNX/IR artifact and matching serialized prompt identity is available for native admission.";
            var artifact = new OpenVocabularyArtifactContract(OpenVocabularyArtifactRole.SourceCheckpoint, new ModelId("external/yoloe-v8s-seg"), "pytorch", "ac2b90ed23011495a3e86d89caeb3432a15129cac8d849ba121293c8fc1e0536", 31135890, 0, null, null, "https://github.com/THU-MIG/yoloe", "40cd606cabdbe2b566d6f14a6b162c89206e9a1b", "official Hugging Face checkpoint yoloe-v8s-seg.pt", "AGPL-3.0", false, blocker);
            return new OpenVocabularyDetectionProfile("open-vocabulary.yoloe.v8s-seg.contract", OpenVocabularyModelFamily.YoloE, "official main 40cd606", OpenVocabularyPromptMode.RuntimeText, new[] { artifact }, null, null, null, null, preprocessingVersion: "official-yoloe-artifact-defined", postprocessingVersion: "official-reparameterized-yolo-detection-segmentation", blocker: blocker);
        }

        /// <summary>Creates the audited MMYOLO YOLO-Worldv2 source contract whose image-only graph lacks serialized vocabulary evidence. / 创建已审计的 MMYOLO YOLO-Worldv2 源合同；其仅图像图缺少序列化词汇证据。</summary>
        public static OpenVocabularyDetectionProfile CreateMmyoloYoloWorldV2Blocker()
        {
            const string blocker = "The local MMYOLO image-only graph exposes num_dets/boxes/scores/labels after reparameterization but carries no serialized vocabulary order or tokenizer/embedding identity; precise open-vocabulary labels cannot be admitted.";
            var artifact = new OpenVocabularyArtifactContract(OpenVocabularyArtifactRole.SourceCheckpoint, new ModelId("external/yolo-world-v2-s-mmyolo"), "pytorch", "55b943ea2643f716f012243a66e49f7f0b12c216a01230ccc9c99e4e128da1a6", 305052941, 0, null, null, "https://github.com/AILab-CVC/YOLO-World", "4f70adbaacf5685bd9ec5bea85f1f91057f6fc0b", "official checkpoint; deploy/export_onnx.py reparameterizes custom text before export", "GPL-3.0", false, blocker);
            return new OpenVocabularyDetectionProfile("open-vocabulary.yolo-world-v2.mmyolo.contract", OpenVocabularyModelFamily.YoloWorld, "YOLO-Worldv2 official master", OpenVocabularyPromptMode.FixedVocabulary, new[] { artifact }, null, null, null, null, preprocessingVersion: "mmyolo-yolo-letterbox", postprocessingVersion: "mmyolo-exported-nms", blocker: blocker);
        }
    }
}
