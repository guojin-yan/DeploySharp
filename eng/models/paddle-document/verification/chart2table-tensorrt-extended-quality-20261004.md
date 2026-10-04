# Chart2Table TensorRT extended ChartQA quality selection (2026-10-04)

This record extends the four-image TensorRT qualitative regression with the same six official `ChartQA val` image/table pairs used by the ORT and OpenVINO quality selection. The files are acquired at ChartQA revision `044eabfc306abfe9340c5741f0093aefc5973d06`; the images and CSV tables remain outside the repository.

The run used the four plans built by `TensorRtOnnxEngineBuilder` on the local Windows machine: FP16 vision/projector and token-embedding plans, plus FP32 prefill/decode plans. Runtime was TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`, .NET `10`, and an RTX 3060 Laptop (`sm_86`). GPU clocks were not locked, so elapsed values are observations rather than a controlled benchmark.

| Sample | EOS | Structure | Cell match | Total ms | Decode P50/P95 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | yes | yes | 4/6 | 1,511.7 | 34.30 / 43.47 |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | yes | yes | 5/12 | 2,499.9 | 34.42 / 40.24 |
| `two_col_100022.png` | yes | yes | 20/20 | 3,811.3 | 35.05 / 42.67 |
| `two_col_100050.png` | yes | yes | 11/16 | 4,436.1 | 34.67 / 39.34 |
| `00006834003066.png` | yes | no | 0/56 | 13,251.8 | 40.39 / 48.60 |
| `00108924006058.png` | yes | yes | 4/6 | 1,180.4 | 33.32 / 34.32 |

All six generations reached `EndOfSequence`; five had matching row/column dimensions and the sixth reproduced the known multi-column row/column boundary. Aggregate cell matching was `44/116` (`37.93%`), and only one sample was an exact text match. This is consistent with the ORT/OpenVINO bounded quality selection and is useful for diagnosing the task contract, but it is not ChartQA split accuracy, a question-answer score, or a cross-device performance ranking.

The machine-readable report includes image/table SHA-256, all four Engine SHA-256 values, stage timings, token counts, EOS state, decode P50/P95 and the full structure metrics: [chart2table-tensorrt-extended-quality-20261004.json](chart2table-tensorrt-extended-quality-20261004.json).

## Reproduction

Acquire the external six-image cache with the existing script, then provide the already built plans and matching TensorRT bridge:

```powershell
$env:DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHARTQA_EXTENDED_ROOT = 'E:\Model\PaddleDocument\validation\chartqa-chart2table-extended-20261002'
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

The external ChartQA files are not redistributed by DeploySharp. Do not reuse the plans on another TensorRT/CUDA/GPU combination without rebuilding them.
