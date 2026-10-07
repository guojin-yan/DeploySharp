# Formula model OpenCV DNN dynamic Batch boundary (2026-10-07)

## Scope and result

The six official dynamic-input formula exports were each attempted through the DeploySharp OpenCV DNN CPU adapter with two distinct prepared rows (`[2,1,384,384]`, `[2,1,768,768]` or `[2,1,192,672]`). All six attempts stopped before inference during OpenCV input-shape specialization / ONNX import with `DS-OCV-8002`; no formula decoder ran.

| Model | Input | Result |
|---|---|---|
| PP-FormulaNet Plus-S | `[2,1,384,384]` | Unsupported for this exact attempt; OpenCV 5.0 `ConstantOfShape` import rejected a zero dimension |
| PP-FormulaNet-S | `[2,1,384,384]` | Same `ConstantOfShape` / zero-dimension importer error |
| PP-FormulaNet Plus-M | `[2,1,384,384]` | Same `ConstantOfShape` / zero-dimension importer error |
| PP-FormulaNet Plus-L | `[2,1,768,768]` | Same `ConstantOfShape` / zero-dimension importer error |
| PP-FormulaNet-L | `[2,1,768,768]` | Same `ConstantOfShape` / zero-dimension importer error |
| UniMERNet | `[2,1,192,672]` | Unsupported for this exact attempt; OpenCV 5.0 `GatherND` importer reported zero available inputs |

The opt-in integration test passed `1/1` because each native adapter failure was intentionally captured as a result row. Exact model hashes, distinct prepared-row hashes, error codes and native diagnostics are in the [machine-readable JSON](paddle-document-formula-dynamic-batch-opencv-20261007.json). The same six formula models have separate ONNX Runtime CPU batch=2 evidence; this OpenCV result does not alter that status. Existing OpenVINO formula importer boundaries are also unchanged.

## Environment and reproduction

The host was x64 Windows build `26200`, .NET `10.0.12`, Release `net10.0`, OpenCV DNN `5.0.0`. The input was the official `general_formula_rec_001.png`; top and bottom bands were independently preprocessed and confirmed to have different tensor hashes.

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_OPENCV_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-formula-dynamic-batch-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --configuration Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~OfficialFormulaModelsDynamicBatchRunOnOpenCvDnnWhenSupported `
  --verbosity minimal
```

## Boundary

This is a negative importer/adapter result for the six exact model artifacts and batch=2 probe on one Windows host. It does not evaluate formula recognition quality, numerical parity, latency, other OpenCV versions, other backends or batch=1 behavior. In accordance with project scope, no OpenCV importer/native root-cause investigation was performed.
