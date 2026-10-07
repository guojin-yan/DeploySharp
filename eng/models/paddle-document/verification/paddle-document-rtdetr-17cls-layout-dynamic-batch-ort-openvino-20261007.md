# RT-DETR-H 17-class layout dynamic Batch evidence (2026-10-07)

## Result

The official `paddle-doc/rt-detr-h-layout-17cls` ONNX export completed true batch=2 inference and full Paddle-NMS decoding through DeploySharp on ONNX Runtime CPU and OpenVINO CPU. Both batch rows use distinct top/bottom regions from `E:\Data\image\bus.jpg`; the model input is `[2,3,640,640]`, and both `im_shape` and `scale_factor` are bound as `[2,2]`. Each backend returned two decoder rows with 300 detections per row. The test asserts that the two prepared rows and decoded results remain distinct.

| Backend | Input and geometry shapes | Decoded rows | Distinct input/result rows |
|---|---|---:|---|
| ONNX Runtime CPU | `[2,3,640,640]`; `im_shape=[2,2]`; `scale_factor=[2,2]` | 2 × 300 | Yes |
| OpenVINO CPU | `[2,3,640,640]`; `im_shape=[2,2]`; `scale_factor=[2,2]` | 2 × 300 | Yes |
| OpenCV DNN CPU | Session created; forward failed with `DS-OCV-8004`, native OpenCV 5.0 `Requested blob not found` | No decoded rows | No result |

Exact ONNX SHA-256: `556ba39ce91419069135e8446e155edda3d5cd475632d6ea5cec0f4dc072b41a`. Per-backend input/result row hashes, source geometry, output names, decoded labels and the OpenCV failure are recorded in the [machine-readable report](paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json).

## Reproduction

With `rt-detr-h-layout-17cls.onnx` available under `E:\Model\PaddleDocument\onnx`:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --configuration Release --no-restore `
  --filter 'FullyQualifiedName~OfficialDynamicBatchRtDetrSeventeenClassLayoutRunsOnOrtOpenVinoAndOpenCvDnn' `
  --logger 'console;verbosity=minimal'
```

## Boundary

The two regions are an execution/row-isolation probe, not representative document-layout ground truth. The result establishes neither layout accuracy nor throughput. The OpenCV DNN failure is scoped to dynamic batch=2 on this exact model/runtime/input contract; it does not overturn existing single-batch OpenCV model evidence or characterize other RT-DETR exports. TensorRT dynamic Batch remains unverified.
