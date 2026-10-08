# PaddleOCR core seven-model ORT CUDA performance matrix (2026-10-08)

This is a fixed-device complete-pipeline performance record for PP-OCR v4/v5/v6. Every selected page uses five warm-up iterations and fifty measured iterations with batch=16 and one stage channel. The benchmark excludes model-load time.

- Machine: `JYPPX` / `Microsoft Windows 10.0.26200` / `X64` / 16 logical CPUs.
- Backend/device: `onnxruntime-cuda` / `cuda`; model pages: `70/70` successful.
- Protocol: warmup `5`, measured iterations `50`, batch `16`, inference channels `1`, overflow `Clamp`, window overlap `0.2`.
- Source revision: `c9e45a67740082e18c99c9efdff1d40008aa9af9`; benchmark assembly SHA-256: `6f3426f989f9a7c95fb0e572511d05e1de17eca96ae689ad38251d30e63da3ed`; selected manifest SHA-256: `5f334b20afbdc9303c3bb9426867fa3087c19bb45f78c889d612b4d2ae10512c`.
- P50/P95 columns are page-level percentiles over the fifty repetitions of each page. The model-level `median page P50` and `median page P95` columns summarize ten page-level percentiles; they are not a pooled dataset percentile.
- No GPU clock/power/frequency telemetry was captured by this runner. These numbers must not be described as a locked-clock or device-limit result.

## Summary

| Model | Pages | Batch | Channels | Median page P50 (ms) | Page P50 range (ms) | Median page P95 (ms) | Page P95 range (ms) | Median page mean (ms) | Preprocess median (ms) | Detection median (ms) | Recognition median (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| v4-mobile | 10/10 | 16 | 1 | 277.780 | 55.755–496.620 | 314.608 | 75.390–558.006 | 281.750 | 10.434 | 19.933 | 110.162 |
| v4-server | 10/10 | 16 | 1 | 415.828 | 127.474–782.692 | 495.066 | 137.912–944.024 | 421.504 | 10.346 | 80.871 | 184.351 |
| v5-mobile | 10/10 | 16 | 1 | 273.575 | 55.598–691.302 | 335.501 | 62.102–806.638 | 280.659 | 11.895 | 19.802 | 151.151 |
| v5-server | 10/10 | 16 | 1 | 474.673 | 117.275–1340.753 | 567.212 | 127.827–1542.102 | 491.092 | 11.184 | 68.639 | 313.073 |
| v6-medium | 10/10 | 16 | 1 | 357.265 | 90.581–851.669 | 552.139 | 157.257–1253.631 | 384.644 | 13.139 | 49.354 | 309.241 |
| v6-small | 10/10 | 16 | 1 | 237.323 | 49.704–790.159 | 286.466 | 57.544–1073.948 | 242.284 | 11.201 | 20.374 | 209.827 |
| v6-tiny | 10/10 | 16 | 1 | 136.051 | 37.723–419.655 | 175.312 | 43.579–575.723 | 139.976 | 10.360 | 15.809 | 117.832 |

## Interpretation and limits

- This matrix measures the DeploySharp full page path (`decode/preprocess → DET → crop → optional CLS → REC → merge`) on one Windows host and one ORT CUDA provider configuration. It is not a cross-device ranking.
- Recognition and crop work depend on the number and width of detected regions. Compare models only with the page-level region counts and the raw CSVs available in the local run directory.
- CUDA quality differences versus ORT CPU are reported separately in [the seven-model quality comparison](paddleocr-core-seven-models-ort-cuda-sample-002-quality-20261008.md). This matrix does not imply CPU/CUDA output equivalence, TensorRT support, or PP-Structure support.
- The raw run directories are intentionally not committed because they contain local dataset paths, images and per-page outputs. The JSON report preserves the protocol and per-page provenance needed to reproduce it.

## Reproduction

```powershell
pwsh -NoProfile -File .\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1 `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-002.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' -Version v6 -Variant tiny `
  -Backend onnxruntime-cuda -MaxImages 10 -Warmup 5 -Iterations 50 `
  -BatchSize 16 -InferenceChannels 1 `
  -OutputDirectory 'artifacts/public-ocr-evaluation/cuda-seven-models-performance-sample-002-20261008/v6-tiny'

uv run --offline python .\eng\models\paddle-ocr\scripts\Summarize-PaddleOcrPerformanceMatrix.py `
  --run-root .\artifacts\public-ocr-evaluation\cuda-seven-models-performance-sample-002-20261008 `
  --output-json .\eng\models\paddle-ocr\verification\paddleocr-core-seven-models-ort-cuda-performance-20261008.json `
  --output-markdown .\eng\models\paddle-ocr\verification\paddleocr-core-seven-models-ort-cuda-performance-20261008.md
```
