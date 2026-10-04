# PaddleOCR full-page degradation backend parity

- Left: `onnxruntime` (`042841ea0cfe4c9002c4c4c4a3680a4f83c42982c58decd468ff480bd945cb95`)
- Right: `openvino` (`92a2a7bf62bf08c35d98e156a2b51bd525abcaf2da7950b12b0ffbe6e5d6999c`)
- Images: `60`; status `60/60`; region count `60/60`
- Compared regions: `3169`; text mismatches `0` across `0` images
- Maximum polygon absolute difference: `0.0`; confidence absolute difference: `4.830000000000112e-05`

| Condition | Images | Region count | Text mismatched images | Text mismatches | Max polygon diff | Max confidence diff |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| blur | 10 | 10/10 | 0 | 0 | 0 | 4.83e-05 |
| jpeg | 10 | 10/10 | 0 | 0 | 0 | 3.084e-05 |
| low-contrast | 10 | 10/10 | 0 | 0 | 0 | 3.09e-05 |
| noise | 10 | 10/10 | 0 | 0 | 0 | 2.54e-05 |
| normal | 10 | 10/10 | 0 | 0 | 0 | 2.19e-05 |
| shadow | 10 | 10/10 | 0 | 0 | 0 | 1.584e-05 |

This is a prediction contract comparison only; it does not establish accuracy or a production performance ranking.
