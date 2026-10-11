# PP-OCRv5 Mobile CLS TensorRT 11 动态 Batch 解码一致性

本记录验证 PP-OCRv5 Mobile 文本行方向分类模型在 TensorRT 11 动态 Batch 下的真实解码合同。测试使用 `E:\Data\ocr\demo_1.jpg`，先由 PP-OCRv6 Small DET 生成两个真实文本区域，再使用同一个 `[2,3,80,160]` batch 分别运行 ORT CPU 和 TensorRT CUDA。

## 结果

| 项目 | 结果 |
| --- | --- |
| 模型 | `paddleocr/ppocrv5/mobile-cls` |
| ONNX SHA-256 | `dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2` |
| 输入 | `[2,3,80,160]`，两行均来自真实 DET crop |
| ORT 输出 | `[2,2]` |
| TensorRT 输出 | `[2,2]` |
| batch 顺序/类别/角度/拒绝决策 | `2/2` 行一致 |
| 最大归一化分数差 | `2.955857780762017E-11` |
| 最大置信度差 | `0` |
| 测试 | `1 passed / 0 failed / 0 skipped` |

两行均解码为 `Degrees0`，置信度分别为 `0.9999949` 和 `0.9999995`。原始 ORT/TensorRT 输出 SHA 不同，但差异仅为浮点执行路径差异，语义解码合同满足。

## 运行环境

- Windows 10.0.26200 x64
- NVIDIA RTX 3060 Laptop GPU
- TensorRT `11.0.0.114-cu12`
- CUDA `12.9`
- cuDNN `9.22`
- DeploySharp TensorRT bridge：`artifacts/local-model-benchmarks/tensorrt11-bridge/runtimes/win-x64/native/jyppxtrtbridge.dll`

## 重现

在仓库 `DeploySharp` 目录执行：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_TRT_PARITY_RUN_EXTERNAL='1'
$env:DEPLOYSHARP_PADDLEOCR_TRT_CLS_PARITY_REPORT_PATH='E:\GitSpace\DeploySharp-V2.0\DeploySharp\eng\models\paddle-ocr\verification\paddleocr-v5-mobile-cls-tensorrt-dynamic-batch-parity-20261011.json'
$env:JYPPX_NATIVE_BRIDGE_PATH=(Resolve-Path 'artifacts/local-model-benchmarks/tensorrt11-bridge/runtimes/win-x64/native').Path
$env:JYPPX_TENSORRT_ROOT='D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT='C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT='D:\Program Files\cuDNN-9.22.0-cuda12.9'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release --no-restore --framework net10.0 --filter FullyQualifiedName~PaddleOcrOrientationTensorRtParityIntegrationTests
```

机器可读结果见 [JSON 报告](paddleocr-v5-mobile-cls-tensorrt-dynamic-batch-parity-20261011.json)。

## 边界

这是一台 Windows 主机上的两个真实检测裁剪的 CLS 解码证据，不是 CER/WER、完整 `det→crop→cls→rec` 质量评测、吞吐基准，也不代表 PP-OCR 全部模型或跨设备 TensorRT 已全部通过。DET 解码器当前仍是 batch=1 合同；本记录只证明 CLS 动态 Batch 与方向解码在该组合下的一致性。
