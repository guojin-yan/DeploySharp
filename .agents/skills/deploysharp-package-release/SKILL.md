---
name: deploysharp-package-release
description: Prepare, verify, pack, or publish DeploySharp NuGet packages and coordinated GitHub release assets. Use for versioning, package boundaries, release workflows, or publication; publishing requires explicit user authorization.
---

# DeploySharp Package And Release

Preserve the repository's package boundaries. Each project under `src` is an independently versioned NuGet package unless an approved architecture change says otherwise.

## Preflight

1. Read `Directory.Build.props`, the target `.csproj`, `.github/workflows/ci.yml`, `.github/workflows/publish-nuget.yml`, and `docs/articles/package-combinations.md`.
2. Confirm package ID, target frameworks, project references, public dependencies, native-runtime ownership, symbols/source metadata, and version source.
3. Ensure adapters do not accidentally embed third-party native runtimes that the final application is expected to select.
4. Build and test the affected projects on their declared target frameworks, then create packages in a clean output directory and inspect archive contents.

Package restore success is not runtime validation. For native backends, also verify a clean consumer and missing-native diagnostic when those fixtures exist.

## Publication Boundary

Packing and local inspection are reversible. NuGet push, GitHub Release creation or modification, asset upload/removal, tags, and GitHub Packages publication are external mutations: perform them only when the user explicitly requests publication and the authenticated account/repository are confirmed.

Do not replace immutable model assets in place. Publish a new revision, update hashes and catalog entries, and keep backend/device-specific TensorRT engines out of portable Releases unless the asset is clearly scoped.

## Completion

Run `git diff --check`, focused tests, package creation, and package-content inspection. Keep preview and stable release notes aligned with the actual version. Report anything not tested; do not describe a package as published until the registry confirms it.
