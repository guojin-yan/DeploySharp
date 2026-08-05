# Visual Pose estimation / Visual 姿态估计

`JYPPX.DeploySharp.Visual` provides backend-neutral direct-output and heatmap Pose decoding over already prepared tensors. The same profile runs through ONNX Runtime or OpenVINO, while `JYPPX.DeploySharp.Visual.OpenCV` is an optional encoded-image input adapter. Core and backend packages do not reference Pose-specific types. / `JYPPX.DeploySharp.Visual` 对已准备张量提供后端无关的 direct 输出与 heatmap Pose 解码。同一 Profile 可通过 ONNX Runtime 或 OpenVINO 运行，`JYPPX.DeploySharp.Visual.OpenCV` 只是可选编码图像输入适配器。Core 与后端包均不引用 Pose 特有类型。

## Supported contracts / 支持的契约

| Contract / 契约 | Shape / 形状 | Semantics / 语义 |
| --- | --- | --- |
| Direct | `[1,N,K,C]` | Explicit x/y, optional keypoint score and visibility components; optional separately named `[1,N,4]` boxes and `[1,N]` scores / 显式 x/y、可选关键点分数与可见性组件；可选独立命名 `[1,N,4]` boxes 和 `[1,N]` scores |
| Heatmap NCHW | `[1,K,H,W]` | One row-major peak per keypoint / 每个关键点选择一个行优先峰值 |
| Heatmap NHWC | `[1,H,W,K]` | Same peak rule with explicit layout / 使用显式布局和相同峰值规则 |

Float32 and Float64 are supported. Probability schemas require every value in `[0,1]`; raw-score schemas retain finite values without applying sigmoid or softmax. Heatmap decoding does not apply sub-pixel refinement. Equal peaks choose the lowest row-major index. / 支持 Float32 与 Float64。概率 Schema 要求每个值在 `[0,1]`；raw-score Schema 保留有限值且不应用 sigmoid 或 softmax。热力图解码不应用亚像素修正。同值峰选择最小行优先索引。

SimCC, PAF/associative embedding, UDP/DARK refinement, packed YOLO-Pose rows, tracking, 3D Pose, and model-specific flip tests are unsupported in alpha.1. Add a dedicated decoder that reproduces the exporter and official reference exactly; do not approximate those formats with the generic schemas. / alpha.1 不支持 SimCC、PAF/关联嵌入、UDP/DARK 修正、打包 YOLO-Pose 行、跟踪、3D Pose 与模型特有翻转测试。此类格式应添加精确复现导出器及官方参考实现的专用解码器，不得使用通用 Schema 近似。

## Direct profile / Direct Profile

```csharp
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
    schema,
    topology,
    new PoseDecoderOptions(
        instanceScoreThreshold: 0.25f,
        keypointScoreThreshold: 0.2f,
        maximumCandidates: 100,
        maximumInstances: 20,
        maximumResultBytes: 16 * 1024 * 1024,
        oks: new PoseOksOptions(0.8f)));

var profile = new VisualModelProfile(
    "samples/pose.v1",
    modelId,
    VisualTaskId.PoseEstimation,
    "1.0",
    "onnx",
    new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 256, 192), VisualTensorLayout.Nchw),
    new[]
    {
        new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1, -1, 4)),
        new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, -1)),
        new VisualOutputBinding("keypoints", TensorElementType.Float32, new TensorShape(1, -1, 3, 4))
    },
    Array.Empty<VisualLabel>(),
    decoder);
```

The decoder requires exactly the declared named outputs. Candidate order is deterministic: instance score descending, then source index ascending. Optional OKS suppression is applied in that order. `MaximumCandidates`, `MaximumInstances`, `MaximumKeypoints`, and `MaximumResultBytes` are checked before or during bounded allocation. / 解码器要求输出名称与声明完全一致。候选顺序确定为实例分数降序、来源索引升序，可选 OKS 按该顺序抑制。`MaximumCandidates`、`MaximumInstances`、`MaximumKeypoints` 与 `MaximumResultBytes` 会在有界分配前或期间检查。

## Coordinates, visibility, and validity / 坐标、可见性与有效性

Direct coordinates must declare `ModelPixels`, `Normalized`, or `TensorGrid`. Tensor-grid and heatmap coordinates additionally declare `HalfPixel` or `AlignCorners`; the decoder never guesses. Result coordinates are source-image pixel centers restored through `ImageTransform`, so resize, letterbox, and center crop use the same reversible geometry as classification/detection/segmentation. / Direct 坐标必须声明 `ModelPixels`、`Normalized` 或 `TensorGrid`。张量网格与热力图坐标还要声明 `HalfPixel` 或 `AlignCorners`，解码器绝不猜测。结果坐标是通过 `ImageTransform` 恢复的源图像素中心，因此 resize、letterbox 与 center crop 使用和分类/检测/分割相同的可逆几何。

`Preserve` retains an out-of-source coordinate, `Clip` clamps it to the source rectangle, and `MarkInvalid` retains the coordinate while setting `IsValid=false`. Visibility is separate: `Unknown`, `NotVisible`, and `Visible` describe model-declared semantics, while validity also considers score threshold and boundary policy. A model that exposes only confidence remains `Unknown`; DeploySharp does not invent COCO ground-truth visibility. / `Preserve` 保留源图外坐标，`Clip` 将其限制到源图矩形，`MarkInvalid` 保留坐标但设置 `IsValid=false`。可见性与有效性分离：`Unknown`、`NotVisible` 和 `Visible` 描述模型声明语义；有效性还考虑分数阈值与边界策略。只输出置信度的模型保持 `Unknown`，DeploySharp 不伪造 COCO 真实标注可见性。

