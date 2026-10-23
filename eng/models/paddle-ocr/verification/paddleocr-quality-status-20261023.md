# PaddleOCR 质量证据状态增量索引（2026-10-23）

## 使用方式与边界

这是对[2026-10-18 状态索引](paddleocr-quality-status-20261018.md)的后续增量，后者又延续 2026-10-14 基线。本文只登记本轮自然来源收据 crop 增强共识诊断，不覆盖历史报告，不改变后端支持矩阵，也不把局部结果外推到所有模型/后端。原图、标注和逐 crop 内容继续保留在外部数据目录；仓库内 JSON 只保留来源、SHA、统计与文本摘要。

## 本轮新增证据

| 范围 | 数据/协议 | 后端与结果 | 状态 | 不能推出的结论 |
|---|---|---|---|---|
| PP-OCRv6 Small SROIE crop 增强共识 | 固定 SROIE revision；10 页、542 个词级真值区域；DET bypass；batch 16；三种顺序增强候选 | ORT CPU 1.28.0、OpenVINO CPU 2026.2.1；542/542 原文、候选、最终文本与决策完全一致；选中 353 行，CER 改善/恶化/相同 `4/5/344`，WER `3/4/346`；聚合 CER 编辑 `513→514`、WER 编辑 `232→233`。另穷举 ORT `348` / OpenVINO `350` 个非空候选增益切点，均无“零 CER 误纠且至少一项 CER 改善”的阈值；零误纠点仅留下 2 或 1 个 CER 持平区域 | 诊断完成，无总体收益 | 图像成像质量未标注，非自然低质量 split；事后同样本阈值扫描不能作为生产阈值；word crop 指标不是页面/行级准确率；不批准默认增强或业务阈值 |

详见[实验报告](paddleocr-sroie-natural-source-enhancement-20261023.md)、[逐区域机器 JSON](paddleocr-sroie-natural-source-enhancement-20261023.json) 和[外部测试入口](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrSroieNaturalEnhancementIntegrationTests.cs)。

## 当前开放门禁

下列项目承接 2026-10-18 索引，未被本轮关闭：

1. **A2 自然长文本**：自然连续 `>=3,200` 字符、真实 CER/WER 和滑窗接缝门。
2. **A3 自然方向**：自然角度/倒置真值、repair/rejection/CER/WER 门。
3. **B1b/B2/B3 鲁棒性与增强成本**：独立自然低质量 split、业务字段误纠预算和正式延迟成本门；默认增强仍关闭。
4. **公式质量**：数学语义/渲染质量门。
5. **Chart2Table**：更大规模或官方 split accuracy。
6. **完整矩阵与第二设备**：其余精确模型×后端覆盖、第二设备正式性能、锁频对照及长时间稳定性。

本次有一页一次的候选全量运行时间记录，但它包括顺序执行的三种候选，没有对应的无增强配对基线，也不是正式 `5 warm-up + 50 measured` 协议，因此不作为性能门结论。
