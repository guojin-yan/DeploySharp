# OpenCV 图像输入与 DNN 后端

DeploySharp.Backend.OpenCV 通过 OpenCV DNN 加载调用方持有的 ONNX 工件；DeploySharp.Visual.OpenCV 只负责图像解码和预处理，两个包可以独立替换。当前公开验证基线是 Windows x64 CPU，应用需要显式安装匹配的 OpenCV 托管包装器和 native runtime。

## 安装

图像输入适配器需要安装 Visual、OpenCV 适配器和匹配的 native runtime：

```powershell
dotnet add package JYPPX.DeploySharp.Visual --version 2.0.0-alpha.1
dotnet add package JYPPX.DeploySharp.Visual.OpenCV --version 2.0.0-alpha.1
dotnet add package JYPPX.OpenCV.CSharp.API --version 5.0.0-preview.1
dotnet add package JYPPX.OpenCV.runtime.win-x64 --version 5.0.0-preview.1
```

需要 OpenCV DNN 推理时再安装 `JYPPX.DeploySharp.Backend.OpenCV`。DeploySharp 包不隐式携带 native DLL；最终应用必须选择与 RID、进程架构和托管包装器匹配的 runtime。

## 准备图像输入

```csharp
var options = new OpenCvPreprocessOptions(
    new VisualSize(224, 224),
    resizeMode: OpenCvResizeMode.Letterbox,
    colorOrder: VisualColorOrder.Rgb,
    means: new[] { 123.675f, 116.28f, 103.53f },
    standardDeviations: new[] { 58.395f, 57.12f, 57.375f },
    layout: VisualTensorLayout.Nchw);

using PreparedVisualInput input =
    new OpenCvVisualInputFactory().CreateFromFile(
        Path.GetFullPath("image.jpg"), "images", options,
        cancellationToken);
```

输入工厂保存源图尺寸和可逆 `ImageTransform`。如果模型已经提供 `VisualModelProfile.Preprocessing`，优先从 Profile 创建输入，保证 `Resize`、`Letterbox`、`CenterCrop`、插值、填充、颜色顺序、alpha、layout 和归一化使用同一份合同：

```csharp
using PreparedVisualInput input =
    new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile);
```

模型官方 Profile 是默认正确合同。覆盖几何或归一化参数后必须重新验证数值和坐标；PaddleOCR、SAM 和生成式视觉语言模型应使用各自专用 Factory，只修改专用 API 明确暴露的选项。

## 当前合同

| 项目 | 当前值 |
| --- | --- |
| 托管包装器 | JYPPX.OpenCV.CSharp.API 5.0.0-preview.1 |
| Windows native 包 | JYPPX.OpenCV.runtime.win-x64 5.0.0-preview.1 |
| 输入 | NCHW Float32 图像；batch 和空间维度可固定或在运行时解析 |
| 辅助输入 | 标量、向量、矩阵数值输入；Int64 会安全收窄到 OpenCV CV_32S |
| 输出 | Float32、Boolean、Int8、UInt8、Int32、Int64 数值张量 |
| 不在范围 | 隐式前处理、GPU/NPU 目标、Linux/macOS native、通用 ONNX 算子保证 |

输入/输出名称、数据类型和 shape pattern 必须写在 Visual Profile 中，不能根据第一个输出或文件名猜测。OpenCV Mat 和 native 资源由适配器负责释放，返回的 PreparedVisualInput 不会让指针或 span 逃逸。

## 动态 shape 与辅助输入

当 ONNX 图包含符号输入维度时，Provider 会针对具体运行时 shape 在内存中专门化私有图，并在 shape 不变期间复用网络；原始文件和工件身份不会被改写。动态输出最多允许一个受保护的 wildcard 维度，并根据返回元素数解析。

常量输入形式的 Slice、Unsqueeze、Squeeze、Split、Reduce、TopK 等会在能够证明安全时规范化为 OpenCV 5 支持的形式；无法证明的节点保持原样。图内数据依赖的后处理尾部不能被 OpenCV 正确表达时，应绑定已验证的原始输出并使用 DeploySharp 托管 decoder，而不是自动猜测输出。

