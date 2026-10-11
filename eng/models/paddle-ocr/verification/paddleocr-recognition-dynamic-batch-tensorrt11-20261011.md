# PP-OCR REC TensorRT 11 动态 Batch=2 证据

在同一 Windows RTX 3060 Laptop、TensorRT `11.0.0.114-cu12`、CUDA `12.9`、cuDNN `9.22` 和匹配 bridge 环境下，为 PP-OCRv5 Mobile/Server REC 与 PP-OCRv6 Tiny/Small/Medium REC 建立 `min=[1,3,48,320]`、`opt=max=[2,3,48,320]` 的 TensorRT profile，并以 `[2,3,48,320]` 输入实际执行。测试 `1/1` 通过，五个模型均保留输出 batch=2。

| 模型 | Engine SHA-256 | 输入 | 输出形状 | 输出元素 |
|---|---|---|---|---:|
| `paddleocr/ppocrv5/mobile-rec` | `75c3d000c74f6555eff795c22ac91e9068153828b96c1e26c49467fd47853113` | `x[2,3,48,320]` | `[2,40,18385]` | 1,470,800 |
| `paddleocr/ppocrv5/server-rec` | `d55aa745a3cfec4b76c52da63f39fe3a45be7f5a00c5dec7fcf8859f498661cc` | `x[2,3,48,320]` | `[2,40,18385]` | 1,470,800 |
| `paddleocr/ppocrv6/tiny-rec` | `977e4ceb9bdd061ee2e5e15f3ad991c6715f7d5b8f11fdcc99540e94b56b4e70` | `x[2,3,48,320]` | `[2,40,6906]` | 552,480 |
| `paddleocr/ppocrv6/small-rec` | `83ea9adb6301b6fc1fb3ce990f360f242210c32aedd3d15b1a23a62f9da503e4` | `x[2,3,48,320]` | `[2,40,18710]` | 1,496,800 |
| `paddleocr/ppocrv6/medium-rec` | `9ec533747ab92db3f7741d30ce468a80f68d05a156e59be278189d5aedf4ec57` | `x[2,3,48,320]` | `[2,40,18710]` | 1,496,800 |

复现前设置 `DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1`、`DEPLOYSHARP_TENSORRT_API_VERSION=11`、`JYPPX_NATIVE_BRIDGE_PATH`、`JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT` 和 `JYPPX_CUDNN_ROOT`，然后运行：

```powershell
dotnet test .\tests\DeploySharp.Backend.TensorRT.Tests\DeploySharp.Backend.TensorRT.Tests.csproj -c Release --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrRecognitionModelsRunDynamicBatchTwoOnTensorRt11'
```

该证据只证明 TensorRT profile、输入绑定和 raw 输出的 batch 行隔离；没有执行 CTC 解码、文本 parity、完整 `det → crop → cls → rec` 流程或吞吐基准，因此不等价于 OCR 质量或生产性能通过。机器记录见 [`paddleocr-recognition-dynamic-batch-tensorrt11-20261011.json`](paddleocr-recognition-dynamic-batch-tensorrt11-20261011.json)。
