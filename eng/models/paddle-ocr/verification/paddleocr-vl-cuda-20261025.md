# PaddleOCR-VL-1.6 Windows ONNX Runtime CUDA verification

Date: 2026-10-25

The production C# `PaddleOcrVlOnnxSession` and managed RGB processor were run with ONNX Runtime `CUDAExecutionProvider` on Windows 10.0.26200. The host was an NVIDIA GeForce RTX 3060 Laptop GPU with driver 576.02, CUDA 12.9, ONNX Runtime 1.23.2, .NET 10.0.12 and 16 logical processors. The fixed official 1.6 revision and all four graph SHA-256 values were checked before generation.

All six supported task routes reached EOS and matched the pinned official Token IDs and decoded text:

| Task | Tokens | Vision calls | Decoder/head calls | Elapsed observation |
| --- | ---: | ---: | ---: | ---: |
| OCR | 162 | 1 | 162 / 162 | 5,306.8521 ms |
| Table | 71 | 1 | 71 / 71 | 1,926.5893 ms |
| Formula | 80 | 1 | 80 / 80 | 2,219.4399 ms |
| Chart | 137 | 1 | 137 / 137 | 5,862.8289 ms |
| Spotting | 286 | 1 | 286 / 286 | 114,189.9652 ms |
| Seal | 29 | 1 | 29 / 29 | 3,721.6015 ms |

The machine-readable summary is [`paddleocr-vl-cuda-20261025.json`](paddleocr-vl-cuda-20261025.json). The complete external run is retained at `E:/DeploySharpEvidence/paddleocr-vl-cuda-20261025/paddleocr-vl-cuda-generation.json`; its SHA-256 is `f4c62342caec7afde1521a8d2b9ec8102a6243f44f4b4645f53768a5a7df4571`.

This closes the fixed Bundle's six-task **ONNX Runtime CUDA generation-correctness** gap on this Windows host. It does not change the `TensorRT CUDA` column in the model matrix: TensorRT requires its own four-graph Engine and real-model evidence. The run also does not claim a fully device-resident CUDA pipeline because KV and logits are copied through CPU-owned intermediate tensors on every decoder step. The timings are single-run observations with unlocked clocks, not P50/P95, throughput or a cross-device ranking. Labelled quality, page-level accuracy, OpenCV DNN, TensorRT, formal multi-round GPU performance and long-run memory stability remain separate gates.
