# PP-OCR core-model backend quality comparison (2026-10-08)

This report compares complete DET → optional CLS → REC pipeline outputs on the same ten HierText `sample-002` pages. It is smoke evidence, not a release accuracy score or formal performance benchmark. Each page has one warm-up and one measured run.

- Machine: `JYPPX` / `Microsoft Windows 10.0.26200` / `X64`.
- Reference backend: `onnxruntime`; candidate backend(s): `onnxruntime-cuda`.
- Models: 7; backend comparisons: 7; page runs: 70/70 successful.
- Selection SHA-256: `5f334b20afbdc9303c3bb9426867fa3087c19bb45f78c889d612b4d2ae10512c`; model, image, evaluator and assembly provenance are preserved in the JSON evidence.
- The dataset remains local smoke data pending image-specific redistribution review. No images, annotations, or predictions are included in the repository.

## Results

| Model | Backend | Pages | Det F1 | Matched CER/WER | End-to-end CER/WER | Text pages vs reference | Regions | Text mismatches | Max indexed polygon drift (px) | Max confidence drift | One-shot total P50/P95 (ms) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| v4-mobile | onnxruntime-cuda | 10/10 | 40.09% | 47.41% / 76.47% | 94.04% / 111.67% | 5/10 | 346 / 346 | 10 | 2.0085 | 0.486952 | 315.31 / 593.06 |
| v4-server | onnxruntime-cuda | 10/10 | 40.36% | 39.36% / 66.02% | 103.53% / 114.98% | 8/10 | 345 / 345 | 2 | 0.99283 | 0.0515458 | 474.90 / 764.97 |
| v5-mobile | onnxruntime-cuda | 10/10 | 25.79% | 35.58% / 64.08% | 123.38% / 136.44% | 4/10 | 377 / 378 | 86 | n/a (region count differs) | n/a (region count differs) | 285.96 / 618.59 |
| v5-server | onnxruntime-cuda | 10/10 | 41.66% | 48.17% / 64.73% | 107.48% / 117.03% | 5/10 | 404 / 404 | 15 | 7.1105 | 0.252187 | 520.46 / 1156.03 |
| v6-medium | onnxruntime-cuda | 10/10 | 38.64% | 18.33% / 50.63% | 105.29% / 125.08% | 6/10 | 431 / 431 | 6 | 4.04375 | 0.219089 | 343.58 / 762.65 |
| v6-small | onnxruntime-cuda | 10/10 | 33.63% | 16.61% / 49.48% | 111.18% / 126.97% | 6/10 | 367 / 367 | 6 | 1.1286 | 0.0538591 | 265.10 / 641.02 |
| v6-tiny | onnxruntime-cuda | 10/10 | 27.25% | 20.85% / 51.01% | 110.58% / 124.45% | 5/10 | 306 / 307 | 21 | n/a (region count differs) | n/a (region count differs) | 146.79 / 336.83 |

## Interpretation and limits

- The table reports each candidate backend relative to `onnxruntime` for the exact model assets and ten-page selection. A matching aggregate CER/F1 does not imply identical region outputs.
- Polygon and confidence drift are indexed comparisons only, and are omitted when a page has different region counts. They are not IoU-based region reassociation.
- End-to-end CER/WER can exceed 100% because missed labels count as deletions and unmatched predictions as insertions. Matched-crop CER alone overstates complete-page quality.
- Latency values in the JSON are one-shot observations across different pages; they are not 5-warmup/50-iteration performance results and must not be used as a backend ranking.
- This result covers only the backends named above. It does not promote untested backend/model combinations, and it does not close the formal P2 quality gate or the 5-warmup/50-iteration performance matrix.

## Reproduction

Run the quality evaluations first with the same selected manifest and protocol, then compare their local run directories. The script refuses to compare different model hashes, selected manifests, source revisions, assemblies or run protocols.

```powershell
uv run --offline python .\eng\models\paddle-ocr\scripts\Compare-PaddleOcrBackendQualityEvidence.py `
  --reference-root artifacts\public-ocr-evaluation\cpu-seven-models-sample-002-20261008 `
  --candidate-root artifacts\public-ocr-evaluation\cuda-seven-models-sample-002-20261008 `
  --output-json eng\models\paddle-ocr\verification\paddleocr-core-seven-models-ort-cuda-sample-002-quality-20261008.json `
  --output-markdown eng\models\paddle-ocr\verification\paddleocr-core-seven-models-ort-cuda-sample-002-quality-20261008.md
```
