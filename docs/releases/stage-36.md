# Stage 36: package provenance, licenses, symbols, and SBOM / 阶段 36：包来源、许可证、符号与 SBOM

Stage 36 adds a machine-executable release-evidence gate for the nine Stage 35 packages. The current retained baseline covers 9 packages, 82 target-framework groups, 47 managed dependency components, four consumer-owned native runtime components, 82 Release assembly/PDB records, and 82 public API contracts. The evidence is a validated DeploySharp-specific JSON format; it is explicitly not SPDX or CycloneDX.

The retained files are:

| File | Bytes | SHA256 | Conclusion |
| --- | ---: | --- | --- |
| `eng/pack/release-evidence/package-provenance-sbom.json` | 177618 | `3fad8a44644e6dc94e2d2642cdfb55e080e87b7787a373a2e13f5d8111f89b0f` | custom provenance/SBOM JSON; not SPDX/CycloneDX |
| `eng/pack/release-evidence/release-symbols.json` | 146900 | `e895e23babf21a0b5fe1f4370c93c79b03c53ade647db70804adc2f38eb8150c` | custom portable-PDB/SourceLink/SDK evidence |
| `eng/pack/release-evidence/public-api.json` | 518847 | `3d471a11ed0a95e298d0f709a8275effb9af175d1033466d9b6e8a5b14bea0c4` | custom public API metadata baseline |

The positive gate re-runs the package, dependency, license, repository, assembly-reference, internal-closure, PDB, SourceLink, and API checks and then compares all three documents. The seven-case negative suite rejects license removal, dependency content-hash drift, repository commit drift, SourceLink drift, API signature drift, SBOM omission, and native ownership confusion. All mutations are disposable and are not part of the retained baseline.

The repository commit and SourceLink mapping are checked live against `HEAD`. Only the PE/PDB and informational-version hashes that necessarily embed that commit are normalized during retained comparison; stable dependency, license, API surface, sequence-point, compiler, and ownership fields remain exact. This makes the committed evidence reusable without requiring an impossible evidence file that contains the hash of its own commit.

Current evidence is intentionally not release-eligible. Manual-review blockers include legacy license URLs/files and incomplete repository metadata in restored dependencies, absolute source paths in portable PDB documents, the `not-produced` symbol-package policy, unsigned packages, a dirty worktree, lack of publication authority, and the fact that raw nupkg container-bit reproducibility is not established. No dependency was upgraded and no signing, publication, upload, Actions workflow, model conversion, or model execution was authorized by this stage.

The exact Qwen GGUF, its five sidecars, and `deploysharp-stage31-runtime.json` remain byte-for-byte unchanged. Qwen remains External with `AlgorithmVerified=false`, `uploaded=false`, `downloadable=false`; the official catalog remains empty. Admission must still be run with `-RequireAdmitted` before any real CPU gate.
