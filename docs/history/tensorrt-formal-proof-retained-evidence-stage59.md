# TensorRT formal proof and retained-evidence incremental admission Stage 59 / TensorRT 正式证明与保留证据增量准入阶段 59

Stage 59 is an identity-driven maintenance review. TensorRT inference, ONNX-to-engine building, independent CUDA/RTC preprocessing/postprocessing, and the exact repository-signed-package consumer remain admitted. On 2026-08-13 the Owner accepted the two missing upstream supply-chain proofs as residual risk; DeploySharp package publication may therefore proceed with zero publication blockers. The proofs remain missing and no cross-channel same-build equivalence is claimed. / Stage 59 是 identity 驱动的维护复核。TensorRT inference、ONNX 转 engine、独立 CUDA/RTC 前后处理与精确 repository-signed-package consumer 均继续处于已准入状态。Owner 于 2026-08-13 接受两项缺失上游供应链 proof 的残余风险，因此 DeploySharp 包发布可继续，当前发布 blocker 为 0。proof 仍未提供，也不声明跨渠道同次构建等价。

## Upstream proof review / 上游证明复核

Read-only GitHub REST and NuGet V3 review on 2026-08-12 found no package or publication-proof identity change:

| Field | Stage 59 result |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; target `TensorRtSharp4.0`; non-draft; non-prerelease; `immutable=false`; published/updated `2026-08-11T00:49:26Z` |
| Release body | 2,458 characters; SHA256 `f0ff3b6b7a5161fb2043019855863d81819daa36e62b1c170aa4b0bcb4ba4464`; mutable prose, not immutable proof |
| Tag | lightweight ref to commit `673e120807d789d90a13a9f28a043282e95bb5e6`; tree `adb9f24d233924739436e7b7c73c896e67d99e1e`; 3,559 entries; `truncated=false` |
| Release assets | 20; released manifest/provenance/attestation/lock/assets proof assets `=0` |
| GitHub managed package | asset ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7`; timestamps unchanged |
| NuGet.org package | listed; 15,608,836 bytes; SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==` |
| NuGet registration/catalog | catalog URL unchanged; `Apache-2.0`; repository commit `673e120...`; package hash algorithm/value unchanged |
| Package endpoint | HEAD succeeds with 15,608,836 bytes; the regional mirror exposes the same raw SHA512 in `x-ms-meta-SHA512` |
| Tag-tree build proof | exact `packages.lock.json` `=0`; exact `project.assets.json` `=0`; generic freeze/cross-task provenance source and reference files are not released proof |
| GitHub attestations | GitHub-asset and NuGet-package SHA256 subjects both returned HTTP 404 |

The flat-container request redirected to a regional mirror whose mutable `ETag` and `Last-Modified` differ from the Stage 58 transport observation. This is not a package-identity delta: canonical catalog SHA512, package length, mirror SHA512 metadata, and the retained signed nupkg SHA256/raw-SHA512 all match. No nupkg was downloaded. / flat-container 请求重定向到区域镜像，其可变 `ETag`/`Last-Modified` 与 Stage 58 的传输层观察值不同；canonical catalog hash、长度、镜像 SHA512 metadata 与本地 signed nupkg identity 全部一致，因此这不是 package identity 变化，也未下载 nupkg。

The retained admission JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. It retains its historical finding delta of retained 2/new 0/disappeared 0. The eight-class TensorRT package admission was not rerun and the retained JSON was not rewritten. Neither generic tag-tree scripts/matrices nor mutable Release prose can substitute for released immutable proof. The Owner decision changes current DeploySharp publication policy, not upstream evidence history. / retained JSON 保持逐字节 identity，并保留历史 finding delta `retained 2/new 0/disappeared 0`；本轮不重跑八类 admission、不改写 JSON。tag-tree 通用脚本/矩阵和 mutable prose 均不能替代已发布 immutable proof。Owner 决策改变的是当前 DeploySharp 发布政策，不是上游证据历史。

The following upstream proof gaps remain disclosed but no longer block DeploySharp publication:

1. an immutable cross-channel manifest binding repository/tag/commit, the GitHub asset, and the NuGet.org signed-package size/SHA256/SHA512/contentHash/catalog/signature;
2. same-build immutable provenance/attestation binding commit, lock/assets/build inputs, released assets, and exact output hashes.

Because those proofs are absent, the GitHub asset and NuGet.org package cannot be independently proven to originate from exactly the same build. This limitation is accepted, not passed or remediated. / 因两项 proof 缺失，无法独立证明 GitHub asset 与 NuGet.org 包来自完全相同的构建；这是已接受的限制，不是 proof 已通过或已修复。

## Local identity review / 本地 identity 复核

All 17 TensorRT project inputs, all three focused-test inputs, and all five pure-package consumer files match the Stage 58 per-file size/SHA256 baseline. The net8 backend DLL remains 76,288 bytes with SHA256 `c8a8246bb62d5b970bd351959293645543826c624c2239bed8e0a6849bfc298d`; the retained net8 public contract remains 215 members with SHA256 `d5b74032d2a0da2926595bc8db184aa3a1aa6b3f43ee97d60446594ad1c82452`. Core and ModelPack contain zero TensorRT references; only the isolated backend consumes centrally pinned `JYPPX.TensorRT.CSharp.API 4.0.0`. / 源码、测试、纯包 consumer、输出程序集、public API 与 package graph 均未变化；Core/ModelPack 继续无 TensorRT 依赖。

