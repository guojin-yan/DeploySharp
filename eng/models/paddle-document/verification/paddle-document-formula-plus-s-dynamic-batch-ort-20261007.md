# PP-FormulaNet Plus-S dynamic Batch on ONNX Runtime CPU (2026-10-07)

## Scope and result

This opt-in integration run verifies real batch=2 execution and formula-token decoder row isolation for the official `paddle-formula/pp-formulanet-plus-s` ONNX export on ONNX Runtime CPU. The official formula image is split into two distinct horizontal bands, prepared as separate batch rows, and each band is also run independently through the same session. The batch-decoded token IDs and LaTeX are then compared exactly with those independent runs.

The dynamic input bound as `[2,1,384,384]` and token output shape was `[2,180]`. The two prepared row hashes differ. Both rows reached EOS; the top band decoded to 175 tokens and the bottom band to 66. Each row matched its independent run exactly, including token sequence and LaTeX SHA-256. The test passed `1/1`.

## Exact artifacts and environment

| Item | Value |
|---|---|
| Host | Windows x64, OS build `10.0.26200` |
| .NET | `10.0.12`, target `net10.0`, Release |
| Backend | ONNX Runtime CPU (`1.28.0`) |
| Model | `pp-formulanet-plus-s.onnx` |
| Model SHA-256 | `e048777cc76258f2f2daa3cd7a54043f2265173fde357a9e9ad34665003a465d` |
| Tokenizer | official `inference.yml`, SHA-256 `96062655d94c21d39274328dbc82c1a487e66addb8425f5a7fd5b7dfb2421ec3` |
| Input | `general_formula_rec_001.png`, SHA-256 `7885d4a349edcdfbfbc439305b8b554c2500f095949bc53d14835656b902dbcc` |
| Batch input | `[2,1,384,384]`; top and bottom horizontal bands have distinct prepared tensor SHA |
| Batch output | `[2,180]`; top band 175 tokens, bottom band 66 tokens; both reach EOS |

Per-row token hashes, source IDs and artifact metadata are in the [machine-readable report](paddle-document-formula-plus-s-dynamic-batch-ort-20261007.json).

## Reproduction

Place the exact model at `E:\Model\PaddleDocument\onnx\pp-formulanet-plus-s.onnx`, its official source at `E:\Model\PaddleDocument\source\pp-formulanet-plus-s\PP-FormulaNet_plus-S_infer\inference.yml`, and the image at `E:\Model\PaddleDocument\validation\general_formula_rec_001.png`. From the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-formula-plus-s-dynamic-batch-ort-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --configuration Release --no-restore `
  --filter FullyQualifiedName~OfficialFormulaPlusSDynamicBatchMatchesIndependentRunsOnOrtCpu --verbosity minimal
```

## Boundary

This proves dynamic Batch binding, row order and exact single-versus-batch decoder parity for two distinct image regions from one source image, for this one export and ONNX Runtime CPU on this host. These cropped regions are execution probes, not separately labeled formulas or quality samples. It does not establish OpenVINO, OpenCV DNN or TensorRT batch support, throughput, or behavior on another device/runtime. FormulaNet variants and UniMERNet remain separate exact artifacts and require their own evidence.
