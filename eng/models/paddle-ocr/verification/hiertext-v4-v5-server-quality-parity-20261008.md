# PP-OCRv4/v5 Server HierText 质量与跨后端对照（2026-10-08）

本报告把 PP-OCRv4 Server 和 PP-OCRv5 Server 加入同一份 HierText `sample-003` 质量 smoke。每个模型分别在 ONNX Runtime CPU 与 OpenVINO CPU 上执行完整的 `det -> crop -> optional cls -> rec -> merge` 流水线，并用同一份 24 页选集和同一套 SlidingWindow 参数评分。

这是一项固定主机、固定模型工件、`1 warmup + 1 measured iteration` 的诊断性验证，不是发布准确率、排行榜结果或正式 P50/P95 性能矩阵。v4/v5 Server 都没有自然文本行达到 A2 要求的 3,200 字符，因此不能据此关闭长文本质量门。

## 复现范围与来源

- 数据源：`google-research-datasets/hiertext` 的本地 `sample-003` 选集，共 24 张图像、1,020 个标注文本区域。
- 源 manifest SHA-256：`f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`。
- selected manifest SHA-256：`2c3933fbd5f29c2d4054357518feae96eb1bedd4b009333e105bfeb267159316`。
- 数据仍位于本机 `F:\OCRBenchmarkTesting`，图像、标注、预测结果没有复制进仓库或 Release；在完成逐图来源与再分发审查前保持 smoke-only。
- SlidingWindow overlap `0.2`，每个区域最多 32 个窗口，最多 1,024 个区域，recognition 最大宽度 `320`，recognition batch `16`，inference channels `1`。
- 运行时：Windows x64、`.NET 10.0.12`、16 个逻辑处理器、ONNX Runtime `1.23.2`、OpenVINO `2026.2.1`，CPU provider。
- benchmark assembly SHA-256：`a2a0b20f6a794a90f2f9349dd47865994160a3c1eca90cd62220da74c42f9320`。
- 本轮来源 revision：`72808fbc82ed04f44d4b402b9c165a98009925bc`；工作树当时为 dirty，机器报告保留这一事实。

模型工件 SHA-256：

| 模型 | DET | REC | CLS | 字典 |
|---|---|---|---|---|
| PP-OCRv4 Server | `1d6b24b3038d814ba5243691d4d5c2af2d3451f909f12617748aba508345f6b9` | `b089d3cebf235c57ee1cf1621da452c0bf25acb8f2eead7f5fb65a843f17057f` | `f4bb53707100c5f3d59ba834eb05bb400369f20aed35d4b26807b1bfadd2a70e` | `8e9dc37300253c08a5db6d75f8ae7dbf9ab8dc8c8f88827400bfe974269d3953` |
| PP-OCRv5 Server | `0c4ff76f78feb3e4b4e9b3030df350234f570027e1ec9cfe5bbb7a596cb57f47` | `12ec4f2b7266afcca07063786238e1538c8f19656cb2b6bbc0f4b9c492c2667a` | `38aa97cd4be591e0ad304e659f07ba30d946f27a63315433f6659c69c8778345` | `d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b` |

## 质量与延迟结果

百分比由机器报告中的比例乘以 100 展示。检测分数使用 IoU `>= 0.5` 的区域匹配；`matched` 只在已匹配区域上计算，`end-to-end` 将漏检视为删除、未匹配预测视为插入。

| 模型 / 后端 | 页 / 失败 / 空结果 | TP / FP / FN | 检测 F1 | matched CER / WER | end-to-end CER / WER | 总耗时 P50 / P95 (ms) |
|---|---:|---:|---:|---:|---:|---:|
| v4 Server / ORT CPU | 24 / 0 / 0 | 550 / 374 / 470 | 56.58% | 33.50% / 65.95% | 91.53% / 105.52% | 3857.947 / 7529.303 |
| v4 Server / OpenVINO CPU | 24 / 0 / 0 | 550 / 374 / 470 | 56.58% | 33.50% / 65.95% | 91.53% / 105.52% | 3291.705 / 6902.660 |
| v5 Server / ORT CPU | 24 / 0 / 0 | 522 / 328 / 498 | 55.83% | 41.13% / 71.14% | 79.97% / 100.93% | 2998.598 / 6092.443 |
| v5 Server / OpenVINO CPU | 24 / 0 / 0 | 522 / 330 / 498 | 55.77% | 41.13% / 71.14% | 80.02% / 100.98% | 2487.039 / 4641.212 |

