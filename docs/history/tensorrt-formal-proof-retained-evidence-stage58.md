# TensorRT formal proof and retained-evidence incremental admission / TensorRT 正式证明与保留证据增量准入

Stage 58 is an identity-driven maintenance review. TensorRT inference, ONNX-to-engine building, independent CUDA/RTC preprocessing/postprocessing, and the exact signed-package consumer remain admitted. The word `blocked` in this record applies only to formal publication proof; it does not mean that an admitted execution path regressed or became unverified. / Stage 58 是 identity 驱动的维护复核。TensorRT inference、ONNX 转 engine、独立 CUDA/RTC 前后处理与精确 signed-package consumer 均继续处于已准入状态。本记录中的 `blocked` 只指正式发布证明，不表示已准入执行路径回退或变为未验证。

## Upstream proof review / 上游证明复核

Read-only GitHub REST and NuGet V3 review on 2026-08-12 found no identity change:

| Field | Stage 58 result |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; non-draft; non-prerelease; `immutable=false`; published/updated `2026-08-11T00:49:26Z` |
| Release body | 2,458 characters; SHA256 `f0ff3b6b7a5161fb2043019855863d81819daa36e62b1c170aa4b0bcb4ba4464`; mutable prose, not immutable proof |
| Tag | lightweight ref to commit `673e120807d789d90a13a9f28a043282e95bb5e6`; tree `adb9f24d233924739436e7b7c73c896e67d99e1e`; 3,559 entries; `truncated=false` |
| Release assets | 20; released manifest/provenance/attestation/lock/assets proof assets `=0` |
| GitHub managed package | asset ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7`; timestamps unchanged |
| NuGet.org package | listed; 15,608,836 bytes; SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==` |
| NuGet registration/catalog | catalog URL and package endpoint unchanged; `Apache-2.0`; repository commit `673e120...`; HEAD 200, length 15,608,836, ETag `0x8DEF70603ACDDF7` |
| Tag-tree build proof | exact `packages.lock.json` `=0`; exact `project.assets.json` `=0`; two generic freeze-validation source files are not released proof |
| GitHub attestations | GitHub-asset and NuGet-package SHA256 subjects both returned HTTP 404 |

The retained admission JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. The delta is retained 2/new 0/disappeared 0. No nupkg was downloaded, the eight-class TensorRT package admission was not rerun, and the retained JSON was not rewritten. The tag-tree scripts and mutable Release prose cannot substitute for released immutable proof. / retained JSON 保持逐字节 identity；本轮不下载 nupkg、不重跑八类 admission、不改写 JSON。tag-tree 脚本和 mutable Release prose 均不能替代已发布的 immutable proof。

Formal publication remains blocked only by:

1. an immutable cross-channel manifest binding repository/tag/commit, GitHub asset, and NuGet.org signed-package size/SHA256/SHA512/contentHash/catalog/signature;
2. same-build immutable provenance/attestation binding commit, lock/assets/build inputs, released assets, and exact output hashes.

## Retained pure-package proof / 保留的纯包证明

The retained NuGet.org package exactly matches 15,608,836 bytes, SHA256 `92bc1064...e038`, and raw SHA512 `9VPO6fsj...Zw==`. The Stage 55 pure-package evidence remains 3,952 bytes with SHA256 `c87e10bc5796933e6eb56a8a9f05e6aaae3fc825b0ec1fa496d41a910efe747e`. It records the exact contentHash and Repository signature, a local-only three-package source, matching lock/assets resolution, zero native assets, matching output-assembly hashes, zero warnings/errors, and marker `DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK native=consumer-owned engine=external builder=onnx-managed cuda-rtc=managed-contract gpu=not-run`. / 精确 signed package 与 pure-package evidence 的 identity 不变，local-only source、lock/assets、assembly hash 和 marker 均继续为 retained pass。

Package, consumer source, candidate package, and restore identity did not change, so the pure-package consumer was not rerun. Its Stage 58 classification is `retained pass`, not `blocked` and not a new current execution. The unsigned 15,595,749-byte GitHub asset and the rejected Stage 40 global-cache identity were not substituted. / package、consumer、candidate 与 restore identity 未变化，因此不重复运行；状态是 retained pass，不是 blocked，也不冒充本轮执行。

## Retained real-GPU proof / 保留的真实 GPU 证明

