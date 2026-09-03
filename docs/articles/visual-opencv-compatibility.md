# OpenCV DNN 兼容性

DeploySharp.Backend.OpenCV 通过 OpenCV DNN 加载调用方持有的 ONNX 工件；DeploySharp.Visual.OpenCV 只负责图像解码和预处理，两个包可以独立替换。当前公开验证基线是 Windows x64 CPU，应用需要显式安装匹配的 OpenCV 托管包装器和 native runtime。

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

## 已知限制

OpenCV DNN 5.0 对完整动态 Transformer 图的 importer 仍有限制，DEIMv2、RF-DETR 以及部分 PaddleOCR 图可能在导入阶段报告 unsupported。该结果是安全边界，不是把失败转换为 native 崩溃；ONNX Runtime、OpenVINO 和 TensorRT 路径不受此保护逻辑影响。

如果模型在 OpenCV 上失败，请先用同一输入和 Profile 在 ONNX Runtime 验证模型本身，再检查动态维度、辅助输入、输出布局和 importer 支持情况。具体模型状态以[模型后端验证矩阵](../model-backend-verification-matrix.md)为准。

## 诊断与性能

OpenCvRuntimePreflight.Check 会在解码前验证托管/native ABI；DLL 缺失、位数不匹配或入口点错误映射为 DS-OPENCV-5204。稳态性能测试应复用已准备输入和专门化网络，并将图像解码、预处理、DNN 推理和 decoder 分开计时。设备结果见[设备性能实测](device-performance-benchmarks.md)。
