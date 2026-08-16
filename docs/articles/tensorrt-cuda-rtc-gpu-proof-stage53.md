# TensorRT CUDA/RTC GPU and formal-proof incremental admission / TensorRT CUDA/RTC GPU 与正式证明增量准入

Stage 53 performs a read-only identity recheck and preserves all Stage 52 managed inference, ONNX-builder, and CUDA/RTC APIs. No authorized exact CUDA kernel, ONNX, plan, target GPU, or unique CUDA/driver/TensorRT/NVRTC/native-bridge matrix was supplied, so no native initialization, compile, module load, launch, engine build, inference, algorithm validation, or performance measurement was attempted. / 阶段 53 只做身份增量复核，并保留阶段 52 的 inference、builder 与 CUDA/RTC managed API。因未提供并授权精确 kernel/ONNX/plan、目标 GPU 与唯一 native matrix，本轮不执行任何 native/GPU 路径。

## Upstream identity / 上游身份

| Field | Stage 53 read-only value |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; `immutable=false`; updated `2026-08-11T00:49:26Z` |
| Tag/commit/tree | `673e120807d789d90a13a9f28a043282e95bb5e6`; tree `adb9f24d233924739436e7b7c73c896e67d99e1e`; 3,559 entries; not truncated |
| Assets | 20 unchanged assets; proof-named manifest/provenance/attestation assets `=0` |
| GitHub managed package asset | ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| NuGet.org catalog package | 15,608,836 bytes; SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`; listed; repository commit `673e120...` |
| Retained NuGet identity | SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`; repository-signed state retained, not reverified |
| GitHub attestations | Both GitHub-asset and NuGet-package SHA256 subject lookups return no attestation |

The exact package and Release identities did not change. The package was not downloaded, the eight-class package admission was not rerun, and the retained JSON remains byte-identical at 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. Blocker delta is retained 2, new 0, disappeared 0. / 精确包与 Release 身份未变化；不下载包、不重跑八类 admission、不改写 retained JSON。blocker delta 为 retained 2/new 0/disappeared 0。

## Formal publication / 正式发布

Formal publication remains blocked only by:

1. an immutable cross-channel manifest binding repository/tag/commit, the GitHub asset size/SHA256, and the NuGet.org signed package size/SHA256/SHA512/contentHash/catalog/signature state;
2. same-build immutable provenance or attestation binding commit, lock/assets/build inputs, all released assets, and exact output hashes.

Release prose, scripts in the tag tree, consumer locks, and local package caches are not substitutes for either proof. The Stage 42 package-license and source-owner decision blockers remain historical disappeared entries. / 正式发布仍只受跨渠道不可变 manifest 与同次构建 provenance/attestation 两项阻断；已消失的许可证问题不回退。

## Managed API and ownership / Managed API 与所有权

The retained net8 public contract has 215 members and contract SHA256 `d5b74032d2a0da2926595bc8db184aa3a1aa6b3f43ee97d60446594ad1c82452`. It still exposes the engine-only inference provider, explicit ONNX-to-engine builder, and independent CUDA/RTC definition/compiler/artifact/module/launch contracts. Core and ModelPack remain TensorRT-free. / net8 public contract 继续为 215 members；inference、builder、CUDA/RTC managed surface 均未回退，Core/ModelPack 仍无 TensorRT 依赖。

DeploySharp owns only its inference managed wrappers, builder temporary write lifecycle, one loaded CUDA Driver module, and returned launch owner. The caller owns CUDA streams/device memory and all TensorRT/CUDA/NVRTC/cuDNN/driver/native-bridge binaries, kernel/model/engine/plan artifacts, and External/local cache paths. No TensorRT-LLM, Core CUDA backend, default-stream path, persistent cache writer, or package/catalog/inventory payload was added. / DeploySharp 只持有 managed wrapper、builder 临时写入生命周期、已加载 module 与 launch owner；全部 native、stream/buffer 与生成工件继续由调用方持有。

The CUDA kernel cache key remains I/O-free and binds schema, source/header/options/artifact hashes, compiler version and binary identity, target architecture, artifact kind, kernel entry point, CUDA runtime version/binary identity, driver version/identity, GPU architecture/unique identity, and native-bridge identity. Launch identity separately binds artifact/kernel, grid/block/shared memory, synchronization mode, scalar value hashes, buffer descriptor identities, and device ordinals. / cache key 继续完整绑定编译、工件、runtime、driver、GPU 与 bridge identity；launch identity 另行绑定 launch、scalar、buffer 与 device 字段。

## Verification / 验证

- Focused managed contracts pass `15 passed / 0 skipped / 0 failed` without native initialization.
- One fresh `dotnet pack --no-restore` attempt failed because retained `obj/project.assets.json` still pointed to the already-removed Stage 52 isolated package cache. A subsequent `--no-build` pack from the verified Release binary succeeded. The first isolated-consumer restore also exposed a copied relative-source path error; after switching to the absolute local source, dependency resolution reached the exact-package blocker below. These setup failures did not run native code and their temporary outputs were removed.
- The package-only consumer could not be rerun from an exact local dependency graph. The only global-cache `4.0.0` nupkg is the rejected Stage 40 identity (15,230,357 bytes, SHA256 `140d7cc4...`), while the admitted 15,608,836-byte package is absent locally. The no-download boundary therefore makes this verification `blocked`; the old package was not substituted.
- Stage 52 retained package-only evidence remains 17 pass, 11 expected external skip, and 3 expected external block, including the TensorRT marker `cuda-rtc=managed-contract gpu=not-run`; it is not claimed as a Stage 53 rerun.
- Inventory `-Check` passes with 69 entries. Exact Qwen admission is `ADMITTED missing=none`; the GGUF remains 491,400,032 bytes with SHA256 `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db`; official catalog remains empty.
- No code/API/package-graph change occurred, so dual-candidate pack, Stage 35/36 positive and negative gates, full solution, and NuGet vulnerable/deprecated/outdated reports were not rerun. Their latest Stage 52 results remain retained, not current execution claims.

`CUDA/RTC GPU validation skipped/blocked`. The missing authorization/identity also blocks preprocessing and postprocessing GPU paths, synchronization-error propagation, active-launch/module disposal on hardware, artifact/cache-key runtime proof, ONNX build, and TensorRT inference. CPU/mock/ORT evidence was not substituted, and no CUDA/TensorRT algorithm or performance claim is made. / `CUDA/RTC GPU validation skipped/blocked`；不以 CPU/mock/ORT 替代，不声明 CUDA/TensorRT 算法或性能通过。

The temporary Stage 53 candidate package and isolated consumer directory were removed. No model/tool download, dependency upgrade, commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 本轮临时包与 consumer 目录已清理；没有模型/工具下载、依赖升级或 Git/GitHub 写入。
