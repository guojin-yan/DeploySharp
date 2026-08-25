# TensorRT 4.0.0 admission audit / TensorRT 4.0.0 准入审计

Stage 37 stops at the package-admission boundary. It adds a read-only gate and retained blocker evidence; it does not add a backend project, central package version, project/solution reference, TFM, lock/assets entry, public API, native runtime, engine, model, or catalog row. / 阶段 37 在包准入边界停止，只新增只读门和 blocker 证据；不新增后端项目、中央版本、项目/solution 引用、TFM、lock/assets、公共 API、原生运行时、engine、模型或目录项。

The retained evidence is `eng/tensorrt/evidence/tensorrt-4.0.0-admission.blocked.json`, a custom `DeploySharpTensorRtPackageAdmission` JSON document of 5,620 bytes with SHA256 `16da4585ad1435cdbf608d5e5fcba74d0315ef3bb53915ec62291d4105271ccd`. It is a package audit, not SPDX/CycloneDX, native-runtime evidence, an engine, or an inference result. / 保留证据是上述自定义 JSON（5,620 bytes，SHA256 如上）；它不是 SPDX/CycloneDX、native runtime 证据、engine 或推理结果。

## Audited package / 已审计包

| Field | Observed value |
| --- | --- |
| Package | `JYPPX.TensorRT.CSharp.API 4.0.0` |
| Cache source | local offline package source recorded by `.nupkg.metadata` |
| Size | `15,230,357` bytes |
| SHA256 | `140d7cc4f3c2842b5bf601650b955f2ebe9951f910858fc823da2ef6d38f54d8` |
| SHA512 | matches the cached `.nupkg.sha512` and NuGet `contentHash` |
| TFM folders | `net10.0`, `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, `netcoreapp3.1` |
| Managed payload | 45 DLLs: `JYPPX.Shared`, `JYPPX.CudaSharp`, and `JYPPX.TensorRtSharp` for each TFM; no NuGet dependencies |
| Native/model payload | none; no `runtimes/`, native library, `.engine`, `.plan`, ONNX, or GGUF entry |
| Repository | `https://github.com/guojin-yan/TensorRT-CSharp-API`, commit `be2e507ae2d34836982eadc4d18a71d9d6655ab0` |
| Package license | missing: no nuspec `license`/`licenseUrl` and no package license file |
| Signature | unsigned; `dotnet nuget verify --all` reports expected `NU3004` |

The net8.0 managed assembly identities retained in `eng/tensorrt/evidence/tensorrt-4.0.0-admission.blocked.json` are:

| Assembly | Bytes | SHA256 | Assembly version |
| --- | ---: | --- | --- |
| `JYPPX.Shared.dll` | 1,767,424 | `59aa16a68c38c46982678bf131487e047b3a04eadb7fa5ad7833e0e57e9f4b3f` | `4.0.0.0` |
| `JYPPX.CudaSharp.dll` | 327,168 | `a53db8bf6d45becf407e7d3660a66335d22fc90bf1ba47f1d09f019b6fca0a0f` | `4.0.0.0` |
| `JYPPX.TensorRtSharp.dll` | 1,552,896 | `d3d930ebf849ed60be0acd887216eee53c3b7b0cfefe1d58350102805cc03a62` | `4.0.0.0` |

The gate reads PE metadata for every TFM, permits only framework references plus the three bundled managed assemblies, and checks the real XML contract for `BuildSerializedNetwork`, runtime deserialization, execution-context creation, asynchronous enqueue, dynamic input shapes, and optimization profiles. This is API observation only; none of these types is exposed by DeploySharp. / 门禁对每个 TFM 读取 PE metadata，只允许框架引用和三个包内托管程序集，并核对真实 builder/runtime/context/stream/profile XML API。这只是 API 观察，DeploySharp 未暴露或调用这些类型。

## Independent blockers / 独立阻断

| Code | Evidence | Required remediation |
| --- | --- | --- |
| `package-license-metadata-missing` | nuspec and nupkg contain no declared license | owner-select an SPDX expression or package license file and rebuild |
| `source-license-owner-decision-required` | source commit policy has `ownerDecisionState=required` with empty selected license fields | complete the upstream owner decision before publication |
| `formal-v4.0.0-tag-unverified` | the repository URL/commit matches, but the local refs have no `v4.0.0` tag at that commit | provide an immutable release/tag and matching provenance |

