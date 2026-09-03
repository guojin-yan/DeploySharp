# ModelPack JSON 快速开始

`JYPPX.DeploySharp.ModelPack.Json` 用于描述可移动的模型工件，提供严格校验、确定性序列化和本地包加载。它不绑定 ONNX Runtime、OpenVINO、TensorRT 或 LLamaSharp；实际推理后端需要单独安装。

## 安装

~~~powershell
dotnet add package JYPPX.DeploySharp.ModelPack.Json --version 2.0.0-alpha.1
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
