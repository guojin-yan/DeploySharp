# PP-OCRv4 TensorRT 11 engine provider evidence

本记录复核 PP-OCRv4 Server DET/REC 和 legacy CLS 的 TensorRT 11 bridge。前一次失败是当前进程没有注入匹配的 bridge、TensorRT、CUDA 和 cuDNN 路径；按项目脚本显式配置后，三个已有 Engine 均能由 DeploySharp provider 加载并完成一次非空输出推理。

## 环境

- Windows x64，.NET 10，CUDA 12.9，cuDNN 9.22。
- TensorRT：`11.0.0.114-cu12`。
- bridge：仓库本地 `artifacts/local-model-benchmarks/tensorrt11-bridge/runtimes/win-x64/native/jyppxtrtbridge.dll`。
- 测试：`TensorRt11BridgeLoadsAndRunsV4ServerEngines`，结果 `3/3` 通过。

## 精确 Engine

| 模型 | Engine SHA-256 | 输入 | 状态 |
| --- | --- | --- | --- |
| `paddleocr/ppocrv4/server-det` | `8fcfef452cccfa9f2e49db145f44bbef39b2808f94f616a3ad9ac7fac2c44687` | `[1,3,736,736]` | `passed` |
| `paddleocr/ppocrv4/server-rec` | `d3f005eea95a36c7b18eb1c5133cfde161f4d8d5105e3e1a12739cbeb42c12cc` | `[1,3,48,160]` | `passed` |
| `paddleocr/ppocrv4/legacy-cls` | `f19db888144ef5f05a5d090950ca22a0b2eee470c39caf3f1961faf0e1c3b3a3` | `[1,3,48,192]` | `passed` |

复现时需要把 `JYPPX_NATIVE_BRIDGE_PATH`、`JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT`、`JYPPX_CUDNN_ROOT` 指向同一套 ABI/runtime，然后运行：

```powershell
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
dotnet test .\tests\DeploySharp.Backend.TensorRT.Tests\DeploySharp.Backend.TensorRT.Tests.csproj `
  -f net10.0 -c Release --no-restore `
  --filter 'FullyQualifiedName~TensorRt11BridgeLoadsAndRunsV4ServerEngines'
```

该证据把 `paddleocr/ppocrv4/legacy-cls` 的 TensorRT 状态从 `△` 提升为精确 Engine 的 `✓`。它只证明已有序列化 Engine 在这台设备上可加载并返回输出；不等同于从 ONNX 重新构建、完整 OCR 流程质量、5/50 性能或跨设备可移植性。

机器可读详情见 [`paddleocr-v4-server-tensorrt11-engine-provider-20261011.json`](paddleocr-v4-server-tensorrt11-engine-provider-20261011.json)。
