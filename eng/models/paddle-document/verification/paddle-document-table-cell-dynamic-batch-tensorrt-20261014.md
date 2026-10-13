# PP-Structure table-cell TensorRT dynamic Batch=2 evidence — 2026-10-14

在同一台 Windows RTX 3060 Laptop（`compute_86`）主机上，使用 TensorRT `10.11.0.33-cu12`、CUDA `12.9`、cuDNN `9.22` 和匹配的 DeploySharp bridge，分别从两个精确 RT-DETR 表格单元格 ONNX 工件构建动态 Batch=2 Engine。每个输入由 `bus.jpg` 上下两个不重叠 ROI 组成，执行 `[2,3,640,640]`，并用同一准备张量的 ORT CPU 输出做有界行隔离与几何 parity。

| 模型 | ONNX SHA-256 | Engine SHA-256 | 行 0 最小 IoU | 行 1 最小 IoU | 状态 |
| --- | --- | --- | ---: | ---: | --- |
| `paddle-table/rt-detr-l-wired-cell-det` | `c390d3c0…286c74d6` | `c0969f44…de5b6ca` | `0.99987644` | `0.99983690` | pass |
| `paddle-table/rt-detr-l-wireless-cell-det` | `e141c8aa…570b296f` | `2a6b820b…290744f` | `0.99983543` | `0.99996006` | pass |

两个模型均为两行各 `300` 个候选，ORT/TensorRT 的比较候选均为 `300`，分数差均在测试合同的 `0.03` 内。完整环境、输入身份、Engine 大小和逐行结果见[机器可读 JSON](paddle-document-table-cell-dynamic-batch-tensorrt-20261014.json)。

该报告只关闭两个精确 RT-DETR 工件的 TensorRT 动态 Batch=2 执行、辅助输入绑定、行隔离和有界 ORT parity 缺口；不是表格单元格召回率、表格结构准确率、吞吐、跨设备性能或 Engine 可移植性结论。Engine 与 GPU、TensorRT bridge/runtime 和 `compute_86` 绑定，其他设备必须重新构建和验证。
