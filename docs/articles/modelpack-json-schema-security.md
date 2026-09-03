# ModelPack JSON Schema 与路径安全

本文补充 `JYPPX.DeploySharp.ModelPack.Json` 的 Schema 版本、严格读取和包内路径规则，适用于 `2.0.0-alpha.1`。创建清单的完整示例见[ModelPack JSON 快速开始](modelpack-json-getting-started.md)。

## Schema 版本

规范 Schema 为内嵌的 Draft 2020-12，可通过 `ModelPackageSchema.GetJson()` 获取。当前验证器支持 Schema 主版本 `2`；不同主版本直接拒绝，未知关键属性或超出限制的内容会返回诊断。读取器使用 UTF-8 大小上限，属性名区分大小写且不得重复，禁止注释和尾逗号。

确定性序列化使用固定属性顺序，并按序号排序扩展字典。同一份已校验的 `ModelPackageDocument` 在相同版本下会产生稳定文本，适合缓存、差异检查和发布前校验。

## 包内路径

包内路径只能使用正斜杠的相对形式。以下内容会被拒绝：

- 空路径段、`.`、`..`、根路径、UNC 路径和盘符路径；
- 控制字符、保留设备名、尾随点或空格；
- 在不同工件中重复出现的规范化路径。

加载器将清单所在目录作为包根，逐级检查每个路径组件。根目录重解析点、符号链接或其他重解析路径不能把读取范围带出包根；在无法可靠解析的目标框架上，加载器采取失败关闭策略。

## 来源元数据

当前 Schema 的 `source` 对象用于保存模型来源、修订、作者和许可证字段。若清单提供 `source`，`sourceUrl`、`revision`、`author` 和 `licenseExpression` 或 `licenseFile` 必须满足格式校验；`redistributionAllowed` 是显式布尔值。该元数据不会替代后端安装、模型格式检查或应用自己的发布流程。

## 目标框架

ModelPack.Json 的直接 NuGet 资产与项目事实来源保持一致；不要把“可由某个应用引用”写成包本身发布了该 TFM。完整的跨包矩阵以[平台与后端支持](platform-support.md)为准。

| 包资产 | 直接构建目标 |
| --- | --- |
| `netstandard2.0` | `netstandard2.0` |
| `net8.0` | `net8.0` |
| `net9.0` | `net9.0` |
| `net10.0` | `net10.0` |

应用可以在支持 `netstandard2.0` 的目标（例如 .NET Framework 4.6.1+、.NET Core 3.1 和 .NET 5–7）中消费兼容资产，但这不改变包的直接构建目标。最终应用仍需按目标框架和 RID 选择合适的 native 后端包。

## 验证建议

在把包交给推理后端前，先调用 `ModelPackageValidator.Validate`，再使用 `ModelPackageLoader.Load` 读取本地目录。校验失败时直接修复清单或文件，不要绕过诊断，也不要执行模型目录中携带的脚本。后端和模型状态以[模型支持指南](model-support.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。
