---
name: deploysharp-documentation
description: Create, simplify, or review DeploySharp README, tutorials, support matrices, release notes, and maintainer documentation. Use for repository documentation work; keep tutorials detailed and development chronology out of public docs.
---

# DeploySharp Documentation

Write for library users first and maintainers second. Public docs explain how to install, configure, run, troubleshoot, and reproduce current behavior; they are not a diary of development experiments.

## Information Architecture

- Keep the root README concise: project value, current release, package badges, quick code, supported platforms/models, tutorial links, performance link, license, and contact.
- Keep task tutorials detailed enough to complete a realistic workflow, including ownership, cancellation, error handling, configuration, and limitations.
- Keep architecture, compatibility, validation, and maintainer pages concise. State the current contract and decision, not every dated attempt.
- Maintain one canonical page per subject. Merge "getting started", "compatibility", "security", and CLI pages when they describe one small component.
- Put exact per-model backend status in `docs/model-backend-verification-matrix.md` and performance summaries in `docs/articles/device-performance-benchmarks.md`.

## Content Rules

- Never use a local absolute path in user instructions. Use repository-relative paths, placeholders, or `Path.Combine`/`Join-Path` examples.
- Do not link to temporary reports, raw TRX/CSV/JSON, generated engines, private machines, or files excluded from the public repository.
- Distinguish supported, preview, unverified, unsupported, smoke, parity, accuracy, and performance.
- Do not claim all models or backends from a representative test.
- Keep English and Chinese README structure and links aligned without forcing literal translation.
- Preserve useful API examples and real constraints when deleting historical prose.

Development audits and chronological evidence belong outside the public repository when an approved archive exists. Do not move or delete such files without checking references and preserving unrelated work.

## Validation

After edits, verify every local Markdown/HTML link, YAML and JSON syntax, README anchors used by samples, documentation build when available, and `git diff --check`. Report deleted/merged pages and the canonical replacement so maintainers can update external links.
