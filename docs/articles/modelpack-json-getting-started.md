# ModelPack JSON 快速开始

`JYPPX.DeploySharp.ModelPack.Json` 用于描述可移动的模型工件，提供严格校验、确定性序列化和本地包加载。它不绑定 ONNX Runtime、OpenVINO、TensorRT 或 LLamaSharp；实际推理后端需要单独安装。

## 安装

~~~powershell
dotnet add package JYPPX.DeploySharp.ModelPack.Json --version 2.0.0
~~~

## 创建并校验清单

清单至少需要 `schemaVersion`、模型标识、导出器、来源、输入和输出数组，以及一个或多个 `artifacts`。来源和许可证字段属于当前 Schema 的完整性元数据，不代表 ModelPack 会替应用下载或审核第三方内容。

~~~csharp
using JYPPX.DeploySharp.ModelPack.Json;

var document = new ModelPackageDocument(
    "2.0", "demo/resnet", "Demo ResNet", "resnet", "classification", "1.0.0",
    new ModelExporterDocument("torch.onnx", "2.3.0"),
    new ModelSourceDocument(
        "https://example.com/source", "https://example.com/project", "main",
        "Example Org", null, "Apache-2.0", null,
        redistributionAllowed: true),
    generatedAt: DateTimeOffset.UtcNow,
    profileId: "image-classification",
    inputs: Array.Empty<ModelTensorSignatureDocument>(),
    outputs: Array.Empty<ModelTensorSignatureDocument>(),
    artifacts: new[]
    {
        new ModelArtifactDocument(
            "onnx.cpu", "onnx", ModelArtifactLocationKind.File, "model.onnx",
            new[] { "onnxruntime" },
            new[]
            {
                new ModelFileDocument(
                    "model.onnx", "<64 lowercase hex SHA256>", 1234,
                    "application/onnx", ModelFileRole.Model)
            },
            opset: 17, portable: true)
    });

ValidatedModelPackage manifest = ModelPackageValidator.Validate(document);
File.WriteAllText(
    "manifest.json",
    ModelPackageJsonSerializer.Serialize(manifest));
LocalModelPackage loaded = ModelPackageLoader.Load("manifest.json");
var coreArtifacts = loaded.ToCoreArtifacts();
~~~

SHA256 和字节大小必须根据将要分发的精确文件计算。`ModelPackageLoader` 不信任清单中的声明：它会检查每个文件位于清单目录下，并重新核对声明的大小和哈希。

## 工件布局

| 布局 | 示例 | 要求 |
| --- | --- | --- |
| 单文件 | ONNX、GGUF | `locationKind: "file"`；`entrypoint` 必须等于清单中的一个文件。 |
| 目录 | OpenVINO XML + BIN | `locationKind: "directory"`；列出的文件必须全部位于目录入口点下。 |
| 多文件 | ONNX external data、Tokenizer | 每个必需文件都单独列出，并使用互不重复的规范化路径。 |

`compatibleBackends` 只记录能力匹配，不会加载或安装后端。`portable` 说明工件是否设计为在兼容设备之间移动；TensorRT Engine 这类与设备和运行时绑定的文件通常不应标记为可移植。

## 加载错误

捕获 `ModelPackageValidationException` 并查看 `Diagnostics`。每条诊断包含稳定代码、JSON 路径、工件标识和可用时的包内文件路径；原始 I/O 或 JSON 异常保留在 `InnerException` 中。

严格读取器限制 UTF-8 大小、属性名大小写和重复属性，禁止注释与尾逗号，并诊断未知属性。确定性序列化使用固定属性顺序和扩展字典的序号排序，使同一份已校验文档能够产生相同文本。

包内路径必须是正斜杠相对路径。根路径、UNC/盘符路径、`.`、`..`、控制字符、保留设备名及尾随点或空格都会被拒绝；规范化路径必须在整个包内全局唯一。

## Schema 与路径安全

规范 Schema 使用 Draft 2020-12，可通过 `ModelPackageSchema.GetJson()` 取得。当前读取器支持 Schema 主版本 `2`，限制 UTF-8 大小，属性名区分大小写且不能重复，并禁止注释和尾逗号。确定性序列化固定属性顺序并按序号排序扩展字典，适合缓存和发布差异检查。

加载器以清单目录为包根，并逐级检查每个路径组件。符号链接、junction 或其他重解析点不能把读取范围带出包根；无法可靠确认时采用失败关闭。`source` 中的来源、修订、作者、许可证和再分发字段属于清单元数据，不替代第三方授权审查，也不会触发下载或执行。

直接 NuGet 资产为 `netstandard2.0`、`net8.0`、`net9.0` 和 `net10.0`。较旧应用可能消费兼容的 `netstandard2.0` 资产，但这不会改变包的直接构建目标；native 后端仍需按应用 TFM、RID 和设备选择。

## 发布与升级规则

- 修改任意模型或 sidecar 后，重新计算大小与 SHA-256，并发布新的不可变 revision；
- 模型权重变化但合同不变时提升 Release revision；输入输出合同变化时使用新模型 ID 或明确变体；
- 新增可选字段且旧读取器能安全忽略时可保留主版本；改变字段含义、路径或校验语义时提升 Schema 主版本；
- 把包交给推理后端前，先执行 `ModelPackageValidator.Validate` 和 `ModelPackageLoader.Load`，不要绕过诊断。

模型与后端状态见[模型支持指南](model-support.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)。
