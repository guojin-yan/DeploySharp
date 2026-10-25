# PaddleOCR-VL-1.6 formal Seal latency slice (2026-10-25)

This report records the repeated-latency slice for the fixed PaddleOCR-VL-1.6 four-graph Bundle on the official Seal input. It uses the production C# `PaddleOcrVlImageProcessor` and `PaddleOcrVlOnnxSession` with one prepared input for both CPU backends.

## Protocol and device

- Windows 10.0.26200 x64, .NET 10.0.12, 16 logical processors.
- Fixed checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`; graph, fixture, reference and patch hashes are retained in the JSON records.
- One Session containing one `vision`, `embedding`, `decoder` and `head` graph; four inference threads and one request/stream configuration.
- Five warm-up generations followed by 50 measured generations for the official `seal` task. The prepared RGB input is reused and model/session creation is excluded.
- Every warm-up and measured call reached EOS and matched the official 29 generated Token IDs and decoded text. The test completed `2 passed / 0 failed / 0 skipped`.
- Wall-clock time surrounds `GenerateAsync`; `Session elapsed` is the production `result.Elapsed` and excludes caller preprocessing and Session queue. Percentiles use nearest-rank over the 50 measured samples.

## Results

| Backend | Warm-up / measured | Token count | Wall P50 (ms) | Wall P95 (ms) | Session P50 (ms) | Session P95 (ms) | JSON SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 5 / 50 | 29 | 20,141.041 | 20,739.562 | 20,138.735 | 20,736.816 | `4396f50f2078a83cbc298fdb4fe4c10fb34b210c784c4d9cb1448cabaffd0ac9` |
| OpenVINO CPU | 5 / 50 | 29 | 12,785.056 | 15,429.444 | 12,783.900 | 15,428.241 | `f1cbe7401027f2ce289f10dba72e9acb1d8166e81870c768989fc31ed6089424` |

Raw machine-readable records:

- [ONNX Runtime JSON](paddleocr-vl-formal-latency-onnxruntime-seal-20261025.json)
- [OpenVINO JSON](paddleocr-vl-formal-latency-openvino-seal-20261025.json)
- [Formal latency test](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrVlFormalLatencyIntegrationTests.cs)

## Boundary

This closes only one fixed Seal-task repeated-latency slice on one Windows CPU host. It is not seal detection/recognition accuracy, a full VL performance matrix, a backend ranking, a locked-clock result, a GPU/TensorRT/OpenCV result, a memory-soak result or a cross-device claim. OCR, Table, Formula and Chart have independent formal slices; Spotting still needs a separately bounded performance row because of its long sequence and documented GPU memory boundary.

## Reproduction

Set the paths to the pinned Bundle, tokenizer directory, managed RGB fixtures and reference JSON, then run:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = 'seal'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = '50'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore --no-build `
  --filter 'FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests'
```

The test is opt-in and skips when external model, fixture or reference paths are absent. These values are performance evidence only; they do not replace independent seal quality evaluation.
