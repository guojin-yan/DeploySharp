# PP-Structure layout TensorRT 11 verification (2026-10-11)

## Result

`LayoutNmsRunsThroughTensorRtAndPreservesOrtDecoding` passed **5/5** (0 failed, 0 skipped) for the five local PP-Structure layout graphs. Each graph was rebuilt from ONNX with `TensorRtOnnxEngineBuilder`, loaded by the DeploySharp TensorRT provider, and compared with the same input decoded by ONNX Runtime CPU.

| Model | Input | Confident regions | Min matched IoU | P50 / P95 |
|---|---:|---:|---:|---:|
| `pp-doclayout-l` | `[1,3,640,640]` | 9 | 0.999467 | 20.9807 / 23.9627 ms |
| `pp-doclayout-plus-l` | `[1,3,800,800]` | 34 | 0.999202 | 29.3522 / 39.7585 ms |
| `pp-doclayout-m` | `[1,3,640,640]` | 28 | 0.832389 | 10.4862 / 14.1939 ms |
| `pp-doclayout-s` | `[1,3,480,480]` | 48 | 0.996929 | 7.8057 / 9.4161 ms |
| `pp-docblocklayout` | `[1,3,640,640]` | 1 | 0.999996 | 20.0243 / 22.8211 ms |

The contract uses score tolerance `0.01`, same-class global maximum-IoU matching, and IoU threshold `0.80`. `pp-doclayout-m` has one nested/low-score geometry pair below `0.85` (`0.832389`) but no pair below `0.75`; it remains above the declared semantic threshold and all labels, counts and scores passed.

## Runtime and protocol

- Windows 10.0.26200 x64; .NET 10.0.401; NVIDIA GeForce RTX 3060 Laptop GPU.
- TensorRT `11.0.0.114-cu12`, CUDA `12.9`, cuDNN `9.22`, `compute_86`.
- DeploySharp native bridge SHA-256: `a94d1e5fe4454c9402979ae050f7a64d74c7f51bd6bb2d74936531f608a9ef6f`.
- Image: `E:\Data\image\bus.jpg`.
- Five warmup runs and fifty timed runs per model. Timing includes the existing input binding/session execution and decoder call used by the test; it is not a standalone GPU kernel benchmark.
- `pp-doclayout-l`, `pp-doclayout-plus-l`, and `pp-docblocklayout` bind `im_shape` and `scale_factor`; static M/S graphs bind only `scale_factor` because their ONNX contracts do not expose `im_shape`.

## Fix covered by this evidence

PP-DocLayout M/S expose data-dependent `[N,6]` outputs. TensorRT may report a byte-aligned `GetMaxOutputSize` that is not exactly divisible by one row (M reported 2560 bytes for a six-float row). `TensorRtSession` now allocates a ceiling row bound, queries the concrete context shape after enqueue before falling back to that bound, and therefore does not decode allocator padding as real detections.

## Reproduce

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
$env:JYPPX_NATIVE_BRIDGE_PATH = 'E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_WARMUP = '5'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_ITERATIONS = '50'
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  --filter 'Name~LayoutNmsRunsThroughTensorRtAndPreservesOrtDecoding' `
  --logger 'trx;LogFileName=layout-nms-tensorrt11-20261011-final-v2.trx'
```

The machine-readable record is [`paddle-document-layout-tensorrt11-20261011.json`](paddle-document-layout-tensorrt11-20261011.json). The related documentation link audit checked 471 local Markdown links with zero broken links ([JSON](paddle-document-layout-link-audit-20261011.json)). This evidence is limited to these exact ONNX files, one Windows GPU/runtime, and ORT-vs-TensorRT execution parity; it does not mark all PP-Structure models or all backends as supported.
