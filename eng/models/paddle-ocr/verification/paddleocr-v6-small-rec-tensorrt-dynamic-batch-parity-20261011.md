# PP-OCRv6 Small REC TensorRT 11 dynamic-batch CTC parity

## Result

On the Windows RTX 3060 host, the real `demo_1.jpg` detector crops were run as one `[2,3,48,3200]` recognition batch through:

- ONNX Runtime CPU reference;
- a freshly rebuilt TensorRT 11 engine executed by the DeploySharp TensorRT provider.

Both rows returned the same decoded text and the same emitted CTC class sequence:

| Row | ORT text | TensorRT text | Emitted tokens | Non-emitted class drift | Max confidence drift |
|---:|---|---|---:|---:|---:|
| 0 | `纯臻营养护发素` | `纯臻营养护发素` | 7 | 0 | 0.0015450716 |
| 1 | `产品信息/参数` | `产品信息/参数` | 7 | 1 | 0.07490915 |

Raw output shapes were `[2,400,18710]` for both backends. The raw output SHA values differ, as expected for CUDA floating-point execution; the report records maximum/mean absolute values and both output digests.

## Contract and interpretation

The parity contract requires decoded text, batch order, token count and emitted CTC class sequence to match. A class change at a timestep that is removed by CTC blank/repeat collapse is retained as a diagnostic but does not fail the contract. This run had one such non-emitted class drift and two corresponding emission-state changes on row 1, while the emitted sequence and text remained identical.

This is a two-crop, one-image, single-host execution record. It is not a CER/WER score, a throughput benchmark, a full `det → crop → cls → rec` quality gate, or evidence that every PP-OCR model is TensorRT-ready.

## Reproduction

Set the external gate and the consumer-owned native runtime variables, then run:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_TRT_PARITY_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLEOCR_TRT_REC_PARITY_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-ocr/verification/paddleocr-v6-small-rec-tensorrt-dynamic-batch-parity-20261011.json')
$env:JYPPX_NATIVE_BRIDGE_PATH = '...\tensorrt11-bridge\runtimes\win-x64\native'
$env:JYPPX_TENSORRT_ROOT = '...\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = '...\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = '...\cuDNN-9.22.0-cuda12.9'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  --filter FullyQualifiedName~PaddleOcrRecognitionTensorRtParityIntegrationTests
```

The machine-readable result is [paddleocr-v6-small-rec-tensorrt-dynamic-batch-parity-20261011.json](paddleocr-v6-small-rec-tensorrt-dynamic-batch-parity-20261011.json).
