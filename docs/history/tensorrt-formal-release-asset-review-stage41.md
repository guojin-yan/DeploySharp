# TensorRT formal-release asset review / TensorRT 正式 Release 资产复核

Stage 41 remains **blocked** before implementation. A real public `v4.0.0` GitHub Release now exists, but its managed nupkg has not been supplied as a local audit input and the Release does not bind the complete SHA512/contentHash and immutable build provenance required by DeploySharp. / 阶段 41 在实现前继续 **blocked**。上游已经出现真实公开的 `v4.0.0` GitHub Release，但 managed nupkg 尚未作为本地审计输入交付，且该 Release 没有绑定 DeploySharp 要求的完整 SHA512/contentHash 与不可变构建来源。

## Local package enumeration / 本地包枚举

The read-only local search still finds 21 paths: 20 upstream artifact copies and one global NuGet-cache copy. They form 19 distinct SHA256 identities across 17 historical repository commits. All 21 have 15 TFM folders, 45 managed DLLs, no native/model/engine payload, no package license declaration/file, and no NuGet signature. None declares the approved source HEAD or the new release-tag commit. / 本地只读搜索仍得到 21 个路径：上游 artifact 20 份、全局 NuGet cache 1 份；共形成 19 个 SHA256 身份、涉及 17 个历史 repository commit。21 份包均为 15 TFM、45 managed DLL、无 native/model/engine payload、无包许可证声明/文件且无 NuGet 签名；没有一份声明获批 HEAD 或新的 release-tag commit。

The selected retained package remains the cache copy because it is the only package with complete local cache sidecars and retained API evidence. Its identity is unchanged: / 选定的 retained 包仍是具有完整 cache sidecar 与 retained API 证据的缓存副本，其身份不变：

| Field | Stage 40 retained package |
| --- | --- |
| Package | `JYPPX.TensorRT.CSharp.API 4.0.0`, 15,230,357 bytes |
| SHA256 | `140d7cc4f3c2842b5bf601650b955f2ebe9951f910858fc823da2ef6d38f54d8` |
| SHA512/contentHash | `Us/MWKqj2+4c8nFc0cJrQ4uvHXT3T6wEwg7Sfj+1pbOqjvIEBavxuzwGnKNBA0FG3j5nYbbLvHPONSfijtUddQ==` |
| Repository | `https://github.com/guojin-yan/TensorRT-CSharp-API`; commit `be2e507ae2d34836982eadc4d18a71d9d6655ab0` |
| License | missing from nuspec and payload; package commit policy is `required` |
| Signature | unsigned; expected `NU3004` |
| Build provenance | no package-bound lock/assets or equivalent immutable source proof |
| Managed/API/payload | 15 TFMs, 45 DLLs; net8.0 `JYPPX.TensorRtSharp` has 299 exported types and 4,246 public declared methods excluding constructors; PE/XML closure valid; zero native/model/engine payload |

The retained JSON remains 5,620 bytes with SHA256 `16da4585ad1435cdbf608d5e5fcba74d0315ef3bb53915ec62291d4105271ccd`. It was not rewritten because the selected local nupkg and its verifiable package-bound identity did not change. / retained JSON 继续保持 5,620 bytes 与相同 SHA256；选定本地 nupkg 及其可核验的包绑定身份没有变化，因此没有改写。

## New formal Release metadata / 新正式 Release 元数据

The GitHub API and remote tag reference now provide real new upstream metadata: / GitHub API 与远端 tag ref 现已提供真实的新上游元数据：

| Field | Remote `v4.0.0` observation |
| --- | --- |
| Release | ID `368273346`; published `2026-08-11T00:49:26Z`; non-draft; non-prerelease |
| Release mutability | API field `immutable=false` |
| Tag | lightweight `v4.0.0` tag at commit `673e120807d789d90a13a9f28a043282e95bb5e6` |
| Target declaration | mutable branch name `TensorRtSharp4.0`; resolved independently through the tag ref |
| Tag-commit policy | Owner decision `approved`; package expression `Apache-2.0`; source license file `LICENSE` |
| Managed asset | asset ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| Missing release binding | no SHA512/contentHash, package signature state, or package-internal repository commit in Release metadata |
| Build source | tag tree has no `packages.lock.json` or `project.assets.json`; Release assets contain no equivalent provenance bundle |

This is stronger evidence than the Stage 40 release-note claim: a tag, Release object, and exact managed-asset SHA256 really exist. It is still not sufficient admission proof. The public asset was not downloaded because this round forbids downloads; therefore its nuspec license/repository commit, SHA512, signature, 15 TFM/45 DLL/API surface, PE references, XML contracts, and strict payload cannot be asserted. The Release body says `Apache-2.0`, but prose cannot substitute for the package's actual nuspec or license file. / 这比 Stage 40 的发布说明声明更强：tag、Release object 与精确 managed asset SHA256 确实存在；但仍不足以准入。本轮禁止下载，因此不能断言远端包的 nuspec license/repository commit、SHA512、签名、TFM/DLL/API、PE/XML 与严格 payload。Release 正文中的 `Apache-2.0` 也不能替代包内真实声明。

