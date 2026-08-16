# TensorRT retained evidence and accepted supply-chain gaps Stage 60 / TensorRT 保留证据与已接受供应链缺口阶段 60

Stage 60 is an identity-driven maintenance review after the 2026-08-13 Owner risk-acceptance decision. TensorRT inference, ONNX-to-engine building, independent CUDA/RTC preprocessing/postprocessing, the exact repository-signed-package consumer, and DeploySharp package publication remain admitted. The two absent upstream proofs remain disclosed `accepted gaps`; current DeploySharp publication blocker count is zero. Missing proof is not reported as passed, and cross-channel same-build equivalence is not claimed. / Stage 60 是 2026-08-13 Owner 风险接受后的 identity 驱动维护复核。TensorRT inference、ONNX 转 engine、独立 CUDA/RTC 前后处理、精确 repository-signed-package consumer 与 DeploySharp 包发布均继续准入。两项缺失上游 proof 继续作为 `accepted gap` 披露，当前 DeploySharp publication blocker 为 0；不把缺失写成通过，也不声明跨渠道同次构建等价。

## Upstream identity review / 上游 identity 复核

Read-only GitHub REST and NuGet V3 review on 2026-08-13 found no package or proof identity change:

| Field | Stage 60 result |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; target `TensorRtSharp4.0`; non-draft; non-prerelease; `immutable=false`; published/updated `2026-08-11T00:49:26Z` |
| Release body | 2,458 characters; SHA256 `f0ff3b6b7a5161fb2043019855863d81819daa36e62b1c170aa4b0bcb4ba4464`; mutable prose |
| Tag | lightweight ref to commit `673e120807d789d90a13a9f28a043282e95bb5e6`; tree `adb9f24d233924739436e7b7c73c896e67d99e1e`; 3,559 entries; `truncated=false` |
| Release assets | 20; released manifest/provenance/attestation/lock/assets proof assets `=0` |
| GitHub managed package | asset ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7`; timestamps unchanged |
| NuGet.org package | listed; 15,608,836 bytes; SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==` |
| NuGet registration/catalog | catalog URL unchanged; `Apache-2.0`; repository commit `673e120...`; SHA512 algorithm/value unchanged |
| Package endpoint | canonical endpoint still redirects regionally; mirror returns 15,608,836 bytes and matching `x-ms-meta-SHA512` |
| Tag-tree build proof | exact `packages.lock.json` `=0`; exact `project.assets.json` `=0`; generic freeze/cross-task provenance files are not released proof |
| GitHub attestations | GitHub-asset and NuGet-package SHA256 subjects both returned HTTP 404 |

Regional routing and mutable CDN headers remain transport metadata, not package identity. The catalog SHA512, mirror SHA512, package length, and retained signed nupkg SHA256/raw-SHA512 all match. No nupkg was downloaded. / 区域路由与可变 CDN header 继续只属于 transport metadata；catalog/mirror SHA512、包长度与 retained signed nupkg hash 全部匹配，本轮未下载 nupkg。

The retained admission JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. It preserves the historical audit finding delta `retained 2/new 0/disappeared 0` and pre-risk-acceptance decision fields. It was not rewritten. Current policy separately classifies those two findings as accepted, non-blocking gaps and reports DeploySharp publication blocker `0`. / retained JSON 保持逐字节 identity，保留风险接受前的历史 finding delta 与 decision 字段且不改写；当前政策在其外部将两项 finding 分类为已接受的非阻断缺口，DeploySharp publication blocker 为 0。

The disclosed gaps remain:

1. no immutable cross-channel manifest binds repository/tag/commit, the GitHub asset, and the NuGet.org signed-package size/SHA256/SHA512/contentHash/catalog/signature;
2. no same-build immutable provenance/attestation binds commit, lock/assets/build inputs, released assets, and exact output hashes.

