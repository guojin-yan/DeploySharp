# PaddleOCR benchmark matrix

Generated from complete-pipeline CSV reports. P50/P95 are copied from the raw reports; no mean-to-percentile conversion is performed.

| Model | Backend | Device | Status | Batch | Channels | Regions | Mean ms | P50 ms | P95 ms | Preprocess ms | Detection ms | Recognition ms | Input | Result SHA-256 |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
| v4-mobile | onnxruntime | cpu | pass | 4 | 1 | 16 | 578.617 | 568.249 | 779.946 | 0.000 | 76.189 | 436.369 | E:\Data\ocr\demo_1.jpg | `75640159b90fed0a3c1d4b1c75fd65c8a5395452d251c2c9823894e4931a0003` |
| v4-mobile | opencv-dnn | cpu | pass | 4 | 1 | 16 | 923.967 | 884.736 | 1153.383 | 0.000 | 224.823 | 649.449 | E:\Data\ocr\demo_1.jpg | `75640159b90fed0a3c1d4b1c75fd65c8a5395452d251c2c9823894e4931a0003` |
| v4-mobile | openvino | CPU | pass | 4 | 1 | 16 | 106.976 | 104.091 | 121.125 | 0.000 | 17.317 | 75.197 | E:\Data\ocr\demo_1.jpg | `75640159b90fed0a3c1d4b1c75fd65c8a5395452d251c2c9823894e4931a0003` |
| v4-server | onnxruntime | cpu | pass | 4 | 1 | 16 | 1819.450 | 1753.192 | 2162.025 | 0.000 | 611.676 | 1151.907 | E:\Data\ocr\demo_1.jpg | `8f5eafe1fb1fa34c18532cdfd6d561aedf55424e8d361da236c8a337d45507ab` |
| v4-server | opencv-dnn | cpu | pass | 4 | 1 | 16 | 2660.223 | 2665.845 | 2791.708 | 0.000 | 1294.218 | 1313.611 | E:\Data\ocr\demo_1.jpg | `8f5eafe1fb1fa34c18532cdfd6d561aedf55424e8d361da236c8a337d45507ab` |
| v4-server | openvino | CPU | pass | 4 | 1 | 16 | 1256.366 | 1235.854 | 1406.849 | 0.000 | 535.200 | 705.055 | E:\Data\ocr\demo_1.jpg | `8f5eafe1fb1fa34c18532cdfd6d561aedf55424e8d361da236c8a337d45507ab` |
| v5-mobile | onnxruntime | cpu | pass | 4 | 1 | 16 | 519.234 | 504.098 | 676.879 | 0.000 | 74.730 | 385.078 | E:\Data\ocr\demo_1.jpg | `baf43652d258399fb82c0b53502c9ba8a913bca17636af576d828822132642e8` |
| v5-mobile | opencv-dnn | cpu | pass | 4 | 1 | 16 | 1052.806 | 1042.101 | 1164.467 | 0.000 | 208.664 | 814.094 | E:\Data\ocr\demo_1.jpg | `baf43652d258399fb82c0b53502c9ba8a913bca17636af576d828822132642e8` |
| v5-mobile | openvino | CPU | pass | 4 | 1 | 16 | 219.168 | 209.052 | 297.226 | 0.000 | 31.655 | 165.677 | E:\Data\ocr\demo_1.jpg | `baf43652d258399fb82c0b53502c9ba8a913bca17636af576d828822132642e8` |
| v5-server | onnxruntime | cpu | pass | 4 | 1 | 16 | 1584.452 | 1571.850 | 1919.373 | 0.000 | 461.737 | 968.632 | E:\Data\ocr\demo_1.jpg | `d7399516e9006c93099aafc8e87c7a05e17e1603be979254ea4c124da92af45c` |
| v5-server | opencv-dnn | cpu | pass | 4 | 1 | 16 | 3523.117 | 3361.151 | 4733.211 | 0.000 | 1250.242 | 2123.291 | E:\Data\ocr\demo_1.jpg | `d7399516e9006c93099aafc8e87c7a05e17e1603be979254ea4c124da92af45c` |
| v5-server | openvino | CPU | pass | 4 | 1 | 16 | 1093.959 | 1037.821 | 1413.895 | 0.000 | 456.596 | 565.775 | E:\Data\ocr\demo_1.jpg | `d7399516e9006c93099aafc8e87c7a05e17e1603be979254ea4c124da92af45c` |
| v6-medium | onnxruntime | cpu | pass | 4 | 1 | 16 | 1145.070 | 1102.336 | 1430.392 | 0.000 | 214.447 | 930.576 | E:\Data\ocr\demo_1.jpg | `538ee815a79b62453d828c1488f1a7b9c36d2477e4c6a1e5c7f5d77892c41675` |
| v6-medium | opencv-dnn | cpu | pass | 4 | 1 | 16 | 2281.336 | 2281.962 | 2428.063 | 0.000 | 635.344 | 1645.979 | E:\Data\ocr\demo_1.jpg | `538ee815a79b62453d828c1488f1a7b9c36d2477e4c6a1e5c7f5d77892c41675` |
| v6-medium | openvino | CPU | pass | 4 | 1 | 16 | 665.767 | 636.905 | 771.943 | 0.000 | 148.865 | 516.883 | E:\Data\ocr\demo_1.jpg | `538ee815a79b62453d828c1488f1a7b9c36d2477e4c6a1e5c7f5d77892c41675` |
| v6-small | onnxruntime | cpu | pass | 4 | 1 | 16 | 459.898 | 468.182 | 545.636 | 0.000 | 77.150 | 382.728 | E:\Data\ocr\demo_1.jpg | `538ee815a79b62453d828c1488f1a7b9c36d2477e4c6a1e5c7f5d77892c41675` |
| v6-small | opencv-dnn | cpu | pass | 4 | 1 | 16 | 973.849 | 874.086 | 1527.337 | 0.000 | 237.259 | 736.570 | E:\Data\ocr\demo_1.jpg | `538ee815a79b62453d828c1488f1a7b9c36d2477e4c6a1e5c7f5d77892c41675` |
| v6-small | openvino | CPU | pass | 4 | 1 | 16 | 225.570 | 219.314 | 266.676 | 0.000 | 39.288 | 186.266 | E:\Data\ocr\demo_1.jpg | `538ee815a79b62453d828c1488f1a7b9c36d2477e4c6a1e5c7f5d77892c41675` |
| v6-tiny | onnxruntime | cpu | pass | 4 | 1 | 16 | 251.740 | 223.148 | 473.327 | 0.000 | 84.115 | 167.605 | E:\Data\ocr\demo_1.jpg | `e95e3d376d700eb44dd1b7a848c624d97fae3161204b193f2495f4bc4720cc0c` |
| v6-tiny | opencv-dnn | cpu | pass | 4 | 1 | 16 | 348.755 | 344.571 | 388.449 | 0.000 | 154.425 | 194.314 | E:\Data\ocr\demo_1.jpg | `e95e3d376d700eb44dd1b7a848c624d97fae3161204b193f2495f4bc4720cc0c` |
| v6-tiny | openvino | CPU | pass | 4 | 1 | 16 | 93.572 | 81.230 | 160.828 | 0.000 | 27.841 | 65.709 | E:\Data\ocr\demo_1.jpg | `e95e3d376d700eb44dd1b7a848c624d97fae3161204b193f2495f4bc4720cc0c` |

## Environment records

| Report | Machine | OS | Framework | Architecture | CPU count | Source revision | Input SHA-256 | Warmup | Iterations | Batch | Stage channels | TensorRT API |
| --- | --- | --- | --- | --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: |
| E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\paddleocr-core-3backend-demo1-20260924.csv | JYPPX | Microsoft Windows NT 10.0.26200.0 | .NET 10.0.12 | X64 | 16 | unrecorded | `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c` | 5 | 50 | 4 | 1 | 11 |

