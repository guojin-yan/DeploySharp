# Chart2Table OpenVINO multi-image regression (2026-09-30 rerun)

The same four curated ChartQA human samples used for the ORT regression were executed with the four-graph ONNX bundle on OpenVINO CPU (`maximumNewTokens=256`).

| Sample | EOS tokens | Total ms | Decode P50/P95 ms | Structure row/cell | Output SHA == expected SHA |
| --- | ---: | ---: | ---: | --- | --- |
| `png_41699051005347.png` | 168 | 49,083.4 | 145.79 / 175.62 | 1.00 / 1.00 | yes |
| `png_41810321001157.png` | 34 | 11,918.5 | 118.55 / 148.82 | 1.00 / 1.00 | yes |
| `png_8127.png` | 40 | 12,329.6 | 112.59 / 123.15 | 1.00 / 1.00 | yes |
| `png_166.png` | 77 | 20,209.2 | 116.49 / 131.47 | 1.00 / 1.00 | yes |

All four runs finished by EOS and reproduced their expected tables byte-for-byte. The structural evaluator reports exact row and cell matches for all four tables. Full SHA, structure and stage timing evidence is in [chart2table-openvino-multi-image-20260929.json](chart2table-openvino-multi-image-20260929.json). This is a four-sample qualitative regression, not a full ChartQA accuracy score; OpenCV DNN autoregressive generation remains unverified.

Use the same reproduction command as the ORT multi-image test, set `DEPLOYSHARP_CHART2TABLE_OPENVINO_MULTI_RUN_EXTERNAL=1`, set `DEPLOYSHARP_CHART2TABLE_REPORT_PATH` to the OpenVINO JSON path above, and change the test filter to `PaddleChart2TableMultiImageOrtExternalIntegrationTests` (the class now covers both backends).
