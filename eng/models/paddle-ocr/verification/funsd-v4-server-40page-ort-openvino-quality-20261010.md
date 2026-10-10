# PP-OCR core-model backend quality comparison (2026-10-10)

This report compares complete DET → optional CLS → REC pipeline outputs on the same 40 selected pages from FUNSD (test). It is smoke evidence, not a release accuracy score or formal performance benchmark. Each page has one warm-up and one measured run.

- Machine: `JYPPX` / `Microsoft Windows 10.0.26200` / `X64`.
- Reference backend: `onnxruntime`; candidate backend(s): `openvino`.
- Models: 1; backend comparisons: 1; page runs: 40/40 successful.
- Selection SHA-256: `2ed518448155837cb0b81174a6030fda586f0abefccb8a3c123578876ab5c7be`; model, image, evaluator and assembly provenance are preserved in the JSON evidence.
- The dataset remains local smoke data pending image-specific redistribution review. No images, annotations, or predictions are included in the repository.

## Results

| Model | Backend | Pages | Det F1 | Matched CER/WER | End-to-end CER/WER | Text pages vs reference | Regions | Text mismatches | Max indexed polygon drift (px) | Max confidence drift | One-shot total P50/P95 (ms) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| v4-server | openvino | 40/40 | 20.27% | 29.15% / 50.65% | 181.58% / 144.55% | 38/40 | 2082 / 2082 | 2 | 6.87478 | 0.0267324 | 5979.98 / 12234.26 |

## Interpretation and limits

- The table reports each candidate backend relative to `onnxruntime` for the exact model assets and 40-page selection. A matching aggregate CER/F1 does not imply identical region outputs.
- Polygon and confidence drift are indexed comparisons only, and are omitted when a page has different region counts. They are not IoU-based region reassociation.
- End-to-end CER/WER can exceed 100% because missed labels count as deletions and unmatched predictions as insertions. Matched-crop CER alone overstates complete-page quality.
- Latency values in the JSON are one-shot observations across different pages; they are not 5-warmup/50-iteration performance results and must not be used as a backend ranking.
- This result covers only the backends named above. It does not promote untested backend/model combinations, and it does not close the formal P2 quality gate or the 5-warmup/50-iteration performance matrix.

## Reproduction

Run the quality evaluations first with the same selected manifest and protocol, then compare their local run directories. The script refuses to compare different model hashes, selected manifests, source revisions, assemblies or run protocols.

```powershell
uv run --offline python .\eng\models\paddle-ocr\scripts\Compare-PaddleOcrBackendQualityEvidence.py `
  --reference-root F:\OCRBenchmarkTesting\artifacts\deploysharp-v4-server-funsd40-ort-20261010 `
  --candidate-root F:\OCRBenchmarkTesting\artifacts\deploysharp-v4-server-funsd40-openvino-20261010 `
  --output-json eng\models\paddle-ocr\verification\funsd-v4-server-40page-ort-openvino-quality-20261010.json `
  --output-markdown eng\models\paddle-ocr\verification\funsd-v4-server-40page-ort-openvino-quality-20261010.md
```
