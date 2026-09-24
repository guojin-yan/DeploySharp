# PaddleOCR benchmark matrix

Generated from complete-pipeline CSV reports. P50/P95 are copied from the raw reports; no mean-to-percentile conversion is performed.

| Model | Backend | Device | Status | Batch | Channels | Regions | Mean ms | P50 ms | P95 ms | Preprocess ms | Detection ms | Recognition ms | Input | Result SHA-256 |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
| v5-mobile | onnxruntime | cpu | pass | 4 | 1 | 16 | 772.901 | 770.591 | 1012.298 | 0.000 | 71.444 | 640.614 | E:\Data\ocr\demo_1.jpg | `baf43652d258399fb82c0b53502c9ba8a913bca17636af576d828822132642e8` |
| v5-mobile | onnxruntime | cpu | pass | 4 | 1 | 14 | 538.597 | 518.986 | 687.334 | 0.000 | 42.417 | 400.312 | E:\Data\ocr\demo_2.jpg | `2890aa486a16e964a64e45c74ab5818cfb0845ad849d6cd7d1269aa64f10a98e` |
| v5-mobile | onnxruntime | cpu | pass | 4 | 1 | 10 | 502.033 | 499.216 | 605.106 | 0.000 | 161.347 | 280.101 | E:\Data\ocr\demo_3.jpg | `770b4a8eb20008b0a30ec4057387f411d6ad319fbbb73772e8ba864681667831` |
| v5-mobile | opencv-dnn | cpu | pass | 4 | 1 | 16 | 1210.397 | 1196.332 | 1381.975 | 0.000 | 242.581 | 933.018 | E:\Data\ocr\demo_1.jpg | `baf43652d258399fb82c0b53502c9ba8a913bca17636af576d828822132642e8` |
| v5-mobile | opencv-dnn | cpu | pass | 4 | 1 | 14 | 1076.784 | 1043.650 | 1291.752 | 0.000 | 129.518 | 916.206 | E:\Data\ocr\demo_2.jpg | `2890aa486a16e964a64e45c74ab5818cfb0845ad849d6cd7d1269aa64f10a98e` |
| v5-mobile | opencv-dnn | cpu | pass | 4 | 1 | 10 | 884.252 | 883.373 | 956.017 | 0.000 | 448.460 | 415.046 | E:\Data\ocr\demo_3.jpg | `770b4a8eb20008b0a30ec4057387f411d6ad319fbbb73772e8ba864681667831` |
| v5-mobile | openvino | CPU | pass | 4 | 1 | 16 | 307.289 | 289.132 | 476.346 | 0.000 | 43.812 | 234.087 | E:\Data\ocr\demo_1.jpg | `baf43652d258399fb82c0b53502c9ba8a913bca17636af576d828822132642e8` |
| v5-mobile | openvino | CPU | pass | 4 | 1 | 14 | 228.297 | 196.740 | 453.098 | 0.000 | 27.806 | 171.519 | E:\Data\ocr\demo_2.jpg | `2890aa486a16e964a64e45c74ab5818cfb0845ad849d6cd7d1269aa64f10a98e` |
| v5-mobile | openvino | CPU | pass | 4 | 1 | 10 | 192.027 | 188.023 | 218.522 | 0.000 | 78.820 | 97.659 | E:\Data\ocr\demo_3.jpg | `770b4a8eb20008b0a30ec4057387f411d6ad319fbbb73772e8ba864681667831` |

## Environment records

| Report | Machine | OS | Framework | Architecture | CPU count | Source revision | Input SHA-256 | Warmup | Iterations | Batch | Stage channels | TensorRT API |
| --- | --- | --- | --- | --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: |
| E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\paddleocr-v5-mobile-3backend-demo1-20260924.csv | JYPPX | Microsoft Windows NT 10.0.26200.0 | .NET 10.0.12 | X64 | 16 | unrecorded | `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c` | 5 | 50 | 4 | 1 | 11 |
| E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\paddleocr-v5-mobile-3backend-demo2-20260924.csv | JYPPX | Microsoft Windows NT 10.0.26200.0 | .NET 10.0.12 | X64 | 16 | unrecorded | `957a9cc15da49312277796126be225e0ee653f3316578c12d626fa43fbe9561b` | 5 | 50 | 4 | 1 | 11 |
| E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\paddleocr-v5-mobile-3backend-demo3-20260924.csv | JYPPX | Microsoft Windows NT 10.0.26200.0 | .NET 10.0.12 | X64 | 16 | unrecorded | `f9353f07461e913b019f504f501ed7723a1c2164fdc667f095cc7e64cc7caa2d` | 5 | 50 | 4 | 1 | 11 |

