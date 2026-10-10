# Relative document-link audit (2026-10-10)

This audit checks the current six PP-OCR/PP-Structure entry documents and the backend matrix after adding the PP-OCR core seven-model quality-evidence index. It validates only repository-relative local Markdown targets; external URLs and generated runtime paths are outside the scope.

The machine-readable result is [`document-link-audit-20261010-matrix-quality.json`](document-link-audit-20261010-matrix-quality.json): `355` local Markdown links were checked and `0` were broken.

Reproduce from the `DeploySharp` repository root:

```powershell
./eng/models/paddle-document/scripts/Test-PaddleDocumentLinks.ps1 `
  -OutputPath 'eng/models/paddle-document/verification/document-link-audit-20261010-matrix-quality.json'
```

The audit is a documentation integrity check, not evidence that any linked model, backend, or benchmark is supported. The linked SROIE records retain their smoke-only, word-box-versus-text-line and one-shot timing boundaries.
