# FormulaNet / UniMERNet OpenVINO compatibility conversion (2026-10-04)

This is the follow-up to the historical OpenVINO blocker report. The experiment generated six derived ONNX files by alpha-renaming only Loop-body formal parameters, then ran each file in an isolated OpenVINO CPU test process. The source models were not overwritten and the derived files were kept outside the repository at `E:\Model\PaddleDocument\onnx-formula-loop-compat-20261004`.

The machine-readable record is [`formula-openvino-loop-compat-20261004.json`](formula-openvino-loop-compat-20261004.json).

## Result

| Model | ONNX checker | OpenVINO execution | ORT tokens | OpenVINO tokens | Verdict |
| --- | --- | --- | ---: | ---: | --- |
| `pp-formulanet-plus-s` | pass | ran, but output was not token-parity equivalent | 197 | 3 | not admitted |
| `pp-formulanet-plus-m` | pass | Loop `Reshape.22` shape conflict | 197 | n/a | not admitted |
| `pp-formulanet-plus-l` | pass | Loop `Reshape.249` shape conflict | 197 | n/a | not admitted |
| `pp-formulanet-s` | pass | ran to 1023 tokens without EOS; output differed | 213 | 1023 | not admitted |
| `pp-formulanet-l` | pass | Loop `Reshape.249` shape conflict | 197 | n/a | not admitted |
| `unimernet` | pass | native `ov_core_read_model_utf8` access violation (`0xC0000005`) | 208 | n/a | not admitted |

The two graphs that reached execution are important negative evidence. `pp-formulanet-plus-s` produced only three tokens although the ORT reference produced 197. `pp-formulanet-s` reached the declared sequence limit, emitted `missing-eos:sequence-may-be-truncated`, and produced 1023 tokens instead of the ORT reference's 213. An importer pass alone is therefore insufficient for this model family.

The remaining three FormulaNet graphs fail inside an OpenVINO `Loop` body when the CPU plugin evaluates a reshape whose runtime input is `(1,16,1,2)` while the pattern is `(16,1,1)`. UniMERNet still terminates in the native ONNX reader before a managed session can be created. These failures are isolated per model and do not affect the existing ORT CPU evidence.

## Reproduction

Generate a derived graph without overwriting the official file:

```powershell
& E:\Model\PaddleDocument\paddle3\python.exe `
  .\scripts\normalize_loop_parameters.py `
  E:\Model\PaddleDocument\onnx\pp-formulanet-plus-s.onnx `
  E:\Model\PaddleDocument\onnx-formula-loop-compat-20261004\pp-formulanet-plus-s.onnx
```

Run one isolated model after setting the derived model root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_OPENVINO = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_MODEL_ROOT = 'E:\Model\PaddleDocument\onnx-formula-loop-compat-20261004'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_MODEL = 'pp-formulanet-plus-s'
dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  --configuration Release --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentFormulaOpenVinoIsolatedIntegrationTests'
```

The derived root is an explicit compatibility-test switch. With the variable unset, the test uses the catalogued official SHA-256 and preserves the normal artifact-integrity contract.

## Admission boundary

The alpha-renamed artifacts must not be published as OpenVINO-compatible assets. The next attempt requires an exporter or graph rewrite that preserves Loop state shapes, followed by the same isolated importer test and token-level comparison with ORT. Until that evidence exists, all six formula/OpenVINO combinations remain `not supported` in the backend matrix; the six formula/ORT combinations remain supported only within their existing single-image and controlled-variant evidence boundaries.
