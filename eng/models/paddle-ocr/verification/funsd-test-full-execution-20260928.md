# FUNSD full test execution compatibility (2026-09-28)

## Status and interpretation

This is an execution-compatibility check of PP-OCRv6 Tiny on the locally held FUNSD test selection. All 40 manifest images completed through the ONNX Runtime CPU pipeline with no process failures, empty outputs, or missing predictions. It is not an OCR accuracy claim or a model ranking.

FUNSD provides word-level, axis-aligned boxes, while this pipeline detects text lines. A line prediction generally cannot achieve strict IoU against each word box, and the manifest does not provide text-line groupings. Therefore the detection and end-to-end CER/WER values below are diagnostic artifacts of this annotation mismatch. Do not compare them to line-level datasets such as HierText, and do not tune release defaults from these values.

The local FUNSD source audit marks this data non-commercial and rights-review-required. No images, annotations, predictions, or per-image outputs are checked into this repository or included in a Release. See the local audit at `F:\OCRBenchmarkTesting\docs\datasets\FUNSD.md` and the [official FUNSD terms](https://guillaumejaume.github.io/FUNSD/work/).

## Protocol

- Device: `JYPPX`, Windows 10.0.26200, x64, 16 logical processors.
- Dataset: local `funsd-test-full-001.jsonl`; 40 images retained by the manifest converter, 6,938 word-level instances.
- Model: PP-OCRv6 Tiny, ORT CPU; detector SHA-256 `193bab7a04fca699a6c82e6abb5b81bdb28177f0abd4062552b04908dafb19f8`; recognizer SHA-256 `9ef676d6ed3c88256a2d92c640c44f25b0c40947e111b14b8be8f594091563e6`.
- Settings: one warm-up and one measured iteration per page; batch 16; one inference channel; maximum 1,024 regions; `Clamp` recognition overflow.
- Source revision: `ebadfd7d358422225bad9bcee9921a32f42fc8fe`. The worktree was dirty; status digest `28cd9b74edf5eeec829e31416b00302f7a94ce45af2e18f6188127061d6573eb`.
- Benchmark assembly SHA-256: `61dc419e21c4202ac636c1330de0b07b1cae2043ca9aad85082c9c86f4bf1893`.
- Manifest SHA-256: `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3`.

## Results

| Execution status | Images | Empty outputs | Errors | Total latency P50 (ms) | Total latency P95 (ms) |
|---|---:|---:|---:|---:|---:|
| ORT CPU | 40/40 | 0 | 0 | 314.50 | 587.58 |

The latency percentiles describe one measured pass across these 40 pages only. They are not a formal performance result; use the dedicated 5/50 performance protocol for that.

The evaluator also reports word-box IoU 0.5: TP 782, FP 1,298, FN 6,156 (precision 37.60%, recall 11.27%, F1 17.34%), matched-region CER 23.88%, and end-to-end CER 147.66%. These numbers are retained for traceability only and are invalid as line-detection quality estimates because the labels and predictions represent different geometric units.

## Reproduction

Run from the repository root. The selected manifest, raw output, predictions, and evaluation JSON are written under the ignored `artifacts/public-ocr-evaluation/` directory.

```powershell
& .\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1 `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\funsd-test-full-001.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v6 -Variant tiny -Backend onnxruntime `
  -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
  -MaximumRegions 1024 -OverflowMode Clamp `
  -OutputDirectory 'artifacts/public-ocr-evaluation/funsd-test-full-v6-tiny-ort-clamp-20260928' `
  -PipelineTimeoutMs 60000 -ContinueOnFailure

uv run python .\eng\models\paddle-ocr\scripts\Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest 'artifacts/public-ocr-evaluation/funsd-test-full-v6-tiny-ort-clamp-20260928/selected-manifest.jsonl' `
  --dataset-root 'F:\OCRBenchmarkTesting' `
  --run-directory 'artifacts/public-ocr-evaluation/funsd-test-full-v6-tiny-ort-clamp-20260928' `
  --ocrbench-root 'F:\OCRBenchmarkTesting' `
  --predictions 'artifacts/public-ocr-evaluation/funsd-test-full-v6-tiny-ort-clamp-20260928/predictions-corrected.json' `
  --report 'artifacts/public-ocr-evaluation/funsd-test-full-v6-tiny-ort-clamp-20260928/evaluation-corrected.json'
```
