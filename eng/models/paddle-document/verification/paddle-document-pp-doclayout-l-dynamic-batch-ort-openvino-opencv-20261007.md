# PP-DocLayout-L dynamic Batch: ORT, OpenVINO and OpenCV DNN (2026-10-07)

This report records a real `batch=2` run of the official dynamic Paddle-NMS export `paddle-doc/pp-doclayout-l`. The opt-in integration test runs each backend through input preparation, geometry binding, inference and the complete per-row NMS decoder.

## Inputs and model

| Item | Value |
|---|---|
| ONNX | `pp-doclayout-l.onnx` |
| Model SHA-256 | `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9` |
| Input image | `E:\Data\image\bus.jpg` |
| Image SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Batch rows | Two distinct top/bottom `810×540` regions from the `810×1080` image |
| Tensor shape | `[2,3,640,640]` |
| Auxiliary inputs | `im_shape=[2,2]`, `scale_factor=[2,2]` |

## Execution environment

The run used Windows 11 Home, build `26200`, x64; AMD Ryzen 7 5800H (16 logical processors); and .NET `10.0.12`. The test project's resolved packages were ONNX Runtime `1.28.0`, OpenVINO runtime `2026.2.1`, and OpenCV runtime `5.0.0`. The machine-readable report records the OS/runtime architecture and framework description; the OpenCV failure includes its native `5.0.0` diagnostic.

The two prepared image rows and the two decoded result rows have distinct SHA-256 digests on both successful backends. Each row returned 300 post-NMS candidates at the profile's zero score threshold. The candidate count is an export/runtime observation, not ground truth.

## Results

| Backend | Result | Decoder output | Notes |
|---|---|---|---|
| ONNX Runtime CPU | Passed | 2 rows, 300 detections per row | Full `PaddleNmsRegions` decoder; distinct input and result rows |
| OpenVINO CPU | Passed | 2 rows, 300 detections per row | Full `PaddleNmsRegions` decoder; distinct input and result rows |
| OpenCV DNN CPU | Unsupported for this exact dynamic-Batch case | — | Session creation succeeded; forward failed with `DS-OCV-8004`, OpenCV 5.0 `Requested blob not found` |

The OpenCV failure is scoped to this exact model/runtime/input contract at `batch=2`; it does not change the existing single-image OpenCV evidence. No importer investigation was performed. The run does not compare ORT/OpenVINO numerical parity, and the image regions have no layout annotations; therefore it is not an accuracy or throughput result. TensorRT Batch remains unverified.

The complete per-backend input/result digests, auxiliary bindings, output names and OpenCV technical details are in the [machine-readable JSON report](paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.json).

## Reproduction

From the repository root on a machine with the model and image at the paths above:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_PP_DOCLAYOUT_L_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj --filter "FullyQualifiedName~OfficialDynamicBatchDocLayoutLRunOnOrtOpenVinoAndOpenCvDnn" --logger "console;verbosity=normal"
Remove-Item Env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL
Remove-Item Env:DEPLOYSHARP_PADDLE_DOCUMENT_PP_DOCLAYOUT_L_DYNAMIC_BATCH_REPORT_PATH
```

The opt-in run completed `1/1` test. With the gate unset, this external-model test remains skipped by default.
