# Document intelligence and layout understanding / 文档智能与版面理解

Stage 27 adds one artifact-bound document family contract for LayoutLMv3, Donut, and Pix2Struct. The executable representative is official `naver-clova-ix/donut-base-finetuned-cord-v2`: OpenCV prepares an authorized CORD-v2 receipt, then exact named Encoder, Prefill, and KV Decode graphs run on ONNX Runtime CPU and OpenVINO CPU. LayoutLMv3 and Pix2Struct remain explicit official-source blockers. No Python service, remote OCR/API, fixed JSON, substitute 2D position/patch algorithm, positional tensor binding, or TensorRT path is used. / 阶段 27 使用一套工件绑定合同覆盖 LayoutLMv3、Donut 与 Pix2Struct。可执行代表为官方 Donut CORD-v2，LayoutLMv3 与 Pix2Struct 保持精确 blocker；不使用常驻 Python、远程 OCR/API、固定 JSON、替代算法、位置绑定或 TensorRT。

## Quick start / 快速开始

```csharp
DocumentUnderstandingProfile profile = DocumentUnderstandingProfiles.CreateDonutCordV2Onnx();
BackendId backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new DocumentUnderstandingBundle(profile, new[]
{
    new DocumentArtifactBinding(DocumentArtifactRole.DocumentEncoder,
        profile.CreateArtifact(DocumentArtifactRole.DocumentEncoder, encoderPath, backend)),
    new DocumentArtifactBinding(DocumentArtifactRole.DecoderPrefill,
        profile.CreateArtifact(DocumentArtifactRole.DecoderPrefill, prefillPath, backend)),
    new DocumentArtifactBinding(DocumentArtifactRole.DecoderWithPast,
        profile.CreateArtifact(DocumentArtifactRole.DecoderWithPast, decodePath, backend))
});
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new DocumentUnderstandingSession(
    registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
var tokenizer = new DonutDocumentTokenizer(checkpointDirectory, profile.Tokenizer);
using PreparedDocumentPage page = new OpenCvDocumentUnderstandingInputFactory()
    .CreatePageFromFile(imagePath, profile);
using var document = new PreparedDocument(profile, new[] { page });
DocumentEncodedState state = session.SetDocument(document);
DocumentUnderstandingResult result = session.Generate(
    DocumentTaskRequest.StructuredExtraction(profile.Schema.SchemaId), tokenizer);
session.Clear();
```

The caller supplies every graph, XML/BIN sidecar, tokenizer, processor/schema asset, document, and native runtime. DeploySharp NuGet packages contain managed DLL/XML, README, and logo only. / 调用方提供全部模型、Sidecar、Tokenizer、Processor/Schema、文档和 Native Runtime；DeploySharp NuGet 不内置这些资产。

## Exact family matrix / 精确模型族矩阵

| Family/version / 模型族版本 | OCR ownership / OCR 归属 | Input and task / 输入与任务 | Schema/KV / Schema 与 KV | Backend status / 后端状态 |
| --- | --- | --- | --- | --- |
| Donut CORD-v2 `8003d433...` | OCR-free | RGB receipt, `<s_cord-v2>`, structured extraction, one page | `cord-v2.donut-tags.v1`; MBART 4x16x64, cross 1200 | ORT ONNX and OpenVINO FP32 IR executable |
| LayoutLMv3 base `cfbbbff0...` | Caller owns OCR words, boxes, and alignment | image + text + `bbox` + attention; layout classification/entity contract | `layoutlmv3.base.no-task-head`; no decoder KV | Blocked: official base has no task head; no official FUNSD checkpoint admitted |
| Pix2Struct DocVQA base `63f6b3de...` | OCR-free | 16x16 flattened patches with row/column IDs; question text | plain-text schema; official `use_cache=false` | Blocked: no audited dynamic-patch ONNX/OpenVINO bundle |

LayoutLMv3 words and boxes are inseparable caller input. Boxes are finite, non-zero-area integer coordinates in normalized `[0,1000]` page space; token-word alignment, special-token boxes, padding, and truncation belong to the Processor exactly once. Donut and Pix2Struct are OCR-free: neither the Adapter nor Backend may call OCR or accept an untracked OCR-derived replacement. / LayoutLMv3 的 Words/Boxes/Alignment 由调用方拥有并由 Processor 一次派生；Donut/Pix2Struct 为 OCR-free，Adapter 与 Backend 都不得重复 OCR。

