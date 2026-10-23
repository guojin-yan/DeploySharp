# PaddleOCR 质量证据状态增量索引（2026-10-23）

## 使用方式与边界

这是对[2026-10-18 状态索引](paddleocr-quality-status-20261018.md)的后续增量，后者又延续 2026-10-14 基线。本文登记自然来源收据、原始 FUNSD 扫描 crop 的增强共识诊断和配对成本，不覆盖历史报告，不改变后端支持矩阵，也不把局部结果外推到所有模型/后端。原图、标注和逐 crop 内容继续保留在外部数据目录；仓库内 JSON 只保留来源、SHA、统计与文本摘要。

## 本轮新增证据

| 范围 | 数据/协议 | 后端与结果 | 状态 | 不能推出的结论 |
|---|---|---|---|---|
| PP-OCRv6 Small SROIE crop 增强共识 | 固定 SROIE revision；10 页、542 个词级真值区域；DET bypass；batch 16；三种顺序增强候选 | ORT CPU 1.28.0、OpenVINO CPU 2026.2.1；542/542 原文、候选、最终文本与决策完全一致；选中 353 行，CER 改善/恶化/相同 `4/5/344`，WER `3/4/346`；聚合 CER 编辑 `513→514`、WER 编辑 `232→233`。另穷举 ORT `348` / OpenVINO `350` 个非空候选增益切点，均无“零 CER 误纠且至少一项 CER 改善”的阈值；零误纠点仅留下 2 或 1 个 CER 持平区域 | 诊断完成，无总体收益 | 图像成像质量未标注，非自然低质量 split；事后同样本阈值扫描不能作为生产阈值；word crop 指标不是页面/行级准确率；不批准默认增强或业务阈值 |

详见[实验报告](paddleocr-sroie-natural-source-enhancement-20261023.md)、[逐区域机器 JSON](paddleocr-sroie-natural-source-enhancement-20261023.json) 和[外部测试入口](../../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrSroieNaturalEnhancementIntegrationTests.cs)。

### FUNSD 原始扫描与配对成本增量

| 范围 | 数据/协议 | 结果 | 状态与边界 |
|---|---|---|---|
| v6 Small 自然噪声扫描增强 | 固定 10 张 FUNSD 原始 test 扫描、1,769 个词区，与此前 40 页受控增强父图不重叠；未生成退化、未调阈值 | ORT/OpenVINO 原文、全部候选、最终文本 `1,769/1,769` 一致；决策/候选索引 `1,767/1,769` 一致，2 处浮点元数据差异保留；最大 confidence 漂移 `0.000490725 < 0.001`。两后端 CER 编辑 `307/8,582→311/8,582`，WER `214/1,775→219/1,775` | 自然噪声扫描诊断完成，固定策略无总体收益；只有来源级质量描述，无逐图严重度；不是独立数据集或页面准确率，不批准默认增强 |
| v6 Small 配对增强成本 | 固定第一张 FUNSD 测试页的前 16 个有效词区；每后端独立 baseline/enhanced Pipeline；batch 16；5 对预热＋50 对测量，AB/BA 各 25 对 | ORT baseline/enhanced P50/P95 `69.17/104.88→258.29/347.18 ms`，配对额外 P50 `192.49 ms`、配对比值中位数 `3.68×`；OpenVINO `91.53/127.09→364.82/541.88 ms`，额外 `279.06 ms`、`3.93×`。两后端各 50/50 文本合同稳定，每次执行 48 个候选；此子集 CER/WER 无收益 | 有界 CPU 配对 5+50 测量完成；时延为每 16 个词区的识别-only，输入已准备、不含 DET/CLS；未锁频，不是业务预算或完整性能矩阵 |

详见 [FUNSD 原始扫描报告](paddleocr-funsd-natural-low-quality-enhancement-20261023.md)与[JSON 摘要](paddleocr-funsd-natural-low-quality-enhancement-20261023.json)，以及[配对成本报告](paddleocr-funsd-paired-enhancement-cost-20261023.md)与[50 对逐次样本](paddleocr-funsd-paired-enhancement-cost-20261023.json)。FUNSD 事后零误纠且有收益的增益点仅保留一两词，不作生产阈值；完整逐区域证据留在 ignored artifacts，由摘要中的 SHA 引用。本轮外部测试（含 SROIE 回归）为 `3/3 passed`；首次过严的决策位级断言失败已在报告中说明，没有修改生产阈值或 tie-breaking。

## 当前开放门禁

下列项目承接 2026-10-18 索引，未被本轮关闭：

1. **A2 自然长文本**：自然连续 `>=3,200` 字符、真实 CER/WER 和滑窗接缝门。
2. **A3 自然方向**：自然角度/倒置真值、repair/rejection/CER/WER 门。
3. **B1b/B2/B3 鲁棒性与增强成本**：已补原始噪声扫描的页面隔离诊断与单一 16 词 CPU 配对成本；仍缺更细的自然质量分层、独立数据集/业务字段误纠预算和代表性成本门，默认增强仍关闭。
4. **公式质量**：数学语义/渲染质量门。
5. **Chart2Table**：更大规模或官方 split accuracy。
6. **完整矩阵与第二设备**：其余精确模型×后端覆盖、第二设备正式性能、锁频对照及长时间稳定性。

质量报告中的每页一次墙钟包括三个顺序候选，不能作为增强增量成本；增量成本仅使用单独 16 词选集的配对 `5+50` 报告。后者补齐测量切片，不代替业务延迟预算或完整性能矩阵。