Their absence means that the GitHub asset and NuGet.org package cannot be independently proven to originate from exactly the same build. The Owner accepts this limitation for DeploySharp publication; the limitation is not remediated or passed. / 两项缺失意味着无法独立证明 GitHub asset 与 NuGet.org 包来自完全相同的构建；Owner 接受该发布风险，但缺口没有被修复或通过。

## Local identity review / 本地 identity 复核

All 17 TensorRT backend inputs, all three focused-test inputs, and all five pure-package consumer files match the Stage 59 per-file size/SHA256 baseline. The three Stage 36 evidence files remain 181,042/148,680/543,053 bytes with SHA256 `c16e3823...d62`/`6b05cd52...af0`/`96adf252...c87`. The net8 backend DLL remains 76,288 bytes with SHA256 `c8a8246bb62d5b970bd351959293645543826c624c2239bed8e0a6849bfc298d`. The public contract remains 215 members with SHA256 `d5b74032d2a0da2926595bc8db184aa3a1aa6b3f43ee97d60446594ad1c82452`. Core and ModelPack contain zero TensorRT references; only the isolated backend consumes centrally pinned `JYPPX.TensorRT.CSharp.API 4.0.0`. / backend/test/consumer、Stage 36 evidence、输出 DLL、public API 与 package graph 均未变化；Core/ModelPack 继续无 TensorRT 引用。

The retained repository-signed nupkg exactly matches 15,608,836 bytes, SHA256 `92bc1064...e038`, and raw SHA512 `9VPO6fsj...Zw==`. Sixteen existing CUDA/driver/NVRTC/TensorRT/cuDNN/bridge/managed-input/model paths from the Stage 55 success evidence match their recorded size/SHA256 with zero mismatch. The two copied DeploySharp assemblies below the cleaned harness `bin` path remain intentionally absent; their authoritative repository Release outputs exactly match the recorded size/SHA256. `nvidia-smi` still reports ordinal 0, UUID `GPU-34943fb3-11cd-dd8c-7dec-248781e47353`, RTX 3060 Laptop GPU, driver 576.02, and compute capability 8.6. These were identity checks only; no compile, launch, synchronization, engine build, or inference occurred. / signed nupkg、16 个现存 native/model 输入、权威 managed assemblies 与 GPU identity 全匹配；本轮只查 identity，没有执行 GPU workload。

## Retained execution evidence / 保留的执行证据

The Stage 55 pure-package evidence remains 3,952 bytes with SHA256 `c87e10bc5796933e6eb56a8a9f05e6aaae3fc825b0ec1fa496d41a910efe747e`. It records the exact NuGet.org contentHash and Repository signature, local-only three-package source, matching lock/assets resolution, zero native assets, matching output assembly hashes, zero warnings/errors, and the expected marker. Its embedded `formalPublication=blocked` fields are historical Stage 55 evidence produced before Owner risk acceptance; they are not the current publication policy and are not rewritten. Package, consumer, candidate, and restore identities are unchanged, so pure-package restore/build/run remains a `retained pass` and was not repeated. / pure-package evidence 保持精确 identity；其中风险接受前的历史 publication 字段不代表当前政策，也不改写。pure-package 状态为 retained pass，本轮不重复运行。

The Stage 55 success JSON remains 20,589 bytes with SHA256 `20d21872357f40d8d5d260c948df7640dc7fe3cb6424515377569e14da168495`; the fault JSON remains 11,532 bytes with SHA256 `ab7d0bb792b9ff896183c70cf3cdf48ec4ee9803569b36ffe1f0497a090d2eab`. The retained pass covers real CUDA/RTC preprocessing/postprocessing, active-launch disposal, ONNX build, and TensorRT inference on the single pinned matrix. Preprocessing/postprocessing use grid `[1,1,1]`, block `[64,1,1]`; the separate fault launch uses block `[32,1,1]`. CUDA error 700 at caller-managed synchronization remains a retained expected failure followed by successful disposal; unexpected failures are zero. No input/code/API/runtime/native identity changed, so the GPU matrix was not rerun. / GPU success 为 retained pass，同步 error 700 为 retained expected-failure，unexpected failure 为 0；identity 未变，因此不重跑 GPU matrix。

