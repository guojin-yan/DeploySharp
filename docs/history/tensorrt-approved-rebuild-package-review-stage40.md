# TensorRT approved-rebuild package review / TensorRT 已批准版本包重建复核

Stage 40 is **blocked** before implementation. The upstream source has an Owner-approved `Apache-2.0` policy, but it has not produced a locally verifiable rebuilt `JYPPX.TensorRT.CSharp.API 4.0.0` package bound to an immutable `v4.0.0` release and immutable build-source evidence. / 阶段 40 在实现前继续 **blocked**。上游源码已有 Owner-approved `Apache-2.0` policy，但尚未产出可在本地核验、同时绑定不可变 `v4.0.0` 正式身份和不可变构建来源证据的重建包。

## Candidate enumeration / 候选枚举

The read-only search found 21 package paths: one global NuGet-cache copy and the same 20 historical/isolated upstream artifacts reviewed in Stage 39. They represent 17 distinct repository commits. No new path or package identity appeared. / 只读搜索得到 21 个包路径：全局 NuGet cache 副本 1 个，以及 Stage 39 已审阅的上游历史/隔离 artifacts 20 个；它们涉及 17 个不同 repository commit。本阶段没有新增路径或包身份。

Every candidate has 15 TFM folders, 45 managed DLLs, zero native/model/engine payload, the formal repository URL, no package license declaration/file, and no NuGet signature. Candidate policies at their declared commits are either unavailable or `required`; none is an approved-policy package. No candidate declares current approved HEAD `3107d2f6f96d54833480fcbea8683ffed88c6294`. / 所有候选均为 15 TFM、45 managed DLL、无 native/model/engine payload和正式 repository URL，但全部没有包许可证声明/文件与 NuGet 签名；其声明提交的 policy 不是 unavailable 就是 required，没有一份属于 approved-policy 重建包，也没有候选声明当前获批 HEAD。

## Retained package identity / 保留包身份

| Field | Stage 39 and Stage 40 value |
| --- | --- |
| Package | `JYPPX.TensorRT.CSharp.API 4.0.0`, 15,230,357 bytes |
| SHA256 | `140d7cc4f3c2842b5bf601650b955f2ebe9951f910858fc823da2ef6d38f54d8` |
| SHA512/contentHash | `Us/MWKqj2+4c8nFc0cJrQ4uvHXT3T6wEwg7Sfj+1pbOqjvIEBavxuzwGnKNBA0FG3j5nYbbLvHPONSfijtUddQ==` |
| Repository | `https://github.com/guojin-yan/TensorRT-CSharp-API`; commit `be2e507ae2d34836982eadc4d18a71d9d6655ab0` |
| License | nuspec has no license metadata; nupkg has no license entry; package commit policy remains `required` |
| Tag/release | no local immutable `v4.0.0` tag/release bound to the package |
| Signature | unsigned; `dotnet nuget verify --all` returns expected `NU3004` |
| Build source | no package-bound `packages.lock.json`, `project.assets.json`, or equivalent immutable provenance |
| Managed payload | 15 TFMs, 45 DLLs, three bundled assemblies per TFM, zero NuGet dependencies |
| Native/model payload | zero native, ONNX, GGUF, engine, or plan entries |
| API | net8.0 `JYPPX.TensorRtSharp`: 299 exported types and 4,246 public declared methods excluding constructors; required build/deserialize/context/enqueue/dynamic-shape/profile XML members and PE closure remain valid |

There is no new package against which to record an old/new size, hash, repository, license, or signature delta. The retained JSON therefore remains 5,620 bytes with SHA256 `16da4585ad1435cdbf608d5e5fcba74d0315ef3bb53915ec62291d4105271ccd`. / 没有新包可形成大小、哈希、repository、许可证或签名的新旧差异，因此 retained JSON 继续保持原大小、哈希和内容。

## Release-identity cross-check / 正式身份交叉核对

The upstream working repository remains at HEAD `3107d2f6f96d54833480fcbea8683ffed88c6294` with an approved `Apache-2.0` policy, but it is dirty and has no tag pointing at HEAD. Available tags remain only `v4.0.6156`, `v4.0.6167`, `v4.0.6169`, `v4.0.6170`, and `v4.0.6171`. Neither HEAD nor `be2e507...` contains a package lock/assets file in its Git tree. / 上游工作仓库仍位于获批 HEAD，但工作树为 dirty，HEAD 没有 tag；现有 tag 列表不含 `v4.0.0`，HEAD 与旧包提交的 Git tree 均不含 package lock/assets。

