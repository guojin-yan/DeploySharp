# Chart2Table onnxruntime-cpu / tensorrt-cuda evidence alignment

This report compares the same 24 `val` samples at ChartQA revision `044eabfc306abfe9340c5741f0093aefc5973d06` with a 1024-token generation budget. It does not rerun inference and does not claim split accuracy or a controlled benchmark.

| Measure | Result |
| --- | ---: |
| Image SHA matches | 24/24 |
| Source table / expected text SHA matches | 24/24; 24/24 |
| Generated table SHA matches | 23/24 |
| Finish reason / token count matches | 24/24; 24/24 |
| EOS | onnxruntime-cpu 24/24; tensorrt-cuda 24/24 |
| Expected / generated dimensions match | 24/24; 24/24 |
| Structure flag / expected cell count matches | 24/24; 24/24 |
| Exact cells | onnxruntime-cpu 211; tensorrt-cuda 211 |

| Sample | onnxruntime-cpu ms | tensorrt-cuda ms | Ratio | onnxruntime-cpu cells | tensorrt-cuda cells | Generated SHA same |
| --- | ---: | ---: | ---: | ---: | ---: | :---: |
| `02348983021349.png` | 36,133.43 | 18,404.45 | 0.509 | 0 | 0 | yes |
| `00006834003066.png` | 53,789.50 | 25,938.11 | 0.482 | 0 | 0 | yes |
| `00978071004853.png` | 51,708.73 | 24,173.29 | 0.467 | 0 | 0 | yes |
| `OECD_CURRENT_ACCOUNT_BALANCE_FORECAST_BEL_DEU_DNK_NOR_USA_000049.png` | 55,285.86 | 24,482.02 | 0.443 | 0 | 0 | yes |
| `two_col_100128.png` | 30,733.72 | 13,120.92 | 0.427 | 42 | 42 | yes |
| `OECD_ENTERPRISES_BY_BUSINESS_SIZE_BEL_BGR_CZE_NOR_SWE_000003.png` | 31,940.59 | 13,336.57 | 0.418 | 0 | 0 | no |
| `OECD_CAESAREAN_SECTIONS_CZE_KOR_000034.png` | 28,072.61 | 11,705.80 | 0.417 | 0 | 0 | yes |
| `01676320003804.png` | 18,485.47 | 7,316.81 | 0.396 | 16 | 16 | yes |
| `two_col_100172.png` | 23,932.46 | 9,347.41 | 0.391 | 24 | 24 | yes |
| `two_col_100050.png` | 19,328.80 | 7,335.01 | 0.379 | 11 | 11 | yes |
| `two_col_100229.png` | 16,472.47 | 6,220.66 | 0.378 | 12 | 12 | yes |
| `OECD_CRUDE_OIL_IMPORT_PRICES_PRT_000090.png` | 22,793.74 | 8,279.72 | 0.363 | 0 | 0 | yes |
| `two_col_100197.png` | 15,615.08 | 5,616.68 | 0.360 | 15 | 15 | yes |
| `two_col_100022.png` | 18,293.54 | 6,498.09 | 0.355 | 20 | 20 | yes |
| `two_col_100411.png` | 12,611.73 | 4,205.88 | 0.333 | 12 | 12 | yes |
| `two_col_100223.png` | 12,926.12 | 4,288.01 | 0.332 | 10 | 10 | yes |
| `00484591006451.png` | 36,502.87 | 9,024.22 | 0.247 | 22 | 22 | yes |
| `OECD_DAILY_SMOKERS_CZE_IRL_LUX_LVA_SVN_000004.png` | 19,148.85 | 4,013.53 | 0.210 | 0 | 0 | yes |
| `03250329017073.png` | 12,920.16 | 2,674.82 | 0.207 | 0 | 0 | yes |
| `OECD_BROAD_MONEY_(M3)_CHN_CRI_DNK_TUR_ZAF_000026.png` | 14,627.40 | 3,022.16 | 0.207 | 8 | 8 | yes |
| `02348983021411.png` | 12,330.79 | 2,493.81 | 0.202 | 6 | 6 | yes |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 24,232.27 | 3,783.55 | 0.156 | 5 | 5 | yes |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 16,369.66 | 2,270.87 | 0.139 | 4 | 4 | yes |
| `00108924006058.png` | 21,792.85 | 1,887.42 | 0.087 | 4 | 4 | yes |

Backends can agree on EOS while disagreeing on table structure or cells; these dimensions are kept separate. Timing ratios are host- and runtime-specific observations only.
