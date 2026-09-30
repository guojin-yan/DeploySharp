# Chart2Table TensorRT multi-image regression (2026-09-30)

The four curated ChartQA `test_human` samples used by the ORT and OpenVINO regressions were executed through the DeploySharp four-engine TensorRT CUDA bundle. The test used the plans built by `TensorRtOnnxEngineBuilder`, FP16 vision/projector and token-embedding engines, FP32 prefill/decode engines, and `maximumNewTokens=1024`.

| Sample | EOS tokens | Total ms | Vision / embedding / prefill ms | Decode P50/P95 ms | Structure row/cell | Output SHA == expected SHA |
| --- | ---: | ---: | --- | ---: | --- | --- |
| `png_41699051005347.png` | 168 | 99,866.2 | 787.2 / 133.9 / 915.6 | 573.38 / 1,013.80 | 1.00 / 1.00 | yes |
| `png_41810321001157.png` | 34 | 28,838.4 | 1,857.5 / 28.6 / 1,158.4 | 790.29 / 1,164.32 | 1.00 / 1.00 | yes |
| `png_8127.png` | 40 | 26,995.0 | 1,814.1 / 29.2 / 1,044.6 | 692.65 / 975.09 | 1.00 / 1.00 | yes |
| `png_166.png` | 77 | 52,104.3 | 2,429.5 / 55.7 / 606.3 | 609.42 / 1,066.15 | 1.00 / 1.00 | yes |

All four runs reached `EndOfSequence`, reproduced the expected table byte-for-byte, and passed the structural evaluator (row count, column count, row matches and cell matches). The machine-readable report records image SHA-256, all four engine SHA-256 values, token counts, stage timings and the full structure fields: [chart2table-tensorrt-multi-image-20260930.json](chart2table-tensorrt-multi-image-20260930.json).

The runtime was TensorRT 10.11.0.33, CUDA 12.9 and cuDNN 9.22 on the local RTX 3060 Laptop. GPU clocks were not locked and this was one run per image; strict-FP32 language plans make the decode path intentionally slow. These numbers are task-level regression observations, not a controlled throughput benchmark or a ChartQA split accuracy score. OpenCV DNN autoregressive generation remains outside the current adapter contract.

## Reproduction

Download the four external ChartQA images from the official `test_human` distribution into a local directory. The dataset is not redistributed here. Then configure the runtime and existing DeploySharp-built plans:

```powershell
$env:DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHART2TABLE_MODEL_ROOT = 'E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table'
$env:DEPLOYSHARP_CHARTQA_SAMPLE_ROOT = 'E:\Model\PaddleDocument\validation\chartqa-20260924'
$env:DEPLOYSHARP_CHART2TABLE_TRT_ENGINE_ROOT = 'E:\Model\PaddleDocument\chart2table-builder-e2e-20260924'
$env:DEPLOYSHARP_CHART2TABLE_TRT_USE_EXISTING_PLANS = '1'
$env:DEPLOYSHARP_CHART2TABLE_TRT_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/chart2table-tensorrt-multi-image-20260930.json')
$env:JYPPX_NATIVE_BRIDGE_PATH = 'C:\Users\guoji\.nuget\packages\jyppx.tensorrt.csharp.api.runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.bridge\4.0.0\runtimes\win-x64\native\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-10.11.0.33-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:PATH = "$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleChart2TableTensorRtExternalIntegrationTests.ChartQaHumanTestChartsGenerateCompleteTablesThroughTensorRt
```
