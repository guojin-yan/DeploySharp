# PP-Structure multi-page bounded concurrency: TensorRT CUDA 5/50 benchmark (2026-10-05)

This record runs the real two-page orientation -> layout pipeline through DeploySharp TensorRT CUDA sessions. The two pages reuse `bus.jpg` only to exercise ordering, provenance and stage concurrency; this is not a document-quality evaluation.

## Environment and protocol

| Field | Value |
|---|---|
| Host | `JYPPX`, Windows `10.0.26200` |
| Runtime | `.NET 10.0.12`, x64, 16 logical processors |
| Backend | TensorRT CUDA, API 10 |
| CUDA target | `compute_86` |
| Input | `E:\Data\image\bus.jpg` duplicated as page indexes `0` and `1` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| TensorRT root | `D:\Program Files\TensorRT-10.11.0.33-cu12` |
| CUDA root | `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9` |
| cuDNN root | `D:\Program Files\cuDNN-9.22.0-cuda12.9` |
| Session/page concurrency | `2 / 2` |
| Warm-ups / measurements | `5 / 50` per execution mode |
| Source revision | `0d5ea4a88d2c2e2d793e12c572dc9699101bfbeb` |

The orientation ONNX SHA-256 is `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0`; its built Engine SHA-256 is `1246002caf268965fac5ed22f738f4dca28907b13a65284650980c3c9ee6db27`. The layout ONNX SHA-256 is `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`; its built Engine SHA-256 is `3589413bbf43f055ac7b00a1e5040d549e64e0286e83af6fccec6a8a2c539751`. Every measured result checked page order, source SHA and non-empty layout output.

## Results

| Execution | Min (ms) | P50 (ms) | P95 (ms) | Max (ms) |
|---|---:|---:|---:|---:|
| Sequential `RunManyAsync` | 100.6530 | 103.2937 | 109.6768 | 111.9409 |
| Concurrent `RunManyConcurrentAsync(2)` | 64.4305 | 66.7278 | 69.0992 | 72.3972 |

For this fixed two-page workload, page concurrency reduced P50 by approximately `35.4%` and P95 by `37.0%`. Both modes returned pages in caller order, with orientation `0_degree` and `300` layout regions per page. The result is tied to the exact built Engines, TensorRT/CUDA/cuDNN versions, GPU and current thermal/clock state; no lock-clock trace was captured.

The complete per-sample machine-readable evidence is in [paddle-document-tensorrt-multipage-benchmark-20261005.json](paddle-document-tensorrt-multipage-benchmark-20261005.json).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_BENCHMARK = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_MEASUREMENTS = '50'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = '10'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SESSION_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_PAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_EVIDENCE_PATH = `
  'E:\Model\PaddleDocument\validation\paddle-document-tensorrt-multipage-benchmark-{backend}.json'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim()
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-10.11.0.33-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:JYPPX_NATIVE_BRIDGE_PATH = `
  'C:\Users\guoji\.nuget\packages\jyppx.tensorrt.csharp.api.runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.bridge\4.0.0\runtimes\win-x64\native\jyppxtrtbridge.dll'
$bridgeDir = Split-Path -Parent $env:JYPPX_NATIVE_BRIDGE_PATH
$env:PATH = "$bridgeDir;$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_TENSORRT_ROOT\lib;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"

dotnet test .\tests\DeploySharp.Visual.TensorRT.Tests\DeploySharp.Visual.TensorRT.Tests.csproj `
  -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentTensorRtMultiPageIntegrationTests.TwoPageOrientationLayoutRunsThroughTensorRtAndPreservesPageOrder' `
  --logger 'console;verbosity=minimal'
```

This benchmark covers one Windows host, one duplicated input and two exact built Engines. It is not a model Batch benchmark, a quality score, a universal speed claim or evidence for another TensorRT major/API line.
