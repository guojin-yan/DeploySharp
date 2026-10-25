# PaddleOCR-VL-1.6 ORT CUDA formal Seal latency

Date: 2026-10-25
Host: Windows 10.0.26200, NVIDIA GeForce RTX 3060 Laptop GPU (the same host described by [the six-task CUDA correctness report](paddleocr-vl-cuda-20261025.md))
Backend: ONNX Runtime `CUDAExecutionProvider`
Runtime: ONNX Runtime 1.23.2, .NET 10.0.12, 16 logical processors
Source commit: `321fb14`

This is the repeated GPU latency slice for the PaddleOCR-VL 1.6 Seal route. It uses the production `PaddleOcrVlOnnxSession`, the pinned official Seal fixture and the same four-graph Bundle used by the correctness evidence. The test performs five warm-ups followed by 50 measured `GenerateAsync` calls. The prepared RGB input and session are reused; model/session construction and caller preprocessing are excluded from the measured interval. Every call reached EOS and matched the pinned 29-token reference exactly.

| Metric | Result |
| --- | ---: |
| Wall-clock P50 | 4,728.698 ms |
| Wall-clock P95 | 4,825.845 ms |
| Session P50 | 4,727.494 ms |
| Session P95 | 4,824.603 ms |
| Generated tokens | 29 |
| Warm-ups / measurements | 5 / 50 |

The complete machine-readable record is [`paddleocr-vl-formal-latency-onnxruntime-cuda-seal-20261025.json`](paddleocr-vl-formal-latency-onnxruntime-cuda-seal-20261025.json). The run used one session and four intra-op threads. GPU clocks were not locked. ONNX Runtime reported graph memcpy nodes and CPU-assigned shape operations, so this is not a device-resident CUDA pipeline benchmark and should not be compared directly with TensorRT or a GPU-only kernel benchmark.

This closes only the fixed Seal-task ORT CUDA latency slice. It does not establish TensorRT/OpenCV DNN support, labelled seal quality, page-level quality, multi-session scaling, cross-device ranking, or long-run memory stability. The remaining CUDA task routes can be measured with the same test by setting `DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK` to `ocr`, `formula`, `chart` or `spotting`.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_RUN = "1"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_CUDA_RUN = "1"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_BACKEND = "onnxruntime-cuda"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_TASK = "seal"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_WARMUPS = "5"
$env:DEPLOYSHARP_PADDLEOCR_VL_FORMAL_LATENCY_MEASUREMENTS = "50"
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore -p:DeploySharpFormulaOrtCuda=true `
  --filter "FullyQualifiedName~PaddleOcrVlFormalLatencyIntegrationTests"
```
