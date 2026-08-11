# Development model inventory / 开发模型总清单

DeploySharp keeps one machine-readable inventory for every model, converted graph, external blocker, and contract fixture used in Stages 1-35: `eng/models/inventory/development-model-inventory.json`. Regenerate it with `eng/models/inventory/Update-DevelopmentModelInventory.ps1`; use `-Check` in gates. Stage 1 used no model. The current snapshot contains 69 entries: 56 structured manifests, 11 contract-fixture groups, the Stage 2 gated GGUF row, the Stage 17 local YOLO matrix, the Stage 30 GGUF admission blocker, and the Stage 31 exact Qwen external row. Stages 32-35 added no inventory row. / DeploySharp 使用一份机器可读清单汇总阶段 1-35 的所有模型、转换图、External blocker 与合同 Fixture。当前快照仍为 69 条/56 份结构化 Manifest；阶段 32-35 没有新增清单行。

## Storage layout / 存储布局

Newly acquired or converted artifacts use one external warehouse and one model-name directory:

```text
E:\DeploySharp-Models\
  <model-name>\
    original checkpoint/tokenizer/config
    converted-<format-or-opset>\
    evidence and golden sidecars
```

`E:\Model` and `E:\Data` remain read-only caller-owned sources. They are not reorganized, copied into the warehouse, Git, NuGet, ModelFactory catalog, or Release. Existing files created by stages 22-29 are consolidated under the warehouse; earlier caller-owned local matrices remain referenced as `legacy-read-only`. / 新取得或转换的工件统一进入上述仓库并按模型名分目录。`E:\Model` 与 `E:\Data` 继续只读且由用户拥有，不搬迁、不复制到仓库、Git、NuGet、ModelFactory Catalog 或 Release。阶段 22-29 新建工件已归并；更早用户模型仅记录为 `legacy-read-only`。

## Mandatory round closeout / 每轮强制收口

At the end of every development round, inspect and classify all new files, remove only round-local temporary outputs that are no longer needed, verify that every new download/conversion is under `E:\DeploySharp-Models\<model-name>`, and ensure a matching acquisition/conversion article exists. Run the inventory generator with `-Check`, record model paths and hashes, inspect `git status`/`git diff --check`, and explicitly report whether a commit or GitHub push occurred. Do not commit or push without explicit user authorization; ask the user when a missing artifact, license, runtime, or other blocker prevents a decision. / 每轮结束都要检查并分类新文件，只删除本轮不再需要的临时产物；确认所有新下载/转换均在 `E:\DeploySharp-Models\<model-name>`，并有对应获取/转换文章。运行清单生成器和 `-Check`，记录模型路径和哈希，检查 `git status`/`git diff --check`，明确报告是否提交或推送 GitHub。没有用户明确授权不得提交/推送；缺少模型、许可证、运行时或其他前置条件时必须向用户请求。

## Stage 28 audio speech / 阶段 28 音频语音

The executable `audio/wav2vec2-base-960h/external` row contains exact ONNX/OpenVINO IR sizes and SHA256 values, the 16 kHz processor/vocabulary identity, and ORT/OpenVINO/OpenCV/NuGet evidence. Whisper tiny.en, HuBERT base-ls960, and pyannote speaker-diarization 3.1 are explicit source-contract blockers. All four are External, non-redistributable, uploaded=false, and downloadable=false. / 可执行的 Wav2Vec2 行包含 ONNX/OpenVINO IR 大小与 SHA256、16 kHz Processor/词表 Identity 及 ORT/OpenVINO/OpenCV/纯包证据；Whisper、HuBERT、pyannote 保持 source-contract blocker。四条记录均为 External、禁止再分发、uploaded=false、downloadable=false。

## ModelFactory publication state / ModelFactory 发布状态

The inventory separately records metadata readiness, upload state, and download state. The audited manifests, including the Stage 31 local-only Qwen row, remain non-published; therefore `uploaded=0`, `downloadable=0`, and the embedded official catalog remains empty. ModelFactory already supports immutable HTTPS Release assets, size/SHA verification, content-addressed cache, and offline reuse, but local possession is not redistribution permission. / 清单分别记录元数据就绪、上传和下载状态。包括阶段 31 本地 Qwen 在内的已审核 Manifest 均未发布，因此上传数和可下载数为 0，内置 official catalog 保持为空。本机持有不等于再分发授权。

A model becomes downloadable only after all of these are true: exact artifact terms permit redistribution; required notices/source obligations are prepared; every file and sidecar has a reviewed size/SHA; an immutable Release URI exists; the catalog entry passes validation; and an explicit release operation is authorized. This stage performs none of those external publication actions. / 只有工件条款明确允许再分发、义务已准备、全部文件已审核、不可变 Release URI 已存在、目录校验通过且得到明确发布授权后，模型才可下载。本阶段不执行外部发布。

## Family acquisition guides / 模型族获取指南

- [YOLO family](model-acquisition-yolo.md)
- [DETR and RT-DETR family](model-acquisition-detr-rtdetr.md)
- [OCR, anomaly, and RMBG](model-acquisition-ocr-anomaly-rmbg.md)
- [SAM and Grounded-SAM](model-acquisition-sam-grounded-sam.md)
- [CLIP, SigLIP, and SigLIP 2](model-acquisition-clip-siglip.md)
- [BLIP, BLIP-2, and InstructBLIP](model-acquisition-blip-family.md)
- [Document intelligence and layout models](model-acquisition-document-understanding.md)
- [LLaVA, Qwen-VL, and Phi Vision](model-acquisition-native-multimodal.md)
- [Audio speech family](model-acquisition-audio-speech.md)
- [LLM/GGUF](model-acquisition-llm-gguf.md)

