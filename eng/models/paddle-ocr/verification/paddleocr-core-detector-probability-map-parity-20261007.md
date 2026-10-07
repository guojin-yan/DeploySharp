# PP-OCR v4/v5/v6 DET 概率图跨后端对照

日期：2026-10-07。本轮将 ORT CPU / OpenVINO CPU 的原始 DB 概率图差异采集扩展到本地 v4、v5、v6 的七个核心检测器，在同一 HierText `sample-003` holdout 上逐图比较。它验证的是两个执行后端的张量数值合同，不是模型准确率、性能排名，也不评选哪个模型更好。

## 复现范围

- 模型：PP-OCRv4 Mobile/Server、PP-OCRv5 Mobile/Server、PP-OCRv6 Tiny/Small/Medium；每个模型都对照其 catalog 固定的 ONNX SHA-256，完整 SHA 见机器报告。
- 后端：ONNX Runtime CPU 对 OpenVINO CPU；同一模型、同一图像只预处理一次，两个 Session 顺序使用同一个 Float32 tensor 实例，且推理后重新核验输入 SHA 未改变。
- 数据：本地 `F:\OCRBenchmarkTesting` 的 `hiertext-validation-sample-003.jsonl`，24 张唯一图像；manifest SHA-256 为 `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`。数据、标注和图像预测不随仓库分发。
- 总计 168 个 detector-image 对、135,790,592 个 Float32 输出元素。各对后端输出必须同 shape、有限值；通过门限为每个模型的最大绝对差不超过 `0.01`。另按 `PaddleDbPostprocessOptions` 默认概率阈值 `0.3` 统计跨阈值像素。
- 该选择仍是固定 smoke 子集，且不能据此评估检测召回/精度。Dataset 的授权和标注粒度限制继续以各数据源审计为准。

## 结果

| 模型 | 图像数 | 最大绝对差 | 平均绝对差 | `p >= 0.3` 跨阈值像素 | DB 区域差异（无序 polygon 匹配） |
|---|---:|---:|---:|---:|---|
| PP-OCRv4 Mobile DET | 24 | `2.16961e-4` | `1.75108e-8` | 1 | 区域数/几何集合均一致 |
| PP-OCRv4 Server DET | 24 | `6.89444e-3` | `1.72878e-7` | 3 | 一页有 1 个边界框坐标有微小变化；max `0.0181 px`，集合最低匹配 IoU `0.99915` |
| PP-OCRv5 Mobile DET | 24 | `3.87490e-4` | `5.88080e-8` | 0 | 区域数/几何集合均一致 |
| PP-OCRv5 Server DET | 24 | `1.77085e-4` | `2.88523e-8` | 1 | 一页 ORT 47 / OpenVINO 49；47 个 ORT 区域全部与 OpenVINO polygon 以 IoU `1.0` 匹配，另有 2 个 OpenVINO 区域 |
| PP-OCRv6 Tiny DET | 24 | `5.12779e-4` | `1.90468e-7` | 2 | 区域数/几何集合均一致 |
| PP-OCRv6 Small DET | 24 | `8.53539e-5` | `9.69556e-8` | 3 | 区域数/几何集合均一致 |
| PP-OCRv6 Medium DET | 24 | `1.07229e-4` | `7.53122e-8` | 1 | 区域数/几何集合均一致 |

七个模型均通过当前 `0.01` 张量漂移门限。所有模型的 24/24 页原始输出 SHA 都不同，说明跨后端并非 bitwise 相等；135,790,592 个输出值中有 11 个像素跨过默认 `0.3` 阈值（约 `0.0000081%`）。对正式 `PaddleDbTextDetectionDecoder` 输出还作了逐页检测 polygon 对照：v4 Mobile、v5 Mobile、v6 Tiny/Small/Medium 的区域数和几何集合一致；v4 Server 只有一个几何集合中的框轻微偏移；v5 Server 有两个额外区域，需单独注意。

差异定位：

- PP-OCRv4 Server 的最大概率绝对差 `0.00689444` 出现在 `c2291a13c825966f.jpg`，但平均差为 `1.73e-7`。该页区域数仍为 `30/30`，只有 1 个 polygon 坐标变化，最大坐标偏移 `0.0180054` 像素，最小一对一 greedy polygon IoU 为 `0.9991524`。三个跨阈值像素都接近 0.3；其中最大差落在概率约 `0.314/0.321`，不属于阈值边界点。
- PP-OCRv5 Server 的最大 raw probability 差只有 `1.77085e-4`。图像 `291e9c3df3a9ffb5.jpg` 有一个像素由 ORT `0.30000448` 与 OpenVINO `0.29999554` 跨过 `0.3`；区域数由 `47` 变为 `49`。忽略输出排序做 polygon 一对一最大 IoU 贪心配对后，47/47 个 ORT polygon 与 OpenVINO polygon 完全重合（IoU `1.0`），OpenVINO 另有 2 个 unmatched polygon。数值和空间对应关系与微小 DB 连通域/候选敏感性相符，但本实验没有证明该单个像素是唯一起因；这些额外区域无真值标注，不能称为误检。

以上差异是运行时合同诊断，不是 recall/precision 或人工质量评测。阈值 mask 相近也不能替代人工标注结果；v4/v5 Server 上的局部差异必须在更大、标注粒度合适的数据上继续复核。

## 复现

```powershell
$env:DEPLOYSHARP_PADDLEOCR_DET_PARITY_RUN_EXTERNAL = "1"
$env:DEPLOYSHARP_PADDLEOCR_DET_PARITY_HOLDOUT_EVIDENCE_PATH = (Join-Path (Get-Location) "eng/models/paddle-ocr/verification/paddleocr-core-detector-probability-map-parity-20261007.json")
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore `
  --filter "FullyQualifiedName~AllCoreDetectorsHaveProbabilityMapParityOnTheHierTextHoldout"
```

本机运行环境为 Windows build `26200`、.NET `10.0.12`、ONNX Runtime `1.28.0`、OpenVINO `2026.2.1`；七模型×24 页、带正式 DB decoder 和无序 polygon 匹配的 opt-in 测试本轮复跑 `1/1` 通过，用时 `3m41s`。这是集成回归耗时，不是推理延迟基准；没有采集 GPU、warmup、多轮 P50/P95 或频率遥测。

机器报告记录了 7 个模型 SHA、24 页图像 SHA、manifest SHA、逐页输入/输出 SHA、shape、差异指标、阈值方向和运行环境：[JSON](paddleocr-core-detector-probability-map-parity-20261007.json)。本次运行发生在 `45932e1` 基线的 dirty working tree 上，JSON 保留 `sourceWorkingTreeDirty=true`；测试源和后续实现由其后的定向提交固定。v6 Small 的独立 DB 阈值调参仍见[参数敏感性报告](paddleocr-v6-small-db-threshold-sensitivity-20261007.md)。
