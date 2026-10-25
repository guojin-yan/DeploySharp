# PaddleOCR-VL-1.6 formal Table latency slice (2026-10-25)

This report records the first formal repeated-latency slice for the fixed PaddleOCR-VL-1.6 four-graph Bundle. It uses the production C# `PaddleOcrVlImageProcessor` and `PaddleOcrVlOnnxSession` on the same prepared Table input for both CPU backends.

## Protocol and device

- Windows 10.0.26200 x64, .NET 10.0.12, 16 logical processors.
- Fixed checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`, four graph SHA values and managed RGB fixture SHA values are retained in the JSON records.
- One Session containing one `vision`, `embedding`, `decoder` and `head` graph; four inference threads; one request/stream configuration.
- Five warm-up generations followed by 50 measured generations for the official `table` task. The prepared RGB input is reused and model/session creation is excluded.
- Every warm-up and measured call reached EOS and matched the official 71 generated Token IDs and decoded text. The test completed `2 passed / 0 failed / 0 skipped` (one test row per backend).
- Wall-clock time surrounds `GenerateAsync`; `Session elapsed` is the production `result.Elapsed` and excludes caller preprocessing and Session queue. Percentiles use nearest-rank over the 50 measured samples.

## Results

| Backend | Warm-up / measured | Token count | Wall P50 (ms) | Wall P95 (ms) | Session P50 (ms) | Session P95 (ms) | JSON SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 5 / 50 | 71 | 7,268.069 | 7,709.583 | 7,266.868 | 7,708.445 | `3f3ba6f3eb11edd2a4829cdec351b5b05cba74e1dd8a8a871dc0430b4aeb790c` |
| OpenVINO CPU | 5 / 50 | 71 | 6,787.859 | 7,318.409 | 6,787.332 | 7,317.973 | `220cb71acc87c5956942ae7a6195a9e24b3ff96bd06182eb51901e73646028a8` |

Raw machine-readable records:

- [ONNX Runtime JSON](paddleocr-vl-formal-latency-onnxruntime-table-20261025.json)
- [OpenVINO JSON](paddleocr-vl-formal-latency-openvino-table-20261025.json)
- [Formal latency test](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrVlFormalLatencyIntegrationTests.cs)

## Boundary

This closes only one fixed Table-task repeated-latency slice on one Windows CPU host. It is not a full VL performance matrix, a backend ranking, a locked-clock result, a GPU/TensorRT/OpenCV result, a labelled quality score, a memory-soak result or a cross-device claim. OCR, Formula, Chart, Spotting and Seal need their own measured rows; Spotting is deliberately excluded from this first slice because its single generation is much longer and has a documented GPU/native-memory boundary. The single-run correctness reports remain the source for the other task/backend combinations.

## Reproduction

Set the paths to the pinned Bundle, tokenizer directory, managed RGB fixtures and reference JSON, then run:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = 'table'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = '50'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_OUTPUT = 'E:\DeploySharpEvidence\paddleocr-vl-formal-latency-20261025'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests'
```

The test is opt-in and skips when the external model/fixture paths are absent. Do not compare these values with the earlier one-shot correctness observations as if they used the same protocol.
