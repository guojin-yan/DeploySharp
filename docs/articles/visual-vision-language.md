# CLIP, SigLIP, and SigLIP 2 / CLIP、SigLIP 与 SigLIP 2

Stage 24 adds one backend-neutral, artifact-bound dual-encoder API for image/text embedding, zero-shot classification, and cross-modal retrieval. It does not add a model-specific NuGet, bundle weights/tokenizers/native runtimes, or implement TensorRT. / 阶段 24 新增后端无关、工件绑定的双编码器 API，用于图像/文本 Embedding、零样本分类与跨模态检索。不新增单模型 NuGet，不携带权重、Tokenizer 或 Native Runtime，也不实现 TensorRT。

## Quick start / 快速开始

The caller must use the exact official tokenizer named by the Profile and supply its owned `TextTokenBatch`. Token IDs are not inferred from strings and cannot be shared across Profiles. / 调用方必须使用 Profile 指定的精确官方 Tokenizer，并提供自有 `TextTokenBatch`。DeploySharp 不从字符串猜测 Token ID，Token 也不能跨 Profile 复用。

An external official-tokenizer golden summary covers ASCII, Chinese, NFC/NFD composed characters, punctuation, whitespace folding, and overlong truncation for both profiles. It is 4,965 bytes with SHA256 `b896f7dec649e29cd11628e87bf711a76c71c5c024d1ad532abe11f30c3671ce`. The summary records raw UTF-8, fixed INT64 IDs, masks, active counts, and untruncated counts; it remains outside Git/NuGet. DeploySharp validates caller-owned IDs/masks and identity but does not duplicate the tokenizer algorithm. / 外部官方 Tokenizer Golden 摘要覆盖 ASCII、中文、NFC/NFD、标点、空白和超长截断，并记录原文、ID、Mask 与长度 Hash；它不进入 Git/NuGet。DeploySharp 验证调用方 Token/Mask/Identity，但不复制 Tokenizer 算法。

```csharp
VisionLanguageEmbeddingProfile profile = VisionLanguageProfiles.CreateClipVitB32();
var backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new VisionLanguageArtifactBundle(
    profile,
    profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder, imageOnnx, backend),
    profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder, textOnnx, backend));

using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new VisionLanguageEmbeddingSession(
    registry, bundle,
    new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
using PreparedVisualInput imageInput =
    new OpenCvVisionLanguageInputFactory().CreateFromFile(imagePath, profile);

VisionLanguageImageEmbedding image = session.EncodeImage(imageInput);
VisionLanguageTextEmbedding text = session.EncodeText(officialTokenizerOutput);
VisionLanguageScoreMatrix scores = VisionLanguageScorer.Score(profile, image, text);
IReadOnlyList<VisionLanguageRetrievalMatch> top =
    VisionLanguageScorer.RetrieveTexts(profile, image, text, topK: 3);
```

## Exact matrix / 精确合同矩阵

| Profile | Image input/output | Text input/output | Tokenizer/pooling | Score ownership | Status |
| --- | --- | --- | --- | --- | --- |
| CLIP ViT-B/32 | `pixel_values` FP32 `[-1,3,224,224]` -> `image_embedding` `[-1,512]` | `input_ids`, `attention_mask` INT64 `[-1,77]` -> `text_embedding` `[-1,512]` | CLIP byte-BPE, BOS `49406`, EOS/PAD `49407`, official EOT pooling | scale `100.00000762939453`; DeploySharp softmaxes across the exact requested candidate set | External executable on ORT/OpenVINO CPU |
| SigLIP base patch16-224 | `pixel_values` FP32 `[-1,3,224,224]` -> `image_embedding` `[-1,768]` | `input_ids` INT64 `[-1,64]`; no attention-mask port -> `text_embedding` `[-1,768]` | SigLIP SentencePiece, EOS/PAD `1`, official model pooler | scale `117.33076477050781`, bias `-12.9324369430542`; each pair owns an independent sigmoid | External executable on ORT/OpenVINO CPU |
| SigLIP 2 base patch16-224 | Official source checkpoint and Gemma tokenizer only | No audited local native ports | `tokenizer.model`, add-BOS false | Not inferred from SigLIP | External blocker |

CLIP uses RGB, shortest-edge resize to 224, center crop, Pillow bicubic, and mean/std `[0.48145466,0.4578275,0.40821073]` / `[0.26862954,0.26130258,0.27577711]`. SigLIP uses RGB fixed 224 resize, Pillow bicubic, and mean/std `0.5`. Profile values are stored in the byte domain to match `OpenCvPreprocessOptions`. / CLIP 使用 RGB、最短边 224、中心裁剪与 Pillow Bicubic；SigLIP 使用 RGB 固定 224 与 Pillow Bicubic。Profile 以字节域保存均值/标准差，以匹配 OpenCV 合同。

## Identity and ownership / Identity 与所有权

