# Visual 快速开始

JYPPX.DeploySharp.Visual 只处理已经准备好的张量和模型输出，不读取图片文件，也不依赖 OpenCV。请按需安装图像适配器和推理后端。

## 创建 Profile 与输入

Profile 是一个具体模型导出的输入、输出和 decoder 合同；名称、维度、布局和标签必须与模型一致。

~~~csharp
var profile = new VisualModelProfile(
    "demo/classifier.onnx.v1",
    new ModelId("demo/classifier"),
    VisualTaskId.ImageClassification,
    "1.0",
    "onnx",
    new VisualInputBinding(
        "images", TensorElementType.Float32,
        new TensorShape(1, 3, 224, 224),
        VisualTensorLayout.Nchw),
    new[]
    {
        new VisualOutputBinding(
            "scores", TensorElementType.Float32,
            new TensorShape(1, 1000))
    },
    labels,
    new ClassificationDecoder(
        "scores", ClassificationScoreMode.Logits, topK: 5));

var transform = ImageTransform.Letterbox(
    new VisualSize(1920, 1080), new VisualSize(224, 224));
using var input = new PreparedVisualInput(
    "images",
    new Tensor<float>(
        new TensorShape(1, 3, 224, 224), preparedValues),
    new VisualSize(1920, 1080),
    new VisualSize(224, 224),
    1,
    VisualTensorLayout.Nchw,
    transform,
    preprocessing);
~~~

图像文件建议使用 Visual.OpenCV 等适配器创建 PreparedVisualInput。适配器负责解码、resize/letterbox、通道顺序、布局和归一化，并附带实际使用的 ImageTransform。

## 选择后端并推理

注册后端后，用已验证的 ModelArtifact 和 Profile 选择组合，再创建 VisualPipeline。Pipeline 会管理自己创建的推理 session；BackendRegistry 仍由应用负责释放。

~~~csharp
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    new BackendId("onnxruntime"),
    "cpu");
VisualProfileSelection selection =
    profiles.Select(artifact, backendRegistry, request,
        VisualTaskId.ImageClassification);
using var pipeline = new VisualPipeline(
    backendRegistry, selection, request,
    new SessionOptions(maxConcurrency: 1));
VisualInferenceResult result =
    await pipeline.RunAsync(input, cancellationToken: cancellationToken);
~~~

## 批量、异步和滑动窗口

- RunManyAsync 适合固定 batch 模型的有序并发调用。
- InferenceBatchScheduler 适合 Profile 明确声明动态 batch 的模型；真正 batch 需要一个首维大于 1 的张量和对应的 batch decoder。
- RunPrefetchedAsync 会在当前帧推理时有界准备后续帧，保持结果顺序，适合视频流。
- SlidingWindowDetectionRunner 将大图切片、重叠检测、坐标还原和全局 NMS 组合为一个流程。建议从 20% overlap 开始；只有大目标可能被切断时才启用完整图像补充检测。

完整的预取、背压、坐标模式和全局 NMS 说明见[异步帧流水线与滑动窗口](visual-async-and-sliding-window.md)。基础代码见[使用教程](usage-tutorial.md)和具体[视觉任务指南](visual-yolo-detection.md)。模型与后端状态见[模型支持指南](model-support.md)及[验证矩阵](../model-backend-verification-matrix.md)。