## API, ownership, and identities / API、所有权与 identity

DeploySharp owns managed wrappers, builder temporary-write lifecycle, loaded modules, and launch owners. The consumer owns TensorRT/CUDA/NVRTC/cuDNN/driver/bridge files, streams/device memory, kernels/models/ONNX, generated engines/plans, and External/local cache paths. Native execution continues to require the exact consumer-supplied compatible graph, one device, explicit streams/buffers, and caller authorization. / ownership 边界不变，native 执行继续要求调用方提供并授权精确兼容矩阵。

The kernel cache key binds source, ordered headers, full options, artifact hash/kind, compiler version/binary, target, kernel entry, CUDA runtime version/binary, driver version/binary, GPU architecture/UUID, and native-bridge package/DLL. Launch identity separately binds artifact/kernel, grid/block/shared memory, synchronization mode, hashed scalars, buffer descriptors/ranges/access, and device ordinals. PTX stays in memory, generated engines remain temporary External data, cache-entry is none, and no persistent cache writer exists. / cache-key/launch-key 字段保持完整；PTX 仅在内存中、engine 为临时 External 数据、cache-entry 为 none，不存在长期 cache writer。

## Verification ledger / 验证账本

| Gate | Classification | Result |
| --- | --- | --- |
| Focused TensorRT managed tests | current execution | pass 17 / skip 0 / fail 0 |
| Inventory `-Check` | current execution | pass; 69 entries |
| Exact Qwen admission/hash | current execution | pass; `ADMITTED missing=none`; GGUF, five source sidecars, and 7,364-byte Stage 31 evidence match |
| Git/upstream freshness | current execution, read-only | pass; local/tracking/remote `4708561...b2b0`; upstream identity unchanged |
| Pure-package consumer | retained evidence | pass; exact NuGet.org repository-signed package only |
| Real CUDA/RTC and TensorRT | retained evidence | pass; exact Stage 55 matrix only |
| Synchronization fault | retained expected failure | expected failure observed; unexpected failure 0 |
| Dual candidate pack, Stage 35/36, full solution, NuGet audits | skipped by incremental rule | no code/API/package-graph or upstream package-identity change |
| Eight-class TensorRT package admission | skipped by incremental rule | upstream package identity unchanged |
| Upstream immutable proofs | accepted gaps | two proofs remain missing; no same-build equivalence claim |
| DeploySharp package publication | permitted | Owner risk acceptance; publication blockers 0 |
| Inference/builder/CUDA-RTC/pure-package admission | admitted | unchanged retained execution evidence |

No source, public API, dependency, model, sidecar, Stage 31 evidence, ModelPack Manifest, inventory, official catalog, retained JSON, or External evidence changed. No package/tool/model was downloaded. No new package, engine, plan, PTX, CUBIN, fatbin, cache entry, consumer sandbox, or audit directory was retained. No commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 本轮只更新治理文档与持久化计划；无功能、模型、证据、缓存或发布写入。

## Workstream closure / 工作流收尾

Stage 60 closes the repeated retained-evidence and upstream-identity maintenance sequence. Future proof checks are release gates or responses to a real package/proof identity change; they do not consume another development Stage by themselves. Stage 61 returns to complete-module delivery with an opt-in, caller-owned TensorRT External cache store covering implementation, public contracts, tests, consumer integration, package evidence, and documentation. / Stage 60 结束重复的 retained-evidence 与上游 identity 维护序列。后续 proof 检查只属于发布门禁或真实 identity 变化响应，不再单独占用开发阶段。Stage 61 恢复完整模块交付，目标是实现调用方拥有、显式启用的 TensorRT External cache store，并一次完成源码、公共合同、测试、consumer 接入、包证据和文档。
