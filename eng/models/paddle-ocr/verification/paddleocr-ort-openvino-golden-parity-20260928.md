# PaddleOCR ORT/OpenVINO 金样合同验证（2026-09-28）

## 结论

本次本机外部模型验证的 4 项测试全部通过：

| 项目 | 结果 |
| --- | --- |
| 测试总数 | 4 |
| 通过 | 4 |
| 失败 | 0 |
| 跳过 | 0 |
| 后端 | ONNX Runtime CPU、OpenVINO CPU |
| 测试时间 | 19.6 s（测试框架报告） |

这组测试证明了选定 PaddleOCR v5 模型和官方金样在两个 CPU 后端上的 DeploySharp 运行时合同与解码语义一致。它不是数据集级准确率、性能排名，也不代表 OpenCV DNN、TensorRT 或所有 v4/v5/v6 组合已经完成验证。

## 覆盖范围

测试类为 `Stage20PaddleOcrThreeModelParityTests`，使用本机外部模型与仓库中的固定金样文件：

1. **完整文本 OCR**：检测、文本行方向分类、识别和结果合并在 ORT/OpenVINO 上分别执行，比较区域数量、源索引、方向类别、方向拒绝标记、识别文本、置信度和四边形坐标。
2. **识别与方向官方金样**：比较 v5 Mobile Recognition 与方向分类的输入张量哈希、识别文本、CTC 发射 token、方向类别和置信度。识别金样文本为 `绿洲仕格维花园公寓`。
3. **Server DB 检测官方金样**：比较 v5 Server 检测的四个官方四边形及 ORT/OpenVINO 结果区域数量。
4. **Server Recognition/Orientation 官方金样**：比较 Server 识别的文本/token/置信度，以及 Server 方向分类的类别、方向和置信度。

合同中的主要容差为：完整流水线识别置信度 `0.001`、顶点坐标 `0.25 px`；官方 DB 金样四边形的最大循环顶点距离 `3 px`（Server DB 为 `4 px`）；方向和识别金样同时检查固定类别、文本、token 序列和置信度。

测试还校验了固定图像、输入张量、字典及官方输出的 SHA-256。具体 SHA-256 常量保存在测试源文件中，避免在文档中复制一份容易漂移的清单。

## 复现

在仓库 `DeploySharp` 目录执行：

```powershell
$env:DEPLOYSHARP_STAGE20_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~Stage20PaddleOcrThreeModelParityTests' `
  --logger 'console;verbosity=normal'
```

默认路径来自测试代码：

- `E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_mobile_det.onnx`
- `E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_mobile_cls.onnx`
- `E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_mobile_rec.onnx`
- `E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_server_det.onnx`
- `E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_server_cls.onnx`
- `E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_server_rec.onnx`
- `E:\Model\paddleocr\PP-OCRv5\ppocrv5_dict.txt`

如模型或数据位于其他位置，使用测试类支持的 `DEPLOYSHARP_STAGE20_*` 环境变量覆盖路径。未设置 `DEPLOYSHARP_STAGE20_RUN_EXTERNAL=1` 时，测试会保持 Inconclusive，不会误报为通过。

## 证据边界

- 这是固定金样上的后端合同和语义一致性证据，不是公开数据集 CER/WER 或检测召回率。
- 结果比较的是当前本地模型、字典、预处理和后处理组合；更换模型文件、字典、opset 或运行时版本后需要重新执行。
- 本记录不改变 PaddleOCR 模型目录中关于 OpenCV DNN、TensorRT、跨版本全量矩阵和 PP-Structure 任务级验证的未完成状态。
- 测试构建仍可能输出仓库既有旧目标框架警告；本轮未修改无关目标框架或依赖。
