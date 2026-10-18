# UVDoc TensorRT dynamic Batch=2 benchmark (2026-10-18)

This report adds a formal repeated-latency result to the existing UVDoc TensorRT dynamic-batch and ORT parity evidence. It uses the exact official `paddle-doc/uvdoc` ONNX artifact, a TensorRT engine built by DeploySharp, and the full Paddle unwarping decoder.

## Device and runtime

| Item | Value |
| --- | --- |
| Host | `JYPPX`, Windows NT `10.0.26200.0`, x64, .NET `10.0.12` |
| CPU | AMD Ryzen 7 5800H, 8 cores / 16 logical processors |
| GPU | NVIDIA GeForce RTX 3060 Laptop GPU, 6 GiB, driver `576.02` |
| TensorRT | `11.0.0.114-cu12`, DeploySharp TensorRT API 11 bridge |
| CUDA / cuDNN | CUDA `12.9`, cuDNN `9.22` |
| CUDA target | `compute_86` |
| Power policy | Windows Balanced mode |

The GPU was not clock-locked. A post-run `nvidia-smi` snapshot showed P0, `1425/7000 MHz` graphics/memory clocks, 1% GPU utilization and 26.19 W. This snapshot was taken after measurement, not sampled continuously during it; it cannot establish the clocks or thermal/power state for each measured call.

## Protocol and result

- Input: `E:\Data\image\bus.jpg` (SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`), divided into distinct top and bottom image bands and prepared as `[2,3,640,640]`.
- Model: official `uvdoc.onnx`, SHA-256 `10a66fe2e2a5e9fdf5f697385ddb38d26a58dca73fec539b45b4783d2e18e338`.
- Engine: built by `TensorRtOnnxEngineBuilder`, dynamic profile `min=1, opt=2, max=2`; SHA-256 `250dcf2a77feac62423ea929c332c448aaea4fcb2bd5f7e81171d51e07f00a94`, 33,284,620 bytes.
- One TensorRT session, five warm-up calls, then 50 measured calls. The prepared batch and session are reused. Every warm-up and measured call produced the same decoded pixel-batch SHA-256.
- Measured scope: synchronous `Session.Run` plus `PaddleDocumentUnwarpingDecoder`. It excludes image decoding, crop/preprocessing, engine build, session creation and the one-time ORT reference run.

| Batch latency | P50 | P95 | Min | Max | Mean |
| --- | ---: | ---: | ---: | ---: | ---: |
| Batch=2, inference + decoder | 28.3687 ms | 32.0690 ms | 25.9519 ms | 33.3257 ms | 28.6308 ms |

Raw TensorRT output stayed finite and within the established ORT CPU comparison bounds (`max abs ≤0.2`, `mean abs ≤0.01`): row 0 `0.1154633/0.00175354`, row 1 `0.0770721/0.00180646` (max/mean). Each decoder output was `640×640×3`; the two rows remained distinct.

## Reproduction

From the repository root in PowerShell, set the local TensorRT 11 bridge/runtime paths and run:

```powershell
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:JYPPX_NATIVE_BRIDGE_PATH = (Resolve-Path 'artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native\jyppxtrtbridge.dll').Path
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = '11'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_UVDOC_BENCHMARK_REPORT_PATH = (Join-Path (Resolve-Path 'eng\models\paddle-document\verification').Path 'paddle-document-uvdoc-dynamic-batch-tensorrt-benchmark-20261018.json')
$env:PATH = "$(Split-Path -Parent $env:JYPPX_NATIVE_BRIDGE_PATH);$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_TENSORRT_ROOT\lib;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"

dotnet test tests\DeploySharp.Visual.TensorRT.Tests\DeploySharp.Visual.TensorRT.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~UvDocRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows' `
  --logger 'console;verbosity=normal'
```

The test passed `1/1`, with zero failures and zero skips. Machine-readable per-call timings, model/engine/input hashes, row parity values and the output fingerprint are in [`paddle-document-uvdoc-dynamic-batch-tensorrt-benchmark-20261018.json`](paddle-document-uvdoc-dynamic-batch-tensorrt-benchmark-20261018.json).

## Boundary

This is a single-host, single-runtime, repeated Batch=2 latency measurement—not a full-image end-to-end timing, dataset-quality result, cross-device ranking or portable-engine claim. The regions are synthetic execution probes rather than unwarping ground truth. Because clocks were not locked and no in-loop clock/thermal trace was collected, do not treat the P50/P95 as a hardware-limit measurement. TensorRT UVDoc execution/performance evidence does not change the separate OpenCV DNN importer boundary or prove support for neighboring document models.
