# PP-OCRv6 Small DET TensorRT 11 动态 Batch 原始图证据

本记录验证 PP-OCRv6 Small DB 检测图在 TensorRT 11 动态 Batch 下的原始输出行隔离。测试使用 `E:\Data\ocr\demo_1.jpg` 的左右两个不重叠 ROI，分别缩放为 `[1,3,736,736]` 后组成 `[2,3,736,736]`，并在 ORT CPU 与 TensorRT CUDA 上执行。

## 结果

| 项目 | 结果 |
| --- | --- |
| 模型 | `paddleocr/ppocrv6/small-det` |
| ONNX SHA-256 | `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e` |
| 输入 | `[2,3,736,736]`，左右两个不同 ROI |
| ORT 输出 | `[2,1,736,736]` |
| TensorRT 输出 | `[2,1,736,736]` |
| 两行输入/输出 | SHA 均不同，行未复用 |
| ORT↔TensorRT 最大/平均绝对差 | `0.00690505 / 2.43208e-05` |
| 测试 | `1 passed / 0 failed / 0 skipped` |

两个后端输出均为有限值，TensorRT raw 概率图漂移低于本测试 `0.05` 合同。

## 重现

```powershell
$env:DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_RUN_EXTERNAL='1'
$env:DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_REPORT_PATH='E:\GitSpace\DeploySharp-V2.0\DeploySharp\eng\models\paddle-ocr\verification\paddleocr-v6-small-det-tensorrt-dynamic-batch-20261011.json'
$env:JYPPX_NATIVE_BRIDGE_PATH=(Resolve-Path 'artifacts/local-model-benchmarks/tensorrt11-bridge/runtimes/win-x64/native').Path
$env:JYPPX_TENSORRT_ROOT='D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT='C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT='D:\Program Files\cuDNN-9.22.0-cuda12.9'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release --no-restore --framework net10.0 --filter FullyQualifiedName~PaddleOcrDetectionTensorRtDynamicBatchIntegrationTests
```

机器可读结果见 [JSON 报告](paddleocr-v6-small-det-tensorrt-dynamic-batch-20261011.json)。

## 边界

这是原始图级/provider 动态 Batch 证据，不是检测召回、CER/WER、完整 OCR 流水线、吞吐或全部 TensorRT 支持证明。当前 `PaddleDbTextDetectionDecoder` 明确要求 `context.Input.BatchSize == 1`；要开放生产 DET batch decoder，还需要为每个 batch 行定义源图尺寸、缩放/stride、阈值后轮廓和坐标回映射合同。本记录因此只将 v6 Small DET 的 raw TensorRT 动态 Batch 标记为已验证，不改变完整 DET 解码状态。