阶段延迟（P50 / P95，毫秒）如下：

| 模型 / 后端 | DET | CLS | REC |
|---|---:|---:|---:|
| v4 Server / ORT CPU | 1845.811 / 2665.563 | 72.013 / 128.589 | 1811.393 / 5386.975 |
| v4 Server / OpenVINO CPU | 1587.161 / 3312.621 | 28.469 / 65.724 | 1611.697 / 4234.516 |
| v5 Server / ORT CPU | 1204.413 / 1812.768 | 265.629 / 545.793 | 1287.026 / 4326.235 |
| v5 Server / OpenVINO CPU | 1155.284 / 1794.661 | 218.539 / 421.430 | 862.104 / 3062.011 |

同一模型的 ORT/OpenVINO 识别质量基本保持一致，但 v5 Server 的检测区域集合存在一页差异：ORT 返回 47 个区域，OpenVINO 返回 49 个区域。已有的 47 个 ORT polygon 都能在 OpenVINO 中以 IoU `1.0` 找到对应项；额外两个区域没有独立真值，不能称为误检，也不能据此宣称某一后端更准确。v4 Server 的区域计数和区域文本序列在两个后端完全一致，polygon 最大坐标偏移约 `0.01801 px`，confidence 最大漂移约 `0.0008093`。

## 长文本边界

选集中有 42 个长度至少 32 字符的区域、4 个长度至少 128 字符的区域，但没有长度至少 3,200 字符的自然文本行：

| 模型 / 后端 | `>=32` 区域 / 字符 / edits | `>=128` 区域 / 字符 / edits | `>=3200` |
|---|---:|---:|---:|
| v4 Server / ORT 或 OpenVINO | 42 / 2378 / 1822 | 4 / 520 / 428 | 0 |
| v5 Server / ORT 或 OpenVINO | 42 / 2378 / 751 | 4 / 520 / 114 | 0 |

因此本记录只能说明真实页面流水线在该选集上能完成并可重复比较，不能替代自然超长文本标注集，也不能关闭 A2 长文本验收。

## 结论与边界

1. v4/v5 Server 的 ORT CPU、OpenVINO CPU 均完成 24/24 页，未出现失败或空结果。
2. v4 Server 的 ORT/OpenVINO 质量计数和页面文本完全一致；v5 Server 的 matched CER/WER 完全一致，只有一页的两个无真值额外区域造成检测计数差异。
3. v4 Server 的检测 F1 略高，但 v5 Server 的端到端 CER/WER 明显更低；不能只根据 detector F1 选择整条 OCR 模型组合。
4. 延迟是单机一次 warm-up/一次测量的 smoke 观察；它不是正式 5/50 性能协议，不能外推到 GPU、TensorRT、OpenCV DNN 或其他设备。
5. 本轮没有新增 OpenCV DNN、CUDA、TensorRT 支持声明；这些后端仍须分别取得真实模型、运行时和结果证据。

## 机器可读报告与复现

完整逐页统计、模型/数据 SHA、运行时、P50/P95 和跨后端差异见 [JSON 报告](hiertext-v4-v5-server-quality-parity-20261008.json)。可按 [v5 Mobile 公共质量记录](hiertext-v5-mobile-public-ocr-20260925.md) 中的 runner/scorer 方式运行，将模型参数替换为本报告列出的 v4/v5 Server 工件，并分别使用 `onnxruntime` 与 `openvino` 输出到不同目录，再用 `Evaluate-DeploySharpPublicOcrDataset.py` 评分。

报告不包含外部数据集文件或预测资产；复现前请确认本机已有 `F:\OCRBenchmarkTesting` 选集和对应模型文件，并接受该数据源当前的 smoke-only 许可边界。