The Stage 55 success evidence was used as an identity manifest for 18 paths. CUDA runtime, driver, NVRTC/builtins, four TensorRT libraries, cuDNN, bridge package/DLL, the unsigned GPU-only managed input, its three managed DLLs, and the authorized MNIST ONNX all match their recorded size/SHA256. The two copied DeploySharp assemblies under the cleaned GPU harness `bin` directory are intentionally absent; their authoritative repository Release outputs match the recorded size/SHA256 exactly. `nvidia-smi` still reports ordinal 0, UUID `GPU-34943fb3-11cd-dd8c-7dec-248781e47353`, RTX 3060 Laptop GPU, driver 576.02, and compute capability 8.6. This check did not compile, launch, synchronize, build an engine, or run inference. / Stage 55 native/GPU 输入逐项匹配；harness `bin` 中两个复制程序集按既有清理策略不存在，但仓库权威输出精确匹配。此处只做 identity 检查，没有执行 GPU workload。

## Retained pure-package and GPU evidence / 保留的纯包与 GPU 证据

The retained NuGet.org nupkg remains exactly 15,608,836 bytes with SHA256 `92bc1064...e038` and raw SHA512 `9VPO6fsj...Zw==`. The Stage 55 pure-package evidence remains 3,952 bytes with SHA256 `c87e10bc5796933e6eb56a8a9f05e6aaae3fc825b0ec1fa496d41a910efe747e`. It records the exact contentHash and Repository signature, local-only three-package source, matching lock/assets resolution, zero native assets, output-assembly hashes, zero warnings/errors, and marker `DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK native=consumer-owned engine=external builder=onnx-managed cuda-rtc=managed-contract gpu=not-run`. Package, consumer source, candidate package, and restore identity did not change, so this is a `retained pass`; restore/build/run was not repeated. / 精确 signed package 与 pure-package evidence 不变，本轮状态为 retained pass，不重复 restore/build/run，也未以 unsigned GitHub asset 或 Stage 40 cache 替代。

The Stage 55 success JSON remains 20,589 bytes with SHA256 `20d21872357f40d8d5d260c948df7640dc7fe3cb6424515377569e14da168495`; the fault JSON remains 11,532 bytes with SHA256 `ab7d0bb792b9ff896183c70cf3cdf48ec4ee9803569b36ffe1f0497a090d2eab`. The retained pass covers real CUDA/RTC preprocessing/postprocessing, active-launch disposal, ONNX build, and TensorRT inference on the one pinned matrix. Preprocessing/postprocessing use grid `[1,1,1]`, block `[64,1,1]`; the separate fault launch uses block `[32,1,1]`. CUDA error 700 at caller-managed synchronization remains a retained expected failure followed by successful disposal; unexpected failures are zero. No GPU/code/API/runtime/native identity changed, so the GPU matrix was not rerun. / 真实 GPU 路径继续为 retained pass；同步 error 700 为 retained expected-failure，unexpected failure 为 0，本轮不重复执行 GPU matrix。

## Ownership and identities / 所有权与 identity

DeploySharp owns managed wrappers, builder temporary-write lifecycle, loaded modules, and launch owners. The consumer owns TensorRT/CUDA/NVRTC/cuDNN/driver/bridge files, streams/device memory, kernels/models/ONNX, generated engines/plans, and External/local cache paths. Native execution requires the exact consumer-supplied compatible graph, one device, explicit non-default streams/buffers, and caller authorization. / DeploySharp 与 consumer 的 ownership 边界不变；native 执行仍要求调用方提供并授权精确兼容矩阵。

The kernel cache key binds source, ordered headers, full options, artifact hash/kind, compiler version/binary, target, kernel entry, CUDA runtime version/binary, driver version/binary, GPU architecture/UUID, and native-bridge package/DLL. Launch identity separately binds artifact/kernel, grid/block/shared memory, synchronization mode, hashed scalars, buffer descriptors/ranges/access, and device ordinals. PTX stays in memory, generated engines remain temporary External data, cache-entry is none, and no persistent cache writer exists. / cache key 与 launch identity 字段保持完整；PTX 仅在内存中、engine 为临时 External 数据、cache-entry 为 none，不存在长期 cache writer。

## Verification ledger / 验证账本

| Gate | Classification | Result |
| --- | --- | --- |
| Focused TensorRT managed tests | current execution | pass 17 / skip 0 / fail 0 |
| Inventory `-Check` | current execution | pass; 69 entries |
| Exact Qwen admission/hash | current execution | pass; `ADMITTED missing=none`; GGUF, five source sidecars, and 7,364-byte Stage 31 evidence match |
| Git/upstream freshness | current execution, read-only | pass; local/tracking/remote branch commit `4708561...b2b0`; upstream proof/package identity unchanged |
| Pure-package consumer | retained evidence | pass; exact NuGet.org repository-signed package only |
| Real CUDA/RTC and TensorRT | retained evidence | pass; exact Stage 55 matrix only |
| Synchronization fault | retained expected failure | expected failure observed; unexpected failure 0 |
| Dual candidate pack, Stage 35/36, full solution, NuGet audits | skipped by incremental rule | no code/API/package-graph or upstream package-identity change |
| Eight-class TensorRT package admission | skipped by incremental rule | upstream package identity unchanged |
| Upstream publication proof | accepted gap | two immutable proofs remain missing; same-build equivalence is not claimed |
| DeploySharp package publication | permitted | Owner risk acceptance; publication blockers 0 |
| Inference/builder/CUDA-RTC/pure-package admission | not blocked | admitted execution paths retained |

No source, public API, dependency, model, sidecar, Stage 31 evidence, ModelPack Manifest, inventory, official catalog, retained JSON, or External evidence changed. No package/tool/model was downloaded. No new package, engine, plan, PTX, CUBIN, fatbin, cache entry, consumer sandbox, or audit directory was retained. No commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 本轮只更新治理文档与持久化计划；无功能、模型、证据、缓存或发布写入。
