# PP-OCR v6 Medium HierText public quality smoke (2026-10-02)

This is a local exploratory quality run over the 24-page HierText `sample-003` selection stored outside the repository at `F:\OCRBenchmarkTesting`. Images, annotations, predictions and per-image output remain outside Git and model Releases. The source audit marks this selection `smoke_only_until_each_image_landing_page_and_redistribution_reviewed`; these values are not leaderboard or release-accuracy claims.

## Protocol and provenance

- Model: PP-OCRv6 Medium, ONNX Runtime CPU, full `det -> crop -> rec -> merge` pipeline.
- SlidingWindow: overlap `0.2`, maximum `32` windows per region, maximum `1,024` regions, recognition maximum width `320`.
- One warm-up and one measured iteration per page; recognition batch `16`; one inference channel. Timings exclude model load and are observations, not the formal 5-warm-up/50-iteration performance protocol.
- 24 pages completed; process failures `0`; empty outputs `0`; missing predictions `0`.
- Detector ONNX SHA-256: `eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1`.
- Recognizer ONNX SHA-256: `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba`.
- Source manifest: `F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-003.jsonl`.
- Source manifest SHA-256: `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- Selected manifest SHA-256: `2c3933fbd5f29c2d4054357518feae96eb1bedd4b009333e105bfeb267159316`.
- Source revision: `2d951991d21b537ec3186a8bd3408baf0ce34526`; the working tree was dirty during the run and the benchmark assembly SHA-256 was `dcd8bd822201b454713ec7534e717c7275378596cbf2cf09e3e64d001eb2dcf3`.
- Host: Windows 10 (`10.0.26200`), x64, 16 logical processors. No GPU provider was used.

## Quality results

| Metric | Result |
| --- | ---: |
| Detection TP / FP / FN at IoU 0.5 | 459 / 478 / 561 |
| Detection precision / recall / F1 | 48.99% / 45.00% / 46.91% |
| Detection TP / FP / FN at IoU 0.75 | 121 / 816 / 899 |
| Matched-region CER / WER | 12.12% / 32.58% |
| Matched-region exact text rate | 49.89% (229 / 459) |
| End-to-end CER / WER | 71.76% / 98.42% |
| End-to-end correct regions | 229 / 1,020 |
| Total latency P50 / P95 | 1,928.53 / 4,687.44 ms |
| Detection latency P50 / P95 | 608.65 / 905.09 ms |
| Recognition latency P50 / P95 | 1,325.49 / 4,013.38 ms |

The long-text buckets contain 42 ground-truth lines with at least 32 characters (2,378 reference characters, 473 end-to-end edits, 37 IoU matches) and 4 lines with at least 128 characters (520 reference characters, 79 end-to-end edits, 4 IoU matches). This selection contains no line at or above 3,200 characters, so the A2 natural-long-text gate remains open.

The geometry diagnostic found 802 ground-truth lines with at least 50% coverage from one prediction, 804 with at least 50% union coverage, 465 with at least 50% overlap on both polygons, and 459 strict IoU matches. There were 345 lines with combined coverage but no single IoU match, 81 strong-overlap rows, 56 predictions covering at least two ground-truth lines, and a maximum of three lines covered by one prediction. These are merge/coverage diagnostics and do not replace the IoU metric.

## Reproduction

Build the benchmark with the CUDA-12 dependency property so the benchmark and the referenced ONNX Runtime backend use the same managed ORT line, even for a CPU run:

```powershell
dotnet restore tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -p:DeploySharpPaddleOcrCuda12=true --locked-mode
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release --no-restore -p:DeploySharpPaddleOcrCuda12=true

$run = 'artifacts/public-ocr-evaluation/hiertext-validation-sample-003-v6-medium-ort-sliding-20261002-rerun'
& .\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1 `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-003.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v6 -Variant medium -Backend onnxruntime `
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

.\eng\models\paddle-ocr\scripts\Measure-HierTextGeometryCoverage.ps1 `
  -ManifestPath "$run/selected-manifest.jsonl" `
  -PredictionsPath "$run/predictions-corrected.json" `
  -OutputPath "$run/geometry-coverage.json"
```

The local raw run, generated predictions and geometry report are intentionally ignored. The checked-in JSON summary records the same hashes and aggregate metrics without redistributing the source data.
