# BLIP-family generation / BLIP 模型族生成

Stage 25 adds an artifact-bound BLIP/BLIP-2/InstructBLIP contract. One official BLIP base Caption two-graph bundle is executable; BLIP VQA, BLIP-2 OPT-2.7B, and InstructBLIP Flan-T5-XL are precise External blockers. No Python service, remote API, fixed text, substitute model, or positional tensor binding is used. / 阶段 25 新增工件绑定的 BLIP/BLIP-2/InstructBLIP 合同。一个官方 BLIP Base Caption 双图 Bundle 可执行；其余三条路径为精确 External Blocker，不使用 Python 服务、远程 API、固定文本、替代模型或位置绑定。

## Quick start / 快速开始

```csharp
GenerativeVisionLanguageProfile profile = GenerativeVisionLanguageProfiles.CreateBlipCaptionBase();
var tokenizer = new BlipBertTokenizer(vocabularyPath, profile.Tokenizer);
var backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new GenerativeVisionLanguageArtifactBundle(profile, new[]
{
    new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, visionPath, backend)),
    new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoderPath, backend))
});
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new GenerativeVisionLanguageSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
using PreparedVisualInput input = new OpenCvGenerativeVisionLanguageInputFactory().CreateFromFile(imagePath, profile);
GenerativeVisionLanguageImageState state = session.SetImage(input);
GenerativeVisionLanguageResult result = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
session.ClearImage();
```

The application supplies all model, vocabulary, image, backend-native, and OpenCV-native files. DeploySharp packages contain managed DLL/XML only. / 应用显式提供模型、词表、图片和 Native Runtime；DeploySharp 包仅包含 Managed DLL/XML。

## Exact executable contract / 精确可执行合同

| Component / 组件 | BLIP base Caption contract / 合同 |
| --- | --- |
| Processor | RGB, fixed 384x384, Pillow-compatible antialiased bicubic, BLIP mean/std, one decode |
| Vision | `pixel_values [B,3,384,384] float32` -> `encoder_hidden_states [B,577,768] float32` |
| Tokenizer | uncased BERT WordPiece plus `[DEC]`/`[ENC]`; BOS 30522, EOS 102, PAD 0, vocabulary 30524 |
| Prompt | exact template `a picture of `; prefix `[30522,1037,3861,1997]` |
| Decoder | exact named `input_ids`, `attention_mask`, `encoder_hidden_states`, `encoder_attention_mask` -> `logits [B,S,30524]` |
| Generation | greedy, one beam, temperature/top-p/repetition penalty 1, total length 5..20, complete prefix each step, no KV cache |
| Output | owned common `GenerationResult`, original request, normalized prompt, token IDs/scores/log-probabilities, finish reason, timing, complete identity |

The ONNX Vision output metadata keeps symbolic feature dimensions; the immutable Profile narrows them to `[577,768]`. Backend metadata may remain dynamic, but every concrete runtime output is strictly validated before it becomes cached state or decoder input. / Vision ONNX 元数据保留符号维，Profile 将其收紧为 `[577,768]`；元数据可动态，但真实输出进入缓存和 Decoder 前必须严格校验。

## Ownership, state, and concurrency / 所有权、状态与并发

`GenerativeVisionLanguageSession` owns the vision and decoder backend sessions. It caches one managed encoder state bound to Profile ID, ordered artifact hashes, processor SHA, exact encoded-source SHA, source/model sizes, and state value summary. Registry, tokenizer, and borrowed input remain caller-owned. Results and streamed chunks are immutable defensive data. / Session 拥有两条 Backend Session；单个图像状态绑定 Profile、全部工件、Processor、源文件 SHA 与尺寸。Registry、Tokenizer 和借用输入由调用方拥有；结果与流式 Chunk 为防御性数据。

Set-image, generation, clear, and disposal are serialized. A concurrent operation fails with `DS-VISUAL-4805`; cancellation/timeout does not publish partial image/KV state; callback errors abort generation; clear removes state; dispose cancels active work, waits for unwind, clears state, then disposes both sessions once. This BLIP graph has no KV cache: every decoder step receives the full prefix. / 状态操作串行化；并发、取消、回调、clear 与 dispose 均有稳定语义。本 BLIP 图无 KV Cache，每步提交完整 Prefix。

## Backend fidelity / 后端保真

