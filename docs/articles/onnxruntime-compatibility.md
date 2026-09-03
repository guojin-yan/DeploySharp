# ONNX Runtime 兼容性

DeploySharp.Backend.OnnxRuntime 的托管适配器发布 netstandard2.0 与 net8.0 资产。原生运行时、RID、CUDA/cuDNN、DirectML 和驱动由应用选择与部署；本项目当前公开验证基线是 Windows x64。

## 运行时选择

| 运行时 | 适用场景 | 说明 |
| --- | --- | --- |
| Microsoft.ML.OnnxRuntime 1.28.0 | Windows/Linux CPU | 应用显式安装并与适配器配套 |
| Microsoft.ML.OnnxRuntime.Gpu.Windows 1.28.0 | Windows x64 CUDA | 还需匹配 CUDA、cuDNN 和 NVIDIA 驱动 |
| Microsoft.ML.OnnxRuntime.DirectML | Windows DirectML | 当前不作为 DeploySharp 默认后端声明 |

OnnxRuntimeOptions 可以设置图优化、算子内/算子间线程、顺序或并行图执行、内存模式、CPU arena、日志和 ExecutionProvider。默认 device 是 cpu；CUDA 必须同时设置 ExecutionProvider.Cuda、device=cuda 和可选 cudaDeviceId。

## 推理与并发语义

Run 使用同步 native 推理。RunAsync 只有在输出静态、调用 token 不可取消且未强制单线程时才会使用 ORT 异步；动态输出、可取消调用或单线程会话回退到同步 native 调用，不使用 Task.Run。

通过 BackendRegistry 创建 session 时，SessionOptions.MaxConcurrency=n 会从头创建 n 个独立 ORT session。调用从池中租用空闲 session；返回张量是独立托管数据，释放 session 后仍有效。需要 batch 的模型应使用真正的动态 batch 合同，而不是把 batch-one 调用简单拼接。

## 类型、动态 shape 与 external data

输入输出名称按序号精确匹配。Float32、Float64、Boolean 和整数类型按声明桥接；String、Float16、BFloat16 等未准入类型会稳定失败。动态维度只有在模型元数据明确为 -1 时才允许具体运行时尺寸。

External data 按 ONNX 图相对路径加载。ModelPack 应将图与全部 sidecar 一起列出并在创建 session 前校验文件路径、大小和 SHA-256；不要执行模型目录中的脚本。

## 错误码

模型格式、protobuf、算子或 opset 错误使用 DS-ORT-5002；输入名称/类型/形状使用 DS-ORT-5003；桥接类型使用 DS-ORT-5004；执行失败使用 DS-ORT-5005；取消使用 DS-ORT-5006；对象释放使用 DS-ORT-5007；Provider/设备失败使用 DS-ORT-5008；native DLL/ABI 失败使用 DS-NATIVE-6001。原始异常保留在 InnerException 和技术详情中。

具体模型与后端状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。
