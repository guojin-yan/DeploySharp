# PaddleOCR-VL-1.6 formal Chart latency slice (2026-10-25)

This report records the repeated-latency slice for the fixed PaddleOCR-VL-1.6 four-graph Bundle on the official Chart input. It uses the production C# `PaddleOcrVlImageProcessor` and `PaddleOcrVlOnnxSession` with one prepared input for both CPU backends.

## Protocol and device

- Windows 10.0.26200 x64, .NET 10.0.12, 16 logical processors.
- Fixed checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`; graph, fixture, reference and patch hashes are retained in the JSON records.
- One Session containing one `vision`, `embedding`, `decoder` and `head` graph; four inference threads and one request/stream configuration.
- Five warm-up generations followed by 50 measured generations for the official `chart` task. The prepared RGB input is reused and model/session creation is excluded.
- Every warm-up and measured call reached EOS and matched the official 137 generated Token IDs and decoded text. The test completed `2 passed / 0 failed / 0 skipped`.
- Wall-clock time surrounds `GenerateAsync`; `Session elapsed` is the production `result.Elapsed` and excludes caller preprocessing and Session queue. Percentiles use nearest-rank over the 50 measured samples.

## Results

| Backend | Warm-up / measured | Token count | Wall P50 (ms) | Wall P95 (ms) | Session P50 (ms) | Session P95 (ms) | JSON SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 5 / 50 | 137 | 22,372.818 | 24,391.971 | 22,370.785 | 24,390.048 | `d192b869514a2735f113658c579a8d3b48dc0a25f7abc73440e66ade0a67a01b` |
| OpenVINO CPU | 5 / 50 | 137 | 22,076.120 | 25,688.664 | 22,074.872 | 25,687.257 | `7e4b026f43e76404c6bd926b503fe2b488bd1829320fcdbc9c342cde781cc8f5` |

Raw machine-readable records:

- [ONNX Runtime JSON](paddleocr-vl-formal-latency-onnxruntime-chart-20261025.json)
- [OpenVINO JSON](paddleocr-vl-formal-latency-openvino-chart-20261025.json)
- [Formal latency test](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrVlFormalLatencyIntegrationTests.cs)

## Boundary

This closes only one fixed Chart-task repeated-latency slice on one Windows CPU host. It is not ChartQA split accuracy, chart semantic correctness, a full VL performance matrix, a backend ranking, a locked-clock result, a GPU/TensorRT/OpenCV result, a memory-soak result or a cross-device claim. OCR, Table and Formula have independent formal slices; Spotting and Seal still need their own measured rows.

## Reproduction

Set the paths to the pinned Bundle, tokenizer directory, managed RGB fixtures and reference JSON, then run:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = 'chart'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = '50'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore --no-build `
  --filter 'FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests'
```

The test is opt-in and skips when external model, fixture or reference paths are absent. These values are performance evidence only; they do not replace the independent ChartQA or chart-structure quality gates.