The Stage 55 success JSON remains 20,589 bytes with SHA256 `20d21872357f40d8d5d260c948df7640dc7fe3cb6424515377569e14da168495`; the fault JSON remains 11,532 bytes with SHA256 `ab7d0bb792b9ff896183c70cf3cdf48ec4ee9803569b36ffe1f0497a090d2eab`. They bind the authorized kernel and MNIST ONNX, managed assemblies/API, RTX 3060 Laptop GPU UUID/`sm_86`, driver 576.02, CUDA/NVRTC 12.9, TensorRT 10.11.0.33, cuDNN 9.22, and every recorded native/bridge binary identity. / 两份 GPU evidence 的大小/hash 不变，并绑定授权 kernel/ONNX、managed/API、GPU/runtime 与全部 native/bridge binary identity。

The retained pass covers real preprocessing/postprocessing, active-launch disposal, ONNX build, and TensorRT inference. Preprocessing/postprocessing use grid `[1,1,1]`, block `[64,1,1]`; the separate fault launch uses block `[32,1,1]`. CUDA error 700 at caller-managed synchronization remains a retained expected failure followed by successful disposal; unexpected failures are zero. No GPU/code/API/runtime/native identity changed, so the GPU matrix was not rerun. / 真实路径继续为 retained pass；同步 error 700 是 retained expected-failure，unexpected failure 为 0。identity 未变化，因此不重复执行 GPU matrix。

## Public API, ownership, and identities / 公共 API、所有权与 identity

The net8 public contract remains 215 members with SHA256 `d5b74032d2a0da2926595bc8db184aa3a1aa6b3f43ee97d60446594ad1c82452`. Core and ModelPack contain zero TensorRT references. DeploySharp owns managed wrappers, builder temporary-write lifecycle, loaded modules, and launch owners. The consumer owns TensorRT/CUDA/NVRTC/cuDNN/driver/bridge files, streams/device memory, kernels/models/ONNX, generated engines/plans, and External/local cache paths. / public API、Core/ModelPack 隔离与 ownership 均不变。

The kernel cache key binds source, ordered headers, full options, artifact hash/kind, compiler version/binary, target, kernel entry, CUDA runtime version/binary, driver version/binary, GPU architecture/UUID, and native-bridge package/DLL. Launch identity separately binds artifact/kernel, grid/block/shared memory, synchronization mode, hashed scalars, buffer descriptors/ranges/access, and device ordinals. PTX stays in memory, generated engines remain temporary External data, cache-entry is none, and no persistent cache writer exists. / cache key 与 launch identity 字段保持完整；PTX 仅在内存中、engine 为临时 External 数据、cache-entry 为 none，不存在长期 cache writer。

## Verification ledger / 验证账本

| Gate | Classification | Result |
| --- | --- | --- |
| Focused TensorRT managed tests | current execution | pass 17 / skip 0 / fail 0 |
| Inventory `-Check` | current execution | pass; 69 entries |
| Exact Qwen admission/hash | current execution | pass; `ADMITTED missing=none`; GGUF, five source sidecars, and 7,364-byte Stage 31 evidence match |
| Git/upstream freshness | current execution, read-only | pass; local/tracking/remote branch commit `4708561...b2b0`; upstream identity unchanged |
| Pure-package consumer | retained evidence | pass; exact NuGet.org repository-signed package only |
| Real CUDA/RTC and TensorRT | retained evidence | pass; exact Stage 55 matrix only |
| Synchronization fault | retained expected failure | expected failure observed; unexpected failure 0 |
| Dual candidate pack, Stage 35/36, full solution, NuGet audits | skipped by incremental rule | no code/API/package-graph or upstream package-identity change |
| Eight-class TensorRT package admission | skipped by incremental rule | upstream package identity unchanged |
| Formal publication proof | blocked | only the two immutable upstream proofs are missing |
| Inference/builder/CUDA-RTC/pure-package admission | not blocked | admitted execution paths retained |

No source, public API, package graph, dependency, model, sidecar, Stage 31 evidence, ModelPack Manifest, inventory, official catalog, or retained JSON changed. No package/tool/model was downloaded. No new package, engine, plan, PTX, CUBIN, fatbin, cache entry, consumer sandbox, or audit directory was retained. No commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 本轮只更新治理文档；无源码、模型、证据、缓存或发布写入。
