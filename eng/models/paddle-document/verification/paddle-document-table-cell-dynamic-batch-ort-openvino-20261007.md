# RT-DETR-L table-cell dynamic Batch: ONNX Runtime / OpenVINO (2026-10-07)

## Scope

This run verifies true two-row binding and `PaddleDocumentNmsDecoder` row isolation for the official wired and wireless RT-DETR-L table-cell exports. It splits the same `table_recognition.jpg` into two non-overlapping horizontal bands (`551×66` each), so the prepared row tensors have distinct SHA-256 digests. The decoded result rows also have distinct canonical digests, guarding against accidentally reusing one row's output for the other. This is not a cell-detection accuracy evaluation, cross-backend numerical parity test, or throughput benchmark.

## Inputs and artifacts

| Item | Value |
|---|---|
| Wired model | `paddle-table/rt-detr-l-wired-cell-det` |
| Wired ONNX SHA-256 | `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6` |
| Wireless model | `paddle-table/rt-detr-l-wireless-cell-det` |
| Wireless ONNX SHA-256 | `e141c8aa947ef0aea165c45d51caf6d2cccbca5f381ed9c8854f402570b296bf` |
| Input image | `E:\Model\PaddleDocument\validation\table_recognition.jpg` |
| Input SHA-256 | `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Batch input | `[2,3,640,640]` |
| Source regions | Top `[x=0,y=0,w=551,h=66]`; bottom `[x=0,y=66,w=551,h=66]` |
| Auxiliary inputs | `im_shape=[2,2]`, `scale_factor=[2,2]` |
| Runtime | Windows 10 x64; .NET SDK `10.0.401`; ONNX Runtime `1.28.0`; OpenVINO runtime `2026.2.1` |
| Source state | Git HEAD `bf918e2`; the repository worktree was dirty and the new test had not yet been committed |

## Results

All four exact model/backend combinations passed. Each decoder returned two results and 300 exported candidates in each row. For every combination, the two prepared input-row SHA-256 values differed and the two canonical decoded-result SHA-256 values differed, confirming this was not duplicate-row reuse. The value `300` is the graph's post-NMS candidate count at score threshold `0`, not a count of ground-truth cells.

| Model | Backend | Input | Results | Candidates per row | Status |
|---|---|---|---:|---:|---|
| Wired | ONNX Runtime CPU | `[2,3,640,640]` + two `[2,2]` auxiliaries | 2 | `300, 300`; distinct input/result hashes | Pass |
| Wireless | ONNX Runtime CPU | `[2,3,640,640]` + two `[2,2]` auxiliaries | 2 | `300, 300`; distinct input/result hashes | Pass |
| Wired | OpenVINO CPU | `[2,3,640,640]` + two `[2,2]` auxiliaries | 2 | `300, 300`; distinct input/result hashes | Pass |
| Wireless | OpenVINO CPU | `[2,3,640,640]` + two `[2,2]` auxiliaries | 2 | `300, 300`; distinct input/result hashes | Pass |

The [machine-readable JSON](paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.json) records exact artifact/input hashes, source-region geometry, per-row input/result hashes, output names, batch shapes, row counts and statuses. The test is `OfficialDynamicBatchTableCellDetectorsRunOnOrtAndOpenVino` in `tests/DeploySharp.Visual.OpenCV.Tests/PaddleDocumentDynamicBatchIntegrationTests.cs`.

## Reproduction

Place the two ONNX files and the image at the paths above, then run from the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TABLE_CELL_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --configuration Release --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchTableCellDetectorsRunOnOrtAndOpenVino --verbosity minimal
```

The result only covers these two exports, ORT/OpenVINO CPU, and this Windows host. It does not change the separate precision, performance, TensorRT or OpenCV status for either model.
