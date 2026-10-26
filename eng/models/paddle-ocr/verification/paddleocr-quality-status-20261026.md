# PaddleOCR 质量证据状态增量索引（更新至 2026-10-26）

本页承接[2026-10-25 索引](paddleocr-quality-status-20261025.md)。历史报告保留各自日期与原始边界；这里仅增加已经完成的独立标注诊断，不改变模型/后端支持矩阵，也不把局部样本外推为发布精度。

| 范围 | 数据/协议 | 结果 | 状态与边界 |
| --- | --- | --- | --- |
| PaddleOCR-VL-1.6 Spotting 页面 | FUNSD test manifest 固定首张原图、63 个词级标注；生产 C# ORT CPU 四图生成；词中心归属最小包含预测四边形，未归属真值/预测计入编辑 | 411 Token、26 个预测区域、EOS/解析成功；45/63 词中心被覆盖，18 未覆盖，5 个预测区域无归属真值；关联 CER `263/406=64.78%`、WER `47/63=74.60%`。同页官方权重与本库 4,769,856 个 Patch 值 SHA、411/411 Token ID、文本及 EOS 一致 | 首个独立标注页面诊断，暴露与官方固定输出共有的质量差距，不是本页 ONNX/C# 生成漂移。FUNSD 词框与 VL 行级输出粒度不同；非官方 F1/CER、非代表性精度。完整协议与两个原始报告 SHA 见[专题记录](paddleocr-vl-funsd-spotting-quality-20261026.md)及[机器摘要](paddleocr-vl-funsd-spotting-quality-20261026.json)。 |

仍开放：预先固定的更多标注页及跨后端质量、阅读顺序/多页任务语义、公式数学语义、ChartQA split accuracy、A2/A3 和 B1b-B3、正式性能、长期稳定性，以及 `models-paddleocr` 的 VL 资产实际上传/下载回读。同页官方 Token 一致不等于以上标注质量门通过。