`docs/releases/4.0.0.md` calls the version a stable/public release, but a mutable documentation claim is not immutable release identity. Local final-release evidence still reports current-HEAD package count 0, `blocked-owner-authorization-required`, `pending-release-owner-approval`, `canExecutePublicPublish=false`, and package proof that cannot be used as public proof. No local evidence binds a released `v4.0.0` tag, approved commit, exact rebuilt nupkg hashes, and build provenance. / 上游发布说明中的“正式稳定版”是可变文档声明，不是不可变发布身份；本地 final-release evidence 仍明确显示当前 HEAD 包数为 0、Owner approval pending、publish 不可执行、包证据不能作为 public proof，没有证据把 tag、获批提交、重建包哈希和构建来源绑定为一条正式链。

## Blocker delta and remediation / Blocker 变化与修复

| Code | Stage 40 state | Required remediation |
| --- | --- | --- |
| `package-license-metadata-missing` | retained | Build a new nupkg from the approved release commit with `Apache-2.0` SPDX metadata or an explicit package license file. |
| `source-license-owner-decision-required` | retained for the package-bound commit | Make the rebuilt package declare the exact approved release commit; current HEAD cannot retroactively license `be2e507...`. |
| `formal-v4.0.0-tag-unverified` | retained | Provide an immutable `v4.0.0` tag/release binding repository, approved commit, and exact nupkg SHA256/SHA512/contentHash. |
| `package-build-lock-assets-unavailable` | retained | Provide release-commit and nupkg-bound lock/assets or equivalent immutable build provenance. |

New blockers: none. Disappeared blockers: none. A signed package is not mandatory for this gate, but an unsigned result must remain explicit and cannot be represented as verified. / 新增 blocker 为 0，消失 blocker 为 0。签名不是本门的强制准入项，但未签名状态必须如实保留，不能表述为已验证签名。

## Immediate upstream Owner handoff / 上游 Owner 最短交接链

The approved source is close to producing an admissible managed package. `Directory.Build.props` already sets `PackageLicenseExpression=Apache-2.0`, and `.github/workflows/package-managed.yml` packs the managed project with `RepositoryCommit=${github.sha}` and `ContinuousIntegrationBuild=true`. The remaining work is release execution and immutable provenance, not a DeploySharp adapter change. / 获批源码已经接近可生成准入包：中央 props 已声明 `Apache-2.0`，managed package workflow 也会把 `github.sha` 写入 repository commit。剩余工作属于正式发布执行与不可变 provenance，不是 DeploySharp 适配器代码。

The upstream evidence provides this publication-disabled Owner dry-run command; it was **not** executed in Stage 40:

```powershell
gh workflow run release-quality-gate.yml --repo guojin-yan/TensorRT-CSharp-API --ref TensorRtSharp4.0 -f run_package_managed_dry_run=true -f run_release_artifact_audit=false -f run_split_package_build=false
```

Before that command can become admission input, the Owner must select a clean immutable release commit. The dry-run output must include the exact `JYPPX.TensorRT.CSharp.API.4.0.0.nupkg` plus a manifest binding workflow run ID, repository URL, commit, package length, SHA256, SHA512/contentHash, nuspec license/repository fields, and the same-run `project.assets.json`, `packages.lock.json`, or equivalent dependency/build-source evidence. The current workflow uploads only `artifacts/managed/*.nupkg`; that is insufficient for DeploySharp's build-source gate unless the provenance bundle is added or supplied separately. / Owner 必须先选定 clean immutable release commit；dry-run 除精确 nupkg 外，还必须交付绑定 run ID、repository、commit、大小、SHA256/SHA512、nuspec 字段与同次构建 assets/lock 或等价依赖来源的 manifest。当前 workflow 仅上传 nupkg，不足以通过 DeploySharp build-source 门。

After dry-run review, the Owner-controlled formal workflow must create or verify `v4.0.0` at that exact commit before creating the Release, then attach the exact reviewed nupkg and immutable provenance. DeploySharp needs either those local Owner-provided artifacts or a formally cached package plus release metadata; it does not need Bridge packages for managed admission, and it will not accept the 19-package runtime publication model as permission to bundle TensorRT/CUDA/cuDNN. / dry-run 审阅后，Owner 控制的正式 workflow 必须先把 `v4.0.0` 创建/核验到同一提交，再创建 Release 并附加已审阅 nupkg 与不可变 provenance。DeploySharp 只需要上述正式 managed 包输入，不需要 Bridge 包，也不会把上游 19 包发布模型解释为可捆绑 NVIDIA runtime。