## Executable Donut subgraphs / 可执行 Donut 子图

| Role / 角色 | Exact named ports / 精确具名端口 | Shape/type / Shape 与类型 |
| --- | --- | --- |
| Document Encoder | `pixel_values` -> `last_hidden_state` | `float32 [1,3,1280,960]` -> `[1,1200,1024]` |
| Decoder Prefill | `input_ids`, `encoder_hidden_states` -> `logits`, four layers of decoder+encoder `present` key/value | IDs `[1,S]`; logits `[1,S,57580]`; KV `[1,16,T,64]`, cross `T=1200` |
| Decoder with past | `input_ids`, four ordered layers of decoder+encoder `past_key_values` -> `logits`, decoder `present` key/value | one token per step; self KV grows deterministically; cross KV remains fixed |

All ONNX graphs use opset 17 and inline weights; there is no External Data file. The OpenVINO profile binds each XML and verifies its BIN sidecar. Ports are checked by exact name, type, rank, fixed dimension, capacity, artifact SHA, and role. / ONNX 使用 opset 17 且权重内联；OpenVINO Profile 同时绑定 XML/BIN。所有端口按名称、类型、Rank、固定维、容量、SHA 与角色校验。

## Processor, tokenizer, and schema / Processor、Tokenizer 与 Schema

`OpenCvDocumentUnderstandingInputFactory` accepts PNG/JPEG file paths or bytes and BGR/RGB/gray/alpha sources. It decodes once, converts to RGB, reproduces the official shortest-edge resize with Pillow-compatible bilinear sampling, constrains the resized image with Pillow-compatible bicubic thumbnail semantics, centers byte-zero padding on 960x1280, then normalizes to `[-1,1]`. Source SHA, byte length, page index, page size, Profile ID, Processor ID, and tensor ownership are preserved. The audited OpenCV golden differs from Pillow by maximum/mean absolute `0.0156862735748291 / 0.000010231566524857448`; thresholds are explicit, not hidden. / OpenCV 工厂覆盖文件/Bytes 与全部常见通道，单次 Decode 后执行官方 Resize/Thumbnail/Pad/Normalize，并保留源页 Identity。

`DonutDocumentTokenizer` verifies SentencePiece, tokenizer JSON, and added-token SHA before use. It uses managed `Microsoft.ML.Tokenizers` on net8/net9/net10, preserves XLM-R's fairseq model-ID offset, exact CORD task prompt, added structure tags, vocabulary 57580, EOS 2, and maximum 768 tokens. Older TFMs expose the contracts but report a stable tokenizer capability error. / Managed Tokenizer 校验三个资产并保留 XLM-R ID Offset、任务 Prompt、结构 Token、EOS 与容量；旧 TFM 明确报告能力不可用。

`DocumentStructuredOutputParser` accepts only bounded balanced Donut tags. It preserves raw token IDs/text, parse status, schema identity, field path and source-page provenance; repeated fields become arrays. Unbalanced tags, invalid depth/field count/size, or wrong schema fail without JSON/XML repair. / 解析器容量受限并保留原始内容与字段来源；无效结构不会静默修复成成功。

## State, concurrency, and ownership / 状态、并发与所有权

`DocumentUnderstandingSession` owns three backend sessions and one encoded state bound to ordered pages, source identities, Profile, artifacts, Processor, OCR boundary, Tokenizer, Schema, prompt, and KV schema. `SetDocument` is atomic. Multiple task prompts may reuse the immutable encoded page state; every Generate owns transient self/cross KV and publishes only an immutable final summary. Prepared inputs, tokenizer, registry, and external files remain caller-owned; every returned result owns its tokens, text, structured nodes/JSON, provenance, timing, and KV summary. / Session 拥有三条 Backend Session 与完整 Identity 绑定的文档状态；输入和外部对象由调用方拥有，结果自有。

The session is single-writer. Concurrent set/generate/clear fails deterministically. Cancellation, timeout, or callback failure publishes neither a partial document state nor partial KV. Clear invalidates generation state; Dispose cancels active work, waits for unwind, and releases all child sessions exactly once. The current executable Donut profile accepts one page. Typed page order exists for future bundles, but exceeding the profile page limit is a stable failure, not silent truncation. / Session 为 Single-writer；取消和异常不发布部分状态；Clear/Dispose 生命周期明确。当前 Donut 仅单页，多页超限稳定失败。

