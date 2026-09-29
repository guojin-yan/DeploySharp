# Controlled 3,600-character SlidingWindow contract (2026-09-29)

This is a generated, self-labeled contract sample for the OCR A2 boundary. It is not a public dataset and is not a recognition-accuracy claim for natural images. The generator and the PNG/JSONL output are ignored local artifacts under `artifacts/synthetic-ocr-a2-20260929`.

## Input and protocol

- Generator: `eng/models/paddle-ocr/scripts/Generate-SyntheticLongTextCase.py`.
- Text length: 3,600 characters.
- Images: repeated phrase `37309x60`, SHA-256 `d3f31481c2ed4859ad9e3e0735212a591977352028adbe348f23232ced69760a`; varied vocabulary `34192x60`, SHA-256 `e5e27fbc5ae197a2ab63e7ca45a5c3770d53680a53f33cd1723d20325a39a57f`.
- Models: PP-OCRv6 Small detector SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`; recognizer SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Character dictionary SHA-256: `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- Pipeline path: `OcrPipeline.RecognizeOnlyAsync` with `RecognitionOverflowMode.SlidingWindow`.
- Window overlap: `0.2`; maximum windows per region: `256`; maximum windows per image: `512`; maximum merged characters: `8192`; maximum merged timesteps: `65536`.
- Device: Windows 11 Home (build `26200`), AMD Ryzen 7 5800H; CPU execution only.
- Runtime packages: ONNX Runtime Managed `1.28.0`, OpenVINO C# API `3.3.1`, OpenVINO Windows runtime `2026.2.1`.

## Results

| Text profile | Backend | Window count | Natural width | Window target/tensor width | Recognized chars | CER | WER | Recognized text SHA-256 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Repeated phrase | ONNX Runtime CPU | 18 | 44,723 | 3,168 / 3,168 | 3,600 | 0.0556% (2/3,600) | 0.4556% (2 word edits) | `3ea6fdf29b47f9a74312221f02d0cb5cc1fa926e9757bdaa68b8d6165e1adb2a` |
| Repeated phrase | OpenVINO CPU | 18 | 44,723 | 3,168 / 3,168 | 3,600 | 0.0556% (2/3,600) | 0.4556% (2 word edits) | `3ea6fdf29b47f9a74312221f02d0cb5cc1fa926e9757bdaa68b8d6165e1adb2a` |
| Varied vocabulary | ONNX Runtime CPU | 16 | 40,983 | 3,168 / 3,168 | 3,600 | 0.0833% (3/3,600) | 0.7194% (3 word edits) | `55be0783893f82e349ad35ffe3c4e90e88b0950598ce4013f0039637cd1f251e` |
| Varied vocabulary | OpenVINO CPU | 16 | 40,983 | 3,168 / 3,168 | 3,600 | 0.0833% (3/3,600) | 0.7194% (3 word edits) | `55be0783893f82e349ad35ffe3c4e90e88b0950598ce4013f0039637cd1f251e` |

Each profile produced byte-identical recognized text on both backends. The repeated phrase sample used six fuzzy boundaries (one with substitution distance 2 and five with distance 1); the varied vocabulary sample used three (two with distance 1, one with distance 2). Fuzzy recovery only compares equal-length, position-aligned overlap slices; it does not use insertions/deletions to align seams. All fuzzy seams are marked `SeamUncertain` and retain their raw CTC traces. The remaining character edits are within generated OCR output, so exact output length does not imply perfect transcription. Exact matches and accepted fuzzy matches suppress the right-window prefix; a seam with differing overlap lengths or outside the configured distance budget is left intact and remains uncertain.

The Visual contract suite passed 13/13 window tests, including one-edit recovery, rejection above the configured edit budget, preservation of uncertain text, Unicode tokens, and merged-token-to-window trace mapping. The real-model integration test passed all four sample/backend combinations. Result JSON files named `synthetic-a2-{backend}-{image-sha-prefix}.json` record each window's geometry, raw text, CTC timesteps, emitted token class/text/confidence, removed prefix count, uncertainty flag and edit distance. This is implementation/contract evidence only. A2 remains open until naturally occurring or otherwise attributable annotated >3,200-character images are evaluated, and results are repeated on additional supported model families.

## Reproduction

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-SyntheticLongTextCase.py `
  --output-root artifacts/synthetic-ocr-a2-20260929 --characters 3600 `
  --profile repeated-phrase

& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-SyntheticLongTextCase.py `
  --output-root artifacts/synthetic-ocr-a2-varied-20260929 --characters 3600 `
  --profile varied-words --seed 20260929

$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_LONGTEXT = '1'
$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_ROOT = (Resolve-Path 'artifacts/synthetic-ocr-a2-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSyntheticLongTextIntegrationTests

$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_ROOT = (Resolve-Path 'artifacts/synthetic-ocr-a2-varied-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSyntheticLongTextIntegrationTests
```
