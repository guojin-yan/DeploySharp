# Core PP-OCR TensorRT full-pipeline matrix (2026-10-13)

## Result

The opt-in DeploySharp integration test completed all seven registered core
PP-OCR combinations through the real `det -> crop -> cls -> rec -> merge` path.
Each row used its exact detector, classifier, recognizer and dictionary. ORT CPU
ran on the same prepared page as the bounded reference, while DeploySharp built
and loaded fresh TensorRT engines for the CUDA path.

| Combination | Regions ORT / TensorRT | Ordered text | ORT (ms) | TensorRT (ms) |
| --- | ---: | --- | ---: | ---: |
| PP-OCRv4 Mobile | 16 / 16 | SHA-256 match | 1587.9205 | 1667.6698 |
| PP-OCRv4 Server | 16 / 16 | SHA-256 match | 4609.7990 | 1932.5052 |
| PP-OCRv5 Mobile | 16 / 16 | SHA-256 match | 2139.1939 | 2074.4220 |
| PP-OCRv5 Server | 16 / 16 | SHA-256 match | 3361.7707 | 3178.6659 |
| PP-OCRv6 Tiny | 16 / 16 | SHA-256 match | 573.3846 | 755.7612 |
| PP-OCRv6 Small | 16 / 16 | SHA-256 match | 912.4130 | 941.5433 |
| PP-OCRv6 Medium | 16 / 16 | SHA-256 match | 2550.8853 | 2298.4424 |

All `7/7` combinations passed. Both paths recognized every returned region, and
the ordered-text SHA-256 matched for every combination. The machine-readable
record contains the individual ONNX and generated Engine hashes:
[`paddleocr-core-tensorrt-full-pipeline-20261013.json`](paddleocr-core-tensorrt-full-pipeline-20261013.json).

## Environment and input

- input: `E:\Data\ocr\demo_1.jpg`
- input SHA-256: `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`
- host: Windows `10.0.26200`, x64, .NET `10.0.12`
- device: NVIDIA GeForce RTX 3060 Laptop GPU, driver `576.02`
- sampled state: P0, `2002 MHz` graphics / `7001 MHz` memory; clocks were not locked
- TensorRT: `10.11.0.33-cu12`
- CUDA: `12.9`
- cuDNN: `9.22`
- bridge: `JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.Bridge` `4.0.0`

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_TRT_ALL_FULL_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLEOCR_TRT_ALL_FULL_REPORT_PATH = 'eng/models/paddle-ocr/verification/paddleocr-core-tensorrt-full-pipeline-20261013.json'
$env:JYPPX_NATIVE_BRIDGE_PATH = '<matching trt10.11 bridge>\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-10.11.0.33-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'

dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  --filter 'FullyQualifiedName~PaddleOcrAllCorePipelineTensorRtIntegrationTests'
```

The TensorRT, CUDA and cuDNN DLL directories must also be present on `PATH`.
The test is intentionally inconclusive unless the explicit external-model gate
is enabled.

## Timing and evidence boundary

The observations above are one run per combination. They exclude ONNX-to-Engine
construction, but include backend registry, Session/Pipeline construction and
the complete page call. They have no warm-up or percentile distribution and the
GPU clocks were not locked. They are therefore execution diagnostics, not a
formal P50/P95 comparison or proof that one backend is faster.

This record closes complete stage execution and ordered-text parity for the
listed artifacts on this one host. It does **not** establish CER/WER, detection
recall, multi-page quality, formal throughput, cross-device Engine portability,
or support for another TensorRT, CUDA, cuDNN or bridge combination.
