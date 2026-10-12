# Formula model OpenCV DNN batch=1 boundary (2026-10-12)

The six official formula ONNX exports were each attempted through the DeploySharp OpenCV DNN CPU adapter with one prepared input (`batch=1`) from `general_formula_rec_001.png`. The host was Windows `10.0.26200`, .NET `10.0.12`, OpenCV `5.0.0`; the input SHA-256 is `7885d4a349edcdfbfbc439305b8b554c2500f095949bc53d14835656b902dbcc`.

| Model | Prepared shape | Result | Importer boundary |
|---|---:|---|---|
| PP-FormulaNet Plus-S | `[1,1,384,384]` | unsupported | `DS-OCV-8002`, `ConstantOfShape` zero-dimension (`Expand.4`) |
| PP-FormulaNet-S | `[1,1,384,384]` | unsupported | `DS-OCV-8002`, `ConstantOfShape` zero-dimension (`Expand.4`) |
| PP-FormulaNet Plus-M | `[1,1,384,384]` | unsupported | `DS-OCV-8002`, `ConstantOfShape` zero-dimension (`Expand.4`) |
| PP-FormulaNet Plus-L | `[1,1,768,768]` | unsupported | `DS-OCV-8002`, `ConstantOfShape` zero-dimension (`Expand.4`) |
| PP-FormulaNet-L | `[1,1,768,768]` | unsupported | `DS-OCV-8002`, `ConstantOfShape` zero-dimension (`Expand.4`) |
| UniMERNet | `[1,1,192,672]` | unsupported | `DS-OCV-8002`, importer allocation failure while reading a constant |

No model reached OpenCV inference or the formula decoder, so this report has no formula quality, token parity or latency result. The machine-readable rows, exact ONNX SHA-256 values, input tensor SHA-256 values and native diagnostics are in [the JSON report](paddle-document-formula-batch1-opencv-20261012.json).

This closes only the previously unverified batch=1 question for these exact ONNX files on this OpenCV 5.0.0 adapter/runtime. Together with the earlier [dynamic batch=2 boundary](paddle-document-formula-dynamic-batch-opencv-20261007.md), it does not imply that every OpenCV release or every formula export is unsupported, and it does not replace ORT quality evidence or establish OpenVINO/TensorRT behavior. OpenCV importer root-cause work remains outside this scope.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_OPENCV_BATCH1_REPORT_PATH = `
  (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-formula-batch1-opencv-20261012.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~OfficialFormulaModelsBatchOneRunOnOpenCvDnnWhenSupported'
```
