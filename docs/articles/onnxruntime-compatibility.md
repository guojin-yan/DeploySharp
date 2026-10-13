# ONNX Runtime 兼容性

DeploySharp.Backend.OnnxRuntime 的托管适配器发布 netstandard2.0 与 net8.0 资产。原生运行时、RID、CUDA/cuDNN、DirectML 和驱动由应用选择与部署；本项目当前公开验证基线是 Windows x64。

## 运行时选择

| 运行时 | 适用场景 | 说明 |
| --- | --- | --- |
| Microsoft.ML.OnnxRuntime 1.28.0 | Windows/Linux CPU | 应用显式安装并与适配器配套 |
| Microsoft.ML.OnnxRuntime.Gpu.Windows 1.28.0 | Windows x64 CUDA | 还需匹配 CUDA、cuDNN 和 NVIDIA 驱动 |
| Microsoft.ML.OnnxRuntime.DirectML | Windows DirectML | 当前不作为 DeploySharp 默认后端声明 |

OnnxRuntimeOptions 可以设置图优化、算子内/算子间线程、顺序或并行图执行、内存模式、CPU arena、日志和 ExecutionProvider。默认 device 是 cpu；CUDA 必须同时设置 ExecutionProvider.Cuda、device=cuda 和可选 cudaDeviceId。

对于 CUDA provider 的生命周期诊断，`cudaProviderOptions` 和 `sessionConfigEntries` 提供了显式的键值扩展入口；DeploySharp 会复制并冻结这些字典，再在创建 Session 时交给 ORT。它们不改变默认值，也不会自动猜测版本相关选项。若 ORT 返回 unknown provider option，应保留该运行时的原始错误，不把这类实验结果写成 CUDA 支持证据。固定输出图还可以显式打开 `preferPreallocatedCpuOutputs`，让 ORT 写入调用方预分配的 CPU 输出；这是兼容性诊断开关，不适用于动态输出，也不等同于设备端零拷贝。

公式自回归图的当前实测显示：关闭 CUDA Graph、切换默认 stream、启发式 cuDNN 搜索，以及静态输出 CPU 预分配，均未消除部分 FormulaNet-L/UniMERNet 在同一进程多样本运行中的非法 `Int64` token。该问题仍按“进程隔离有界执行”记录，不能通过这些选项宣称普通长生命周期 Session 已支持。实验性选项示例：

FormulaNet-L 的更小生命周期探针进一步只创建一个 CPU Session 和一个 CUDA Session，并在同一进程连续执行三个样本。第一行 raw 输出正常；第二、第三行均出现 `[1,1025]`、1024 个越界 token 且没有 EOS，Decoder 以结构化 `DecodeFailed` 拒绝。该结果说明“每个样本重建 Session”也不是充分修复；当前仍需固定状态 encoder/decoder、隔离 worker 或 ORT/provider 原生修复。详见 [FormulaNet-L 单 Session 报告](../../eng/models/paddle-document/verification/formula-formulanet-l-cuda-lifecycle-20261014.md)。

```csharp
var options = new OnnxRuntimeOptions(
    executionProvider: OnnxRuntimeExecutionProvider.Cuda,
    cudaProviderOptions: new Dictionary<string, string>
    {
        ["enable_cuda_graph"] = "0",
        ["do_copy_in_default_stream"] = "1"
    },
    preferPreallocatedCpuOutputs: true);
```

这些选项需要引用 `System.Collections.Generic`。只有在目标 ORT 包明确支持相应键名时才能使用；当前公式 CUDA 边界和复现报告见 [公式 ORT CUDA 状态](../../eng/models/paddle-document/verification/formula-realformula-ort-cuda-20261014.md)。

一个可复现的 CPU 配置示例：

```csharp
var options = new OnnxRuntimeOptions(
    intraOpThreads: Environment.ProcessorCount,
    interOpThreads: 1,
    graphOptimization: OnnxRuntimeGraphOptimization.Extended,
    executionMode: OnnxRuntimeExecutionMode.Sequential,
    enableMemoryPattern: true,
    enableCpuMemoryArena: true);
backends.UseOnnxRuntime(options);
```

CUDA 配置必须显式选择 Provider，不能通过 `device: "cuda"` 猜测：

```csharp
backends.UseOnnxRuntime(new OnnxRuntimeOptions(
    executionProvider: OnnxRuntimeExecutionProvider.Cuda,
    cudaDeviceId: 0,
    graphOptimization: OnnxRuntimeGraphOptimization.All));
```

如果 CUDA 初始化失败，先保留同一工件切回 CPU，记录 `DS-ORT-5008` 的技术详情；不要在捕获异常后把 CPU 结果标成 CUDA 结果。

## 推理与并发语义

Run 使用同步 native 推理。RunAsync 只有在输出静态、调用 token 不可取消且未强制单线程时才会使用 ORT 异步；动态输出、可取消调用或单线程会话回退到同步 native 调用，不使用 Task.Run。

通过 BackendRegistry 创建 session 时，SessionOptions.MaxConcurrency=n 会从头创建 n 个独立 ORT session。调用从池中租用空闲 session；返回张量是独立托管数据，释放 session 后仍有效。需要 batch 的模型应使用真正的动态 batch 合同，而不是把 batch-one 调用简单拼接。

## 类型、动态 shape 与 external data

输入输出名称按序号精确匹配。Float32、Float64、Boolean 和整数类型按声明桥接；String、Float16、BFloat16 等未准入类型会稳定失败。动态维度只有在模型元数据明确为 -1 时才允许具体运行时尺寸。

External data 按 ONNX 图相对路径加载。ModelPack 应将图与全部 sidecar 一起列出并在创建 session 前校验文件路径、大小和 SHA-256；不要执行模型目录中的脚本。

## 动态 batch 与输入复用

动态维度只代表图允许该维度变化，不代表所有后端都能高效处理任意 batch。应用应先读取模型元数据，再限制 `batch <= profile.MaximumBatch`，对每个实际 batch 记录首次专门化耗时。稳定尺寸的循环推理应复用 `Tensor<T>` 的 backing buffer；不要在每帧用 LINQ 或 `ToArray()` 复制一遍像素。

对于 batch-one 图，`SessionOptions.MaxConcurrency` 创建的是多个真正独立的 native Session。它适合多个视频流或图片队列；单个流不要盲目把并发数开到 CPU 核数，需以 P95、吞吐和内存峰值一起选择。真正动态 batch 则使用 `InferenceBatchScheduler`，不要同时把同一请求放进 Session 池和 batch 调度器。

## 诊断和基准方法

建议将一次测试拆成四个阶段：模型加载（cold start）、输入准备、`Run`/`RunAsync`、decoder 和结果复制。至少丢弃 2 次 warmup，再运行 30 次以上；报告平均值、P50、P95、分配字节和峰值内存。CUDA 还应记录驱动、CUDA/cuDNN、GPU 型号和设备编号。详见[性能基准方法](performance-benchmarking.md)。

## 错误码

模型格式、protobuf、算子或 opset 错误使用 DS-ORT-5002；输入名称/类型/形状使用 DS-ORT-5003；桥接类型使用 DS-ORT-5004；执行失败使用 DS-ORT-5005；取消使用 DS-ORT-5006；对象释放使用 DS-ORT-5007；Provider/设备失败使用 DS-ORT-5008；native DLL/ABI 失败使用 DS-NATIVE-6001。原始异常保留在 InnerException 和技术详情中。

具体模型与后端状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。
