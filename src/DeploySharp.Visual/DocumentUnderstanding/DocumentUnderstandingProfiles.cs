using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Creates audited LayoutLMv3, Donut, and Pix2Struct document profiles. / 创建已审计的 LayoutLMv3、Donut 与 Pix2Struct 文档 Profile。</summary>
    public static class DocumentUnderstandingProfiles
    {
        private const string DonutRevision = "8003d433113256b4ce3a0f5bf604b29ff78a7451";
        private const string DonutSource = "https://huggingface.co/naver-clova-ix/donut-base-finetuned-cord-v2";

        /// <summary>Creates the executable official Donut CORD-v2 ONNX profile for ORT or OpenVINO ONNX import. / 创建供 ORT 或 OpenVINO ONNX Import 执行的官方 Donut CORD-v2 ONNX Profile。</summary>
        public static DocumentUnderstandingProfile CreateDonutCordV2Onnx()
        {
            return CreateDonut(
                "document.donut.cord-v2.onnx",
                "onnx",
                new[]
                {
                    Artifact(DocumentArtifactRole.DocumentEncoder, "external/document/donut-cord-v2/encoder/onnx", "onnx", "cb165bb59193c73c9097b1a306eb55f2e139e9ba0306755b405777c43cd51cbc", 311234390, EncoderInputs(), EncoderOutputs()),
                    Artifact(DocumentArtifactRole.DecoderPrefill, "external/document/donut-cord-v2/prefill/onnx", "onnx", "082b4c414f70be269a5c191c12373e3a19f1e5be10f8d01d6b64dd8c0a8259a3", 743754196, PrefillInputs(), PrefillOutputs()),
                    Artifact(DocumentArtifactRole.DecoderWithPast, "external/document/donut-cord-v2/decode/onnx", "onnx", "e5629e9e13b19e494652d3f1f57136a093593e03e2fca58329e61e06b8fa323e", 710151560, DecodeInputs(), DecodeOutputs())
                });
        }

        /// <summary>Creates the executable FP32 OpenVINO XML/BIN profile converted from the same audited ONNX graphs. / 创建由同一已审计 ONNX 图转换的可执行 FP32 OpenVINO XML/BIN Profile。</summary>
        public static DocumentUnderstandingProfile CreateDonutCordV2OpenVino()
        {
            return CreateDonut(
                "document.donut.cord-v2.openvino-fp32",
                "openvino-ir",
                new[]
                {
                    Artifact(DocumentArtifactRole.DocumentEncoder, "external/document/donut-cord-v2/encoder/openvino", "openvino-ir", "bbb77bdec521335c08ae3f06b95db18e4ac5e823ca406885ba300a57078f03dd", 8659621, EncoderInputs(), EncoderOutputs(), "OpenVINO 2026.2.1 read_model/save_model FP32; BIN 3758443b8ed7b354d44f2eefb1d213cc676ecec1ba51beecf811207799fa43c4"),
                    Artifact(DocumentArtifactRole.DecoderPrefill, "external/document/donut-cord-v2/prefill/openvino", "openvino-ir", "bb2e2923d8809a526bfe824d9116de5829dbf1d8ca6c9a9d771030004a5d1f94", 516304, PrefillInputs(), PrefillOutputs(), "OpenVINO 2026.2.1 read_model/save_model FP32; BIN a5ceca96d677e61a89bb12d505095f899fec61d1ed66791b1dc026817c2cb989"),
                    Artifact(DocumentArtifactRole.DecoderWithPast, "external/document/donut-cord-v2/decode/openvino", "openvino-ir", "07381dad8ba02fea94c24ec2aeb1cdc41a5cfbef24b8cf7b76c1f938ab7c55dc", 462661, DecodeInputs(), DecodeOutputs(), "OpenVINO 2026.2.1 read_model/save_model FP32; BIN b49461b78ab755de2827c4a853f5b25c05153688eb9bf96b3b3ed473e57fae45" )
                });
        }

        /// <summary>Creates the official LayoutLMv3 base source/processor contract with an explicit task-head/export blocker. / 创建官方 LayoutLMv3 Base 源/Processor 合同及明确 Task Head/导出 Blocker。</summary>
        public static DocumentUnderstandingProfile CreateLayoutLmV3BaseContract()
        {
            var processor = new DocumentProcessorContract("microsoft.layoutlmv3-base.processor", "35fa599126fa2221fefecbc5c5f9e6d095122435d00d5313cb8f4d81d8e80220", DocumentProcessorMode.LayoutLmV3ImageAndLayout, new VisualSize(224, 224), new[] { .5f, .5f, .5f }, new[] { .5f, .5f, .5f }, "Pillow bilinear resize", 32, 64 * 1024 * 1024, 512, 196, 16);
            var tokenizer = new DocumentTokenizerContract("microsoft.layoutlmv3-base.roberta-bpe", "06b4d46c8e752d410213d9548eb27a54db70fda0319b6271fb8d59dead5e1cab", "1ce1664773c50f3e0cc8842619a93edc4624525b728b188a9e0be33b7726adc5", "1ce1664773c50f3e0cc8842619a93edc4624525b728b188a9e0be33b7726adc5", "LayoutLMv3Tokenizer/RobertaTokenizerFast", "layoutlmv3.words-boxes-special-v1", "<s>", 50265, 0, 1, 2, 3, 514);
            var schema = new DocumentSchemaContract("layoutlmv3.base.no-task-head", "2b044b1aeff1cfc9fb2fb0bf5259debff2e4b771f89482deaa0119ee452c22f7", "bio-token-labels-v1", 8, 514, 65536);
            return new DocumentUnderstandingProfile("document.layoutlmv3.base.contract", DocumentUnderstandingFamily.LayoutLmV3, "layoutlmv3-base", "cfbbbff0762e6aab37086fdd4739ad14fe7d5db4", DocumentOcrOwnership.Caller, processor, tokenizer, schema, null, new[] { DocumentUnderstandingTask.LayoutClassification, DocumentUnderstandingTask.EntityExtraction }, new DocumentArtifactContract[0], false, "The official Microsoft base checkpoint has no task head; the previously named Microsoft FUNSD checkpoint is no longer available from the official namespace on 2026-08-09. No third-party fine-tune is admitted as an official executable representative.");
        }

        /// <summary>Creates the official Pix2Struct DocVQA processor/tokenizer/patch contract with a native export blocker. / 创建官方 Pix2Struct DocVQA Processor/Tokenizer/Patch 合同及 Native Export Blocker。</summary>
        public static DocumentUnderstandingProfile CreatePix2StructDocVqaContract()
        {
            var processor = new DocumentProcessorContract("google.pix2struct-docvqa-base.processor", "c84e4eebc84171d6069533d9f0147ec7b4afd02ab78697cb5c30f9419ef7dc45", DocumentProcessorMode.Pix2StructFlattenedPatches, new VisualSize(4096, 768), new[] { .5f, .5f, .5f }, new[] { .5f, .5f, .5f }, "official 16x16 patch scaling plus row/column IDs", 1, 64 * 1024 * 1024, 0, 2048, 16);
            var tokenizer = new DocumentTokenizerContract("google.pix2struct-docvqa-base.t5", "7fd650335add59bed55a432186ca0437a09e185c2d241faab468a538fe6bcf94", "0af109b23840545ef2c286073f4373959badba1faa73c8557881d5126f6287c9", "0af109b23840545ef2c286073f4373959badba1faa73c8557881d5126f6287c9", "T5Tokenizer", "pix2struct-docvqa-question-v1", "question: {0}", 50244, 0, 0, 1, 2, 4096);
            var schema = new DocumentSchemaContract("pix2struct.docvqa.text.v1", "8d39973772a4218b555e30daecabdd5ea11aa1345dd711ff7f88fa90750b464f", "plain-text-answer-v1", 4, 16, 65536);
            var kv = new DocumentKvCacheContract("pix2struct.t5.no-cache-official-config", 12, 12, 64, 2048, 4095);
            return new DocumentUnderstandingProfile("document.pix2struct.docvqa-base.contract", DocumentUnderstandingFamily.Pix2Struct, "pix2struct-docvqa-base", "63f6b3de436e39f75c7a486881a9c2c14a7f4e89", DocumentOcrOwnership.NoneOcrFree, processor, tokenizer, schema, kv, new[] { DocumentUnderstandingTask.DocumentQuestionAnswering }, new DocumentArtifactContract[0], false, "The official config sets text_config.use_cache=false and no complete official or traceable dynamic flattened-patch Encoder/Prefill/Decode ONNX plus OpenVINO IR bundle was available. The checkpoint weights were not downloaded merely to invent a KV contract.");
        }

        private static DocumentUnderstandingProfile CreateDonut(string profileId, string format, IEnumerable<DocumentArtifactContract> artifacts)
        {
            var processor = new DocumentProcessorContract("naver.donut.cord-v2.processor", "46a79191272663118d1d5d6f2eaf4c497bce40cc336bd55724daac33a34b250b", DocumentProcessorMode.DonutThumbnailPad, new VisualSize(960, 1280), new[] { .5f, .5f, .5f }, new[] { .5f, .5f, .5f }, "Pillow bilinear thumbnail plus centered zero pad", 1, 64 * 1024 * 1024, 0, 1200, 4);
            var tokenizer = new DocumentTokenizerContract("naver.donut.cord-v2.xlm-roberta", "cb9e3dce4c326195d08fc3dd0f7e2eee1da8595c847bf4c1a9c78b7a82d47e2d", "756fd46f7c829153e68d75ebac3e59fda91244f11c85d3498fe91b20dc5cdf59", "f51dd68d1565c8fb24de0a93f0a98aaed273ff368908069219b74b091bebcbc5", "XLMRobertaTokenizer/SentencePieceUnigram", "donut.cord-v2.task-prompt.v1", "<s_cord-v2>", 57580, 0, 1, 2, 3, 768);
            var schema = new DocumentSchemaContract("cord-v2.donut-tags.v1", "11eef36f495e1c3911961469a23d71b6f6edbe377e420990227514b5c8777733", "donut-tags-v1", 16, 256, 65536);
            var kv = new DocumentKvCacheContract("donut.mbart.4x16x64.cross1200.v1", 4, 16, 64, 1200, 767);
            return new DocumentUnderstandingProfile(profileId, DocumentUnderstandingFamily.Donut, "donut-base-finetuned-cord-v2-opset17-" + format, DonutRevision, DocumentOcrOwnership.NoneOcrFree, processor, tokenizer, schema, kv, new[] { DocumentUnderstandingTask.StructuredExtraction }, artifacts, true);
        }

        private static DocumentArtifactContract Artifact(DocumentArtifactRole role, string modelId, string format, string sha, long size, IEnumerable<GenerativeVisionLanguageTensorContract> inputs, IEnumerable<GenerativeVisionLanguageTensorContract> outputs, string? exporter = null)
            => new DocumentArtifactContract(role, new ModelId(modelId), format, sha, size, 17, inputs, outputs, DonutRevision, exporter ?? "Optimum ONNX 0.1.0 / Transformers 4.57.3 torch.onnx", "MIT", DonutSource);

        private static IEnumerable<GenerativeVisionLanguageTensorContract> EncoderInputs() { yield return Tensor("pixel_values", TensorElementType.Float32, 3_686_400, -1, 3, -1, -1); }
        private static IEnumerable<GenerativeVisionLanguageTensorContract> EncoderOutputs() { yield return Tensor("last_hidden_state", TensorElementType.Float32, 1_228_800, -1, 1200, 1024); }
        private static IEnumerable<GenerativeVisionLanguageTensorContract> PrefillInputs() { yield return Tensor("input_ids", TensorElementType.Int64, 768, -1, -1); yield return Tensor("encoder_hidden_states", TensorElementType.Float32, 1_228_800, -1, -1, 1024); }
        private static IEnumerable<GenerativeVisionLanguageTensorContract> PrefillOutputs()
        {
            yield return Tensor("logits", TensorElementType.Float32, 44_221_440, -1, -1, 57580);
            for (int layer = 0; layer < 4; layer++)
            {
                yield return Tensor("present." + layer + ".decoder.key", TensorElementType.Float32, 786432, -1, 16, -1, 64);
                yield return Tensor("present." + layer + ".decoder.value", TensorElementType.Float32, 786432, -1, 16, -1, 64);
                yield return Tensor("present." + layer + ".encoder.key", TensorElementType.Float32, 1_228_800, -1, 16, -1, 64);
                yield return Tensor("present." + layer + ".encoder.value", TensorElementType.Float32, 1_228_800, -1, 16, -1, 64);
            }
        }
        private static IEnumerable<GenerativeVisionLanguageTensorContract> DecodeInputs()
        {
            yield return Tensor("input_ids", TensorElementType.Int64, 1, -1, -1);
            for (int layer = 0; layer < 4; layer++)
            {
                yield return Tensor("past_key_values." + layer + ".decoder.key", TensorElementType.Float32, 786432, -1, 16, -1, 64);
                yield return Tensor("past_key_values." + layer + ".decoder.value", TensorElementType.Float32, 786432, -1, 16, -1, 64);
                yield return Tensor("past_key_values." + layer + ".encoder.key", TensorElementType.Float32, 1_228_800, -1, 16, -1, 64);
                yield return Tensor("past_key_values." + layer + ".encoder.value", TensorElementType.Float32, 1_228_800, -1, 16, -1, 64);
            }
        }
        private static IEnumerable<GenerativeVisionLanguageTensorContract> DecodeOutputs()
        {
            yield return Tensor("logits", TensorElementType.Float32, 57580, -1, 1, 57580);
            for (int layer = 0; layer < 4; layer++)
            {
                yield return Tensor("present." + layer + ".decoder.key", TensorElementType.Float32, 786432, -1, 16, -1, 64);
                yield return Tensor("present." + layer + ".decoder.value", TensorElementType.Float32, 786432, -1, 16, -1, 64);
            }
        }
        private static GenerativeVisionLanguageTensorContract Tensor(string name, TensorElementType type, long maximumElements, params long[] shape) => new GenerativeVisionLanguageTensorContract(name, type, new TensorShape(shape), maximumElements);
    }
}
