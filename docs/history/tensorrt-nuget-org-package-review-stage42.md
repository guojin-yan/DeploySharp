# TensorRT NuGet.org package review / TensorRT NuGet.org 正式包复核

Stage 42 confirms that the managed package itself is suitable for an isolated adapter, but overall admission remains **blocked** by two release-engineering inputs. The user explicitly authorized downloading the exact `4.0.0` package from the configured NuGet source; no GitHub asset, tool, model, or dependency upgrade was downloaded. / 阶段 42 已确认 managed 包本体适合后续隔离适配器，但整体准入仍因两项发布工程输入而 **blocked**。用户明确授权从已配置 NuGet 正式源下载精确 `4.0.0` 包；本轮没有下载 GitHub asset、工具或模型，也没有升级依赖。

## Three package identities / 三个包身份

| Field | Stage 40 retained | GitHub Release managed asset | Stage 42 NuGet.org package |
| --- | --- | --- | --- |
| Bytes | 15,230,357 | 15,595,749 | 15,608,836 |
| SHA256 | `140d7cc4...4d8` | `58add436...1d7` | `92bc1064...038` |
| SHA512 | `Us/MWKqj...dQ==` | not published | `9VPO6fsj...Zw==` |
| NuGet contentHash | same as unsigned SHA512 | not published | `jJeYAI80...row==` |
| Repository commit | `be2e507...` | Release tag is `673e120...`; package internals not downloaded | `673e120807d789d90a13a9f28a043282e95bb5e6` |
| License | missing | Release prose says Apache-2.0 | nuspec expression `Apache-2.0`; NuGet catalog agrees |
| Signature | unsigned / `NU3004` | uninspected | NuGet.org repository-signed; `dotnet nuget verify --all` passes |
| API | 299 exported types; 4,246 public declared methods | uninspected | 311 exported types; 4,374 public declared methods |

The GitHub asset and NuGet.org bytes are expected to differ when NuGet.org adds its repository signature. That difference is not itself a defect. The release still needs an immutable manifest that records both channel identities and the NuGet contentHash so the signed feed artifact can be traced back to the unsigned Release asset without inference. / NuGet.org 添加 repository signature 后，GitHub asset 与 NuGet.org 包的原始字节可以不同，这本身不是缺陷；但 Release 仍需不可变 manifest 同时记录两个渠道身份与 NuGet contentHash，避免通过推断建立关系。

## Package inspection / 包复核

The official NuGet V3 catalog fixes package ID/version, published time, package size, SHA512, `Apache-2.0`, repository URL, and commit. The exact package has 15 expected TFMs, 45 managed DLLs, three managed assemblies per TFM, no NuGet dependencies, and zero native, model, `.engine`, or `.plan` payload. All PE references remain inside the three managed assemblies or framework assemblies. Every required builder/runtime/context/profile member exists in all 15 XML contracts. / NuGet V3 catalog 固定了 ID/version、发布时间、大小、SHA512、许可证、仓库与 commit。精确包具有预期 15 TFM、45 managed DLL，每个 TFM 三个托管程序集，无 NuGet 依赖，也不携带 native/model/engine/plan；PE 引用与 15 份 XML 合同均通过。

The selected package identity is: / 选定包身份为：

- SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`;
- raw signed-package SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`;
- NuGet contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`;
- repository signature subject `NuGet.org Repository by Microsoft`, certificate SHA256 `1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D`;
- assembly informational version `4.0.0+673e120807d789d90a13a9f28a043282e95bb5e6`.

The retained JSON was updated because the selected nupkg and its package-bound identity changed. A temporary `net8.0` consumer generated a lock and assets file only to prove exact NuGet resolution; those files are not accepted as upstream build provenance and are deleted with the isolated package cache. / 由于选定 nupkg 与其绑定身份确实改变，retained JSON 已更新。临时 consumer 的 lock/assets 只证明 NuGet 精确解析，不作为上游构建 provenance，并随隔离缓存删除。

## Four-blocker delta / 四项 blocker 变化

| Entry condition | Stage 42 state | Evidence or remediation |
| --- | --- | --- |
| Package license metadata | disappeared | nuspec and NuGet catalog declare `Apache-2.0` |
| Source license owner decision | disappeared | `673e120...:pack/publication-license-policy.json` is approved and selects `Apache-2.0`/`LICENSE` |
| Immutable `v4.0.0` release and exact package binding | retained | tag commit matches, but Release ID `368273346` reports `immutable=false` and has no cross-channel SHA256/SHA512/contentHash manifest |
| Same-build immutable lock/assets or equivalent provenance | retained | none is committed at `673e120...` or attached to the Release |

New blockers: zero. Disappeared blockers: two. Retained blockers: two. To remove them upstream, attach a machine-readable manifest before freezing the Release that binds the tag/repository/commit, unsigned GitHub asset hashes, signed NuGet.org package SHA256/SHA512/contentHash/catalog URL/signature state, and hashes of the same-build lock/assets or equivalent attestation. Then make the Release immutable. Republishing `4.0.0` is not requested. / 新增 blocker 为 0，消失 2 项，保留 2 项。上游只需在冻结 Release 前附加机器 manifest，绑定 tag/repository/commit、GitHub 未签名资产、NuGet.org 签名包身份和同次构建 provenance，随后将 Release 设为 immutable；不要求重发 `4.0.0`。

## Ownership and implementation boundary / 所有权与实现边界

The future dependency remains managed-only. TensorRT, CUDA, cuDNN, NVIDIA driver, and the selected native bridge remain consumer-machine owned. The only permitted adapter package is `JYPPX.DeploySharp.Backend.TensorRT`; Core and ModelPack stay TensorRT-free. `.engine/.plan` outputs remain device/runtime/profile-bound External local cache and never enter NuGet, the official catalog, a general Release, or inventory. TensorRT-LLM is outside scope. / 后续依赖继续保持纯 managed；TensorRT/CUDA/cuDNN/driver/native bridge 由 consumer 持有。仅允许隔离 `JYPPX.DeploySharp.Backend.TensorRT`，Core/ModelPack 不引入 TensorRT；`.engine/.plan` 继续是设备绑定 External/local cache，且不创建 TensorRT-LLM 能力。

No adapter project/reference/API/TFM, consumer, native probe, engine/plan/cache, or GPU evidence was created. Real engine build/cache/infer remains blocked until the user separately authorizes an exact local ONNX, one matching GPU/runtime matrix, and recordable runtime identity. CPU, mock, and ONNX Runtime are not substitutes for TensorRT evidence. / 未创建适配器、引用/API/TFM、consumer、native probe、engine/plan/cache 或 GPU 证据；真实 engine 工作仍需用户另行授权精确 ONNX 与唯一匹配运行时矩阵，且不会用 CPU/mock/ORT 替代。

## Verification / 验证

- TensorRT baseline returns `DEPLOYSHARP_TENSORRT_ADMISSION_BLOCKED` with exactly two blockers; `-RequireAdmitted` is an expected failure. All eight independent negative scenarios pass and their mutation directories are removed.
- `dotnet nuget verify --all` passes with a NuGet.org repository signature. Nuspec, 15 TFMs, 45 DLLs, PE references, XML API, and strict no-native/model/engine payload checks pass.
- Stage 35 passes nine packages, 82 TFMs, nine locks/assets, and 5/5 negatives. It remains correctly release-ineligible for the existing dirty worktree and nine unsigned DeploySharp packages.
- Stage 36 passes 47 managed dependencies, four consumer-owned native runtimes, 27 SPDX rows, 20 retained license blockers, 82/82 SourceLink/PDB/API checks, and 7/7 negatives. The retained evidence hashes remain byte-identical.
- Full solution: 378 passed, 50 explicitly gated skips, zero failed. Inventory `-Check` passes with 69 entries and 56 manifests; official catalog, uploaded, and downloadable counts remain zero.
- Exact Qwen admission is `ADMITTED missing=none`. The GGUF, five source sidecars, and Stage 31 runtime evidence match every retained size/SHA256 and remain External with `AlgorithmVerified=false`.
- Vulnerability and deprecated reports are empty across all 18 projects. Outdated is report-only; no version was changed. Unexpected failures: zero.

The isolated NuGet cache, downloaded nupkg, temporary consumer lock/assets, nine-package pack, and all negative-test copies are removed after validation. DeploySharp did not commit, push, tag, sign, mutate a Release, upload, or trigger Actions. / 隔离 NuGet cache、下载包、临时 consumer lock/assets、九包 pack 与全部负向副本均在验证后删除；DeploySharp 未 commit、push、tag、签名、修改 Release、上传或触发 Actions。
