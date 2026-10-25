# PaddleOCR-VL-1.6 formal OCR latency slice (2026-10-25)

This report records the repeated-latency slice for the fixed PaddleOCR-VL-1.6 four-graph Bundle on the official OCR input. It uses the production C# `PaddleOcrVlImageProcessor` and `PaddleOcrVlOnnxSession` with the same prepared input for both CPU backends.

## Protocol and device

- Windows 10.0.26200 x64, .NET 10.0.12, 16 logical processors.
- Fixed checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`; graph, fixture, reference and patch hashes are retained in the JSON records.
- One Session containing one `vision`, `embedding`, `decoder` and `head` graph; four inference threads and one request/stream configuration.
- Five warm-up generations followed by 50 measured generations for the official `ocr` task. The prepared RGB input is reused and model/session creation is excluded.
- Every warm-up and measured call reached EOS and matched the official 162 generated Token IDs and decoded text. The test completed `2 passed / 0 failed / 0 skipped`.
- Wall-clock time surrounds `GenerateAsync`; `Session elapsed` is the production `result.Elapsed` and excludes caller preprocessing and Session queue. Percentiles use nearest-rank over the 50 measured samples.

## Results

| Backend | Warm-up / measured | Token count | Wall P50 (ms) | Wall P95 (ms) | Session P50 (ms) | Session P95 (ms) | JSON SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 5 / 50 | 162 | 19,163.912 | 22,211.472 | 19,157.991 | 22,209.748 | `9cd1b1161b0a1ac37807419efea2508cd58d29d2a0db03f9cb6e747600d54a56` |
| OpenVINO CPU | 5 / 50 | 162 | 20,744.661 | 23,868.052 | 20,743.887 | 23,866.677 | `b74d5f83b68f366308c7377cf8c44ee7af84029dba6e3d7e717884dd1abf81df` |

Raw machine-readable records:

- [ONNX Runtime JSON](paddleocr-vl-formal-latency-onnxruntime-ocr-20261025.json)
- [OpenVINO JSON](paddleocr-vl-formal-latency-openvino-ocr-20261025.json)
- [Formal latency test](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrVlFormalLatencyIntegrationTests.cs)

## Boundary

This closes only one fixed OCR-task repeated-latency slice on one Windows CPU host. It is not a full VL performance matrix, a backend ranking, a locked-clock result, a GPU/TensorRT/OpenCV result, a labelled quality score, a memory-soak result or a cross-device claim. Table has an independent formal slice; Formula, Chart, Spotting and Seal still need their own measured rows. The OCR row is also not a page-level OCR accuracy or end-to-end PP-Structure result.

## Reproduction

Set the paths to the pinned Bundle, tokenizer directory, managed RGB fixtures and reference JSON, then run:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = 'ocr'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = '50'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore --no-build `
  --filter 'FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests'
```

The test is opt-in and skips when external model, fixture or reference paths are absent. These values should not be compared with one-shot correctness observations or with the Table task as if they used the same generated-token count.
