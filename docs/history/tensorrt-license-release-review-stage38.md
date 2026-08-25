# TensorRT license and formal-release review / TensorRT 许可证与正式版本复核

Stage 38 is blocked before implementation. The cached `JYPPX.TensorRT.CSharp.API 4.0.0` package is byte-identical to Stage 37, but the required package license, owner decision, immutable `v4.0.0` release identity, and release-bound lock/assets evidence are not complete. No TensorRT adapter, dependency, TFM, public API, native probe, engine, plan, cache, model, or GPU claim was created. / 阶段 38 在实现前阻断。包身份与 Stage 37 完全一致，但包许可证、Owner 决策、不可变 `v4.0.0` 正式身份和与发布绑定的 lock/assets 证据仍不完整；未创建任何 TensorRT 后端、依赖、TFM、公共 API、native probe、engine、plan、cache、模型或 GPU 结论。

## Actual package and API identity / 实际包与 API 身份

| Field | Observed value |
| --- | --- |
| Package | `JYPPX.TensorRT.CSharp.API 4.0.0`, 15,230,357 bytes |
| SHA256 | `140d7cc4f3c2842b5bf601650b955f2ebe9951f910858fc823da2ef6d38f54d8` |
| SHA512/contentHash | `Us/MWKqj2+4c8nFc0cJrQ4uvHXT3T6wEwg7Sfj+1pbOqjvIEBavxuzwGnKNBA0FG3j5nYbbLvHPONSfijtUddQ==`; nupkg sidecar and `.nupkg.metadata` match |
| Cache identity | local offline directory `article-classification-packages-be2e507`; not a formal feed release identity |
| Repository | `https://github.com/guojin-yan/TensorRT-CSharp-API`, package commit `be2e507ae2d34836982eadc4d18a71d9d6655ab0` |
| Source refs | commit exists; local repository HEAD is `3107d2f6f96d54833480fcbea8683ffed88c6294`; no local `v4.0.0` tag and no tag points at the package commit |
| License | nuspec has no `license` or `licenseUrl`; nupkg has no license entry; source policy remains `ownerDecisionState=required` with empty selected fields |
| Signature | unsigned; `dotnet nuget verify --all` exits 1 with `NU3004` |
| TFM/payload | 15 TFM folders, 45 managed DLLs, three bundled assemblies per TFM, no NuGet dependencies, no native/model/ONNX/GGUF/engine/plan payload |
| API boundary | net8.0 `JYPPX.TensorRtSharp` has 299 exported types and 4,246 public declared methods excluding constructors; all TFMs expose the required build, deserialize, context, enqueue, dynamic-shape, and optimization-profile XML members; all PE references remain inside the three managed assemblies plus framework assemblies |

The package project and source commit provide no immutable lock/assets set for this nupkg. `git ls-tree` at the package commit contains zero `packages.lock.json` and zero `project.assets.json`; the local pack project also has no `obj/project.assets.json`. Assets under other source projects belong to the mutable local checkout and are not bound to the audited nupkg. / 包项目和包提交没有提供与该 nupkg 绑定的不可变 lock/assets。包提交中两类文件均为 0，本地 pack 项目也没有 assets；其他源码项目的可变 assets 不能作为该包的正式构建来源证明。

## Blocker delta / Blocker 变化

| Code | Stage 38 state | Remediation |
| --- | --- | --- |
| `package-license-metadata-missing` | retained, unchanged | Owner-select an approved SPDX expression or package license file and rebuild the package. |
| `source-license-owner-decision-required` | retained, unchanged | Record an approved owner decision with non-empty selected package/source license fields at the release commit. |
| `formal-v4.0.0-tag-unverified` | retained, unchanged | Provide an immutable `v4.0.0` tag/release bound to the exact repository commit and nupkg hashes. |
| `package-build-lock-assets-unavailable` | new entry-evidence blocker | Provide lock/assets and build provenance bound to the release commit and exact nupkg content. |

