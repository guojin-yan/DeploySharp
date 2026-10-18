# Static PP-Structure layout — ONNX Runtime CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` results for eight exact static-layout ONNX artifacts. The run uses the same `bus.jpg`, model profiles, prepared input contract and decoder policy as the corresponding OpenVINO static-layout benchmark. Each backend gets one Session; image decoding/preprocessing, model/Session creation and the one-time OpenVINO reference are outside the timed interval.

## Device and protocol

- Host: `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64.
- CPU: AMD Ryzen 7 5800H with Radeon Graphics, 8 cores / 16 logical processors.
- Backend: ONNX Runtime CPU, `Microsoft.ML.OnnxRuntime 1.28.0`.
- Power/clock: Windows was on the Balanced power plan during this test session. CPU frequency was not locked. The observed WMI current/max frequency was 3.2/3.2 GHz, which is not proof of a fixed clock; thermal telemetry was unavailable through the queried Windows counter.
- Input: `E:\Data\image\bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`.
- Timing: synchronous `VisualPipeline.Run`, including ORT CPU execution and Paddle NMS decoding; one prepared input reused, batch 1, one Session, nearest-rank percentiles.
- Comparison: one OpenVINO CPU execution on the same ONNX artifact and input. High-confidence detections (`score ≥ 0.05`) are greedily matched by class, score tolerance `0.05`, and IoU `≥ 0.80`; at least 95% of the smaller set must match.

## Results

| Model | Input | Candidates | High-confidence parity (ORT/OpenVINO) | P50 ms | P95 ms | Raw samples |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| `pp-doclayout-m` | `[1,3,640,640]` | 100 | 28/28 | 159.81 | 189.70 | [JSON](paddle-document-ort-pp-doclayout-m-benchmark-20261018.json) |
| `pp-doclayout-s` | `[1,3,480,480]` | 100 | 48/48 | 22.75 | 26.66 | [JSON](paddle-document-ort-pp-doclayout-s-benchmark-20261018.json) |
| `picodet-layout-1x` | `[1,3,800,608]` | 18 | 5/5 | 72.59 | 96.87 | [JSON](paddle-document-ort-picodet-layout-1x-benchmark-20261018.json) |
| `picodet-layout-1x-table` | `[1,3,800,608]` | 3 | 0/0 | 73.93 | 84.24 | [JSON](paddle-document-ort-picodet-layout-1x-table-benchmark-20261018.json) |
| `picodet-s-layout-3cls` | `[1,3,480,480]` | 66 | 25/25 | 20.57 | 22.44 | [JSON](paddle-document-ort-picodet-s-layout-3cls-benchmark-20261018.json) |
| `picodet-l-layout-3cls` | `[1,3,640,640]` | 76 | 22/22 | 144.83 | 220.83 | [JSON](paddle-document-ort-picodet-l-layout-3cls-benchmark-20261018.json) |
| `picodet-s-layout-17cls` | `[1,3,480,480]` | 77 | 4/4 | 20.56 | 22.58 | [JSON](paddle-document-ort-picodet-s-layout-17cls-benchmark-20261018.json) |
| `picodet-l-layout-17cls` | `[1,3,640,640]` | 63 | 11/11 | 155.52 | 173.58 | [JSON](paddle-document-ort-picodet-l-layout-17cls-benchmark-20261018.json) |

All eight runs passed the execution and parity contracts. The table-only PicoDet sample produced no detections above the declared confidence threshold on either backend; its `0/0` is not an accuracy result. A few P95 values include scheduling outliers, so the exact 50 samples in JSON should be retained when comparing devices; do not interpret these single-host measurements as locked-clock or dataset-level performance.

## Reproduction

Place the eight ONNX files under `E:\Model\PaddleDocument\onnx` and use `E:\Data\image\bus.jpg`. From the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK = '1'
$env:DEPLOYSHARP_SOURCE_REVISION = (git rev-parse HEAD).Trim()
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentOrtStaticLayoutBenchmarkTests
```

The eight machine-readable per-model records alongside this report contain runtime/device metadata, source revision, exact model/input hashes, all 50 measured samples, percentiles and parity counts. They do not establish layout accuracy, cross-device ranking, locked-clock performance, or support for other graph exports.
