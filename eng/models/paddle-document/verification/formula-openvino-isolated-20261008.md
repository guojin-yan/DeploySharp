# Formula OpenVINO isolated recheck (2026-10-08)

The six exact formula ONNX exports were rechecked one per isolated test process with OpenVINO runtime `2026.2.1`, `JYPPX.OpenVINO.CSharp.API 3.3.1`, Windows 11 build `26200`, .NET `10.0.401`, and the official `general_formula_rec_001.png` input. The source worktree was dirty; no unrelated files were staged.

| Exact artifact | Result | Failure boundary |
| --- | --- | --- |
| `pp-formulanet-plus-s` | unsupported | `Loop-18`: loop body canonical inputs `1` vs required dependency/mandatory total `31` |
| `pp-formulanet-plus-m` | unsupported | `Loop-18`: required total `63` |
| `pp-formulanet-plus-l` | unsupported | `Loop-18`: required total `79` |
| `pp-formulanet-s` | unsupported | `Loop-18`: required total `31` |
| `pp-formulanet-l` | unsupported | `Loop-18`: required total `79` |
| `unimernet` | unsupported | Isolated test host aborted while OpenVINO read the model; no decoder/token inference was reached |

The five non-crashing artifacts fail in the OpenVINO ONNX frontend before session creation completes:

```text
The provided loop body graph canonical inputs size (1),
does not match the sum of loop carried dependencies and two mandatory inputs (...)
```

The UniMERNet process is isolated and classified as an unsupported native model-reader boundary rather than allowed to hide the other results. The machine-readable details are in [`formula-openvino-isolated-20261008.json`](formula-openvino-isolated-20261008.json).

This recheck does not indicate a missing DLL, a DeploySharp Decoder bug, or a formula accuracy result. It confirms that no exact formula ONNX export is currently admitted for OpenVINO on this runtime. The existing ORT CPU formula evidence remains valid; changing the matrix to OpenVINO `✓` requires a compatible export (or a validated graph rewrite) plus token-level parity with ORT.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_OPENVINO = '1'
foreach ($model in @('pp-formulanet-plus-s','pp-formulanet-plus-m','pp-formulanet-plus-l',
  'pp-formulanet-s','pp-formulanet-l','unimernet')) {
  $env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_MODEL = $model
  dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
    -c Release --no-build --no-restore `
    --filter 'FullyQualifiedName~PaddleDocumentFormulaOpenVinoIsolatedIntegrationTests.SelectedFormulaExportRunsOnOpenVinoCpu'
}
```
