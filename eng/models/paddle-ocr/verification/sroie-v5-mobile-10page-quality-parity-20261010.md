# PP-OCRv5 Mobile SROIE ten-page quality and ORT/OpenVINO parity (2026-10-10)

This record reruns the complete `det → crop → optional cls → rec → merge` pipeline for PP-OCRv5 Mobile on the same ten local SROIE receipt pages with ONNX Runtime CPU and OpenVINO CPU. It uses the current source revision and supersedes neither the earlier three-backend smoke record nor the documented limits. It is a reproducible smoke diagnostic, not an official SROIE line-accuracy score or a formal performance benchmark.

## Result

| Backend | Pages | IoU@0.5 TP/FP/FN | Det F1 | Matched CER/WER | End-to-end CER/WER | One-shot total P50/P95 (ms) |
|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 10/10 | 492/46/50 | 91.11% | 38.09% / 69.80% | 45.11% / 76.40% | 2640.82 / 3289.84 |
| OpenVINO CPU | 10/10 | 492/46/50 | 91.11% | 38.09% / 69.80% | 45.11% / 76.40% | 1113.67 / 1468.35 |

The two backends returned 538 regions each. Ordered page text matched on `10/10` pages with zero indexed region-text mismatches; polygon coordinates were identical and the maximum indexed confidence drift was `2.25e-5`. These are index-aligned comparisons and do not perform geometric reassociation.

## Reproduction

The run uses the two pinned SROIE five-page manifests from `F:\OCRBenchmarkTesting`, one warm-up and one measured iteration per page, batch size `16`, one inference channel, and `SlidingWindow` overflow handling (`overlap=0.2`, maximum `32` windows per region). Generate the two backend runs, merge them with `Merge-PaddleOcrPublicDatasetRuns.py`, evaluate with `Evaluate-DeploySharpPublicOcrDataset.py`, then compare with:

```powershell
uv run --offline python .\eng\models\paddle-ocr\scripts\Compare-PaddleOcrBackendQualityEvidence.py `
  --reference-root artifacts\public-ocr-evaluation\sroie-v5-mobile-ort-20261010-merged `
  --candidate-root artifacts\public-ocr-evaluation\sroie-v5-mobile-openvino-20261010-merged `
  --output-json artifacts\public-ocr-evaluation\sroie-v5-mobile-backend-compare-20261010\compare.json `
  --output-markdown artifacts\public-ocr-evaluation\sroie-v5-mobile-backend-compare-20261010\compare.md
```

The complete machine-readable summary is [sroie-v5-mobile-10page-quality-parity-20261010.json](sroie-v5-mobile-10page-quality-parity-20261010.json). The earlier [three-backend smoke record](sroie-v5-mobile-public-ocr-smoke-20260928.md) remains useful for the OpenCV DNN boundary; its source revision and asset hashes are not interchangeable with this current-revision pair.

## Limits

- SROIE annotations are word-level while PP-OCR emits text-line polygons; the reported IoU/CER/WER values are smoke diagnostics, not official line-level accuracy.
- This is one measured pass over ten distinct pages, not the formal `5 warm-up + 50 measured` performance protocol.
- The selection has 21 ground-truth regions with at least 32 characters, none with at least 128 or 3,200 characters; the natural long-text A2 gate remains open.
- Matching this ORT/OpenVINO pair does not promote PP-OCRv5 Mobile on CUDA or TensorRT, and does not replace the earlier OpenCV DNN smoke scope.