固定 batch 的 OCR profile 会为最后一个不足批次补齐输入。多输入图必须按名称绑定每个辅助张量；超出 Int32 范围或 shape/rank 不匹配时应直接失败。

## 导入失败的最小复现

遇到 importer 错误时，先把问题缩小到 OpenCV DNN，而不是直接从完整流水线猜测：

1. 使用与 DeploySharp 相同版本的 OpenCV runtime，单独加载 ONNX 图。
2. 打印所有输入名、shape 和辅助输入；将一个固定 shape 的 `Mat` 绑定到每个端口。
3. 只调用一次 `forward`，保存 importer 的原始诊断和模型 SHA-256。
4. 在 ONNX Runtime 运行同一个输入，比较是否是模型图本身的问题。

如果导入器只在某个空间尺寸失败，优先导出固定输入图或在应用侧固定到一个已验证 shape；如果失败发生在 `ReduceMean`、动态 `Slice`、`Shape/Gather` 等图形元算子，通常应更换后端，而不是修改 decoder 阈值。

## 已知限制

OpenCV DNN 5.0 对完整动态 Transformer 图的 importer 仍有限制，DEIMv2、RF-DETR 以及部分 PaddleOCR 图可能在导入阶段报告 unsupported。该结果是安全边界，不是把失败转换为 native 崩溃；ONNX Runtime、OpenVINO 和 TensorRT 路径不受此保护逻辑影响。

如果模型在 OpenCV 上失败，请先用同一输入和 Profile 在 ONNX Runtime 验证模型本身，再检查动态维度、辅助输入、输出布局和 importer 支持情况。具体模型状态以[模型后端验证矩阵](../model-backend-verification-matrix.md)为准。

## 诊断与性能

OpenCvRuntimePreflight.Check 会在解码前验证托管/native ABI；DLL 缺失、位数不匹配或入口点错误映射为 DS-OPENCV-5204。稳态性能测试应复用已准备输入和专门化网络，并将图像解码、预处理、DNN 推理和 decoder 分开计时。设备结果见[设备性能实测](device-performance-benchmarks.md)。

OpenCV DNN 的“稳态”至少包含两种口径：第一次 `Net` 导入后的冷启动，以及已缓存网络、已复用 blob 的 steady state。对比其他后端时必须保持输入尺寸、线程数、warmup、输出复制方式一致；只测 `forward` 而把 blob 创建算进其他后端，会得到误导性的结论。当前矩阵中标为 unsupported 的模型不应进入平均值。

## ROI 输入边界

`DeploySharp.Visual.OpenCV` 的 ROI 输入合同已经覆盖以下不依赖具体模型的情况：源图只解码一次、矩形 `SubMat` 的非连续行步长、逐行 Batch 的独立 `ImageTransform`、Gray 单通道、BGR、BGRA/RGBA 通道、Mask 清零、旋转矩形/四边形透视以及释放后拒绝继续使用。对应合同测试位于 `tests/DeploySharp.Visual.OpenCV.Tests/OpenCvInputTests.cs`，用于验证内存布局、通道数、张量 shape 和源图投影，不代表某个官方模型在 OpenCV DNN 上已经成功导入。

这些合同测试证明内存布局和坐标映射正确，不代表任意官方模型都能被 OpenCV DNN 导入。具体模型状态以[验证矩阵](../model-backend-verification-matrix.md)为准；提交真实模型证据时至少记录模型与输入 SHA-256、OpenCV managed/native 版本、动态 shape/辅助输入、冷启动与稳态、各阶段 P50/P95 和内存分配。

## 生命周期

`CreateFromFile` 只接受绝对普通文件；`CreateFromStream`/`CreateFromBytes` 在边界内读取编码数据。`PreparedVisualInput` 默认由调用方持有并负责释放。取消会在解码、几何变换和有界行处理之间观察；正在执行的单次 native 调用返回后才会响应取消。
