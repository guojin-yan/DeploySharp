# PP-Structure two-page TensorRT benchmark — 2026-10-14

在同一台 Windows RTX 3060 Laptop 主机上，以 TensorRT 10.11 bridge/runtime 从精确 PP-LCNet 文档方向和 PP-DocLayout-L ONNX 重新构建 Engine，运行 `bus.jpg` 两页方向 → 版面流水线。协议为 `sessionConcurrency=2`、`pageConcurrency=2`、`5 warm-up + 50 measured`。

| 后端 | 串行 P50/P95 (ms) | 并发 P50/P95 (ms) | Engine |
| --- | ---: | ---: | --- |
| TensorRT CUDA (`compute_86`) | `99.344 / 128.749` | `67.187 / 72.894` | orientation `30d080b1…42fbf`；layout `3f6043b6…e2dc7` |

两页均保持顺序 `0 → 1`，方向为 `0_degree`，每页返回 300 个版面候选。原始机器记录见 [JSON](paddle-document-tensorrt-multipage-benchmark-20261014.json)，包含 ONNX/Engine SHA、环境、50 个样本和结果合同。

当前配置下并发 P50 比串行低约 `32.4%`。该数字只代表本机、当前 Engine、输入和并发组合；TensorRT Engine 与 GPU 架构/runtime 绑定，不能外推到其他设备，也不是张量 Batch、准确率或长时间 soak 结论。
