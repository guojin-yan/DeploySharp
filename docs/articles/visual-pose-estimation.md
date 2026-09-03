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

## 坐标与有效性

坐标空间可选 ModelPixels、Normalized 或 TensorGrid；TensorGrid/Heatmap 还要声明 HalfPixel 或 AlignCorners。结果通过 ImageTransform 还原到源图。边界策略可选 Preserve、Clip 或 MarkInvalid；可见性 Unknown、NotVisible、Visible 与坐标有效性分开处理。

PoseTopology 中的 sigma 只用于推理期 OKS 抑制，不是 COCO 评估器。正式模型应使用官方 sigma、面积和可见性规则。Alpha.1 不支持 SimCC、PAF、UDP/DARK、跟踪或 3D 姿态的隐式转换。

## Batch、并发与资源

当输入和输出合同都声明有界动态 batch 时，decoder 返回 PoseEstimationBatchResult；batch-one 保留 PoseEstimationResult。每行必须附带自己的 VisualInputFrame。batch-one 模型使用 RunManyAsync 或独立 session 池；多个 session 必须拥有独立 native 上下文。

结果对象是防御性托管数据，不保留后端输出或 OpenCV Mat。候选、关键点、输出字节和工作区均受上限保护，解码循环会观察取消。

## 后端与性能

元数据匹配时可选择 ONNX Runtime 或 OpenVINO；TensorRT 只支持已经绑定的静态视觉 Engine。性能测试请区分预处理、后端推理、峰值/OKS 解码和结果分配，并复用输入。具体模型状态见[模型支持指南](model-support.md)与[验证矩阵](../model-backend-verification-matrix.md)。
