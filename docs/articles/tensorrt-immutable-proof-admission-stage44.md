# TensorRT immutable proof admission / TensorRT 不可变证明准入

Stage 44 rechecks only the two Stage 43 external proof conditions. The public `v4.0.0` Release and exact NuGet.org package identities are unchanged, so both blockers remain and no adapter is admitted. The Stage 42 package-license and source-owner decision blockers remain disappeared and are not reintroduced. / 阶段 44 只复核 Stage 43 的两项外部发布证明。公开 `v4.0.0` Release 与精确 NuGet.org 包身份均未变化，因此两项 blocker 继续保留，不准入适配器。Stage 42 的 package license 与 source Owner decision blocker 继续是 disappeared，不重新加入。

## Formal Release proof / 正式 Release 证明

| Field | Stage 44 read-only observation |
| --- | --- |
| Release | ID `368273346`, tag `v4.0.0`, non-draft, non-prerelease |
| Repository | `https://github.com/guojin-yan/TensorRT-CSharp-API` |
| Tag commit | `673e120807d789d90a13a9f28a043282e95bb5e6` |
| Target declaration | branch `TensorRtSharp4.0`; tag ref independently resolves to the commit above |
| Mutability | `immutable=false` |
| Asset list | 19 nupkgs plus `TensorRtSharp4.0-source-4.0.0.zip` |
| GitHub managed asset | 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| Missing machine proof | no manifest binding both channels; no GitHub SHA512/contentHash; no NuGet catalog/signature binding; no lock/assets/provenance/attestation asset |

The Release body declares a stable public package set and `Apache-2.0`, but prose is not a machine-readable binding. The tag commit tree has zero exact `packages.lock.json` and `project.assets.json` files. Other manifest/provenance-named files in the source tree are templates, scripts, tests, or native API records; none is an attached immutable proof for this Release and exact managed nupkg. / Release 正文声明稳定公开包集合和 `Apache-2.0`，但正文不是 machine-readable binding。tag commit tree 中精确 `packages.lock.json` 与 `project.assets.json` 均为 0。源码树中其他 manifest/provenance 文件是模板、脚本、测试或 native API 记录，不是该 Release 与精确 managed nupkg 绑定的不可变证明。

## Exact NuGet package / 精确 NuGet 包

The user-authorized restore used only `https://api.nuget.org/v3/index.json` and a system-temporary package root. The package identity remains:

- 15,608,836 bytes;
- SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`;
- raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`;
- contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`;
- NuGet.org catalog `https://api.nuget.org/v3/catalog0/data/2026.08.10.17.36.39/jyppx.tensorrt.csharp.api.4.0.0.json`;
- Repository signature subject `NuGet.org Repository by Microsoft`, certificate SHA256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`.

The nuspec still declares `Apache-2.0`, repository `https://github.com/guojin-yan/TensorRT-CSharp-API`, and commit `673e120807d789d90a13a9f28a043282e95bb5e6`. The package has 15 TFMs, 45 managed DLLs, no NuGet dependencies, no native/model/engine/plan payload, and net8 `JYPPX.TensorRtSharp` remains 311 exported types with 4,374 public declared methods. Required PE references and XML members pass. `dotnet nuget verify --all` passes. / nuspec 仍声明上述 `Apache-2.0`、正式 repository 与 `673e120...`；包含 15 TFM、45 managed DLL、无 NuGet 依赖和无 native/model/engine/plan payload，net8 API 仍为 311/4,374，PE/XML 合同与签名门通过。

The temporary consumer lock/assets record only exact feed resolution and were deleted in `finally`. They are not same-build provenance for the upstream Release. The retained TensorRT JSON was not rewritten because the package and binding identity did not change: 10,200 bytes, SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. / 临时 consumer lock/assets 只记录精确 feed resolution，并已在 `finally` 删除，不能作为上游 Release 的同次构建 provenance。包与 binding 未变，retained JSON 不改写，仍为 10,200 bytes、原 SHA256。

## Blocker delta / Blocker 变化

| Blocker | Stage 44 state | Required remediation |
| --- | --- | --- |
| `formal-v4.0.0-release-package-binding-incomplete` | retained | Make Release immutable and attach a manifest binding repository/tag/commit, GitHub unsigned asset size/SHA256/SHA512/contentHash, and the NuGet.org signed package size/SHA256/SHA512/contentHash/catalog/signature state. |
| `package-build-lock-assets-unavailable` | retained | Attach same-release-build `packages.lock.json`, `project.assets.json`, or equivalent immutable attestation bound to commit `673e120...` and exact package hashes. |

