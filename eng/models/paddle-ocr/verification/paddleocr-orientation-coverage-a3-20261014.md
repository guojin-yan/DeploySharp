# PaddleOCR orientation coverage audit (A3) (2026-10-14)

This audit scans every local OCR benchmark manifest for explicitly sourced natural direction/angle labels. It is a provenance/coverage audit, not an OCR accuracy result. Unknown, unavailable, vertical-only, geometry-inferred and generated transform fields are not treated as upright/inverted ground truth.

## Aggregate result

- Manifests: 8; image records: 243; instances: 33425.
- Non-empty text instances: 32827.
- Explicit natural direction labels: 0 on 0 images.
- Unknown orientation images: 209; vertical instances: 23.
- Non-zero angle metadata: 0 instances on 0 images.
- A3 data status: **blocked-no-natural-direction-labels**.

## Per-manifest counts

| Manifest | Images | Instances | Explicit natural labels | Unknown orientation images | Vertical instances | Non-zero angle metadata |
|---|---:|---:|---:|---:|---:|---:|
| funsd-test-full-001.jsonl | 40 | 6938 | 0 | 40 | 0 | 0 |
| funsd-test-smoke-001.jsonl | 10 | 1769 | 0 | 10 | 0 | 0 |
| funsd-train-full-001.jsonl | 129 | 18488 | 0 | 129 | 0 | 0 |
| funsd-train-smoke-001.jsonl | 20 | 3400 | 0 | 20 | 0 | 0 |
| hiertext-validation-sample-002.jsonl | 10 | 634 | 0 | 0 | 19 | 0 |
| hiertext-validation-sample-003.jsonl | 24 | 1654 | 0 | 0 | 4 | 0 |
| sroie-train-20260923T072455614249Z.jsonl | 5 | 267 | 0 | 5 | 0 | 0 |
| sroie-train-20260923T072550009854Z.jsonl | 5 | 275 | 0 | 5 | 0 | 0 |

## Boundary

The current local manifests do not provide an explicitly sourced natural upright/inverted/angle label when the status is blocked. Controlled SROIE rotations, vertical-text candidates and retry contracts remain useful execution/geometry evidence, but they cannot close natural A3 repair rate, rejection rate, CER/WER or coordinate-error gates. A future natural-angle dataset must retain source image identity, crop identity, annotator/source label, angle convention and license/provenance fields.

## Reproduction

```powershell
.\eng\models\paddle-ocr\scripts\Audit-PaddleOcrOrientationCoverage.ps1 `
  -ManifestRoot 'F:\OCRBenchmarkTesting\data\annotations\manifests'
  -OutputJson 'eng\models\paddle-ocr\verification\paddleocr-orientation-coverage-a3-20261014.json'
  -OutputMarkdown 'eng\models\paddle-ocr\verification\paddleocr-orientation-coverage-a3-20261014.md'
```
