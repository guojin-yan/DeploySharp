# SROIE known-angle orientation retry evidence (A3) (2026-10-14)

This report measures DeploySharp's opt-in confidence-based orientation retry policy on real, source-linked SROIE word crops with controlled known rotations. It is stronger than a rendered glyph sample because the source crop, parent page and transcription are retained, but it is **not** a natural rotation-distribution accuracy benchmark.

## Protocol

- Source: 4 distinct SROIE receipt pages from `F:\OCRBenchmarkTesting`.
- Input manifest SHA-256: `5e8c22b5a87911a0e940d10c614408f1a94fe6cbf16a098995c0c618ce6d53bb`.
- 32 generated images: each source crop at `0`, `12`, `-12`, `27`, `-27`, `90`, `-90` and `180` degrees CCW. The generated image bytes remain outside the repository.
- Detector: PP-OCRv6 Small ONNX, SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`.
- Recognizer: PP-OCRv6 Small ONNX, SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Dictionary SHA-256: `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- Policy: `confidenceThreshold=1`, `minimumConfidenceGain=0`, relative retries in the order clockwise `90°`, `180°`, counter-clockwise `90°`, maximum 2 regions and 32 additional crops per image.
- Backends: ONNX Runtime CPU and OpenVINO CPU.

The initial orientation is always `0°`. For the exact right-angle rows, the expected correction is `Clockwise90` for a `+90°` source rotation, `CounterClockwise90` for `-90°`, `Degrees180` for `180°`, and `Degrees0` for `0°`. The `±12°` and `±27°` rows are deliberately excluded from orientation-selection accuracy because the API only retries right angles.

## Results

| Backend | Rows | Non-empty | Exact text | Mean CER | Mean WER | Retry executed | Right-angle expected | Right-angle selected correctly |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| ONNX Runtime CPU | 32/32 | 32/32 | 16/32 | 35.0962% | 43.7500% | 32/32 | 16 | 16/16 (100%) |
| OpenVINO CPU | 32/32 | 32/32 | 16/32 | 35.0962% | 43.7500% | 32/32 | 16 | 16/16 (100%) |

Both backends selected the same orientation for every row. Selected orientation counts were `Degrees0=13`, `Clockwise90=7`, `CounterClockwise90=8`, `Degrees180=4`. The overall CER/WER includes the non-right-angle rows and is therefore a controlled-policy diagnostic, not a model-quality score.

## Interpretation and boundary

This closes the missing **controlled retry-policy execution** slice: the public API runs all configured candidates, retains attempt metadata, and chooses the expected correction on all 16 exact right-angle cases for both CPU backends. It does not close A3. The source transformations are known, the angle distribution is artificial, and the `confidenceThreshold=1`/zero-gain policy is intentionally diagnostic rather than a production default. Natural tilted/perspective text, automatic 0/180 classification quality, rejection/repair rates and business-level CER/WER still require a legally usable annotated angle set.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_RETRY = '1'
$env:DEPLOYSHARP_PADDLEOCR_SROIE_ANGLE_ROOT = (Resolve-Path 'artifacts\synthetic-sroie-angle-a3-20260929').Path
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSroieAngleOrientationRetryIntegrationTests `
  --logger 'console;verbosity=normal'
```

The test is `PaddleOcrSroieAngleOrientationRetryIntegrationTests.cs`. It writes one JSON report per backend containing every row, source image SHA, selected attempt and policy metadata. Generated images, labels and raw test output are intentionally not copied into this repository or a model Release.
