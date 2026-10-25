# PaddleOCR-VL-1.6 formal Formula latency slice (2026-10-25)

This report records the repeated-latency slice for the fixed PaddleOCR-VL-1.6 four-graph Bundle on the official Formula input. It uses the production C# `PaddleOcrVlImageProcessor` and `PaddleOcrVlOnnxSession` with one prepared input for both CPU backends.

## Protocol and device

- Windows 10.0.26200 x64, .NET 10.0.12, 16 logical processors.
- Fixed checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`; graph, fixture, reference and patch hashes are retained in the JSON records.
- One Session containing one `vision`, `embedding`, `decoder` and `head` graph; four inference threads and one request/stream configuration.
- Five warm-up generations followed by 50 measured generations for the official `formula` task. The prepared RGB input is reused and model/session creation is excluded.
- Every warm-up and measured call reached EOS and matched the official 80 generated Token IDs and decoded text. The test completed `2 passed / 0 failed / 0 skipped`.
- Wall-clock time surrounds `GenerateAsync`; `Session elapsed` is the production `result.Elapsed` and excludes caller preprocessing and Session queue. Percentiles use nearest-rank over the 50 measured samples.

## Results

| Backend | Warm-up / measured | Token count | Wall P50 (ms) | Wall P95 (ms) | Session P50 (ms) | Session P95 (ms) | JSON SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 5 / 50 | 80 | 8,446.139 | 8,878.795 | 8,445.385 | 8,877.865 | `da1fd8e7d4376a2ed3e1f16c241ddd6d7ed7d408ee98da6eedc7418c919fcaa6` |
| OpenVINO CPU | 5 / 50 | 80 | 8,186.035 | 8,600.807 | 8,185.575 | 8,600.297 | `0abb49730caa7ba0a6dfd3a8b1dde5f2c472c73d3ce4d21ae0d865628871acac` |

Raw machine-readable records:

- [ONNX Runtime JSON](paddleocr-vl-formal-latency-onnxruntime-formula-20261025.json)
- [OpenVINO JSON](paddleocr-vl-formal-latency-openvino-formula-20261025.json)
- [Formal latency test](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrVlFormalLatencyIntegrationTests.cs)

## Boundary

This closes only one fixed Formula-task repeated-latency slice on one Windows CPU host. It is not a formula semantic accuracy score, rendered-equation equivalence, full VL performance matrix, backend ranking, locked-clock result, GPU/TensorRT/OpenCV result, memory-soak result or cross-device claim. OCR and Table have independent formal slices; Chart, Spotting and Seal still need their own measured rows.

## Reproduction

Set the paths to the pinned Bundle, tokenizer directory, managed RGB fixtures and reference JSON, then run:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = 'formula'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = '50'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore --no-build `
  --filter 'FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests'
```

The test is opt-in and skips when external model, fixture or reference paths are absent. These values are performance evidence only; they do not replace the independent formula quality gate.
