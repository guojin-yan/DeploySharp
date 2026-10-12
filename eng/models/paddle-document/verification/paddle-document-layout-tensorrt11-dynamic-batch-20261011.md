# PP-Structure layout TensorRT 11 dynamic Batch=2 evidence (2026-10-11)

## Result

The opt-in DeploySharp integration test passed **5/5 model rows** (`0 failed / 0 skipped`) for the five dynamic PP-Structure layout graphs covered here. Each engine was rebuilt from the registered ONNX artifact with `TensorRtOnnxEngineBuilder`, bound with `image`, `im_shape` and `scale_factor`, executed with two different `bus.jpg` regions, and decoded with the same Paddle NMS decoder used by the ORT CPU reference.

| Model | Batch input | Engine SHA-256 | Confident rows (ORT/TRT) | Minimum IoU | Max score drift | P50 / P95 |
|---|---|---|---:|---:|---:|---:|
| `paddle-doc/pp-doclayout-l` | `[2,3,640,640]`, geometry `[2,2]` | `3132ae8d42f044c299544fecb7db3db9f8e7e1e3a412c82c3478da386df0e55` | `1 / 1` per row | `0.999942` | `2.14e-5` | `40.0028 / 45.6559 ms` |
| `paddle-doc/pp-doclayout-plus-l` | `[2,3,800,800]`, geometry `[2,2]` | `16934219aa2a16885632b16cdf1a4dac7e086fe95c054e611accf36ed4b80450` | `6 / 6`, `4 / 4` | `0.999655` | `1.59e-4` | `52.2604 / 54.0856 ms` |
| `paddle-doc/pp-docblocklayout` | `[2,3,640,640]`, geometry `[2,2]` | `0fbf835d5c62ccc6471f9925cca6d4e557498858621d6e3b7b47487aca1a5245` | `1 / 1` per row | `0.999991` | `6.62e-6` | `34.6254 / 37.1798 ms` |
| `paddle-doc/rt-detr-h-layout-3cls` | `[2,3,640,640]`, geometry `[2,2]` | `5bec71bcf57b9d6bffbc601e4164b8d2ad305b5e3a55f959f87a924753304a30` | `3 / 3`, `2 / 2` | `0.999894` | `5.15e-4` | `115.2087 / 116.2460 ms` |
| `paddle-doc/rt-detr-h-layout-17cls` | `[2,3,640,640]`, geometry `[2,2]` | `177251e9d579a04236954f3f6da3328831a73ba06f20c32e890f7bc6ba1f2841` | `1 / 1` per row | `0.999907` | `4.12e-6` | `121.8828 / 148.5517 ms` |

The comparison contract applies only to semantic candidates with `score >= 0.20`: same class, global maximum-IoU matching, IoU `>= 0.80`, and score difference `<= 0.01`. The decoder still records all exported candidates (300 per row in these graphs); low-score NMS tail count differences are diagnostic and are not treated as semantic failures. Both input rows were confirmed distinct by per-row tensor SHA-256, and the decoded results retained row ownership.

## Host and protocol

- Windows `10.0.26200` x64; .NET `10.0.12`; NVIDIA RTX 3060 Laptop (`compute_86`).
- TensorRT `11.0.0.114-cu12`, CUDA `12.9`, cuDNN `9.22`.
- Source image: `E:\Data\image\bus.jpg`; the two non-overlapping regions are `810×540` top and bottom bands. They are execution probes, not layout ground truth.
- Five warmup runs and fifty measured runs per model. P50/P95 include the existing TensorRT session execution and Paddle NMS decoder path used by the test; they are not GPU-kernel-only timings.
- ORT CPU is the numerical reference. No accuracy, cross-device portability, or all-model TensorRT claim is implied.

## Reproduce

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_DYNAMIC_BATCH_WARMUP = '5'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_DYNAMIC_BATCH_ITERATIONS = '50'
$env:JYPPX_NATIVE_BRIDGE_PATH = 'E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_86'

dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  --no-restore --configuration Release `
  --filter 'FullyQualifiedName~LayoutNmsRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows'
```

The exact ONNX SHA-256, image SHA-256, input row digests, auxiliary shapes and per-row comparison values are in the accompanying [machine-readable JSON records](paddle-document-layout-paddle-doc-pp-doclayout-l-tensorrt11-dynamic-batch-20261011.json), [Plus-L](paddle-document-layout-paddle-doc-pp-doclayout-plus-l-tensorrt11-dynamic-batch-20261011.json), [DocBlockLayout](paddle-document-layout-paddle-doc-pp-docblocklayout-tensorrt11-dynamic-batch-20261011.json), [RT-DETR-H 3-class](paddle-document-layout-paddle-doc-rt-detr-h-layout-3cls-tensorrt11-dynamic-batch-20261011.json) and [RT-DETR-H 17-class](paddle-document-layout-paddle-doc-rt-detr-h-layout-17cls-tensorrt11-dynamic-batch-20261011.json).

## Boundary

This closes the exact dynamic Batch=2 execution, row-isolation and bounded ORT parity contract for these five layout artifacts on one Windows TensorRT 11 host. It does not close TensorRT dynamic Batch for static `pp-doclayout-m`/`pp-doclayout-s` or PicoDet layout graphs; it does not establish layout accuracy, a full PP-Structure pipeline, cross-device Engine portability, or backend-wide performance. The existing single-image TensorRT evidence for the five PP-DocLayout graphs remains a separate report.
