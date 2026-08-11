using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Creates audited native multimodal profiles without inferring ports from filenames or ranks. / 创建已审计原生多模态 Profile，不从文件名或 Rank 推断端口。</summary>
    public static class NativeMultimodalProfiles
    {
        private const string Revision = "74dd0bf867a4cda7950c17663794267c60cf4b40";
        private const string ModelRoot = "https://huggingface.co/llava-hf/llava-onevision-qwen2-0.5b-ov-hf/resolve/" + Revision + "/";
        private const string License = "Apache-2.0";

        /// <summary>Creates the official LLaVA-OneVision Qwen2 0.5B mixed FP32/INT8 ONNX bundle with named 24-layer KV cache. / 创建官方 LLaVA-OneVision Qwen2 0.5B 混合 FP32/INT8 ONNX Bundle，并绑定具名 24 层 KV Cache。</summary>
        /// <remarks>The Vision graph includes the official multimodal projector; anyres packing and image-newline insertion remain typed managed operations. / Vision 图包含官方多模态 Projector；Anyres 打包与 Image-newline 插入由 Typed Managed 操作负责。</remarks>
        public static NativeMultimodalProfile CreateLlavaOneVisionQwen2HalfB(string profileId = "native-vlm.llava-onevision-qwen2-0.5b-ov-hf.onnx-mixed")
        {
            var vision = new GenerativeVisionLanguageArtifactContract(
                GenerativeVisionLanguageArtifactRole.VisionEncoder,
                new ModelId("external/native-vlm/llava-onevision-qwen2-0.5b/vision-projector-fp32"),
                "onnx",
                "06cf8f4eefdea6cb8f095724e37da8fa0358820a3506e1c85915d5d2bdadab43",
                1598932026,
                14,
                new[] { Tensor("pixel_values", TensorElementType.Float32, 17_000_000, -1, 3, 384, 384) },
                new[] { Tensor("image_features", TensorElementType.Float32, 25_000_000, -1, 729, 896) },
                Revision,
                "official llava-hf Transformers.js ONNX publication; FP32 Vision Tower plus multimodal Projector",
                License,
                ModelRoot + "onnx/vision_encoder.onnx");
            var embedding = new GenerativeVisionLanguageArtifactContract(
                GenerativeVisionLanguageArtifactRole.TokenEmbedding,
                new ModelId("external/native-vlm/llava-onevision-qwen2-0.5b/token-embedding-int8"),
                "onnx",
                "4b4dec69949d75a775d871c5e1dc3db6bd4fd6e8ceffb3deafe64e8f16a8323d",
                136192544,
                13,
                new[] { Tensor("input_ids", TensorElementType.Int64, 8192, -1, -1) },
                new[] { Tensor("inputs_embeds", TensorElementType.Float32, 8_000_000, -1, -1, 896) },
                Revision,
                "official llava-hf Transformers.js ONNX dynamic INT8 Token Embedding",
                License,
                ModelRoot + "onnx/embed_tokens_int8.onnx");
            var kv = new NativeMultimodalKvCacheContract("qwen2-24l-2kvh-d64-past-present-fp32", 24, 2, 64, 6144);
            var decoderInputs = new List<GenerativeVisionLanguageTensorContract>
            {
                Tensor("attention_mask", TensorElementType.Int64, 8192, -1, -1),
                Tensor("position_ids", TensorElementType.Int64, 8192, -1, -1)
            };
            var decoderOutputs = new List<GenerativeVisionLanguageTensorContract>
            {
                Tensor("logits", TensorElementType.Float32, 950_000_000, -1, -1, 152000)
            };
            for (int layer = 0; layer < kv.LayerCount; layer++)
            {
                decoderInputs.Add(Tensor(kv.PastKey(layer), TensorElementType.Float32, 2_000_000, -1, 2, -1, 64));
                decoderInputs.Add(Tensor(kv.PastValue(layer), TensorElementType.Float32, 2_000_000, -1, 2, -1, 64));
                decoderOutputs.Add(Tensor(kv.PresentKey(layer), TensorElementType.Float32, 2_000_000, -1, 2, -1, 64));
                decoderOutputs.Add(Tensor(kv.PresentValue(layer), TensorElementType.Float32, 2_000_000, -1, 2, -1, 64));
            }
            decoderInputs.Add(Tensor("inputs_embeds", TensorElementType.Float32, 8_000_000, -1, -1, 896));
            var decoder = new GenerativeVisionLanguageArtifactContract(
                GenerativeVisionLanguageArtifactRole.LanguageDecoder,
                new ModelId("external/native-vlm/llava-onevision-qwen2-0.5b/decoder-merged-int8"),
                "onnx",
                "cc674946412447fa76df18686c32541b1388c0fa62cbf53c36dccd1a90649c3f",
                512154211,
                14,
                decoderInputs,
                decoderOutputs,
                Revision,
                "official llava-hf Transformers.js ONNX merged INT8 Qwen2 Prefill/Decode with empty/non-empty past",
                License,
                ModelRoot + "onnx/decoder_model_merged_int8.onnx");

            var grids = new List<NativeMultimodalImageGrid>();
            for (int rows = 1; rows <= 6; rows++) for (int columns = 1; columns <= 6; columns++) grids.Add(new NativeMultimodalImageGrid(rows, columns));
            var processor = new NativeMultimodalProcessorContract(
                "llava-onevision-slow-pillow-bicubic-anyres-max9-v1",
                "3644c108b9f0fa53e62ff422a9be6639642f0e64dab4a71f961c7911d4386384",
                384,
                14,
                896,
                36,
                9,
                "2902ee144440d5a54e9c773e8cc7700297105d9687691386dbfade15cce5e160",
                grids,
                "PIL.Image.Resampling.BICUBIC; RGB; rescale 1/255; mean/std 0.5; centered zero-byte anyres padding");
            const string regex = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";
            const string template = "<|im_start|>user <image>\n{0}<|im_end|><|im_start|>assistant\n";
            var tokenizer = new NativeMultimodalTokenizerContract(
                "qwen2-bytelevel-bpe-llava-onevision",
                "3c0ce3213b50ff38d8aa1e91136a2d2cb142a3f569246170872e439cb2a29d15",
                "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910",
                "8831e4f1a044471340f7c0a83d7bd71306a5b867e95fd870f74d0c5308a904d5",
                regex,
                template,
                152000,
                151646,
                151643,
                151644,
                151645,
                6144);
            var generation = new GenerativeVisionLanguageGenerationContract(
                "llava-onevision-greedy-kv-max16-v1",
                "89dc53229f50b59570b6852056dafeac8116c458f1a748bff491b6d4d24d3b51",
                GenerativeVisionLanguageGenerationMode.Greedy,
                GenerativeVisionLanguageCacheMode.PastPresent,
                1,
                16,
                lengthMode: GenerativeVisionLanguageLengthMode.NewTokens);
            return new NativeMultimodalProfile(
                profileId,
                NativeMultimodalFamily.Llava,
                "llava-onevision-qwen2-0.5b-ov-hf",
                Revision,
                processor,
                tokenizer,
                kv,
                generation,
                new[] { GenerativeVisionLanguageTask.ImageCaptioning, GenerativeVisionLanguageTask.VisualQuestionAnswering },
                new[] { vision, embedding, decoder },
                true);
        }

        private static GenerativeVisionLanguageTensorContract Tensor(string name, TensorElementType type, long maximumElements, params long[] shape) => new GenerativeVisionLanguageTensorContract(name, type, new TensorShape(shape), maximumElements);
    }
}