`VisionLanguageEmbeddingSession` owns exactly two backend sessions. Every call is single-writer; concurrent calls return `DS-VISUAL-4705`. Successful calls atomically replace the matching cache. Cancellation or timeout publishes no partial result. `ClearCache` drops session references, while already returned embeddings remain defensive managed copies. `Dispose` cancels an active operation, waits for it to unwind, then disposes both sessions once. / Session 精确拥有两条 Backend Session。调用为单写者；并发返回 `DS-VISUAL-4705`。成功后原子替换相应缓存；取消/超时不发布部分结果。`ClearCache` 只清会话引用，已返回 Embedding 仍为托管防御性副本。`Dispose` 取消并等待活动调用，再仅一次释放两条 Session。

Embedding identity binds Profile ID, ordered image/text artifact SHA identity, source-image SHA or ordered token-batch SHA, and dimension. `VisionLanguageScorer` rejects mixed Profile/artifact/dimension values. Template aggregation means the selected normalized text vectors and L2-normalizes the aggregate once; score normalization remains owned by the exact candidate set. / Embedding Identity 绑定 Profile、按角色排序的工件 SHA、源图/有序 Token 批 SHA 与维度。Scorer 拒绝混用。模板聚合对所选归一化文本向量求均值并再次 L2 归一化；候选集评分归一化仍由当前请求拥有。

ModelFactory Offline Preview can filter a complete bundle by family, version, task, capability, format, backend, precision, tokenizer, language, resolution, and score semantics. Bundle validation rejects missing roles/sidecars and mixed tokenizer, preprocessing, projection, normalization, score, language, resolution, or conversion identity. / ModelFactory Offline Preview 可按模型族、版本、任务、能力、格式、后端、精度、Tokenizer、语言、分辨率和评分语义查询完整 Bundle，并拒绝缺角色/Sidecar 或混 Identity。

## Official evidence / 官方保真证据

Audited upstream snapshot on 2026-08-08/09: OpenAI CLIP commit `d05afc436d78f1c48dc0dbf8e5980a9d471f35f6` (MIT); Google Big Vision commit `0127fb6b337ee2a27bf4e54dea79cff176527356` (Apache-2.0). Exact model revisions are CLIP `3d74acf9a28c67741b2f4f2ea7635f0aaf6f0268`, SigLIP `7fd15f0689c79d79e38b1c2e2e2370a7bf2761ed`, and SigLIP 2 `75de2d55ec2d0b4efc50b3e9ad70dba96a7b2fa2`. / 官方 Commit、许可证与模型 Revision 如上，均固定到审计快照。

The isolated exporter used Python 3.13.12, torch 2.9.1 CPU, Transformers 4.57.3, ONNX 1.20.0, ONNX Runtime 1.23.2, `torch.onnx.export(dynamo=false)`, opset 17, dynamic batch only, and no external data. It wrapped the official `get_image_features`/`get_text_features`, projection, pooling, and L2 normalization. / 隔离导出链版本、opset、动态轴与官方 Feature API 如上；未使用 External Data。

The external export script SHA256 is `1677b64797dd93ca1f93c30c32d937969a563f8a59b5f498f2fc3f6e099946b2`; its `pip freeze --all` lock is 642 bytes with SHA256 `bc2d20a8a923bce22682f009b50cec5f5007748efcd7b6e12c77e31babb1f9fb`. The exact commands were the following; `hf-mirror.com` was transport only and every requested official revision was checked. / 外部导出脚本与依赖锁 SHA 如上；镜像只承担传输，所有请求均固定并核验官方 Revision。

```powershell
$env:HF_ENDPOINT='https://hf-mirror.com'
E:\DeploySharp-stage24-temp\.venv\Scripts\python.exe E:\DeploySharp-stage24-temp\export_vlm.py --family clip --model openai/clip-vit-base-patch32 --revision 3d74acf9a28c67741b2f4f2ea7635f0aaf6f0268 --image E:\Data\image\bus.jpg --output E:\DeploySharp-Models\clip-vit-base-patch32
E:\DeploySharp-stage24-temp\.venv\Scripts\python.exe E:\DeploySharp-stage24-temp\export_vlm.py --family siglip --model google/siglip-base-patch16-224 --revision 7fd15f0689c79d79e38b1c2e2e2370a7bf2761ed --image E:\Data\image\bus.jpg --output E:\DeploySharp-Models\siglip-base-patch16-224
```

