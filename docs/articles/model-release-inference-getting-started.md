# Release 模型推理快速开始

本文演示如何把 ModelFactory 目录中的模型下载到应用缓存，再交给 DeploySharp 视觉和 ONNX Runtime 合同执行。适用于 `2.0.0-alpha.1` 的 Windows x64 Preview 工件；模型下载、校验和推理后端是三个独立步骤。未指定 `--image` 时，案例会从[默认测试图片 Release](test-images.md) 下载并校验 `bus.jpg`。

## 前置条件

- .NET 8 SDK；
- 能访问目标 Release 的网络，或已有完整的离线缓存；
- 与模型输入尺寸匹配的本地图片；
- 应用自行部署的 ONNX Runtime、OpenCV 和其他 native runtime。

## 使用 Release 推理案例

案例位于 `samples/06-models/release-inference`。以 BRIA RMBG 2.0 为例：

~~~powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision fp32 --quantization none
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision fp32 --quantization none --image .\images\your-image.jpg
~~~

案例会将模型下载到应用缓存，校验 ModelPack、文件大小和 SHA256，然后执行背景 Alpha 推理，默认输出 `deploysharp-alpha.pgm`。RMBG 1.4 使用 `--model-id bria/rmbg-1.4`；PaDiM 使用 `anomalib/padim/mvtec-bottle`。

## 离线复用

首次在线运行成功后，可以指定应用自己的缓存目录并切换到离线模式：

~~~powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image .\images\your-image.jpg --cache .\.cache\deploysharp --offline
~~~

`--offline` 会强制使用已完成并已验证的本地包；缺少文件、大小或 SHA256 不匹配时直接失败，不会静默联网下载。

## 从目录到 Session 的完整检查

Release 推理案例实际经过以下阶段：

1. 按 `model-id`、backend、format、precision 和 quantization 选择唯一目录项；
2. 下载主模型、external data、标签、tokenizer 和 ModelPack 清单到临时目录；
3. 验证相对路径、文件大小、SHA-256 和完成标志后原子提交到缓存；
4. 根据目录中的 Profile 创建视觉输入和 decoder；
5. 注册目标后端，创建 Session，执行一次真实输入并将输出保存到案例目录。

任一步失败都应修复对应层，不要通过改文件名或关闭哈希校验绕过。尤其是 TensorRT Engine、OpenVINO IR 和 GGUF 的可移动性不同：目录中存在资产只表示可以下载，仍需在目标设备加载验证。

可以先用 CLI 查看选择结果，再运行案例：

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- show `
  --model-id bria/rmbg-2.0 --preview
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- `
  --model-id bria/rmbg-2.0 --precision fp32 --quantization none
```

目录 CLI 的 `--preview` 用于查询 Preview 记录；推理案例内部已经显式允许 Preview，并固定使用 ONNX Runtime CPU，因此不接收 `--preview` 或 `--backend`。可运行 `dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --help` 查看案例的完整参数。

## 支持边界

模型目录中的 `Preview` 条目需要显式打开 Preview 查询；目录中未出现的模型不能用相近名称替代。TensorRT Engine、Tokenizer、OpenVINO XML/BIN 和其他 sidecar 仍由应用按对应后端要求准备。具体模型与后端状态以[模型支持指南](model-support.md)、[官方模型目录](model-catalog.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。

## 常见结果和处理

| 结果 | 含义 | 处理 |
| --- | --- | --- |
| `catalog miss` | 目录没有该模型/后端/精度组合 | 先查看 `list --preview`，不要猜 ID |
| `offline-cache-miss` | 缓存未完整校验或被修改 | 删除该模型的临时目录后重新安装 |
| native DLL 错误 | 后端 runtime 不在发布目录或版本不匹配 | 先跑后端预检，再重试同一工件 |
| 模型加载成功但结果异常 | Profile 或输入预处理不匹配 | 对照模型目录中的输入输出合同 |

案例的输出应包含模型 ID、版本、后端、设备和工件 SHA-256，便于把“能运行”与“结果正确”区分开。
