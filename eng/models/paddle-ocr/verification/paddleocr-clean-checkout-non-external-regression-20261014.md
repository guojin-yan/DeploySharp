# Clean-checkout non-external regression — 2026-10-14

在提交 `a3c3fdc` 的 detached clean checkout 中，对 `DeploySharp.sln` 执行完整的非外部测试集。`applications` 目录不在解决方案和本报告范围内。

| 测试项目 | 通过 | 失败 | 跳过 | 总计 |
| --- | ---: | ---: | ---: | ---: |
| `DeploySharp.LLM.Tests` | 10 | 0 | 0 | 10 |
| `DeploySharp.Backend.LlamaSharp.Tests` | 9 | 0 | 1 | 10 |
| `DeploySharp.Core.Tests` | 33 | 0 | 0 | 33 |
| `DeploySharp.ModelPack.Json.Tests` | 29 | 0 | 3 | 32 |
| `DeploySharp.Multimodal.Tests` | 7 | 0 | 0 | 7 |
| `DeploySharp.Backend.OpenCV.Tests` | 32 | 0 | 0 | 32 |
| `DeploySharp.ModelFactory.Tests` | 63 | 0 | 2 | 65 |
| `DeploySharp.Visual.TensorRT.Tests` | 8 | 0 | 0 | 8 |
| `DeploySharp.Extensibility.Tests` | 8 | 0 | 0 | 8 |
| `DeploySharp.Visual.OpenCV.Tests` | 99 | 0 | 4 | 103 |
| `DeploySharp.Backend.TensorRT.Tests` | 60 | 0 | 0 | 60 |
| `DeploySharp.Backend.OnnxRuntime.Tests` | 33 | 0 | 0 | 33 |
| `DeploySharp.Visual.Tests` | 522 | 0 | 5 | 527 |
| `DeploySharp.Backend.OpenVINO.Tests` | 25 | 0 | 0 | 25 |
| **合计** | **938** | **0** | **15** | **953** |

执行环境：Windows `10.0.26200.0`、`win-x64`、.NET SDK `10.0.401`、`Release/net10.0`。命令为：

```powershell
dotnet restore DeploySharp.sln --nologo --verbosity minimal
dotnet test DeploySharp.sln -c Release --no-build --no-restore --filter "TestCategory!=ExternalModels" --nologo --verbosity minimal
```

退出码为 `0`。15 个跳过项均属于显式外部模型、公开下载或运行时门控；它们不转换成模型、后端、精度或性能支持结论。该报告证明的是当前提交在 clean checkout 中的非外部回归通过，不替代真实模型质量、多后端性能、native runtime、长时间 soak 或跨设备验证。

机器可读结果见 [JSON](paddleocr-clean-checkout-non-external-regression-20261014.json)。原始测试日志 SHA-256 为 `36e8fa056020534a55a246a076a0c183da7fa576317ba46b3b5128a49723c0a0`；restore 日志 SHA-256 为 `df625ad99a18d2e219fd5df60085bf920194bc537cd850029a04f175b5c6c18f`。

本轮同步后的 PP-OCR/PP-Structure 维护入口文档链接审计见 [`document-link-audit-20261014-non-external-regression.json`](../../paddle-document/verification/document-link-audit-20261014-non-external-regression.json)。
