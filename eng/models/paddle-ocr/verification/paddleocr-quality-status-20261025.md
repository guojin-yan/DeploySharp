# PaddleOCR 质量证据状态增量索引（更新至 2026-10-25）

## 使用方式与边界

这是对 [2026-10-23 状态索引](paddleocr-quality-status-20261023.md) 的后续增量；历史索引保留原始时间点和边界。本页只导航已经产生的质量、执行和性能证据，不修改[模型 × 后端验证矩阵](../../../docs/model-backend-verification-matrix.md)，也不把局部诊断外推成发布准确率或全后端支持。

## 本轮新增证据

| 范围 | 数据/协议 | 结果 | 状态与边界 |
|---|---|---|---|
| PP-OCRv6 Tiny HierText 十图端到端 | `F:\OCRBenchmarkTesting` validation `sample-002`，10 图；ORT CPU；batch `8`、2 通道；SlidingWindow overlap `0.2`、每区域最多 32 窗口；每图 `1+1` | 10/10 完成、0 失败、0 空输出；IoU0.5 `TP/FP/FN=115/191/422`，F1 `27.28%`；匹配 CER/WER `22.54%/55.70%`；端到端 CER/WER `113.33%/126.50%`；总耗时观察 P50/P95 `219.440/361.046 ms` | 公开数据 smoke/复现证据；不是发布准确率、正式 `5+50` 性能或后端排名。选集没有 `>3,200 px` 真值宽行，A2 保持开放；词框/文本行粒度差异和低召回照实保留 |
| PaddleOCR-VL-1.6 六任务生成 | 固定四图 ONNX Bundle、官方 Tokenizer；ORT CPU（Windows/Linux）、OpenVINO CPU（Windows/Linux）、ORT CUDA（Windows RTX 3060） | OCR、Table、Formula、Chart、Spotting、Seal 均达到 EOS；Token/Text 与官方参考一致；Table/OCR/Formula/Chart/Seal 已有 CPU `5+50` 延迟切片，Spotting 使用 `1+5` 有界协议 | 生成正确性和固定任务延迟证据；不等于页面标注质量、TensorRT/OpenCV DNN 支持、设备端 KV/logits 或长期稳定性 |
| PaddleOCR-VL Release Bundle | 本地四图、四个 Tokenizer sidecar 和 manifest 共 9 个资产；远端 `models-paddleocr` Release 回读检查 | 本地资产已准备并由 manifest 记录大小/SHA；远端当前 VL 前缀资产数为 `0`，上传接口返回 HTTP `404`，当前账号对仓库只有 `pull` 权限 | 代码下载路径已完成，在线下载仍被 GitHub 写权限阻断；不能标记为“远端可下载” |

## 当前开放门禁

1. **A2/A3/B1b-B3**：自然宽行接缝、自然方向真值、退化分层、增强误纠成本和完整质量门。
2. **PP-Structure 质量**：公式语义/渲染、Chart2Table split accuracy、多页阅读顺序和更广独立数据。
3. **完整模型 × 后端矩阵**：TensorRT/OpenCV DNN 的剩余精确工件、第二设备正式性能、锁频对照和长期稳定性。
4. **PaddleOCR-VL**：TensorRT/OpenCV DNN、标注集质量、跨设备性能、长时间稳定性，以及 `models-paddleocr` Release 的 9 个远端资产上传/下载回读。

## 复现入口

- [v6 Tiny HierText 十图报告](hiertext-v6-tiny-10page-public-ocr-20261025.md)及[机器 JSON](hiertext-v6-tiny-10page-public-ocr-20261025.json)
- [PaddleOCR-VL 模型说明](../README.md)
- [PaddleOCR-VL 后端矩阵](../../../docs/model-backend-verification-matrix.md)

本索引只记录证据状态，不改变默认 OCR 增强策略，也不把远端 Release 权限问题转换成模型或后端不支持。