New blockers: zero. Disappeared blockers: zero in Stage 44. Historical disappeared entries remain `package-license-metadata-missing` and `source-license-owner-decision-required`; they are not current blockers. / Stage 44 新增 blocker 为 0，消失 blocker 为 0；历史 disappeared 项仍为 package license 与 source Owner decision，不是当前 blocker。

## Adapter and ownership boundary / 适配器与所有权边界

Both proof conditions remain blocked, so no `JYPPX.DeploySharp.Backend.TensorRT` project, package reference, TFM, public API, DeploySharp lock/assets, package-only consumer, native probe, engine, plan, cache key, or GPU evidence was created. Core and ModelPack remain TensorRT-free. TensorRT/CUDA/cuDNN/NVIDIA driver/native bridge remain consumer-owned; `.engine/.plan` remains device/runtime/profile-bound External local cache data and never enters NuGet, the official catalog, general Release assets, or inventory. TensorRT-LLM is outside scope. / 两项 proof 仍阻断，因此未创建隔离适配器项目、引用、TFM、公共 API、DeploySharp lock/assets、consumer、native probe、engine、plan、cache key 或 GPU 证据。Core/ModelPack 继续无 TensorRT 依赖；native 运行时由 consumer 持有，`.engine/.plan` 只能是设备/运行时/Profile 绑定的 External 本地缓存，不进入 NuGet、官方目录、通用 Release 或 inventory，不创建 TensorRT-LLM。

The future cache key remains a design contract only: exact input-model SHA256, managed API/native TensorRT versions, CUDA/cuDNN versions, GPU compute capability, OS/architecture, precision, optimization profiles/dynamic shapes, workspace/builder flags, network metadata, and adapter schema version. No key was materialized. No exact local ONNX, unique authorized GPU/runtime matrix, or recordable runtime identity was supplied, so engine build/cache/infer and algorithm/performance validation are blocked and skipped without CPU/mock/ORT substitution. / 未来 cache key 仍只是设计合同，包含模型 SHA256、managed/native 版本、CUDA/cuDNN、GPU compute capability、OS/架构、精度、Profile/动态 shape、workspace/builder flags、网络 metadata 与 adapter schema version；本轮未物化 key。没有精确 ONNX、唯一授权 GPU/runtime matrix 或可记录 identity，因此 engine/cache/infer 与算法/性能验证 blocked/skip，不用 CPU/mock/ORT 替代。

## Verification / 验证

- Release API/tag/tree proof review: pass for read-only identity checks; formal admission proof: blocked because `immutable=false` and the required manifest/provenance fields are absent.
- NuGet restore: pass from the official feed; package identity unchanged. TensorRT baseline reports exactly two blockers; `-RequireAdmitted` is an expected failure; eight independent negative scenarios pass 8/8.
- `dotnet nuget verify --all`: pass with the NuGet.org Repository signature. Static nuspec, 15 TFM, 45 DLL, PE/XML/API, and strict payload checks pass.
- Stage 35: fresh pack exits zero and passes 9 packages, 82 TFMs, 9 locks/assets, and 5/5 negative scenarios. Release eligibility remains false only for the retained dirty worktree and nine unsigned DeploySharp packages.
- Stage 36: passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX rows, 20 retained license-review rows, 82/82 SourceLink/PDB/API, and 7/7 negative scenarios. The three retained evidence files remain byte-identical.
- Full solution: 378 passed, 50 explicitly gated skips, 0 failed. Inventory `-Check` passes with 69 entries and 56 manifests; official catalog, uploaded, and downloadable counts remain zero.
- Exact Qwen: `ADMITTED missing=none`; its 491,400,032-byte GGUF, five source sidecars, and Stage 31 evidence retain every recorded SHA256. It remains External and `AlgorithmVerified=false`.
- NuGet reports: all 18 projects have zero vulnerable and zero deprecated packages; outdated remains report-only at 113 rows. No dependency was upgraded.
- No unexpected validation failure occurred. Real engine/GPU work is a deliberate skip/blocker, not a pass. No model or tool was downloaded or converted, and no GitHub write was performed.
