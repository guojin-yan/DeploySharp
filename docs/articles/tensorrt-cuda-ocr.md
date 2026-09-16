# TensorRT CUDA OCR

本页说明 DeploySharp 的可选 TensorRT CUDA OCR 加速路径。它面向已具备 CUDA、TensorRT 与匹配 Engine 的 Windows x64 环境；ONNX Runtime、OpenVINO 与 OpenCV DNN 仍使用各自的主机内存流程，不受影响。

> [!IMPORTANT]
> TensorRT Engine 与 CUDA 架构、TensorRT 版本和模型输入形状相关。请在部署目标机生成或验证 Engine，不能把其他显卡生成的 Engine 当作通用文件分发。

## 适用范围

- 适用于已完成 Engine 构建的 PaddleOCR 检测、方向分类和识别阶段。
- 识别阶段可使用批处理和独立创建的多个推理会话；实际数量应以目标 GPU 的实测结果为准。
- 启用 CUDA 架构选项后，CTC 贪心解码可在 GPU 上完成，只回传紧凑的 token 与置信度，避免把完整 logits 复制回 CPU。
- 图像归一化、letterbox 以及透视裁剪提供 CUDA 基元。常规后端继续使用 CPU 回退，因此同一业务代码无需依赖 CUDA 专属类型。

当前 DB 文本检测的轮廓提取与文本框归并仍由通用 Visual 流程处理。不要把这条路径描述为“所有 OCR 步骤均在 GPU 完成”。

## 配置原则

1. 安装与当前 GPU、CUDA、TensorRT 对应的原生运行时，并确保 DLL 可被进程加载。
2. 为模型准备匹配的 TensorRT Engine；动态形状 Engine 必须为实际输入形状配置 profile。
3. 在 `TensorRtBackendOptions` 中填写部署 GPU 的 CUDA 目标架构，例如 `compute_75`；未配置时保持通用的 CPU 后处理回退。
4. 先使用单会话、较小识别 batch 验证结果一致性，再增加 batch 或会话数。多个会话必须独立创建；仅创建多个托管包装而复用同一阻塞上下文不会带来并发收益。

## 设备内存与 Stream 合同

`ITensorRtDeviceInferenceSession.RunDevice` 接收调用方拥有的 `CudaMemory` 与 `CudaStream`，并把执行入队到该 stream。调用方必须在最后一次 stream 同步前保持输入和输出显存有效。

- `TensorRtDeviceInferenceExecution.Dispose()` 是安全的默认同步边界。
- 对于连续的设备端图，可在依赖操作全部入队后使用 `ReleaseAfterEnqueue()`，并在整个图末尾同步一次。
- 动态 Engine 的设备张量形状必须与运行时解析出的具体形状一致；分配前先解析输出形状。

## CUDA 前后处理

`TensorRtCudaOcrKernels` 提供可组合的 CUDA 操作：

- BGR 图像 resize、letterbox 和 NCHW 归一化；
- 四边形透视裁剪与批量归一化；
- 融合的四边形裁剪，避免中间单应矩阵缓冲；
- CTC argmax、blank/repeat 折叠与平均置信度计算。

这些 API 仅负责设备侧计算和紧凑结果传输。token 到字典文本的映射以及现有 `RecognizedText` 结果对象仍在主机侧完成。

## 完整流水线的调度

PaddleOCR 的三个阶段不应按“每个文本框顺序跑一次”实现。推荐调度是：

1. det 使用一个 Session 对整图执行一次；
2. GPU 或 CPU 后处理得到文本四边形，并按识别宽高比/方向组织 crop；
3. cls 和 rec 按实际 Engine profile 组成 batch，最后一个不足 batch 的请求按合同补齐；
4. 当 crop 数量超过一个 batch 时，从独立创建的 Session 池租用空闲通道，并用有界队列限制在途 batch；
5. 按原始文本框索引合并方向和识别结果，再返回源图坐标。

同一 CUDA stream 可以减少阶段同步，但多个独立 Session 通常需要各自的 stream 和 workspace。最佳 batch 与通道数取决于 Engine profile、显存和图片中文字区域数，必须在目标设备用完整流水线扫描组合，不能只测 rec 单图。

## 数据传输和内存复用

性能路径应复用图像 staging buffer、crop batch buffer、Engine 输入输出和紧凑 token buffer。只有最终四边形、token 长度、token ID 和置信度需要回到 CPU；完整 det/CTC logits 回读仅用于诊断。缓冲区租约必须覆盖最后一次 stream 同步，不能在 kernel 入队后立即归还池。

JPEG/PNG 解码目前仍在主机侧，第一次 H2D 上传无法消除。若输入来自相机 CUDA buffer，可直接进入设备预处理接口，但需要调用方提供正确的 stride、格式和生命周期。

## 验证与性能

先运行 CUDA 探针，确认本机可以编译并加载 kernel：

```powershell
dotnet run --project tools/DeploySharp.TensorRtCudaOcrProbe -c Release -- --load
dotnet run --project tools/DeploySharp.TensorRtCudaOcrProbe -c Release -- --execute
```

设置 `DEPLOYSHARP_CUDA_ARCHITECTURE` 为目标 GPU 的兼容架构。若要验证 Engine 设备内存调用，还需配置 `DEPLOYSHARP_TENSORRT_ENGINE`、`JYPPX_NATIVE_BRIDGE_PATH` 以及 CUDA/TensorRT DLL 搜索路径。

性能只应在相同设备、模型、输入图、batch、会话数和预热策略下比较。请查看[设备性能实测](device-performance-benchmarks.md)了解已记录设备的完整 OCR 流水线结果与最佳组合；具体模型/后端可用性以[模型后端验证矩阵](../model-backend-verification-matrix.md)为准。

验证不仅看耗时，还要检查文本框数量、阅读顺序、方向分类、token 序列和置信度与 CPU reference 一致。建议允许明确的浮点容差，但文本内容、框映射和空白折叠规则应完全一致。报告同时给出 det、crop、cls、rec、merge、total、区域数和 P50/P95，避免用一个异常少文本的输入得到虚假的最优时间。
