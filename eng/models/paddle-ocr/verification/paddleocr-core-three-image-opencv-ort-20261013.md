# Core PP-OCR OpenCV DNN / ORT matched-contract parity (2026-10-13)

## Result

The current-revision external integration test ran all seven registered core
PP-OCR combinations on `demo_1.jpg`, `demo_2.jpg` and `demo_3.jpg` through both
ONNX Runtime CPU and OpenCV DNN CPU. All `21/21` image/model pairs passed.

| Model | Images | Exact region text | Confidence gate | Polygon gate |
| --- | ---: | ---: | --- | --- |
| PP-OCRv4 Mobile | 3/3 | 3/3 | pass | pass |
| PP-OCRv4 Server | 3/3 | 3/3 | pass | pass |
| PP-OCRv5 Mobile | 3/3 | 3/3 | pass | pass |
| PP-OCRv5 Server | 3/3 | 3/3 | pass | pass |
| PP-OCRv6 Tiny | 3/3 | 3/3 | pass | pass |
| PP-OCRv6 Small | 3/3 | 3/3 | pass | pass |
| PP-OCRv6 Medium | 3/3 | 3/3 | pass | pass |

The comparison requires equal region counts and exact region text. For every
corresponding region, recognition confidence must differ by no more than
`0.01`, polygon cardinality must match, and every vertex coordinate must differ
by no more than `0.5 px`.

## Matched contract

- recognition batch: `1` on both backends
- pipeline concurrency: `1` on both backends
- paths: complete `det -> crop -> cls -> rec -> merge`
- images: `E:\Data\ocr\demo_1.jpg`, `demo_2.jpg`, `demo_3.jpg`; SHA-256 values are in the JSON
- models: v4 Mobile/Server, v5 Mobile/Server, v6 Tiny/Small/Medium; DET/CLS/REC SHA-256 values are stored per run
- host: `JYPPX`, Windows `10.0.26200`, x64, .NET `10.0.12`
- loaded runtimes: Microsoft.ML.OnnxRuntime `1.28.0.0`, JYPPX.OpenCV.CSharp.API `5.0.0.0`; paths and SHA-256 values are in the JSON
- source revision: `3c86aa5e31ed6d211b5b13f7ed0668daf7146b32+opencv-batch1-parity-schema2-dirty`

Machine-readable evidence: [schema 2 JSON](paddleocr-core-three-image-opencv-ort-20261013.json).

## Interpretation

This closes the apparent v5 backend mismatch from the OpenCV formal report:
that report compared OpenCV's required static Batch=1 output with a separate
ORT/OpenVINO Batch=8 formal matrix. At the same Batch=1 contract, all seven
models match across three images. The observed Batch=1 versus Batch=8 text/CTC
fingerprint difference remains a configuration behavior and must not be
misreported as an OpenCV importer error.

This test has no human text truth and does not calculate CER/WER, detection
recall or official model accuracy. Its per-run elapsed values include setup and
are compatibility diagnostics, not a 5+50 performance ranking. The formal
OpenCV performance and allocation baseline remains in the separate
[steady-state report](paddleocr-core-opencv-dnn-steady-formal-20261013.md).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLEOCR_ROOT = 'E:\Model\paddleocr'
$env:DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_IMAGE_ROOT = 'E:\Data\ocr'
$env:DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_EVIDENCE_PATH = `
  'artifacts/local-model-benchmarks/paddleocr-core-three-image-opencv-ort-current.json'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = '<commit-or-dirty-revision>'

dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --framework net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrCoreOpenCvThreeImageParityTests'
```