Always select an exact manifest first. Its source revision, artifact files, ports, size/SHA, license conclusion, backend evidence, and blocker override filenames or remembered defaults. / 始终先选择精确 Manifest；其中的 revision、文件、端口、大小/SHA、许可证、后端证据与 blocker 优先于文件名和经验默认值。

## Stage 30 exact GGUF audit / 阶段 30 精确 GGUF 审计

`eng/models/llm/Test-GgufAdmission.ps1` first audits `DEPLOYSHARP_LLAMA_MODEL`, then scans `E:\DeploySharp-Models` only to locate a candidate. It never downloads, converts, loads, or executes a model. An ambiguous warehouse requires an explicit environment path; a selected candidate still requires `DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST` and exact source, revision, license, model size/SHA256, quantization, context, BOS/EOS/PAD, tokenizer, chat-template, generation, embedding, managed/native runtime, and real runtime-evidence fields. The current audit returned `missing-exact-gguf`; the Stage 30 row remains External, non-executable, non-AlgorithmVerified, non-uploaded, and non-downloadable. / `eng/models/llm/Test-GgufAdmission.ps1` 先审计 `DEPLOYSHARP_LLAMA_MODEL`，再仅扫描 `E:\DeploySharp-Models` 定位候选；它绝不下载、转换、加载或执行模型。仓库中多个候选时必须显式设置环境路径；选中候选后仍必须提供 `DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST` 与精确 source、revision、license、模型 size/SHA256、量化、context、BOS/EOS/PAD、Tokenizer、chat-template、generation、embedding、托管/原生 runtime 和真实运行证据字段。当前审计结果为 `missing-exact-gguf`；阶段 30 行保持 External、不可执行、非 AlgorithmVerified、不可上传和不可下载。

## Stage 31 exact Qwen runtime evidence / 阶段 31 精确 Qwen 运行实证

The new `llm/qwen2.5-0.5b-instruct-q4-k-m/external` row binds the authorized local GGUF, source sidecars, exact size/SHA256, Apache-2.0 evidence, embedded tokenizer/chat-template identity, and hash-protected LLamaSharp CPU operation evidence. The admission audit returns `ADMITTED`, but the row remains External, `AlgorithmVerified=false`, `redistributionAllowed=false`, `uploaded=false`, and `downloadable=false`; Stage 30's historical missing-model blocker remains unchanged. / 新增行绑定授权 GGUF、来源 sidecar、精确大小/哈希、许可证、内嵌 Tokenizer/chat-template 与 LLamaSharp CPU 证据。准入审计返回 `ADMITTED`，但该行继续保持 External、非 AlgorithmVerified、禁止再分发、未上传且不可下载；阶段 30 历史 blocker 保留。

## Stage 32 immutable GGUF audit / 阶段 32 GGUF 不可变审计

Stage 32 revalidated the existing Qwen row without adding or promoting an entry. The read-only gate now checks GGUF magic, all Manifest-bound sidecars, exact model-local evidence placement, and the evidence's internal model/runtime/operation identity. Counts remain 69 entries and 56 structured Manifests; publication fields remain zero and the official catalog remains empty. / 阶段 32 不新增或提升清单行，而是重新验证既有 Qwen 行的 magic、全部 sidecar、模型本地 evidence 路径与内部身份。计数保持 69/56，发布字段为 0，official catalog 为空。

## Stage 33 package maintenance / 阶段 33 包维护

Stage 33 revalidated the same Qwen row and package boundary without adding or promoting an inventory entry. The managed-only backend package and caller-owned native consumer graphs passed; inventory counts and all publication fields remain unchanged. / 阶段 33 在不新增或提升清单行的前提下复验同一 Qwen 与包边界；纯托管 backend 和调用方持有 native 的 consumer 图均通过，清单计数及发布字段不变。

## Stage 34 executable package boundary / 阶段 34 可执行包边界

Stage 34 added no inventory entry and changed no Manifest or publication field. The reusable package gate, native-injection negative test, isolated consumer asset graphs, exact admission, and real CPU resource lifecycle all passed. Counts remain 69 entries and 56 structured Manifests; uploaded/downloadable remain zero and the official catalog remains empty. / 阶段 34 没有新增清单行，也没有修改 Manifest 或发布字段；包边界正负向门、隔离 consumer 资产图、精确准入与真实 CPU 生命周期均通过，计数与空 official catalog 保持不变。

## Stage 35 all-package governance / 阶段 35 全包治理

Stage 35 added no inventory entry and changed no Manifest or publication field. The all-package positive/mutation gates, 30 package-only consumers, exact admission, and real caller-owned CPU runtime passed without writing model evidence. Counts remain 69 entries and 56 structured Manifests; uploaded/downloadable remain zero and the official catalog remains empty. / 阶段 35 没有新增清单行或修改 Manifest/发布字段；全包正负向门、30 项纯包 consumer、精确准入与调用方持有的真实 CPU 路径通过，且没有写入模型证据。计数与空 official catalog 保持不变。
