# PP-OCR v4/v5/v6 DET 概率图跨后端对照

日期：2026-10-07。本轮将 ORT CPU / OpenVINO CPU 的原始 DB 概率图差异采集扩展到本地 v4、v5、v6 的七个核心检测器，在同一 HierText `sample-003` holdout 上逐图比较。它验证的是两个执行后端的张量数值合同，不是模型准确率、性能排名，也不评选哪个模型更好。

## 复现范围

- 模型：PP-OCRv4 Mobile/Server、PP-OCRv5 Mobile/Server、PP-OCRv6 Tiny/Small/Medium；每个模型都对照其 catalog 固定的 ONNX SHA-256，完整 SHA 见机器报告。
- 后端：ONNX Runtime CPU 对 OpenVINO CPU；同一模型、同一图像只预处理一次，两个 Session 顺序使用同一个 Float32 tensor 实例，且推理后重新核验输入 SHA 未改变。
- 数据：本地 `F:\OCRBenchmarkTesting` 的 `hiertext-validation-sample-003.jsonl`，24 张唯一图像；manifest SHA-256 为 `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`。数据、标注和图像预测不随仓库分发。
- 总计 168 个 detector-image 对、135,790,592 个 Float32 输出元素。各对后端输出必须同 shape、有限值；通过门限为每个模型的最大绝对差不超过 `0.01`。另按 `PaddleDbPostprocessOptions` 默认概率阈值 `0.3` 统计跨阈值像素。
- 该选择仍是固定 smoke 子集，且不能据此评估检测召回/精度。Dataset 的授权和标注粒度限制继续以各数据源审计为准。

## 结果

| 模型 | 图像数 | 最大绝对差 | 平均绝对差 | `p >= 0.3` 跨阈值像素 | ORT only / OpenVINO only |
|---|---:|---:|---:|---:|---:|
| PP-OCRv4 Mobile DET | 24 | `2.16961e-4` | `1.75108e-8` | 1 | 1 / 0 |
| PP-OCRv4 Server DET | 24 | `6.89444e-3` | `1.72878e-7` | 3 | 1 / 2 |
| PP-OCRv5 Mobile DET | 24 | `3.87490e-4` | `5.88080e-8` | 0 | 0 / 0 |
| PP-OCRv5 Server DET | 24 | `1.77085e-4` | `2.88523e-8` | 1 | 1 / 0 |
| PP-OCRv6 Tiny DET | 24 | `5.12779e-4` | `1.90468e-7` | 2 | 1 / 1 |
| PP-OCRv6 Small DET | 24 | `8.53539e-5` | `9.69556e-8` | 3 | 3 / 0 |
| PP-OCRv6 Medium DET | 24 | `1.07229e-4` | `7.53122e-8` | 1 | 1 / 0 |

七个模型均通过当前 `0.01` 张量漂移门限。所有模型的 24/24 页原始输出 SHA 都不同，说明跨后端并非 bitwise 相等；但 135,790,592 个输出值中只有 11 个像素跨过默认 `0.3` 阈值（约 `0.0000081%`）。这只能说明该运行矩阵上的阈值 mask 基本一致，不能替代区域几何或人工标注质量比较。

PP-OCRv4 Server 的最大差值 `0.00689444` 显著高于其余模型，虽低于既有 `0.01` 门限，仍是本次观测到的数值离群项。其最大差出现在图像文件 `c2291a13c825966f.jpg`，且该模型总计有 3 个跨阈值像素；后续应优先用更大、标签适当的数据复核此模型的 DB 解码区域变化，不要把其余模型结果替它背书。

## 复现

```powershell
$env:DEPLOYSHARP_PADDLEOCR_DET_PARITY_RUN_EXTERNAL = "1"
$env:DEPLOYSHARP_PADDLEOCR_DET_PARITY_HOLDOUT_EVIDENCE_PATH = (Join-Path (Get-Location) "eng/models/paddle-ocr/verification/paddleocr-core-detector-probability-map-parity-20261007.json")
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore `
  --filter "FullyQualifiedName~AllCoreDetectorsHaveProbabilityMapParityOnTheHierTextHoldout"
```

本机运行环境为 Windows build `26200`、.NET `10.0.12`；七模型×24 页 opt-in 测试 `1/1` 通过，用时约 `3m37s`。这是集成回归耗时，不是推理延迟基准；没有采集 GPU、warmup、多轮 P50/P95 或频率遥测。

机器报告记录了 7 个模型 SHA、24 页图像 SHA、manifest SHA、逐页输入/输出 SHA、shape、差异指标、阈值方向和运行环境：[JSON](paddleocr-core-detector-probability-map-parity-20261007.json)。本次运行发生在 `45932e1` 基线的 dirty working tree 上，JSON 保留 `sourceWorkingTreeDirty=true`；测试源和后续实现由其后的定向提交固定。v6 Small 的独立 DB 阈值调参仍见[参数敏感性报告](paddleocr-v6-small-db-threshold-sensitivity-20261007.md)。
