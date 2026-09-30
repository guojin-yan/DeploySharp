# PP-Structure table-cell OpenVINO execution matrix (2026-09-30)

The wired and wireless RT-DETR table-cell artifacts were executed on the local OpenVINO CPU runtime with their registered DeploySharp Paddle NMS input and decoder contracts. The source image was `E:\Data\image\bus.jpg`.

| Model | Status | Detection count |
| --- | --- | ---: |
| `paddle-table/rt-detr-l-wired-cell-det` | pass | 300 |
| `paddle-table/rt-detr-l-wireless-cell-det` | pass | 300 |

Both exact artifacts passed and returned finite scores and coordinates. Model SHA-256, input size, labels and the execution boundary are in the [machine-readable report](paddle-document-openvino-table-cell-matrix-20260930.json).

This is execution and decoder-contract evidence on one image. It does not establish cell recall, table structure accuracy or performance ranking.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_TABLE_CELL_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-openvino-table-cell-matrix-20260930.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentOpenVinoSemanticIntegrationTests.AllLocalTableCellDecodersRunOnRealOpenVinoCpu
```
