# PaddleOCR / PP-Structure documentation link audit (2026-10-02)

The repository checks six primary PaddleOCR/PP-Structure documents with the tracked `Test-PaddleDocumentLinks.ps1` script:

- `docs/articles/visual-ocr.md`
- `docs/articles/visual-paddle-structure.md`
- `eng/models/paddle-ocr/README.md`
- `eng/models/paddle-document/README.md`
- `docs/model-backend-verification-matrix.md`
- `eng/models/paddle-document/verification/chart2table-extended-quality-20261002.md`

The audit resolves relative links against each document directory, accepts GitHub-style directory links when that directory contains `README.md` or `index.md`, removes anchors/query strings, and intentionally skips external URLs and generated runtime paths. The latest rerun on 2026-10-04 checked `149` local links and found `0` broken links. It also caught and fixed the `eng/models/paddle-ocr/README.md` link to `docs/articles/visual-ocr.md`, which required three parent-directory segments rather than two.

Machine-readable output: [document-link-audit-20261002.json](document-link-audit-20261002.json).

Reproduce from the repository root:

```powershell
pwsh -NoProfile -File eng/models/paddle-document/scripts/Test-PaddleDocumentLinks.ps1
```
