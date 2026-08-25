# Native multimodal dialogue / 原生多模态对话

Stage 26 adds an artifact-bound native multimodal Profile/Bundle and stateful three-session pipeline. The executable representative is official `llava-hf/llava-onevision-qwen2-0.5b-ov-hf`; Qwen2.5-VL 3B and Phi-3.5 Vision remain precise External blockers. No Python service, remote inference, fixed response, substitute vision algorithm, positional tensor binding, or TensorRT path is used. / 阶段 26 新增工件绑定的原生多模态 Profile/Bundle 与三 Session 有状态流水线。可执行代表为官方 LLaVA OneVision Qwen2 0.5B；Qwen2.5-VL 3B 与 Phi-3.5 Vision 保持精确 External blocker。不使用 Python 服务、远程推理、固定回答、替代视觉算法、位置绑定或 TensorRT。

## Quick start / 快速开始

```csharp
NativeMultimodalProfile profile = NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
var tokenizer = new Qwen2NativeMultimodalTokenizer(modelRoot, profile.Tokenizer);
BackendId backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new NativeMultimodalArtifactBundle(profile, new[]
{
    new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, visionPath, backend)),
    new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.TokenEmbedding, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.TokenEmbedding, embeddingPath, backend)),
    new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoderPath, backend))
});
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new NativeMultimodalSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"), imageNewlinePath);
using NativeMultimodalPreparedImage image = new OpenCvNativeMultimodalInputFactory().CreateFromFile(imagePath, profile);
NativeMultimodalImageState state = session.SetImage(image);
NativeMultimodalResult vqa = session.Generate(GenerativeVisionLanguageRequest.Question("What languages are visible on this clothing label?"), tokenizer);
NativeMultimodalResult caption = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
session.Clear();
```

The caller supplies every graph, checkpoint, tokenizer, image-newline sidecar, image, and native runtime. DeploySharp NuGet packages contain managed DLL/XML only. / 调用方提供全部 Graph、Checkpoint、Tokenizer、Image-newline、图片与 Native Runtime；DeploySharp NuGet 仅包含 Managed DLL/XML。

## Exact model and subgraph matrix / 精确模型与子图矩阵

| Family/version / 模型族版本 | Status / 状态 | Exact contract / 精确合同 |
| --- | --- | --- |
| LLaVA OneVision Qwen2 0.5B, revision `74dd0bf...` | External executable, single image / External 可执行、单图 | Vision+projector FP32, token embedding INT8, merged empty/non-empty-past decoder INT8 |
| Qwen2.5-VL 3B Instruct, revision `66285546...` | External blocker | Official configuration/license only; no complete audited Vision RoPE/projector/embedding/Prefill/KV ONNX+OpenVINO bundle |
| Phi-3.5 Vision Instruct, revision `12b77fb4...` | External blocker | Official configuration/license only; no complete audited HD processor/projector/embedding/Prefill/KV ONNX+OpenVINO bundle |

| Artifact / 工件 | Exact named ports / 精确具名端口 | Shape/type / Shape 与类型 |
| --- | --- | --- |
| Vision + projector | `pixel_values` -> `image_features` | `float32 [crops,3,384,384]` -> `float32 [crops,729,896]`, opset 14 |
| Token embedding | `input_ids` -> `inputs_embeds` | `int64 [1,S]` -> `float32 [1,S,896]`, opset 13 |
| Prefill/KV decoder | `attention_mask`, `position_ids`, 24 ordered `past_key_values.{L}.{key,value}`, `inputs_embeds` -> `logits`, 24 ordered `present.{L}.{key,value}` | logits `[1,S,152000]`; KV `[1,2,past,64]`, opset 14 |

Empty past means Prefill; non-empty past means Decode. There is no hidden `use_cache_branch` input. Ports are checked by exact name, order, type, rank, fixed dimensions, capacity, and artifact SHA before use. / 空 Past 表示 Prefill，非空 Past 表示 Decode；不存在隐藏的 `use_cache_branch`。使用前按名称、顺序、类型、Rank、固定维、容量与工件 SHA 严格校验。

## Processor, tokenizer, and image tokens / Processor、Tokenizer 与图像 Token

The audited processor decodes once, converts BGR/gray/alpha to RGB, chooses one official 384-pixel grid from 1x1 through 6x6, creates a base crop plus high-resolution crops with Pillow-compatible bicubic resize and centered raw-zero padding, normalizes to `[-1,1]`, runs the official projector, unpads spatial features, applies the official anyres-max-9 budget, inserts the verified 896-float image-newline vector per row, and returns the exact image-token count. The 350x350 gate selects 1x1, submits two crops, and packs 1485 tokens. / 已审核 Processor 单次解码，完成 RGB 转换、官方网格选择、Base/高分辨率 Crop、Pillow-compatible Bicubic、居中零值 Padding、归一化、Projector、Unpad、Anyres Max-9 与逐行 Image-newline；350x350 门控选择 1x1、提交两个 Crop、打包 1485 个 Token。

`Qwen2NativeMultimodalTokenizer` uses `Microsoft.ML.Tokenizers` ByteLevel BPE after verifying `tokenizer.json`, `vocab.json`, and `merges.txt`. The exact single-image chat template, image sentinel 151646, vocabulary 152000, EOS 151645, context 6144, and default Caption instruction `Describe this image briefly.` are Profile identity. An empty common Caption request therefore remains compatible while explicit text can override it. / Managed Tokenizer 校验三个官方资产后使用 ByteLevel BPE；Chat Template、图像 Sentinel、词表、EOS、Context 与默认 Caption 指令均进入 Profile Identity。

