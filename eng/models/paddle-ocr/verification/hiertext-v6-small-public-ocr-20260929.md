# PP-OCR v6 Small HierText sliding-window cross-check (2026-09-29)

This is a local exploratory quality run over the 24-page HierText `sample-003` selection stored outside the repository at `F:\OCRBenchmarkTesting`. Images and annotations are not copied into Git or a Release. The source audit keeps this selection `smoke_only_until_each_image_landing_page_and_redistribution_reviewed`; image and annotation terms are recorded in the local manifest.

## Protocol

- Model: PP-OCRv6 Small, ONNX Runtime CPU.
- Model ONNX SHA-256: detector `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`; recognizer `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Dataset manifest: `F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-003.jsonl`; source manifest SHA-256 `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- 24 pages, one warm-up and one measured iteration per page, recognition batch 16, one inference channel.
- SlidingWindow overlap `0.2`, maximum 32 windows per region, maximum 1,024 regions, detector/recognizer maximum width 320.
- All 24 pages completed with zero process failures, empty outputs or timeouts.
- This is a one-iteration quality run. Its latency values are observations and do not satisfy the formal 5/50 performance protocol.

## Quality results

| Metric | Result |
| --- | ---: |
| Detection TP / FP / FN at IoU 0.5 | 436 / 377 / 584 |
| Detection precision / recall / F1 | 53.63% / 42.75% / 47.57% |
| Matched-region CER / WER | 12.10% / 28.50% |
| End-to-end CER / WER | 73.65% / 93.50% |
| Matched exact text rate | 55.96% (244 / 436) |
| Total latency P50 / P95 across pages | 981.89 / 2,173.75 ms |
| Detection latency P50 / P95 | 274.43 / 490.56 ms |
| Recognition latency P50 / P95 | 691.67 / 1,740.82 ms |

Long-text buckets contain 42 lines with at least 32 characters and 4 lines with at least 128 characters. SlidingWindow produced `501/2,378` character edits for the ≥32-character bucket (21.07% CER) and `100/520` for the ≥128-character bucket (19.23% CER). The selection contains no line over 3,200 characters, so the A2 >3200 gate remains open.

The geometry diagnostic found 703 ground-truth lines with at least 50% coverage from one prediction, 705 with at least 50% union coverage, 446 with both polygons above 50% overlap, and 436 strict IoU matches. The difference shows merged/over-expanded predictions and genuine misses; it is diagnostic and does not replace the official IoU metric.

## Reproduction

```powershell
$runner = '.\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1'
$run = 'artifacts/public-ocr-evaluation/hiertext-v6-small-sliding-20260929-rerun'
& $runner `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-003.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v6 -Variant small -Backend onnxruntime `
  -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
  -MaximumRegions 1024 -OverflowMode SlidingWindow `
  -WindowOverlap 0.2 -MaximumWindowsPerRegion 32 `
  -OutputDirectory $run -ContinueOnFailure

uv run python .\eng\models\paddle-ocr\scripts\Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest "$run/selected-manifest.jsonl" `
  --dataset-root 'F:\OCRBenchmarkTesting' `
  --run-directory $run `
  --ocrbench-root 'F:\OCRBenchmarkTesting' `
  --predictions "$run/predictions-corrected.json" `
  --report "$run/evaluation-corrected.json"
```

The raw run, predictions and geometry report are ignored local artifacts. The run recorded source revision `0586924f52dc9dc9f37f12161607a0c675cb57c5` with a dirty-worktree status digest; this result is therefore tied to that exact local run and is not a clean-release benchmark.
