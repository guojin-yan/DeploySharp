# Visual prepared-tensor quick start / Visual 已准备张量快速开始

`JYPPX.DeploySharp.Visual` provides image-library-neutral classification and detection workflows. It does not read image files or reference OpenCV. Install an image adapter and an inference backend separately; until those packages are available, applications and tests can prepare a Core tensor directly. / `JYPPX.DeploySharp.Visual` 提供不绑定图像库的分类与检测流程。它不读取图片文件，也不引用 OpenCV。图像适配器和推理后端需分别安装；在这些包发布前，应用和测试可以直接准备 Core 张量。

```powershell
dotnet add package JYPPX.DeploySharp.Visual --version 2.0.0-alpha.1
# Add one backend package separately, for example the future Backend.OnnxRuntime.
# 另行添加一个后端包，例如后续的 Backend.OnnxRuntime。
```

## 1. Register a profile / 注册 Profile

A profile is the explicit contract for one supported model export. Tensor names and shapes must match the actual artifact. / Profile 是某个受支持模型导出物的显式契约；张量名称与形状必须和真实工件一致。

```csharp
var modelId = new ModelId("demo/classifier");
var profile = new VisualModelProfile(
    "demo/classifier.onnx.v1",
    modelId,
    VisualTaskId.ImageClassification,
    "1.0",
    "onnx",
    new VisualInputBinding(
        "images", TensorElementType.Float32,
        new TensorShape(1, 3, 224, 224), VisualTensorLayout.Nchw),
    new[]
    {
        new VisualOutputBinding(
            "scores", TensorElementType.Float32, new TensorShape(1, 1000))
    },
    labels,
    new ClassificationDecoder("scores", ClassificationScoreMode.Logits, topK: 5));

var profiles = new VisualProfileRegistry();
profiles.Register(profile);
profiles.Freeze();
```

The registry is instance-scoped. Freeze it after startup so selection is deterministic and concurrent reads are safe. / 注册中心是实例级对象。启动期注册完成后应冻结，以确保选择确定且并发读取安全。

## 2. Prepare input outside Visual / 在 Visual 外部准备输入

An image adapter performs decode, resize/letterbox/crop, channel order, layout conversion, and normalization. It then supplies the tensor and the exact transform used. / 图像适配器负责解码、Resize/Letterbox/Crop、通道顺序、布局转换和归一化，然后提供张量及实际使用的精确变换。

```csharp
var sourceSize = new VisualSize(1920, 1080);
var modelSize = new VisualSize(224, 224);
var transform = ImageTransform.Letterbox(sourceSize, modelSize);
var tensor = new Tensor<float>(
    new TensorShape(1, 3, 224, 224), preparedValues);

using var input = new PreparedVisualInput(
    "images", tensor, sourceSize, modelSize, 1,
    VisualTensorLayout.Nchw, transform,
    new VisualPreprocessingDescriptor(
        VisualColorOrder.Rgb,
        means: new[] { 0.485f, 0.456f, 0.406f },
        scales: new[] { 1f / 0.229f, 1f / 0.224f, 1f / 0.225f }));
```

The default ownership is borrowed: disposing `input` does not dispose the tensor. An adapter that rents native or pooled memory can use `PreparedInputOwnership.Owned` and attach its disposable lease. / 默认所有权为 Borrowed：释放 `input` 不会释放张量。租用原生内存或池化内存的适配器可使用 `PreparedInputOwnership.Owned` 并附加其可释放租约。

## 3. Select and run / 选择并运行

Register a backend provider in Core, convert a verified ModelPack artifact to Core `ModelArtifact`, then select the profile/backend pair. / 在 Core 中注册后端 Provider，把已验证 ModelPack 工件转换为 Core `ModelArtifact`，再选择 Profile/后端组合。

```csharp
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    backendId: new BackendId("onnxruntime"));
VisualProfileSelection selection = profiles.Select(
    artifact, backendRegistry, request, VisualTaskId.ImageClassification);

using var pipeline = new VisualPipeline(
    backendRegistry, selection, request,
    new SessionOptions(maxConcurrency: 1));
VisualInferenceResult result = await pipeline.RunAsync(
    input,
    new VisualExecutionOptions(
        timeout: TimeSpan.FromSeconds(30), correlationId: "request-42"),
    cancellationToken);

var classification = (ClassificationResult)result.Value;
```

The pipeline owns and releases the created backend session. The application retains ownership of `BackendRegistry`. Errors are reported as `VisualException` with a stable `Code`, model/profile/backend/tensor context, `InnerException`, and technical details. / Pipeline 拥有并释放创建的后端会话；应用保留 `BackendRegistry` 的所有权。错误通过 `VisualException` 报告，包含稳定 `Code`、模型/Profile/后端/张量上下文、`InnerException` 与技术细节。

## Detection / 检测

Use `DetectionOutputSchema` to describe the actual dense output layout, including XYXY/XYWH/CXCYWH coordinates, normalized or absolute values, class-score offset, and optional objectness. `DetectionDecoderOptions` configures score threshold, IoU threshold, class-aware/class-agnostic NMS, and limits. / 使用 `DetectionOutputSchema` 描述真实稠密输出布局，包括 XYXY/XYWH/CXCYWH、归一化或绝对坐标、类别分数偏移和可选 objectness。`DetectionDecoderOptions` 配置分数阈值、IoU 阈值、按类别/忽略类别 NMS 以及数量限制。
