# PaddleOCR / PP-Structure regression and contract evidence (2026-10-08)

This report records a focused regression on Windows 11 build `26200`, .NET `10.0.401`, Release `net10.0`, using the `d8efcba` source revision with a pre-existing dirty worktree. It is not a clean-checkout release gate; unrelated local changes were intentionally not staged.

## Contract and regression results

| Project / scope | Passed | Skipped | Failed | Total |
| --- | ---: | ---: | ---: | ---: |
| Core `GenericBatchScheduler` contracts | 2 | 0 | 0 | 2 |
| Visual `RunPrefetchedAsync` + concurrent multi-page contracts | 4 | 0 | 0 | 4 |
| Visual non-external regression | 498 | 5 | 0 | 503 |
| Visual.OpenCV non-external regression | 99 | 4 | 0 | 103 |
| Attributable HierText long-text ORT/OpenVINO | 2 | 0 | 0 | 2 |
| Natural vertical-line ORT/OpenVINO candidates | 2 | 0 | 0 | 2 |

The six selected contract/regression scopes therefore completed with `607 passed`, `9 skipped` and `0 failed` across `616` test cases. The two long-text and two vertical-line rows are external-model runs, not part of the non-external gate.

## Long-text boundary

PP-OCRv6 Small recognition ran on the existing attributable composition made from 102 real HierText line crops across 18 source pages. ORT CPU and OpenVINO CPU both processed 3,603 expected characters in 32 bounded windows, preserved the per-window source mapping and produced text SHA-256 `7880bb2ce0ee3e58ccea551c0502169a353528efecbf29e58143f1e17a08a463`. Both measured CER/WER were `19.789%/50.72%`.

This is useful evidence for real glyphs, budgeted SlidingWindow execution and source provenance. It is not a natural continuous 3,603-character line, and its non-zero error prevents treating it as an accuracy pass. The natural `>=3200`-character gate remains open.

## Vertical-line boundary

The existing HierText selection contains 18 non-empty `vertical=true` lines. For each crop, the test runs explicit 0°, clockwise 90° and counter-clockwise 90° candidates on ORT/OpenVINO and selects the lowest-CER candidate only as an oracle diagnostic. The two backends agree on all 18 rows; the oracle orientation counts are `7/10/1` and mean case-folded CER is `46.3356%`.

This validates candidate execution and cross-backend consistency. It does not implement or prove automatic angle classification, and the A3 quality gate remains open.

## Reproduction

```powershell
dotnet test tests/DeploySharp.Core.Tests/DeploySharp.Core.Tests.csproj -c Release --no-restore `
  --filter "FullyQualifiedName~GenericBatchScheduler"
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -c Release --no-restore `
  --filter "TestCategory!=ExternalModels"
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release --no-restore `
  --filter "TestCategory!=ExternalModels"

$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LONGTEXT = '1'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LONGTEXT_ROOT = (Resolve-Path 'artifacts/hiertext-composed-long-a2-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release --no-restore `
  --filter "FullyQualifiedName~PaddleOcrHierTextComposedLongIntegrationTests"

$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_VERTICAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release --no-restore `
  --filter "FullyQualifiedName~PaddleOcrHierTextVerticalIntegrationTests"
```

The machine-readable counterpart is [`paddleocr-ppstructure-regression-20261008.json`](paddleocr-ppstructure-regression-20261008.json). External model gates remain explicit; a skipped test is not evidence of support.
