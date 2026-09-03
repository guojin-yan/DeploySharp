# TensorRT CUDA 视觉流水线

本文介绍 `JYPPX.DeploySharp.Visual.TensorRT` 的设备侧视觉推理路径，适用于 Windows x64 上由应用自行部署 TensorRT、CUDA、cuDNN 和 OpenCV 的场景。页面适用于 `2.0.0-alpha.1`；标准 `VisualPipeline` 仍是其他后端和不满足设备侧合同时的兼容路径。

## 适用范围

设备侧流水线针对静态 `batch=1`、单个 `Float32 NCHW` 图像输入。它接收紧凑的 BGR `UInt8` 图像，在同一 CUDA stream 上完成 resize 或 letterbox、通道转换和归一化，然后直接交给 TensorRT。分类、检测、实例分割、姿态、OBB、异常检测和抠图 Profile 在输入输出元数据满足合同时可以使用该路径。

动态空间尺寸、多输入图、灰度或 RGBA 输出、`batch>1` 以及需要额外辅助输入的模型继续使用标准已准备 Tensor 路径。设备侧后处理只对已实现对应输出合同的 map 和 YOLO mask Profile 自动启用；其他 decoder 会安全回退到 CPU 后处理。

## 安装与所有权

~~~powershell
dotnet add package JYPPX.DeploySharp.Backend.TensorRT --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Visual.TensorRT --version 2.0.0-alpha.1
~~~

调用方负责 TensorRT/CUDA 原生 DLL、序列化 Engine、模型 Profile、OpenCV 运行时和设备选择。DLL 的 API 主版本、Engine 构建环境和当前进程必须匹配；DeploySharp 不会替应用下载、锁定或替换这些原生依赖。

## 最小调用

~~~csharp
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.TensorRT;

var backendOptions = new TensorRtBackendOptions(
    TensorRtApiVersion.TensorRt11,
    cudaTargetArchitecture: "compute_75");
using var image = new OpenCvBgrImageFactory()
    .CreateFromFile(@"images\input.jpg");
using var pipeline = new TensorRtVisualPipeline(
    visualProfile,
    @"models\model.onnx.engine",
    preprocessing,
    backendOptions);
VisualInferenceResult result = pipeline.Run(image);
~~~

`visualProfile`、`preprocessing` 和 Engine 必须描述同一个固定输入合同。`cudaTargetArchitecture` 使用部署 GPU 的 `compute_XX` 或 `sm_XX` 形式；不确定时应从目标设备和 NVRTC/TensorRT 安装说明中确认，而不是照搬其他机器的值。

`OpenCvBgrImage` 是不可变的紧凑 BGR 所有者。实时视频可以在解码线程中复用或转移字节数组，再把每帧交给 Pipeline；同一个 Pipeline 会复用 CUDA stream、设备缓冲、输出槽、事件和固定形状的 Kernel 启动参数。单个 Pipeline 会串行保护自己的 TensorRT context，需要多个同时执行的 CUDA 通道时，应创建数量受显存约束的独立 Pipeline 实例。

## 设备侧后处理

默认的 `TensorRtCudaVisualPostprocessingMode.WhenSupported` 会根据 Profile 自动选择：

- 单通道 map：在设备侧进行有限值校验、阈值化、双线性恢复，最后只回传结果平面；
- YOLO 实例分割：候选过滤和 prototype 组合留在 CUDA stream，CPU 只处理紧凑候选元数据的排序与 NMS，最终回传稠密 mask；
- PaDiM 等需要同时保留原始分辨率和源图分辨率的任务：若额外设备物化会增加内存或复制，则保持优化后的 CPU decoder；
- 未满足合同的输出：自动复制输出并调用 Profile 自带 decoder，结果语义不改变。

如果需要严格的 CPU 后处理对照，可传入 `TensorRtCudaVisualPostprocessingMode.Disabled`。`pipeline.UsesCudaPostprocessing` 可用于记录当前 Profile 是否真的进入设备侧后处理，不应仅根据后端名称推断。

## 输入生命周期与性能测量

设备侧路径适合“每帧都是新的 BGR 图像”的实时场景：上传、融合预处理、TensorRT 执行和必要的最终结果回传在同一 stream 上排序。若应用已经拥有同一个不可变的设备 Tensor，标准 TensorRT 路径可以缓存该输入，此时不应为了使用 CUDA 预处理而重复上传。

测量时应分别记录图像解码、主机到设备上传、CUDA 预处理、TensorRT 执行、设备侧后处理和最终回读；不要把 Engine 加载、NVRTC 首次编译或 CUDA 初始化混入稳态结果。可使用 `tools/DeploySharp.VisualBenchmark` 的 `steady` 模式，并在[设备性能实测](device-performance-benchmarks.md)中按设备单独记录结果。

## 故障排查

- Engine 加载失败：检查 TensorRT API 主版本、GPU 架构、CUDA/cuDNN DLL 和 Engine 构建环境是否一致。
- Profile 合同不匹配：确认输入名称、元素类型、固定 `[1,3,H,W]` 形状以及所有输出名称和形状逐项一致。
- NVRTC 编译失败：确认 `cudaTargetArchitecture`、CUDA Runtime 和 NVRTC DLL 可见；临时关闭设备侧路径并使用标准 TensorRT 后端可区分问题范围。
- 显存不足：减少独立 Pipeline 数量，关闭不需要的 RLE/稠密附加结果，并避免同时缓存多个大图输入。
- 结果需要更多后处理：保持 `WhenSupported`，让不兼容的 decoder 自动回退，不要在应用层对同一结果重复缩放或转换。

TensorRT 与 CUDA 的通用安装、Engine 绑定和 OCR 全流程说明见[TensorRT CUDA OCR](tensorrt-cuda-ocr.md)；每个模型/后端单元格的状态以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。
