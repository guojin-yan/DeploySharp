# ONNX Runtime 快速开始

DeploySharp.Backend.OnnxRuntime 只提供托管适配层，不内置 onnxruntime.dll、CUDA、DirectML 或其他 native Provider。应用需要显式安装一个匹配的官方运行时。

~~~powershell
dotnet add package JYPPX.DeploySharp.Backend.OnnxRuntime --version 2.0.0
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

CUDA 提供程序缺失或设备初始化失败会报告 `DS-ORT-5008`/`DS-NATIVE-6001`，不会偷偷切到 CPU。

## 运行时与兼容性

`DeploySharp.Backend.OnnxRuntime` 发布 `netstandard2.0` 与 `net8.0` 资产。原生运行时、RID、CUDA/cuDNN 和驱动由最终应用选择；当前公开验证基线是 Windows x64。

| 运行时 | 适用场景 | 说明 |
| --- | --- | --- |
| `Microsoft.ML.OnnxRuntime 1.28.0` | CPU | Windows/Linux 应用显式安装 |
| `Microsoft.ML.OnnxRuntime.Gpu.Windows 1.28.0` | Windows x64 CUDA | 还需匹配驱动、CUDA 与 cuDNN |
| DirectML | Windows GPU | 不是当前默认发布验证后端 |

`OnnxRuntimeOptions` 可设置图优化、线程、执行模式、内存模式、日志和 Provider 选项。版本相关的 `cudaProviderOptions` 只应使用当前 ORT 明确支持的键；未知选项应保留原始错误，不能被当作 CUDA 已通过的证据。

一个可复现的 CPU 配置如下：

```csharp
backends.UseOnnxRuntime(new OnnxRuntimeOptions(
    intraOpThreads: Environment.ProcessorCount,
    interOpThreads: 1,
    graphOptimization: OnnxRuntimeGraphOptimization.Extended,
    executionMode: OnnxRuntimeExecutionMode.Sequential,
    enableMemoryPattern: true,
    enableCpuMemoryArena: true));
```

## CPU、CUDA 和 fallback 的选择

建议把后端选择做成启动配置，而不是在每个请求中动态创建 Provider：

```csharp
BackendRegistry backends = new();
if (useCuda)
{
    backends.UseOnnxRuntime(new OnnxRuntimeOptions(
        executionProvider: OnnxRuntimeExecutionProvider.Cuda,
        cudaDeviceId: deviceId));
}
else
{
    backends.UseOnnxRuntime();
}
```

如果业务允许 fallback，应在启动阶段显式探测 CUDA 并记录最终选择；不要在同一请求中先执行半次 CUDA、失败后又把部分输出交给 CPU。需要严格可比性能时禁止 fallback，任何 Provider 错误直接作为测试失败。

## 异步和 Session 池

`RunAsync` 是后端提供的异步 API，不等于无限并发。对视频流，使用有界 `RunPrefetchedAsync` 或 `RunManyAsync`，让预处理与当前推理重叠；对多个独立流，设置 `SessionOptions.MaxConcurrency` 创建真实独立 Session。每个 Session 都会占用 native 内存，最佳数量应以 P95、吞吐、CPU/GPU 利用率和峰值内存共同决定。

## 模型输入自检

第一次加载建议打印：输入输出名称、元素类型、每个维度的固定/动态状态、external data 文件列表和 Profile SHA-256。若模型有多个输入，使用命名 `InferenceInputs` 完整绑定；不要依赖 ONNX 文件中端口顺序。遇到 `DS-ORT-5003` 时先修正合同，遇到 `DS-ORT-5005` 才进一步检查执行器或模型算子。

动态维度只表示模型允许该维度变化，不保证任意 batch 都高效。先读取模型元数据，再限制实际 batch；固定形状循环应复用张量缓冲区，避免每帧通过 LINQ 或 `ToArray()` 重复复制。Batch-one 图需要并发时使用有限的独立 Session，不要把同一个请求同时交给 Session 池和 batch 调度器。

## 故障定位

| 错误 | 含义与优先检查 |
| --- | --- |
| `DS-ORT-5002` | 模型格式、protobuf、opset 或算子 |
| `DS-ORT-5003` | 输入名称、类型、rank 或具体 shape |
| `DS-ORT-5004` | 尚未准入的张量类型 |
| `DS-ORT-5005` | 模型执行失败；查看原始 `InnerException` |
| `DS-ORT-5006` | 调用被取消 |
| `DS-ORT-5007` | Session 已释放 |
| `DS-ORT-5008` | Provider 或设备初始化失败 |
| `DS-NATIVE-6001` | native DLL、架构或 ABI 不匹配 |

性能测试应区分加载、输入准备、Session 执行、decoder 和结果复制，至少报告 P50/P95、分配量和峰值内存。统一方法见[性能基准方法](performance-benchmarking.md)，具体模型状态见[模型与后端验证矩阵](../model-backend-verification-matrix.md)。
