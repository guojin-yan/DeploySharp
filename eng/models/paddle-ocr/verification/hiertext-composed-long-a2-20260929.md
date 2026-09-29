# HierText attributable composed long-line evidence (2026-09-29)

The public/local HierText selection has no naturally annotated text line longer than 132 characters. To exercise the long-line implementation with real glyphs and source provenance, 102 real HierText text-line crops from 18 parent images were placed side-by-side with fixed gaps, producing a 3,603-character attributable composition. The composition is not a natural continuous line and is not a dataset accuracy score.

## Results

PP-OCRv6 Small recognizer-only SlidingWindow ran on ORT CPU and OpenVINO CPU:

| Backend | Source pages | Segments | Windows | Expected chars | CER | WER | Text SHA |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 18 | 102 | 32 | 3,603 | 19.789% | 50.72% | `7880bb2ce0ee3e58ccea551c0502169a353528efecbf29e58143f1e17a08a463` |
| OpenVINO CPU | 18 | 102 | 32 | 3,603 | 19.789% | 50.72% | `7880bb2ce0ee3e58ccea551c0502169a353528efecbf29e58143f1e17a08a463` |

Both backends produced byte-identical text and 32 bounded windows. Every window intersected at least one source segment; evidence records each window's source segment IDs, parent image IDs, source crop SHA, character offsets and seam diagnostics. This demonstrates real-font/real-background sliding-window execution and source mapping, while the nonzero CER/WER shows why it cannot be promoted to a long-text accuracy claim.

## Reproduction

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-HierTextComposedLongCase.py `
  --dataset-root F:\OCRBenchmarkTesting `
  --output-root artifacts\hiertext-composed-long-a2-20260929 `
  --characters 3600 --target-height 48 --gap 8

$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LONGTEXT = '1'
$env:DEPLOYSHARP_PADDLEOCR_HIERTEXT_LONGTEXT_ROOT = (Resolve-Path 'artifacts\hiertext-composed-long-a2-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrHierTextComposedLongIntegrationTests
```
