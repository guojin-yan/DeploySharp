# FormulaNet-L ORT CUDA single-session lifecycle probe (2026-10-14)

This is a provider-lifecycle diagnostic, not a formula accuracy benchmark. It keeps exactly one ORT CPU Session and one ORT CUDA Session alive in one process, then runs three consecutive `realFormula` samples. The test records the raw integer output before invoking the DeploySharp formula decoder, so a provider-side token corruption cannot be confused with a decoder exception.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE = "1"
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE_LIMIT = "3"
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIFECYCLE_REPORT_PATH = "eng/models/paddle-document/verification/formula-formulanet-l-cuda-lifecycle-20261014.json"
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-build --no-restore -f net10.0 `
  -p:DeploySharpFormulaOrtCuda=true `
  --filter FullyQualifiedName~FormulaNetLRecordsSingleSessionCudaLifecycle
```

The probe requires the opt-in `Microsoft.ML.OnnxRuntime.Gpu.Windows` 1.23.2 restore variant, the local `pp-formulanet-l.onnx` and official tokenizer, and the pinned MathNet realFormula manifest. The machine-readable output is [formula-formulanet-l-cuda-lifecycle-20261014.json](formula-formulanet-l-cuda-lifecycle-20261014.json).

## Result on the recorded host

| sample | CPU raw shape / EOS | CUDA raw shape / EOS | valid token IDs | token parity | decoder |
| --- | --- | --- | ---: | --- | --- |
| `220808471-63.png` | `[1,220]` / yes | `[1,220]` / yes | `220/220` | equal | succeeded |
| `220312373-25.png` | `[1,58]` / yes | `[1,1025]` / no | `1/1025` | different | rejected as structured `DecodeFailed` |
| `230110053-1.png` | `[1,232]` / yes | `[1,1025]` / no | `1/1025` | different | rejected as structured `DecodeFailed` |

All three rows were produced by process `29156` with `sessionCreationCount=1` for both backend sessions. The first row is stable; the second and third rows contain 1,024 out-of-range `Int64` values and no EOS. The report preserves the raw minimum/maximum and first 12 values; it does not clamp or reinterpret them.

## Boundary

The result reproduces the earlier same-process CUDA contamination with a single long-lived FormulaNet-L Session. It rules out “one CUDA Session per sample” as a sufficient library-level fix for this model/runtime combination, but it does not identify whether the state originates in the exported autoregressive Loop, ORT CUDA provider state, or another native dependency. FormulaNet-L remains **process-isolated/bounded only** for ORT CUDA and is not admitted as ordinary multi-sample CUDA support. OpenVINO, OpenCV DNN and TensorRT status is unchanged. A future fix must provide a fixed-state encoder/decoder export, an isolated worker contract, or a provider/runtime correction, followed by raw token parity, decoder, quality and performance evidence.
