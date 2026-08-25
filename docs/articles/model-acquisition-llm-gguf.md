# Acquiring an LLM GGUF / 获取 LLM GGUF

An executable GGUF admission requires an authorized exact file, upstream project and immutable revision, file size and SHA256, quantization, context length, tokenizer identity, chat template, BOS/EOS/PAD behavior, generation configuration, embedding capability, model license, and reproducible CPU/native runtime evidence. Record these values in a ModelPack 2.0 manifest before adding a catalog entry.

Stage 30 again found no `.gguf` below `E:\DeploySharp-Models` and no `DEPLOYSHARP_LLAMA_MODEL`. The manifest `eng/models/llm/manifests/llama-gguf-external-blocker.modelpack.json` therefore remains `portable:false`, `redistributionAllowed:false`, `uploaded:false`, `downloadable:false`, and `deploysharp.execution-status=external-blocker`. The retained Stage 30 audit evidence is `eng/models/llm/evidence/llama-gguf-admission-stage30.blocked.txt` (658 bytes, SHA256 `075d62fb93f80a6f52dbd7c404229002dd8d76b300b5c3fe2fb59f571153fcd1`); it is not a model asset.

Do not download an arbitrary model, infer a model family from a filename, or treat the LLamaSharp package as a model license. Before an environment-gated integration test can load a caller-owned file, set `DEPLOYSHARP_LLAMA_MODEL` and `DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST`, then run `eng/models/llm/Test-GgufAdmission.ps1 -RequireAdmitted`. The ModelPack must bind the exact file path, size/SHA256, source/revision/license, quantization, context, BOS/EOS/PAD, tokenizer/chat-template, generation, embedding, LLamaSharp/native runtime, and hash-protected real CPU evidence. The caller owns the file and matching LLamaSharp native backend.

## Stage 31 authorized acquisition / 阶段 31 授权获取

The user authorized local CPU testing of exactly `Qwen/Qwen2.5-0.5B-Instruct-GGUF` revision `9217f5db79a29953eb74d5343926648285ec7e67`, file `qwen2.5-0.5b-instruct-q4_k_m.gguf`. The authorization excludes upload and publication. The immutable download URL was:

```text
https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/resolve/9217f5db79a29953eb74d5343926648285ec7e67/qwen2.5-0.5b-instruct-q4_k_m.gguf
```

PowerShell `Invoke-WebRequest` downloaded that URL directly to `E:\DeploySharp-Models\qwen2.5-0.5b-instruct-q4_k_m\qwen2.5-0.5b-instruct-q4_k_m.gguf`. No conversion was performed. The exact file is 491,400,032 bytes with SHA256 `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db`; its first four bytes are `GGUF`.

The same model directory retains the following source evidence:

| File | Revision | Bytes | SHA256 |
| --- | --- | ---: | --- |
| `source/LICENSE` | GGUF `9217f5db79a29953eb74d5343926648285ec7e67` | 11,343 | `832dd9e00a68dd83b3c3fb9f5588dad7dcf337a0db50f7d9483f310cd292e92e` |
| `source/README.gguf.md` | GGUF `9217f5db79a29953eb74d5343926648285ec7e67` | 4,856 | `fa1fede7f775a20f111cc098c00900092020572ce78976970a5b0ad9ca211999` |
| `source/config.json` | base `7ae557604adf67be50417f59c2c2f167def9a775` | 659 | `18e18afcaccafade98daf13a54092927904649e1dd4eba8299ab717d5d94ff45` |
| `source/generation_config.json` | base `7ae557604adf67be50417f59c2c2f167def9a775` | 242 | `e558847a8b4402616f1273797b015104dc266fe4b520056fca88823ba8f8ebe6` |
| `source/tokenizer_config.json` | base `7ae557604adf67be50417f59c2c2f167def9a775` | 7,305 | `5b5d4f65d0acd3b2d56a35b56d374a36cbc1c8fa5cf3b3febbbfabf22f359583` |

The upstream distribution declares Apache-2.0 and includes the retained license file. DeploySharp records `redistributionAllowed:false` because the user's authorization is local-only and no publication was requested. Native loading confirmed GGUF V3, architecture `qwen2`, `Q4_K - Medium`, context 32,768, embedding length 896, GPT-2/Qwen2 tokenizer identity, BOS 151643, EOS 151645, PAD 151643, and the embedded Qwen ChatML template. `LLamaSharp.Backend.Cpu 0.27.0` selected the Windows x64 AVX2 libraries built from llama.cpp revision `3f7c29d318e317b63f54c558bc69803963d7d88c`.

The retained runtime evidence is `evidence/deploysharp-stage31-runtime.json`, 7,364 bytes, SHA256 `68f2b1e144c3d4537cb2f7c91473554296bda97a52bc5e5b5e9517dfb0dfc973`. It records real CPU Generate, Stream, in-flight Cancel, deterministic Repeat, `DS-LLM-4004` contention, idempotent Dispose/use-after-dispose, and a normalized 896-dimensional embedding. See [Stage 31 GGUF evidence](../history/llm-gguf-stage31.md). This local execution evidence does not make the model AlgorithmVerified or downloadable.

## Stage 32 immutability audit / 阶段 32 不可变性审计

Stage 32 performed no acquisition or conversion and made no write below the model directory. It recomputed the model, all five source-sidecar hashes, and the retained evidence hash; all values remained identical to the table above and the Stage 31 Manifest. The strengthened gate and package-only consumers reran against the existing files without generating a replacement evidence record. / 阶段 32 没有获取或转换，也没有写入模型目录；主模型、五个来源 sidecar 与既有 evidence 的大小/哈希均保持不变。加固后的门禁和纯包 consumer 仅复用现有文件，没有生成替代证据。

## Stage 33 package maintenance audit / 阶段 33 包维护审计

Stage 33 performed no acquisition, conversion, model-directory write, or new evidence generation. It recomputed the same seven file identities, reran admission, and exercised isolated package-only skip/no-native/CPU graphs. Every hash remained identical, and the retained evidence stayed immutable. / 阶段 33 没有获取、转换、写入模型目录或生成新证据；仅重算相同七个文件、重跑准入与隔离纯包矩阵，全部哈希和既有 evidence 均保持不变。

## Stage 34 support-baseline audit / 阶段 34 支持基线审计

Stage 34 performed no acquisition, conversion, model-directory write, or new evidence generation. It recomputed the exact GGUF, five source sidecars, and retained evidence, reran admission before and after real execution, and left the evidence directory at one unchanged file. Package and consumer probes used repository-local temporary directories only; they never copied or rewrote a model asset. / 阶段 34 没有获取、转换、写入模型目录或生成新证据；精确 GGUF、五个来源 sidecar 和旧 evidence 全部重算并保持一致，真实执行前后准入通过且 evidence 目录仍只有一个未变化文件。包与 consumer 探针仅使用仓库本轮临时目录，不复制或改写模型资产。

## Stage 35 all-package audit / 阶段 35 全包审计

Stage 35 performed no acquisition, conversion, model-directory write, or new evidence generation. It recomputed the exact GGUF, all five source sidecars, and retained evidence before package execution, required `ADMITTED missing=none`, and ran package-only no-native/consumer-owned CPU paths with the evidence output variable unset. The evidence directory remained one unchanged file. / 阶段 35 没有获取、转换、写入模型目录或生成新证据；包执行前重算主模型、五个 sidecar 和旧 evidence，并要求准入为 `ADMITTED missing=none`。纯包无 native/调用方 CPU 路径均未设置 evidence 输出变量，证据目录继续只有一个未变化文件。
