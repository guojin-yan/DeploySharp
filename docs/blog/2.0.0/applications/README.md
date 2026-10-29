# DeploySharp 2.0.0 多模型应用实战

一个模型能完成检测或识别，实际应用还需要组织输入、组合结果、处理异常，并把结果交给使用者。这个系列围绕完整任务，提供可以直接运行的 C# 工程、真实输入和结果查看页面。

| 编号 | 应用 | 模型组合 | 最终产物 |
| --- | --- | --- | --- |
| 01 | [智能巡检台：园区车辆与人员事件分析](2026-10/2026-10-29-01-campus-inspection.md) | NanoDet-Plus + CLIP 图像/文本编码器 | 区域统计、车型候选、待复核事件、证据截图、JSON/CSV 与 HTML 复核台 |
| 02 | [智能文档工作台：混合资料解析与复核](2026-10/2026-10-29-02-document-archive.md) | 文档方向 + PP-DocLayout-L + PP-OCRv5 + 表格分类/RT-DETR cell/SLANeXt + FormulaNet | 9 张图、4 组资料，方向校正、文字/表格/公式分流、双坐标定位、分组归档、字段候选与复核 |
| 03 | [智能素材工厂：商品图一站式处理](2026-10/2026-10-29-03-product-catalog.md) | BRIA RMBG 1.4 + CLIP ViT-B/32 + PP-OCRv5 Mobile | 原图、透明 PNG、白底图、类别候选、文字复核、CSV 与可搜索图册 |

三篇主稿均已完成，尚未向博客平台发表。工程使用正式 NuGet `2.0.0`，在 Windows 11 x64、Ubuntu 22.04 x64 的 ORT CPU 与 OpenVINO CPU 上运行。文档工作台选择 OpenVINO 时，公式任务仍明确使用 ORT CPU；各篇实测章节记录输入、设备和真实结果。

商品案例保留低置信度 OCR，返回待复核状态；文档工作台保留复杂表格和公式诊断，支持点击结果定位原图。示例的识别输出可以复核，不把少量图片的运行结果作为准确率结论。

## 工程与素材

工程位于各月份的 `examples/NN-slug/`，每个工程有独立 README、配置和 `demo-inputs`。模型按文章或工程 README 下载到本地 `models/`；运行生成的 `results/` 不提交仓库。文章配图位于同月的 `assets/NN-slug/`。

| 应用 | 完整工程 | 输入来源 |
| --- | --- | --- |
| 园区巡检 | [01-campus-inspection](2026-10/examples/01-campus-inspection/README.md) | [固定视角截图](2026-10/examples/01-campus-inspection/demo-inputs/README.md) |
| 智能文档工作台 | [02-document-archive](2026-10/examples/02-document-archive/README.md) | [9 张混合文档图片](2026-10/examples/02-document-archive/demo-inputs/README.md) |
| 商品整理 | [03-product-catalog](2026-10/examples/03-product-catalog/README.md) | [三张商品照片](2026-10/examples/03-product-catalog/demo-inputs/README.md) |

原基础第 07 篇的园区巡检案例迁入本系列第一篇；[基础第 07 篇](../2026-10/2026-10-29-07-custom-visual.md)现聚焦 NanoDet 单模型的自定义前处理、Decoder 和结果类。

[返回 2.0.0 系列](../README.md) · [返回博客文章索引](../../README.md)
