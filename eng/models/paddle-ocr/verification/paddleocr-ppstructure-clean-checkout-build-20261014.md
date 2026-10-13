# DeploySharp clean-checkout build — 2026-10-14

## 结果

在提交 `d02638ecdaa9a597fe88214e185b5cde3f7b673e` 创建隔离 detached worktree，执行完整 `DeploySharp.sln` 的还原和 Release 构建。解决方案列出的 37 个项目全部参与构建，包含 `src`、`tests`、`samples` 和 CLI；`applications` 不在本次范围内。

- `dotnet restore DeploySharp.sln --nologo --verbosity minimal`：退出码 `0`
- `dotnet build DeploySharp.sln --configuration Release --no-restore --nologo --verbosity minimal`：退出码 `0`
- 编译错误：`0`
- 警告：`42`
- 主机：Windows NT `10.0.26200.0`，`win-x64`
- SDK：`.NET 10.0.401`

## 证据身份

- 隔离目录：`E:\GitSpace\DeploySharp-V2.0\_cleancheck-20261014`
- restore 日志 SHA-256：`0b2d3f3c672300f386eccc56bd6801e86c9e4b395121726ed39891d6f7468c2c`
- build 日志 SHA-256：`1f758d37f9ffab804e830bad033982f3ab84ef9a70d3e7ce556f3d8830b877b5`
- 入口文档链接审计：[document-link-audit-20261014-clean-checkout.json](../../paddle-document/verification/document-link-audit-20261014-clean-checkout.json)，`680` 条本地 Markdown 链接，断链 `0`
- 机器可读记录：[paddleocr-ppstructure-clean-checkout-build-20261014.json](paddleocr-ppstructure-clean-checkout-build-20261014.json)

## 边界

本证据关闭的是“当前提交能否从干净 checkout 还原并编译”的 P7 门禁。42 条警告来自项目声明的旧目标框架与当前 NuGet 包的 TFM 支持提示，不是编译失败。它不证明任何 native runtime、外部模型、准确率、CER/WER、性能、跨设备或后端矩阵结论；在其它机器上构建旧目标框架仍可能需要安装对应的 .NET Framework Developer Pack。