The local GPU is an RTX 3060 Laptop GPU with compute capability 8.6, but the observed CUDA and TensorRT paths do not form one verified runtime matrix. The audit deliberately stopped before loading a native library or building an engine. A GPU, CUDA installation, or readable wrapper API cannot override a package-license or release-identity blocker. / 本机虽有 compute capability 8.6 的 RTX 3060 Laptop GPU，但 CUDA/TensorRT 路径并未构成单一已验证矩阵。本阶段在加载 native 或构建 engine 前停止；硬件和可读 API 不能绕过许可证或正式身份 blocker。

## Ownership and future cache contract / 所有权与后续 cache 合同

| Scope | Owner | Stage 37 state |
| --- | --- | --- |
| DeploySharp TensorRT managed adapter | DeploySharp | not created; would use the repository's Apache-2.0 only after dependency admission |
| TensorRT-CSharp-API wrapper | external managed dependency | license not established; blocked |
| TensorRT/CUDA/cuDNN/driver/native bridge | consumer machine | never bundled or implicitly selected by DeploySharp |
| ONNX/model and `.engine/.plan` | external/local | no file created; never admitted to the official catalog or general Release |

If admission later succeeds, an engine cache key must bind the exact input-model SHA256, managed API and native TensorRT versions, CUDA/cuDNN versions, GPU compute capability, OS/architecture, precision, optimization profiles/dynamic shapes, workspace and builder flags, network metadata, and adapter-schema version. Writes must use an atomic temporary file, flush and completion marker, size/SHA256 verification, invalidation, and concurrent-build deduplication. These are future acceptance requirements, not implemented Stage 37 behavior. / 后续若准入成功，engine key 和原子写入/完整性/失效/并发合同必须覆盖上述全部字段；它们是未来验收条件，不是 Stage 37 已实现能力。

## Executable gate / 可执行门

```powershell
pwsh -NoProfile -File eng/tensorrt/Test-TensorRtPackageAdmission.ps1
pwsh -NoProfile -File eng/tensorrt/Test-TensorRtPackageAdmission.ps1 -RequireAdmitted
pwsh -NoProfile -File eng/tensorrt/Invoke-TensorRtPackageAdmissionNegativeTests.ps1
```

The baseline command succeeds with `DEPLOYSHARP_TENSORRT_ADMISSION_BLOCKED`; `-RequireAdmitted` fails by design. Stage 37 retained evidence records the original six scenarios: required-admission bypass, SHA512 drift, retained-blocker drift, unavailable source, injected native payload, and removed managed API. Stage 38 expands the live script to eight scenarios by adding independent package-license and repository-metadata mutations. All mutations use a unique system temporary directory and are deleted in `finally`. / 基线命令以 `BLOCKED` 正常完成，`-RequireAdmitted` 按设计失败。Stage 37 retained 证据记录原六类场景；Stage 38 在 live 脚本中新增独立包许可证和 repository 元数据突变，扩展为八类。所有突变均在系统临时目录并自动清理。

Because no adapter exists, TensorRT clean-consumer, engine/cache/profile, and real GPU matrices are accurately reported as blocked/not applicable rather than mocked or substituted with CPU/ONNX Runtime results. Stage 35 remains 9 packages/82 TFMs with semantic `9/9` and raw ZIP `0/9`; its five negative cases pass. Stage 36 retains the same three JSON files and passes 82/82 SourceLink/PDB/API plus seven negative cases. Its gate now checks package and SourceLink commits live against `HEAD` before normalizing only commit-bound snapshot hashes, and clean consumers compare output DLLs with the current validated nupkg rather than a pre-commit DLL hash. The 30 consumers remain 16 passed, 11 external skips, and 3 expected external blockers. Inventory remains 69/56, exact Qwen admission remains `ADMITTED missing=none`, and the official catalog remains empty. / 因未创建适配器，TensorRT consumer、engine/cache/profile 和真实 GPU 矩阵均如实记录为 blocked/not applicable；Stage 35/36 正负向门和 30 项 consumer 复验通过，retained JSON 不变，inventory、精确 Qwen 与空目录保持不变。
