# PP-DocLayout-L dynamic Batch execution evidence

This report records a real two-row execution of the official PP-DocLayout-L dynamic Paddle NMS export. It verifies auxiliary geometry binding, flattened `fetch_name_0` plus `bbox_num` row partitioning, and decoder result order. It is not a layout accuracy score or a throughput benchmark.

## Protocol

| Field | Value |
| --- | --- |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Batch | `2` identical full-source ROI rows |
| Model | `paddle-doc/pp-doclayout-l` / `pp-doclayout-l.onnx` |
| Model SHA-256 | `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9` |
| Image input | `[2,3,640,640]` |
| Auxiliary inputs | `im_shape=[2,2]`, `scale_factor=[2,2]` |
| Output contract | Flattened `[300*batch,6]` plus one `bbox_num` value per row |

## Results

| Backend | Result rows | Detections per row | Identical-row labels/boxes | Status |
| --- | ---: | ---: | --- | --- |
| ONNX Runtime CPU | 2 | 300 / 300 | preserved exactly | pass |
| OpenVINO CPU | 2 | 300 / 300 | preserved exactly | pass |

The machine-readable result is [`paddle-document-layout-dynamic-batch-ort-openvino-20261005.json`](paddle-document-layout-dynamic-batch-ort-openvino-20261005.json). The opt-in test is in `PaddleDocumentDynamicBatchIntegrationTests`:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_LAYOUT_DYNAMIC_BATCH_REPORT_PATH = `
  (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-layout-dynamic-batch-ort-openvino-20261005.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --filter FullyQualifiedName~PaddleDocumentDynamicBatchIntegrationTests
```

## Boundary

This proves true `[2,3,640,640]` binding and `bbox_num`-based row partitioning for this exact PP-DocLayout-L graph on one Windows host. It does not prove layout accuracy, TensorRT/OpenCV support, static PicoDet or PP-DocLayout-M/S batching, or a general performance improvement. The 300 detections per row are the export's post-NMS candidate count under `scoreThreshold=0`, not a ground-truth object count.
