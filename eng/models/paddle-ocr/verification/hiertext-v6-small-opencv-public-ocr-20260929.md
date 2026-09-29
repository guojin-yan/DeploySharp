# PP-OCR v6 Small OpenCV DNN HierText sliding-window cross-check (2026-09-29)

This is a local exploratory quality run over the 24-page HierText `sample-003` selection stored outside the repository at `F:\OCRBenchmarkTesting`. Images and annotations are not copied into Git or a Release.

## Protocol and results

- Model: PP-OCRv6 Small, OpenCV DNN CPU.
- Detector SHA-256: `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`.
- Recognizer SHA-256: `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Manifest SHA-256: `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- One warm-up and one measured iteration per page, recognition batch 16, one inference channel.
- SlidingWindow overlap `0.2`, maximum 32 windows per region, maximum 1,024 regions, maximum width 320.
- 24/24 pages completed with zero failures, empty outputs or timeouts.

| Metric | Result |
| --- | ---: |
| Detection TP / FP / FN at IoU 0.5 | 436 / 377 / 584 |
| Detection precision / recall / F1 | 53.63% / 42.75% / 47.57% |
| Matched-region CER / WER | 12.10% / 28.50% |
| End-to-end CER / WER | 73.65% / 93.50% |
| ≥32-character edits | 501 / 2,378 |
| ≥128-character edits | 100 / 520 |
| Total latency P50 / P95 across pages | 2,038.82 / 3,677.14 ms |

The detection and quality metrics are identical to the v6 Small ORT/OpenVINO runs. Ordered region text sequences match ORT on 23/24 pages; page `hiertext-validation-466c6ce044b8bd46` differs by one character in the serialized recognized text. This is close CPU behavior, not exact text parity. The selection contains no line over 3,200 characters, and the one-iteration timings do not satisfy the formal 5/50 performance protocol.

## Reproduction

```powershell
$runner = '.\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1'
$run = 'artifacts/public-ocr-evaluation/hiertext-v6-small-opencv-sliding-20260929'
$env:JYPPX_OPEN_CV_RUNTIME_PATH = (Resolve-Path 'tools\DeploySharp.PaddleOcrBenchmark\bin\Release\net10.0\runtimes\win-x64\native').Path
& $runner `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-003.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v6 -Variant small -Backend opencv-dnn `
  -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
  -MaximumRegions 1024 -OverflowMode SlidingWindow `
  -WindowOverlap 0.2 -MaximumWindowsPerRegion 32 `
  -OutputDirectory $run -ContinueOnFailure
```
