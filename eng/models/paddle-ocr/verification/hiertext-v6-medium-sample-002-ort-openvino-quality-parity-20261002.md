# PP-OCR v6 Medium HierText sample-002 ORT/OpenVINO parity (2026-10-02)

This record adds the ten-page `sample-002` HierText selection to the PP-OCRv6 Medium quality evidence. The source images, annotations, raw predictions and per-page output remain outside the repository at `F:\OCRBenchmarkTesting`; the selection is smoke-only until source-specific landing pages and redistribution terms are reviewed.

## Protocol and provenance

- Pipeline: PP-OCRv6 Medium `det -> crop -> rec -> merge` with SlidingWindow overlap `0.2`, maximum `32` windows per region, maximum `1,024` regions, recognition maximum width `320`.
- One warm-up and one measured iteration per page; recognition batch `16`; one inference channel. Timings exclude model load and are observations, not the formal 5-warm-up/50-iteration performance protocol.
- Host: Windows 10 `10.0.26200`, x64, 16 logical processors (`JYPPX`), CPU providers only.
- Detector SHA-256: `eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1`.
- Recognizer SHA-256: `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba`.
- Source manifest SHA-256: `e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7`.
- Selected manifest SHA-256: `5f334b20afbdc9303c3bb9426867fa3087c19bb45f78c889d612b4d2ae10512c`.
- Source revision: `a8e81930ee51ccc995a6541e3106a0ad51607926`; benchmark assembly SHA-256: `dcd8bd822201b454713ec7534e717c7275378596cbf2cf09e3e64d001eb2dcf3`.

## Quality and latency

| Metric | ONNX Runtime CPU | OpenVINO CPU |
| --- | ---: | ---: |
| Completed pages / failures / empty outputs | 10 / 0 / 0 | 10 / 0 / 0 |
| Detection TP / FP / FN at IoU 0.5 | 187 / 244 / 350 | 187 / 244 / 350 |
| Detection precision / recall / F1 | 43.39% / 34.82% / 38.64% | 43.39% / 34.82% / 38.64% |
| Matched-region CER / WER | 18.08% / 49.79% | 18.08% / 49.79% |
| End-to-end CER / WER | 108.92% / 125.87% | 108.92% / 125.87% |
| Total latency P50 / P95 | 1,258.12 / 2,948.36 ms | 835.75 / 1,900.75 ms |
| Detection latency P50 / P95 | 564.55 / 647.61 ms | 375.49 / 423.37 ms |
| Recognition latency P50 / P95 | 646.48 / 2,457.81 ms | 425.48 / 1,574.99 ms |

The quality counters and matched text metrics are identical across the two backends. The ten ordered page text sequences and region counts match `10/10`; polygon coordinates have maximum absolute difference `0`, and confidence has maximum absolute difference `2.185e-5`. This establishes parity for this exact host, model, manifest and smoke protocol. It does not establish GPU parity, general-device performance or release accuracy.

The selection has 537 labeled regions and 2,836 reference characters, but no natural line at or above 3,200 characters. The A2 natural-long-text gate therefore remains open. Geometry coverage is reported in the machine-readable summary as a diagnostic and must not be substituted for the IoU metric.

## Reproduction

Build the benchmark with the repository's locked dependencies, then run the same command twice with distinct output directories and change only `-Backend`:

```powershell
dotnet restore tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -p:DeploySharpPaddleOcrCuda12=true --locked-mode
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release --no-restore -p:DeploySharpPaddleOcrCuda12=true

$run = 'artifacts/public-ocr-evaluation/hiertext-validation-sample-002-v6-medium-ort-sliding-20261002-rerun'
& .\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1 `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-002.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' -ModelRoot 'E:\Model\paddleocr' `
  -Version v6 -Variant medium -Backend onnxruntime `
  -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
  -MaximumRegions 1024 -OverflowMode SlidingWindow -WindowOverlap 0.2 `
  -MaximumWindowsPerRegion 32 -OutputDirectory $run -ContinueOnFailure

uv run python .\eng\models\paddle-ocr\scripts\Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest "$run/selected-manifest.jsonl" --dataset-root 'F:\OCRBenchmarkTesting' `
  --run-directory $run --ocrbench-root 'F:\OCRBenchmarkTesting' `
  --predictions "$run/predictions-corrected.json" --report "$run/evaluation-corrected.json"

.\eng\models\paddle-ocr\scripts\Measure-HierTextGeometryCoverage.ps1 `
  -ManifestPath "$run/selected-manifest.jsonl" `
  -PredictionsPath "$run/predictions-corrected.json" `
  -OutputPath "$run/geometry-coverage.json"
```

Repeat with `-Backend openvino` and a new `$run`. The checked-in summary is [hiertext-v6-medium-sample-002-ort-openvino-quality-parity-20261002.json](hiertext-v6-medium-sample-002-ort-openvino-quality-parity-20261002.json).
