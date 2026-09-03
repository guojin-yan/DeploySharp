# 实例分割

DeploySharp.Visual 提供 Direct mask 和 Prototype/系数两种后端无关的实例分割 decoder。输入是 PreparedVisualInput，图像适配器和推理后端按需安装。

## 输出合同

| 类型 | 必需输出 | 支持布局 | 重建方式 |
| --- | --- | --- | --- |
| Direct | boxes、scores、classes、masks | mask 为 [B,N,H,W] 或 [B,N,H,W,1] | 每个候选独立还原 |
| Prototype | boxes、scores、classes、prototypes、coefficients | prototype 为 [C,H,W] 或带 batch 的等价布局 | 对保留候选执行系数与 prototype 加权 |

每个输出名称、rank、元素类型和数值语义必须显式声明。分数、类别和 mask 中的非有限值、错误形状或越界值会被拒绝；通用 decoder 不会根据文件名猜测 YOLO 或 Mask R-CNN 布局，也不会隐式添加 sigmoid。

## 创建 decoder

~~~csharp
var candidates = new InstanceSegmentationCandidateSchema(
    "boxes", "scores", "classes",
    DetectionBoxFormat.Xyxy,
    normalizedBoxes: false,
    InstanceScoreKind.Probability);
var schema = new DirectInstanceSegmentationOutputSchema(
    candidates, "masks",
    InstanceMaskTensorLayout.Nchw,
    InstanceMaskValueKind.Probabilities,
    activation: InstanceMaskActivation.None,
    interpolation: InstanceMaskInterpolationMode.BilinearHalfPixel,
    thresholdOrder: InstanceMaskThresholdOrder.AfterResize,
    cropSpace: InstanceMaskCropSpace.ModelInput,
    cropOrder: InstanceMaskCropOrder.AfterResize);
var decoder = new DirectInstanceSegmentationDecoder(
    schema,
    new InstanceSegmentationDecoderOptions(
        scoreThreshold: 0.25f, maskThreshold: 0.5f,
        iouThreshold: 0.45f,
        nmsMode: DetectionNmsMode.ClassAware,
        maximumCandidates: 1000,
        maximumInstances: 100));
~~~

将 decoder 绑定到任务为 VisualTaskId.InstanceSegmentation 的 VisualModelProfile。Prototype 模型改用 PrototypeInstanceSegmentationOutputSchema 和 PrototypeInstanceSegmentationDecoder；分数筛选与框 NMS 会在 prototype 组合前执行，避免为被抑制候选分配完整掩码。

## 真正 Batch 与并发

模型明确声明动态 batch 时，可将输入和输出改为有界 [B,...]，并通过 PreparedVisualInput.BatchFrames 为每行附加源图变换。batch 大于 1 时 decoder 返回 InstanceSegmentationBatchResult；batch 为 1 保留兼容结果类型。

如果导出图只支持 batch-one，请使用 RunManyAsync 或独立 session 池。多个 session 必须从头创建各自的 native 上下文；不能只复制托管包装器。模型 batch、会话数和在途张量数量应按目标设备显存和吞吐调节。

## 几何与结果

结果 mask 默认恢复到完整源图空间，所有 Resize、Letterbox、Crop 和动态尺寸都通过同一个 ImageTransform。Independent 模式保留重叠掩码；ScorePriorityOwnership 可额外生成按分数分配的 ownership map。可选 RLE 是 DeploySharp 行优先游程，不是 COCO 压缩 RLE。

返回的 boxes、实例、稠密 mask 和 RLE 都是调用方拥有的托管数据，不依赖后端 tensor、native request 或 OpenCV Mat。解码器在候选、NMS、prototype、源图行和结果字节分配处检查取消及资源上限。

## 后端与性能

同一 Profile 可用于元数据匹配的 ONNX Runtime 或 OpenVINO；TensorRT 视觉路径只准入匹配的静态 Engine。性能测试应分开记录预处理、后端推理、掩码重建和端到端时间，并复用已准备输入。具体模型状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)，设备实测见[设备性能实测](device-performance-benchmarks.md)。
