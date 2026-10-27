# 通用检测器与 RT-DETR

DeploySharp 为 DEIMv2、RF-DETR、Paddle RT-DETR、RT-DETRv2 和 PP-YOLOE 提供显式的检测 Profile 与解码器。不同导出图的输入、辅助张量和输出布局并不相同，不能把一个模型的配置复制给另一个模型。

## 合同要点

| 模型族 | 常见输入 | 输出/后处理 |
| --- | --- | --- |
| DEIMv2 | `images`、必要时 `orig_target_sizes` | 已解码 boxes、scores、labels。 |
| Paddle RT-DETR | `images`、`im_shape`、`scale_factor` | 已解码向量或 raw query，按实际导出图选择 Decoder。 |
| RF-DETR | `images` 及查询/尺寸辅助输入 | 查询、类别和可选 mask，需绑定确切端口。 |
| PP-YOLOE | `image` 和尺度辅助输入 | 检测行或 Paddle 已解码输出。 |

预处理产生的尺寸、比例和原图坐标只能来自同一个 `PreparedVisualInput` 合同。解码器负责阈值、坐标还原和 NMS，并拒绝缺失或类型不匹配的辅助输入。

## 建立新 Profile 的步骤

接入一个新的便携检测器时，建议按以下顺序记录合同：

1. 用 Netron 或后端元数据读取工具确认全部输入输出名称、类型、rank 和动态维度；
2. 记录原始导出使用的颜色顺序、归一化、resize/letterbox、stride 对齐和 padding 值；
3. 先选择最接近的输出 schema，再为 query、尺寸、尺度因子等辅助输入创建显式绑定；
4. 用一张固定图片保存 raw output，独立验证坐标还原和 NMS；
5. 将模型 SHA-256、标签文件和阈值写进 ModelPack 或应用配置。

`PortableDetectorProfiles` 只提供已知导出合同的便捷构造器。若新模型的输出布局不同，应创建新的 Profile/decoder，而不是修改现有 Profile 让它“兼容”多个不相容布局。

## 运行示例

```csharp
var profile = PortableDetectorProfiles.CreateDeimV2(
    new ModelId("models/deimv2"),
    modelSha256,
    inputSize: new VisualSize(640, 640));

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var profiles = new VisualProfileRegistry();
profiles.Register(profile.VisualProfile);
profiles.Freeze();

var artifact = profile.CreateArtifact(
    modelPath, OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cpu");
using var pipeline = new VisualPipeline(
    backends, profiles.Select(artifact, backends, request,
        VisualTaskId.ObjectDetection), request);
using var input = imageAdapter.Prepare(imagePath);
DetectionResult result = pipeline.Run(input)
    .GetValue<DetectionResult>();
```

## 后端选择

ONNX Runtime 是最通用的路径；OpenVINO 需要匹配的 XML/BIN 或兼容 ONNX；TensorRT Engine 与 GPU、CUDA、TensorRT 版本及静态输入 profile 绑定。OpenCV DNN 对动态 Transformer shape、辅助输入和图内后处理存在工件级限制，遇到不兼容图时会返回托管诊断而不是强行进入 native 层。

批量推理时，只有导出图明确支持动态 batch 才使用 `InferenceBatchScheduler`；否则通过多个独立 Session 的 `RunManyAsync` 并发处理。

具体可用模型、后端结果和已知限制见[模型支持指南](model-support.md)、[OpenCV 兼容性](visual-opencv-compatibility.md)和[模型 × 后端验证矩阵](../model-backend-verification-matrix.md)。

## 误检、漏检和速度定位

先固定同一张图片、同一张量和同一 raw output，再调阈值。若 raw output 已经不同，问题在后端、精度或输入合同；若 raw output 相同但结果不同，问题在 decoder、NMS 或坐标还原。性能报告至少分开列出图像解码、预处理、模型执行、候选筛选/NMS 和结果序列化。

检测大图时可以组合[滑动窗口检测](visual-async-and-sliding-window.md)，但每个窗口都必须携带自己的 transform，最终在源图坐标上执行一次全局 NMS。窗口重叠越大召回率通常越高，但模型调用和全局候选数量也会上升，应使用固定测试集选择重叠比例。
