# 架构概览

DeploySharp 将稳定契约、领域流程、托管后端适配器和厂商原生运行时分层隔离。Core 不引用推理引擎或图像库，应用在启动时注册所需的 Provider，并负责模型文件和原生运行时的部署。

## 包和职责

| 层 | 主要职责 |
| --- | --- |
| `JYPPX.DeploySharp.Core` | 模型身份、类型化张量、结果、错误、日志、Session 和后端注册。 |
| `JYPPX.DeploySharp.Extensibility` | 插件描述、运行时依赖、选项 Schema、native 探测和结构化运行时状态。 |
| `JYPPX.DeploySharp.Visual` | 视觉 Profile、已准备张量、可逆坐标变换和任务解码器。 |
| `JYPPX.DeploySharp.Visual.OpenCV` | 使用 OpenCV 完成图像解码、缩放、裁剪和张量准备。 |
| `JYPPX.DeploySharp.Visual.TensorRT` | 可选的 CUDA 前处理、设备常驻视觉推理和紧凑结果回传。 |
| `JYPPX.DeploySharp.LLM` | 文本生成、流式输出和 Embedding 契约。 |
| `JYPPX.DeploySharp.Multimodal` | 有序媒体、请求校验、流式结果和生命周期编排。 |
| `JYPPX.DeploySharp.Backend.*` | ONNX Runtime、OpenVINO、OpenCV DNN、TensorRT 和 LLamaSharp 托管适配器。 |
| `JYPPX.DeploySharp.ModelPack.Json` / `ModelFactory` | 模型清单序列化、制品完整性、目录选择、下载和离线缓存；不进入 Core。 |
| 应用运行时包 | 由最终应用选择的 CUDA、TensorRT、OpenVINO、OpenCV 或 LLamaSharp 原生文件。 |

## Session、池和批处理

`BackendRegistry.CreateSession` 将 `SessionOptions.MaxConcurrency` 解释为独立 Session 池大小。池大小为 1 时请求排队；大于 1 时由池租用空闲 Session，每个 Session 从 Provider 重新创建单独的执行通道，避免依赖厂商运行时内部可能仍然串行的请求通道。

`InferenceBatchScheduler<TInput,TOutput>` 用于模型真正支持动态 batch 的场景：按模型上限切分输入，调度批次，校验解码数量并恢复输入顺序。固定 batch=1 的视觉模型使用 Session 池并发处理；有状态或自回归工作流则显式使用单 Session。`MaximumInFlightBatches` 限制准备中的批次数量，防止大任务一次性保留全部张量。

## Visual 数据流

图像适配器将像素转换为 `PreparedVisualInput`，其中包含张量、源图尺寸、模型尺寸和可逆 `ImageTransform`。Visual 根据 Profile 选择解码器，后端只接收命名张量，不接触 OpenCV `Mat` 或其他图像对象。解码器把检测、分割、姿态和 OCR 坐标还原到源图；应用不应再次缩放结果。

`VisualPipeline.RunManyAsync` 提供有序并发调用；`RunPrefetchedAsync` 在当前帧推理期间有界准备后续视频帧；`SlidingWindowDetectionRunner` 对大图进行重叠切片、映射回全图并执行一次全局 NMS。

## 后端边界

- ONNX Runtime：应用显式选择 CPU、CUDA 或 DirectML Execution Provider；CPU 是默认验证路径，CUDA 依赖本机驱动和运行时。
- OpenVINO：使用应用提供的 Windows runtime，XML/BIN 工件按 Profile 的输入输出合同加载。
- OpenCV DNN：仅声明已验证的数值张量和图像路径；动态 Shape、辅助输入和 importer 限制按具体工件报告，不把不兼容图强行导入 native 层。
- TensorRT：Engine 与 GPU、CUDA、TensorRT 版本和输入 profile 绑定。可选 `ITensorRtDeviceInferenceSession` 在同一 CUDA stream 上串联预处理、推理和紧凑后处理，普通 `IInferenceSession` 仍保留 host tensor 回退。
- LLamaSharp：模型和 CPU/CUDA 原生后端由应用选择，DeploySharp 不自动下载或安装。

## TensorRT 设备常驻路径

`RunDevice` 绑定调用方拥有的 `CudaMemory`，将工作入队到调用方的 `CudaStream`，并返回执行租约。调用方可以连续提交 CUDA 归一化、Letterbox、透视裁剪、TensorRT 推理和 CTC 紧凑解码，最后统一同步；普通 `Dispose()` 仍提供带同步的安全默认行为。

`TensorRtCudaOcrKernels` 支持融合归一化/Letterbox、四边形单应裁剪和 CTC argmax/blank collapse/置信度计算，仅回传紧凑 token、长度和置信度。JPEG/PNG 解码与 token 到字符串的转换仍在主机完成。非 TensorRT 后端继续使用现有 CPU 前处理和托管解码路径。
