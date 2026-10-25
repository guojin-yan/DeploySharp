# PaddleOCR-VL-1.6 bounded Spotting latency slice (2026-10-25)

This report records a bounded repeated-latency slice for the fixed PaddleOCR-VL-1.6 four-graph Bundle and the official `spotting-demo-1` input. Spotting generates a much longer sequence than the other VL tasks, so this result uses `1 warm-up + 5 measured` generations and is kept separate from the standard `5 warm-up + 50 measured` rows.

## Protocol and device

- Windows 10.0.26200 x64, .NET 10.0.12, 16 logical processors, AMD64 Family 25 Model 80 Stepping 0.
- Source revision `1136083`; fixed PaddleOCR-VL-1.6 checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`.
- One Session containing `vision`, `embedding`, `decoder` and `head`; four inference threads, one request/stream configuration.
- The managed RGB input and Patch tensor are prepared once and reused. Model and Session creation are excluded from measured time.
- Each warm-up and measured generation reached EOS and produced the official 286 Token IDs/text. Both backends completed `1 warm-up + 5 measured` with no failures or skips.
- Wall-clock time surrounds `GenerateAsync`; Session time is the production result elapsed value. Percentiles use nearest-rank over the five measured samples.

## Results

| Backend | Warm-up / measured | Token count | Wall P50 (ms) | Wall P95 (ms) | Session P50 (ms) | Session P95 (ms) | Raw JSON |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | 1 / 5 | 286 | 132,459.805 | 134,999.906 | 132,422.615 | 134,963.577 | [JSON](paddleocr-vl-formal-latency-onnxruntime-spotting-20261025.json) |
| OpenVINO CPU | 1 / 5 | 286 | 106,346.339 | 112,527.683 | 106,319.102 | 112,468.801 | [JSON](paddleocr-vl-formal-latency-openvino-spotting-20261025.json) |

## Boundary

This is a fixed single-image CPU latency observation with token/text consistency. It is not a Spotting localization or recognition accuracy score, a page-level quality result, a full `5+50` matrix, a backend ranking, a locked-clock result, a GPU/CUDA/TensorRT/OpenCV DNN result, a memory-soak result or a cross-device claim. The long sequence makes the measured cost substantially higher than Table/OCR/Formula/Chart/Seal. The existing RTX2060 CUDA experiment still fails Spotting workspace allocation and remains a partial backend experiment.

## Reproduction

Set the Bundle, tokenizer, managed RGB fixture and six-task reference paths, then run the opt-in test with the bounded counts:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = 'spotting'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = '5'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore --no-build `
  --filter 'FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests'
```

The test also verifies the official reference Token IDs and decoded text on every sample. These values are a bounded evidence slice and should not be used to infer the latency of another input, sequence limit or backend configuration.
