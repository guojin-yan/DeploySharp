# Stage 36: package provenance, licenses, symbols, and SBOM / 阶段 36：包来源、许可证、符号与 SBOM

Stage 36 adds a machine-executable release-evidence gate. The current retained baseline covers 12 packages, 106 target-framework groups, 48 managed dependency components, four consumer-owned native runtime components, 106 Release assembly/PDB records, 106 public API contracts, and a 39-entry official catalog identity. The evidence is a validated DeploySharp-specific JSON format; it is explicitly not SPDX or CycloneDX.

The retained files are:

| File | Bytes | SHA256 | Conclusion |
| --- | ---: | --- | --- |
| `eng/pack/release-evidence/package-provenance-sbom.json` | 223169 | `0a66f77aaf77f5b77131c99afd4d246abbf9b3e90e1bc5c30db8cb999d0f4f2b` | custom provenance/SBOM JSON; not SPDX/CycloneDX |
| `eng/pack/release-evidence/release-symbols.json` | 190552 | `6165c68b1bc65d0612085cd97c8c07575d5db0e63ddae64f2cb3671e5cd3e57e` | custom portable-PDB/SourceLink/SDK evidence |
| `eng/pack/release-evidence/public-api.json` | 608590 | `831abf93299d2e4b12abae282a9d04ba6a80acb2afe50e5c06f63c541d98e767` | custom public API metadata baseline |

The positive gate re-runs the package, dependency, license, repository, assembly-reference, internal-closure, PDB, SourceLink, API, official-catalog identity, and third-party-notice identity checks and then compares all three documents. The twelve-case negative suite rejects license removal, dependency content-hash drift, repository commit drift, SourceLink drift, API signature drift, SBOM omission, official-catalog identity drift, third-party-notice identity drift, release-blocker drift, native ownership confusion, advisory omission, and commercial-scope widening. All mutations are disposable and are not part of the retained baseline.

The repository commit and SourceLink mapping are checked live against `HEAD`. Only the PE/PDB and informational-version hashes that necessarily embed that commit are normalized during retained comparison; stable dependency, license, API surface, sequence-point, compiler, and ownership fields remain exact. This makes the committed evidence reusable without requiring an impossible evidence file that contains the hash of its own commit.

This Stage 36 snapshot was intentionally not release-eligible. Its manual-review blockers included legacy license URLs/files and incomplete repository metadata in restored dependencies, absolute source paths in portable PDB documents, the then-current `not-produced` symbol-package policy, unsigned packages, a dirty worktree, lack of publication authority, and raw nupkg container-bit reproducibility. The current policy requires `.snupkg` packages and verifies normalized raw containers from two independent packs before signing; it still does not authorize signing, publication, upload, or model redistribution.

The exact Qwen GGUF, its five sidecars, and `deploysharp-stage31-runtime.json` remain byte-for-byte unchanged. Qwen remains a Preview catalog entry with `AlgorithmVerified=false`; its exact Release assets remain bound, while the official catalog now contains 39 entries. Admission must still be run with `-RequireAdmitted` before any real CPU gate.
