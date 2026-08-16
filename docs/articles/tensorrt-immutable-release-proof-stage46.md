# TensorRT immutable release proof review / TensorRT 不可变发布证明复核

Stage 46 checks only the two unresolved Stage 45 release proofs. The public Release and exact NuGet.org package remain unchanged, but neither required proof exists. Admission therefore remains **blocked**; the Stage 42 package-license and source-owner decision entries remain historical disappeared items. / 阶段 46 只复核 Stage 45 剩余两项正式证明。Release 与精确 NuGet.org 包均未变化，但两项 proof 仍不存在，因此准入继续 **blocked**；Stage 42 已消失的许可证与 Owner decision 不回退。

## Release and tag identity / Release 与 tag 身份

| Field | Stage 46 read-only value |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; non-draft; non-prerelease |
| Timestamps | created `2026-08-10T17:08:13Z`; published/updated `2026-08-11T00:49:26Z` |
| Immutable | `false` |
| Repository/tag/commit | `https://github.com/guojin-yan/TensorRT-CSharp-API`; `v4.0.0`; `673e120807d789d90a13a9f28a043282e95bb5e6` |
| Asset set | 20: 19 nupkgs plus `TensorRtSharp4.0-source-4.0.0.zip` |
| Proof assets | 0 after excluding nupkgs and source ZIP |
| GitHub managed asset | ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| Tag tree | SHA `adb9f24d233924739436e7b7c73c896e67d99e1e`; 3,559 entries; not truncated |
| Lock/assets paths | `packages.lock.json=0`; `project.assets.json=0` |
| Generic proof paths | release manifest/provenance/attestation `=0` |

No machine-readable object binds the GitHub unsigned managed asset's size/SHA256/SHA512/contentHash to the repository-signed NuGet.org package's size/SHA256/SHA512/contentHash/catalog URL/signature state. The Release body and source templates are descriptive evidence, not this cross-channel proof. / 没有 machine-readable 对象把 GitHub unsigned managed asset 与 NuGet.org repository-signed 包的完整身份联合绑定；Release 正文和源码模板不能替代该 proof。

## Exact NuGet.org package / 精确 NuGet.org 包

The authorized isolated restore produced the unchanged package:

- 15,608,836 bytes;
- SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`;
- raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`;
- contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`;
- catalog URL `https://api.nuget.org/v3/catalog0/data/2026.08.10.17.36.39/jyppx.tensorrt.csharp.api.4.0.0.json`;
- Repository signature certificate SHA256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`.

Nuspec `Apache-2.0`, repository commit, 15 TFMs, 45 managed DLLs, no native/model/engine/plan payload, 311 exported net8 types, 4,374 public declared methods, PE/XML contracts, and `dotnet nuget verify --all` pass. The temporary project resolved exact `4.0.0` and the same contentHash, but its generated lock/assets prove only this Stage 46 feed resolution and are not upstream build provenance. / 包内许可证、repository、TFM/API、纯 managed payload 与签名门均通过；临时 lock/assets 只证明本轮 feed resolution，不能替代上游同次构建 provenance。

## Blockers and remediation / 阻断与修复

| Blocker | Stage 46 state | Required remediation |
| --- | --- | --- |
| `formal-v4.0.0-release-package-binding-incomplete` | retained | Freeze Release `368273346` as immutable and attach the complete cross-channel machine manifest. |
| `package-build-lock-assets-unavailable` | retained | Attach same-release-build lock/assets or equivalent immutable attestation bound to commit `673e120...` and both output identities. |

Stage 46 blocker delta is retained 2, new 0, disappeared 0. Package-license and source-owner decision remain historical disappeared entries only. Because the nupkg and binding identity did not change, retained JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0` and is not rewritten. / 本轮 blocker delta 为 retained 2、new 0、disappeared 0；retained JSON 不改写。

## Implementation and runtime boundary / 实现与运行时边界

No `JYPPX.DeploySharp.Backend.TensorRT` project, reference, TFM, public API, DeploySharp lock/assets, consumer, native probe, engine, plan, cache key, or GPU evidence is created. Core and ModelPack remain TensorRT-free. TensorRT/CUDA/cuDNN/NVIDIA driver/native bridge remain consumer-owned; `.engine/.plan` remains device/runtime/profile-bound External local cache and never enters NuGet, the official catalog, inventory, or general Release assets. TensorRT-LLM remains out of scope. / 未创建适配器或运行时产物；native graph 继续由 consumer 持有，`.engine/.plan` 只能是 External 本地缓存。

No authorized exact ONNX, unique CUDA/cuDNN/TensorRT/bridge matrix, matching GPU identity, or recordable runtime identity was supplied. Engine build/cache/infer and algorithm/performance validation remain blocked/skipped without CPU/mock/ORT substitution. A future cache key must bind model SHA256, all runtime versions, GPU compute capability, OS/architecture, precision, profiles/shapes, builder/workspace flags, network metadata, and adapter schema version. / 未提供精确 ONNX 与唯一 runtime/GPU identity；engine/cache/infer 与算法/性能继续 blocked/skip，不能用替代后端。

## Verification / 验证

- Release/tag/tree read-only identity checks pass; formal proof remains blocked.
- Official-feed restore and exact package identity pass.
- Baseline reports exactly two blockers; `-RequireAdmitted` is an expected failure.
- Eight independent negative scenarios pass 8/8; Repository signature verification passes.
- Stage 35 passes 9 packages, 82 TFMs, 9 lock/assets pairs, and 5/5 negatives. Its local candidates remain ineligible only for `dirty-worktree,unsigned-packages`, with `signed=0`.
- Stage 36 passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX rows, 20 retained license-review rows, 82/82 SourceLink/PDB/API contracts, and 7/7 negatives.
- The full solution passes `378 passed / 50 skipped / 0 failed` under single-node scheduling.
- Inventory `-Check` passes with 69 entries and 56 structured manifests. Exact Qwen admission is `ADMITTED missing=none`; the GGUF, five source sidecars, and Stage 31 evidence retain their exact sizes/SHA256. Qwen remains External with `AlgorithmVerified=false`, `uploaded=false`, and `downloadable=false`; the official catalog remains empty.
- Solution NuGet reports return zero vulnerable rows, zero deprecated rows, and 113 outdated rows. Outdated dependencies are report-only and are not upgraded.
- The three Stage 36 retained evidence files remain 177,618 / 518,847 / 146,900 bytes with SHA256 `3fad8a44644e6dc94e2d2642cdfb55e080e87b7787a373a2e13f5d8111f89b0f`, `3d471a11ed0a95e298d0f709a8275effb9af175d1033466d9b6e8a5b14bea0c4`, and `e895e23babf21a0b5fe1f4370c93c79b03c53ade647db70804adc2f38eb8150c`.
- Isolated NuGet cache, temporary consumer lock/assets, mutation inputs, and the Stage 46 validation pack are removed after verification.

Pass/skip/failure classification is: package, signature, Stage 35/36, solution, inventory, Qwen, and report commands pass; 50 environment-gated solution tests and all unauthorized GPU/engine work skip; `-RequireAdmitted` and the two external proof conditions are expected failures/blockers; unexpected failures are zero. No model/tool download, dependency upgrade, Git write, or GitHub write occurred. TensorRT algorithm and performance are not validated. / 分类结果为：规定门禁均通过；50 项环境门控测试及未授权 GPU/engine 工作跳过；强制准入与两项外部 proof 为预期失败/阻断；非预期失败为 0。未执行模型/工具下载、依赖升级或 Git/GitHub 写入，也未验证 TensorRT 算法或性能。
