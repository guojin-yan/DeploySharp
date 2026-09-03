# 快速开始

DeploySharp V2 使用显式后端注册。应用按需安装 Core、领域包和后端适配器；模型文件与厂商原生运行时由应用自行管理。

当前 Alpha 提供 ONNX Runtime、OpenVINO、OpenCV DNN 和 TensorRT 适配器。请先查看[平台与后端支持](platform-support.md)及[模型支持指南](model-support.md)，再选择实际后端和模型工件。

## 最小张量推理

```csharp
using System.Threading;
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
    "models/classifier.onnx",
    preferredBackend: OnnxRuntimeBackendProvider.BackendId);

using IInferenceSession session = backends.CreateSession(
    artifact,
    new BackendRequest(
        BackendCapabilities.TensorInference,
        OnnxRuntimeBackendProvider.BackendId,
        "cpu"),
    SessionOptions.Default);

var input = new Tensor<float>(
    new TensorShape(1, 3, 224, 224),
    inputValues);

InferenceOutputs outputs = session.Run(
    InferenceInputs.Create("images", input),
    CancellationToken.None);

float[] scores = (float[])outputs.GetRequired("scores").Buffer;
Console.WriteLine(scores.Length);
```

输入和输出名称、元素类型及形状必须与实际工件一致。`BackendRegistry` 管理已注册的 Provider；应用负责释放创建的 Session。视觉流程、批量调用和异步预取请继续阅读[使用教程](usage-tutorial.md)。
