# Stage 36 package provenance, license, symbol, and SBOM evidence / 阶段 36 包来源、许可证、符号与 SBOM 证据门

Stage 36 adds `eng/pack/Test-ReleaseEvidence.ps1` and a retained evidence set under `eng/pack/release-evidence/`. The gate reads the nine Stage 35 pack definitions, all nine lock/assets pairs, generated nuspecs, local NuGet cache metadata, Release assemblies/PDBs, public XML contracts, the exact Qwen manifest, and the empty official catalog. It does not restore from the network, change dependencies, sign packages, or publish artifacts. / 阶段 36 新增 `eng/pack/Test-ReleaseEvidence.ps1`，并在 `eng/pack/release-evidence/` 保留机器可读证据。门禁读取九个 Stage 35 包定义、九组 lock/assets、生成的 nuspec、本地 NuGet 缓存元数据、Release 程序集/PDB、公共 XML 合同、精确 Qwen Manifest 和空 official catalog；不联网恢复、不升级依赖、不签名、不发布。

## Evidence formats / 证据格式

The three retained documents are deliberately declared as a validated custom JSON format, not SPDX or CycloneDX. The format records its media type and explicitly states that it does not claim either standard. A consumer can parse the documents as JSON and use the stable schema fields below.

| File | Machine-readable contract |
| --- | --- |
| `eng/pack/release-evidence/package-provenance-sbom.json` | nine DeploySharp packages, 82 TFM groups, managed dependency closure, resolved lock/assets hash, cached nupkg SHA512, raw nupkg SHA256, license source/status, repository metadata, ownership scopes, model license boundary, and release blockers |
| `eng/pack/release-evidence/release-symbols.json` | 82 Release assembly/PDB records with MVID, deterministic marker, portable PDB ID, documents/sequence-point digests, compiler/SDK options, SourceLink commit/status, source path mode, and symbol blockers |
| `eng/pack/release-evidence/public-api.json` | public/protected XML member IDs plus visible metadata, generic constraints, nullable/custom attributes, defaults, and assembly-reference hashes for all supported TFMs |

The package provenance document records both the lock/assets resolved content hash and the local cache `.nupkg.sha512`. They are separate identities: the cached SHA512 is checked against the actual cached nupkg, while `.nupkg.metadata` must bind that cache entry to the lock/assets content hash. `-PackageCacheDirectory` selects the exact restored cache explicitly; otherwise `NUGET_PACKAGES` or the standard per-user cache is used. A raw nupkg SHA256 is retained independently from semantic payload evidence because NuGet container metadata can vary between equivalent packs. / 包来源证据同时校验缓存 nupkg 的原始 SHA512 与 `.nupkg.metadata` 中绑定 lock/assets 的 content hash；可通过 `-PackageCacheDirectory` 显式选择精确恢复缓存，默认依次使用 `NUGET_PACKAGES` 和标准用户缓存。

Repository commits and SourceLink are validated live against the current `HEAD`. Commit injection also changes deterministic PE/PDB identities and the assembly informational-version attribute even when the public contract and source sequence points are unchanged. The retained comparison therefore normalizes only those enumerated commit-bound fields after the live `HEAD` checks; dependency/license identities, API surface, nullable/generic metadata, sequence points, compiler options, ownership, and all other stable fields remain exact baseline comparisons. A retained dirty worktree is likewise historical: the gate first compares every non-`dirty-worktree` release blocker exactly, then normalizes only the linked `subject.repositoryDirty` and `dirty-worktree` pair. This avoids an impossible self-referential evidence-commit hash while preserving repository-commit, SourceLink, and release-blocker negative gates. / Repository commit 与 SourceLink 先实时强制匹配当前 `HEAD`；随后 retained 比对只归一化明确列举的 commit-bound PE/PDB/attribute 字段，其余稳定字段继续精确比较。retained 的脏工作树同属历史状态：门禁先精确比较除 `dirty-worktree` 外的所有 release blocker，再只归一化关联的 `subject.repositoryDirty` 和 `dirty-worktree` 对，从而避免 evidence commit 自引用而不削弱 repository、SourceLink 与 release-blocker 负向门。

DeploySharp package licenses, managed dependency licenses, consumer-owned native runtime licenses, and external model licenses are separate ownership scopes. The four native runtime packages (`LLamaSharp.Backend.Cpu`, `Microsoft.ML.OnnxRuntime`, `OpenVINO.runtime.win`, and `JYPPX.OpenCV.runtime.win-x64`) are recorded as consumer-owned and are rejected if they appear in a DeploySharp nuspec or package payload. License expressions outside the verified SPDX allow-list, license files, license URLs, missing repository metadata, and other manual-review cases remain explicit blockers.

## API and symbol policy / API 与符号策略

API evidence compares the same XML contract across every supported TFM and supplements it with metadata-level signatures and attributes. Internal assembly references are checked against the nuspec dependency transitive closure. This stage fixes no API difference and does not expand ModelFactory or model capabilities.

Release outputs must contain portable PDBs and the deterministic PE marker. SourceLink is parsed and its commit must equal the current repository head. The retained symbol document also records the `global.json` SDK version, actual SDK version, and MSBuild identity. `DeterministicSourcePaths` maps document paths to the compiler's stable `/_/` virtual root; physical local filesystem paths remain a release blocker. The current policy is `not-produced` for symbol packages, so symbol-package authorization remains a release blocker even when assembly/PDB semantics are stable.

## Negative suite / 负向套件

`eng/pack/Invoke-ReleaseEvidenceNegativeTests.ps1` creates eight independent temporary mutations and requires rejection for license metadata removal, dependency content-hash drift, repository commit drift, SourceLink drift, API signature drift, SBOM omission, non-worktree release-blocker drift, and native ownership confusion. The caller supplies the temporary working directory; it must be removed after the run. Pass `-PackageCacheDirectory` when the candidate was restored against an explicit offline cache, so each mutation uses the same dependency identity. / 当候选使用显式离线缓存恢复时，负向套件也传入 `-PackageCacheDirectory`，确保每项突变使用相同的依赖身份。

The gate is normally run after the Stage 35 package gate:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File eng/pack/Test-ReleaseEvidence.ps1 `
  -PackageDirectory artifacts/stage36-pack-current `
  -PackageCacheDirectory artifacts/stage36-package-cache `
  -EvidenceDirectory eng/pack/release-evidence
```

The exact Qwen GGUF and retained Stage 31 runtime evidence are outside this evidence set. Before any real CPU execution, recompute their hashes and run `Test-GgufAdmission.ps1 -RequireAdmitted`; any drift is an immediate blocker.
