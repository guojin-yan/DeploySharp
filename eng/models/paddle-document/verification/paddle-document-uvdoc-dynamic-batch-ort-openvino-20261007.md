# UVDoc dynamic Batch: ONNX Runtime / OpenVINO (2026-10-07)

## Scope

This run verifies that DeploySharp can bind a true two-row batch to the official UVDoc dynamic-batch ONNX export, run the full corrected-image decoder, preserve each row independently, and compare the resulting tensor across ONNX Runtime CPU and OpenVINO CPU. It uses two distinct, non-overlapping regions of one real source image, not duplicated rows. It is a model execution contract, not a visual-quality evaluation or throughput benchmark.

## Inputs and artifacts

| Item | Value |
|---|---|
| Model | `paddle-doc/uvdoc` |
| ONNX | `E:\Model\PaddleDocument\onnx\uvdoc.onnx` |
| ONNX SHA-256 | `10a66fe2e2a5e9fdf5f697385ddb38d26a58dca73fec539b45b4783d2e18e338` |
| Input image | `E:\Data\image\bus.jpg` |
| Image SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Prepared input | `[2,3,640,640]` |
| Prepared input tensor SHA-256 | `ea644a214e4e9cfe0d2169d3558275990bed25016e373a436372cfc7e2f6e7cf` |
| Model output | `[2,3,640,640]` |
| Batch rows | Two non-overlapping full-height ROIs, each `405×1080` (`x=0` and `x=405`) |
| Runtime/device | Windows x64, ONNX Runtime CPU and OpenVINO CPU |

## Result

Both backends returned two finite `640×640×3` results with page indexes `[0,1]`. The distinct left/right crop results remain distinct (within-backend row mean absolute difference `72.4329`). ORT/OpenVINO per-row mean absolute differences were `0.002078` and `0.002263`, both under the existing diagnostic threshold `0.01`; the combined mean was `0.002171` and maximum absolute difference was `0.103020`. This is bounded numerical parity, not pixel equivalence or visual-quality evidence.

| Backend | Input | Output rows | Duplicate-row mean absolute difference | Status |
|---|---|---:|---:|---|
| ONNX Runtime CPU | `[2,3,640,640]` | 2 | 72.432916 | Pass |
| OpenVINO CPU | `[2,3,640,640]` | 2 | 72.432947 | Pass |

The machine-readable [JSON result](paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.json) records the exact image/model hashes, shapes, per-row decoder sizes and parity values. The test is `OfficialDynamicBatchUnwarpingRunsOnOrtAndOpenVino` in `tests/DeploySharp.Visual.OpenCV.Tests/PaddleDocumentDynamicBatchIntegrationTests.cs`.

## Reproduction

Provide the model and image at the recorded paths, then run from the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_UVDOC_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --configuration Release --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchUnwarpingRunsOnOrtAndOpenVino --verbosity minimal
```

If either local asset is absent, the opt-in integration test reports an inconclusive skip rather than an execution failure. OpenCV DNN remains unsupported for this exact UVDoc graph due to its recorded importer failure; TensorRT remains unverified. This result does not change either status.
