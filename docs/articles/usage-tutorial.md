# Usage tutorial / 使用教程

This is the short, code-first path from a new .NET console project to a real DeploySharp tensor inference call. It deliberately shows a few lines of application code; repository-wide scripts belong to the [platform support guide](platform-support.md) and the complete workflows under <code>samples/</code>. / 本教程用代码展示从新建 .NET 控制台项目到真实 DeploySharp 张量推理的最短路径。这里只展示应用代码；仓库级脚本放在[平台支持说明](platform-support.md)和 <code>samples/</code> 完整示例中。

## 1. Create a project / 创建项目

~~~powershell
dotnet new console -n DeploySharpQuickstart
cd DeploySharpQuickstart
dotnet add package JYPPX.DeploySharp.Core --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
~~~

During the current source-first Alpha, use project references instead of package feeds when the package is not available in your configured source. The package IDs and responsibilities are listed in the root README. / 当前 Alpha 仍以源码为主；如果配置的源中尚未提供包，可改用项目引用。包 ID 和职责见仓库根目录 README。

## 2. Run a tensor / 运行一次张量推理

Place a compatible ONNX file at <code>models/classifier.onnx</code>, then use exact model input/output names. / 将兼容的 ONNX 文件放到 <code>models/classifier.onnx</code>，并使用模型的精确输入/输出名称。

~~~csharp
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
~~~

Input/output names and shapes are strict. The session owns native execution resources and is disposed by the caller. / 输入输出名称和形状采用严格校验；会话由调用方释放，并负责释放原生执行资源。

## 3. Add a visual workflow / 加入视觉流程

Visual does not decode images itself. Add <code>JYPPX.DeploySharp.Visual</code> and an image adapter such as <code>JYPPX.DeploySharp.Visual.OpenCV</code>; prepare the tensor and reversible image transform, register the model profile, then run a <code>VisualPipeline</code>. / Visual 本身不解码图片。添加 <code>JYPPX.DeploySharp.Visual</code> 和 <code>JYPPX.DeploySharp.Visual.OpenCV</code> 等图像适配器，准备张量和可逆图像变换，注册模型 Profile 后再运行 <code>VisualPipeline</code>。

~~~csharp
var sourceSize = new VisualSize(1920, 1080);
var modelSize = new VisualSize(224, 224);
var transform = ImageTransform.Letterbox(sourceSize, modelSize);

using var prepared = new PreparedVisualInput(
    "images", preparedTensor, sourceSize, modelSize, 1,
    VisualTensorLayout.Nchw, transform, preprocessingDescriptor);

VisualProfileSelection selection = profiles.Select(
    artifact, backends, request, VisualTaskId.ImageClassification);

using var pipeline = new VisualPipeline(
    backends, selection, request, SessionOptions.Default);
VisualInferenceResult result = await pipeline.RunAsync(
    prepared, VisualExecutionOptions.Default, CancellationToken.None);
~~~

See the [Visual quick start](visual-getting-started.md) for complete Profile and decoder setup.

## 4. Use a catalog model / 使用目录模型

For downloadable artifacts, use ModelFactory rather than hard-coding a mutable URL. ModelFactory resolves the catalog entry, verifies ModelPack size/SHA-256, caches the artifact, and returns a local identity for the selected backend. The runnable path is in [Model Release inference](model-release-inference-getting-started.md).

## 5. Choose the next guide / 下一步

- [Installation and package boundaries](installation.md)
- [ONNX Runtime quick start](onnxruntime-getting-started.md)
- [OpenVINO quick start](openvino-getting-started.md)
- [ModelFactory quick start](modelfactory-getting-started.md)
- [Model/backend verification matrix](../model-backend-verification-matrix.md)
