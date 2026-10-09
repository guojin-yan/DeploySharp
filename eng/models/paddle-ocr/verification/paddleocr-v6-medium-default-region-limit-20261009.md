# PP-OCR benchmark default region-limit regression（2026-10-09）

这是一项基准工具回归，不是模型准确率评测。此前的 channels=2 跟进报告显式设置了 `MAX_REGIONS=32`；HierText 页面 `97a0add3f8f47b65` 实际检测到 33 个区域，因此在检测完成后被安全上限拒绝。该限制不是核心 DB 解码器的默认值，也不应让性能/合同证据依赖偶然的测试夹具。

本轮将 `DeploySharp.PaddleOcrBenchmark` 在未设置 `DEPLOYSHARP_PADDLEOCR_MAX_REGIONS` 时的默认值从 `32` 调整为 `128`，并把实际值写入 width sidecar 的 `Protocol.MaximumRegions`。数据集运行器的元数据也记录 `maximumRegions`，便于复现实验。调用方仍可显式降低该值；没有改变 `OcrPipelineOptions` 的用户配置，也没有把 channels=2 提升为全局默认。

## 协议

| 项目 | 值 |
| --- | --- |
| 设备 | JYPPX / Windows 10 build 26200 / RTX 3060 Laptop |
| 后端 | ONNX Runtime CUDA |
| 输入 | HierText `97a0add3f8f47b65.jpg`（SHA-256 见 JSON） |
| 预热 / 计时 | `5 / 50` |
| Batch / 独立通道 | `16 / 2` |
| Overflow | `Clamp` |
| `MAX_REGIONS` | 未设置，使用基准默认 `128` |
| 合同门 | 严格合同；未设置 numeric-drift 诊断开关 |

## 结果

| 模型 | 区域数 | 状态 | 严格合同变体 | 数值最大漂移 | 总耗时 P50/P95 (ms) |
| --- | ---: | --- | ---: | ---: | ---: |
| PP-OCRv6 Medium | 33 | pass | 1 | 0 | 193.425 / 204.081 |
| PP-OCRv6 Small | 32 | pass | 1 | 0 | 53.598 / 105.228 |
| PP-OCRv6 Tiny | 22 | pass | 1 | 0 | 68.304 / 73.215 |

Medium 行是本次回归的关键：33 个检测区域在默认 128 上限内完整执行，不再被错误的 32 区域夹具阻断。文本、语义和完整结果合同 SHA 均保留在 [机器可读报告](paddleocr-v6-medium-default-region-limit-20261009.json) 中。

## 边界

- 该证据只说明 benchmark 默认上限不再拒绝该页，并验证了当前进程内的严格结果合同；它不构成检测召回、CER/WER、跨后端 parity 或设备极限结论。
- 旧的 32 区域报告仍是“显式配置 32”的历史诊断，不能改写为默认行为。
- v6 Medium channels=2 的多进程/跨运行稳定性仍按统一计划保持开放；生产默认 `inferenceChannels` 未改变。
