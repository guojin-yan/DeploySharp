# FormulaNet-L expanded token-diversity probe

## Scope and selection

This adds seven raw-token traces to the initial four-image diagnostic in [the first report](formula-formulanet-l-token-diversity-20261007.md). They are the seven highest decoded prediction-token counts among rows not already traced in the existing 121-sample MathNet realFormula ORT CPU report. This is a targeted long-output probe, not a random or representative sample. The model/export SHA is unchanged.

The seven images were rerun through the existing opt-in FormulaNet-L ORT CPU test with token tracing enabled, in two batches (six images, then the final ranking correction). Both test runs completed `1/1` with no failure; total elapsed test time was about 71.5 seconds. Their image SHA, prediction length, CER and EOS result match the pinned per-sample rows in the [121-sample baseline](formula-realformula-pp-formulanet-l-ort-20261007.json).

## Added samples

| Image | Raw IDs | EOS raw index | Decoded tokens / characters | CER | Low-diversity window / exact suffix |
| --- | ---: | ---: | ---: | ---: | --- |
| `220408168-60.png` | 642 | 641 | 313 / 546 | 72% | none / none |
| `210807632-266.png` | 505 | 504 | 248 / 376 | 84% | none / none |
| `230101132-49.png` | 484 | 483 | 232 / 358 | 48% | none / none |
| `211205994-13.png` | 276 | 275 | 189 / 273 | 429% | none / none |
| `220406062-108.png` | 339 | 338 | 163 / 269 | 71% | none / none |
| `211201486-39.png` | 312 | 311 | 154 / 274 | 75% | none / none |
| `230101946-47.png` | 324 | 323 | 153 / 402 | 109% | none / none |

All seven reached EOS before the 1,024-iteration graph boundary. None met the initial report's exploratory 128-token low-diversity threshold, and none had an exact terminal period of at least three cycles for a period up to 64 tokens. One sample nevertheless has 429% CER. This shows why the descriptive threshold cannot be used as a correctness gate: a negative flag does not imply a good transcription. Across all eleven selected traces, the three previously flagged cases and one exact-cycle case remain isolated observations, not evidence of causation or prevalence.

## Reproduction

The [machine-readable report](formula-formulanet-l-token-diversity-expanded-20261007.json) contains all eleven summaries, the model SHA, report input SHA-256 values, and each raw-token trace SHA-256. Raw traces and the dataset remain outside Git. To rerun the seven additions from the repository root, use the image list below and set the report output path to a new external file. The checked-in analysis was assembled from two captures (the six-image batch and `211201486-39.png`); the separate capture JSONs are listed by name and SHA in the machine report.

```powershell
$env:DEPLOYSHARP_PADDLE_REAL_FORMULA = '1'
$env:DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT = 'F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public'
$env:DEPLOYSHARP_PADDLE_REAL_FORMULA_MODELS = 'pp-formulanet-l'
$env:DEPLOYSHARP_PADDLE_REAL_FORMULA_IMAGES = '220408168-60.png,210807632-266.png,230101132-49.png,211205994-13.png,220406062-108.png,211201486-39.png,230101946-47.png'
$env:DEPLOYSHARP_PADDLE_REAL_FORMULA_TRACE_TOKENS = '1'
$env:DEPLOYSHARP_PADDLE_REAL_FORMULA_REPORT_PATH = 'E:\Model\PaddleDocument\validation\formula-formulanet-l-eos-token-trace-expanded-repro-20261007.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~SixFormulaModelsEvaluateRealFormulaDatasetOnOrtCpu'
```

Then run `Analyze-FormulaTokenDiversity.py` on the two initial trace reports plus the new repro report, writing output outside the repository or to a new sibling JSON. This reproduces the same eleven token traces, while the capture-file SHA entries differ from the checked-in report because the seven additions are consolidated into one new test report. See the first report for the complete analysis-script invocation.

**Boundary:** one FormulaNet-L ONNX export, seven purposively selected high-output additions (eleven traces total), and ORT CPU on one device. Training overlap is unknown. This does not estimate held-out accuracy, establish the reason for the original missing EOS, validate other backends, or define a production degeneration detector.
