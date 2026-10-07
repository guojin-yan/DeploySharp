# PP-OCRv6 Small DB 参数敏感性（HierText smoke）

日期：2026-10-07。此报告评估 DB 检测解码器的三个可配置参数，不修改库默认值，也不是发布精度结论。

## 复现范围

- 模型：`paddleocr/ppocrv6/small-det`，`PP-OCRv6_small_det_inference.onnx`，SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`。
- 后端：ONNX Runtime CPU 和 OpenVINO CPU，各自独立运行。
- 数据：`F:\OCRBenchmarkTesting` 中 `hiertext-validation-sample-002.jsonl`（10 页调参）与 `hiertext-validation-sample-003.jsonl`（24 页独立 holdout）；清单 SHA、图像 SHA、输入张量 SHA、原始输出 SHA、每页候选区域数及解码几何摘要保存在同目录 JSON 报告。
- 每张图每个后端只执行一次模型推理；同一原始 DB 概率图被复用于所有解码候选，避免把模型执行差异混入阈值对比。
- 按调参集的 aggregate IoU@0.5 F1 选择候选；并列优先保留默认值。仅将调参集排名第一的候选带入 sample-003，与默认值比较。
- 数据使用限制：HierText 图片为 CC-BY-2.0、标注为 CC-BY-SA-4.0；本地选择仍属 smoke-only，遵守各源授权，不随项目重新分发数据。

本机复现（需要上述本地模型和 `F:\OCRBenchmarkTesting` 数据清单）：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_DB_SWEEP = "1"
$env:DEPLOYSHARP_PADDLEOCR_DB_SWEEP_OUTPUT = "eng/models/paddle-ocr/verification"
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --filter "FullyQualifiedName~PaddleOcrDbThresholdSensitivityIntegrationTests"
```

## 调参集结果

基线参数为 `probabilityThreshold=0.30`、`boxThreshold=0.60`、`unclipRatio=1.50`。每次仅改变一个参数。

| 后处理候选 | IoU@0.5 TP/FP/FN | Precision | Recall | F1 | IoU@0.75 F1 |
|---|---:|---:|---:|---:|---:|
| 默认值 | 152 / 215 / 385 | 0.4142 | 0.2831 | 0.3363 | 0.0420 |
| probability 0.25 | 143 / 207 / 394 | 0.4086 | 0.2663 | 0.3224 | 0.0316 |
| probability 0.35 | 155 / 219 / 382 | 0.4144 | 0.2886 | 0.3403 | 0.0439 |
| box 0.55 | 161 / 227 / 376 | 0.4149 | 0.2998 | 0.3481 | 0.0432 |
| box 0.65 | 142 / 199 / 395 | 0.4164 | 0.2644 | 0.3235 | 0.0342 |
| unclip 1.30 | 176 / 191 / 361 | 0.4796 | 0.3277 | **0.3894** | **0.0730** |
| unclip 1.70 | 124 / 243 / 413 | 0.3379 | 0.2309 | 0.2743 | 0.0199 |

两后端的检测计数与 F1 汇总完全一致，因此按事先固定规则选择 `unclipRatio=1.30`。

## 独立 holdout

| 后端 | 参数 | IoU@0.5 TP/FP/FN | Precision | Recall | F1 | IoU@0.75 F1 |
|---|---|---:|---:|---:|---:|---:|
| ORT CPU | 默认值 | 436 / 377 / 584 | 0.5363 | 0.4275 | 0.4757 | 0.1113 |
| ORT CPU | unclip 1.30 | 509 / 304 / 511 | 0.6261 | 0.4990 | **0.5554** | **0.1626** |
| OpenVINO CPU | 默认值 | 436 / 377 / 584 | 0.5363 | 0.4275 | 0.4757 | 0.1113 |
| OpenVINO CPU | unclip 1.30 | 509 / 304 / 511 | 0.6261 | 0.4990 | **0.5554** | **0.1626** |

在此固定 holdout 上，unclip 1.30 的 IoU@0.5 F1 比默认值高约 0.0797，IoU@0.75 F1 高约 0.0513。改善表现为召回增加且误检减少；该结果值得后续扩大验证，但两个小切片、单模型、单设备不足以证明该值对不同场景普遍更优，所以仍不改默认配置。

## 跨后端观察与代码修复

- 34/34 页的 ORT/OpenVINO 输入张量 SHA 和输出 shape 一致。
- 原始输出 SHA 为 0/34 完全相同；118/118 个候选解码摘要也不同，因此不能声称两后端原始概率输出逐值相同。原始概率图逐元素 max/mean absolute difference 尚未在这次阈值扫描中记录。
- 对照两个 JSON 报告中的检测几何：118/118 候选的区域数量与 `geometrySha256` 均一致；本次独立坐标比较还确认调参集与 holdout 合计 16,720 个对应多边形顶点（33,440 个 X/Y 标量）逐值一致。该固定工作负载上，微小输出数值差没有改变多边形几何；分数/完整解码指纹仍有差异。
- 使用本地官方 OCRBench `evaluate` 对两个后端的全部调参/holdout 候选（每个后端 9 组）独立复算 IoU@0.5/0.75 TP/FP/FN，全部与机器报告一致。
- 阈值扫描需要复用同一个后端输出。检查时发现 DB Decoder 会原地夹紧借用的 Float32 输出数组，违反 Visual Decoder 的只读借用合同，并会使后续候选看到被修改的数据。现改为只夹紧局部读取值；回归测试确认边界浮点值可容忍且输出数组在解码后保持原值。

## 结论和未完成项

1. 保持默认 `unclipRatio=1.5`；目前没有足够广泛、独立的数据证明应改成 1.3。
2. 若用户希望在相同 DB 解码合同下做应用侧试验，可通过 `PaddleDbPostprocessOptions` 显式传入候选值，不必改模型或推理后端。
3. 下一步应采集原始 ORT/OpenVINO DB 概率图逐元素差值，并用更多具备文本行四边形真值、来源与授权已审计的图像复核候选值；之后再覆盖 v4/v5/v6 其他 DET 模型。当前结果不代表 REC/CER/WER、端到端 OCR、多后端普遍性能或官方数据集成绩。

机器报告：

- [ORT CPU JSON](paddleocr-v6-small-db-threshold-sensitivity-onnxruntime-20261007T134054712Z.json)
- [OpenVINO CPU JSON](paddleocr-v6-small-db-threshold-sensitivity-openvino-20261007T134100984Z.json)
- 光学阈值扫描集成测试：`tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrDbThresholdSensitivityIntegrationTests.cs`，需要显式设置 `DEPLOYSHARP_PADDLEOCR_DB_SWEEP=1`。
