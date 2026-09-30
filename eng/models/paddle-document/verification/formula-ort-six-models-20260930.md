# Formula recognition: six-model ORT semantic evidence (2026-09-30)

This record covers the six locally acquired PaddleX formula exports on the
same official image, `general_formula_rec_001.png`. Each model uses its own
official `inference.yml` vocabulary and BPE tokenizer, then runs through the
DeploySharp Formula decoder on ONNX Runtime CPU.

The machine-readable record is [`formula-ort-six-models-20260930.json`](formula-ort-six-models-20260930.json).
It contains model, tokenizer and input SHA-256 values, model dimensions,
token IDs/LaTeX hashes, EOS status, warnings and the complete decoded LaTeX.

| Model | Input | Tokens | LaTeX chars | EOS | Plus reference match |
| --- | --- | ---: | ---: | --- | --- |
| `pp-formulanet-plus-s` | 384 x 384 | 197 | 262 | yes | yes |
| `pp-formulanet-plus-m` | 384 x 384 | 197 | 262 | yes | yes |
| `pp-formulanet-plus-l` | 768 x 768 | 197 | 262 | yes | yes |
| `pp-formulanet-s` | 384 x 384 | 213 | 276 | yes | n/a |
| `pp-formulanet-l` | 768 x 768 | 197 | 262 | yes | n/a |
| `unimernet` | 672 x 192 | 208 | 270 | yes | n/a |

The Plus-S/M/L rows match the official reference after removing formatting
whitespace. FormulaNet-S/L and UniMERNet produce valid, EOS-terminated
semantic output on this image, but their LaTeX differs in command formatting or
accent choice; this report deliberately does not score those differences as a
dataset metric. Additional independently labeled formulas are still required
for the formula quality item in the PaddleOCR/PP-Structure closeout plan.

Reproduce the report (the test takes roughly one minute on the recorded
machine):

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_FORMULA_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/formula-ort-six-models-20260930.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --disable-build-servers `
  --filter 'FullyQualifiedName~PaddleDocumentSemanticIntegrationTests.FormulaExportsDecodeWithOfficialTokenizerOnRealOrtCpu'
```

OpenVINO remains separately marked unsupported for the six exact formula
artifacts because of the recorded `Loop-18` importer failures and UniMERNet
native crash; this ORT report does not change that backend matrix.
