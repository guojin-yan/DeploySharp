# TensorRT release proof recheck / TensorRT 发布证明再复核

Stage 47 checks only the two unresolved Stage 46 proofs. Both remain absent while the public Release and exact NuGet.org package remain unchanged. Admission therefore stays **blocked**; the resolved Stage 42 package-license and source-owner decision entries are not reopened. / 阶段 47 只检查 Stage 46 剩余的两项 proof。两项仍缺失，Release 与精确 NuGet.org 包均无变化，因此准入继续 **blocked**，Stage 42 已解决项不回退。

## Formal Release state / 正式 Release 状态

| Field | Stage 47 read-only value |
| --- | --- |
| Release | ID `368273346`; `v4.0.0`; non-draft; non-prerelease |
| Timestamps | created `2026-08-10T17:08:13Z`; published/updated `2026-08-11T00:49:26Z` |
| Immutable | `false` |
| Repository/tag/commit | `https://github.com/guojin-yan/TensorRT-CSharp-API`; `v4.0.0`; `673e120807d789d90a13a9f28a043282e95bb5e6` |
| Assets | 20: 19 nupkgs plus `TensorRtSharp4.0-source-4.0.0.zip` |
| Proof assets | 0 |
| GitHub managed asset | ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| Tag tree | SHA `adb9f24d233924739436e7b7c73c896e67d99e1e`; 3,559 entries; not truncated |
| Lock/assets | `packages.lock.json=0`; `project.assets.json=0` |
| Generic proof | release manifest/provenance/attestation paths `=0` |

The Release still has no machine-readable manifest binding repository/tag/commit, the GitHub unsigned managed asset's size/SHA256/SHA512/contentHash, and the repository-signed NuGet.org package's size/SHA256/SHA512/contentHash/catalog URL/signature state. It also has no same-build immutable lock/assets or equivalent attestation. Release prose and temporary consumer restore output are not substitutes. / Release 仍没有跨渠道 machine manifest，也没有同次构建 immutable provenance；正文与临时 consumer restore 不能替代。

## Exact package identity / 精确包身份

The NuGet.org-only isolated restore remains:

- 15,608,836 bytes;
- SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`;
- raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`;
- contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`;
- catalog URL `https://api.nuget.org/v3/catalog0/data/2026.08.10.17.36.39/jyppx.tensorrt.csharp.api.4.0.0.json`;
- Repository certificate SHA256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`.

Nuspec `Apache-2.0`, repository commit, 15 TFMs, 45 managed DLLs, no native/model/engine/plan payload, 311 exported net8 types, 4,374 public declared methods, PE/XML contracts, and signature verification pass unchanged. The temporary lock resolved exact `4.0.0` with the same contentHash but proves feed resolution only. / 包内许可证、repository、TFM/API、纯 managed payload 与签名门保持通过；临时 lock 只证明 feed resolution。

## Blocker delta / Blocker 变化

| Blocker | State | Remediation |
| --- | --- | --- |
| `formal-v4.0.0-release-package-binding-incomplete` | retained | Freeze Release `368273346` and attach the complete cross-channel manifest. |
| `package-build-lock-assets-unavailable` | retained | Attach same-build lock/assets or equivalent immutable attestation bound to commit `673e120...` and exact output hashes. |

Stage 47 delta is retained 2, new 0, disappeared 0. Package license and source-owner decision remain historical disappeared entries. Because package and binding identities did not change, retained JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0` and is not rewritten. / 本轮 retained 2、new 0、disappeared 0；retained JSON 不改写。

## Ownership and runtime / 所有权与运行时

No `JYPPX.DeploySharp.Backend.TensorRT` project, reference, TFM, public API, DeploySharp lock/assets, consumer, native probe, engine, plan, cache key, or GPU evidence is created. Core and ModelPack remain TensorRT-free. TensorRT/CUDA/cuDNN/NVIDIA driver/native bridge remain consumer-owned; `.engine/.plan` remains device/runtime/profile-bound External local cache and cannot enter NuGet, the official catalog, inventory, or general Release assets. TensorRT-LLM remains out of scope. / 未创建适配器或运行时产物；native graph 由 consumer 持有，`.engine/.plan` 只能是 External 本地缓存。

No authorized exact ONNX, unique CUDA/cuDNN/TensorRT/bridge matrix, matching GPU, or recordable runtime identity was supplied. Engine build/cache/infer and algorithm/performance validation remain blocked/skipped without CPU/mock/ORT substitution. A future cache key must bind model SHA256, runtime versions, GPU compute capability, OS/architecture, precision, profiles/shapes, builder/workspace flags, network metadata, and adapter schema version. / 缺少精确 ONNX 与唯一 runtime/GPU identity，因此 engine/cache/infer 与算法/性能继续 blocked/skip。

## Focused verification / 聚焦验证

- Release/tag/tree checks pass; formal proof remains blocked.
- Exact official-feed restore and package identity pass.
- Baseline reports exactly two blockers; `-RequireAdmitted` is an expected failure.
- Eight negative scenarios pass 8/8; Repository signature verification passes.
- Stage 35 passes 9 packages, 82 TFMs, 9 lock/assets pairs, and 5/5 negatives. Local candidates remain ineligible only for `dirty-worktree,unsigned-packages`, with `signed=0`.
- Stage 36 passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX rows, 20 retained license-review rows, 82/82 SourceLink/PDB/API contracts, and 7/7 negatives.
- The full solution passes `378 passed / 50 skipped / 0 failed` under single-node scheduling.
- Inventory `-Check` passes with 69 entries and 56 structured manifests. Exact Qwen admission is `ADMITTED missing=none`; its GGUF, five source sidecars, and Stage 31 evidence retain exact size/SHA256. Qwen stays External with `AlgorithmVerified=false`, `uploaded=false`, and `downloadable=false`; the official catalog remains empty.
- Solution NuGet reports return zero vulnerable rows, zero deprecated rows, and 113 outdated rows. No dependency is upgraded.
- Stage 36 retained evidence remains 177,618 / 518,847 / 146,900 bytes with SHA256 `3fad8a44644e6dc94e2d2642cdfb55e080e87b7787a373a2e13f5d8111f89b0f`, `3d471a11ed0a95e298d0f709a8275effb9af175d1033466d9b6e8a5b14bea0c4`, and `e895e23babf21a0b5fe1f4370c93c79b03c53ade647db70804adc2f38eb8150c`.
- Isolated NuGet cache, temporary lock/assets, mutations, and the Stage 47 validation pack are removed after verification.

Pass/skip/failure classification is: package, signature, Stage 35/36, solution, inventory, Qwen, and report commands pass; 50 environment-gated tests and unauthorized GPU/engine work skip; `-RequireAdmitted` and both external proofs are expected failures/blockers; unexpected failures are zero. No model/tool download, dependency upgrade, Git write, or GitHub write occurred. TensorRT algorithm and performance are not validated. / 规定门禁均通过；50 项环境门控测试及未授权 GPU/engine 工作跳过；强制准入与两项 proof 为预期失败/阻断；非预期失败为 0。未验证 TensorRT 算法或性能。
