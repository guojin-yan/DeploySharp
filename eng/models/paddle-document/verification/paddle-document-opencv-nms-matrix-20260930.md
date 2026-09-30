# PP-Structure OpenCV DNN Paddle NMS matrix (2026-09-30)

Fourteen PP-Structure ONNX artifacts were attempted on the local OpenCV DNN 5.0 CPU runtime with the registered DeploySharp input and Paddle NMS decoder contracts. The source image was `E:\Data\image\bus.jpg`.

| Model | Status | Detection count / blocker |
| --- | --- | ---: |
| `paddle-doc/pp-doclayout-plus-l` | pass | 300 |
| `paddle-doc/pp-doclayout-m` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/pp-doclayout-s` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/pp-docblocklayout` | pass | 300 |
| `paddle-doc/picodet-layout-1x` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/picodet-layout-1x-table` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/picodet-s-layout-3cls` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/picodet-l-layout-3cls` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/rt-detr-h-layout-3cls` | pass | 300 |
| `paddle-doc/picodet-s-layout-17cls` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/picodet-l-layout-17cls` | unsupported | OpenCV `MatMul` importer: rank-1 input B |
| `paddle-doc/rt-detr-h-layout-17cls` | pass | 300 |
| `paddle-table/rt-detr-l-wired-cell-det` | pass | 300 |
| `paddle-table/rt-detr-l-wireless-cell-det` | pass | 300 |

The run produced 6 passes, 8 importer-level unsupported results and 0 unexpected failures. The eight failures all report `DS-OCV-8002` while specializing the ONNX input contract; the native OpenCV message is `DNN/MatMul: invalid shape of input B ... shape_B.size() is 1`. No workaround was applied to the ONNX graphs, so these rows remain unsupported for this OpenCV runtime rather than being presented as partial support.

This is importer/execution and decoder-contract evidence on one image. It does not establish layout or cell recall, table structure accuracy or performance ranking.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_NMS_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-opencv-nms-matrix-20260930.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentOpenCvSemanticIntegrationTests.AllLocalPaddleNmsDecodersRunOnOpenCvDnnWhenImporterSupportsThem
```
