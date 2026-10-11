# PP-OCRv5/v6 核心模型 TensorRT 11 ONNX→Engine 证据

在 Windows x64、RTX 3060 Laptop GPU、TensorRT `11.0.0.114-cu12`、CUDA `12.9`、cuDNN `9.22` 和仓库内匹配的 `jyppxtrtbridge.dll` 环境下，使用 `TensorRtOnnxEngineBuilder` 对 PP-OCRv5 的六个核心工件和 PP-OCRv6 的六个 DET/REC 工件逐一重新构建 Engine，再由 DeploySharp TensorRT provider 加载并执行一次零填充输入。两个测试均通过：v5 `1/1`、v6 `1/1`；合计 `12/12` 模型完成 ONNX→Engine→provider smoke。

| 模型 | 输入 | ONNX SHA-256 | Engine SHA-256 | Engine bytes | 输出元素 |
|---|---|---|---|---:|---:|
| `paddleocr/ppocrv5/mobile-cls` | `x[1,3,80,160]` | `dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2` | `0e855e18ba0032a08909e07ad295ae8643341e01dd40431e44ecd467b06917c1` | 1,486,380 | 2 |
| `paddleocr/ppocrv5/mobile-det` | `x[1,3,736,736]` | `1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039` | `da423fbdd47585b204aca00a35dd44ca3d715a25a601607f28098781346724c9` | 6,706,844 | 541,696 |
| `paddleocr/ppocrv5/mobile-rec` | `x[1,3,48,320]` | `f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9` | `e1fa58d4f01d639d1c2dfb9c514234bba08722cb2285fbc30de706e882caea63` | 17,755,436 | 735,400 |
| `paddleocr/ppocrv5/server-cls` | `x[1,3,80,160]` | `38aa97cd4be591e0ad304e659f07ba30d946f27a63315433f6659c69c8778345` | `9633a043b8c86e4e6159febadbe1f25c1d868c16ced7dbbde3954767f5fa1651` | 7,257,716 | 2 |
| `paddleocr/ppocrv5/server-det` | `x[1,3,736,736]` | `0c4ff76f78feb3e4b4e9b3030df350234f570027e1ec9cfe5bbb7a596cb57f47` | `b617e413324e707b1c645c2bb63552b0839a850f2ccad40c8a440ebad455d62c` | 91,908,596 | 541,696 |
| `paddleocr/ppocrv5/server-rec` | `x[1,3,48,320]` | `12ec4f2b7266afcca07063786238e1538c8f19656cb2b6bbc0f4b9c492c2667a` | `91080c94d72318cbcf5e8c6cc4d4414a76a81c90f70636392063fd5295342ae3` | 87,197,716 | 735,400 |
| `paddleocr/ppocrv6/tiny-det` | `x[1,3,736,736]` | `193bab7a04fca699a6c82e6abb5b81bdb28177f0abd4062552b04908dafb19f8` | `2d2ca2bf48b83f22da5d0639d2f873b6fce388be4cafc5707b4e8b0a91de34e9` | 2,818,108 | 541,696 |
| `paddleocr/ppocrv6/tiny-rec` | `x[1,3,48,320]` | `9ef676d6ed3c88256a2d92c640c44f25b0c40947e111b14b8be8f594091563e6` | `dc499bc66c3537506433b00827533ba323b733a0217221cec098188187102643` | 5,081,460 | 276,240 |
| `paddleocr/ppocrv6/small-det` | `x[1,3,736,736]` | `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e` | `db60cc3fac48ab7b3441c433c32709d9e5fdfc61086af755fe0cd809fdd6e9a8` | 10,995,796 | 541,696 |
| `paddleocr/ppocrv6/small-rec` | `x[1,3,48,320]` | `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634` | `70005201848578f48c0c925bccd4346a93011a3f9a690a9699747481b2316d77` | 22,260,012 | 748,400 |
| `paddleocr/ppocrv6/medium-det` | `x[1,3,736,736]` | `eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1` | `ec9aef5b3a3834174b3530f44588067b9927cab4d0b5644925c960f8abc24427` | 63,527,076 | 541,696 |
| `paddleocr/ppocrv6/medium-rec` | `x[1,3,48,320]` | `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba` | `1b196eceac3337856499c45560def4b5658c7dc82008484cb3ff22b685b269bc` | 77,810,940 | 748,400 |

表中的 Engine SHA 是最后一次 `1 m 44 s`、`2/2` 测试运行生成的序列化计划哈希；TensorRT Builder 生成的计划不承诺跨次构建字节级稳定，复现时应以稳定的 ONNX SHA、运行时版本和输入合同为准。

复现前设置 `DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1`、`DEPLOYSHARP_TENSORRT_API_VERSION=11`、`JYPPX_NATIVE_BRIDGE_PATH`、`JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT` 和 `JYPPX_CUDNN_ROOT`，然后运行：

```powershell
dotnet test .\tests\DeploySharp.Backend.TensorRT.Tests\DeploySharp.Backend.TensorRT.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PaddleOcrV5OnnxModelsBuildAndRunTensorRt11Engines'
dotnet test .\tests\DeploySharp.Backend.TensorRT.Tests\DeploySharp.Backend.TensorRT.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~PaddleOcrV6OnnxModelsBuildAndRunTensorRt11Engines'
```

这是精确 ONNX→Engine→provider 的可运行性证据，不是完整 `det → crop → cls → rec` 质量、TensorRT P50/P95、跨设备 Engine 可移植性或发布准确率结论。机器可读记录见 [`paddleocr-v5-v6-core-tensorrt11-build-20261011.json`](paddleocr-v5-v6-core-tensorrt11-build-20261011.json)。
