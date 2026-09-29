# Chart2Table ORT multi-image regression (2026-09-29)

The four curated ChartQA human samples previously covered by the TensorRT qualitative task check were rerun through the DeploySharp four-graph ONNX bundle on ONNX Runtime CPU with `maximumNewTokens=256`.

| Sample | EOS tokens | Total ms | Decode P50/P95 ms | Output SHA == expected SHA |
| --- | ---: | ---: | ---: | --- |
| `png_41699051005347.png` | 168 | 47,049.1 | 101.02 / 138.29 | yes |
| `png_41810321001157.png` | 34 | 14,493.3 | 90.15 / 109.17 | yes |
| `png_8127.png` | 40 | 12,649.4 | 90.25 / 116.43 | yes |
| `png_166.png` | 77 | 19,941.2 | 91.02 / 125.76 | yes |

All four runs finished with `EndOfSequence`; exact expected/generated table SHA-256 values match. Image SHA-256, graph root, token counts and stage timings are in [chart2table-ort-multi-image-20260929.json](chart2table-ort-multi-image-20260929.json).

This is a four-sample qualitative regression, not a ChartQA dataset accuracy score. It strengthens ORT CPU task-level evidence; OpenCV DNN autoregressive generation remains unverified and the full ChartQA split is not claimed.

## Reproduction

```powershell
$env:DEPLOYSHARP_CHART2TABLE_ORT_MULTI_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHART2TABLE_MODEL_ROOT = 'E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table'
$env:DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT = 'E:\Model\PaddleDocument\chart-export'
$env:DEPLOYSHARP_CHART2TABLE_TEXT_ROOT = 'E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923'
$env:DEPLOYSHARP_CHARTQA_SAMPLE_ROOT = 'E:\Model\PaddleDocument\validation\chartqa-20260924'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleChart2TableMultiImageOrtExternalIntegrationTests
```