| External file / 外部文件 | Size | SHA256 |
| --- | ---: | --- |
| CLIP source `pytorch_model.bin` | 605,247,071 | `a63082132ba4f97a80bea76823f544493bffa8082296d62d71581a4feff1576f` |
| CLIP image encoder ONNX | 351,593,168 | `51e6e8f7c1d0f43c9434751d55238bba6cd6fde02865a4839683f82928e30963` |
| CLIP text encoder ONNX | 253,943,687 | `e167dd8f5510fb1bf6cdf6458f0582e69f27abacd123fe660b93ca90db4be3a8` |
| CLIP `tokenizer.json` | 2,224,041 | `b556ac8c99757ffb677208af34bc8c6721572114111a6e0aaf5fa69ff0b8d842` |
| SigLIP source `model.safetensors` | 812,672,320 | `2c63cb7d1f2e95ba501893cbb8faeb4ea9a3af295498d35097126228659c2af8` |
| SigLIP image encoder ONNX | 371,784,017 | `6f6d699bee2f2978675a3aa5e3d47c2933df0a9e68ea4ad854c77cdde9174155` |
| SigLIP text encoder ONNX | 441,298,653 | `da30eb3ed3fc15add817d4c24ebcd53bfd4525cae833b3f91e82a04fe1d9c9c9` |
| SigLIP `tokenizer.json` / `spiece.model` | 2,399,357 / 798,330 | `c6e405cb7c670d56636a9402c81023a55bc6c3c53d89cf02b92f5c5005bfe920` / `1e5036bed065526c3c212dfbe288752391797c4bb1a284aa18c9a0b23fcaf8ec` |
| SigLIP 2 source / tokenizer | 1,500,800,904 / 4,241,003 | `612923381c76ec5a9bed335d1c48827e3f2e506ac31b044b63b2031fadee6a0b` / `61a7b147390c64585d6c3543dd6fc636906c9af3865a5548f27f31aee1d4c8e2` |

On authorized `bus.jpg` SHA `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` with prompts `bus/person/dog`, official CLIP logits are `26.5425701/19.4810085/17.3104191`; official SigLIP logits are `-1.7420225/-13.3275032/-15.3673458`. Both rank bus first. Exact official pixel goldens produce image-embedding maximum absolute differences: CLIP ORT/OpenVINO `2.682209e-7/2.682209e-7`; SigLIP `3.6507845e-7/2.682209e-7`. Final single observed encoder timings were CLIP ORT image/text `79.097/150.884 ms`, OpenVINO `374.266/170.571 ms`; SigLIP ORT `139.516/165.819 ms`, OpenVINO `185.884/174.023 ms`. These are single observations, not percentile, throughput, memory, or accuracy claims. / 同图官方 Logit、排序、Embedding 差异与单次 Timing 如上；不构成 P50/P95、吞吐、内存或精度结论。

OpenCV 5 `INTER_CUBIC` and official Pillow bicubic are not pixel-equivalent. On this image, normalized pixel max/mean absolute differences are CLIP `2.2511654/0.17610928` and SigLIP `1.1921569/0.09764213`. The actual OpenCV-to-ORT path still ranks bus first; CLIP/SigLIP image-embedding max differences are `0.14006306/0.06029393`, with logit max differences `0.5200653/1.1853752`. One observed OpenCV decode/preprocess, image encoder, text encoder, score, and retrieval timing was CLIP `37.778/49.218/163.669/0.104/0.121 ms` and SigLIP `26.009/137.245/178.176/0.036/0.068 ms`. The geometry, channel order, and normalization sequence are verified, but exact official-pixel backend parity uses the retained external Pillow golden. This limitation is explicit and is not called exact OpenCV fidelity. / OpenCV 与 Pillow Bicubic 内核不等价；实测像素、Embedding、Logit 差异和单次分阶段 Timing 如上，实际路径仍保持 bus 第一。精确后端对齐使用外部 Pillow Golden，不把当前 OpenCV 路径宣称为逐像素官方一致。

## Errors and compatibility / 错误与兼容性

| Code | Meaning / 含义 |
| --- | --- |
| `DS-VISUAL-4701` | Profile, tokenizer, tensor, port, output normalization, or score contract invalid / 合同无效 |
| `DS-VISUAL-4702` | Batch, token, tensor, or top-k capacity exceeded / 容量超限 |
| `DS-VISUAL-4703` | Session state does not permit the operation / 状态无效 |
| `DS-VISUAL-4704` | Profile/artifact/token/image embedding identity mismatch / Identity 失配 |
| `DS-VISUAL-4705` | Concurrent session operation rejected / 并发调用被拒绝 |

Managed Core/Visual APIs retain all declared library TFMs. Real model evidence and the clean consumer use `net10.0` on Windows x64. Applications explicitly install `Microsoft.ML.OnnxRuntime` or `OpenVINO.runtime.win`, plus `JYPPX.OpenCV.runtime.win-x64` for image loading. The DeploySharp adapter packages remain managed-only and do not select native runtimes. / Managed API 保留全部声明 TFM；真实证据与 Consumer 使用 Windows x64 `net10.0`。应用必须显式安装对应 Native Runtime；DeploySharp Adapter 不代选 Native Runtime。

All three manifests are External, `redistributionAllowed:false`, outside the empty official catalog, and not `AlgorithmVerified`. SigLIP 2 remains blocked because no complete local audited official ONNX/OpenVINO dual-encoder export and native processor fidelity evidence exists; no resident Python fallback is provided. / 三份 Manifest 均为 External、禁止再分发、不进入空 official catalog 且非 AlgorithmVerified。SigLIP 2 因缺少完整可审计官方 Native 双编码器导出与 Processor 保真证据而阻断，也不提供 Python 常驻回退。
