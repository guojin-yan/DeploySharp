# Chart2Table onnxruntime-cpu / openvino-cpu evidence alignment

This report compares the same 48 `val` samples at ChartQA revision `044eabfc306abfe9340c5741f0093aefc5973d06` with a 1024-token generation budget. It does not rerun inference and does not claim split accuracy or a controlled benchmark.

| Measure | Result |
| --- | ---: |
| Image SHA matches | 48/48 |
| Source table / expected text SHA matches | 48/48; 48/48 |
| Generated table SHA matches | 48/48 |
| Finish reason / token count matches | 48/48; 48/48 |
| EOS | onnxruntime-cpu 48/48; openvino-cpu 48/48 |
| Expected / generated dimensions match | 48/48; 48/48 |
| Parseable / structure / exact-text flags match | 48/48; 48/48; 48/48 |
| Expected / actual cell counts match | 48/48; 48/48 |
| Exact rows | onnxruntime-cpu 205; openvino-cpu 205 |
| Exact cells | onnxruntime-cpu 450; openvino-cpu 450 |

| Sample | onnxruntime-cpu ms | openvino-cpu ms | Ratio | onnxruntime-cpu cells | openvino-cpu cells | Generated SHA same |
| --- | ---: | ---: | ---: | ---: | ---: | :---: |
| `01676320003804.png` | 21,924.47 | 36,405.31 | 1.660 | 16 | 16 | yes |
| `00978071004853.png` | 44,771.03 | 69,679.70 | 1.556 | 0 | 0 | yes |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 15,859.56 | 18,857.02 | 1.189 | 4 | 4 | yes |
| `OECD_GOVERNMENT_PRODUCTION_COSTS_EST_FRA_LTU_LVA_POL_000018.png` | 22,467.18 | 24,850.23 | 1.106 | 0 | 0 | yes |
| `two_col_100414.png` | 31,613.90 | 34,792.18 | 1.101 | 36 | 36 | yes |
| `04769019006609.png` | 11,858.14 | 12,819.57 | 1.081 | 8 | 8 | yes |
| `02348983021411.png` | 9,967.35 | 10,745.01 | 1.078 | 6 | 6 | yes |
| `04301786022389.png` | 10,060.83 | 10,662.76 | 1.060 | 6 | 6 | yes |
| `OECD_GROSS_PENSION_REPLACEMENT_RATES_HUN_MEX_000001.png` | 8,349.43 | 8,601.02 | 1.030 | 4 | 4 | yes |
| `two_col_100412.png` | 13,874.89 | 14,247.72 | 1.027 | 14 | 14 | yes |
| `07474185020345.png` | 20,520.08 | 20,966.22 | 1.022 | 10 | 10 | yes |
| `03250329017073.png` | 10,801.83 | 10,815.66 | 1.001 | 0 | 0 | yes |
| `08760254021671.png` | 34,243.10 | 34,068.20 | 0.995 | 11 | 11 | yes |
| `OECD_CAESAREAN_SECTIONS_CZE_KOR_000034.png` | 24,899.41 | 24,641.35 | 0.990 | 0 | 0 | yes |
| `02348983021349.png` | 39,378.51 | 38,936.33 | 0.989 | 0 | 0 | yes |
| `08852562005685.png` | 39,477.58 | 38,986.23 | 0.988 | 0 | 0 | yes |
| `OECD_FDI_RESTRICTIVENESS_LVA_SVN_000012.png` | 18,400.82 | 17,999.19 | 0.978 | 1 | 1 | yes |
| `OECD_ENTERPRISES_BY_BUSINESS_SIZE_BEL_BGR_CZE_NOR_SWE_000003.png` | 28,828.48 | 28,163.11 | 0.977 | 0 | 0 | yes |
| `00006834003066.png` | 50,504.07 | 49,304.27 | 0.976 | 0 | 0 | yes |
| `two_col_100411.png` | 13,364.48 | 13,033.68 | 0.975 | 12 | 12 | yes |
| `OECD_DAILY_SMOKERS_CZE_IRL_LUX_LVA_SVN_000004.png` | 12,959.88 | 12,503.08 | 0.965 | 0 | 0 | yes |
| `OECD_FERTILITY_RATES_ARG_IDN_000080.png` | 35,764.42 | 34,319.00 | 0.960 | 0 | 0 | yes |
| `two_col_100172.png` | 23,138.15 | 22,201.01 | 0.959 | 24 | 24 | yes |
| `OECD_EXPORTS_BY_BUSINESS_SIZE_HUN_IRL_LUX_000009.png` | 8,786.18 | 8,019.75 | 0.913 | 6 | 6 | yes |
| `two_col_100229.png` | 17,634.74 | 15,973.61 | 0.906 | 12 | 12 | yes |
| `00108924006058.png` | 8,890.10 | 8,025.80 | 0.903 | 4 | 4 | yes |
| `two_col_100128.png` | 30,802.87 | 27,729.46 | 0.900 | 42 | 42 | yes |
| `two_col_100050.png` | 20,709.59 | 18,473.49 | 0.892 | 11 | 11 | yes |
| `two_col_100022.png` | 18,260.87 | 16,226.01 | 0.889 | 20 | 20 | yes |
| `04902692006383.png` | 19,901.00 | 17,589.34 | 0.884 | 0 | 0 | yes |
| `two_col_100197.png` | 17,269.23 | 15,238.26 | 0.882 | 15 | 15 | yes |
| `OECD_CURRENT_ACCOUNT_BALANCE_FORECAST_BEL_DEU_DNK_NOR_USA_000049.png` | 51,861.73 | 45,417.02 | 0.876 | 0 | 0 | yes |
| `OECD_HOUSEHOLDS_WITH_BROADBAND_ACCESS_CAN_SVN_USA_000027.png` | 27,890.47 | 24,217.40 | 0.868 | 0 | 0 | yes |
| `05257096003209.png` | 10,167.81 | 8,789.71 | 0.864 | 2 | 2 | yes |
| `two_col_100534.png` | 11,126.04 | 9,594.67 | 0.862 | 10 | 10 | yes |
| `08825558022322.png` | 11,944.77 | 10,236.79 | 0.857 | 10 | 10 | yes |
| `OECD_CRUDE_OIL_IMPORT_PRICES_PRT_000090.png` | 22,208.07 | 18,977.64 | 0.855 | 0 | 0 | yes |
| `two_col_100223.png` | 14,786.57 | 12,600.26 | 0.852 | 10 | 10 | yes |
| `00484591006451.png` | 21,869.47 | 18,411.46 | 0.842 | 22 | 22 | yes |
| `two_col_100542.png` | 88,572.22 | 74,540.63 | 0.842 | 49 | 49 | yes |
| `OECD_HOUSEHOLD_NET_WORTH_CZE_EST_FIN_LUX_SWE_000030.png` | 49,399.47 | 41,268.80 | 0.835 | 0 | 0 | yes |
| `two_col_100430.png` | 23,830.37 | 19,362.20 | 0.813 | 22 | 22 | yes |
| `two_col_100521.png` | 23,250.68 | 18,875.35 | 0.812 | 16 | 16 | yes |
| `OECD_IMPORTS_BY_BUSINESS_SIZE_LVA_NZL_PRT_ROU_000002.png` | 11,867.07 | 9,155.20 | 0.771 | 8 | 8 | yes |
| `OECD_BROAD_MONEY_(M3)_CHN_CRI_DNK_TUR_ZAF_000026.png` | 13,282.81 | 9,740.62 | 0.733 | 8 | 8 | yes |
| `two_col_100478.png` | 32,978.57 | 20,712.44 | 0.628 | 18 | 18 | yes |
| `two_col_100509.png` | 19,146.12 | 10,332.03 | 0.540 | 8 | 8 | yes |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 24,872.01 | 11,531.28 | 0.464 | 5 | 5 | yes |

Backends can agree on EOS while disagreeing on table structure or cells; these dimensions are kept separate. Timing ratios are host- and runtime-specific observations only.
