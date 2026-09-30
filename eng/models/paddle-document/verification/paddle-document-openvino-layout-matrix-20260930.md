# PP-Structure layout OpenVINO execution matrix (2026-09-30)

The twelve previously unverified layout artifacts were executed on the local OpenVINO CPU runtime with their registered DeploySharp input and decoder contracts. The source image was `E:\Data\image\bus.jpg`; every model loaded, ran inference and produced finite decoded scores/coordinates.

| Model | Status | Detection count |
| --- | --- | ---: |
| `pp-doclayout-plus-l` | pass | 300 |
| `pp-doclayout-m` | pass | 100 |
| `pp-doclayout-s` | pass | 100 |
| `pp-docblocklayout` | pass | 300 |
| `picodet-layout-1x` | pass | 18 |
| `picodet-layout-1x-table` | pass | 3 |
| `picodet-s-layout-3cls` | pass | 66 |
| `picodet-l-layout-3cls` | pass | 76 |
| `rt-detr-h-layout-3cls` | pass | 300 |
| `picodet-s-layout-17cls` | pass | 77 |
| `picodet-l-layout-17cls` | pass | 63 |
| `rt-detr-h-layout-17cls` | pass | 300 |

All 12/12 exact artifacts passed and 0 were classified as unsupported or failed. The complete JSON records model SHA-256, model size, label contract, auxiliary geometry-input choice and detection count: [paddle-document-openvino-layout-matrix-20260930.json](paddle-document-openvino-layout-matrix-20260930.json).

This is execution and decoder-contract evidence on one image. It does not establish layout recall, mAP, label accuracy or performance ranking; those require aligned region annotations and a controlled multi-image protocol.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_LAYOUT_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-openvino-layout-matrix-20260930.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentOpenVinoSemanticIntegrationTests.AllLocalLayoutDecodersRunOnRealOpenVinoCpu
```