The remote asset differs from the retained package by 365,392 bytes and SHA256. No local file has the remote length or digest, so it cannot be selected for the baseline, `-RequireAdmitted`, negative, or `dotnet nuget verify --all` gates. / 远端资产与 retained 包相差 365,392 bytes 且 SHA256 不同；本地没有匹配其大小或 digest 的文件，因此不能把它选入 baseline、强制准入、负向或签名门。

## Four-blocker delta / 四项 blocker 变化

| Code | Stage 41 state | Evidence and remediation |
| --- | --- | --- |
| `package-license-metadata-missing` | retained | The retained nupkg still has no license. Supply the exact Release asset locally and verify its nuspec SPDX expression or package license file. |
| `source-license-owner-decision-required` | retained for the selected package | The tag commit has an approved policy, but the retained package declares `be2e507...`; inspect the new package to prove it declares the same approved release commit. |
| `formal-v4.0.0-tag-unverified` | retained | A real tag/Release now exists, but the Release reports `immutable=false` and does not bind SHA512/contentHash or the package-internal commit. Provide immutable release proof for the exact local asset. |
| `package-build-lock-assets-unavailable` | retained | The tag tree and Release assets contain no package-bound lock/assets or equivalent immutable build-source bundle. |

New blockers: none. Disappeared blockers: none. The remote Release is partial remediation, not admission. Signature is not mandatory, but its actual state must be recorded after the exact asset is supplied. / 新增 blocker 为 0，消失 blocker 为 0。远端 Release 属于部分修复而非准入；签名不是强制项，但精确资产到达后必须如实记录。

## Ownership and implementation boundary / 所有权与实现边界

The future wrapper dependency remains managed-only. TensorRT, CUDA, cuDNN, the NVIDIA driver, and any native bridge remain consumer-machine owned. A future adapter may only be the isolated `JYPPX.DeploySharp.Backend.TensorRT`; Core and ModelPack remain free of TensorRT dependencies. Device/runtime/profile-bound `.engine/.plan` files remain External local cache and cannot enter NuGet, the official catalog, a general Release, or inventory. Standard TensorRT does not imply TensorRT-LLM. / 后续 wrapper 依赖继续保持纯 managed；TensorRT/CUDA/cuDNN/driver/native bridge 由 consumer 机器持有。适配器只能是隔离包，Core/ModelPack 不得获得 TensorRT 依赖；`.engine/.plan` 只能是 External 本地缓存，也不得扩展为 TensorRT-LLM。

No adapter project, reference, TFM, public API, TensorRT lock/assets, consumer, native probe, engine, plan, cache key, or GPU evidence was created. There is no user-authorized local ONNX, verified matching NVIDIA GPU, unique runtime matrix, or recorded runtime identity. Engine build/cache/infer and TensorRT algorithm/performance validation remain blocked; CPU, mock, and ONNX Runtime were not substituted. / 未创建适配器、引用、TFM、公共 API、TensorRT lock/assets、consumer、native probe、engine/plan/cache key 或 GPU 证据；真实 GPU 前置条件不完整，engine、算法与性能验证继续 blocked，未用 CPU/mock/ORT 替代。

## Verification / 验证

- Retained TensorRT baseline returns `DEPLOYSHARP_TENSORRT_ADMISSION_BLOCKED`; `-RequireAdmitted` and unsigned `NU3004` are expected failures. The eight independent negative scenarios pass 8/8 and their system-temporary mutations are removed.
- Stage 35 fresh `--no-restore` pack passes 9 packages, 82 TFMs, 9 locks, and 9 assets; five negatives pass. It is correctly release-ineligible for `dirty-worktree,unsigned-packages`. The retained two-pack result remains semantic 9/9 and raw ZIP 0/9.
- Stage 36 passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX rows, 20 retained license blockers, and 82/82 SourceLink, portable PDB, and API checks; seven negatives pass. Retained evidence SHA256 values remain `3fad8a44644e6dc94e2d2642cdfb55e080e87b7787a373a2e13f5d8111f89b0f`, `3d471a11ed0a95e298d0f709a8275effb9af175d1033466d9b6e8a5b14bea0c4`, and `e895e23babf21a0b5fe1f4370c93c79b03c53ade647db70804adc2f38eb8150c`.
- Full solution: 378 passed, 50 explicitly gated skips, 0 failed. The unchanged package graph leaves the retained 30-consumer result at 16 passed, 11 external skips, and 3 expected external blockers.
- Inventory `-Check` passes with 69 entries and 56 manifests; official catalog entries and uploaded/downloadable counts are 0. Exact Qwen admission is `ADMITTED missing=none`; its GGUF, five sidecars, and Stage 31 evidence match every recorded size/SHA256. It remains External with `AlgorithmVerified=false`.
- NuGet vulnerability and deprecated reports are empty across 18 projects. Outdated results are report-only; no package was upgraded. Unexpected failures: zero.

The fixed system-temporary pack/negative directory was removed. No model or tool was downloaded or converted. DeploySharp did not commit, push, tag, sign, create or mutate a Release, upload, or trigger Actions; the observed upstream Release pre-existed this audit. / 固定系统临时 pack/负向目录已删除；没有下载/转换模型或工具。DeploySharp 未 commit、push、tag、签名、创建或修改 Release、上传或触发 Actions；观察到的上游 Release 在本轮审计前已经存在。
