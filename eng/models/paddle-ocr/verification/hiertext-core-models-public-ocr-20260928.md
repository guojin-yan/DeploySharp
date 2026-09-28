# PP-OCR v4/v6 public-data quality cross-check (2026-09-28)

## Scope and status

This is a local, exploratory quality cross-check using the public-source HierText selections under `F:\OCRBenchmarkTesting`. It extends the v5 mobile record in [the preceding validation report](hiertext-v5-mobile-public-ocr-20260925.md) with a v4 mobile baseline and v6 tiny/medium runs. The images and annotations remain outside this repository and are not included in a model Release. The selection is still smoke-only until each image landing page and redistribution terms are reviewed; these numbers are not leaderboard results.

The evaluation uses the same selected manifests, source-image SHA checks, NFC/case-sensitive CER/WER definitions and IoU matching as the v5 report. `sample-002` has 10 pages and 537 labeled lines (no line at least 32 characters). `sample-003` has 24 pages and 1,020 labeled lines, including 42 lines at least 32 characters and 4 lines at least 128 characters.

## Fixed protocol

- Device: `JYPPX`, Windows 10.0.26200, x64, 16 logical processors.
- Backend: ONNX Runtime CPU; one warm-up and one measured iteration per page; batch 16; one inference channel; maximum 1,024 regions; detector maximum width 320.
- `sample-002` uses `Clamp` recognition overflow. `sample-003` uses `SlidingWindow`, overlap `0.2`, and at most 32 windows per region.
- Model assets stay in `E:\Model\paddleocr`. The run metadata records model SHA-256, selected-manifest SHA-256, benchmark assembly SHA-256 and the dirty-worktree status digest.
- The latency values below are one-iteration observations, not a formal P50/P95 performance claim. The public-data report is for quality and behavior diagnosis.

## Results

### 10-page regular-text selection

| Model/backend | TP | FP | FN | Precision | Recall | F1 | Matched CER | End-to-end CER | Total P50 (ms) | Total P95 (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| PP-OCRv4 Mobile / ORT CPU, Clamp | 177 | 169 | 360 | 51.16% | 32.96% | 40.09% | 47.41% | 94.11% | 799.53 | 1,988.62 |
| PP-OCRv6 Tiny / ORT CPU, Clamp | 115 | 191 | 422 | 37.58% | 21.42% | 27.28% | 21.11% | 110.54% | 209.49 | 317.16 |
| PP-OCRv6 Tiny / OpenVINO CPU, Clamp | 115 | 191 | 422 | 37.58% | 21.42% | 27.28% | 21.11% | 110.54% | 142.06 | 411.48 |
| PP-OCRv6 Tiny / OpenCV DNN CPU, Clamp | 115 | 191 | 422 | 37.58% | 21.42% | 27.28% | 21.11% | 110.54% | 649.21 | 956.52 |
| PP-OCRv6 Medium / ORT CPU, Clamp | 187 | 244 | 350 | 43.39% | 34.82% | 38.64% | 18.42% | 105.36% | 1,671.31 | 4,273.47 |

The v6 medium recognizer has the lowest matched CER in this small selection, while v6 tiny is substantially faster. The detector and end-to-end scores vary by model and scene; these results do not justify selecting a default model for every domain.

The v6 Tiny OpenVINO and OpenCV runs completed all 10 pages without errors or timeouts. Their per-page `result_text_sha256` values matched the ORT run on all 10 pages. This is a CPU text-output parity result for this exact model, manifest and runtime set; it is not a claim of numerical tensor parity or GPU parity. The latency values above are one-iteration observations and remain separate from the formal 5/50 performance protocol.

### 24-page long-text selection

| Model/backend | Overflow | TP | FP | FN | Precision | Recall | F1 | Matched CER | End-to-end CER | ≥32-character edits | ≥128-character edits |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| PP-OCRv6 Tiny / ORT CPU | SlidingWindow | 400 | 350 | 620 | 53.33% | 39.22% | 45.20% | 16.43% | 70.20% | 516/2,378 | 80/520 |

The run completed all 24 pages without an empty output, page error or timeout. SlidingWindow prevents the fixed-width recognizer from being silently clamped on the long lines, but it does not recover detector misses. The four lines at least 128 characters are not a sufficient sample for a general long-text accuracy claim, and this public selection contains no line over 3,200 characters.

## Geometry coverage diagnostic

The evaluator's IoU match is intentionally strict, but a low IoU can also result from a detector box that covers most of a line while being over-expanded, or from multiple predictions splitting one line. `Measure-HierTextGeometryCoverage.ps1` reports these cases without replacing the official IoU metric. It clips convex quadrilaterals and reports both single-prediction coverage and the union of all overlapping predictions.

| Run | GT lines | One prediction covers ≥50% of GT | Union coverage ≥50% | Both polygons ≥50% | IoU ≥50% | Union ≥50% but no single IoU match | Predictions covering ≥2 GT lines | Max GT lines per prediction |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| v4 mobile, sample-002, Clamp | 537 | 289 | 289 | 182 | 177 | 112 | 20 | 3 |
| v6 tiny, sample-003, SlidingWindow | 1,020 | 647 | 653 | 409 | 401 | 252 | 36 | 3 |
| v6 medium, sample-002, Clamp | 537 | 385 | 388 | 190 | 187 | 201 | 21 | 3 |

The gap between coverage and IoU confirms that the low recall is not explained by one geometric issue alone. There are merged predictions, over-expanded predictions and genuine uncovered text. Threshold tuning or post-processing changes must therefore be evaluated against labeled data rather than inferred from the coverage count.

## Reproduction

Run the public-data runner from the repository root. The generated output is ignored under `artifacts/public-ocr-evaluation/` and must not be committed or uploaded.

```powershell
$runner = '.\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1'
$manifest = 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-002.jsonl'

& $runner `
  -ManifestPath $manifest `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v6 -Variant medium -Backend onnxruntime `
  -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
  -MaximumRegions 1024 -OverflowMode Clamp `
  -OutputDirectory 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run' `
  -ContinueOnFailure

uv run python .\eng\models\paddle-ocr\scripts\Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run/selected-manifest.jsonl' `
  --dataset-root 'F:\OCRBenchmarkTesting' `
  --run-directory 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run' `
  --ocrbench-root 'F:\OCRBenchmarkTesting' `
  --predictions 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run/predictions-corrected.json' `
  --report 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run/evaluation-corrected.json'

& .\eng\models\paddle-ocr\scripts\Measure-HierTextGeometryCoverage.ps1 `
  -ManifestPath 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run/selected-manifest.jsonl' `
  -PredictionsPath 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run/predictions-corrected.json' `
  -OutputPath 'artifacts/public-ocr-evaluation/my-v6-medium-hiertext-run/geometry-coverage.json'
```

The exact runs documented here used source revision `bc0050f7de240918fc2bb10be80c3a5de45566a7`, status digest `bc494a5a61ff352e3028d0bfdc4dd24fabb94cbcf8fcf7c9047b579eb832aae7` and benchmark assembly SHA-256 `61dc419e21c4202ac636c1330de0b07b1cae2043ca9aad85082c9c86f4bf1893`. The selected-manifest hashes are recorded in each generated evaluation report.

## Remaining boundary

This cross-check does not complete the PaddleOCR plan's long-text, rotated-text, degraded-image or formal multi-backend accuracy gates. It also does not replace the pending 5/50 performance protocol. The official IoU/CER/WER reports remain the release-facing evidence; geometry coverage is a diagnostic aid for deciding whether a future post-processing change is worth implementing.
