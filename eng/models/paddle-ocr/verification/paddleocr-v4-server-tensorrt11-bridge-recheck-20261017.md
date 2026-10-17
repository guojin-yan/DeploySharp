# PP-OCRv4 Server TensorRT 11 bridge recheck (2026-10-17)

本次复核确认：此前的 v4 Server bridge 失败是测试进程没有注入匹配的 native bridge、TensorRT、CUDA 和 cuDNN 路径，不是三个 Engine 的 `deserializeCudaEngine` 或 DeploySharp provider 失败。显式配置同一套运行时后，Release 配置测试 `1/1` 通过，三个精确 Engine 均加载并返回非空输出绑定。

## 复现环境

- Windows x64，.NET 10。
- TensorRT `11.0.0.114-cu12`：`D:\Program Files\TensorRT-11.0.0.114-cu12`。
- CUDA `12.9`：`C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9`。
- cuDNN `9.22`：`D:\Program Files\cuDNN-9.22.0-cuda12.9`。
- DeploySharp bridge：`artifacts/local-model-benchmarks/tensorrt11-bridge/runtimes/win-x64/native/jyppxtrtbridge.dll`，SHA-256 `a94d1e5fe4454c9402979ae050f7a64d74c7f51bd6bb2d74936531f608a9ef6f`。

运行测试前，必须把 `JYPPX_NATIVE_BRIDGE_PATH`、`JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT`、`JYPPX_CUDNN_ROOT` 指向上述匹配路径，并把 bridge、TensorRT、CUDA、cuDNN 的 native 目录置于当前进程 `PATH` 前部：

```powershell
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLEOCR_ROOT = 'E:\Model\paddleocr'
$env:JYPPX_NATIVE_BRIDGE_PATH = 'E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
dotnet test .\tests\DeploySharp.Backend.TensorRT.Tests\DeploySharp.Backend.TensorRT.Tests.csproj `
  -f net10.0 -c Release --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrV4ServerTensorRtIntegrationTests'
```

## 结果

| 精确模型 | Engine SHA-256 | 输入 | 结果 |
| --- | --- | --- | --- |
| `paddleocr/ppocrv4/server-det` | `8fcfef452cccfa9f2e49db145f44bbef39b2808f94f616a3ad9ac7fac2c44687` | `[1,3,736,736]` | `passed` |
| `paddleocr/ppocrv4/server-rec` | `d3f005eea95a36c7b18eb1c5133cfde161f4d8d5105e3e1a12739cbeb42c12cc` | `[1,3,48,160]` | `passed` |
| `paddleocr/ppocrv4/legacy-cls` | `f19db888144ef5f05a5d090950ca22a0b2eee470c39caf3f1961faf0e1c3b3a3` | `[1,3,48,192]` | `passed` |

## 边界

这次复核关闭的是“缺少匹配 DLL 导致 bridge 无法启动”的环境问题，并不等于完整 `det → crop → cls → rec → merge` OCR 质量通过。它也不证明 ONNX→Engine 可重复构建、5+50 性能、Engine 跨 GPU/Runtime 可移植性或其它 TensorRT/CUDA 组合支持。若再次出现 `DllNotFoundException`，应先检查进程级 bridge/runtime 注入，而不是把错误归因于 Engine 反序列化。

机器可读结果见 [`paddleocr-v4-server-tensorrt11-bridge-recheck-20261017.json`](paddleocr-v4-server-tensorrt11-bridge-recheck-20261017.json)。
