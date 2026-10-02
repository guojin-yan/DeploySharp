# Chart2Table bounded ChartQA quality selection (2026-10-02)

This record extends the four-image ChartQA qualitative regression with six official `val` image/table pairs. The selection is acquired by `Acquire-ChartQaChart2TableValidation.ps1` at ChartQA revision `044eabfc306abfe9340c5741f0093aefc5973d06`; images and CSV tables stay in the local validation cache and are not redistributed by DeploySharp.

The same six-image manifest, four-graph ONNX bundle and tokenizer were run on ONNX Runtime CPU and OpenVINO CPU. Both backends reached `EndOfSequence` for all six samples, and generated text SHA-256 values matched across backends for `6/6` samples. The quality evaluator compares the CSV-derived pipe table at row and cell level; it does not infer ChartQA question accuracy from table output.

| Sample | Expected shape | ORT cell match | OpenVINO cell match | Cross-backend text | ORT total ms | OpenVINO total ms |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 3 x 2 | 4/6 (`66.67%`) | 4/6 (`66.67%`) | equal | 13,001.2 | 16,221.7 |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 6 x 2 | 5/12 (`41.67%`) | 5/12 (`41.67%`) | equal | 21,203.2 | 16,903.7 |
| `two_col_100022.png` | 10 x 2 | 20/20 (`100%`) | 20/20 (`100%`) | equal | 22,011.4 | 24,732.8 |
| `two_col_100050.png` | 8 x 2 | 11/16 (`68.75%`) | 11/16 (`68.75%`) | equal | 22,236.5 | 25,108.7 |
| `00006834003066.png` | 7 x 8 | 0/56 (`0%`) | 0/56 (`0%`) | equal | 68,021.7 | 73,697.8 |
| `00108924006058.png` | 3 x 2 | 4/6 (`66.67%`) | 4/6 (`66.67%`) | equal | 8,721.9 | 9,430.8 |

Across this bounded selection, both backends produced EOS `6/6`, equal generated text SHA `6/6`, equal structural dimensions for `5/6`, and `44/116` matching cells (`37.93%`) against the CSV-derived tables. The multi-column `00006834003066` sample exposed the same seven-versus-eight row/column boundary on both backends, so the result is useful for task-quality diagnosis but is not a release accuracy score. The selection is too small and deliberately grouped by filename family; it must not be generalized to the ChartQA split.

Machine-readable reports retain image/table source SHA-256, generated text SHA-256, EOS reason, token count, stage timings, decode P50/P95 and full structure metrics:

- [ORT report](chart2table-extended-quality-ort-20261002.json)
- [OpenVINO report](chart2table-extended-quality-openvino-20261002.json)

## Reproduction

Acquire the external cache (no model or dataset files are written to the repository):

```powershell
pwsh -NoProfile -File eng/models/paddle-document/scripts/Acquire-ChartQaChart2TableValidation.ps1 `
  -Destination E:\Model\PaddleDocument\validation\chartqa-chart2table-extended-20261002 `
  -Revision 044eabfc306abfe9340c5741f0093aefc5973d06 `
  -CountPerGroup 2
```

Run ORT and OpenVINO with the same manifest. The bundle variables follow the four-image regression; only the gate and report path change:

```powershell
$env:DEPLOYSHARP_CHARTQA_EXTENDED_ROOT = 'E:\Model\PaddleDocument\validation\chartqa-chart2table-extended-20261002'
$env:DEPLOYSHARP_CHART2TABLE_MODEL_ROOT = 'E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table'
$env:DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT = 'E:\Model\PaddleDocument\chart-export'
$env:DEPLOYSHARP_CHART2TABLE_TEXT_ROOT = 'E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923'
$env:DEPLOYSHARP_CHART2TABLE_EXTENDED_ORT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHART2TABLE_EXTENDED_REPORT_PATH = (Join-Path (Get-Location) 'artifacts/chart2table-ort-extended-quality-20261002.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleChart2TableExtendedQualityExternalIntegrationTests
```

For OpenVINO, replace the ORT gate/report variables with `DEPLOYSHARP_CHART2TABLE_EXTENDED_OPENVINO_RUN_EXTERNAL=1` and `chart2table-openvino-extended-quality-20261002.json`. TensorRT remains a separate CUDA run; this six-image record does not claim a TensorRT performance profile.

The official dataset source describes `val/png` and `val/tables` as paired chart images and underlying tables. This document intentionally records a bounded task-quality selection, not a full split evaluation or a redistribution of the source dataset.
