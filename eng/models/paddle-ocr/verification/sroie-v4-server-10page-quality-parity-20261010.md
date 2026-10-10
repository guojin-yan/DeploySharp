# PP-OCRv4 Server SROIE ten-page quality and ORT/OpenVINO parity (2026-10-10)

This record runs the complete `det → crop → optional cls → rec → merge` pipeline for PP-OCRv4 Server on the same ten local SROIE receipt pages with ONNX Runtime CPU and OpenVINO CPU. It is a reproducible smoke diagnostic, not an official SROIE line-accuracy score or a formal performance benchmark.

## Result

| Backend | Pages | IoU@0.5 TP/FP/FN | Det F1 | Matched CER/WER | End-to-end CER/WER | One-shot total P50/P95 (ms) |
|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 10/10 | 482/52/60 | 89.59% | 36.22% / 75.55% | 44.89% / 82.64% | 8063.73 / 10209.76 |
| OpenVINO CPU | 10/10 | 482/52/60 | 89.59% | 36.22% / 75.55% | 44.89% / 82.64% | 5901.97 / 19871.63 |

The two backends returned 534 regions each. Ordered page text matched on `10/10` pages with zero indexed region-text mismatches; polygon coordinates were identical and the maximum indexed confidence drift was `1.98e-5`. These are index-aligned comparisons and do not perform geometric reassociation.

## Reproduction

The run uses the two pinned SROIE five-page manifests from `F:\OCRBenchmarkTesting`, one warm-up and one measured iteration per page, batch size `16`, one inference channel, and `SlidingWindow` overflow handling (`overlap=0.2`, maximum `32` windows per region). Generate the two backend runs, merge them with `Merge-PaddleOcrPublicDatasetRuns.py`, evaluate with `Evaluate-DeploySharpPublicOcrDataset.py`, then compare with:

```powershell
uv run --offline python .\eng\models\paddle-ocr\scripts\Compare-PaddleOcrBackendQualityEvidence.py `
  --reference-root artifacts\public-ocr-evaluation\sroie-v4-server-ort-20261010-merged `
  --candidate-root artifacts\public-ocr-evaluation\sroie-v4-server-openvino-20261010-merged `
  --output-json artifacts\public-ocr-evaluation\sroie-v4-server-backend-compare-20261010\compare.json `
  --output-markdown artifacts\public-ocr-evaluation\sroie-v4-server-backend-compare-20261010\compare.md
```

The complete machine-readable summary is [sroie-v4-server-10page-quality-parity-20261010.json](sroie-v4-server-10page-quality-parity-20261010.json). The source images, annotations, predictions and raw runs remain local and are not committed.

## Limits

- SROIE annotations are word-level while PP-OCR emits text-line polygons; the reported IoU/CER/WER values are smoke diagnostics, not official line-level accuracy.
- This is one measured pass over ten distinct pages, not the formal `5 warm-up + 50 measured` performance protocol.
- The selection has 21 ground-truth regions with at least 32 characters, none with at least 128 or 3,200 characters; the natural long-text A2 gate remains open.
- Matching this ORT/OpenVINO pair does not promote PP-OCRv4 Server on CUDA, TensorRT or OpenCV DNN, and does not promote other model variants.
