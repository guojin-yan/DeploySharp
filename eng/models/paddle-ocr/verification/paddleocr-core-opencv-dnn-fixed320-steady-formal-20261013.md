# PaddleOCR core OpenCV DNN fixed-width performance matrix (2026-10-13)

This report measures the explicit fixed-recognition-width optimization for the
seven maintained PP-OCR core combinations. It follows the
[dynamic-width baseline](paddleocr-core-opencv-dnn-steady-formal-20261013.md)
and does not replace its quality semantics.

## Protocol

- Source revision: `918d572`.
- Host: `JYPPX`, Windows `10.0.26200`, .NET `10.0.12`, x64, 16 logical CPUs.
- Backend: OpenCV DNN CPU through `JYPPX.OpenCV.CSharp.API 5.0.0.0`.
- Input: `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`.
- Full path: `DET -> crop -> optional CLS -> REC -> merge`.
- Five warm-up calls and 50 measured calls; prepared detector input reused.
- Static recognition batch `1`, two independent stage Sessions and 16
  recognition calls per page.
- Recognition crop width explicitly fixed at `320`. Crop content retains its
  aspect ratio and unused columns are padded; content is not stretched.
- Model loading and ONNX import are outside measured calls.

All seven rows passed the formal report validator, returned 16 regions, kept a
single strict result-contract variant through all 50 measurements and reported
zero within-row numeric drift.

## Results

| Model | Fixed P50/P95 (ms) | Dynamic P50/P95 (ms) | P50 reduction | Managed allocation fixed / dynamic | Allocation reduction |
|---|---:|---:|---:|---:|---:|
| v4 Mobile | `864.920 / 996.148` | `1285.438 / 1394.693` | `32.7%` | `17.6 MiB / 824.4 MiB` | `97.87%` |
| v4 Server | `2764.973 / 3561.289` | `4135.023 / 4656.340` | `33.1%` | `17.5 MiB / 7215.8 MiB` | `99.76%` |
| v5 Mobile | `934.582 / 1375.469` | `1379.577 / 1665.937` | `32.3%` | `46.3 MiB / 1494.5 MiB` | `96.91%` |
| v5 Server | `2338.607 / 2989.877` | `4582.283 / 5803.217` | `49.0%` | `46.5 MiB / 7895.7 MiB` | `99.41%` |
| v6 Tiny | `250.094 / 302.631` | `338.130 / 369.176` | `26.0%` | `18.1 MiB / 456.3 MiB` | `96.04%` |
| v6 Small | `540.833 / 642.687` | `992.798 / 1152.157` | `45.5%` | `46.9 MiB / 2025.8 MiB` | `97.69%` |
| v6 Medium | `1693.691 / 2011.435` | `2723.843 / 2859.905` | `37.8%` | `46.9 MiB / 6958.0 MiB` | `99.33%` |

The managed allocation value is the process-wide cumulative managed allocation
during one pipeline call. It is neither peak working set nor native OpenCV
allocation. The reduction is consistent with avoiding repeated graph
specialization/import when recognition crop widths change.

## Result contract and limits

A separate diagnostic run used the same fixed-width `320`, batch `1` and two
stage channels for ORT CPU and OpenCV DNN. All seven model pairs had identical
ordered-text and semantic-contract SHA-256 values; strict numeric hashes differed
because backend confidence values are floating-point results.

Fixed width is an explicit performance/quality trade-off, not the default.
v6 Tiny retained the dynamic-width text on this page, while v5 Server did not.
This report therefore does not prove CER/WER, recognition accuracy, detection
recall, a universal recommended width or equivalence to dynamic-width output.
Use dynamic width for the quality-first default. Select fixed width only after
validating representative data, especially long or densely padded text lines.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'opencv-dnn'
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = 'v4,v5,v6'
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = 'E:\Data\ocr\demo_1.jpg'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '1'
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = 'false'
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = 'true'
$env:DEPLOYSHARP_PADDLEOCR_FIXED_RECOGNITION_WIDTH = '320'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = '918d572'

dotnet run --project .\tools\DeploySharp.PaddleOcrBenchmark -c Release --no-build -- `
  'E:\Model\paddleocr' `
  '.\eng\models\paddle-ocr\verification\paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.csv'

pwsh -NoProfile -File .\eng\models\paddle-ocr\scripts\Test-PaddleOcrBenchmarkReport.ps1 `
  -ReportPath .\eng\models\paddle-ocr\verification\paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.csv `
  -RequireSourceRevision `
  -OutputPath .\eng\models\paddle-ocr\verification\paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.validation.json
```

Evidence files:

- [CSV matrix](paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.csv)
- [environment, protocol and model hashes](paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.csv.environment.json)
- [formal report validation](paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.validation.json)
