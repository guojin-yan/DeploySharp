# PP-DocLayout_plus-L and PP-DocBlockLayout dynamic Batch (2026-10-07)

## Scope

This run validates batch=2 binding and per-row `PaddleDocumentNmsDecoder` isolation for two additional official dynamic layout exports: `PP-DocLayout_plus-L` and `PP-DocBlockLayout`. Each row is a distinct, non-overlapping half of `E:\Data\image\bus.jpg`; it is an execution probe, not a representative document-quality sample. Both models ran through ONNX Runtime CPU and OpenVINO CPU on the same Windows x64 host.

This is not a layout accuracy evaluation, cross-backend numerical parity test, throughput benchmark, or TensorRT/OpenCV batch claim.

## Artifacts and environment

| Item | Value |
|---|---|
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Batch source regions | `[0,0,810,540]` and `[0,540,810,540]` |
| ONNX Runtime | `1.28.0` |
| OpenVINO runtime | `2026.2.1` |
| .NET SDK | `10.0.401` |
| Image tensor | Plus-L `[2,3,800,800]`; DocBlockLayout `[2,3,640,640]` |
| Auxiliary tensors | `im_shape=[2,2]`, `scale_factor=[2,2]` |

| Model | ONNX SHA-256 |
|---|---|
| `paddle-doc/pp-doclayout-plus-l` | `d67689f6325ec0d4cc812308dbd1c84bc801e76731c0f183297013d6b59e0048` |
| `paddle-doc/pp-docblocklayout` | `abbf5febf79a35c9f329b4f591d440c7a4b3ae90cbb042bc8835900b49582218` |

## Results

All four exact model/backend combinations passed. Both batch rows were decoded independently and had distinct input tensor and canonical result digests. Each row contained 300 graph-emitted post-NMS candidates at score threshold `0`; this number is not a count of ground-truth regions.

| Model | Backend | Input | Result rows | Candidates per row | Row isolation |
|---|---|---|---:|---:|---|
| PP-DocLayout_plus-L | ONNX Runtime CPU | `[2,3,800,800]` + two `[2,2]` auxiliaries | 2 | `300, 300` | distinct input/result SHA |
| PP-DocLayout_plus-L | OpenVINO CPU | `[2,3,800,800]` + two `[2,2]` auxiliaries | 2 | `300, 300` | distinct input/result SHA |
| PP-DocBlockLayout | ONNX Runtime CPU | `[2,3,640,640]` + two `[2,2]` auxiliaries | 2 | `300, 300` | distinct input/result SHA |
| PP-DocBlockLayout | OpenVINO CPU | `[2,3,640,640]` + two `[2,2]` auxiliaries | 2 | `300, 300` | distinct input/result SHA |

The machine-readable [JSON report](paddle-document-additional-layout-dynamic-batch-ort-openvino-20261007.json) includes row-level input/result SHA-256 values, source geometry, output names and exact artifact hashes. The integration test is `OfficialDynamicBatchAdditionalLayoutsRunOnOrtAndOpenVino` in `tests/DeploySharp.Visual.OpenCV.Tests/PaddleDocumentDynamicBatchIntegrationTests.cs`.

## Reproduction

Ensure the two ONNX files are in `E:\Model\PaddleDocument\onnx` and `bus.jpg` is at the path above. Run from the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_ADDITIONAL_LAYOUT_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-ort-openvino-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --configuration Release --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchAdditionalLayoutsRunOnOrtAndOpenVino --verbosity minimal
```

The test passed `1/1`; it covers four exact model/backend pairs on this host. It does not establish representative page-level detection quality, parity between backend outputs, or performance. TensorRT/OpenCV batch state remains unchanged.
