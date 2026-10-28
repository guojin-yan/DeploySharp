# 快速开始

DeploySharp V2 使用显式后端注册。应用按需安装 Core、领域包和后端适配器；模型文件与厂商原生运行时由应用自行管理。

`2.0.0` 提供 ONNX Runtime、OpenVINO、OpenCV DNN 和 TensorRT 适配器。请先查看[平台与后端支持](platform-support.md)及[模型支持指南](model-support.md)，再选择实际后端和模型工件。

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

## 先检查模型合同

在创建 Session 前，至少确认下面四项：

- 模型文件和所有 external-data sidecar 位于同一受控目录，并已通过 SHA-256 校验；
- 输入名称（示例中的 `images`）与实际图一致，元素类型为 `float32`，形状是 `[1,3,H,W]` 或模型声明的动态形状；
- 输出名称（示例中的 `scores`）及布局已经写入 Profile 或应用配置；
- 当前后端在目标设备上有真实验证记录。可以先用 CPU 验证模型合同，再切换 CUDA、OpenVINO 或 TensorRT。

不要把 `inputValues` 当成可省略的占位变量。实际应用应明确颜色顺序、缩放模式、归一化范围和通道布局；视觉任务请使用 [Visual 快速开始](visual-getting-started.md) 的 `PreparedVisualInput`，不要在张量创建后再次修改原图尺寸。

## 创建项目和验证第一次运行

```powershell
dotnet new console -n DeploySharpQuickStart -f net8.0
cd DeploySharpQuickStart
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
dotnet run
```

第一次运行只做冷启动验证：加载一次工件、打印输入输出元数据、执行一张真实图片，并在 `using` 结束前释放 Session。确认成功后，再把 Session 放入长期存活的池中；不要每一帧创建和销毁 native Session。

## 生命周期和取消

Session 可以复用，但不能跨释放后的 Registry 或 native runtime 使用。调用方应在上层创建一个 `CancellationTokenSource`，将超时和用户取消传入 `Run`/`RunAsync`；捕获取消后应丢弃本次结果并继续使用仍然有效的 Session。对于视频，准备下一帧的任务必须有界，避免输入图片和输出张量无限堆积。

## 最小故障定位顺序

1. 用 `dotnet --info` 和 `Environment.Is64BitProcess` 记录 SDK、运行时和进程位数。
2. 用后端自己的预检接口确认 native DLL 能加载。
3. 打印模型的输入输出名称、类型和维度，与 Profile 逐项比较。
4. 关闭并发和动态 batch，只跑一张固定输入；成功后再逐项打开优化。
5. 将结果和同一模型的参考实现比较，确认预处理和坐标还原，而不是只比较“有没有异常”。
