# ModelPack JSON schema, provenance, and security / ModelPack JSON Schema、来源与安全

## Schema and versioning / Schema 与版本

The canonical Draft 2020-12 schema is embedded in the managed assembly and is available from `ModelPackageSchema.GetJson()`. The managed validator supports schema major `2`; newer minor versions are accepted by default only when no unknown critical property is present. A different major version is always rejected. / 规范 Draft 2020-12 Schema 以内嵌资源保存在托管程序集内，并可通过 `ModelPackageSchema.GetJson()` 获取。托管验证器支持 Schema 主版本 `2`；默认只有在没有未知关键属性时才接受更高次版本。不同主版本始终拒绝。

The JSON reader is strict: UTF-8 input is bounded, object property names are case-sensitive and unique, comments and trailing commas are disallowed, and unknown properties are diagnosed. Deterministic serialization uses the documented property order and ordinal sorting for extension dictionaries, so the same validated document produces the same text. / JSON 读取器是严格的：UTF-8 输入有大小上限，对象属性名区分大小写且必须唯一，禁止注释和尾逗号，并诊断未知属性。确定性序列化使用文档规定的属性顺序和序号排序扩展字典，因此同一个已验证文档产生相同文本。

## Safe paths and links / 安全路径与链接

Package paths are portable forward-slash paths. Empty segments, `.`, `..`, rooted/UNC/drive-qualified paths, control characters, reserved device names, and trailing dots or spaces are rejected. Normalized paths are unique across all artifacts, not only within one artifact. / 包内路径是可移植的正斜杠路径。空段、`.`、`..`、根路径/UNC/盘符路径、控制字符、保留设备名以及以点或空格结尾的路径都会被拒绝。规范化路径在所有工件之间全局唯一，而不只是单个工件内唯一。

The loader treats the manifest directory as the package root. It rejects a root reparse point and walks every path component. On modern .NET it resolves link targets and requires them to remain within the root; on older targets it fails closed when a reparse point cannot be reliably resolved. / 加载器将清单目录作为包根。它拒绝根重解析点并遍历每个路径组件。在现代 .NET 上会解析链接目标并要求目标仍在根目录内；在无法可靠解析重解析点的旧目标上采取封闭失败。

## Provenance and licenses / 来源与许可证

`source.sourceUrl`, `source.revision`, and `source.author` are required. A manifest must provide an SPDX `licenseExpression` or a package-relative `licenseFile`; if a license file is declared it must be listed in an artifact. `redistributionAllowed` is explicit metadata for future publication workflows and does not override the upstream license. / `source.sourceUrl`、`source.revision` 和 `source.author` 是必需的。清单必须提供 SPDX `licenseExpression` 或包内相对 `licenseFile`；声明许可证文件时，该文件必须列在某个工件中。`redistributionAllowed` 是未来发布流程使用的显式元数据，不会覆盖上游许可证。

## Compatibility matrix / 兼容性矩阵

| Package asset / 包资产 | Directly built / 直接构建 | Consumer note / 使用说明 |
|---|---|---|
| `netstandard2.0` | .NET Framework 4.6.1–4.8.1, .NET Core 3.1, .NET 5–7 | Uses the portable System.Text.Json asset. / 使用可移植 System.Text.Json 资产。 |
| `net8.0` | .NET 8 | Modern optimized asset. / 现代优化资产。 |
| `net9.0` | .NET 9 | Modern optimized asset. / 现代优化资产。 |
| `net10.0` | .NET 10 | Modern optimized asset. / 现代优化资产。 |

The package intentionally does not publish direct `net46`, `netcoreapp3.1`, or `net5.0`–`net7.0` assets because verified `System.Text.Json` 10.0.10 emits unsupported-TFM build warnings for those direct targets. / 由于已验证的 System.Text.Json 10.0.10 对这些直接目标产生不受支持 TFM 构建警告，本包有意不发布 `net46`、`netcoreapp3.1` 或 `net5.0`–`net7.0` 的直接资产。
