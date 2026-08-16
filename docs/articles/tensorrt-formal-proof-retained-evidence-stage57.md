# TensorRT formal proof and retained-evidence incremental admission / TensorRT 正式证明与保留证据增量准入

Stage 57 is an identity-driven maintenance review. It preserves the admitted inference, ONNX builder, and independent CUDA/RTC managed APIs and does not rerun real GPU or pure-package execution when their complete identities are unchanged. Stage 55 remains the execution proof for its one exact matrix; no general algorithm-accuracy or performance claim is made. / Stage 57 是 identity 驱动的维护复核。已准入的 inference、ONNX builder 与独立 CUDA/RTC managed API 均不回退；完整 identity 未变化时不重复执行真实 GPU 或纯包路径。Stage 55 只证明一套精确 matrix，不推导一般算法精度或性能。

## Upstream proof review / 上游证明复核

Read-only GitHub REST and NuGet V3 review on 2026-08-12 found no upstream identity change:

| Field | Stage 57 result |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; non-draft; non-prerelease; `immutable=false`; published/updated `2026-08-11T00:49:26Z` |
| Tag | lightweight ref to commit `673e120807d789d90a13a9f28a043282e95bb5e6`; tree `adb9f24...`; 3,559 entries; `truncated=false` |
| Release assets | 20; exact released manifest/provenance/attestation/lock/assets proof assets `=0` |
| GitHub managed package | asset ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| NuGet.org package | listed; 15,608,836 bytes; SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==` |
| NuGet registration/catalog | unchanged catalog URL and package endpoint; `Apache-2.0`; repository commit `673e120...`; retained contentHash and Repository signature unchanged |
| Tag-tree provenance | zero `packages.lock.json`; zero `project.assets.json`; two generic freeze-validation source files are not released proof |
| GitHub attestations | GitHub-asset and NuGet-package SHA256 subjects both returned HTTP 404 |

The retained admission JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. Blocker delta is retained 2/new 0/disappeared 0. No nupkg was downloaded, the eight-class TensorRT package admission was not rerun, and the retained JSON was not rewritten. Mutable prose and the generic tag-tree validation sources do not establish an immutable released proof. / 上游 identity 未变化；不下载 nupkg、不重跑八类 admission、不改写 retained JSON。mutable prose 与通用验证源码不能替代不可变发布证明。

Formal publication remains blocked only by:

1. an immutable cross-channel manifest binding repository/tag/commit, GitHub asset, and NuGet.org signed package size/SHA256/SHA512/contentHash/catalog/signature;
2. same-build immutable provenance/attestation binding commit, lock/assets/build inputs, released assets, and exact output hashes.

## Retained pure-package proof / 保留的纯包证明

The exact signed nupkg still matches 15,608,836 bytes, SHA256 `92bc1064...e038`, and raw SHA512 `9VPO6fsj...Zw==`. The Stage 55 evidence remains 3,952 bytes with SHA256 `c87e10bc5796933e6eb56a8a9f05e6aaae3fc825b0ec1fa496d41a910efe747e`. It records the signed package/contentHash/signature, a local-only three-package source, matching lock/assets resolution, zero native assets, matching output-assembly hashes, zero warnings/errors, and marker `DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK native=consumer-owned engine=external builder=onnx-managed cuda-rtc=managed-contract gpu=not-run`. / 精确 signed nupkg 与纯包 evidence 均保持 identity，local-only restore、lock/assets、assembly hash 和 marker 继续为 retained pass。

The package, consumer source, candidate packages, and restore identity did not change. The consumer is therefore `retained pass`, not a Stage 57 current execution. The unsigned 15,595,749-byte GitHub asset and rejected Stage 40 global-cache identity were not substituted. / package、consumer、candidate 与 restore identity 未变化，因此纯包 consumer 本轮不重跑，也不冒充 current execution；未替换为 unsigned GitHub asset 或 Stage 40 旧 cache identity。

## Retained real-GPU proof / 保留的真实 GPU 证明

The Stage 55 success JSON remains 20,589 bytes with SHA256 `20d21872357f40d8d5d260c948df7640dc7fe3cb6424515377569e14da168495`; the fault JSON remains 11,532 bytes with SHA256 `ab7d0bb792b9ff896183c70cf3cdf48ec4ee9803569b36ffe1f0497a090d2eab`. They bind the authorized kernel and MNIST ONNX, managed code/API, RTX 3060 Laptop GPU UUID/`sm_86`, driver 576.02, CUDA/NVRTC 12.9, TensorRT 10.11.0.33, cuDNN 9.22, native binary hashes, bridge package/DLL hashes, compiler options, in-memory artifacts, and cache/launch identities. / 两份 GPU evidence 的大小与 hash 不变，并完整绑定 kernel/model、managed、GPU/runtime/native/bridge 与 cache/launch identity。

The retained pass covers CUDA/RTC preprocessing and postprocessing, active-launch disposal, ONNX build, and TensorRT inference. Preprocessing/postprocessing use grid `[1,1,1]`, block `[64,1,1]`; the independent fault launch uses block `[32,1,1]`. The retained expected failure is CUDA error 700 at caller-managed synchronization, followed by successful disposal; unexpected failures are zero. No GPU/code/API/runtime/native identity changed, so real GPU execution was not rerun. / retained pass 覆盖 CUDA/RTC 前后处理、active-launch disposal、ONNX build 与 TensorRT inference；同步 fault 为 expected failure，unexpected failure 为 0。identity 未变化，因此不重复执行真实 GPU。

## Managed contract, ownership, and keys / Managed 合同、所有权与 key

The net8 public contract remains 215 members with SHA256 `d5b74032d2a0da2926595bc8db184aa3a1aa6b3f43ee97d60446594ad1c82452`. Core and ModelPack contain no TensorRT dependency. DeploySharp owns managed wrappers, builder temporary-write lifecycle, loaded modules, and launch owners. The caller owns TensorRT/CUDA/NVRTC/cuDNN/driver/bridge files, streams/device memory, kernels/models/ONNX, generated engines/plans, and External/local cache paths. / public API、Core/ModelPack 隔离与 ownership 均不变。

The kernel cache key binds source, ordered headers, full options, artifact hash/kind, compiler version/binary, target, kernel entry, CUDA runtime version/binary, driver version/binary, GPU architecture/UUID, and native-bridge package/DLL identity. Launch identity separately binds artifact/kernel, grid/block/shared memory, synchronization mode, hashed scalars, buffer descriptors/ranges/access, and device ordinals. PTX remains in memory, temporary engines are caller-selected External data, cache-entry remains none, and no persistent cache writer exists. / cache key 与 launch identity 字段保持完整；PTX 只在内存中、临时 engine 属于 External，cache-entry 为 none，不存在长期 cache writer。

## Verification ledger / 验证账本

| Gate | Classification | Result |
| --- | --- | --- |
| Focused TensorRT managed tests | current execution | pass 17 / skip 0 / fail 0 |
| Inventory `-Check` | current execution | pass; 69 entries |
| Exact Qwen admission/hash | current execution | pass; `ADMITTED missing=none`; GGUF, five source sidecars, and 7,364-byte Stage 31 evidence match |
| Git/upstream freshness | current execution, read-only | pass; local/tracking/remote branch commit `4708561...b2b0`; TensorRT upstream identity unchanged |
| Pure-package consumer | retained evidence | pass; exact NuGet.org signed package only |
| Real CUDA/RTC and TensorRT | retained evidence | pass; exact Stage 55 matrix only |
| Synchronization fault | retained expected failure | expected failure observed; unexpected failure 0 |
| Dual candidate pack, Stage 35/36, full solution, NuGet audits | skipped by incremental rule | no code/API/package-graph or upstream package-identity change |
| Eight-class TensorRT package admission | skipped by incremental rule | upstream package identity unchanged |
| Formal publication | blocked | two immutable upstream proofs missing |

No source, public API, package graph, dependency, model, sidecar, Stage 31 evidence, ModelPack Manifest, inventory, official catalog, or retained JSON changed. No package/tool/model was downloaded. No new package, engine, plan, PTX, CUBIN, fatbin, cache entry, consumer sandbox, or audit directory was retained. No commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 本轮仅更新治理文档；无源码/模型/证据改写、下载、临时工件保留或 Git/GitHub 发布写入。
