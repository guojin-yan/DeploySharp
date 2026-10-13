# PaddleOCR 质量证据状态索引（2026-10-14）

## 目的与边界

这是一份**诊断证据索引**，把当前仓库中可复现的 PaddleOCR 真实图像/真实标注证据集中列出，帮助使用者区分“代码可以执行”“后端结果可比较”和“模型质量门已经通过”。它不是准确率排行榜，也不把某一个模型、后端或单台设备上的 smoke 结果外推为全部 PaddleOCR/PP-Structure 支持。

所有记录都绑定了模型资产、数据选择、源码 revision 和运行协议；原始图片、标注和逐条预测保留在本机 `F:\OCRBenchmarkTesting` 或其它外部缓存中，不复制进仓库。除非特别注明，以下结果来自 Windows 主机 `JYPPX`，并且一次测量协议不能替代正式 `5 warm-up + 50 measured` 性能协议。

状态含义：

- **诊断通过**：在记录的模型、数据和后端组合上完成执行，并满足该报告定义的输出/Parity 合同。
- **受控诊断**：测试证明了策略或退化处理的执行行为，但数据构造或标注边界不足以关闭质量门。
- **部分通过**：完成了运行，但存在后端文本差异、粒度不匹配或其它报告中明确的边界。
- **未完成**：当前没有足够的合法、可归因或跨后端证据，不能宣称支持。

## 已有真实证据

| 范围 | 数据与协议 | 后端/结果 | 状态 | 不能推出的结论 |
|---|---|---|---|---|
| PP-OCR v4/v5/v6 核心七模型 | SROIE 10 页；`det → crop → optional cls → rec → merge`；SlidingWindow；7 个模型各自有独立报告 | ORT CPU、OpenVINO CPU：14/14 个模型-后端组合、每组合 10/10 页完成；文本序列与区域索引按各报告记录 | 诊断通过 | SROIE 是词框标注而流水线输出文本行；不能作为官方行级准确率，也不覆盖 CUDA、TensorRT、OpenCV DNN 或 PP-Structure |
| v6 Small REC（识别-only） | HierText sample-002/003；1,557 条真实关联、非空、非 ignore text-line crop | ORT/OpenVINO：1,557/1,557；exact `713/1557`，CER `25.4278%`，WER `46.9777%`；逐行文本 `1557/1557` 一致 | 诊断通过 | 不能推出完整页面检测召回、自然长文本、鲁棒性或其它后端质量 |
| v6 Medium 完整页 | HierText 两个不重叠选集共 34 页；SlidingWindow；batch 16（OpenCV batch 1） | ORT/OpenVINO：34/34，页面文本序列一致；OpenCV：34/34 执行，但仅 11/34 页与 ORT 文本序列一致，1,368 个对应区域中 178 个文本不同 | 部分通过 | OpenCV 的 importer/文本差异是当前后端边界；不是质量排名或正式性能结论 |
| v6 Small 完整页三后端 | SROIE 10 页；batch 1 以兼容静态 OpenCV 合同 | ORT/OpenVINO/OpenCV：10/10 页；531 个区域、页面文本序列全部一致；观察 P50/P95 为 `1302.87/2534.95 ms`、`779.76/1054.15 ms`、`7722.31/10131.55 ms` | 诊断通过 | 只覆盖 v6 Small、该十页和一次测量；不外推其它模型或 5/50 性能 |
| FUNSD v6 Small | 40 页；batch 16、两通道、SlidingWindow | ORT/OpenVINO：40/40；1,978 个区域，逐页有序文本无差异 | 诊断通过 | FUNSD 是 word-level 标注而模型输出 text-line；IoU/CER/WER 仅是粒度敏感诊断 |
| FUNSD v4 Mobile | 同一 40 页与协议 | ORT/OpenVINO：40/40；2,357 个区域；39/40 页有序文本一致，1 页存在索引文本差异 | 部分通过 | 不能写成严格跨后端 parity 或发布准确率 |
| 方向 retry 策略 | 4 个真实 SROIE 页的 32 个 parent-linked crop，构造 8 个已知角度；候选 `90°/180°/-90°` | ORT/OpenVINO：32/32 执行；右角可比较样本 16/16 选中预期方向，逐行文本/方向结果一致 | 受控诊断 | 不是自然旋转分布准确率；阈值 `1`、零增益只用于诊断，不能作为生产默认 |
| 受控退化与增强 | SROIE/FUNSD parent-linked crop/page 的 blur、噪声、对比度、阴影、JPEG 等变体；另有 240 条三配方共识证据 | ORT/OpenVINO 执行和决策一致；共识候选存在 CER 改善也存在恶化，5 折安全阈值全部弃权 | 受控诊断 | 不能证明自然低质量鲁棒性、默认增强收益或业务字段误纠率；增强继续 opt-in |

## 当前仍未关闭的质量门

1. **正式质量门**：需要与输出粒度匹配的独立标注集、明确的检测/识别阈值和可复现的 acceptance criteria；现有 SROIE/FUNSD 结果只能作为 smoke/诊断。
2. **A2 自然长文本**：当前 HierText 选集最长自然连续行约 132 字符，没有合法、可归因的自然连续 `>=3,200` 字符样本；合成拼接不能替代自然标注。
3. **A3 自然角度/倒置**：HierText 旋转元数据与 OCR 标注没有有效 ID 交集；受控旋转、竖排候选和 retry 合同不能替代自然角度真值、repair/rejection rate 与自然 CER/WER。
4. **B1b/B2/B3 退化质量和成本**：已有受控退化运行，但没有独立自然低质量验证 split、业务字段误纠门和正式成本预算，因此默认增强保持关闭。
5. **正式性能协议**：质量记录中的 one-shot P50/P95 不能替代按固定设备、锁频/遥测、`5 warm-up + 50 measured`、批量/通道配置记录的统一性能矩阵。
6. **未覆盖后端/模型**：CUDA、TensorRT、OpenCV DNN 的结果必须按精确模型和 runtime 单独准入；已有某一代表模型的证据不扩展到全部 v4/v5/v6 或 PP-Structure 模型。公式、SLANeXt、Chart2Table 等任务还保留各自的 importer、生成质量或语义质量边界。

## 复现入口

- 核心七模型汇总：[SROIE 十页汇总](sroie-core-seven-models-10page-quality-summary-20261010.md)
- 行级识别：[HierText v6 Small REC](hiertext-line-recognition-v6-small-20261010.md)
- 三后端完整页：[HierText v6 Medium 34 页](hiertext-v6-medium-34page-three-backend-quality-20261003.md)
- FUNSD：[v6 Small 40 页](funsd-v6-small-40page-ort-openvino-quality-20261010.md)、[v4 Mobile 40 页](funsd-v4-mobile-40page-ort-openvino-quality-20261010.md)
- 三后端 SROIE：[v6 Small](sroie-v6-small-10page-three-backend-quality-20261009.md)
- 方向策略：[A3 受控 retry](sroie-angle-orientation-retry-a3-20261014.md)

机器可读索引见 [`paddleocr-quality-status-20261014.json`](paddleocr-quality-status-20261014.json)。新增证据必须同时登记数据集/选择、模型与字典 SHA、后端/runtime、协议、结果和不能推出的边界；没有这些字段时只能标记为未完成，不能升级为支持结论。

本轮维护范围的 Markdown 链接审计为 [721 条、断链 0](../../paddle-document/verification/document-link-audit-20261014-quality-status.json)；外部 URL、数据集路径和运行时下载地址不在该审计范围内。
