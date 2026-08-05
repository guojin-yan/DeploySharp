# OpenVINO quick start / OpenVINO 快速开始

Install the DeploySharp adapter and the runtime selected by the final application. The adapter depends only on the managed `JYPPX.OpenVINO.CSharp.API`; it does not embed OpenVINO native libraries, device plug-ins, GenAI, or OpenCV. This release verifies Windows x64 CPU with the matching runtime below. / 安装 DeploySharp 适配器和最终应用选择的运行时。适配器仅依赖托管的 `JYPPX.OpenVINO.CSharp.API`，不内嵌 OpenVINO 原生库、设备插件、GenAI 或 OpenCV。本版本使用下列匹配运行时验证 Windows x64 CPU。

```powershell
dotnet add package JYPPX.DeploySharp.Backend.OpenVINO --version 2.0.0-alpha.1
dotnet add package OpenVINO.runtime.win --version 2026.2.1
```

```csharp
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

using var backends = new BackendRegistry();
backends.UseOpenVino(new OpenVinoOptions(device: "CPU"));

var artifact = new ModelArtifact(
    new ModelId("examples/classifier"),
    "onnx",
    @"models\classifier.onnx",
    preferredBackend: OpenVinoBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OpenVinoBackendProvider.BackendId,
    "CPU");

using IInferenceSession session = backends.CreateSession(
    artifact,
    request,
    new SessionOptions(maxConcurrency: 1));
var tensor = new Tensor<float>(new TensorShape(1, 3, 224, 224), inputValues);
InferenceOutputs outputs = await session.RunAsync(
    InferenceInputs.Create("images", tensor),
    cancellationToken);
float[] scores = (float[])outputs.GetRequired("scores").Buffer;
```

For OpenVINO IR, set the format to `openvino-ir` and use the `.xml` file as the primary path. The sibling `.bin` is mandatory. A ModelPack.Json manifest must list and verify both files by size and SHA256 before backend selection. / 对于 OpenVINO IR，将格式设为 `openvino-ir` 并以 `.xml` 为主路径；同目录 `.bin` 是必需文件。进入后端选择前，ModelPack.Json 清单必须按大小和 SHA256 列出并验证两个文件。

Names are exact and ordinal. Every input must be supplied once with no extras. Dynamic dimensions appear as `-1`. Boolean, signed and unsigned 8/16/32/64-bit integers, Float32, and Float64 are verified. String, Float16, BFloat16, and rank-zero inputs return stable unsupported diagnostics in this managed-wrapper baseline. / 名称采用精确序号匹配。每个输入必须且只能提供一次。动态维度表示为 `-1`。已验证 Boolean、有符号和无符号 8/16/32/64 位整数、Float32 与 Float64。String、Float16、BFloat16 以及零秩输入在当前托管包装器基线中返回稳定的不支持诊断。

For Visual, also install `JYPPX.DeploySharp.Visual`, register a `VisualModelProfile`, and pass `PreparedVisualInput`. The package-only executable under `tests/clean-consumer/openvino` demonstrates named inference and classification without an image library. / Visual 场景还需安装 `JYPPX.DeploySharp.Visual`、注册 `VisualModelProfile` 并传入 `PreparedVisualInput`。`tests/clean-consumer/openvino` 的纯包程序演示了不依赖图像库的命名推理和分类。
