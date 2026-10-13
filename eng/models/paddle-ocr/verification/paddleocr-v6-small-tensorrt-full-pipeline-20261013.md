# PP-OCRv6 Small TensorRT full pipeline (2026-10-13)

## Result

The opt-in DeploySharp integration test completed one real page through the full
`det → crop → cls → rec → merge` path on Windows x64:

- detector: `paddleocr/ppocrv6/small-det`
- text-line classifier: `paddleocr/ppocrv5/mobile-cls`
- recognizer: `paddleocr/ppocrv6/small-rec`
- input: `E:\Data\ocr\demo_1.jpg` (SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`)
- device: RTX 3060 Laptop GPU, Windows `10.0.26200`, .NET `10.0.12`
- runtime: TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`, bridge package `JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.Bridge` `4.0.0`
- GPU sample at test time: P0, `2002 MHz` graphics / `7001 MHz` memory; clocks were not locked

Each ONNX graph was converted to a caller-owned TensorRT Engine by
`TensorRtOnnxEngineBuilder`. ORT CPU was run on the same prepared input as a
bounded reference. Both paths returned 16 regions, recognized all 16 regions,
and produced the same ordered-text SHA-256
(`2e5319967419ae76ba1f9c21efb10edca237c170f4c89dae081b088a1ccbbaea`). The
single-run end-to-end observations were ORT CPU `1157.2269 ms` and TensorRT
CUDA `1288.2198 ms`; these are execution observations, not a P50/P95 benchmark.

The machine-readable record contains the input/model/Engine hashes and exact
runtime paths: [`paddleocr-v6-small-tensorrt-full-pipeline-20261013.json`](paddleocr-v6-small-tensorrt-full-pipeline-20261013.json).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLEOCR_TRT_FULL_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLEOCR_TRT_FULL_REPORT_PATH = 'eng/models/paddle-ocr/verification/paddleocr-v6-small-tensorrt-full-pipeline-20261013.json'
$env:JYPPX_NATIVE_BRIDGE_PATH = '<matching trt10.11 bridge>\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-10.11.0.33-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'

dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  --filter 'FullyQualifiedName~PaddleOcrTensorRtFullPipelineIntegrationTests'
```

The bridge, CUDA, cuDNN and TensorRT DLL directories must match the installed
runtime. The test is intentionally opt-in and inconclusive without the gate
environment variable.

## Boundary

This closes complete stage execution and ordered-text parity for these exact
three artifacts on this one host. It does **not** claim OCR accuracy, CER/WER,
multi-page quality, formal throughput, cross-device Engine portability, or
TensorRT support for the other PP-OCR variants. The seven-detector dynamic
Batch→DB Decoder evidence remains a separate graph/decoder contract.
