# 姿态估计

DeploySharp.Visual 支持 Direct keypoint、Heatmap 和符合 Profile 合同的 YOLO-Pose 输出。关键点数量、组件语义、坐标空间、可见性和拓扑必须显式声明。

## Direct decoder

~~~csharp
var topology = new PoseTopology(
    new[]
    {
        new PoseKeypointDefinition(0, "left", mirrorIndex: 1, oksSigma: 0.1f),
        new PoseKeypointDefinition(1, "right", mirrorIndex: 0, oksSigma: 0.1f),
        new PoseKeypointDefinition(2, "center", oksSigma: 0.1f)
    },
    new[] { new PoseSkeletonEdge(0, 2), new PoseSkeletonEdge(1, 2) });
var schema = new DirectPoseOutputSchema(
    "keypoints",
    keypointCount: 3,
    componentCount: 4,
    visibilityComponentIndex: 3,
    boxesOutputName: "boxes",
    instanceScoresOutputName: "scores",
    coordinateSpace: PoseCoordinateSpace.ModelPixels);
var decoder = new DirectPoseDecoder(
    schema, topology,
    new PoseDecoderOptions(
        instanceScoreThreshold: 0.25f,
        keypointScoreThreshold: 0.2f,
        maximumCandidates: 100,
        maximumInstances: 20,
        maximumResultBytes: 16 * 1024 * 1024,
        oks: new PoseOksOptions(0.8f)));
~~~

HeatmapPoseDecoder 使用 [B,K,H,W]（NCHW）或 [B,H,W,K]（NHWC）热力图，并可附加每行一个实例分数。Direct decoder 使用 [B,N,K,C] 关键点，可附加 [B,N,4] boxes 与 [B,N] scores。

选择 decoder 时先看导出图，而不是模型名称：Heatmap 输出需要声明热图尺寸、坐标解码规则和是否使用半像素修正；Direct 输出需要声明每个组件的顺序（x、y、score、visibility 等）。如果模型同时输出 boxes 和 keypoints，二者的实例索引必须一一对应，不能通过排序“碰巧”对齐。

## 坐标与有效性

坐标空间可选 ModelPixels、Normalized 或 TensorGrid；TensorGrid/Heatmap 还要声明 HalfPixel 或 AlignCorners。结果通过 ImageTransform 还原到源图。边界策略可选 Preserve、Clip 或 MarkInvalid；可见性 Unknown、NotVisible、Visible 与坐标有效性分开处理。

PoseTopology 中的 sigma 只用于推理期 OKS 抑制，不是 COCO 评估器。正式模型应使用官方 sigma、面积和可见性规则。Alpha.1 不支持 SimCC、PAF、UDP/DARK、跟踪或 3D 姿态的隐式转换。

## Batch、并发与资源

当输入和输出合同都声明有界动态 batch 时，decoder 返回 PoseEstimationBatchResult；batch-one 保留 PoseEstimationResult。每行必须附带自己的 VisualInputFrame。batch-one 模型使用 RunManyAsync 或独立 session 池；多个 session 必须拥有独立 native 上下文。

结果对象是防御性托管数据，不保留后端输出或 OpenCV Mat。候选、关键点、输出字节和工作区均受上限保护，解码循环会观察取消。

## 结果校验与可视化

对每个实例检查关键点数量、坐标有限性、可见性和边界策略。`MarkInvalid` 表示关键点存在但坐标不可用，不能当成置信度为零；`NotVisible` 也不应被绘制成源图左上角的点。可视化时只绘制 `Valid && Visible` 的点，并按 `PoseTopology` 的边连接。

建议使用一张包含左右对称目标、一张被遮挡目标和一张贴近边界的图片做回归。检查镜像索引、sigma、坐标变换和 OKS 抑制前后的实例数。更换输入尺寸或热图插值方式后，必须重新验证关键点偏差，而不是只看总实例数。

## 后端与性能

元数据匹配时可选择 ONNX Runtime 或 OpenVINO；TensorRT 只支持已经绑定的静态视觉 Engine。性能测试请区分预处理、后端推理、峰值/OKS 解码和结果分配，并复用输入。具体模型状态见[模型支持指南](model-support.md)与[验证矩阵](../model-backend-verification-matrix.md)。

关键点数量较大时，后处理分配会超过模型执行本身。应设置候选和实例上限，复用准备输入，避免在每个关键点上创建临时对象；若只需要骨架绘制，可在结果层转换为扁平数组后立即释放中间 logits。批量模型需为每行保留独立 `VisualInputFrame`，不能把一张图的 transform 复用于整个 batch。
