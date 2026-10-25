# HierText public OCR evaluation — PP-OCRv6 Tiny / ONNX Runtime CPU (2026-10-25)

This report records a reproducible ten-image end-to-end OCR evaluation on the local public `OCRBenchmarkTesting` selection. It is a quality smoke slice for the current v6 Tiny artifacts, not a release accuracy claim or a formal latency benchmark.

## Protocol

| Item | Value |
| --- | --- |
| Dataset | `google-research-datasets/hiertext`, validation split |
| Manifest | `hiertext-validation-sample-002.jsonl`, 10 images, source revision `70b6620b2b112597d8219e11eee9773a1403827c` |
| Manifest SHA-256 | `e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7` |
| Backend | ONNX Runtime CPU |
| Model | PP-OCRv6 Tiny detector + recognizer; no separate classifier in this artifact |
| Detector SHA-256 | `193bab7a04fca699a6c82e6abb5b81bdb28177f0abd4062552b04908dafb19f8` |
| Recognizer SHA-256 | `9ef676d6ed3c88256a2d92c640c44f25b0c40947e111b14b8be8f594091563e6` |
| Batch / inference channels | `8 / 2` |
| Recognition overflow | `SlidingWindow`, overlap `0.2`, maximum 32 windows per region |
| Warm-up / measured iterations | `1 / 1` per image |
| Host | Windows 10.0.26200, 16 logical processors, .NET 10.0.12 |
| Source revision | `aa64a4f6c22ce2410c4c7f86d3324df73eb7f347` |

## Results

The run completed all 10 images with 10 non-empty prediction records and no benchmark process failures. At IoU 0.5, the detector produced `TP/FP/FN = 115/191/422`, precision `37.58%`, recall `21.42%` and F1 `27.28%`. On the 115 matched regions, recognition exact-match rate was `44.35%`, CER `22.54%` and WER `55.70%`. End-to-end CER/WER, which includes missed ground-truth regions and unmatched predictions, were `113.33%/126.50%`.

The single measured total-time observations across the ten pages were P50 `219.440 ms`, P95 `361.046 ms`, minimum `157.689 ms`, maximum `361.046 ms`, and mean `229.771 ms`. These values use one measured iteration and an unlocked host; they are diagnostic observations and are not the repository's formal `5 warm-up + 50 measured` performance protocol.

No selected ground-truth region contained at least 32 characters, so this ten-page slice provides no evidence for the A2 normalized-width `>3,200 px` gate. The low detector recall and word-box/line-output mismatch are retained as measured limitations rather than hidden by filtering. The full machine-readable evaluation, including per-image metrics, provenance, model hashes and the recorded benchmark assembly hash, is [`hiertext-v6-tiny-10page-public-ocr-20261025.json`](hiertext-v6-tiny-10page-public-ocr-20261025.json).

## Reproduction

Build the benchmark tool, then run the public selection:

```powershell
pwsh -NoProfile -File eng/models/paddle-ocr/scripts/Invoke-PaddleOcrPublicDataset.ps1 `
  -ManifestPath F:/OCRBenchmarkTesting/data/annotations/manifests/hiertext-validation-sample-002.jsonl `
  -DatasetRoot F:/OCRBenchmarkTesting `
  -ModelRoot E:/Model/paddleocr `
  -BenchmarkDll tools/DeploySharp.PaddleOcrBenchmark/bin/Release/net10.0/DeploySharp.PaddleOcrBenchmark.dll `
  -OutputDirectory E:/DeploySharpEvidence/paddleocr-hiertext-v6-tiny-10page-20261025-ort `
  -Version v6 -Variant tiny -Backend onnxruntime `
  -Warmup 1 -Iterations 1 -BatchSize 8 -InferenceChannels 2 `
  -MaxImages 10 -OverflowMode SlidingWindow -WindowOverlap 0.2 `
  -MaximumWindowsPerRegion 32
```

Then score the preserved CSV/width reports with the local evaluator:

```powershell
python eng/models/paddle-ocr/scripts/Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest E:/DeploySharpEvidence/paddleocr-hiertext-v6-tiny-10page-20261025-ort/selected-manifest.jsonl `
  --dataset-root F:/OCRBenchmarkTesting `
  --run-directory E:/DeploySharpEvidence/paddleocr-hiertext-v6-tiny-10page-20261025-ort `
  --ocrbench-root F:/OCRBenchmarkTesting `
  --predictions E:/DeploySharpEvidence/paddleocr-hiertext-v6-tiny-10page-20261025-ort/predictions.json `
  --report E:/DeploySharpEvidence/paddleocr-hiertext-v6-tiny-10page-20261025-ort/evaluation.json
```

The source manifest marks this selection `smoke_only_until_each_image_landing_page_and_redistribution_reviewed`; the source image and annotation terms remain authoritative. This evidence does not close A2 natural-wide-line quality, B1b degradation robustness, backend parity, or formal performance gates.
