# PaddleOCR 质量证据状态增量索引（2026-10-18）

## 使用方式与边界

这是对[2026-10-14 基线索引](paddleocr-quality-status-20261014.md)的增量，不替代历史报告，也不把局部运行证据升级为“全部模型、全部后端已支持”。原始图片、标注和逐条预测仍保留在外部数据目录，不复制到仓库。

## 本轮新增证据

| 范围 | 数据/协议 | 后端与结果 | 状态 | 不能推出的结论 |
|---|---|---|---|---|
| PP-OCR v4/v5/v6 七个核心识别器 | HierText validation `sample-002`/`sample-003`；256 个 source-linked、非空、非 ignore 文本行 crop；显式跳过 DET，调用 `RecognizeOnlyAsync` | ORT CPU、OpenVINO CPU：`14/14` 组合完成，每组合 256 行；v6 Small CER/WER `25.0000%/48.1663%`，v6 Medium `23.0658%/45.2323%`；逐行文本 SHA `3584/3584` 一致 | 诊断通过 | 不代表检测召回、页面阅读顺序、自然连续 `>=3,200` 字符、自然角度、退化鲁棒性、TensorRT/OpenCV DNN/GPU 质量或发布准确率 |

证据详情：

- [Markdown 报告](paddleocr-core-recognizer-quality-hiertext-20261018.md)
- [机器可读 JSON](paddleocr-core-recognizer-quality-hiertext-20261018.json)
- [测试入口](../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrCoreRecognizerQualityExternalIntegrationTests.cs)

## 当前状态快照

- **代码/执行合同**：PP-OCR v4/v5/v6 核心 DET/CLS/REC、Session 池和异步预取已有精确模型合同；本轮新增的是识别器质量诊断，不改变默认后端或模型支持枚举。
- **后端矩阵**：PP-Structure 的精确资产矩阵仍以 [`paddle-document-backend-matrix-current-20261017.json`](../../paddle-document/verification/paddle-document-backend-matrix-current-20261017.json) 为准；其中 `pass` 只适用于登记的精确模型、runtime、输入和 Decoder 合同。
- **性能矩阵**：正式 `5 warm-up + 50 measured` 证据仍是设备/模型/后端绑定的观察值，不能替代第二设备、锁频和长时间稳定性验证。

## 尚未关闭的质量门

1. A2：合法、可归因的自然连续 `>=3,200` 字符标注与真实 CER/WER/接缝门；当前自然宽行最长标注文本为 182 字符。
2. A3：自然角度、倒置和倾斜真值，以及 repair/rejection/CER/WER 门；受控旋转合同不等价于自然分布。
3. B1b/B2/B3：独立自然低质量 split、增强误纠率和延迟预算；默认增强继续关闭。
4. 公式：数学语义/渲染质量门；现有 ORT CPU/CUDA 和 MathJax/数值诊断均不构成语义等价证明。
5. Chart2Table：更大规模或官方 split accuracy；24 图 bundle 证据仍是有界任务诊断。
6. 全矩阵与第二设备：其余精确模型×后端、Linux/RTX 2060 的 DeploySharp 运行证据、锁频对照和长时间 soak。

该增量索引只改变文档导航，不改变任何 `pass`、`unsupported` 或 `unverified` 状态。新增证据必须继续绑定模型/字典 SHA、输入选择、runtime、协议、结果和明确边界。