Once this exact bundle arrives, the next DeploySharp run can compare old/new identity, update retained evidence, execute `-RequireAdmitted`, and, only if all four blockers disappear, start the isolated `JYPPX.DeploySharp.Backend.TensorRT` adapter. / 该精确 bundle 到达后，下一轮即可比较新旧身份、更新 retained evidence、执行强制准入，并只在四项 blocker 全部消失后开始隔离适配器。

## Ownership and implementation state / 所有权与实现状态

The wrapper package remains managed-only. TensorRT, CUDA, cuDNN, the NVIDIA driver, and any native bridge remain consumer-machine owned. A future adapter may only be the isolated `JYPPX.DeploySharp.Backend.TensorRT`; Core and ModelPack remain free of TensorRT dependencies. Device/runtime/profile-bound `.engine/.plan` files remain External local cache and cannot enter NuGet, ModelFactory's official catalog, a general Release, or inventory. Standard TensorRT does not imply TensorRT-LLM. / wrapper 包继续为纯 managed；TensorRT/CUDA/cuDNN/driver/native bridge 由 consumer 机器持有。后续只能创建隔离适配器，Core/ModelPack 不得引入 TensorRT；`.engine/.plan` 只能是 External 本地缓存，也不得扩展为 TensorRT-LLM。

No adapter project, package/reference, TFM, public API, TensorRT lock/assets, consumer, native probe, engine, plan, cache key, or GPU evidence was created. There is no user-authorized local ONNX, verified matching NVIDIA GPU, unique CUDA/cuDNN/TensorRT matrix, or recorded runtime identity. Engine build/cache/infer and TensorRT algorithm/performance validation remain blocked; CPU, mock, and ONNX Runtime results were not substituted. / 未创建适配器、包/引用、TFM、公共 API、TensorRT lock/assets、consumer、native probe、engine/plan/cache key 或 GPU 证据。真实 GPU 前置条件仍不完整，engine、算法与性能验证继续 blocked，未使用 CPU/mock/ORT 替代。

## Verification / 验证

- TensorRT baseline returns `DEPLOYSHARP_TENSORRT_ADMISSION_BLOCKED`; `-RequireAdmitted` and unsigned `NU3004` are expected failures. The eight independent negative scenarios pass 8/8 and their temporary directories are removed.
- Stage 35 fresh `--no-restore` pack passes 9 packages, 82 TFMs, 9 locks, and 9 assets; five negative scenarios pass. The candidate remains release-ineligible because the worktree is dirty and all nine packages are unsigned. The direct run did not request a two-pack semantic/raw comparison; retained results remain semantic 9/9 and raw ZIP 0/9.
- Stage 36 passes 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 27 SPDX license rows, 20 retained license blockers, and 82/82 SourceLink, portable PDB, and API checks; seven negative scenarios pass. Retained SHA256 values remain `3fad8a44644e6dc94e2d2642cdfb55e080e87b7787a373a2e13f5d8111f89b0f`, `3d471a11ed0a95e298d0f709a8275effb9af175d1033466d9b6e8a5b14bea0c4`, and `e895e23babf21a0b5fe1f4370c93c79b03c53ade647db70804adc2f38eb8150c`.
- Full solution: 378 passed, 50 explicitly gated skips, 0 failed. The retained 30-consumer result remains 16 passed, 11 external skips, and 3 expected external blockers; no adapter/package graph change justified rerunning it.
- Inventory `-Check` passes with 69 entries and 56 manifests; official catalog admission is empty and uploaded/downloadable counts are 0. Exact Qwen admission remains `ADMITTED missing=none`; the GGUF, five source sidecars, and Stage 31 evidence retain their recorded hashes. It remains External, `AlgorithmVerified=false`, `uploaded=false`, and `downloadable=false`.
- NuGet vulnerability and deprecated reports are empty across 18 projects. Outdated results are report-only; no dependency was upgraded. Unexpected failures: zero.

No model or tool was downloaded or converted. No commit, push, tag, signature, Release, upload, or Actions run occurred. / 本阶段没有下载/转换模型或工具，也没有 commit、push、tag、签名、Release、upload 或 Actions。
