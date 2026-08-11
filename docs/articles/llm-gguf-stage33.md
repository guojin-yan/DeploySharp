# LLamaSharp package maintenance gate / LLamaSharp 包维护门

Stage 33 found no implementation or public-contract gap. The packed `JYPPX.DeploySharp.Backend.LlamaSharp 2.0.0-alpha.1` contains only `net8.0` and `netstandard2.0` managed assemblies. Both dependency groups bind `LLamaSharp 0.27.0`; neither declares `LLamaSharp.Backend.Cpu` nor contains a `runtimes/`, LLama, or ggml native payload. The application remains responsible for selecting and owning a matching native package. / 阶段 33 未发现实现或公共合同缺口。打包后的 Backend.LlamaSharp 仅包含两个托管目标；两个依赖组都固定托管 `LLamaSharp 0.27.0`，不声明 CPU native 包，也不携带 native payload。匹配原生包仍由应用选择和持有。

## Package-only matrix / 纯包矩阵

An isolated `net8.0` consumer restored every DeploySharp package from the Stage 33 local pack output. With `IncludeLlamaNativeBackend=false`, its asset graph contained managed LLamaSharp but no `LLamaSharp.Backend.*` package or LLama/ggml DLL, and loading the exact model returned `DEPLOYSHARP_LLAMA_NO_NATIVE_OK error=DS-NATIVE-6001`. The normal graph explicitly contained `LLamaSharp.Backend.Cpu 0.27.0`; it produced the stable missing-model skip with no model environment, then passed a real CPU generate/embedding run with the exact model SHA256. Neither path wrote runtime evidence. / 隔离的 `net8.0` consumer 从本轮本地包输出还原。无 native 图仅含托管 LLamaSharp，加载精确模型时返回稳定 `DS-NATIVE-6001`；正常图由 consumer 显式持有 CPU backend，先通过缺模型 skip，再通过精确 SHA256 的真实 CPU 生成与嵌入。两条路径均未写运行证据。

## Immutable files / 不可变文件

The read-only audit recomputed every Manifest-bound file before execution:

| File | Bytes | SHA256 |
| --- | ---: | --- |
| `qwen2.5-0.5b-instruct-q4_k_m.gguf` | 491,400,032 | `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db` |
| `source/config.json` | 659 | `18e18afcaccafade98daf13a54092927904649e1dd4eba8299ab717d5d94ff45` |
| `source/generation_config.json` | 242 | `e558847a8b4402616f1273797b015104dc266fe4b520056fca88823ba8f8ebe6` |
| `source/tokenizer_config.json` | 7,305 | `5b5d4f65d0acd3b2d56a35b56d374a36cbc1c8fa5cf3b3febbbfabf22f359583` |
| `source/LICENSE` | 11,343 | `832dd9e00a68dd83b3c3fb9f5588dad7dcf337a0db50f7d9483f310cd292e92e` |
| `source/README.gguf.md` | 4,856 | `fa1fede7f775a20f111cc098c00900092020572ce78976970a5b0ad9ca211999` |
| `evidence/deploysharp-stage31-runtime.json` | 7,364 | `68f2b1e144c3d4537cb2f7c91473554296bda97a52bc5e5b5e9517dfb0dfc973` |

The magic remained `GGUF`, `Test-GgufAdmission.ps1 -RequireAdmitted` returned `ADMITTED missing=none`, and the evidence hash was unchanged after the gated test and package-only real run. Cancellation, single-writer ownership, concurrent Dispose, idempotent Dispose, and use-after-dispose remain covered by the real Stage 31/32 matrix. / magic 与准入状态保持不变，真实门控和纯包运行前后 evidence 哈希一致；取消、单写入、并发/幂等 Dispose 与释放后使用继续由真实矩阵覆盖。

The exact Qwen remains External, `AlgorithmVerified=false`, `redistributionAllowed=false`, `uploaded=false`, and `downloadable=false`; the official catalog remains empty. Stage 33 downloaded, converted, created, or replaced no model, Tokenizer, sidecar, or runtime evidence. / 精确 Qwen 继续保持 External 与全部发布门禁；本阶段没有下载、转换、创建或替换模型、Tokenizer、sidecar 或运行证据。
