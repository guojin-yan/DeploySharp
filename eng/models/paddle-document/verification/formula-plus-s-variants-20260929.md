# FormulaNet Plus-S controlled variant evidence (2026-09-29)

The official `general_formula_rec_001.png` formula image was transformed into five deterministic variants: original, white border, contrast, JPEG quality 45 and light blur. The expected LaTeX is the official Plus-S semantic reference already used by the single-image regression. This is a controlled one-formula quality probe, not a formula dataset score.

ORT CPU results:

| Variant | Tokens | EOS | Normalized LaTeX exact | Observation |
| --- | ---: | --- | --- | --- |
| original | 197 | no | yes | output matches the reference; decoder reaches its bounded sequence limit |
| white-border | 197 | no | yes | exact semantic output |
| contrast | 197 | no | yes | exact semantic output |
| light blur | 197 | no | yes | exact semantic output |
| JPEG q45 | 204 | no | no | `\\tilde{\\Psi}` / `\\bar{\\epsilon}` substitutions under compression |

All five runs returned tokens and no decoder warnings. The JPEG result is a useful quality boundary: small compression artifacts changed two symbols while leaving the overall expression recognizable. The test records image/model SHA-256 and full LaTeX for every row. It does not claim formula accuracy, EOS completeness, or robustness beyond this one source expression.

Reproduce with:

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-FormulaVariantCase.py `
  --image E:\Model\PaddleDocument\validation\general_formula_rec_001.png `
  --output-root artifacts\formula-variants-20260929

$env:DEPLOYSHARP_PADDLE_FORMULA_VARIANTS = '1'
$env:DEPLOYSHARP_PADDLE_FORMULA_VARIANT_ROOT = (Resolve-Path 'artifacts\formula-variants-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentFormulaVariantIntegrationTests
```
