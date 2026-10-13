# PP-Structure two-page concurrency benchmark — 2026-10-14

在 Windows 主机上用 `E:\Data\image\bus.jpg` 复制为两个有序页对象，运行方向分类 → 版面检测两阶段流水线。协议固定为 `sessionConcurrency=2`、`pageConcurrency=2`、`5 warm-up + 50 measured`；每次都校验页码顺序、输入 SHA、方向标签和 300 个版面候选。这里比较的是页面级并发调度，不是模型张量 Batch。

| 后端 | 串行 P50/P95 (ms) | 并发 P50/P95 (ms) | 首次串行/并发 (ms) |
| --- | ---: | ---: | ---: |
| ONNX Runtime CPU | `1139.597 / 1261.217` | `1018.432 / 1211.011` | `1684.202 / 1123.341` |
| OpenVINO CPU | `1137.557 / 1280.655` | `915.874 / 987.933` | `1839.093 / 1106.566` |

两后端均完成 `2/2` 页、顺序保持 `0 → 1`，每页返回 300 个版面候选；模型和输入 SHA、50 个原始样本及运行环境保存在对应机器 JSON 中：[ORT](paddle-document-multipage-benchmark-20261014-onnxruntime.json)、[OpenVINO](paddle-document-multipage-benchmark-20261014-openvino.json)。

在本机该配置下，并发 P50 相比串行约降低 ORT `10.6%`、OpenVINO `19.5%`；这只是单机、单图复制页、当前 Session/page 并发组合的观察，不能外推到 TensorRT、真实多页分布、张量 Batch、准确率、长时间 soak 或跨设备吞吐。不同后端和设备仍需重新调参。

机器可读汇总见 [JSON](paddle-document-multipage-benchmark-20261014.json)。
