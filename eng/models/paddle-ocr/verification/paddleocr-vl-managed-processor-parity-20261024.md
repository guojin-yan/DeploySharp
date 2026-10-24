# PaddleOCR-VL managed RGB processor and six-task ORT generation

The managed `PaddleOcrVlImageProcessor` now produces exactly the same patch values as the pinned official PaddleOCR-VL-1.6 processor on six real task inputs. The same production Visual assembly then completes all six tasks on Linux ONNX Runtime CPU, with all 765 generated tokens and decoded texts matching the official reference and all requests reaching EOS.

## Exact scope and evidence

- Checkpoint: `PaddlePaddle/PaddleOCR-VL-1.6`, revision `c5630abae1d940eafe0697512a0325494b02ab42`.
- Reference: the pinned upstream image-processor source, SHA-256 `a4fa521b9cb16e207f94b7f2d16427771776dfc634420d319fc4916ee58049ec`, with Pillow `12.3.0` and NumPy `2.2.6`.
- [Input manifest](paddleocr-vl-processor-inputs-20261024.json) identifies the five source images and six task uses. Images and binary tensors stay outside Git.
- [Fixture manifest](paddleocr-vl-processor-fixtures-20261024.json) records original RGB bytes, official patch shapes and SHA-256 values, generator identity and configuration identity.
- [Before/after pixel report](paddleocr-vl-managed-processor-parity-20261024.json) records all 6,818,448 float32 values, assembly identities and Windows/.NET environment.
- [Linux managed RGB generation report](paddleocr-vl-managed-rgb-csharp-ort-linux-20261024.json) records the four graph hashes, production assembly identities, source/pixel identities, every generated token, final text and explicit finish reasons. Its Visual assembly SHA matches the pixel report.

The earlier [C# ORT report](paddleocr-vl-managed-csharp-ort-linux-20261024.json) started from saved official patches. This new run starts from upstream-decoded RGB bytes: resizing, normalization and patch packing all run in C# before graph execution.

## Result

| Task / input | Source size | Patch values | Different values before repair | Different values after repair | Generated tokens | Finish |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| OCR / `ocr-demo-1` | 500×500 | 762,048 | 7 | 0 | 162 | EOS |
| Table / `table-official-example` | 551×132 | 352,800 | 9 | 0 | 71 | EOS |
| Formula / `formula-official-example` | 448×64 | 376,320 | 3 | 0 | 80 | EOS |
| Chart / `chart-official-example` | 625×570 | 1,034,880 | 14 | 0 | 137 | EOS |
| Spotting / `spotting-demo-1` | 500×500 | 3,048,192 | 223 | 0 | 286 | EOS |
| Seal / `seal-official-example` | 640×640 | 1,244,208 | 22 | 0 | 29 | EOS |

Before repair, floating-point filter accumulation and byte rounding differed from Pillow's 8-bit resampler at 278 components. Maximum normalized error was `0.0078431368` for ordinary tasks and `0.0156862736` for Spotting, which performs Lanczos followed by bicubic resizing. The managed resampler now quantizes normalized filter coefficients to 22 fractional bits and applies integer rounding/clipping after each pass, matching Pillow's byte arithmetic. The geometry, RGB order, normalization and raster patch order remain the official 1.6 contract.

After repair, every patch value is bit-exact on these inputs; the test fails on any differing float32 bit pattern. The `1e-6` diagnostic counter is also retained in the report, but is not used to relax the exact-match acceptance criterion.

## Reproduce the processor comparison

Use the model/tokenizer directory already acquired for the official reference. Copy the [input manifest](paddleocr-vl-processor-inputs-20261024.json) into an external working directory and place the five images at its relative `inputs/` paths. The exporter checks their exact SHA-256 values; it does not download or substitute other images.

With the existing [reference Python environment](../scripts/paddleocr-vl-reference.requirements.txt):

```powershell
python eng/models/paddle-ocr/scripts/Export-PaddleOcrVlProcessorFixtures.py `
  --model-root E:/Model/PaddleOCR-VL/1.6/c5630abae1d940eafe0697512a0325494b02ab42 `
  --manifest E:/DeploySharpEvidence/vl-inputs/paddleocr-vl-processor-inputs-20261024.json `
  --output-root E:/DeploySharpEvidence/vl-processor-fixtures

$env:DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES = 'E:/DeploySharpEvidence/vl-processor-fixtures'
$env:DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_REPORT = 'E:/DeploySharpEvidence/vl-processor-report.json'
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj `
  -c Release --filter PaddleOcrVlImageProcessorIntegrationTests
```

The exporter only needs the official processor/configuration files and the images; it does not load the model weights. Its raw RGB and float32 outputs stay in the external output directory. C# independently checks fixture hashes, computes the managed patches and compares all components.

## Reproduce real C# generation on Linux

Use the existing four-graph dynamic FP32 Bundle and the same official tokenizer directory. The reference JSON is the [six-task official generation baseline](paddleocr-vl-six-task-reference-linux-20261023.json).

```bash
export DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT=/path/to/dynamic-fp32-bundle
export DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT=/path/to/official-tokenizer-directory
export DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES=/path/to/vl-processor-fixtures
export DEPLOYSHARP_PADDLEOCR_VL_REFERENCE="$PWD/eng/models/paddle-ocr/verification/paddleocr-vl-six-task-reference-linux-20261023.json"
export DEPLOYSHARP_PADDLEOCR_VL_REPORT=/path/to/managed-rgb-generation.json
dotnet test tests/DeploySharp.Backend.OnnxRuntime.Tests/DeploySharp.Backend.OnnxRuntime.Tests.csproj \
  -c Release --filter SixRealTaskPromptsGenerateThroughManagedRgbProcessingAndDynamicKvSession
```

The actual validation used Ubuntu 22.04.5 LTS, .NET 10.0.9, ORT CPU `1.28.0`, four intra-op threads, one inter-op thread, and disabled CPU arena/memory pattern. The filtered integration test passed `1/1`; each task additionally requires its C# patch SHA to match the original official reference before generation.

This is a fixed-input correctness run. It does not establish OpenCV JPEG/PNG decoder parity, layout detection or crop routing, real multi-page output, labelled quality, GPU/multi-backend availability, or formal P50/P95 performance. The earlier Vision-feature floating-point difference boundary remains; matching final tokens does not imply every internal ONNX tensor is bit-exact.
