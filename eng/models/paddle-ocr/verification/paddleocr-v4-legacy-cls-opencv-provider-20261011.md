# PP-OCRv4 legacy CLS OpenCV DNN provider evidence

本记录补齐 `paddleocr/ppocrv4/legacy-cls` 的独立 OpenCV DNN provider 证据。测试使用本机精确 ONNX 工件，在 Windows x64、.NET 10、CPU 上通过 DeploySharp provider 创建 Session，绑定真实输入并检查输出绑定和元素数量。

## 结果

| 项目 | 值 |
| --- | --- |
| 模型 | `paddleocr/ppocrv4/legacy-cls` |
| 模型 SHA-256 | `f4bb53707100c5f3d59ba834eb05bb400369f20aed35d4b26807b1bfadd2a70e` |
| 输入 | `x`, `[1,3,48,192]`, `float32` |
| 输出 | `softmax_0.tmp_0`, `[1,2]`, `float32` |
| 后端 | DeploySharp OpenCV DNN CPU |
| 结果 | `passed` |
| 测试耗时观察 | `247 ms`（含测试方法开销，不是推理性能基准） |

复现命令：

```powershell
$env:DEPLOYSHARP_OPENCV_RUN_EXTERNAL = '1'
dotnet test .\tests\DeploySharp.Backend.OpenCV.Tests\DeploySharp.Backend.OpenCV.Tests.csproj `
  -f net10.0 -c Release --no-restore `
  --filter 'FullyQualifiedName~PaddleOcrV4LegacyClassificationUsesDeploySharpOpenCvProvider'
```

这项证据只把该精确工件的 OpenCV DNN 状态从“未验证”提升为“已执行通过”。它不表示完整 OCR 质量、数值 parity、TensorRT 支持或跨设备性能已通过；TensorRT 仍需匹配的 bridge/runtime 独立验证。

机器可读详情见 [`paddleocr-v4-legacy-cls-opencv-provider-20261011.json`](paddleocr-v4-legacy-cls-opencv-provider-20261011.json)。
