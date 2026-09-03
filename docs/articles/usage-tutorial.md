# 使用教程

本教程从一个 .NET 控制台项目开始，展示张量推理、视觉流水线、批量调度、异步预取和大图滑动窗口检测。仓库级验证脚本和完整模型案例位于 `samples/`，不需要复制到业务代码中。

## 1. 创建项目

```powershell
dotnet new console -n DeploySharpQuickstart
cd DeploySharpQuickstart
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
```

如果 Alpha 包尚未出现在配置的 NuGet 源中，请改用本仓库的项目引用。模型文件和原生运行时放在应用自己的目录中。

## 2. 执行一次张量推理

将兼容的 ONNX 文件放到 `models/classifier.onnx`，并根据实际图的名称和形状创建输入：

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
    @"models\classifier.onnx",
    preferredBackend: OnnxRuntimeBackendProvider.BackendId);

var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cpu");

using IInferenceSession session = backends.CreateSession(
    artifact, request, SessionOptions.Default);

var input = new Tensor<float>(
    new TensorShape(1, 3, 224, 224),
    inputValues);

InferenceOutputs outputs = session.Run(
    InferenceInputs.Create("images", input),
    CancellationToken.None);

float[] scores = (float[])outputs.GetRequired("scores").Buffer;
Console.WriteLine(scores.Length);
```

输入/输出名称、元素类型和形状采用严格合同；会话由应用释放，并负责释放对应的原生执行资源。

## 3. 使用视觉流水线

`JYPPX.DeploySharp.Visual` 不负责解码图片。应用先由图像适配器完成解码、缩放、Letterbox、通道转换和归一化，再把带有可逆坐标变换的 `PreparedVisualInput` 交给 `VisualPipeline`：

```csharp
var prepared = imageAdapter.Prepare("images/photo.jpg", cancellationToken);
using var pipeline = new VisualPipeline(
    backendRegistry, selection, request,
    new SessionOptions(maxConcurrency: 2));

VisualInferenceResult result = await pipeline.RunAsync(
    prepared,
    new VisualExecutionOptions(
        timeout: TimeSpan.FromSeconds(30),
        correlationId: "image-42"),
    cancellationToken);
```

解码器根据 Profile 还原源图坐标，并返回分类、检测、分割、姿态、OCR、异常或抠图结果。应用不应在结果外再次缩放坐标。

## 4. 批量和并发

固定 batch=1 的模型可以通过独立 Session 池并发处理多张图片，同时保持输入顺序：

```csharp
IReadOnlyList<VisualInferenceResult> results =
    await pipeline.RunManyAsync(
        preparedInputs,
        cancellationToken: cancellationToken);
```

模型本身声明动态 batch 时，使用 `InferenceBatchScheduler<TInput,TOutput>`，让多个样本进入同一个张量 batch。`SessionOptions.MaxConcurrency` 控制独立推理通道数量；过大的通道数会增加显存和上下文切换开销，应通过目标设备基准选择。

## 5. 视频异步预取

视频或相机流可以在当前帧推理期间准备后续帧。`RunPrefetchedAsync` 使用有界在途窗口，避免无限制积压，并按输入顺序返回结果：

```csharp
IReadOnlyList<VisualInferenceResult> frames =
    await pipeline.RunPrefetchedAsync(
        frameSources,
        (source, token) => imageAdapter.PrepareAsync(source, token),
        prefetch: 2,
        cancellationToken: cancellationToken);
```

预取只重叠准备和推理，不改变 batch-one 模型合同。准备失败、取消和推理异常会沿任务顺序返回。

## 6. 大图滑动窗口检测

大图中存在小目标时，可使用 `SlidingWindowDetectionRunner` 按重叠切片检测。运行器负责切片坐标映射、有限并发和一次全局 NMS：

```csharp
var options = new SlidingWindowDetectionOptions(
    windowSize: new VisualSize(1024, 1024),
    overlap: 0.20f,
    includeFullImagePass: false,
    coordinateMode: SlidingWindowCoordinateMode.Auto);

SlidingWindowDetectionResult tiled =
    await runner.RunAsync(sourceSize, options,
        (window, token) => imageAdapter.PrepareCropAsync(window, token),
        cancellationToken: cancellationToken);

DetectionResult detections = tiled.Detections;
Console.WriteLine($"windows={tiled.WindowCount}, detections={detections.Detections.Count}");
```

`PrepareCropAsync` 是应用提供的图像适配回调，应从一张已解码源图裁出 `window.Bounds` 并返回与检测 Profile 完全匹配的 `PreparedVisualInput`。完整实现要求和坐标模式见[异步帧流水线与滑动窗口](visual-async-and-sliding-window.md)。

建议先从 20% 重叠开始；细长目标可分别调节水平和垂直重叠。只有在大目标可能被切片截断时才打开整图补检。最终 NMS 的 IoU 阈值应与模型类别和目标尺寸一起在目标设备上校准。

## 7. 结果、取消和资源

所有公开异步接口都接受 `CancellationToken`。超时、取消、模型合同错误和后端异常会映射为带稳定错误码的 `VisualException`，其中包含模型、Profile、后端和张量上下文。`VisualPipeline` 释放自己创建的 Session 池；`BackendRegistry` 仍由应用统一管理。更多后端特性和模型边界见[平台与后端支持](platform-support.md)、[模型支持指南](model-support.md)和[设备性能实测](device-performance-benchmarks.md)。
