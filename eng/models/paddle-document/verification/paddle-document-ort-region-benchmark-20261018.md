# PP-Structure region and seal — ONNX Runtime CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` results for seven region/cell ONNX artifacts and two PP-OCRv4 seal artifacts. The runs use one prepared input and one ONNX Runtime CPU Session per model. Image decoding/preprocessing, model/Session creation and the one-time OpenVINO reference are outside the timed interval.

## Device and protocol

- Host: `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64; AMD Ryzen 7 5800H, 8 cores / 16 logical processors.
- Backend: ONNX Runtime CPU, `Microsoft.ML.OnnxRuntime 1.28.0`.
- Inputs: `E:\Data\image\bus.jpg` for layout, `E:\Model\PaddleDocument\validation\table_recognition.jpg` for cell detection, and `E:\Data\ocr\demo_1.jpg` for seal detection.
- Power/clock: Windows Balanced power plan; CPU frequency was not locked. These values are single-host observations, not locked-clock limits.
- Region comparison: high-confidence detections (`score ≥ 0.5`) are matched against OpenVINO CPU by class, score tolerance `0.01`, coordinate tolerance `1.5 px` and IoU `≥ 0.80`. Seal comparison is limited to mask shape and decoder region count; raw probability-map numerical parity is not claimed.

## Results

| Model | Input | Contract comparison | P50 ms | P95 ms | JSON |
| --- | --- | --- | ---: | ---: | --- |
| `pp-doclayout-l` | `bus.jpg`, `[1,3,640,640]` | 1/1 high-confidence detection | 476.69 | 555.68 | [JSON](paddle-document-ort-pp-doclayout-l-benchmark-20261018.json) |
| `pp-docblocklayout` | `bus.jpg`, `[1,3,640,640]` | 1/1 high-confidence detection | 471.97 | 525.26 | [JSON](paddle-document-ort-pp-docblocklayout-benchmark-20261018.json) |
| `pp-doclayout-plus-l` | `bus.jpg`, `[1,3,800,800]` | 1/1 high-confidence detection | 727.38 | 1115.24 | [JSON](paddle-document-ort-pp-doclayout-plus-l-benchmark-20261018.json) |
| `rt-detr-h-layout-3cls` | `bus.jpg`, `[1,3,640,640]` | 1/1 high-confidence detection | 1612.83 | 1856.54 | [JSON](paddle-document-ort-rt-detr-h-layout-3cls-benchmark-20261018.json) |
| `rt-detr-h-layout-17cls` | `bus.jpg`, `[1,3,640,640]` | 1/1 high-confidence detection | 1612.95 | 2255.31 | [JSON](paddle-document-ort-rt-detr-h-layout-17cls-benchmark-20261018.json) |
| `rt-detr-l-wired-cell-det` | `table_recognition.jpg`, `[1,3,640,640]` | 13/13 high-confidence detections | 451.93 | 481.81 | [JSON](paddle-document-ort-rt-detr-l-wired-cell-det-benchmark-20261018.json) |
| `rt-detr-l-wireless-cell-det` | `table_recognition.jpg`, `[1,3,640,640]` | 13/13 high-confidence detections | 481.58 | 565.51 | [JSON](paddle-document-ort-rt-detr-l-wireless-cell-det-benchmark-20261018.json) |
| `ppocrv4-mobile-seal-det` | `demo_1.jpg`, `[1,3,224,224]` | `224×224` mask; regions 0/0 | 9.43 | 10.98 | [JSON](paddle-document-ort-ppocrv4-mobile-seal-det-benchmark-20261018.json) |
| `ppocrv4-server-seal-det` | `demo_1.jpg`, `[1,3,224,224]` | `224×224` mask; regions 1/1 | 124.62 | 134.89 | [JSON](paddle-document-ort-ppocrv4-server-seal-det-benchmark-20261018.json) |

All nine tests passed execution and their declared result contracts. `demo_1.jpg` has no seal ground-truth annotation; seal region counts are a decoder/execution check, not recall or accuracy. The exact 50 samples, model/input SHA-256 values and device metadata are in the linked JSON records. These measurements do not establish dataset accuracy, locked-clock performance, cross-device ranking or support for neighboring exports.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK = '1'
$env:DEPLOYSHARP_SOURCE_REVISION = (git rev-parse HEAD).Trim()
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentOrtRegionBenchmarkTests
```
