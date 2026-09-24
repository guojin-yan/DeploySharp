# PP-OCRv5 Mobile public-data quality check (2026-09-25)

## Scope and status

This is a local, exploratory quality evaluation using the public-source HierText validation selection stored outside the DeploySharp repository at `F:\OCRBenchmarkTesting`. It is not a leaderboard result and is not permission to redistribute the images or annotations. Both manifests mark the selection `smoke_only_until_each_image_landing_page_and_redistribution_reviewed`; image rows carry per-image Open Images attribution/license metadata, while the HierText line annotations are identified as CC BY-SA 4.0. No images, ground-truth text, predictions, or per-image reports from this dataset are committed or included in a model Release.

The official dataset card revision recorded by the manifests is [google-research-datasets/HierText at 70b6620](https://github.com/google-research-datasets/hiertext/tree/70b6620b2b112597d8219e11eee9773a1403827c). Before reusing or redistributing an individual image, review its landing page and terms from that image's manifest record.

The local selection contains two disjoint subsets:

| Manifest | Pages | Valid labeled text lines | Lines ≥32 chars | Lines ≥128 chars | Lines ≥3200 chars | Longest line |
|---|---:|---:|---:|---:|---:|---:|
| `hiertext-validation-sample-002.jsonl` | 10 | 537 | 0 | 0 | 0 | 27 |
| `hiertext-validation-sample-003.jsonl` | 24 | 1,020 | 42 | 4 | 0 | 132 |
| Combined local selection | 34 | 1,557 | 42 | 4 | 0 | 132 |

This gives useful line-level truth for regular and moderately long scene text, but it does **not** satisfy the plan's `>3200`-character long-text requirement.

## Fixed test setup

- Model: PP-OCRv5 Mobile detector, classifier and recognizer. SHA-256: detector `1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039`; classifier `dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2`; recognizer `f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9`.
- Device: `JYPPX`, Windows 10.0.26200, x64, 16 logical processors. All three backends used CPU; no GPU timing claim is made.
- Protocol: 1 warm-up, 1 measured iteration per page, batch 16, one inference channel, at most 1,024 regions per page, 60-second page timeout. SlidingWindow uses overlap `0.2` and at most 32 windows per region. No page failed or timed out.
- Source commit reported by the runner: `b0793b02bb65c276fa59c421721f25e714d7cf0b`. The working tree was dirty; the runner recorded status digest `d8b5931bd24e950c2e8d69a24abc561eada61ebba81df953a81bd14c50c1f563` and benchmark assembly SHA-256 `61dc419e21c4202ac636c1330de0b07b1cae2043ca9aad85082c9c86f4bf1893`. Treat this as a pinned local experiment, not a clean-release build result.
- Manifest SHA-256: sample-002 source `e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7`, selected `5f334b20afbdc9303c3bb9426867fa3087c19bb45f78c889d612b4d2ae10512c`; sample-003 source `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`, selected `2c3933fbd5f29c2d4054357518feae96eb1bedd4b009333e105bfeb267159316`.

The evaluator checks every source-image SHA against the manifest and every selected-manifest SHA against its run metadata. CER uses case-sensitive NFC text; WER splits on whitespace and preserves punctuation. “Matched” recognition scores only regions paired at polygon IoU ≥0.5. End-to-end CER/WER include missed ground-truth regions as deletions and unmatched detections as insertions, so rates above 100% are possible. Long-text character buckets use the complete end-to-end error count, including unmatched regions.

## Results

### 24-page long-text subset: clamp versus sliding windows

All detection counts were unchanged by the recognition overflow policy: 424 true positives, 337 false positives and 596 false negatives (`P=55.72%`, `R=41.57%`, `F1=47.61%` at IoU 0.5). The Clamp run marked 134 of 761 detected regions as width-clamped. SlidingWindow produced 1,000 recognition windows for those 761 regions and avoided those width clamps.

| Policy/backend | Matched CER | Matched WER | End-to-end CER | End-to-end WER |
|---|---:|---:|---:|---:|
| Clamp, ONNX Runtime CPU | 52.05% | 73.17% | 90.04% | 107.10% |
| SlidingWindow, ONNX Runtime CPU | 27.80% | 60.30% | 79.45% | 102.02% |
| SlidingWindow, OpenVINO CPU | 27.80% | 60.30% | 79.45% | 102.02% |
| SlidingWindow, OpenCV DNN CPU | 27.80% | 60.30% | 79.45% | 102.02% |

For the 42 reference lines with at least 32 characters (2,378 chars total), end-to-end errors fell from `2,236/2,378` (CER 94.03%) to `669/2,378` (CER 28.13%); 36 of 42 had a geometric match. All four ≥128-character lines were geometrically matched in both modes, but Clamp returned blank text for those lines (`520/520` character errors); SlidingWindow reduced that bucket to `105/520` errors (CER 20.19%).

This isolates a real width-overflow problem and shows a substantial local improvement on the selected long lines. It does not prove general OCR accuracy: detector recall remains low, all four long lines come from this small selection, and no >3200-character line exists here.

### 10-page regular-text subset

The same ORT-only A/B had 377 detections and 6 clamped regions under Clamp; SlidingWindow generated 388 total windows and no width clamps. Matched CER changed from 35.73% to 34.58%, but end-to-end CER worsened from 123.41% to 130.36% (WER 136.59% → 139.27%). This is an important counterexample: sliding-window handling is not a universal quality improvement and should remain a bounded overflow policy rather than an unconditional accuracy claim.

### Combined 34-page ORT A/B

Weighted from summed edit distances and reference counts (not a macro-average), the combined detector result was unchanged: 541 TP, 597 FP, 1,016 FN (`P=47.54%`, `R=34.75%`, `F1=40.15%`). Matched-region CER/WER changed from `50.30% / 72.02%` to `28.53% / 60.64%`; end-to-end CER/WER changed from `97.94% / 114.69%` to `91.50% / 111.60%`. The aggregate gain is driven by the long-text subset and must be interpreted alongside the regular-subset regression above.

### Cross-backend consistency

For SlidingWindow, the 24 sample-003 pages and 10 sample-002 pages all passed on ONNX Runtime, OpenVINO and OpenCV DNN CPU. Per-image `result_text_sha256` had zero mismatches across those three backends for each subset (34/34 pages). This is text-output parity for these exact assets and CPU runtimes; it does not establish GPU parity, coordinate equivalence, or accuracy on other devices/models.

## Reproduction

Use the local manifests and images under `F:\OCRBenchmarkTesting`, and point `-ModelRoot` at the local PP-OCRv5 Mobile assets. The runner refuses to overwrite a previous output directory. Run each backend separately; use `-OverflowMode Clamp` for the controlled baseline and `SlidingWindow` for the comparison.

```powershell
$runner = '.\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1'
$manifest = 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-003.jsonl'
$run = 'artifacts/public-ocr-evaluation/my-hiertext-v5-mobile-sliding-run'

& $runner `
  -ManifestPath $manifest `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v5 -Variant mobile -Backend onnxruntime `
  -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
  -MaximumRegions 1024 -OverflowMode SlidingWindow `
  -WindowOverlap 0.2 -MaximumWindowsPerRegion 32 `
  -PipelineTimeoutMs 60000 -ContinueOnFailure `
  -OutputDirectory $run

python .\eng\models\paddle-ocr\scripts\Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest "$run\selected-manifest.jsonl" `
  --dataset-root 'F:\OCRBenchmarkTesting' `
  --run-directory $run `
  --ocrbench-root 'F:\OCRBenchmarkTesting' `
  --predictions "$run\predictions.json" `
  --report "$run\evaluation.json"
```

Repeat with `-Backend openvino` and `-Backend opencv-dnn` for the other CPU backends. To run the controlled Clamp baseline, change the output directory and set `-OverflowMode Clamp`. Use more than one measured iteration and a separately controlled device protocol before publishing latency percentiles.

Run outputs are ignored under `artifacts/public-ocr-evaluation/` and contain per-image recognition text locally. Do not add those generated files, dataset rows, images, or attribution-incomplete samples to Git or a Release.
