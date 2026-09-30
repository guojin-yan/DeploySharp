# Formula recognition: six models × five controlled variants (2026-09-30)

All six locally acquired PaddleX formula exports were run through DeploySharp on ONNX Runtime CPU against five traceable variants of the official `general_formula_rec_001.png`: the original, a white-border variant, a contrast variant, a JPEG variant and a blur variant. Each run used that model's official `inference.yml` tokenizer and model-specific input size.

| Model | Variants | EOS / no truncation | Normalized reference match | Token count range |
| --- | ---: | ---: | ---: | ---: |
| `pp-formulanet-plus-s` | 5 | 5/5 | 4/5 | 197–204 |
| `pp-formulanet-plus-m` | 5 | 5/5 | 4/5 | 197–197 |
| `pp-formulanet-plus-l` | 5 | 5/5 | 4/5 | 197–206 |
| `pp-formulanet-s` | 5 | 5/5 | 0/5 | 213–214 |
| `pp-formulanet-l` | 5 | 5/5 | 0/5 | 197–197 |
| `unimernet` | 5 | 5/5 | 0/5 | 208–210 |

The three Plus models match the normalized reference on the original, white-border, contrast and blur variants. All three differ on the JPEG variant, which is a useful controlled degradation signal rather than a natural-image accuracy score. FormulaNet-S/L and UniMERNet produce complete, warning-free sequences on all five variants but differ from this Plus-model reference equation; their outputs are retained in the JSON so the token/LaTeX differences remain auditable. The public formula result removes EOS from `TokenIds`, so `reachedEndOfSequence` is determined by the absence of the decoder's `missing-eos:sequence-may-be-truncated` warning; `explicitEosTokenRetained` is therefore false for all rows by design.

The machine-readable report contains the model, tokenizer and image SHA-256 values, token/LaTeX hashes, complete LaTeX strings, warning lists and variant-level reference flags: [formula-six-models-variants-20260930.json](formula-six-models-variants-20260930.json). The five variant images and manifest are generated locally by `eng/models/paddle-ocr/scripts/Generate-FormulaVariantCase.py` and are not redistributed with the repository.

This is a controlled multi-model regression over one equation. It does not measure formula dataset accuracy, symbol-level CER, natural-image robustness or any non-ORT backend. The next quality gate is a legally redistributable multi-equation labeled set, followed by per-model normalized LaTeX/CER evaluation and the currently blocked OpenVINO combinations.

## Reproduction

```powershell
python eng/models/paddle-ocr/scripts/Generate-FormulaVariantCase.py `
  --source-image E:\Model\PaddleDocument\validation\general_formula_rec_001.png `
  --output-root artifacts\formula-variants-20260929
$env:DEPLOYSHARP_PADDLE_FORMULA_VARIANTS = '1'
$env:DEPLOYSHARP_PADDLE_FORMULA_VARIANT_ROOT = (Join-Path (Get-Location) 'artifacts/formula-variants-20260929')
$env:DEPLOYSHARP_PADDLE_FORMULA_VARIANT_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/formula-six-models-variants-20260930.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentFormulaVariantIntegrationTests.SixFormulaModelsRecordMultiVariantQualityEvidence
```
