# PP-Structure dynamic Batch execution evidence

This report records a real model Batch run on the Windows development host. It is execution and decoder-order evidence, not an accuracy score or a throughput benchmark.

## Protocol

| Field | Value |
| --- | --- |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Batch | `2` identical source rows |
| Models | `paddle-doc/pp-lcnet-x1-0-doc-ori`, `paddle-table/pp-lcnet-x1-0-table-cls` |
| Backends | ONNX Runtime CPU, OpenVINO CPU |
| Model input | `[2,3,224,224]` |
| Result contract | Two ordered `ClassificationResult` rows; identical source rows must keep identical label and score |

## Results

| Backend | Model | Rows | Labels | Scores | Status |
| --- | --- | ---: | --- | --- | --- |
| ONNX Runtime CPU | `pp-lcnet-x1-0-doc-ori` | 2 | `0_degree`, `0_degree` | `0.9240474`, `0.9240474` | pass |
| ONNX Runtime CPU | `pp-lcnet-x1-0-table-cls` | 2 | `wired`, `wired` | `0.7577595`, `0.7577595` | pass |
| OpenVINO CPU | `pp-lcnet-x1-0-doc-ori` | 2 | `0_degree`, `0_degree` | `0.9240475`, `0.9240475` | pass |
| OpenVINO CPU | `pp-lcnet-x1-0-table-cls` | 2 | `wired`, `wired` | `0.75775963`, `0.75775963` | pass |

The machine-readable result is [`paddle-document-dynamic-batch-ort-openvino-20261005.json`](paddle-document-dynamic-batch-ort-openvino-20261005.json). The test is opt-in:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_REPORT_PATH = `
  (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-dynamic-batch-ort-openvino-20261005.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --filter FullyQualifiedName~PaddleDocumentDynamicBatchIntegrationTests
```

## Boundary

This proves true `[2,3,224,224]` binding, two-row decoding and identical-row stability for these two exact ONNX files on this host. It does not prove dynamic Batch for the static PicoDet or PP-DocLayout-M/S exports, the Chart2Table autoregressive bundle, every backend, or a general performance improvement. Those assets keep the Session-pool/page-concurrency path until an exact graph and backend run is available.
