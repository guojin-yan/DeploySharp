# ONNX Runtime 快速开始

DeploySharp.Backend.OnnxRuntime 只提供托管适配层，不内置 onnxruntime.dll、CUDA、DirectML 或其他 native Provider。应用需要显式安装一个匹配的官方运行时。

~~~powershell
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0-alpha.1
dotnet add package Microsoft.ML.OnnxRuntime --version 1.28.0
~~~

CUDA 场景将 CPU 包替换为 Microsoft.ML.OnnxRuntime.Gpu.Windows，并安装匹配的 CUDA、cuDNN 和 NVIDIA 驱动；代码中显式选择 CUDA，不会静默回退到 CPU。

~~~csharp
using var provider = new OnnxRuntimeBackendProvider(
    new OnnxRuntimeOptions(
        executionProvider: OnnxRuntimeExecutionProvider.Cuda,
        cudaDeviceId: 0));
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cuda");
~~~

## 命名张量推理

~~~csharp
using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var artifact = new ModelArtifact(
    new ModelId("examples/classifier"),
    "onnx",
    modelPath,
    preferredBackend: OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cpu");
using IInferenceSession session =
    backends.CreateSession(artifact, request,
        new SessionOptions(maxConcurrency: 1));
var tensor = new Tensor<float>(
    new TensorShape(1, 3, 224, 224), inputValues);
InferenceOutputs outputs = session.Run(
    InferenceInputs.Create("images", tensor),
    cancellationToken);
float[] scores = (float[])outputs.GetRequired("scores").Buffer;
~~~

输入/输出名称必须精确匹配，每个输入只能绑定一次。只有模型元数据声明为动态的维度才允许运行时尺寸。当前桥接验证 Boolean、整数、Float32 和 Float64；String、Float16 和 BFloat16 会报告稳定的不支持类型。

## Visual 与外部数据

Visual 需要额外安装 DeploySharp.Visual，并把图像适配器生成的 PreparedVisualInput 传入 Pipeline。ONNX external data 按图文件所在目录解析；ModelPack 应将图文件及所有 sidecar 一并列出，确保创建 session 前完成路径、大小和 SHA-256 校验。

CUDA 提供程序缺失或设备初始化失败会报告 DS-ORT-5008/DS-NATIVE-6001，不会偷偷切到 CPU。更多运行时选项、异步条件和错误码见[ONNX Runtime 兼容性](onnxruntime-compatibility.md)。