## Official and backend fidelity / 官方与后端保真

The authorized source is CORD-v2 test row 0, dataset revision `7f0115a4...`, CC-BY-4.0. The extracted PNG SHA256 is `8612d04b70f430f3aef07fbbd5200e382dcc4152b344cc2eff9f735f05a257c8`. Official Transformers/PyTorch, Python ORT 1.23.2, Python OpenVINO 2026.2.1 ONNX import and FP32 IR, DeploySharp ORT, DeploySharp OpenVINO IR, and the package-only ORT consumer all produced the exact same 53 completion tokens, EOS decision, balanced tags, and JSON:

```json
{"menu":{"nm":"- TICKET CP","num":"901016","unitprice":"60.000","cnt":"2","price":"60,000"},"sub_total":{"subtotal_price":"-60.000","tax_price":"5,455"},"total":{"total_price":"60.000","emoneyprice":"60.000","menuqty_cnt":"2.00"}}
```

Official encoder/prefill raw golden SHAs are `456c51d7...` and `34d63331...`. Python ORT encoder/prefill numeric summaries are `6f096256...` / `d81d8474...`; Python OpenVINO IR summaries are `9900dd03...` / `634cd08d...`. The final OpenCV-based .NET run recorded ORT feature/KV `e82c16e4...` / `c51bf2c...` and OpenVINO feature/KV `765e497e...` / `e835c48d...`. The exact values and tokens remain under the external warehouse evidence directory. / 官方、Python 双后端、DeploySharp 双后端和纯包 Consumer 的 53 Token/EOS/字段 JSON 完全一致；中间数值摘要按 Runtime 分别保留。

One observed run, not a benchmark:

| Path / 路径 | Preprocess | Encoder | Tokenize | Prefill | Decode total | Parse |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Official Predictor | 79.322 ms | 3616.232 ms | included | 283.908 ms | 5441.758 ms generation | included |
| DeploySharp ORT CPU | 160.148 ms | 3526.245 ms | 29.602 ms | 131.930 ms | 1037.578 ms | 5.242 ms |
| DeploySharp OpenVINO IR CPU | 120.955 ms | 3364.769 ms | 15.007 ms | 169.444 ms | 2340.868 ms | 0.372 ms |

These numbers are diagnostics only, not P50/P95, throughput, memory, quality, or cross-machine claims. / 以上仅单次诊断，不代表分位数、吞吐、内存、质量或跨机器结论。

## Compatibility and diagnostics / 兼容性与诊断

| Layer / 层 | Declared target / 声明目标 | Application responsibility / 应用责任 |
| --- | --- | --- |
| Visual contracts/session | all package-declared TFMs | Managed API; Donut tokenizer implementation is net8/net9/net10 |
| Visual.OpenCV | package-declared TFMs; Windows x64 verified | install matching `JYPPX.OpenCV.runtime.win-x64` explicitly |
| ORT backend | package-declared TFMs; Windows x64 CPU verified | install `Microsoft.ML.OnnxRuntime` explicitly |
| OpenVINO backend | package-declared TFMs; Windows x64 CPU verified | install matching OpenVINO runtime explicitly |
| Model/Tokenizer/Schema/Golden | external | caller-owned, or ModelFactory only after publication authorization |

Stable document errors are `DS-VISUAL-5002..5010`: invalid contract/ports, capacity, state, identity, concurrency, tokenizer, generation, schema, and unavailable capability. Existing cancellation, timeout, native-load, inference, and disposed errors remain unchanged. / 稳定错误覆盖合同、容量、状态、Identity、并发、Tokenizer、生成、Schema 与能力不可用；既有取消、超时、Native、推理和 Dispose 映射不变。

See [the acquisition article](model-acquisition-document-understanding.md), [Stage 27 API changes](api-changes-stage27.md), [all-stage inventory](development-model-inventory.md), and `eng/models/document-understanding/document-understanding-family-support.json`. All three records are External with `redistributionAllowed:false`, `uploaded:false`, and `downloadable:false`; the official catalog remains empty. TensorRT remains unimplemented. / 三条记录均为 External 且不进入空 official catalog；TensorRT 仍未实现。
