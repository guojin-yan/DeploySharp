# ADR 0006: ModelPack JSON schema and safe local resolution / ModelPack JSON Schema 与安全本地解析

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-04

## Decision / 决策

DeploySharp V2 uses a strict, versioned `2.0` JSON manifest for portable model packages. The schema describes model identity, task, tensors, exporter provenance, source/license metadata, and one or more backend-specific artifacts. An artifact can be a single file or a directory entrypoint and always lists every required file with byte size, SHA256, media type, and role. / DeploySharp V2 使用严格且有版本的 `2.0` JSON 清单描述可移植模型包。Schema 描述模型标识、任务、张量、导出来源、来源/许可证元数据以及一个或多个后端工件。工件可以是单文件或目录入口点，并且始终列出每个必需文件的字节大小、SHA256、媒体类型和角色。

The managed package publishes `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`. .NET Framework 4.6.1 through 4.8.1, .NET Core 3.1, and .NET 5 through .NET 7 applications consume the `netstandard2.0` asset. Direct assets for older frameworks are omitted because the verified `System.Text.Json` 10.0.10 build emits unsupported-TFM warnings there. / 托管包发布 `netstandard2.0`、`net8.0`、`net9.0` 和 `net10.0`。.NET Framework 4.6.1 到 4.8.1、.NET Core 3.1 以及 .NET 5 到 .NET 7 应用使用 `netstandard2.0` 资产。由于已验证的 `System.Text.Json` 10.0.10 在这些目标上产生不受支持 TFM 警告，因此不发布旧框架的直接资产。

Manifest parsing rejects comments, trailing commas, duplicate or unknown properties, invalid enum values, unsupported major versions, and unsafe package-relative paths. The local loader resolves paths below the manifest directory, verifies file size and SHA256 by default, and fails closed for unsafe symbolic links/reparse points. / 清单解析拒绝注释、尾逗号、重复或未知属性、无效枚举值、不支持的主版本以及不安全的包内相对路径。本地加载器只在清单目录下解析路径，默认验证文件大小和 SHA256，并对不安全符号链接/重解析点采取封闭失败策略。

## Consequences / 影响

- The format is suitable for ONNX plus external data, OpenVINO XML/BIN directories, GGUF files, and future portable formats; device-bound TensorRT engines can be marked `portable: false` and are not implied to be cross-device artifacts. / 该格式适用于 ONNX 外部数据、OpenVINO XML/BIN 目录、GGUF 文件和未来的可移植格式；设备绑定的 TensorRT engine 可标记 `portable: false`，不会被误认为跨设备工件。
- Source and license metadata are mandatory so future model distribution tooling can audit provenance before publishing. / 来源和许可证元数据是必需的，使未来模型分发工具可在发布前审计来源。
- The ModelFactory/download service is intentionally outside this module and will be designed in a later stage. / ModelFactory/下载服务有意不属于本模块，将在后续阶段设计。
