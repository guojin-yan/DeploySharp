# Formula recognition: six models × five controlled variants (2026-09-30)

All six locally acquired PaddleX formula exports were run through DeploySharp on ONNX Runtime CPU against five traceable variants of the official `general_formula_rec_001.png`: the original, a white-border variant, a contrast variant, a JPEG variant and a blur variant. Each run used that model's official `inference.yml` tokenizer and model-specific input size.

| Model | Variants | EOS / no truncation | Normalized reference match | CER min / max / mean | Token count range |
| --- | ---: | ---: | ---: | ---: | ---: |
| `pp-formulanet-plus-s` | 5 | 5/5 | 4/5 | 0.00% / 5.92% / 1.18% | 197–204 |
| `pp-formulanet-plus-m` | 5 | 5/5 | 4/5 | 0.00% / 2.37% / 0.47% | 197–197 |
| `pp-formulanet-plus-l` | 5 | 5/5 | 4/5 | 0.00% / 3.55% / 0.71% | 197–206 |
| `pp-formulanet-s` | 5 | 5/5 | 0/5 | 4.73% / 13.02% / 7.10% | 213–214 |
| `pp-formulanet-l` | 5 | 5/5 | 0/5 | 2.37% / 2.37% / 2.37% | 197–197 |
| `unimernet` | 5 | 5/5 | 0/5 | 5.33% / 11.24% / 6.51% | 208–210 |

The three Plus models match the normalized reference on the original, white-border, contrast and blur variants. All three differ on the JPEG variant; their mean normalized character error rates are 1.18%, 0.47% and 0.71%. FormulaNet-S/L and UniMERNet produce complete, warning-free sequences on all five variants but differ from this Plus-model reference equation, with mean normalized character error rates of 7.10%, 2.37% and 6.51%; their outputs are retained in the JSON so the token/LaTeX differences remain auditable. CER here is Levenshtein distance over the whitespace-stripped LaTeX string, not a published formula benchmark. The public formula result removes EOS from `TokenIds`, so `reachedEndOfSequence` is determined by the absence of the decoder's `missing-eos:sequence-may-be-truncated` warning; `explicitEosTokenRetained` is therefore false for all rows by design.

The machine-readable report contains the model, tokenizer and image SHA-256 values, token/LaTeX hashes, complete LaTeX strings, warning lists and variant-level reference flags: [formula-six-models-variants-20260930.json](formula-six-models-variants-20260930.json). The five variant images and manifest are generated locally by `eng/models/paddle-ocr/scripts/Generate-FormulaVariantCase.py` and are not redistributed with the repository.

This is a controlled multi-model regression over one equation. It does not measure formula dataset accuracy, symbol-level CER, natural-image robustness or any non-ORT backend. The next quality gate is a legally redistributable multi-equation labeled set, followed by per-model normalized LaTeX/CER evaluation and the currently blocked OpenVINO combinations.

## 2026-10-02 reproducibility rerun

The same two external test methods were rerun on the current `DeploySharpV2.0` worktree with the pinned model, tokenizer, image and five-variant manifest. Both methods passed (`2/2`, `0` skipped, `0` failed); all six models produced 5/5 non-empty, non-truncated sequences (30/30 rows). The six-model report was compared row by row with the checked-in 2026-09-30 report: model, variant, LaTeX SHA, normalized edit distance and EOS state matched for all 30 rows.

The rerun reproduced the per-model exact-match/CER summary: Plus-S `4/5, 1.1834%`, Plus-M `4/5, 0.4734%`, Plus-L `4/5, 0.7101%`, FormulaNet-S `0/5, 7.1006%`, FormulaNet-L `0/5, 2.3669%`, and UniMERNet `0/5, 6.5089%`. The generated rerun JSON is kept in the local ignored artifact directory (`artifacts/formula-variants-20260929/formula-six-models-variants-20261002-rerun.json`) because it contains full output strings; the checked-in 2026-09-30 JSON remains the canonical review artifact. This rerun confirms decoder reproducibility only and does not close the multi-equation labeled-set, symbol-level CER or OpenVINO gates.

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
