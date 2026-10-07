# PP-OCRv4 seal detector dynamic Batch across CPU backends (2026-10-07)

## Scope

This run verifies true batch=2 tensor binding and row-preserving seal decoding for the official PP-OCRv4 mobile and server seal-detector ONNX exports. Each batch contains two distinct full source images (`demo_4.jpg` and `demo_5.jpg`). The six exact model/backend combinations ran on ONNX Runtime CPU, OpenVINO CPU and OpenCV DNN CPU on one Windows x64 host.

This is execution, output-row separation and source/page mapping evidence only. The images are not seal-labeled. The run does not establish precision/recall, cross-backend numerical parity, throughput, TensorRT Batch or cross-device behavior. OpenCV's raw mask digests differ from ORT/OpenVINO, so no numerical parity claim is made.

## Inputs and artifacts

| Item | Value |
|---|---|
| Image 0 | `E:\Data\ocr\demo_4.jpg` |
| Image 0 SHA-256 | `1b3978f3708bc31e3acfe603b052531a480db87ebaa1460559ee7d3eb1160593` |
| Image 1 | `E:\Data\ocr\demo_5.jpg` |
| Image 1 SHA-256 | `f76ac52816fa74fa9efac12ab4c382cbed3c137d02b689738edbcb63717abe9e` |
| Input tensor | `[2,3,224,224]` Float32 |
| Output tensor | `[2,1,224,224]` Float32 probability maps |
| ONNX Runtime | `1.28.0` |
| OpenVINO runtime | `2026.2.1` |
| OpenCV DNN | `JYPPX.OpenCV.CSharp.API` / native runtime `5.0.0` |
| Target | Windows x64, .NET 10, Release |

| Model | ONNX SHA-256 |
|---|---|
| `paddle-seal/ppocrv4-mobile` | `e4b20a5c47c70dbe67cebfdad970124e8c43b0c23cfa4eda5e17f77ef51f9199` |
| `paddle-seal/ppocrv4-server` | `f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7` |

## Results

All six model/backend combinations passed. Each row of the prepared input had a distinct SHA-256, each combination returned a rank-4 mask with batch dimension 2, and its two mask-row digests differed. The decoder returned two results in order; `PageIndex` was `0,1`, and each result's source-image SHA matched the corresponding input row.

| Model | Backend | Batch input → output | Regions (row 0, row 1) | Row/source mapping |
|---|---|---|---:|---|
| PP-OCRv4 mobile seal | ONNX Runtime CPU | `[2,3,224,224]` → `[2,1,224,224]` | `0, 0` | pass |
| PP-OCRv4 server seal | ONNX Runtime CPU | `[2,3,224,224]` → `[2,1,224,224]` | `1, 0` | pass |
| PP-OCRv4 mobile seal | OpenVINO CPU | `[2,3,224,224]` → `[2,1,224,224]` | `0, 0` | pass |
| PP-OCRv4 server seal | OpenVINO CPU | `[2,3,224,224]` → `[2,1,224,224]` | `1, 0` | pass |
| PP-OCRv4 mobile seal | OpenCV DNN CPU | `[2,3,224,224]` → `[2,1,224,224]` | `0, 0` | pass |
| PP-OCRv4 server seal | OpenCV DNN CPU | `[2,3,224,224]` → `[2,1,224,224]` | `1, 0` | pass |

The mobile model returning zero regions and the server model returning `1,0` on these unannotated images are diagnostics, not evidence of recall. OpenCV row masks are individually distinct but their digests do not match the corresponding ORT/OpenVINO outputs; investigating threshold/interpolation/numeric differences requires a labeled seal set and direct tensor-error measurements. Per-row input/output hashes, mask dimensions and decoder metadata are retained in the [machine-readable JSON](paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json).

## Reproduction

Place `ppocrv4-mobile-seal-det.onnx` and `ppocrv4-server-seal-det.onnx` under `E:\Model\PaddleDocument\onnx`; retain the two image paths above. Run from the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_SEAL_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --configuration Release --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchSealDetectorsRunOnOrtOpenVinoAndOpenCvDnn --verbosity minimal
```

The opt-in test passed `1/1`, covering all six exact model/backend pairs. This report is not a model-quality or speed result.
