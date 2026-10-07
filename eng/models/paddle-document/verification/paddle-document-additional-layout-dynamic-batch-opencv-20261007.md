# PP-DocLayout_plus-L and PP-DocBlockLayout: OpenCV DNN dynamic Batch (2026-10-07)

## Scope

This opt-in run attempted true batch=2 inference and full Paddle NMS decoding for two official dynamic ONNX exports on OpenCV DNN CPU. Each batch row is a distinct top/bottom `810×540` region from `bus.jpg`; the models use their registered image profile and per-row `im_shape` / `scale_factor` geometry inputs.

This is not a layout accuracy evaluation, parity test or throughput benchmark. The existing model-level OpenCV checkmarks describe prior single-image runs; this report records a narrower dynamic batch boundary and does not overturn those results.

## Artifacts and environment

| Item | Value |
|---|---|
| Host OS | Windows 11 Home, build `26200`, x64 |
| CPU | AMD Ryzen 7 5800H, 16 logical processors |
| Runtime | .NET `10.0.12`, OpenCV DNN `5.0.0` |
| Image | `E:\Data\image\bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Regions | `[0,0,810,540]` and `[0,540,810,540]` |
| PP-DocLayout_plus-L | `pp-doclayout-plus-l.onnx`, SHA-256 `d67689f6325ec0d4cc812308dbd1c84bc801e76731c0f183297013d6b59e0048`, input `[2,3,800,800]` |
| PP-DocBlockLayout | `pp-docblocklayout.onnx`, SHA-256 `abbf5febf79a35c9f329b4f591d440c7a4b3ae90cbb042bc8835900b49582218`, input `[2,3,640,640]` |
| Geometry inputs | `im_shape=[2,2]`, `scale_factor=[2,2]` |

## Results

| Model | Status | Error |
|---|---|---|
| PP-DocLayout_plus-L | Dynamic batch unsupported for this exact combination | `DS-OCV-8004`, OpenCV 5.0 `Requested blob not found` during `forward_many` |
| PP-DocBlockLayout | Dynamic batch unsupported for this exact combination | `DS-OCV-8004`, OpenCV 5.0 `Requested blob not found` during `forward_many` |

For both models, the session/contract setup completed and the native OpenCV DNN forward call failed while resolving output blob `fetch_name_1`. In accordance with the project's compatibility boundary, no importer/native-library root-cause investigation was performed. This result does not imply single-image OpenCV failure, does not change ORT/OpenVINO dynamic Batch evidence and does not generalize to other layout artifacts or OpenCV versions. There is no layout annotation for the cropped regions, so no quality or speed statement is made.

The [machine-readable JSON](paddle-document-additional-layout-dynamic-batch-opencv-20261007.json) preserves both ONNX hashes, image identity, error codes and complete native technical details. The integration test is `OfficialDynamicBatchAdditionalLayoutsRunOnOpenCvDnn` in `PaddleDocumentDynamicBatchIntegrationTests.cs`.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_ADDITIONAL_LAYOUT_OPENCV_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchAdditionalLayoutsRunOnOpenCvDnn --verbosity minimal
```

The opt-in probe passed `1/1` test because both expected adapter failures were captured as evidence. With the environment gate unset, the test is skipped by default.
