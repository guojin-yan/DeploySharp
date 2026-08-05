# ModelPack JSON quick start / ModelPack JSON 快速开始

`JYPPX.DeploySharp.ModelPack.Json` validates a portable model manifest, serializes it deterministically, and loads a local package with integrity checks. It is format- and backend-neutral; install a backend package separately. / `JYPPX.DeploySharp.ModelPack.Json` 验证可移植模型清单、以确定性方式序列化，并在完整性检查下加载本地包。它不绑定模型格式和推理后端；后端包需要单独安装。

```powershell
dotnet add package JYPPX.DeploySharp.ModelPack.Json --version 2.0.0-alpha.1
```

The minimum manifest contains `schemaVersion`, model identity, `exporter`, `source`, empty-or-populated `inputs` and `outputs`, and at least one `artifacts` entry. / 最小清单包含 `schemaVersion`、模型标识、`exporter`、`source`、可为空或有内容的 `inputs` 和 `outputs`，以及至少一个 `artifacts` 项。

```csharp
using JYPPX.DeploySharp.ModelPack.Json;

var document = new ModelPackageDocument(
    "2.0", "demo/resnet", "Demo ResNet", "resnet", "classification", "1.0.0",
    new ModelExporterDocument("torch.onnx", "2.3.0"),
    new ModelSourceDocument(
        "https://example.com/source", "https://example.com/project", "main",
        "Example Org", null, "Apache-2.0", null, redistributionAllowed: true),
    generatedAt: DateTimeOffset.UtcNow,
    profileId: "image-classification",
    inputs: Array.Empty<ModelTensorSignatureDocument>(),
    outputs: Array.Empty<ModelTensorSignatureDocument>(),
    artifacts: new[]
    {
        new ModelArtifactDocument(
            "onnx.cpu", "onnx", ModelArtifactLocationKind.File, "model.onnx",
            new[] { "onnxruntime" },
            new[] { new ModelFileDocument("model.onnx", "<64 lowercase hex SHA256>", 1234, "application/onnx", ModelFileRole.Model) },
            opset: 17, portable: true)
    });

ValidatedModelPackage manifest = ModelPackageValidator.Validate(document);
File.WriteAllText("manifest.json", ModelPackageJsonSerializer.Serialize(manifest));
LocalModelPackage loaded = ModelPackageLoader.Load("manifest.json");
var coreArtifacts = loaded.ToCoreArtifacts();
```

Calculate the SHA256 and byte size from the exact bytes that will be distributed. `ModelPackageLoader` does not trust the manifest: it checks that each declared file exists beneath the manifest directory and that the declared size/hash match. / SHA256 和字节大小必须根据将要分发的精确字节计算。`ModelPackageLoader` 不信任清单：它会检查每个声明文件位于清单目录下且声明的大小/hash 匹配。

## Artifact layouts / 工件布局

| Layout / 布局 | Example / 示例 | Rules / 规则 |
|---|---|---|
| Single file / 单文件 | ONNX, GGUF | `locationKind: "file"`; `entrypoint` must match one listed file. / `locationKind: "file"`；`entrypoint` 必须匹配一个已列出的文件。 |
| Directory / 目录 | OpenVINO XML + BIN | `locationKind: "directory"`; every file must be below the directory entrypoint. / `locationKind: "directory"`；所有文件必须位于目录入口点下。 |
| Multi-file / 多文件 | ONNX + external tensor data, tokenizer files | List every required file with a distinct normalized path. / 列出每个必需文件，并使用互不重复的规范化路径。 |

`compatibleBackends` records capability matching only; it does not load or install a backend. `portable` documents whether the artifact is intended to move between compatible devices. / `compatibleBackends` 只记录能力匹配，不负责加载或安装后端。`portable` 说明工件是否设计为可在兼容设备间移动。

## Error handling / 错误处理

Catch `ModelPackageValidationException` and inspect `Diagnostics`. Each diagnostic has a stable code, JSON path, artifact id, and package-relative file path where available. The original I/O or JSON exception is retained as `InnerException` and technical details are preserved for logs. / 捕获 `ModelPackageValidationException` 并检查 `Diagnostics`。每条诊断包含稳定代码、JSON 路径、工件标识以及可用时的包内相对文件路径。原始 I/O 或 JSON 异常保留在 `InnerException` 中，技术细节也会保留用于日志。