## OKS scope / OKS 范围

`PoseOks.CalculateSimilarity` uses explicit per-keypoint sigmas, valid/visible common keypoints, and an explicit reference area. `PoseOksOptions` uses that pairwise value for deterministic inference-time suppression. It follows the COCO exponential distance normalization but is not the COCO evaluator: it does not implement matching, ignore/crowd handling, area ranges, recall, or AP. For official accuracy claims, copy the exact sigma table, area rule, visibility rule, thresholds, and postprocessing order from the official model reference and compare golden outputs within documented tolerances. / `PoseOks.CalculateSimilarity` 使用显式逐关键点 sigma、双方共同有效/可见关键点及显式参考面积。`PoseOksOptions` 使用该成对值执行确定性的推理期抑制。它遵循 COCO 指数距离归一化，但不是 COCO evaluator：不实现匹配、ignore/crowd、面积区间、召回或 AP。正式精度声明必须从官方模型参考实现复制精确 sigma 表、面积规则、可见性规则、阈值与后处理顺序，并按记录容差比较黄金输出。

## Ownership, cancellation, and performance / 所有权、取消与性能

`PoseEstimationResult`, instances, keypoints, topology, and optional source boxes are defensive managed data. They do not retain backend output tensors, OpenVINO requests, ONNX Runtime values, or OpenCV Mat objects. Cancellation is observed before decoding and throughout candidate, heatmap, and OKS loops. A benchmark-style test records repeated decode elapsed time and managed allocation as evidence entry points, but no machine-independent latency claim is made. Hot loops avoid LINQ and repeated score validation; probability heatmaps are validated once before peak search. / `PoseEstimationResult`、实例、关键点、拓扑与可选源框都是防御性托管数据，不保留后端输出张量、OpenVINO request、ONNX Runtime value 或 OpenCV Mat。解码前及候选、热力图与 OKS 循环中都会观察取消。基准风格测试记录重复解码耗时和托管分配作为证据入口，但不声明跨机器固定延迟。热循环避免 LINQ 和重复分数校验；概率热力图在峰值搜索前一次校验。

## Verified fixtures and supply chain / 已验证夹具与供应链

The repository generates `direct-pose.onnx` (681 bytes, SHA256 `237ea6ae4b9b34cefcafa07b4d45e5cc644c5963f91357c5e498bbde9f91aa96`) and `heatmap-pose.onnx` (445 bytes, SHA256 `12bf5bc741e70d9ea6a2d567fa857f8d7aa2d008cd4b409ccd73c279ce4a2e9`) with `onnx==1.22.0`, opset 13. OpenVINO `2026.2.1` converts the direct graph into `direct-pose.xml` (SHA256 `8972c5e9dad75e55e63f2e394db2b843901ba739392bbbfba9ef268e54a239f6`) plus `direct-pose.bin` (SHA256 `06c59f2a4aab85de4591f5644db055c9d6189d1fefaadac16f86acc4e4558853`). Manifests record exact size, hash, inputs, and outputs. / 仓库使用 `onnx==1.22.0`、opset 13 生成 `direct-pose.onnx`（681 字节，SHA256 `237ea6ae4b9b34cefcafa07b4d45e5cc644c5963f91357c5e498bbde9f91aa96`）和 `heatmap-pose.onnx`（445 字节，SHA256 `12bf5bc741e70d9ea6a2d567fa857f8d7aa2d008cd4b409ccd73c279ce4a2e9`）。OpenVINO `2026.2.1` 将 direct 图转换为 `direct-pose.xml`（SHA256 `8972c5e9dad75e55e63f2e394db2b843901ba739392bbbfba9ef268e54a239f6`）与 `direct-pose.bin`（SHA256 `06c59f2a4aab85de4591f5644db055c9d6189d1fefaadac16f86acc4e4558853`）。清单记录精确大小、哈希、输入与输出。

Real CPU tests cover ONNX Runtime direct/heatmap inference, OpenVINO ONNX/IR direct inference, and PNG decode/resize through Visual.OpenCV into ONNX Runtime. ModelPack validates every file and ModelFactory selects offline Preview records in tests only. These constant Apache-2.0 graphs are adapter contracts, not official Pose algorithms, accuracy evidence, benchmarks, catalog assets, or GitHub Release assets. The embedded official catalog remains empty. No TensorRT code or dependency exists because `JYPPX.TensorRT.CSharp.API` is still under development. / 真实 CPU 测试覆盖 ONNX Runtime direct/heatmap 推理、OpenVINO ONNX/IR direct 推理，以及 Visual.OpenCV 解码/缩放 PNG 后进入 ONNX Runtime。ModelPack 校验每个文件，ModelFactory 只在测试中选择离线 Preview 记录。这些 Apache-2.0 常量图只是适配器契约，不是官方 Pose 算法、精度证据、性能基准、目录资产或 GitHub Release 资产。嵌入式官方目录保持为空。`JYPPX.TensorRT.CSharp.API` 仍在开发，因此不存在任何 TensorRT 代码或依赖。
