# Chart2Table TensorRT 12-image ChartQA quality selection (2026-10-05)

This record runs the same fixed 12-image `ChartQA/val` selection used by the ORT report at revision `044eabfc306abfe9340c5741f0093aefc5973d06`. The images and CSV tables remain outside this repository. The test exercises the four library-built TensorRT plans, greedy generation and the dynamic KV decode path; it is a bounded task-quality and timing diagnostic, not a ChartQA split score.

Runtime: TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`, .NET `10`, local Windows RTX 3060 Laptop (`sm_86`). The GPU clock was not locked. The four Engine SHA-256 values and every per-sample result are in [chart2table-extended-quality-tensorrt-20261005.json](chart2table-extended-quality-tensorrt-20261005.json).

| Measure | Result |
| --- | ---: |
| Samples | 12 |
| EOS complete | 12/12 |
| Matching row/column dimensions | 9/12 |
| Exact cell matches | 140/293 (47.78%) |
| Total latency P50/P95 | 4,398.95 / 12,209.76 ms |
| Decode-step P50 range | 33.37–39.48 ms |
| Decode-step P95 range | 35.85–48.81 ms |

The three dimension mismatches are the same difficult multi-column cases already visible in the ORT bounded selection. All generations reached `EndOfSequence`; EOS completion therefore does not imply correct table structure or cell content. The aggregate result is useful for comparing the TensorRT route with the aligned ORT/OpenVINO diagnostics, but it is not a dataset accuracy claim, question-answer score, or a controlled performance ranking.

## Reproduction

Use the external ChartQA cache and the already-built plans; the dataset files and plans are not redistributed by DeploySharp:

```powershell
$env:DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHARTQA_EXTENDED_ROOT = 'E:\Model\PaddleDocument\validation\chartqa-chart2table-extended-20261004'
$env:DEPLOYSHARP_CHART2TABLE_MODEL_ROOT = 'E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table'
$env:DEPLOYSHARP_CHART2TABLE_TRT_ENGINE_ROOT = 'E:\Model\PaddleDocument\chart2table-builder-e2e-20260924'
$env:DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_MAX_NEW_TOKENS = '1024'
$env:DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_REPORT_PATH = (Join-Path (Get-Location) 'artifacts/chart2table-tensorrt-extended-quality.json')
$env:JYPPX_NATIVE_BRIDGE_PATH = 'C:\Users\guoji\.nuget\packages\jyppx.tensorrt.csharp.api.runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.bridge\4.0.0\runtimes\win-x64\native\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-10.11.0.33-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:PATH = "$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleChart2TableTensorRtExternalIntegrationTests.OfficialChartQaExtendedSelectionRecordsTensorRtQualityAndTiming
```

The plans are tied to their TensorRT/CUDA/cuDNN and GPU combination. Rebuild them on another device, record new Engine hashes, and do not compare the elapsed values as a portable benchmark without a locked-clock protocol.
