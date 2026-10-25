# PaddleOCR-VL-1.6 ORT CUDA formal Spotting latency

Date: 2026-10-25  
Host: Windows 10.0.26200, NVIDIA GeForce RTX 3060 Laptop GPU (the same host described by [the six-task CUDA correctness report](paddleocr-vl-cuda-20261025.md))  
Backend: ONNX Runtime `CUDAExecutionProvider`  
Runtime: ONNX Runtime 1.23.2, .NET 10.0.12, 16 logical processors  
Source commit: `321fb14`

This is the bounded repeated-latency slice for the fixed PaddleOCR-VL 1.6 Spotting route. Spotting generates a much longer sequence than the other tasks, so the protocol uses one warm-up followed by five measured `GenerateAsync` calls instead of the standard five warm-ups plus 50 measurements. The prepared RGB input and session are reused; model/session construction and caller preprocessing are excluded from the measured interval. Every call reached EOS and matched the pinned 286-token reference exactly.

| Metric | Result |
| --- | ---: |
| Wall-clock P50 | 178,731.687 ms |
| Wall-clock P95 | 212,603.536 ms |
| Session P50 | 178,644.882 ms |
| Session P95 | 212,526.093 ms |
| Generated tokens | 286 |
| Warm-ups / measurements | 1 / 5 |

The complete machine-readable record is [`paddleocr-vl-formal-latency-onnxruntime-cuda-spotting-20261025.json`](paddleocr-vl-formal-latency-onnxruntime-cuda-spotting-20261025.json). The run used one session, four inference threads and one request/stream configuration. GPU clocks were not locked. The run retains the ONNX Runtime CUDA graph memcpy, CPU-assigned shape and CPU-owned KV/logits boundaries documented by the six-task report; this is not a device-resident CUDA pipeline benchmark and should not be compared directly with TensorRT or a GPU-only kernel benchmark.

This closes only the fixed Spotting-task ORT CUDA latency slice. It does not establish Spotting localization or recognition accuracy, TensorRT/OpenCV DNN support, labelled quality, throughput, cross-device ranking, or long-run memory stability. The long sequence makes this cost substantially higher than the other PaddleOCR-VL tasks, and the bounded five-sample percentile should not be generalized to another input or sequence limit.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = "1"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_CUDA_RUN = "1"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_BACKEND = "onnxruntime-cuda"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = "spotting"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = "1"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = "5"
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore -p:DeploySharpFormulaOrtCuda=true `
  --filter "FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests"
```

The test verifies the official reference Token IDs and decoded text on every warm-up and measured sample. These values are a bounded evidence slice and should not be used to infer the latency of another image, sequence length or backend configuration.
