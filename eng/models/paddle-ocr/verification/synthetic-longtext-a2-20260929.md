# Controlled 3,600-character SlidingWindow contract (2026-09-29)

This is a generated, self-labeled contract sample for the OCR A2 boundary. It is not a public dataset and is not a recognition-accuracy claim for natural images. The generator and the PNG/JSONL output are ignored local artifacts under `artifacts/synthetic-ocr-a2-20260929`.

## Input and protocol

- Generator: `eng/models/paddle-ocr/scripts/Generate-SyntheticLongTextCase.py`.
- Text length: 3,600 characters.
- Image: `37309x60` PNG, SHA-256 `d3f31481c2ed4859ad9e3e0735212a591977352028adbe348f23232ced69760a`.
- Model: PP-OCRv6 Small recognizer, SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Pipeline path: `OcrPipeline.RecognizeOnlyAsync` with `RecognitionOverflowMode.SlidingWindow`.
- Window overlap: `0.2`; maximum windows per region: `256`; maximum windows per image: `512`; maximum merged characters: `8192`; maximum merged timesteps: `65536`.

## Results

| Backend | Windows | Natural width | Window target/tensor width | Recognized chars | CER | WER | Recognized text SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 18 | 44,723 | 3,168 / 3,168 | 3,911 | 8.6389% | 8.8838% | `5664de035075f8f595b669e1c350923574de9408aff32c03dafccf9078bede88` |
| OpenVINO CPU | 18 | 44,723 | 3,168 / 3,168 | 3,911 | 8.6389% | 8.8838% | `5664de035075f8f595b669e1c350923574de9408aff32c03dafccf9078bede88` |

Both backends produced byte-identical recognized text. The nonzero CER/WER is evidence that the current conservative seam matcher still leaves repeated or altered characters on this synthetic boundary case; SlidingWindow avoids silent width clamping but does not guarantee perfect seam reconstruction. The result proves that `RecognizeOnlyAsync` now uses the same bounded window planner and merger path as full-page OCR.

The existing unit tests cover window budget rejection and merged-timestep/character limits. A future A2 completion still requires real or attributable >3200-character data, source/token mapping evidence, repeated-boundary cases and a decision on seam improvements.

## Reproduction

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-SyntheticLongTextCase.py `
  --output-root artifacts/synthetic-ocr-a2-20260929 --characters 3600

$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_LONGTEXT = '1'
$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_ROOT = (Resolve-Path 'artifacts/synthetic-ocr-a2-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSyntheticLongTextIntegrationTests
```
