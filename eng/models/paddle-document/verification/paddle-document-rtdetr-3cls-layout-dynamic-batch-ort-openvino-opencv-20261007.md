# RT-DETR-H 3-class layout dynamic Batch evidence (2026-10-07)

## Result

The official `paddle-doc/rt-detr-h-layout-3cls` ONNX export was exercised with true batch=2 input and the complete Paddle-NMS region decoder on ONNX Runtime CPU, OpenVINO CPU and OpenCV DNN CPU. Each batch row came from a different horizontal region of `E:\Data\image\bus.jpg`; the image tensor was `[2,3,640,640]`, and both `im_shape` and `scale_factor` were bound as `[2,2]`.

| Backend | Result | Decoder output |
|---|---|---|
| ONNX Runtime CPU | Passed | 2 rows; 300 candidates per row |
| OpenVINO CPU | Passed | 2 rows; 300 candidates per row |
| OpenCV DNN CPU | Unsupported for this exact dynamic-batch contract | Session creation succeeded; `forward_many` returned `DS-OCV-8004 / Requested blob not found`; no decoded rows |

The two prepared input rows and the two decoded result rows have distinct SHA-256 digests for each successful backend. The model SHA-256 is `e003528247ea0324eba25c94127a1ff5ef48f8a83ae725a8f42f8f0a1d551d8b`; the source image SHA-256 is `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`. The complete hashes, geometry, output names and native OpenCV error are retained in the [machine-readable report](paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json).

## Environment and reproduction

The recorded host is x64, Windows build `26200`, .NET `10.0.12`, with 16 logical processors. Runtime versions are ONNX Runtime `1.28.0`, OpenVINO `2026.2.1` and OpenCV DNN `5.0.0`.

With the ONNX and image available at the paths recorded in the JSON:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RTDETR_3CLASS_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchRtDetrThreeClassLayoutRunsOnOrtOpenVinoAndOpenCvDnn `
  --verbosity minimal
```

The opt-in integration test passed `1/1`; the OpenCV failure is captured as an expected backend boundary rather than treated as a test failure.

## Boundary

This is execution and batch-row isolation evidence for this exact model, batch size, runtime versions and one Windows host. The regions are unannotated execution probes, not layout ground truth; 300 is the exported post-NMS candidate count, not an accuracy measure. No numerical parity, throughput, TensorRT dynamic-batch or general layout-model claim is made. The OpenCV result is limited to dynamic batch=2 for this exact contract and does not change the existing single-image OpenCV evidence. In accordance with the project scope, the OpenCV importer/native failure was not investigated further.