On the authorized `bus.jpg` (SHA256 `33b198...b69c`), the official predictor, independent PyTorch full-prefix loop, DeploySharp ORT CPU, DeploySharp OpenVINO CPU, and OpenCV source-image path produced the same 11 completion token IDs, EOS decision, and `a group of people standing in front of a bus`. ORT/PyTorch encoder max/mean absolute errors were `4.4816732e-4 / 2.7750696e-6`; OpenVINO/PyTorch were `7.7462196e-4 / 1.1715268e-6`. OpenCV/Pillow normalized pixel max/mean errors were `0.015007794 / 2.6076993e-7`. Selected token logits were checked per step. / 同一获授权图片的官方 Predictor、PyTorch 循环、ORT、OpenVINO 与 OpenCV 路径得到相同 Token、EOS 和文本，并按上述误差核验 Encoder、Pixel 与逐步 Logit。

One observed run, not a benchmark: ORT encoder/decoder `402.910/944.229 ms`; OpenVINO encoder/decoder `331.393/988.254 ms`; OpenCV preprocess/encoder/decoder `328.936/401.975/885.117 ms`. Official Python load/preprocess/predictor/encoder/full-prefix decode observations were `4415.028/17.527/1713.023/502.371/1438.977 ms`. Do not interpret these as P50/P95, throughput, memory, quality, or cross-machine comparisons. / 以上仅单次观测，不代表分位数、吞吐、内存、质量或跨机器结论。

## Blockers / 阻断

- BLIP VQA: official source/config and checkpoint URI/size are known; checkpoint SHA and exact question encoder, generated-answer decoder/ranker native graphs are missing.
- BLIP-2 OPT-2.7B: official LAVIS config is pinned; checkpoint SHA and complete EVA-CLIP-G/Q-Former/query-token/projection/OPT tokenizer/decoder/KV native bundle are missing.
- InstructBLIP Flan-T5-XL: research-only model notice applies; checkpoint SHA and complete instruction-aware Q-Former/projection/Flan-T5 native bundle are missing.

These Profiles throw `CapabilityUnavailable`; they do not invent token IDs or graph ports. TensorRT remains unimplemented. / 这些 Profile 稳定抛出能力不可用，不虚构 Token ID 或端口；TensorRT 仍未实现。

## Compatibility and diagnostics / 兼容与诊断

| Layer / 层 | Declared target or runtime / 声明目标或运行时 | Ownership / 所有权 |
| --- | --- | --- |
| Core and Visual contracts | package-declared .NET TFMs; tokenizer implementation is enabled on net8.0/net9.0/net10.0 | managed package |
| Visual.OpenCV | package-declared TFMs; Windows x64 verified | application explicitly installs matching `JYPPX.OpenCV.runtime.win-x64` |
| ONNX Runtime backend | package-declared TFMs; Windows x64 CPU verified | application explicitly installs `Microsoft.ML.OnnxRuntime` |
| OpenVINO backend | package-declared TFMs; Windows x64 CPU verified | application explicitly installs matching OpenVINO native runtime |
| Model/vocabulary/config | external files, no RID inference | ModelFactory for the published BLIP Caption Base Preview; caller for blocked profiles |

Stable generation errors use `DS-VISUAL-4801..4807`: invalid contract/port, identity mismatch, invalid state, invalid generation/logit, concurrent operation, capacity, and tokenizer failure. General cancellation, timeout, inference, native-load, and disposed errors retain their existing Visual/Core codes. A blocker Profile returns `CapabilityUnavailable`; missing external consumer files return the documented stable skip marker. / 稳定生成错误覆盖合同/端口、Identity、状态、Logit、并发、容量与 Tokenizer；取消、超时、推理、Native 与 Dispose 复用既有错误码。Blocker 返回能力不可用，Consumer 缺文件输出稳定 Skip。

The separate BLIP Caption Base release manifest is `redistributionAllowed:true` and available as a ModelFactory Preview in the [shared vision collection](models-vision-collection.md). The historical BLIP Caption development manifest and the three blocked profiles remain External and are not `AlgorithmVerified`. See [the acquisition article](model-acquisition-blip-family.md), [all-stage inventory](development-model-inventory.md), and `eng/models/generative-vision-language/generative-vision-language-family-support.json` for the remaining supply-chain status. / 独立 BLIP Caption Base 发布清单允许再分发，并作为 ModelFactory Preview 收录到[共享视觉模型集合](models-vision-collection.md)；历史开发清单及三条阻断路径继续保持 External，且均非 `AlgorithmVerified`。
