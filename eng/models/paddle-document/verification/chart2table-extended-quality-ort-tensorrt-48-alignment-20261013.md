# Chart2Table onnxruntime-cpu / tensorrt-cuda evidence alignment

This report compares the same 48 `val` samples at ChartQA revision `044eabfc306abfe9340c5741f0093aefc5973d06` with a 1024-token generation budget. It does not rerun inference and does not claim split accuracy or a controlled benchmark.

| Measure | Result |
| --- | ---: |
| Image SHA matches | 48/48 |
| Source table / expected text SHA matches | 48/48; 48/48 |
| Generated table SHA matches | 46/48 |
| Finish reason / token count matches | 48/48; 47/48 |
| EOS | onnxruntime-cpu 48/48; tensorrt-cuda 48/48 |
| Expected / generated dimensions match | 48/48; 48/48 |
| Parseable / structure / exact-text flags match | 48/48; 48/48; 48/48 |
| Expected / actual cell counts match | 48/48; 48/48 |
| Exact rows | onnxruntime-cpu 205; tensorrt-cuda 205 |
| Exact cells | onnxruntime-cpu 450; tensorrt-cuda 450 |

| Sample | onnxruntime-cpu ms | tensorrt-cuda ms | Ratio | onnxruntime-cpu cells | tensorrt-cuda cells | Generated SHA same |
| --- | ---: | ---: | ---: | ---: | ---: | :---: |
| `08852562005685.png` | 39,477.58 | 22,401.91 | 0.567 | 0 | 0 | no |
| `two_col_100414.png` | 31,613.90 | 17,860.36 | 0.565 | 36 | 36 | yes |
| `00978071004853.png` | 44,771.03 | 25,129.61 | 0.561 | 0 | 0 | yes |
| `00006834003066.png` | 50,504.07 | 27,702.91 | 0.549 | 0 | 0 | yes |
| `08760254021671.png` | 34,243.10 | 18,622.12 | 0.544 | 11 | 11 | yes |
| `OECD_FERTILITY_RATES_ARG_IDN_000080.png` | 35,764.42 | 18,837.54 | 0.527 | 0 | 0 | yes |
| `OECD_HOUSEHOLD_NET_WORTH_CZE_EST_FIN_LUX_SWE_000030.png` | 49,399.47 | 25,221.33 | 0.511 | 0 | 0 | yes |
| `two_col_100542.png` | 88,572.22 | 44,996.04 | 0.508 | 49 | 49 | yes |
| `OECD_ENTERPRISES_BY_BUSINESS_SIZE_BEL_BGR_CZE_NOR_SWE_000003.png` | 28,828.48 | 14,602.51 | 0.507 | 0 | 0 | no |
| `OECD_CURRENT_ACCOUNT_BALANCE_FORECAST_BEL_DEU_DNK_NOR_USA_000049.png` | 51,861.73 | 26,142.34 | 0.504 | 0 | 0 | yes |
| `OECD_CAESAREAN_SECTIONS_CZE_KOR_000034.png` | 24,899.41 | 12,396.19 | 0.498 | 0 | 0 | yes |
| `02348983021349.png` | 39,378.51 | 19,556.33 | 0.497 | 0 | 0 | yes |
| `07474185020345.png` | 20,520.08 | 10,166.91 | 0.495 | 10 | 10 | yes |
| `two_col_100172.png` | 23,138.15 | 11,000.26 | 0.475 | 24 | 24 | yes |
| `OECD_GOVERNMENT_PRODUCTION_COSTS_EST_FRA_LTU_LVA_POL_000018.png` | 22,467.18 | 10,492.64 | 0.467 | 0 | 0 | yes |
| `two_col_100128.png` | 30,802.87 | 14,359.76 | 0.466 | 42 | 42 | yes |
| `OECD_HOUSEHOLDS_WITH_BROADBAND_ACCESS_CAN_SVN_USA_000027.png` | 27,890.47 | 12,760.12 | 0.458 | 0 | 0 | yes |
| `00484591006451.png` | 21,869.47 | 9,483.71 | 0.434 | 22 | 22 | yes |
| `OECD_FDI_RESTRICTIVENESS_LVA_SVN_000012.png` | 18,400.82 | 7,538.32 | 0.410 | 1 | 1 | yes |
| `two_col_100050.png` | 20,709.59 | 8,164.95 | 0.394 | 11 | 11 | yes |
| `OECD_CRUDE_OIL_IMPORT_PRICES_PRT_000090.png` | 22,208.07 | 8,713.21 | 0.392 | 0 | 0 | yes |
| `two_col_100022.png` | 18,260.87 | 7,143.84 | 0.391 | 20 | 20 | yes |
| `two_col_100229.png` | 17,634.74 | 6,645.21 | 0.377 | 12 | 12 | yes |
| `two_col_100521.png` | 23,250.68 | 8,747.03 | 0.376 | 16 | 16 | yes |
| `two_col_100412.png` | 13,874.89 | 5,153.79 | 0.371 | 14 | 14 | yes |
| `two_col_100197.png` | 17,269.23 | 6,315.28 | 0.366 | 15 | 15 | yes |
| `two_col_100430.png` | 23,830.37 | 8,483.00 | 0.356 | 22 | 22 | yes |
| `04902692006383.png` | 19,901.00 | 7,033.82 | 0.353 | 0 | 0 | yes |
| `01676320003804.png` | 21,924.47 | 7,691.22 | 0.351 | 16 | 16 | yes |
| `two_col_100411.png` | 13,364.48 | 4,600.45 | 0.344 | 12 | 12 | yes |
| `04769019006609.png` | 11,858.14 | 3,900.98 | 0.329 | 8 | 8 | yes |
| `OECD_DAILY_SMOKERS_CZE_IRL_LUX_LVA_SVN_000004.png` | 12,959.88 | 4,260.56 | 0.329 | 0 | 0 | yes |
| `two_col_100223.png` | 14,786.57 | 4,582.99 | 0.310 | 10 | 10 | yes |
| `08825558022322.png` | 11,944.77 | 3,601.44 | 0.302 | 10 | 10 | yes |
| `two_col_100478.png` | 32,978.57 | 9,487.04 | 0.288 | 18 | 18 | yes |
| `02348983021411.png` | 9,967.35 | 2,625.89 | 0.263 | 6 | 6 | yes |
| `03250329017073.png` | 10,801.83 | 2,750.68 | 0.255 | 0 | 0 | yes |
| `OECD_BROAD_MONEY_(M3)_CHN_CRI_DNK_TUR_ZAF_000026.png` | 13,282.81 | 3,373.46 | 0.254 | 8 | 8 | yes |
| `two_col_100534.png` | 11,126.04 | 2,622.96 | 0.236 | 10 | 10 | yes |
| `OECD_EXPORTS_BY_BUSINESS_SIZE_HUN_IRL_LUX_000009.png` | 8,786.18 | 2,068.25 | 0.235 | 6 | 6 | yes |
| `00108924006058.png` | 8,890.10 | 2,024.52 | 0.228 | 4 | 4 | yes |
| `OECD_IMPORTS_BY_BUSINESS_SIZE_LVA_NZL_PRT_ROU_000002.png` | 11,867.07 | 2,641.70 | 0.223 | 8 | 8 | yes |
| `04301786022389.png` | 10,060.83 | 2,160.85 | 0.215 | 6 | 6 | yes |
| `OECD_GROSS_PENSION_REPLACEMENT_RATES_HUN_MEX_000001.png` | 8,349.43 | 1,658.48 | 0.199 | 4 | 4 | yes |
| `05257096003209.png` | 10,167.81 | 1,945.30 | 0.191 | 2 | 2 | yes |
| `two_col_100509.png` | 19,146.12 | 3,417.87 | 0.179 | 8 | 8 | yes |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 24,872.01 | 3,981.06 | 0.160 | 5 | 5 | yes |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 15,859.56 | 2,062.91 | 0.130 | 4 | 4 | yes |

Backends can agree on EOS while disagreeing on table structure or cells; these dimensions are kept separate. Timing ratios are host- and runtime-specific observations only.
