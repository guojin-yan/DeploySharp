# Stage 34 LLamaSharp support baseline / 阶段 34 LLamaSharp 支持基线

Stage 34 converted the manual package inspection from Stage 33 into the read-only `eng/pack/Test-LlamaSharpPackageBoundary.ps1` gate. It requires one matching central version for managed `LLamaSharp` and consumer-owned `LLamaSharp.Backend.Cpu`, the exact `netstandard2.0`/`net8.0` backend targets, lock-file and assets consistency, managed-only nuspec dependency groups, a strict package payload allowlist, and no native backend assembly reference. The package version remains `0.27.0`; no dependency was upgraded. / 阶段 34 将阶段 33 的手工包审计固化为只读门禁。门禁同时校验中央版本、项目、lock/assets、nuspec、包 payload 与程序集引用；托管包和 consumer 持有的 CPU native 包继续固定为 `0.27.0`，本轮没有升级依赖。

## Package boundary / 包边界

Two independent Backend.LlamaSharp packs passed semantic comparison:

| Pack | Bytes | SHA256 |
| --- | ---: | --- |
| A | 117,845 | `09c90c41e418d3f798464ee3c0a40313e5dcfa571881f98e83d3b7da1593ce41` |
| B | 117,846 | `c10233f47f3890de615d9accfad825e5a353e3d5337920f26830117e6dafa42d` |

The gate reported `semantic-comparison=match raw-identical=false`. Every functional entry and its SHA256 matched. The raw archives differ only in NuGet-generated `_rels/.rels` relationship identity/target and the random core-properties entry path; the core-properties content is identical. DeploySharp therefore claims reproducible semantic package payload, not byte-for-byte deterministic `.nupkg` containers. A negative package with an injected `runtimes/win-x64/native/llama.dll` was rejected as `Unexpected NuGet payload`. / 两次独立打包的功能 payload 完全一致，但 NuGet 自动生成的关系标识和 core-properties 路径不同，因此不声称 `.nupkg` 位级可复现。注入 native DLL 的负向包被门禁拒绝。

The accepted Backend package contains ten entries, only managed `netstandard2.0` and `net8.0` assemblies/XML documentation, and nuspec groups that each depend only on `LLamaSharp 0.27.0`. A unit assertion now rejects any `LLamaSharp.Backend.*` assembly reference. Core, LLM, and Backend packages all packed successfully; signature verification remains the known release blocker `NU3004` because the development packages are unsigned. / Backend 包严格为十个条目，两个依赖组仅含托管 LLamaSharp；新增单元断言阻止 native 程序集引用泄漏。三个开发包打包成功，但仍因未签名返回 `NU3004`。

## Isolated consumer matrix / 隔离 consumer 矩阵

Fresh `net8.0` restores used only the Stage 34 local DeploySharp packages plus NuGet.org. The no-native graph contained managed `LLamaSharp 0.27.0`, zero native packages, zero `runtimeTargets`, and zero native output files; loading the exact model returned `DS-NATIVE-6001`. The consumer-owned CPU graph contained `LLamaSharp.Backend.Cpu 0.27.0`, 20 native output files for `win-x64` and `win-arm64`, produced the stable missing-model and missing-SHA skips, then passed exact-SHA real CPU generation. / 全新纯包还原分别验证了无 native 和 consumer 自持 CPU native 两张资产图；前者稳定返回 `DS-NATIVE-6001`，后者依次通过 missing-model、missing-SHA 与精确 SHA 的真实 CPU 路径。

The environment-gated real test passed Generate, Stream, in-flight Cancel, deterministic Repeat, `DS-LLM-4004` contention, normalized Embedding, eight concurrent Dispose calls, idempotent Dispose, and use-after-dispose rejection. No evidence output path was set. The retained Stage 31 evidence remained the only evidence file at 7,364 bytes and SHA256 `68f2b1e144c3d4537cb2f7c91473554296bda97a52bc5e5b5e9517dfb0dfc973`. / 真实门控覆盖生成、流式、取消、重复、并发占用、Embedding 与并发/幂等释放；没有设置证据输出路径，Stage 31 旧 evidence 保持唯一且不可变。

## Admission and status / 准入与状态

The exact model, five source sidecars, GGUF magic, and retained evidence all matched their immutable identities. Admission returned `ADMITTED missing=none`; inventory remained 69 entries and 56 structured Manifests. The Qwen row remains External, `AlgorithmVerified=false`, `redistributionAllowed=false`, `uploaded=false`, and `downloadable=false`, and the official catalog remains empty. Stage 34 downloaded, converted, created, replaced, uploaded, or published no model, Tokenizer, sidecar, or evidence. / 精确模型、五个 sidecar、magic 和旧 evidence 均无漂移，准入与 inventory 通过。模型继续保持全部 External/发布门禁，本阶段没有模型资产写入或外部发布操作。
