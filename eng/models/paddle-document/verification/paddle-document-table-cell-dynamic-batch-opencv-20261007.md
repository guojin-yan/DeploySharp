# RT-DETR-L table-cell OpenCV DNN dynamic Batch boundary (2026-10-07)

## Scope

This opt-in run attempted true batch=2 execution and full Paddle-NMS decoding for the official wired and wireless RT-DETR-L table-cell ONNX exports on OpenCV DNN CPU. Each row was prepared from a distinct, non-overlapping top/bottom band of `E:\Model\PaddleDocument\validation\table_recognition.jpg`; both `[2,2]` geometry inputs were included. The band crops are execution probes, not independently labeled tables or cell ground truth.

## Environment and results

| Item | Value |
|---|---|
| Host | Windows build `26200`, x64 |
| Runtime | .NET `10.0.12`, OpenCV DNN `5.0.0` |
| Image SHA-256 | `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Wired ONNX SHA-256 | `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6` |
| Wireless ONNX SHA-256 | `e141c8aa947ef0aea165c45d51caf6d2cccbca5f381ed9c8854f402570b296bf` |

| Model | Session creation | Batch-2 forward | Exact result |
|---|---|---|---|
| `paddle-table/rt-detr-l-wired-cell-det` | Succeeded | Failed with `DS-OCV-8004` | `Requested blob not found` |
| `paddle-table/rt-detr-l-wireless-cell-det` | Succeeded | Failed with `DS-OCV-8004` | `Requested blob not found` |

The opt-in test passed `1/1` because both adapter failures were intentionally retained as machine-readable evidence. The [JSON report](paddle-document-table-cell-dynamic-batch-opencv-20261007.json) contains the full native diagnostics and artifact hashes. Separate reports already show that these exact model exports execute dynamic batch=2 with full decoding on ONNX Runtime CPU and OpenVINO CPU; this OpenCV attempt does not affect those results.

## Reproduction

With both ONNX files and the table image in the paths listed above:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TABLE_CELL_OPENCV_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --configuration Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchTableCellDetectorsRunOnOpenCvDnn `
  --verbosity minimal
```

## Boundary

Only these two exact artifacts, the OpenCV DNN 5.0.0 runtime, batch size 2 and one Windows host were tested. Mark this dynamic-batch combination unsupported; do not change existing single-image OpenCV evidence or ORT/OpenVINO batch evidence. In accordance with the project's scope, no importer/native root-cause investigation was performed. No cell accuracy, numerical parity, latency or throughput conclusion follows from this attempt.
