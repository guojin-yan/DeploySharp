# Chart2Table 24-image ChartQA quality extension (ORT CPU, 2026-10-12)

This is a deterministic, bounded extension of the Chart2Table task-quality diagnostics. The selection contains 24 official `ChartQA/val` image/table pairs from revision `044eabfc306abfe9340c5741f0093aefc5973d06`: eight `OECD*` charts, eight `two_col_*` charts and eight other validation filenames. The external manifest SHA-256 is `0eb2e6ee66c0eb30d5c5bbcd56c4e8e611050ae7a1baf66b663a5213c86dadb1`.

The published four-graph ONNX bundle and tokenizer were run through the real DeploySharp ORT CPU `vision → embedding → prefill → autoregressive decode` path with a `1024` new-token budget. Every sample reached `EndOfSequence` (`24/24`) and produced a structurally valid generated pipe table. The CSV-derived table evaluator is intentionally conservative: it reports row/column shape, row exact matches and cell exact matches; it is not ChartQA question accuracy, a split-level score or a controlled performance benchmark.

## Aggregate result

| Metric | Result |
| --- | ---: |
| Samples / EOS | `24 / 24` |
| Structure dimensions matched | `16 / 24` |
| Exact generated pipe tables | `6 / 24` |
| Row exact matches | `98 / 161` (`60.87%`) |
| Cell exact matches | `211 / 547` (`38.57%`) |
| Total latency order-statistic P50 / P95 | `19,328.80 / 51,708.73 ms` |
| Per-sample decode-step P50 median / P95 | `106.15 / 136.49 ms` |
| Per-sample decode-step P95 median / P95 | `161.55 / 243.55 ms` |

The largest tables remain the difficult cases: several OECD multi-column charts and the eight-column `00006834003066` chart generated valid text but did not match the CSV-derived cells. The two-column group contains the strongest bounded examples, including exact matches for `two_col_100022`, `two_col_100128`, `two_col_100229`, `two_col_100411` and `01676320003804`. These observations are useful for regression triage only; they must not be generalized to the full ChartQA split.

## Per-sample diagnostics

| Sample | Expected shape | Cell exact | Shape match | Total ms | Decode P50/P95 ms |
| --- | ---: | ---: | :---: | ---: | ---: |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 3×2 | 4/6 | yes | 16,369.66 | 116.09 / 279.11 |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 6×2 | 5/12 | yes | 24,232.27 | 142.68 / 223.20 |
| `OECD_BROAD_MONEY_(M3)_CHN_CRI_DNK_TUR_ZAF_000026.png` | 5×2 | 8/10 | yes | 14,627.40 | 110.58 / 157.70 |
| `OECD_CAESAREAN_SECTIONS_CZE_KOR_000034.png` | 3×9 | 0/27 | no | 28,072.61 | 112.49 / 162.98 |
| `OECD_CRUDE_OIL_IMPORT_PRICES_PRT_000090.png` | 2×11 | 0/22 | no | 22,793.74 | 117.73 / 170.95 |
| `OECD_CURRENT_ACCOUNT_BALANCE_FORECAST_BEL_DEU_DNK_NOR_USA_000049.png` | 6×9 | 0/54 | no | 55,285.86 | 122.73 / 192.23 |
| `OECD_DAILY_SMOKERS_CZE_IRL_LUX_LVA_SVN_000004.png` | 4×4 | 0/16 | no | 19,148.85 | 116.82 / 164.17 |
| `OECD_ENTERPRISES_BY_BUSINESS_SIZE_BEL_BGR_CZE_NOR_SWE_000003.png` | 6×6 | 0/36 | no | 31,940.59 | 99.06 / 131.76 |
| `two_col_100022.png` | 10×2 | 20/20 | yes | 18,293.54 | 97.20 / 146.51 |
| `two_col_100050.png` | 8×2 | 11/16 | yes | 19,328.80 | 106.15 / 161.55 |
| `two_col_100128.png` | 21×2 | 42/42 | yes | 30,733.72 | 105.50 / 164.33 |
| `two_col_100172.png` | 12×2 | 24/24 | yes | 23,932.46 | 103.14 / 153.37 |
| `two_col_100197.png` | 8×2 | 15/16 | yes | 15,615.08 | 97.91 / 151.02 |
| `two_col_100223.png` | 6×2 | 10/12 | yes | 12,926.12 | 93.30 / 120.12 |
| `two_col_100229.png` | 6×2 | 12/12 | yes | 16,472.47 | 97.29 / 145.10 |
| `two_col_100411.png` | 6×2 | 12/12 | yes | 12,611.73 | 92.26 / 133.22 |
| `00006834003066.png` | 7×8 | 0/56 | no | 53,789.50 | 117.05 / 201.26 |
| `00108924006058.png` | 3×2 | 4/6 | yes | 21,792.85 | 136.49 / 455.97 |
| `00484591006451.png` | 12×2 | 22/24 | yes | 36,502.87 | 150.17 / 243.55 |
| `00978071004853.png` | 5×10 | 0/50 | no | 51,708.73 | 107.76 / 154.76 |
| `01676320003804.png` | 8×2 | 16/16 | yes | 18,485.47 | 97.79 / 157.83 |
| `02348983021349.png` | 6×7 | 0/42 | no | 36,133.43 | 101.59 / 166.89 |
| `02348983021411.png` | 4×2 | 6/8 | yes | 12,330.79 | 109.36 / 190.89 |
| `03250329017073.png` | 4×2 | 0/8 | yes | 12,920.16 | 97.79 / 146.84 |

## Reproduction

The dataset cache is external and is not redistributed by DeploySharp:

```powershell
pwsh -NoProfile -File eng/models/paddle-document/scripts/Acquire-ChartQaChart2TableValidation.ps1 `
  -Destination E:\Model\PaddleDocument\validation\chartqa-chart2table-extended-20261012 `
  -Revision 044eabfc306abfe9340c5741f0093aefc5973d06 `
  -Split val `
  -CountPerGroup 8
```

Run the external test with the four-graph bundle and matching tokenizer:

```powershell
$env:DEPLOYSHARP_CHARTQA_EXTENDED_ROOT = 'E:\Model\PaddleDocument\validation\chartqa-chart2table-extended-20261012'
$env:DEPLOYSHARP_CHART2TABLE_MODEL_ROOT = 'E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table'
$env:DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT = 'E:\Model\PaddleDocument\chart-export'
$env:DEPLOYSHARP_CHART2TABLE_EXTENDED_ORT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHART2TABLE_EXTENDED_ORT_MAX_NEW_TOKENS = '1024'
$env:DEPLOYSHARP_CHART2TABLE_EXTENDED_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/chart2table-extended-quality-ort-20261012.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleChart2TableExtendedQualityExternalIntegrationTests.OfficialChartQaSelectionRecordsTableQuality'
```

The machine-readable report retains every image/table SHA-256, generated text SHA-256, finish reason, token count, stage timings, decode percentiles and structure metrics: [chart2table-extended-quality-ort-20261012.json](chart2table-extended-quality-ort-20261012.json). The 24-image expansion is ORT-only in this increment; the existing 12-image OpenVINO/TensorRT reports remain the cross-backend evidence. A matching 24-image OpenVINO/TensorRT run is a separate follow-up and must not be inferred from this ORT result.
