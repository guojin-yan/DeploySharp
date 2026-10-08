# PP-OCR core-model CPU backend quality comparison (2026-10-08)

This report compares complete DET → optional CLS → REC pipeline outputs on the same ten HierText `sample-002` pages. It is smoke evidence, not a release accuracy score or formal performance benchmark. Each page has one warm-up and one measured run.

- Machine: `JYPPX` / `Microsoft Windows 10.0.26200` / `X64`.
- Models: 7; backend comparisons: 14; page runs: 140/140 successful.
- Selection SHA-256: `5f334b20afbdc9303c3bb9426867fa3087c19bb45f78c889d612b4d2ae10512c`; model, image, evaluator and assembly provenance are preserved in the JSON evidence.
- The dataset remains local smoke data pending image-specific redistribution review. No images, annotations, or predictions are included in the repository.

## Results

| Model | Backend | Pages | Det F1 | Matched CER/WER | End-to-end CER/WER | Text pages vs ORT | Regions | Text mismatches | Max indexed polygon drift (px) | Max confidence drift | One-shot total P50/P95 (ms) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| v4-mobile | openvino | 10/10 | 40.09% | 47.41% / 76.47% | 94.11% / 111.67% | 10/10 | 346 / 346 | 0 | 0.694925 | 0.00039691 | 205.79 / 519.87 |
| v4-server | openvino | 10/10 | 40.36% | 39.36% / 66.02% | 103.53% / 114.98% | 10/10 | 345 / 345 | 0 | 0 | 1.563e-05 | 3010.99 / 9607.87 |
| v5-mobile | openvino | 10/10 | 25.60% | 35.73% / 63.83% | 123.41% / 136.59% | 10/10 | 377 / 377 | 0 | 0 | 2.0596e-05 | 846.08 / 1957.20 |
| v5-server | openvino | 10/10 | 41.66% | 47.57% / 64.73% | 107.86% / 117.03% | 9/10 | 404 / 404 | 1 | 1.47354 | 0.0677949 | 2352.37 / 5024.77 |
| v6-medium | openvino | 10/10 | 38.64% | 18.42% / 50.63% | 105.36% / 125.08% | 10/10 | 431 / 431 | 0 | 0 | 2.185e-05 | 1395.58 / 3332.66 |
| v6-small | openvino | 10/10 | 33.63% | 16.61% / 49.48% | 111.21% / 126.97% | 10/10 | 367 / 367 | 0 | 0 | 2.68e-05 | 292.60 / 649.59 |
| v6-tiny | openvino | 10/10 | 27.28% | 21.11% / 51.68% | 110.54% / 124.45% | 10/10 | 306 / 306 | 0 | 0 | 8.27e-06 | 138.75 / 230.11 |
| v4-mobile | opencv-dnn | 10/10 | 40.09% | 47.41% / 76.47% | 94.11% / 111.67% | 10/10 | 346 / 346 | 0 | 0.694925 | 0.00039794 | 1716.19 / 3696.93 |
| v4-server | opencv-dnn | 10/10 | 40.36% | 39.36% / 66.02% | 103.53% / 114.98% | 10/10 | 345 / 345 | 0 | 0 | 1.592e-05 | 7045.44 / 10928.46 |
| v5-mobile | opencv-dnn | 10/10 | 25.60% | 35.73% / 63.83% | 123.41% / 136.59% | 10/10 | 377 / 377 | 0 | 0 | 4.79e-05 | 1665.61 / 4127.24 |
| v5-server | opencv-dnn | 10/10 | 41.45% | 47.60% / 64.58% | 108.29% / 117.19% | 10/10 | 404 / 404 | 0 | 0 | 2.333e-05 | 5776.31 / 10307.79 |
| v6-medium | opencv-dnn | 10/10 | 38.64% | 18.42% / 50.63% | 105.36% / 125.08% | 10/10 | 431 / 431 | 0 | 0 | 7.9e-06 | 4971.10 / 9399.77 |
| v6-small | opencv-dnn | 10/10 | 33.63% | 16.61% / 49.48% | 111.21% / 126.97% | 10/10 | 367 / 367 | 0 | 0.18994 | 0.004521 | 1543.69 / 2817.12 |
| v6-tiny | opencv-dnn | 10/10 | 27.28% | 21.11% / 51.68% | 110.54% / 124.45% | 10/10 | 306 / 306 | 0 | 0 | 1.508e-05 | 630.58 / 989.45 |

## Interpretation and limits

- The table reports each backend relative to ORT CPU for the exact model assets and ten-page selection. A matching aggregate CER/F1 does not imply identical region outputs.
- Polygon and confidence drift are indexed comparisons only, and are omitted when a page has different region counts. They are not IoU-based region reassociation.
- End-to-end CER/WER can exceed 100% because missed labels count as deletions and unmatched predictions as insertions. Matched-crop CER alone overstates complete-page quality.
- Latency values in the JSON are one-shot observations across different pages; they are not 5-warmup/50-iteration performance results and must not be used as a backend ranking.
- This result covers ORT CPU, OpenVINO CPU and OpenCV DNN CPU only. It does not change CUDA or TensorRT support status, and it does not close the formal P2 quality gate.

## Reproduction

Run the quality evaluations first with the same selected manifest and protocol, then compare their local run directories. The script refuses to compare different model hashes, selected manifests, source revisions, assemblies or run protocols.

```powershell
uv run --offline python .\eng\models\paddle-ocr\scripts\Compare-PaddleOcrBackendQualityEvidence.py `
  --reference-root .\artifacts\public-ocr-evaluation\core-quality-20261008-125111 `
  --candidate-root .\artifacts\public-ocr-evaluation\core-seven-models-openvino-sample-002-20261008 `
  --candidate-root .\artifacts\public-ocr-evaluation\core-seven-models-opencv-dnn-sample-002-20261008 `
  --output-json .\eng\models\paddle-ocr\verification\paddleocr-core-seven-models-three-cpu-backend-sample-002-quality-20261008.json `
  --output-markdown .\eng\models\paddle-ocr\verification\paddleocr-core-seven-models-three-cpu-backend-sample-002-quality-20261008.md
```
