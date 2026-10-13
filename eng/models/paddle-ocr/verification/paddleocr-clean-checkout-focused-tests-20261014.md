# Clean-checkout focused regression — 2026-10-14

在提交 `f23d19f` 的 detached clean checkout 中，针对上一次回归暴露的两个问题重跑定向测试：

| 项目 | 结果 |
| --- | --- |
| `DeploySharp.Backend.OpenCV.Tests` | `32 passed / 0 failed / 0 skipped` |
| `DeploySharp.ModelFactory.Tests` | `63 passed / 0 failed / 2 skipped` |
| 合计 | `95 passed / 0 failed / 2 skipped` |

使用 `.NET 10.0.401`、Windows `10.0.26200.0`、`Release/net10.0` 和 `--no-restore`。两个项目退出码均为 `0`。跳过项是显式外部 release metadata/Qwen 下载门控，不转换成模型或后端支持结论。

本轮确认：OpenCV DNN 的 BatchNorm/Reshape 兼容修复已经进入提交并通过；PaddleOCR ModelPack 的 revision-pinned 官方 ONNX 来源与 admission 状态同步后，ModelFactory 合同也通过。机器可读结果见 [JSON](paddleocr-clean-checkout-focused-tests-20261014.json)。

这是一份聚焦回归，不替代完整解决方案测试、外部模型质量、native runtime、性能、soak 或跨设备验证。

本轮维护入口文档链接审计为 `680` 条本地 Markdown 链接、断链 `0`，机器记录为 `../../paddle-document/verification/document-link-audit-20261014-focused-tests.json`。
