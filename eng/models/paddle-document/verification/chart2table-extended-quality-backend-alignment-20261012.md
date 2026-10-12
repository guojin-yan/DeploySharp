# Chart2Table onnxruntime-cpu / openvino-cpu evidence alignment

This report compares the same 24 `val` samples at ChartQA revision `044eabfc306abfe9340c5741f0093aefc5973d06` with a 1024-token generation budget. It does not rerun inference and does not claim split accuracy or a controlled benchmark.

| Measure | Result |
| --- | ---: |
| Image SHA matches | 24/24 |
| Source table / expected text SHA matches | 24/24; 24/24 |
| Generated table SHA matches | 24/24 |
| Finish reason / token count matches | 24/24; 24/24 |
| EOS | onnxruntime-cpu 24/24; openvino-cpu 24/24 |
| Expected / generated dimensions match | 24/24; 24/24 |
| Structure flag / expected cell count matches | 24/24; 24/24 |
| Exact cells | onnxruntime-cpu 211; openvino-cpu 211 |

| Sample | onnxruntime-cpu ms | openvino-cpu ms | Ratio | onnxruntime-cpu cells | openvino-cpu cells | Generated SHA same |
| --- | ---: | ---: | ---: | ---: | ---: | :---: |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 16,369.66 | 20,129.56 | 1.230 | 4 | 4 | yes |
| `02348983021349.png` | 36,133.43 | 33,331.98 | 0.922 | 0 | 0 | yes |
| `two_col_100411.png` | 12,611.73 | 11,600.80 | 0.920 | 12 | 12 | yes |
| `two_col_100197.png` | 15,615.08 | 14,088.20 | 0.902 | 15 | 15 | yes |
| `two_col_100229.png` | 16,472.47 | 14,566.83 | 0.884 | 12 | 12 | yes |
| `two_col_100172.png` | 23,932.46 | 21,081.38 | 0.881 | 24 | 24 | yes |
| `two_col_100223.png` | 12,926.12 | 11,241.75 | 0.870 | 10 | 10 | yes |
| `00006834003066.png` | 53,789.50 | 46,694.59 | 0.868 | 0 | 0 | yes |
| `two_col_100050.png` | 19,328.80 | 16,655.78 | 0.862 | 11 | 11 | yes |
| `00978071004853.png` | 51,708.73 | 44,478.57 | 0.860 | 0 | 0 | yes |
| `01676320003804.png` | 18,485.47 | 15,887.68 | 0.859 | 16 | 16 | yes |
| `OECD_CAESAREAN_SECTIONS_CZE_KOR_000034.png` | 28,072.61 | 24,067.64 | 0.857 | 0 | 0 | yes |
| `two_col_100022.png` | 18,293.54 | 15,529.91 | 0.849 | 20 | 20 | yes |
| `OECD_ENTERPRISES_BY_BUSINESS_SIZE_BEL_BGR_CZE_NOR_SWE_000003.png` | 31,940.59 | 27,052.11 | 0.847 | 0 | 0 | yes |
| `two_col_100128.png` | 30,733.72 | 25,903.78 | 0.843 | 42 | 42 | yes |
| `OECD_CURRENT_ACCOUNT_BALANCE_FORECAST_BEL_DEU_DNK_NOR_USA_000049.png` | 55,285.86 | 44,197.13 | 0.799 | 0 | 0 | yes |
| `OECD_CRUDE_OIL_IMPORT_PRICES_PRT_000090.png` | 22,793.74 | 17,407.08 | 0.764 | 0 | 0 | yes |
| `02348983021411.png` | 12,330.79 | 8,557.91 | 0.694 | 6 | 6 | yes |
| `03250329017073.png` | 12,920.16 | 8,778.72 | 0.679 | 0 | 0 | yes |
| `OECD_BROAD_MONEY_(M3)_CHN_CRI_DNK_TUR_ZAF_000026.png` | 14,627.40 | 9,585.80 | 0.655 | 8 | 8 | yes |
| `00484591006451.png` | 36,502.87 | 21,516.74 | 0.589 | 22 | 22 | yes |
| `OECD_DAILY_SMOKERS_CZE_IRL_LUX_LVA_SVN_000004.png` | 19,148.85 | 11,106.13 | 0.580 | 0 | 0 | yes |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 24,232.27 | 12,490.13 | 0.515 | 5 | 5 | yes |
| `00108924006058.png` | 21,792.85 | 8,819.15 | 0.405 | 4 | 4 | yes |

Backends can agree on EOS while disagreeing on table structure or cells; these dimensions are kept separate. Timing ratios are host- and runtime-specific observations only.
