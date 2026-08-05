# ONNX Runtime quick start / ONNX Runtime 快速开始

Install the DeploySharp adapter and exactly one application-selected official runtime. The adapter does not contain `onnxruntime.dll`, CUDA, DirectML, or another native provider. / 安装 DeploySharp 适配器和一个由应用选择的官方运行时。适配器不包含 `onnxruntime.dll`、CUDA、DirectML 或其他原生 Provider。

```powershell
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

```csharp
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();

var artifact = new ModelArtifact(
    new ModelId("examples/classifier"),
    "onnx",
    @"models\classifier.onnx",
    preferredBackend: OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cpu");

using IInferenceSession session = backends.CreateSession(artifact, request, new SessionOptions(maxConcurrency: 1));
var tensor = new Tensor<float>(new TensorShape(1, 3, 224, 224), inputValues);
InferenceOutputs outputs = session.Run(InferenceInputs.Create("images", tensor), cancellationToken);
float[] scores = (float[])outputs.GetRequired("scores").Buffer;
```

Input and output names are exact and ordinal. Every model input must be supplied once, with no extras. Shapes accept runtime values only where model metadata is dynamic (`-1`). Boolean, signed/unsigned 8/16/32/64-bit integers, Float32, and Float64 are verified. String, Float16, and BFloat16 currently return a stable unsupported-type diagnostic instead of reinterpreting memory. / 输入输出名称使用精确序号匹配。必须且只能提供每个模型输入一次。仅模型元数据为动态维度（`-1`）的位置可接受运行时尺寸。已验证 Boolean、有符号/无符号 8/16/32/64 位整数、Float32 和 Float64。String、Float16 与 BFloat16 当前返回稳定的不支持类型诊断，不会错误重解释内存。

For Visual, also install `JYPPX.DeploySharp.Visual`, register a `VisualModelProfile`, and pass a `PreparedVisualInput`. Visual does not reference ONNX Runtime and this stage does not decode image files. The package-only executable under `tests/clean-consumer/onnxruntime` demonstrates both Core named inference and Visual classification. / Visual 场景还需安装 `JYPPX.DeploySharp.Visual`、注册 `VisualModelProfile` 并传入 `PreparedVisualInput`。Visual 不引用 ONNX Runtime，本阶段也不解码图片文件。`tests/clean-consumer/onnxruntime` 下的纯包程序同时演示 Core 命名推理和 Visual 分类。

ONNX external data is resolved by ONNX Runtime relative to the graph file. List the graph and every external-data file in one ModelPack.Json artifact so path, size, and SHA256 checks complete before session creation. Do not execute scripts shipped beside a model. / ONNX external data 由 ONNX Runtime 相对计算图文件解析。应在同一 ModelPack.Json 工件中列出计算图和全部 external-data 文件，使路径、大小和 SHA256 在创建会话前完成校验。不要执行模型旁附带的脚本。
