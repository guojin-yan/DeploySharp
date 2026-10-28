---
uid: index
---

# DeploySharp V2 文档

DeploySharp V2 是面向 .NET 的模型部署工具。它通过显式后端、模型制品和运行时边界，提供视觉、语言与多模态推理能力。

本文档面向 `2.0.0`，发布验证范围为 Windows x64。V2 为全新 API，不提供与 V1 的源码、二进制、配置或行为兼容。

安装时请保持所有 DeploySharp 包版本一致；从 Alpha 预览版升级时请重新编译应用，并核对[版本说明](releases/2.0.0.md)中的 API 与平台边界。实际可用版本见仓库 README 的 NuGet 徽章及 GitHub Releases。

## 常用入口

| 目标 | 文档 |
| --- | --- |
| 安装并运行首个推理 | [快速开始](articles/getting-started.md) / [安装与运行时](articles/installation.md) |
| 了解当前版本与平台范围 | [2.0.0 版本说明](releases/2.0.0.md) / [平台与后端支持](articles/platform-support.md) |
| 选择模型与后端 | [模型支持状态](articles/model-support.md) / [模型与后端验证矩阵](model-backend-verification-matrix.md) |
| 下载并校验模型 | [ModelFactory 与 CLI](articles/modelfactory-getting-started.md) |
| 构建视觉任务 | [视觉任务指南](articles/visual-getting-started.md) |
| 选择 NuGet 包组合 | [包组合与安装指南](articles/package-combinations.md) |
| 复现性能测试 | [性能基准方法](articles/performance-benchmarking.md) / [设备性能实测](articles/device-performance-benchmarks.md) |
| 查询公共 API | [API 参考](api/index.md) |

完整的按任务导航见[使用指南](articles/index.md)。
