# Getting Started / 快速开始

DeploySharp V2 uses explicit backend registration. Applications install Core, a domain package, and only the backend adapters they need. / DeploySharp V2 使用显式后端注册；应用安装 Core、所需领域包以及实际使用的后端适配器即可。

> The current alpha contains the Core contracts and a test-only fake backend. A runnable production-backend example will be added with the first backend package. / 当前 alpha 仅包含 Core 契约和测试专用 Fake Backend；首个后端包完成时会补充可运行的生产后端示例。

```csharp
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;

using var runtime = new DeploySharpRuntimeBuilder()
    .AddBackend(provider)
    .Build();

using var session = runtime.CreateSession(
    new ModelArtifact(new ModelId("vision/example"), "onnx", "model.onnx"),
    new BackendRequest(BackendCapabilities.TensorInference));
```

The runtime owns registered backend providers and disposes them when the runtime is disposed. Sessions remain caller-owned. / Runtime 拥有已注册后端提供程序，并在自身释放时释放它们；推理会话仍由调用方负责释放。
