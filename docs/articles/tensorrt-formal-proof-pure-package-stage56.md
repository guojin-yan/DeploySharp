# TensorRT formal-proof and exact pure-package incremental admission / TensorRT 正式证明与精确纯包增量准入

Stage 56 is an identity-driven maintenance review. It keeps the admitted inference, ONNX builder, and independent CUDA/RTC managed APIs intact; it does not rerun real GPU or pure-package execution when their complete inputs are unchanged. Stage 55 remains the execution proof for its exact matrix, not a claim of general accuracy or performance. / Stage 56 是 identity 驱动的维护复核。inference、ONNX builder 与独立 CUDA/RTC managed API 均不回退；完整输入未变化时不重复运行真实 GPU 或纯包路径。Stage 55 继续作为精确 matrix 的执行证明，不推导一般精度或性能结论。

## Upstream proof review / 上游证明复核

Read-only GitHub REST and NuGet V3 review on 2026-08-12 found no identity change:

| Field | Stage 56 result |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; non-draft; non-prerelease; `immutable=false`; published/updated `2026-08-11T00:49:26Z` |
| Tag | lightweight ref to commit `673e120807d789d90a13a9f28a043282e95bb5e6`; non-truncated tree, 3,559 entries |
| Release assets | 20; exact proof-named manifest/provenance/attestation/lock/assets assets `=0` |
| GitHub managed package | asset ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| NuGet.org package | listed; 15,608,836 bytes; SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==` |
| NuGet catalog | unchanged catalog URL; `Apache-2.0`; repository commit `673e120...`; retained contentHash and Repository signature unchanged |
| Tag-tree provenance | zero `packages.lock.json`; zero `project.assets.json`; two generic freeze-validation source files are not released proof |
| GitHub attestations | GitHub-asset and NuGet-package SHA256 subjects both returned HTTP 404 |

The retained admission JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. Blocker delta is retained 2/new 0/disappeared 0. No nupkg was downloaded, the eight-class TensorRT package admission was not rerun, and the retained JSON was not rewritten. / 上游 identity 未变化；不下载 nupkg、不重跑八类 admission、不改写 retained JSON。

Formal publication remains blocked only by:

1. an immutable cross-channel manifest binding repository/tag/commit, GitHub asset, and NuGet.org signed package size/SHA256/SHA512/contentHash/catalog/signature;
2. same-build immutable provenance/attestation binding commit, lock/assets/build inputs, released assets, and exact output hashes.

## Retained pure-package proof / 保留的纯包证明

The retained signed nupkg still exists under the hash-named Stage 55 External directory and matches 15,608,836 bytes, SHA256 `92bc1064...e038`, and raw SHA512 `9VPO6fsj...Zw==`. The Stage 55 evidence remains 3,952 bytes with SHA256 `c87e10bc5796933e6eb56a8a9f05e6aaae3fc825b0ec1fa496d41a910efe747e`. It records the exact signed package identity and contentHash, valid Repository signature, local-only three-package source, matching lock/assets resolution, zero native assets, matching output-assembly hashes, zero warnings/errors, and marker `DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK native=consumer-owned engine=external builder=onnx-managed cuda-rtc=managed-contract gpu=not-run`. / retained signed nupkg 与纯包 evidence 均保持精确 identity，且记录 local-only restore、lock/assets、assembly hash 与 marker 全部通过。

No package, consumer source, candidate-output identity, or restore identity changed after that run. The consumer worktree, temporary source, and isolated restore cache remain deleted. The 15,595,749-byte unsigned GitHub asset and rejected Stage 40 global-cache identity were not substituted. The pure-package consumer is therefore `retained pass`, not a Stage 56 current execution claim. / 本轮纯包状态为 retained pass，不冒充 current execution；unsigned GitHub asset 与 Stage 40 旧 identity 均未替代。

## Retained real-GPU proof / 保留的真实 GPU 证明

The Stage 55 success JSON remains 20,589 bytes with SHA256 `20d21872357f40d8d5d260c948df7640dc7fe3cb6424515377569e14da168495`; the fault JSON remains 11,532 bytes with SHA256 `ab7d0bb792b9ff896183c70cf3cdf48ec4ee9803569b36ffe1f0497a090d2eab`. They bind the exact RTX 3060 Laptop GPU UUID/`sm_86`, driver 576.02, CUDA/NVRTC 12.9, TensorRT 10.11.0.33, cuDNN 9.22, native binary hashes, bridge package/DLL hashes, local MNIST ONNX hash, managed assemblies, kernel source/options, artifact hashes, and cache keys. / 两份 GPU evidence 的大小与 hash 不变，并完整绑定 GPU/runtime/native/model/managed/kernel identity。

The retained pass covers real preprocessing and postprocessing, active-launch disposal, ONNX build, and TensorRT inference. The retained expected failure is CUDA error 700 at caller-managed synchronization with successful subsequent disposal; unexpected failures are zero. The success JSON records grid `[1,1,1]`, block `[64,1,1]` for preprocessing/postprocessing, and a 32-byte Float32 `[8]` caller buffer. This corrects the Stage 55 prose-only `[32,1,1]` transcription; no execution result or evidence file changed. The fault launch separately uses block `[32,1,1]`. / success JSON 中前后处理 block 为 `[64,1,1]`；Stage 55 prose 的 `[32,1,1]` 是转录错误，本轮按 retained JSON 更正，不改变任何执行结果或 evidence。

Real GPU execution is `retained pass` and was not rerun. GPU prerequisites remain the exact authorized kernel/ONNX, code/public API, RTX 3060 UUID, runtime versions, and native/bridge binary hashes. A change to any identity requires new authorization and a Stage 55-equivalent record. / 真实 GPU 本轮为 retained pass，不重复执行；任一 identity 变化才需要重新授权并生成同等字段证据。

## Managed contract and ownership / Managed 合同与所有权

The net8 public contract remains 215 members with SHA256 `d5b74032d2a0da2926595bc8db184aa3a1aa6b3f43ee97d60446594ad1c82452`. Core and ModelPack contain no TensorRT dependency. DeploySharp owns managed wrappers, builder temporary-write lifecycle, loaded modules, and launch owners. The caller owns native TensorRT/CUDA/NVRTC/cuDNN/driver/bridge files, streams/device memory, kernel/model/ONNX, generated engine/plan, and External/local cache paths. / public API、Core/ModelPack 隔离与 ownership 均不变。

The kernel cache key binds source, ordered headers, full options, artifact hash/kind, compiler version/binary, target, kernel entry, CUDA runtime version/binary, driver version/binary, GPU architecture/UUID, and native-bridge package/DLL identity. Launch identity separately binds artifact/kernel, grid/block/shared memory, synchronization mode, hashed scalars, buffer descriptors/ranges/access, and device ordinals. No cache I/O occurs, and there is no persistent cache writer. / cache key 与 launch identity 字段完整，但不执行 I/O，也没有长期 cache writer。

## Verification ledger / 验证账本

| Gate | Classification | Result |
| --- | --- | --- |
| Focused TensorRT managed tests | current execution | pass 17 / skip 0 / fail 0 |
| Inventory `-Check` | current execution | pass; 69 entries |
| Exact Qwen admission/hash | current execution | pass; `ADMITTED missing=none`; 491,400,032 bytes; SHA256 `74a4da8...a9db` |
| Git/upstream freshness | current execution, read-only | pass; local/remote branch commit `4708561...b2b0`; TensorRT upstream identity unchanged |
| Pure-package consumer | retained evidence | pass; exact signed package only |
| Real CUDA/RTC and TensorRT | retained evidence | pass; exact Stage 55 matrix only |
| Synchronization fault | retained expected failure | expected failure observed; unexpected failure 0 |
| Dual candidate pack, Stage 35/36, full solution, NuGet audits | skipped by incremental rule | Stage 55 retained results; no code/API/package-graph or package-identity change |
| Eight-class TensorRT package admission | skipped by incremental rule | upstream package identity unchanged |
| Formal publication | blocked | two immutable upstream proofs missing |

No source, public API, package graph, dependency, model, sidecar, Stage 31 evidence, ModelPack Manifest, inventory, or catalog data changed. No package/tool/model was downloaded. No new package, engine, plan, PTX, CUBIN, fatbin, cache entry, consumer sandbox, or temporary audit directory was created; the Stage 55 External scan remains free of those temporary artifact types. No commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 本轮仅更新治理文档，不修改代码、包图、模型或证据；无临时工件和 Git/GitHub 发布写入。
