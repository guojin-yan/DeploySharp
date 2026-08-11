# Exact GGUF admission audit / 精确 GGUF 准入审计

Stage 30 adds a read-only admission gate before any local GGUF is loaded. It does not add a model, tokenizer, native runtime, ModelFactory catalog entry, or public API. `eng/models/llm/Test-GgufAdmission.ps1` audits an explicitly selected `DEPLOYSHARP_LLAMA_MODEL` first; when it is unset, it only enumerates `.gguf` candidates below `E:\DeploySharp-Models`. It never downloads, converts, loads, or executes a candidate. / 阶段 30 在加载本地 GGUF 前增加只读准入门。它不新增模型、Tokenizer、原生运行时、ModelFactory catalog 条目或公共 API。`eng/models/llm/Test-GgufAdmission.ps1` 优先审计显式选择的 `DEPLOYSHARP_LLAMA_MODEL`；该变量未设置时仅枚举 `E:\DeploySharp-Models` 下的 `.gguf` 候选，绝不下载、转换、加载或执行候选。

The 2026-08-10 audit found zero warehouse candidates, with both `DEPLOYSHARP_LLAMA_MODEL` and `DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST` unset. Its retained evidence is `eng/models/llm/evidence/llama-gguf-admission-stage30.blocked.txt` (658 bytes, SHA256 `075d62fb93f80a6f52dbd7c404229002dd8d76b300b5c3fe2fb59f571153fcd1`). This is an audit record, not a model file or inference result. / 2026-08-10 审计发现 warehouse 候选为零，`DEPLOYSHARP_LLAMA_MODEL` 与 `DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST` 均未设置。保留的证据为 `eng/models/llm/evidence/llama-gguf-admission-stage30.blocked.txt`（658 bytes，SHA256 如上）；它是审计记录，不是模型文件或推理结果。

## Admission states / 准入状态

| Marker | Meaning / 含义 | Permitted next action / 允许的下一步 |
| --- | --- | --- |
| `DEPLOYSHARP_LLAMA_ADMISSION_BLOCKED` | No exact file, ambiguous selection, or incomplete ModelPack metadata. / 无精确文件、选择歧义或 ModelPack 元数据不完整。 | Supply the missing authorized artifact and evidence; do not load it. / 提供缺失的已授权工件与证据；不得加载。 |
| `DEPLOYSHARP_LLAMA_ADMISSION_READY` | Artifact identity and metadata match, but runtime evidence is incomplete. / 工件身份与元数据相符，但运行证据不完整。 | Run the real CPU evidence matrix and record it. / 运行真实 CPU 证据矩阵并记录。 |
| `DEPLOYSHARP_LLAMA_ADMISSION_ADMITTED` | Exact metadata, hash, native runtime, and retained evidence all match. / 精确元数据、哈希、原生运行时与保留证据均匹配。 | The environment-gated integration test may load the selected model. / 可运行环境门控集成测试加载选中模型。 |

## Required evidence / 必需证据

The ModelPack must name one GGUF model file whose recorded size and SHA256 equal the selected path. Its direct upstream HTTPS URL, immutable revision, license conclusion, quantization, positive context length, BOS/EOS/PAD behavior, tokenizer identity, chat-template identity, generation identity, embedding capability, LLamaSharp version, native package/version, and absolute model path must be concrete values, not `unknown` or `unverified`. The retained runtime-evidence file must hash-match the Manifest and report CPU generation, streaming, cancellation, repeat, single-writer contention, disposal, and either embeddings or an explicit embedding-unsupported result. / ModelPack 必须声明一个 GGUF 模型文件，记录的 size/SHA256 必须匹配选中路径。其直接 upstream HTTPS URL、不可变 revision、许可证结论、quantization、正数 context length、BOS/EOS/PAD 行为、Tokenizer identity、chat-template identity、generation identity、embedding capability、LLamaSharp 版本、原生包/版本与绝对模型路径都必须是具体值，不能是 `unknown` 或 `unverified`。保留的 runtime-evidence 文件必须与 Manifest 哈希匹配，并报告 CPU generation、streaming、cancellation、repeat、single-writer contention、disposal，以及 embedding 或明确的 embedding-unsupported 结果。

```powershell
$env:DEPLOYSHARP_LLAMA_MODEL = 'E:\DeploySharp-Models\approved-model\model.gguf'
$env:DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST = 'E:\GitSpace\DeploySharp-V2.0\DeploySharp\eng\models\llm\manifests\approved-model.modelpack.json'
powershell -NoProfile -ExecutionPolicy Bypass -File eng\models\llm\Test-GgufAdmission.ps1 -RequireAdmitted
```

No exact GGUF, license chain, tokenizer/chat template, generation/context record, model hash, or native CPU evidence is available in this audit. `llm/gguf/external-blocker` therefore remains External, `AlgorithmVerified=false`, `executable=false`, `uploaded=false`, and `downloadable=false`. / 本次审计没有精确 GGUF、许可证链、Tokenizer/chat template、generation/context 记录、模型哈希或原生 CPU 证据。因此 `llm/gguf/external-blocker` 继续保持 External、`AlgorithmVerified=false`、`executable=false`、`uploaded=false` 与 `downloadable=false`。
