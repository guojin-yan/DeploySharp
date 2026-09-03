# 语义分割

DeploySharp.Visual 的语义分割 decoder 接收 PreparedVisualInput，并返回自有的类别索引 mask。它不绑定图像库或推理后端。

## 输出类型

| 类型 | 支持元素类型 | 布局 | 解码规则 |
| --- | --- | --- | --- |
| Logits | Float32、Float64 | NCHW、NHWC、CHW、HWC | 多类 argmax；不会隐式激活 |
| Probabilities | Float32、Float64 | NCHW、NHWC、CHW、HWC | 值必须在 [0,1]，按显式阈值解码 |
| LabelMap | Int8/UInt8 至 Int64/UInt64 | NCHW、NHWC、CHW、HWC、NHW、HW | 单通道非负类别索引 |

~~~csharp
var schema = new SegmentationOutputSchema(
    "logits",
    SegmentationOutputKind.Logits,
    SegmentationTensorLayout.Nchw,
    classCount: 3,
    backgroundClassIndex: 0);
var decoder = new SemanticSegmentationDecoder(
    schema,
    new SegmentationDecoderOptions(
        outputSizeMode: SegmentationOutputSizeMode.Source,
        minimumRegionPixels: 1,
        generateRle: true,
        maximumOutputBytes: 256L * 1024 * 1024));
~~~

将 decoder 绑定到 VisualTaskId.SemanticSegmentation 的 VisualModelProfile，再注册 ONNX Runtime 或 OpenVINO 后端执行。需要编码图片时，使用 Visual.OpenCV 创建 PreparedVisualInput。

## 真正 Batch

输入和输出同时声明有界动态 batch（例如 [B,3,512,512]）并附带每行 VisualInputFrame 后，decoder 才会返回 SemanticSegmentationBatchResult。batch-one 仍返回兼容的 SemanticSegmentationResult。若模型是 batch-one，请使用 RunManyAsync 或独立 session 池，不要把多个样本拼进未经声明的张量。

## 几何、RLE 与限制

Source 输出模式按 ImageTransform 将 mask 还原到源图，letterbox padding 不会成为内容；Model/Tensor 模式保留各自分辨率。CreateBinaryMask 可按类别派生 byte mask，DeploySharp RLE 是行优先游程格式，不是 COCO 压缩 RLE。

解码器在 tensor 复制、mask、RLE、概率图和区域过滤分配前检查 MaximumOutputBytes。无效 rank、layout、类型、类别或非有限值使用稳定 Visual 错误码。性能测试应分开记录预处理、后端推理、mask 还原和 RLE/区域后处理。

具体模型与后端状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)，设备实测见[设备性能实测](device-performance-benchmarks.md)。
