# Chart2Table ORT/TensorRT evidence alignment

This report compares the same 12 `val` samples at ChartQA revision `044eabfc306abfe9340c5741f0093aefc5973d06`. It does not rerun inference and does not claim split accuracy or a controlled benchmark.

| Measure | Result |
| --- | ---: |
| Image SHA matches | 12/12 |
| Finish reason matches | 12/12 |
| EOS | ORT 12/12; TensorRT 12/12 |
| Structure flag matches | 12/12 |
| Expected cell count matches | 12/12 |
| Exact cells | ORT 140; TensorRT 140 |

| Sample | ORT ms | TensorRT ms | Ratio | ORT cells | TensorRT cells | Structure same |
| --- | ---: | ---: | ---: | ---: | ---: | :---: |
| `00978071004853.png` | 66,133.14 | 11,743.24 | 0.178 | 0 | 0 | yes |
| `00006834003066.png` | 72,932.52 | 12,779.94 | 0.175 | 0 | 0 | yes |
| `OECD_CAESAREAN_SECTIONS_CZE_KOR_000034.png` | 36,886.74 | 6,271.89 | 0.170 | 0 | 0 | yes |
| `00484591006451.png` | 27,763.18 | 4,697.25 | 0.169 | 22 | 22 | yes |
| `two_col_100050.png` | 26,866.00 | 4,100.64 | 0.153 | 11 | 11 | yes |
| `two_col_100022.png` | 23,984.12 | 3,623.61 | 0.151 | 20 | 20 | yes |
| `two_col_100172.png` | 34,921.25 | 5,048.19 | 0.145 | 24 | 24 | yes |
| `two_col_100128.png` | 49,442.14 | 6,796.23 | 0.137 | 42 | 42 | yes |
| `OECD_BROAD_MONEY_(M3)_CHN_CRI_DNK_TUR_ZAF_000026.png` | 14,358.55 | 1,873.82 | 0.131 | 8 | 8 | yes |
| `00108924006058.png` | 9,758.82 | 1,170.27 | 0.120 | 4 | 4 | yes |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 23,685.42 | 2,512.18 | 0.106 | 5 | 5 | yes |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 18,124.24 | 1,778.29 | 0.098 | 4 | 4 | yes |

TensorRT and ORT can agree on EOS while disagreeing on table structure or cells; those dimensions are kept separate above. Timing ratios are host- and plan-specific observations only.
