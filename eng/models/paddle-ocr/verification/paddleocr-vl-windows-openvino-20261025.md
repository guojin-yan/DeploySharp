# PaddleOCR-VL-1.6 Windows OpenVINO CPU verification

Date: 2026-10-25

The production C# `PaddleOcrVlOnnxSession` and `PaddleOcrVlImageProcessor` completed the fixed PaddleOCR-VL-1.6 four-graph Bundle on Windows 10.0.26200 with OpenVINO Runtime `2026.2.1`, .NET `10.0.12`, 16 logical processors, four inference threads, one stream and one request. The four graph files, the pinned checkpoint revision and the managed RGB fixture manifest were checked by SHA-256 before generation.

All six real task fixtures reached EOS and matched the pinned official reference Token IDs and decoded text:

| Task | Generated tokens | Prompt tokens | Elapsed observation |
| --- | ---: | ---: | ---: |
| OCR | 162 | 337 | 27,982.701 ms |
| Table | 71 | 163 | 9,204.415 ms |
| Formula | 80 | 173 | 9,295.832 ms |
| Chart | 137 | 453 | 26,246.844 ms |
| Spotting | 286 | 1,309 | 111,908.035 ms |
| Seal | 29 | 543 | 18,785.950 ms |

The machine-readable summary is [`paddleocr-vl-windows-openvino-20261025.json`](paddleocr-vl-windows-openvino-20261025.json). The complete external run JSON remains at `E:/DeploySharpEvidence/paddleocr-vl-windows-openvino-20261025.json` with SHA-256 `12ea9b0129dacfbfd0a5c76b2c15c10a0534957da1db40ac5c96c4dad966ea00`.

## Identity and protocol

| Item | Value |
| --- | --- |
| Checkpoint | `PaddlePaddle/PaddleOCR-VL-1.6` / `c5630abae1d940eafe0697512a0325494b02ab42` |
| Graphs | Original fixed `vision.onnx`, `embedding.onnx`, `decoder.onnx`, `head.onnx` |
| OS / framework | Windows 10.0.26200 x64 / .NET 10.0.12 |
| CPU | `AMD64 Family 25 Model 80 Stepping 0`, 16 logical processors |
| Backend | OpenVINO CPU, Runtime `2026.2.1-21919-ede283a88e3-releases/2026/2` |
| Configuration | `PERFORMANCE_HINT=LATENCY`, 4 inference threads, 1 stream, 1 request, one Session per graph |
| Precision | Float32 graph ports with native-default OpenVINO computation; no strict internal-FP32 claim |
| Generation | Bounded greedy generation, maximum 512 new tokens, one sequential request per task, 15-minute timeout |
| Reference SHA | `aaa5ace39098c307e27107562045994aabdd952fcf8dc1cb388be87e6f58eb79` |
| Fixture manifest SHA | `ff6ffe322ac3a1373ce830ffc953e18173115b654f265591c0dd214c701db481` |

The four graph SHA-256 values are the same as the Linux ORT/OpenVINO and Windows ORT correctness records:

| Graph | SHA-256 |
| --- | --- |
| `vision.onnx` | `940600685c0b69453bbfb28371fee5c89782c2ea47fb0ab6f52876ad9f46dd4f` |
| `embedding.onnx` | `1d268b40696b0f1ebf8ee67f6706a9e2feb3b64b71484d6fa87ec624ff734d86` |
| `decoder.onnx` | `823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad` |
| `head.onnx` | `8663d1fd3f2ca1cf2af308c76fadefcee12e65a6a1f402603a81085daa6d0148` |

The test passed `1/1` and checked the real graph ports, the six managed RGB patch hashes, one vision call per image, 36 empty/growing KV tensors, decoder query/past/present lengths, all 765 generated Token IDs, decoded text and EOS. The machine-readable record preserves the per-task call counts and elapsed observations.

## Boundary

This closes the Windows OpenVINO CPU generation-correctness gap for the fixed 1.6 four-graph Bundle. The elapsed values are one run per task and are not a formal performance result: there was no warm-up distribution, `5+50` measurement, clock lock, or labelled quality set. They include the generation work reported by the production Session, but do not establish page-level layout quality, reading-order accuracy, CER/WER, table/公式/chart/seal semantic quality, or long-running memory stability.

The result adds a Windows OpenVINO CPU `✓` for this exact Bundle only. It does not promote Windows CUDA, TensorRT or OpenCV DNN. ORT CPU remains separately verified on Windows and Linux; Linux OpenVINO remains covered by the [Linux six-task report](paddleocr-vl-openvino-linux-20261024.md). The release catalog/download boundary is independent of this runtime result.

## Reproduction

Prepare the pinned four graphs, tokenizer directory, managed RGB fixtures and the official six-task reference, then set:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT = 'E:\Model\PaddleOCR-VL\bundle\paddleocr-vl-1.6'
$env:DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT = 'E:\Model\PaddleOCR-VL\1.6\c5630abae1d940eafe0697512a0325494b02ab42'
$env:DEPLOYSHARP_PADDLEOCR_VL_PROCESSOR_FIXTURES = 'E:\DeploySharpEvidence\paddleocr-vl-processor-20261024\managed-processor-fixtures-20261024'
$env:DEPLOYSHARP_PADDLEOCR_VL_REFERENCE = 'eng\models\paddle-ocr\verification\paddleocr-vl-six-task-reference-linux-20261023.json'
$env:DEPLOYSHARP_PADDLEOCR_VL_OPENVINO_REPORT = 'E:\DeploySharpEvidence\paddleocr-vl-windows-openvino-20261025.json'
dotnet test tests\DeploySharp.Backend.OpenVINO.Tests\DeploySharp.Backend.OpenVINO.Tests.csproj `
  -c Release -f net10.0 --filter 'FullyQualifiedName~PaddleOcrVlIntegrationTests'
```

The test skips when the external paths are absent and only reports success when all six tasks reach EOS with exact Token/text parity. The source report and local image/model assets are intentionally kept outside the repository.
