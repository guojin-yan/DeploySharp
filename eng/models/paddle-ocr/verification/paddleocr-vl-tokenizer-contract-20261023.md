# PaddleOCR-VL-1.6 tokenizer preparation contract — 2026-10-23

The managed tokenizer matches the pinned upstream `tokenizer.json` reference for the tested text, task templates, all added markers and all model-vocabulary slots. This is token preparation and decoding evidence, not executable model-generation or ONNX-backend support.

## Exact scope

- Official checkpoint: `PaddlePaddle/PaddleOCR-VL-1.6`, revision `c5630abae1d940eafe0697512a0325494b02ab42`.
- Production code: [`PaddleOcrVlTokenizer`](../../../../src/DeploySharp.Visual/Models/PaddleOcr/VisionLanguage/PaddleOcrVlTokenizer.cs), [`PaddleOcrVlTokenSequence`](../../../../src/DeploySharp.Visual/Models/PaddleOcr/VisionLanguage/PaddleOcrVlTokenSequence.cs); no new NuGet package.
- Dependency: existing `Microsoft.ML.Tokenizers 2.0.0` on `net8.0`, `net9.0`, `net10.0`. Other library targets expose a deterministic capability-unavailable boundary for this tokenizer; scalar image planning remains available separately.
- All four tokenizer/template assets are SHA256 verified before use. Initial model and other VL revisions are not accepted as interchangeable assets.
- Python reference: actual `tokenizers 0.23.2` output from the verified official `tokenizer.json`; no managed output is used to manufacture the expected values.

## Results

| Check | Actual result |
|---|---|
| Text vectors | 18, including Chinese, formulas, leading/repeated spaces, CR/LF/tab, combining characters, emoji and structural markers |
| Task prompts | OCR, table, formula, chart, spotting and seal; expanded-token CSV SHA256 agrees for all six |
| Added tokens | All 1,041 exact IDs and encode/decode results agree; 22 special and 1,019 ordinary markers |
| Vocabulary decode | All 103,424 slots, both including and excluding special tokens; independent framed UTF-8 SHA256 agrees |
| Byte-fallback decode vectors | 71, including incomplete/invalid UTF-8, complete emoji and markers inserted inside byte runs |
| Focused managed tests | 30 passed / 0 failed / 0 skipped: 14 image-planning cases plus 16 tokenizer cases |
| Full Visual regression | 561 passed / 0 failed / 6 skipped; the six optional external-asset cases are listed in the JSON report |
| Visual Release compilation | All 16 declared TFMs succeed, 0 warnings / 0 errors with existing TFM-support notices suppressed |

The tests also check exact context-plus-output budgets, image-placeholder counts, CLS `100273` rather than BOS `1`, null/invalid IDs, missing or mismatched assets, 24 concurrent callers and independent read-only prompt ownership.

Two compatibility details are handled explicitly: Microsoft treats all registered added tokens as special, while the official assets distinguish digits and structural markers from control tokens; the upstream byte-fallback decoder also replaces every byte in an invalid byte run, which differs from the normal .NET UTF-8 replacement rule. The production decoder preserves both upstream behaviors. It does not trim whitespace or discard a final non-EOS token.

## Reproduce

Acquire the small official assets with [`Inspect-PaddleOcrVlOfficialAssets.py`](../scripts/Inspect-PaddleOcrVlOfficialAssets.py), then run the independent reference using a Python environment with `tokenizers==0.23.2`:

```powershell
python eng/models/paddle-ocr/scripts/Get-PaddleOcrVlTokenizerReference.py `
  --model-root E:/Model/PaddleOCR-VL/1.6/c5630abae1d940eafe0697512a0325494b02ab42 `
  --output eng/models/paddle-ocr/verification/paddleocr-vl-tokenizer-reference-20261023.json

$env:DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT = 'E:/Model/PaddleOCR-VL/1.6/c5630abae1d940eafe0697512a0325494b02ab42'
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj `
  --configuration Release --framework net10.0 --no-restore -p:TargetFrameworks=net10.0 `
  --filter 'FullyQualifiedName~PaddleOcrVl'

dotnet build src/DeploySharp.Visual/DeploySharp.Visual.csproj `
  --configuration Release --no-restore -p:SuppressTfmSupportBuildWarnings=true
```

Machine-readable evidence: [contract report](paddleocr-vl-tokenizer-contract-20261023.json), [independent expected output](paddleocr-vl-tokenizer-reference-20261023.json), [original official asset identities](paddleocr-vl-official-assets-20261023.json).

## Remaining boundary

These checks do not establish image interpolation/normalization/patch parity, multimodal positions, weight generation, dynamic KV export, C# generation, quality, performance, layout/ROI/multi-page behavior or any backend availability. Those remain separate acceptance items.
