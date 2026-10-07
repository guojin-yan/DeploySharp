# PP-Structure documentation link audit (2026-10-07)

The repository's `Test-PaddleDocumentLinks.ps1` checks relative local Markdown links across the PP-OCR README, PP-Structure README, PP-Structure article, backend matrix and Chart2Table quality report. This rerun includes the PP-DocLayout_plus-L and PP-DocBlockLayout dynamic Batch report. Results: **227 links checked; 0 broken**.

The audit checks local relative targets only. External URLs and generated runtime paths are outside its scope. Machine-readable details: [JSON report](document-link-audit-20261007-additional-layout.json). Reproduce from the repository root with:

```powershell
./eng/models/paddle-document/scripts/Test-PaddleDocumentLinks.ps1 -OutputPath 'eng/models/paddle-document/verification/document-link-audit-20261007-additional-layout.json'
```
