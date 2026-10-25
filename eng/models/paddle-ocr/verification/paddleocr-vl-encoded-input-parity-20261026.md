# PaddleOCR-VL 1.6 encoded-image input parity

The production `OpenCvPaddleOcrVlInputFactory` was compared with the pinned official Pillow processor on five encoded images used by six task fixtures. The source JPEG/PNG bytes, official RGB/float32 patch fixture manifest, and both production assemblies are identified by SHA-256 in the [machine report](paddleocr-vl-encoded-input-parity-20261026.json). The six inputs contain 6,818,448 patch values in total.

| Task fixture | Format | Patch values | Raw EXIF mode: differing values | Default page mode: differing values |
| --- | --- | ---: | ---: | ---: |
| OCR `ocr-demo-1` | JPEG | 762,048 | 0 | 584,344 |
| Table | JPEG | 352,800 | 0 | 0 |
| Formula | PNG | 376,320 | 0 | 0 |
| Chart | PNG | 1,034,880 | 0 | 0 |
| Spotting `spotting-demo-1` | same JPEG as OCR | 3,048,192 | 0 | 2,372,200 |
| Seal | PNG | 1,244,208 | 0 | 0 |

The OCR/Spotting JPEG has EXIF Orientation `3` (180 degrees). The generic OpenCV loader deliberately applies this orientation so displayed pages, detector coordinates and crops share one upright coordinate system. The official Pillow reference uses `Image.open(...).convert("RGB")` without EXIF transposition. That policy difference, not VL resize or channel packing, caused the default-mode mismatch. The new explicit `normalizeExifOrientation: false` option preserves encoded JPEG pixels for standalone official-processor parity. It leaves the existing default and already-decoded page/ROI ownership unchanged. Do not mix raw-source coordinates with regions obtained from an EXIF-normalized page without mapping between them.

The first run detected the two JPEG mismatches. After adding the opt-in mode, the test passed `1/1`; all six raw-mode patch hashes match the official fixture hashes exactly. The related VL input, page/region and generic EXIF regression run passed `23/23`. The report JSON SHA-256 is `93aa05a2d7aee6cc0325243fdc632426df66761a798db675ea23965bb77a2ad7`; no image, model or tensor binaries are committed.

To reproduce, place the five images named by [the input manifest](paddleocr-vl-processor-inputs-20261024.json) under an external root's `inputs/` directory, preserving their SHA-256. Prepare the official RGB and patch fixtures using [the existing exporter](../scripts/Export-PaddleOcrVlProcessorFixtures.py) and the pinned PaddleOCR-VL-1.6 processor source. Then run:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES = 'E:\DeploySharpEvidence\paddleocr-vl-processor-20261024\managed-processor-fixtures-20261024'
$env:DEPLOYSHARP_PADDLEOCR_VL_INPUT_MANIFEST = 'eng\models\paddle-ocr\verification\paddleocr-vl-processor-inputs-20261024.json'
$env:DEPLOYSHARP_PADDLEOCR_VL_ENCODED_IMAGE_ROOT = 'E:\DeploySharpEvidence\paddleocr-vl-encoded-parity-20261026'
$env:DEPLOYSHARP_PADDLEOCR_VL_ENCODED_PARITY_REPORT = 'E:\DeploySharpEvidence\paddleocr-vl-encoded-parity-20261026\encoded-parity.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --filter EncodedOfficialImagesMatchPinnedPillowPatches
```

This closes encoded-image patch parity only for these exact JPEG/PNG assets on the tested Windows/OpenCV 5.0 runtime. It does not measure image decoding performance, run VL generation from encoded files, establish arbitrary JPEG/ICC/EXIF parity, or measure labelled OCR/table/formula/document quality. OpenCV DNN inference remains separately unsupported for the original dynamic VL vision/decoder graphs.
