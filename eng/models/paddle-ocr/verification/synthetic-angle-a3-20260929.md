# OCR A3 controlled angle and crop-transform evidence (2026-09-29)

This is a controlled geometry contract, not a natural-image accuracy benchmark. The generator creates one known text line and rotates the source canvas by six exact angles (`0`, `12`, `-12`, `27`, `-27`, `180` degrees CCW). The manifest stores the expected text, source polygon, angle and image SHA-256 for every image.

## Protocol

- Generator: `eng/models/paddle-ocr/scripts/Generate-SyntheticAngleOcrCase.py`.
- Root: `artifacts/synthetic-ocr-angle-20260929` (local generated asset; not committed).
- Model: PP-OCRv6 Small detector/recognizer; recognizer dictionary SHA-256 `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.
- Backends: ONNX Runtime CPU and OpenVINO CPU.
- Each annotated polygon was sent through `RecognizeOnlyAsync` twice: default `Perspective` and `AffineWhenEquivalent`.
- `AffineWhenEquivalent` was required to report `cropTransform=Affine`; the default path was required to report `cropTransform=Perspective`.
- Source polygon round-trip error was asserted at `0`; input tensor parity was measured between the two crop modes.
- The generated text is deliberately small and rendered with a local system font. Recognition CER below is diagnostic of this synthetic/model setup and must not be advertised as model accuracy.

## Results

Both backend cases passed (`2/2` tests, `12` angle × transform records per backend). For every angle:

- Perspective and affine produced the same recognized text and the same CER/WER.
- Source polygon provenance was retained exactly (`polygonRoundTripMaxAbs=0`).
- Affine mode hit the affine fast path for all six rotated rectangles.
- For `0` and `180` degrees the prepared tensors were byte-identical (`max abs diff=0`). For the other angles, the maximum Float32 tensor difference was `0.0078432` (one 2/255-like normalized interpolation step), within the explicit `0.02` interpolation tolerance. This is bounded numerical drift, not pixel equality.
- ORT and OpenVINO generated the same per-angle text SHA and the same CER/WER. The prepared input tensor SHA is also equal across backends for the same transform.

The generated sample's CER ranged from `63.33%` to `77.22%` because the small synthetic render is not a representative PaddleOCR quality set. Those values are retained to expose behavior and must not be interpreted as a release quality score.

## Reproduction

```powershell
& 'E:\Model\PaddleDocument\paddle3\python.exe' `
  .\eng\models\paddle-ocr\scripts\Generate-SyntheticAngleOcrCase.py `
  --output-root artifacts/synthetic-ocr-angle-20260929 `
  --characters 180 --angles '0,12,-12,27,-27,180'

$env:DEPLOYSHARP_PADDLEOCR_SYNTHETIC_ANGLE = '1'
$env:DEPLOYSHARP_PADDLEOCR_ANGLE_ROOT = (Resolve-Path 'artifacts/synthetic-ocr-angle-20260929').Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSyntheticAngleIntegrationTests `
  --logger 'console;verbosity=normal'
```

The test writes `paddleocr-angle-{backend}.json` with model/dictionary/image SHA, angle, transform contract, tensor SHA, bounded tensor drift, polygon round-trip error, text and CER/WER. A3 remains incomplete until a legally usable natural/annotated angle set is evaluated; the controlled case only proves geometry, transform selection, provenance and cross-backend execution contracts.
