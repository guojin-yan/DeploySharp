# A3 real-crop known-angle evidence (2026-09-29)

This is a controlled geometric transformation of real, annotated SROIE word crops. It is stronger than a rendered synthetic glyph because the source crop, parent receipt, transcription and SHA are real and retained, but it is still not a natural rotation-distribution benchmark: the pixels are rotated by a known transform.

## Protocol

- Source: four different SROIE receipt pages selected from `F:\OCRBenchmarkTesting`.
- Source annotations: pinned SROIE distributor revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`; word-level text and crop SHA are retained.
- Generator: `eng/models/paddle-ocr/scripts/Generate-SroieAngleOcrCase.py`.
- Angles (CCW): `0`, `12`, `-12`, `27`, `-27`, `90`, `-90`, `180` degrees.
- Model: PP-OCRv6 Small recognizer; ORT CPU and OpenVINO CPU.
- Pipeline: `OcrPipeline.RecognizeOnlyAsync`, full-page source crop with known quadrilateral, no detector execution.
- Records: 4 source crops × 8 angles = 32 per backend; each row records source parent/instance, source crop SHA, transformed image SHA, expected/recognized text, case-folded CER/WER, confidence and input tensor SHA.

## Results

ORT and OpenVINO produced identical per-angle text behavior and equivalent aggregate metrics:

| Angle | Empty outputs (of 4) | Mean case-folded CER | Mean case-folded WER | Mean confidence |
| ---: | ---: | ---: | ---: | ---: |
| 0° | 0 | 0.00% | 0.00% | 0.994 |
| 12° | 0 | 23.08% | 66.67% | 0.824 |
| -12° | 0 | 23.08% | 66.67% | 0.834 |
| 27° | 1 | 100.00% | 100.00% | 0.218 |
| -27° | 2 | 98.08% | 100.00% | 0.159 |
| 90° | 4 | 100.00% | 100.00% | 0.000 |
| -90° | 4 | 100.00% | 100.00% | 0.000 |
| 180° | 0 | 32.69% | 66.67% | 0.690 |

The result is an actionable boundary: the current recognizer-only path handles the unrotated linked crops, degrades at ±12°, and does not handle 90°/270° without an explicit orientation correction. This is not a claim that the model fails on all rotated documents; it is evidence for the current crop contract and parameters. The matching ORT/OpenVINO behavior confirms this is not a backend divergence in this test.

## Reproduction

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-SroieAngleOcrCase.py `
  --dataset-root F:\OCRBenchmarkTesting `
  --degradation-manifest artifacts\synthetic-degraded-ocr-b1b-20260929\data\annotations\manifests\synthetic-degraded-ocr.jsonl `
  --output-root artifacts\synthetic-sroie-angle-a3-20260929 `
  --angles '0,12,-12,27,-27,90,-90,180'

$env:DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE = '1'
$env:DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_ROOT = (Resolve-Path 'artifacts\synthetic-sroie-angle-a3-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSroieAngleIntegrationTests
```

The controlled transform set does not complete A3's natural-image requirement. It does provide source-linked, non-rendered angle evidence for choosing orientation retry/CLS policies and for detecting backend mismatches.
