# RT-DETR-H layout dynamic Batch execution evidence

This report records a real two-row execution of the official `RT-DETR-H_layout_3cls` Paddle NMS export. It is a cross-architecture execution and decoder-order check, not a layout accuracy score or a throughput benchmark.

## Protocol

| Field | Value |
| --- | --- |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Batch | `2` identical full-source ROI rows |
| Model | `paddle-doc/rt-detr-h-layout-3cls` / `rt-detr-h-layout-3cls.onnx` |
| Model SHA-256 | `e003528247ea0324eba25c94127a1ff5ef48f8a83ae725a8f42f8f0a1d551d8b` |
| Image input | `[2,3,640,640]` |
| Auxiliary inputs | `im_shape=[2,2]`, `scale_factor=[2,2]` |
| Output contract | Flattened `[300*batch,6]` plus one `bbox_num` value per row |

## Results

| Backend | Result rows | Detections per row | Identical-row labels/boxes | Status |
| --- | ---: | ---: | --- | --- |
| ONNX Runtime CPU | 2 | 300 / 300 | preserved exactly | pass |
| OpenVINO CPU | 2 | 300 / 300 | preserved exactly | pass |

The machine-readable result is [`paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.json`](paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.json). The opt-in test is in `PaddleDocumentDynamicBatchIntegrationTests`:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RTDETR_DYNAMIC_BATCH_REPORT_PATH = `
  (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --filter FullyQualifiedName~OfficialDynamicBatchRtDetrLayoutRunsOnOrtAndOpenVino
```

## Boundary

This proves true `[2,3,640,640]` binding and `bbox_num`-based row partitioning for this exact RT-DETR-H graph on one Windows host. It does not prove layout accuracy, TensorRT/OpenCV support, static PicoDet or PP-DocLayout-M/S batching, or a general performance improvement. The 300 detections per row are the export's post-NMS candidate count under `scoreThreshold=0`, not a ground-truth object count.
