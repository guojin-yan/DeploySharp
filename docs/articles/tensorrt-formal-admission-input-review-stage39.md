# TensorRT formal-admission input review / TensorRT 正式准入输入复核

Stage 39 is **blocked** at the package and release-evidence boundary. An upstream owner decision now exists at a newer source HEAD, but no rebuilt package binds that decision to package metadata, an immutable `v4.0.0` release, and immutable build-source evidence. No TensorRT implementation or GPU work was started. / 阶段 39 在包与正式发布证据边界继续 **blocked**。上游较新的源码 HEAD 已出现 Owner 决策，但尚无重建包把该决策与包元数据、不可变 `v4.0.0` 正式版本和不可变构建来源证据绑定；未开始 TensorRT 实现或 GPU 工作。

## Audited package and API identity / 已审计包与 API 身份

| Field | Observed value |
| --- | --- |
| Package | `JYPPX.TensorRT.CSharp.API 4.0.0`, 15,230,357 bytes; byte-identical to Stages 37/38 |
| SHA256 | `140d7cc4f3c2842b5bf601650b955f2ebe9951f910858fc823da2ef6d38f54d8` |
| SHA512/contentHash | `Us/MWKqj2+4c8nFc0cJrQ4uvHXT3T6wEwg7Sfj+1pbOqjvIEBavxuzwGnKNBA0FG3j5nYbbLvHPONSfijtUddQ==`; nupkg sidecar and NuGet metadata match |
| Repository | `https://github.com/guojin-yan/TensorRT-CSharp-API`; nupkg commit `be2e507ae2d34836982eadc4d18a71d9d6655ab0` |
| Release identity | No local immutable `v4.0.0` tag/release; available tags are `v4.0.6156`, `.6167`, `.6169`, `.6170`, and `.6171` |
| Package license | nuspec has no `license`/`licenseUrl`; nupkg has no package license file |
| Signature | unsigned; `dotnet nuget verify --all` exits 1 with expected `NU3004` |
| Build source | no package-bound `packages.lock.json`, `project.assets.json`, or equivalent immutable provenance at the package commit or current HEAD |
| Payload | 15 TFMs, 45 managed DLLs, three bundled assemblies per TFM, no NuGet dependencies, no native/model/ONNX/GGUF/engine/plan payload |
| API | net8.0 `JYPPX.TensorRtSharp`: 299 exported types and 4,246 public declared methods excluding constructors; required build/deserialize/context/enqueue/dynamic-shape/profile XML members exist for all TFMs |
| PE closure | references resolve only to the three managed package assemblies or framework assemblies |

The local offline directory remains a cache location, not a formal release identity. The retained JSON remains 5,620 bytes with SHA256 `16da4585ad1435cdbf608d5e5fcba74d0315ef3bb53915ec62291d4105271ccd`. It was not rewritten because the audited package and declared source identity did not change. / 本地离线目录只是缓存位置，不是正式发布身份。retained JSON 的大小、哈希和内容均未变化，因真实包身份未变化而没有改写。

## New upstream input and binding result / 新上游输入与绑定结论

The upstream repository HEAD is now `3107d2f6f96d54833480fcbea8683ffed88c6294`. Its `pack/publication-license-policy.json` records `ownerDecisionState=approved`, package SPDX expression `Apache-2.0`, and source archive license file `LICENSE`. This is a real new source state, but it does not authorize the old package: the nupkg declares commit `be2e507...`, where the same policy remains `ownerDecisionState=required` with empty selected fields. / 上游 HEAD 的 policy 已记录 Owner approved、`Apache-2.0` 和 `LICENSE`，属于真实的新源码状态；但旧 nupkg 声明的是 `be2e507...`，该提交的 policy 仍为 required 且选择字段为空，因此不能把新 HEAD 决策追溯套用到旧包。

Twenty locally available historical or isolated `JYPPX.TensorRT.CSharp.API.4.0.0.nupkg` files were inspected. All lack package license metadata and signatures, none is bound to the approved HEAD, and none supplies the complete release/tag/lock/assets chain. Consequently no replacement package qualifies as a new audited identity. / 已检查本地 20 份历史或隔离包；它们全部缺少包许可证与签名，没有一份绑定获批 HEAD，也没有完整 release/tag/lock/assets 链，因此不存在可替换的正式包身份。

## Four-blocker delta / 四项 blocker 变化

