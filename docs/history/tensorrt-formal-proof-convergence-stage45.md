# TensorRT formal proof convergence / TensorRT 正式证明收敛

Stage 45 checks only the two unresolved Stage 44 release proofs. Neither proof appeared, while the exact NuGet.org package stayed unchanged. Admission therefore remains **blocked**, and the resolved package-license and source-owner decision entries are not reopened. / 阶段 45 只检查 Stage 44 尚未解决的两项发布证明。两项 proof 均未出现，精确 NuGet.org 包也未变化，因此准入继续 **blocked**，不会重新打开已解决的 package license 与 source Owner decision。

## Release manifest and provenance / Release manifest 与 provenance

| Field | Read-only Stage 45 value |
| --- | --- |
| Release | ID `368273346`, `v4.0.0`, non-draft, non-prerelease |
| Release timestamps | created `2026-08-10T17:08:13Z`; published/updated `2026-08-11T00:49:26Z` |
| Immutable | `false` |
| Repository/tag/commit | `https://github.com/guojin-yan/TensorRT-CSharp-API`; `v4.0.0`; `673e120807d789d90a13a9f28a043282e95bb5e6` |
| Assets | 20 total: 19 nupkgs plus `TensorRtSharp4.0-source-4.0.0.zip` |
| Proof assets | 0 after excluding the nupkgs and source ZIP |
| GitHub managed asset | 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| Tag-tree build sources | `packages.lock.json=0`; `project.assets.json=0` |
| Generic release proof paths | release manifest/provenance/attestation paths `=0` |

The required manifest fields therefore do not exist: GitHub asset SHA512/contentHash, NuGet.org package size/SHA256/SHA512/contentHash/catalog URL/signature state, and their same-build relationship are not jointly bound. Release prose, source templates, scripts, tests, native manifests, and temporary consumer lock/assets are not substitutes. / 必需的 machine manifest 字段仍不存在：GitHub asset SHA512/contentHash、NuGet.org 包完整身份及二者同次构建关系没有联合绑定。Release 正文、源码模板、脚本、测试、native manifest 与临时 consumer lock/assets 均不能替代。

## Exact package identity / 精确包身份

The authorized NuGet.org-only restore produced the unchanged package:

- 15,608,836 bytes;
- SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`;
- raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`;
- contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`;
- catalog URL `https://api.nuget.org/v3/catalog0/data/2026.08.10.17.36.39/jyppx.tensorrt.csharp.api.4.0.0.json`;
- NuGet.org Repository signature certificate SHA256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`.

Nuspec `Apache-2.0`, repository/commit, 15 TFMs, 45 managed DLLs, no NuGet dependencies, no native/model/engine/plan payload, 311 exported net8 types, 4,374 public declared methods, and required PE/XML contracts pass. `dotnet nuget verify --all` passes. The retained JSON remains unchanged at 10,200 bytes and SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. / nuspec、repository、TFM、API、PE/XML、纯 managed payload 与签名门均通过；retained JSON 的大小和 SHA256 不变。

## Blocker delta / Blocker 变化

| Blocker | Stage 45 state | Remediation |
| --- | --- | --- |
| `formal-v4.0.0-release-package-binding-incomplete` | retained | Freeze the Release as immutable and attach the complete cross-channel manifest described above. |
| `package-build-lock-assets-unavailable` | retained | Attach same-release-build lock/assets or equivalent immutable attestation bound to commit `673e120...` and both exact output identities. |

Stage 45 new blockers: 0. Stage 45 disappeared blockers: 0. Historical disappeared entries remain package license metadata and source Owner decision; neither is a current blocker. / Stage 45 新增和消失 blocker 均为 0；package license 与 source Owner decision 继续只是历史 disappeared 项。

## Implementation and runtime boundary / 实现与运行时边界

Because both blockers remain, no `JYPPX.DeploySharp.Backend.TensorRT` project, package/reference, TFM, public API, DeploySharp lock/assets, package-only consumer, native probe, engine, plan, cache key, or GPU evidence was created. Core and ModelPack remain TensorRT-free. TensorRT/CUDA/cuDNN/NVIDIA driver/native bridge remain consumer-owned; `.engine/.plan` remains device/runtime/profile-bound External local cache and never enters NuGet, the official catalog, general Release assets, or inventory. TensorRT-LLM remains out of scope. / 两项 blocker 仍存在，因此未创建任何适配器、引用/API/TFM、DeploySharp lock/assets、consumer、native probe、engine/plan/cache key 或 GPU 证据；native graph 继续由 consumer 持有，`.engine/.plan` 只能是 External 本地缓存，不创建 TensorRT-LLM。

No exact authorized local ONNX, unique CUDA/cuDNN/TensorRT/bridge matrix, matching GPU identity, or recordable runtime identity was supplied. Engine build/cache/infer and algorithm/performance validation are blocked/skipped, without CPU/mock/ORT substitution. The future cache key remains design-only and must bind model SHA256, all runtime versions, GPU compute capability, OS/architecture, precision, profiles/shapes, builder/workspace flags, network metadata, and adapter schema version. / 未提供精确 ONNX 和唯一 runtime/GPU identity，因此 engine/cache/infer 与算法/性能验证 blocked/skip，不使用替代后端；cache key 继续只是设计合同。

## Focused verification / 聚焦验证

- Release/tag/tree read-only identity checks pass; formal proof remains blocked.
- Official-feed restore and exact package identity pass.
- TensorRT baseline reports exactly two blockers; `-RequireAdmitted` is an expected failure.
- Eight independent negative scenarios pass 8/8; `dotnet nuget verify --all` passes.
- Stage 35 passes 9 packages, 82 TFMs, 9 lock/assets pairs, and 5/5 negatives; its local candidates remain ineligible only for `dirty-worktree,unsigned-packages` and report `signed=0`.
- Stage 36 passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX rows, 20 retained license-review rows, 82/82 SourceLink/PDB/API contracts, and 7/7 negatives. Its three retained evidence files keep their exact sizes and hashes.
- The full solution passes `378 passed / 50 skipped / 0 failed` under single-node scheduling. An initial parallel run left a stalled vstest process after the command wrapper timed out; that exact process tree was removed and the deterministic rerun passed, so unexpected assertion failures remain zero.
- Inventory `-Check` passes with 69 entries and 56 structured manifests. Exact Qwen admission is `ADMITTED missing=none`; the GGUF, five source sidecars, and Stage 31 evidence retain their exact sizes/SHA256. It remains External with `AlgorithmVerified=false`, `uploaded=false`, and `downloadable=false`; the official catalog remains empty.
- Solution NuGet reports return zero vulnerable rows, zero deprecated rows, and 113 outdated rows. Outdated dependencies are report-only and are not upgraded.
- Isolated package cache, temporary consumer lock/assets, TensorRT mutations, the Stage 45 validation pack, and the recovered stalled test process tree are removed. No model/tool download, dependency upgrade, Git write, or GitHub write occurred.

Pass/skip/failure classification is therefore: package, signature, Stage 35/36, solution, inventory, Qwen, and report commands pass; 50 environment-gated solution tests and all unauthorized GPU/engine work skip; `-RequireAdmitted` and the two external proof conditions are expected failures/blockers; unexpected test assertion failures are zero. TensorRT algorithm and performance are not validated. / 分类结果为：包、签名、Stage 35/36、全解、inventory、Qwen 和报告命令通过；50 项环境门控测试及未授权 GPU/engine 工作跳过；`-RequireAdmitted` 与两项外部证明是预期失败/阻断；非预期测试断言失败为 0。TensorRT 算法与性能未验证。
