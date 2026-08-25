# TensorRT release binding admission / TensorRT 正式发布绑定准入

Stage 43 rechecks the two remaining Stage 42 proof conditions. The exact managed package is unchanged and passes its package gate; the public Release still lacks the immutable cross-channel manifest and same-build provenance required before an adapter can be created. / 阶段 43 复核 Stage 42 剩余两项证明条件。精确 managed 包没有变化且通过包门；公开 Release 仍缺 immutable 跨渠道 manifest 与同次构建 provenance，因此不能创建适配器。

## Release state / Release 状态

| Field | Read-only observation |
| --- | --- |
| Release | ID `368273346`, `v4.0.0`, published `2026-08-11T00:49:26Z` |
| Tag | lightweight `v4.0.0` ref resolves to `673e120807d789d90a13a9f28a043282e95bb5e6` |
| Repository | `https://github.com/guojin-yan/TensorRT-CSharp-API` |
| Mutability | `immutable=false` |
| Asset set | 19 `.nupkg` assets plus `TensorRtSharp4.0-source-4.0.0.zip`; no manifest/provenance asset |
| Managed Release asset | 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7`; not downloaded |
| Release proof fields | no SHA512/contentHash for the managed asset; no binding to the NuGet.org signed package; no lock/assets/attestation |

The tag ref and commit are real, but a mutable Release plus separate hashes cannot establish that the unsigned Release asset and the repository-signed NuGet.org package are the same build output. The required manifest must record both channel identities, including the NuGet catalog URL and repository signature state. / tag/ref 与 commit 本身真实存在，但 mutable Release 和分散 hash 不能证明 GitHub 未签名资产与 NuGet.org repository-signed 包来自同一构建；必须用 manifest 同时记录两个渠道身份、NuGet catalog URL 与签名状态。

## Exact NuGet package / 精确 NuGet 包

The user-authorized package was restored from `https://api.nuget.org/v3/index.json` into an isolated system temporary cache. Its identity remains: 15,608,836 bytes; SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`; contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`. NuGet.org Repository signature verification passes. / 用户授权的精确包从 NuGet.org 隔离恢复；大小、SHA256、raw SHA512、contentHash 与 Stage 42 完全一致，NuGet.org Repository signature 通过。

The package still declares `Apache-2.0`, repository commit `673e120...`, 15 TFMs, 45 managed DLLs, and no native/model/engine/plan payload. The net8 API remains 311 exported types and 4,374 public declared methods. No package-bound identity changed, so `eng/tensorrt/evidence/tensorrt-4.0.0-admission.blocked.json` was not rewritten. / 包仍声明 `Apache-2.0`、仓库 commit、15 TFM/45 managed DLL，且无 native/model/engine/plan payload；net8 API 仍为 311/4,374。包绑定身份没有变化，因此 retained JSON 不改写。

## Blocker delta / Blocker 变化

| Blocker | Stage 43 state | Exact remediation |
| --- | --- | --- |
| `formal-v4.0.0-release-package-binding-incomplete` | retained | Attach an immutable manifest binding repository/tag/commit, the GitHub unsigned asset size/SHA256/SHA512/contentHash, and the NuGet.org signed package size/SHA256/SHA512/contentHash/catalog/signature identity; then freeze the Release as immutable. |
| `package-build-lock-assets-unavailable` | retained | Attach same-run `packages.lock.json`/`project.assets.json` or equivalent immutable attestation tied to commit `673e120...` and the exact package hashes. |

New blockers: zero. Disappeared blockers: zero. The Stage 42 consumer lock/assets are intentionally not reused as proof; they only prove NuGet feed resolution. / 新增 blocker 为 0，消失 blocker 为 0。Stage 42 consumer lock/assets 只证明 feed resolution，不能作为上游证明。

## Implementation boundary / 实现边界

Because both proof conditions remain blocked, no `JYPPX.DeploySharp.Backend.TensorRT` project, reference, TFM, public API, DeploySharp lock/assets, package-only consumer, native probe, engine, plan, cache key, or GPU evidence was created. TensorRT/CUDA/cuDNN/driver/native bridge remain consumer-owned; `.engine/.plan` remain External local cache only; TensorRT-LLM is outside scope. / 两项证明仍阻断，因此未创建隔离适配器项目、引用、TFM、公共 API、DeploySharp lock/assets、consumer、native probe、engine、plan、cache key 或 GPU 证据。TensorRT/CUDA/cuDNN/driver/native bridge 继续由 consumer 持有，`.engine/.plan` 只能是 External/local cache，不创建 TensorRT-LLM。

## Verification / 验证

- TensorRT baseline remains `DEPLOYSHARP_TENSORRT_ADMISSION_BLOCKED` with exactly the two retained blockers.
- `-RequireAdmitted` is an expected failure; all eight independent negative scenarios pass.
- `dotnet nuget verify --all` passes with the NuGet.org Repository signature; TFM/API/PE/XML/payload checks remain unchanged and pass.
- Stage 35 passes 9 packages, 82 TFMs, 9 locks/assets and 5/5 negative scenarios. Its retained release eligibility remains false only for the dirty worktree and nine unsigned DeploySharp packages.
- Stage 36 passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX rows, 20 retained license-review rows, 82/82 SourceLink/PDB/API checks and 7/7 negative scenarios. Its three retained evidence files remain byte-identical.
- The full solution passes 378 tests, skips 50 explicitly gated tests and has zero failures. Inventory `-Check` passes with 69 entries and 56 manifests.
- Exact Qwen admission remains `ADMITTED missing=none`; the GGUF, five source sidecars and Stage 31 evidence retain their bound sizes and SHA256 values. It remains External with `AlgorithmVerified=false`, `uploaded=false` and `downloadable=false`; the official catalog remains empty.
- Vulnerability and deprecated reports are empty across all 18 solution projects. Outdated reports 113 rows only; no dependency was upgraded.
- Real TensorRT engine build/cache/inference is skipped and remains blocked because no exact local ONNX plus unique authorized CUDA/cuDNN/TensorRT/bridge/GPU runtime identity was supplied. CPU, mock and ORT were not used as substitutes, and no algorithm or performance claim is made.
- The isolated NuGet cache and all Stage 43 mutation/validation files are removed after verification; no Git/GitHub write or Release mutation occurs.
