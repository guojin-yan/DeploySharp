# Chart2Table OpenVINO 12-image quality extension (2026-10-11)

This is a bounded quality extension for the pinned ChartQA `val` selection at revision `044eabfc306abfe9340c5741f0093aefc5973d06`. It reuses the same 12 image/table pairs and four-graph ONNX bundle as the ORT record [`chart2table-extended-quality-ort-20261004.json`](chart2table-extended-quality-ort-20261004.json). The images and CSV tables remain in the local validation cache and are not redistributed.

OpenVINO CPU completed all `12/12` generations with `EndOfSequence`. It produced `9/12` matching row/column structures, `3/12` exact generated tables and `140/293` exact cells. Every generated table SHA matched the ORT 12-image record, so the cross-backend output agreement is `12/12`; this is an alignment and task-diagnostic result, not ChartQA split accuracy.

| Sample | Cells | Structure | Total ms |
| --- | ---: | :---: | ---: |
| `OECD_ADULT_EDUCATION_LEVEL_CZE_NZL_000011.png` | 4/6 | yes | 18,394.96 |
| `OECD_AGRICULTURAL_SUPPORT_COL_IND_JPN_KOR_NZL_000003.png` | 5/12 | yes | 16,412.10 |
| `OECD_BROAD_MONEY_(M3)_CHN_CRI_DNK_TUR_ZAF_000026.png` | 8/10 | yes | 13,095.59 |
| `OECD_CAESAREAN_SECTIONS_CZE_KOR_000034.png` | 0/27 | no | 40,041.59 |
| `two_col_100022.png` | 20/20 | yes | 27,410.24 |
| `two_col_100050.png` | 11/16 | yes | 31,081.40 |
| `two_col_100128.png` | 42/42 | yes | 44,446.07 |
| `two_col_100172.png` | 24/24 | yes | 30,885.95 |
| `00006834003066.png` | 0/56 | no | 78,195.87 |
| `00108924006058.png` | 4/6 | yes | 8,856.23 |
| `00484591006451.png` | 22/24 | yes | 29,616.29 |
| `00978071004853.png` | 0/50 | no | 75,507.60 |

The machine-readable per-sample hashes, token counts, timing and aggregate boundary are in [`chart2table-extended-quality-openvino-20261011.json`](chart2table-extended-quality-openvino-20261011.json). The four graph SHA-256 values are recorded there to make the result reproducible. The reported P50/P95 total latency is `29,616.2891/78,195.865 ms`; it is a single-host observation with a `1024` new-token cap, not a locked-clock performance benchmark.

## Reproduction

Use the existing ChartQA cache and bundle paths from [`chart2table-extended-quality-20261002.md`](chart2table-extended-quality-20261002.md), set `DEPLOYSHARP_CHART2TABLE_EXTENDED_OPENVINO_RUN_EXTERNAL=1`, set `DEPLOYSHARP_CHART2TABLE_EXTENDED_OPENVINO_MAX_NEW_TOKENS=1024`, and select a backend-specific report path such as `artifacts/chart2table-openvino-extended-quality-{backend}-20261011.json`. Run:

```powershell
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleChart2TableExtendedQualityExternalIntegrationTests
```

This record closes the previous missing-report observation for the 12-image OpenVINO attempt. It does not close OpenCV full autoregressive support, split-level accuracy, TensorRT portability or repeated locked-clock performance.