No Stage 37 blocker disappeared. The Stage 37 retained JSON remains 5,620 bytes with SHA256 `16da4585ad1435cdbf608d5e5fcba74d0315ef3bb53915ec62291d4105271ccd`; it was not rewritten because the package identity did not change. / Stage 37 blocker 没有消失；retained JSON 因包身份未变化而保持原大小、哈希和内容。

## Ownership and runtime boundary / 所有权与运行时边界

TensorRT, CUDA, cuDNN, the NVIDIA driver, and any native bridge remain consumer-machine owned. A future isolated adapter may be named `JYPPX.DeploySharp.Backend.TensorRT` only after all admission inputs pass. Device/runtime/profile-bound `.engine/.plan` files remain External local cache data and cannot enter NuGet, ModelFactory's official catalog, a general Release, or model inventory. Standard TensorRT does not imply TensorRT-LLM support. / TensorRT、CUDA、cuDNN、NVIDIA driver 和 native bridge 继续由 consumer 机器持有。只有全部准入通过后才可创建隔离适配器；设备绑定的 `.engine/.plan` 仍只能是 External 本地缓存，且不得扩展为 TensorRT-LLM 能力。

Stage 38 did not perform a native or GPU probe. There is no user-authorized local ONNX input and the retained CUDA/TensorRT paths do not form one verified runtime matrix. Therefore engine build, cache-key materialization, inference, algorithm verification, and performance validation are all blocked/not applicable; CPU, mock, and ONNX Runtime results were not substituted. / 本阶段没有 native/GPU probe，也没有用户明确授权的本地 ONNX，且 retained CUDA/TensorRT 路径不构成唯一已验证矩阵。因此 engine build、cache key、推理、算法与性能均准确记录为 blocked/not applicable，未使用 CPU、mock 或 ORT 替代。

## Validation / 验证

- TensorRT baseline: `DEPLOYSHARP_TENSORRT_ADMISSION_BLOCKED`; `-RequireAdmitted` is an expected failure. The live negative suite passes 8/8, including independent license, repository, SHA512, XML API, and native-payload mutations; all mutation copies are under the system temporary directory and deleted in `finally`.
- Stage 35: fresh `--no-restore` packages pass 9 packages/82 TFMs/9 lock/assets pairs; five negative scenarios pass. The direct Stage 38 run did not request the two-pack semantic/raw comparison; the retained Stage 35 result remains semantic 9/9 and raw ZIP 0/9.
- Stage 36: 9 packages, 82 TFMs, 47 managed dependencies, 4 consumer-owned native runtimes, 82/82 SourceLink, 82/82 portable PDB, and 82/82 API baselines pass; seven negative scenarios pass. Retained SHA256 values remain `3fad8a44644e6dc94e2d2642cdfb55e080e87b7787a373a2e13f5d8111f89b0f` (provenance/SBOM), `3d471a11ed0a95e298d0f709a8275effb9af175d1033466d9b6e8a5b14bea0c4` (public API), and `e895e23babf21a0b5fe1f4370c93c79b03c53ade647db70804adc2f38eb8150c` (symbols).
- Full solution: 378 passed, 50 skipped, 0 failed. Inventory `-Check` passes with 69 entries and 56 manifests; official catalog, uploaded, and downloadable counts remain 0.
- Exact Qwen: admission is `ADMITTED missing=none`; the 491,400,032-byte GGUF, five source sidecars, and Stage 31 evidence all retain their recorded SHA256 values. It remains External, `AlgorithmVerified=false`, `uploaded=false`, and `downloadable=false`.
- NuGet report: 18 solution projects have zero vulnerable and zero deprecated packages. Outdated results are reported only; no dependency was changed.

The retained 30-consumer Stage 37 result remains 16 passed, 11 external skips, and 3 expected external blockers; it was not rerun because no package graph or adapter was admitted. No model was downloaded or converted. No commit, push, tag, signature, Release, upload, or Actions run occurred. / 30 项 consumer 沿用 Stage 37 retained 结果，本阶段没有包图变化或适配器准入，因此未重跑；也没有模型下载/转换或任何 GitHub 写操作。
