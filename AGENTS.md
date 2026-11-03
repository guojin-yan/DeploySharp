# DeploySharp 仓库工作约定

本文件适用于当前 Git 仓库。先确认 Git 根目录、分支和工作树状态；保留其它会话及用户的修改，只提交本任务的文件或变更片段。`applications/` 仅在用户将其纳入任务时修改。

## 按任务读取技能

开始对应工作前，完整读取下列技能；需要多个技能时按实际涉及的部分组合使用。

| 任务 | 技能 |
| --- | --- |
| 增加或升级内置模型、Profile、Decoder、模型下载及案例 | [deploysharp-model-onboarding](.agents/skills/deploysharp-model-onboarding/SKILL.md) |
| 后端适配、原生运行时、模型真实推理与结果一致性 | [deploysharp-backend-validation](.agents/skills/deploysharp-backend-validation/SKILL.md) |
| 前后处理优化、延迟、吞吐和内存测量 | [deploysharp-performance-analysis](.agents/skills/deploysharp-performance-analysis/SKILL.md) |
| 用户文档、README、支持矩阵和教程 | [deploysharp-documentation](.agents/skills/deploysharp-documentation/SKILL.md) |
| 包版本、打包与发布工作流 | [deploysharp-package-release](.agents/skills/deploysharp-package-release/SKILL.md) |

新增内置模型时，以模型接入技能及其交付清单为准，完成代码、真实模型验证、可运行案例、下载配置和用户文档的相应闭环。仅下载或转换模型、仅添加合同或案例，不等于完成内置支持；缺失项应明确保留状态。

## 代码和交付边界

- 保持现有 NuGet 包边界。先读取 `Directory.Build.props`、`Directory.Packages.props` 和受影响项目配置，复用现有抽象，不为每个模型新增包。
- 新增公共 API 遵循现有中英文 XML 注释、资源所有权与取消约定，并核对实际目标框架。源码新增能力不能写成已发布包已具备的能力。
- 用户文档说明当前用法与可复现结果。开发计划、验收审计、原始日志、临时脚本和大模型文件不进入公开文章；优先使用仓库外已有的 `plan/`、`engineering-archive/` 或临时目录。提供给用户的模型转换、部署和测试工具仍可放在相应 `eng/`、`tools/` 或案例目录。
- 普通模型接入任务允许获取所需公开模型和本地转换。模型 Release 上传、NuGet 发布、Tag 和已有资产修改按用户已经授予的范围执行；接入任务本身不代表授权发布，也不要为已获授权的步骤重复询问。
- 构建和测试按改动范围选择；检查通过后不重复跑无变化的全量矩阵。提交、推送按当前会话授权执行，交付时说明实际验证范围及尚未完成的部分。