Only one image is supported. Multi-image ordering, video, document tiling beyond this exact anyres contract, region grounding, Beam, sampling, tools, JSON mode, and OCR grounding are not claimed. / 当前只支持单图；不宣称多图、视频、额外文档切块、区域 Grounding、Beam、Sampling、Tool、JSON Mode 或 OCR Grounding。

## State and ownership / 状态与所有权

`NativeMultimodalSession` owns three backend sessions and one cached packed image state bound to Profile ID, ordered artifact hashes, processor identity, image-newline SHA, source image SHA/size, selected grid, and feature summary. Registry, tokenizer, external files, and prepared input remain caller-owned. Every Generate creates transient owned KV tensors bound to prompt/image/layout/generation identity; mutable KV is released after the request and only an immutable `NativeMultimodalKvStateSummary` is published. Results reuse the common owned `GenerativeVisionLanguageResult` and `GenerationResult`. / Session 拥有三条 Backend Session 与完整 Identity 绑定的单图状态；Registry、Tokenizer、外部文件和 Prepared Input 由调用方拥有。每次 Generate 创建临时自有 KV，结束后只发布不可变摘要；结果复用通用生成 DTO。

The session is single-writer. Concurrent set-image/generate/clear fails deterministically. Cancellation, timeout, or callback failure publishes neither partial image state nor KV summary. Dispose cancels active work, waits for unwind, clears state, and disposes all child sessions exactly once. / Session 为 Single-writer；并发稳定失败。取消、超时或 Callback 异常不发布部分状态；Dispose 取消活动操作、等待回卷并 Exactly-once 释放子 Session。

## Official and backend fidelity / 官方与后端保真

The authorized 350x350 JPEG has SHA256 `957a9cc...561b`. Official PyTorch/Transformers 4.57.3 produced VQA IDs `[785,17438,2383,374,304,8453,13,151645]` and `The clothing label is in Chinese.`. Independent Python ORT 1.23.2 reproduced those IDs. DeploySharp uses the current package ORT 1.28.0 and OpenVINO 2026.2.1; numeric-kernel drift is retained rather than hidden:

- ORT 1.28.0 with official pixels: 16 tokens, MaxTokens, `The clothing label in the image displays text in both English and Chinese. The English`.
- OpenVINO 2026.2.1 with official pixels: `[785,17438,2383,374,304,6364,323,8453,13,151645]`, EOS, `The clothing label is in English and Chinese.`.
- OpenCV JPEG + ORT 1.28.0: the same 10-token EOS result as OpenVINO; normalized pixel max/mean absolute difference from Pillow was `0.0078431815 / 2.2406036e-7`.

The official comparison is therefore complete but not identical across current runtimes. The manifest stays `External`, `AlgorithmVerified:false`, and records each runtime-specific packed-feature SHA. / 官方对比已完成，但当前 Runtime 间不完全一致；Manifest 保持 External、非 AlgorithmVerified，并记录各 Runtime 的 Packed Feature SHA。

One observed run, not a benchmark: OpenCV preprocess `177.380 ms`; ORT Vision+pack/Prefill/Decode `4923.133/4341.870/884.568 ms`; OpenVINO Vision+pack/Prefill/Decode `5037.460/13889.351/5866.725 ms`. Do not interpret these as P50/P95, throughput, memory, quality, or cross-machine results. / 以上仅单次诊断 timing，不代表分位数、吞吐、内存、质量或跨机器结论。

## Compatibility and diagnostics / 兼容性与诊断

| Layer / 层 | Declared target / 声明目标 | Application responsibility / 应用责任 |
| --- | --- | --- |
| Visual contracts/session | all package-declared TFMs | managed API; Qwen tokenizer implementation is enabled on net8/net9/net10, older TFMs report capability unavailable |
| Visual.OpenCV | package-declared TFMs; Windows x64 verified | explicitly install matching `JYPPX.OpenCV.runtime.win-x64` |
| ORT backend | package-declared TFMs; Windows x64 CPU verified | explicitly install `Microsoft.ML.OnnxRuntime` 1.28.0 |
| OpenVINO backend | package-declared TFMs; Windows x64 CPU verified | explicitly install matching OpenVINO runtime |
| Models/tokenizer/golden | external, no RID inference | caller-owned or ModelFactory only after publication authorization |

Stable native multimodal errors are `DS-VISUAL-4901..4908`: invalid contract/port, identity mismatch, invalid state, invalid generation/KV/logits, concurrency, capacity, tokenizer, and unavailable capability. Existing cancellation, timeout, native-load, inference, and disposed errors remain unchanged. / 稳定错误覆盖合同、Identity、状态、生成/KV、并发、容量、Tokenizer 与能力不可用；取消、超时、Native、推理与 Dispose 复用既有错误。

See [the acquisition article](model-acquisition-native-multimodal.md), [all-stage inventory](../history/development-model-inventory.md), and `eng/models/native-multimodal/native-multimodal-family-support.json`. TensorRT remains unimplemented. / 供应链、统一仓库与发布状态见获取文章、全阶段清单和结构化支持文件；TensorRT 仍未实现。
