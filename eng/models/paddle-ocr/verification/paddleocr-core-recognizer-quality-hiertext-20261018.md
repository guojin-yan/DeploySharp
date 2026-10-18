# PP-OCR v4/v5/v6 recognizer-only quality evidence — HierText (2026-10-18)

## Scope

This record scores the seven core PP-OCR recognizers shipped in the DeploySharp catalog on the same 256 source-linked HierText validation line crops. Detection is deliberately bypassed: each crop is supplied as a known full-crop quadrilateral to `OcrPipeline.RecognizeOnlyAsync`. The result therefore measures recognizer behavior only; it is not a page OCR, detector recall, reading-order, robustness, or release-accuracy result.

The selection is evenly spaced across the eligible, non-empty and non-ignored crops from the local `sample-002` and `sample-003` manifests. The pinned crop revision is `70b6620b2b112597d8219e11eee9773a1403827c`. The longest reference in this selection is 132 characters. The dataset and source-linked crop files remain outside the repository under `F:\OCRBenchmarkTesting`.

## Environment and protocol

- Windows/.NET test project: `tests/DeploySharp.Visual.OpenCV.Tests`, `net10.0`, Release configuration.
- Backends: ONNX Runtime CPU (`onnxruntime`) and OpenVINO CPU (`openvino`).
- Models: PP-OCRv4 mobile/server, PP-OCRv5 mobile/server, PP-OCRv6 tiny/small/medium.
- One recognition session, batch size 1, one crop per call; 256 crops per model/backend.
- Metrics: exact row rate, character error rate (CER), word error rate (WER), empty predictions.
- Every model, recognizer dictionary and row contract is hash-bound in the machine-readable report.

## Results

| Backend | Model | Rows | Exact | Exact rate | CER | WER | Empty |
|---|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | v4-mobile | 256 | 84 | 32.81% | 33.7041% | 70.4156% | 36 |
| ONNX Runtime CPU | v4-server | 256 | 104 | 40.6250% | 31.9149% | 62.8362% | 19 |
| ONNX Runtime CPU | v5-mobile | 256 | 83 | 32.4219% | 36.6538% | 62.5917% | 32 |
| ONNX Runtime CPU | v5-server | 256 | 84 | 32.8125% | 37.5242% | 75.3056% | 53 |
| ONNX Runtime CPU | v6-tiny | 256 | 89 | 34.7656% | 35.0097% | 59.6577% | 18 |
| ONNX Runtime CPU | v6-small | 256 | 117 | 45.7031% | 25.0000% | 48.1663% | 7 |
| ONNX Runtime CPU | v6-medium | 256 | 121 | 47.2656% | 23.0658% | 45.2323% | 5 |
| OpenVINO CPU | v4-mobile | 256 | 84 | 32.8125% | 33.7041% | 70.4156% | 36 |
| OpenVINO CPU | v4-server | 256 | 104 | 40.6250% | 31.9149% | 62.8362% | 19 |
| OpenVINO CPU | v5-mobile | 256 | 83 | 32.4219% | 36.6538% | 62.5917% | 32 |
| OpenVINO CPU | v5-server | 256 | 84 | 32.8125% | 37.5242% | 75.3056% | 53 |
| OpenVINO CPU | v6-tiny | 256 | 89 | 34.7656% | 35.0097% | 59.6577% | 18 |
| OpenVINO CPU | v6-small | 256 | 117 | 45.7031% | 25.0000% | 48.1663% | 7 |
| OpenVINO CPU | v6-medium | 256 | 121 | 47.2656% | 23.0658% | 45.2323% | 5 |

The two CPU backends produced the same aggregate metrics and row-level text hashes for this bounded recognizer-only selection. This is an execution/parity observation for these exact artifacts and inputs, not a claim that all PP-OCR graphs or all backends are bitwise equivalent.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_MAX = '256'
$env:DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_BACKENDS = 'onnxruntime,openvino'
$env:DEPLOYSHARP_PADDLEOCR_CORE_LINE_REC_OUTPUT = (Resolve-Path 'eng/models/paddle-ocr/verification').Path

dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrCoreRecognizerQualityExternalIntegrationTests `
  --logger "console;verbosity=normal"
```

The checked-in JSON report is [`paddleocr-core-recognizer-quality-hiertext-20261018.json`](paddleocr-core-recognizer-quality-hiertext-20261018.json). Its SHA-256 is `6aeef2e2d3b77731d613a0cb3bf5b7acd77bbcc833aad7dda2b819a2b40a4103`.

## Boundaries and follow-up

This evidence does not close the formal PP-OCR quality gates. It does not measure detector recall, page reading order, natural continuous text of at least 3,200 characters, natural rotation/orientation, synthetic or natural degradation, enhancement routing, TensorRT/OpenCV DNN/GPU quality, or the formal 5+50 performance protocol. Dataset licensing and training-overlap review remain separate concerns. The next quality work should use output-granular line/page annotations and explicit acceptance thresholds before promoting any model to a release-quality accuracy statement.