| Code | Stage 39 state | Required remediation |
| --- | --- | --- |
| `package-license-metadata-missing` | retained | Rebuild the nupkg from the approved release commit with an Owner-approved SPDX expression or explicit package license file. |
| `source-license-owner-decision-required` | retained for package-bound commit | Bind the approved decision to the exact commit declared by the rebuilt package; a newer unrelated HEAD cannot remediate the old nupkg. |
| `formal-v4.0.0-tag-unverified` | retained | Create/provide an immutable `v4.0.0` tag and release that bind repository URL, exact commit, and nupkg SHA256/SHA512/contentHash. |
| `package-build-lock-assets-unavailable` | retained | Provide `packages.lock.json`, `project.assets.json`, or equivalent immutable build provenance bound to that release commit and exact nupkg. |

New blockers: none. Disappeared blockers: none. All four admission inputs must pass together; partial progress does not admit a dependency. / 新增 blocker 为 0，消失 blocker 为 0；四项输入必须同时通过，部分完成不构成依赖准入。

## Ownership and implementation boundary / 所有权与实现边界

All 45 package DLLs are managed. TensorRT, CUDA, cuDNN, the NVIDIA driver, and any native bridge remain consumer-machine owned. A future adapter, if admitted, must be isolated as `JYPPX.DeploySharp.Backend.TensorRT`; Core and ModelPack cannot acquire TensorRT dependencies or imply TensorRT-LLM support. Device/runtime/profile-bound `.engine/.plan` files remain External local-cache data and cannot enter NuGet, ModelFactory's official directory, a general Release, or model inventory. / 45 个包 DLL 均为托管程序集；TensorRT/CUDA/cuDNN/driver/native bridge 继续由 consumer 机器持有。后续适配器只能是隔离包，Core/ModelPack 不得获得 TensorRT 依赖或暗示 TensorRT-LLM；`.engine/.plan` 只能作为设备/运行时/Profile 绑定的 External 本地缓存。

No adapter project, package/reference, TFM, public API, lock/assets, consumer, native probe, engine, plan, cache key, or GPU evidence was created. There is no user-authorized local ONNX, verified matching NVIDIA GPU, unique CUDA/cuDNN/TensorRT matrix, or recorded runtime identity. Engine build/cache/infer and TensorRT algorithm/performance validation are therefore blocked, not replaced by CPU, mock, or ONNX Runtime results. / 本阶段没有创建任何适配器、包/引用、TFM、公共 API、consumer、native probe、engine/plan/cache key 或 GPU 证据；真实 GPU 前置条件不完整，因此 engine 与算法/性能验证准确记为 blocked，未用 CPU、mock 或 ORT 替代。

## Verification / 验证

- TensorRT baseline returns `DEPLOYSHARP_TENSORRT_ADMISSION_BLOCKED`; `-RequireAdmitted` is an expected failure. The eight-scenario negative suite passes 8/8, with independent license, repository, SHA512, API, and native-payload mutations. All mutation directories are deleted in `finally`.
- Stage 35 fresh `--no-restore` pack passes 9 packages, 82 TFMs, 9 locks, and 9 assets; five negative cases pass. The candidate is correctly release-ineligible because the worktree is dirty and all nine packages are unsigned. Semantic/raw two-pack comparison was not requested in this direct run; retained results remain semantic 9/9 and raw ZIP 0/9.
- Stage 36 passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX license rows, 20 retained license blockers, and 82/82 SourceLink, portable PDB, and API checks; seven negative cases pass. Retained SHA256 values remain `3fad8a44644e6dc94e2d2642cdfb55e080e87b7787a373a2e13f5d8111f89b0f`, `3d471a11ed0a95e298d0f709a8275effb9af175d1033466d9b6e8a5b14bea0c4`, and `e895e23babf21a0b5fe1f4370c93c79b03c53ade647db70804adc2f38eb8150c`.
- Full solution: 378 passed, 50 explicitly gated skips, 0 failed. The retained 30-consumer result remains 16 passed, 11 external skips, and 3 expected external blockers; it was not rerun because no package graph or adapter was admitted.
- Inventory `-Check` passes with 69 entries and 56 manifests; official catalog admission is empty, uploaded/downloadable counts are 0. Exact Qwen admission remains `ADMITTED missing=none`; its GGUF, five source sidecars, and Stage 31 evidence retain their recorded hashes. It remains External, `AlgorithmVerified=false`, `uploaded=false`, and `downloadable=false`.
- NuGet vulnerability and deprecated reports are both empty across 18 projects. Outdated results are report-only; no package was upgraded. No unexpected failure occurred.

No model or tool was downloaded or converted. No commit, push, tag, signature, Release, upload, or Actions run occurred. The isolated Stage 39 pack and all negative-test directories were removed after validation; retained repository evidence was preserved. / 本阶段没有下载/转换模型或工具，没有 commit/push/tag/签名/Release/upload/Actions；隔离 pack 与全部负向临时目录在验证后清理，仓库 retained evidence 保持不变。
