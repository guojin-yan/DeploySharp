# Visual instance segmentation / Visual 实例分割

`JYPPX.DeploySharp.Visual` provides backend-neutral Direct-mask and Prototype/coefficient instance segmentation over `PreparedVisualInput`. It does not reference OpenCV or a concrete inference backend. Install ONNX Runtime or OpenVINO for inference; install `JYPPX.DeploySharp.Visual.OpenCV` only when encoded image input is required. / `JYPPX.DeploySharp.Visual` 基于 `PreparedVisualInput` 提供后端无关的 Direct 掩码与 Prototype/系数实例分割。它不引用 OpenCV 或具体推理后端。推理时安装 ONNX Runtime 或 OpenVINO；仅在需要编码图像输入时安装 `JYPPX.DeploySharp.Visual.OpenCV`。

## Exact output contracts / 精确输出契约

| Family / 系列 | Required outputs / 必需输出 | Layouts / 布局 | Reconstruction / 重建 |
| --- | --- | --- | --- |
| Direct | boxes `[1,N,4]`, scores `[1,N]`, classes `[1,N]`, masks | masks `[1,N,H,W]` or `[1,N,H,W,1]` | One independent grid per original candidate / 每个原始候选一个独立网格 |
| Prototype | boxes `[1,N,4]`, scores `[1,N]`, classes `[1,N]`, prototypes, coefficients `[1,N,C]` | prototypes `[1,C,H,W]` or `[1,H,W,C]` | `sum(coeff[c] * prototype[c,y,x])` / `sum(coeff[c] * prototype[c,y,x])` |

Every output name is explicit and extra outputs are rejected. Float32 and Float64 are supported. Class values must be finite, non-negative integers. Probability values must remain in `[0,1]`; binary values must be exactly zero or one; logits remain raw unless the schema explicitly selects sigmoid. The generic decoder never guesses a packed YOLO/Mask R-CNN row or applies an implicit activation. / 每个输出名称都必须显式声明，拒绝额外输出。支持 Float32 与 Float64。类别值必须是有限非负整数。概率必须位于 `[0,1]`，二值必须精确为零或一；除非 Schema 显式选择 sigmoid，否则 logits 保持原值。通用解码器绝不猜测打包 YOLO/Mask R-CNN 行，也不隐式应用激活。

## Direct profile / Direct Profile

```csharp
var candidates = new InstanceSegmentationCandidateSchema(
    "boxes", "scores", "classes",
    DetectionBoxFormat.Xyxy,
    normalizedBoxes: false,
    InstanceScoreKind.Probability);

var schema = new DirectInstanceSegmentationOutputSchema(
    candidates,
    "masks",
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
        scoreThreshold: 0.25f,
        maskThreshold: 0.5f,
        iouThreshold: 0.45f,
        nmsMode: DetectionNmsMode.ClassAware,
        overlapMode: InstanceMaskOverlapMode.Independent,
        maximumCandidates: 1000,
        maximumInstances: 100));
```

Bind all four named outputs in a `VisualModelProfile` whose task is `VisualTaskId.InstanceSegmentation`. The same profile and decoder work with any backend whose metadata matches those bindings. / 在任务为 `VisualTaskId.InstanceSegmentation` 的 `VisualModelProfile` 中绑定全部四个命名输出。同一 Profile 和解码器可用于任何元数据与这些绑定匹配的后端。

## Prototype profile / Prototype Profile

```csharp
var schema = new PrototypeInstanceSegmentationOutputSchema(
    candidates,
    "prototypes",
    "coefficients",
    InstanceMaskTensorLayout.Nchw,
    combinationValueKind: InstanceMaskValueKind.Logits,
    activation: InstanceMaskActivation.Sigmoid,
    interpolation: InstanceMaskInterpolationMode.BilinearHalfPixel,
    thresholdOrder: InstanceMaskThresholdOrder.AfterResize,
    cropSpace: InstanceMaskCropSpace.ModelInput,
    cropOrder: InstanceMaskCropOrder.BeforeResize);

var decoder = new PrototypeInstanceSegmentationDecoder(schema, options);
```

Score filtering and box NMS run before any candidate prototype combination. NCHW combines channel-major and NHWC combines position-major to preserve contiguous access. One bounded Float32 plane is allocated only for each retained instance; the decoder does not reconstruct suppressed candidates. / 分数筛选和边界框 NMS 在任何候选原型组合前执行。NCHW 按通道优先组合，NHWC 按位置优先组合，以保持连续访问。仅为每个保留实例分配一个有界 Float32 平面；解码器不会重建被抑制候选。

## Geometry and operation order / 几何与操作顺序

All result masks occupy full `SourceImage` space at origin `(0,0)`. Source pixel centers are mapped through `ImageTransform`, so Resize, Letterbox, Crop, and dynamic source sizes use the same reversible geometry as the rest of Visual. Letterbox padding and source pixels outside a configured crop become background. Candidate boxes use half-open model-input coordinates and returned boxes are clipped half-open source rectangles. / 所有结果掩码都占据原点 `(0,0)` 的完整 `SourceImage` 空间。源图像素中心通过 `ImageTransform` 映射，因此 Resize、Letterbox、Crop 和动态源图尺寸与 Visual 其他模块使用相同可逆几何。letterbox 填充和配置裁剪外的源图像素变为背景。候选框使用模型输入空间半开区间坐标，返回框是裁剪后的源图半开区间矩形。

Interpolation is explicit: `NearestNeighbor` uses half-open cells, `BilinearHalfPixel` uses half-pixel centers, and `BilinearAlignCorners` aligns corner centers; bilinear edges clamp to the nearest tensor position. `BeforeResize` crop zeros tensor samples whose mapped model-grid centers are outside the candidate box before interpolation. `AfterResize` crop tests each restored model-space source center. Before-resize thresholding requires nearest neighbor; after-resize thresholding applies the configured inclusive threshold to the restored continuous value. / 插值必须显式声明：`NearestNeighbor` 使用半开单元，`BilinearHalfPixel` 使用半像素中心，`BilinearAlignCorners` 对齐角点中心；双线性边缘钳制到最近张量位置。`BeforeResize` 裁剪会在插值前将映射后模型网格中心位于候选框外的张量采样置零；`AfterResize` 裁剪检查每个恢复后的模型空间源图中心。阈值前置要求最近邻；阈值后置对恢复后的连续值应用配置的包含边界阈值。

These choices must copy the official exporter/reference implementation exactly for an algorithm-specific profile. A different pixel-center rule, crop order, activation, or threshold order is a different model contract. / 算法专用 Profile 必须精确复制官方导出器/参考实现的这些选择。不同的像素中心规则、裁剪顺序、激活或阈值顺序就是不同模型契约。

## Results, overlap, RLE, and ownership / 结果、重叠、RLE 与所有权

`InstanceSegmentationResult` is ordered by score descending then original source index ascending. Each `InstanceSegmentationInstance` contains the original candidate index, class, label, score, clipped source box, owned dense binary mask, optional external ID/metadata, and optional RLE. Mask access, copy, foreground count, and SHA-256 are deterministic and remain valid after backend output/session disposal. / `InstanceSegmentationResult` 按分数降序再按原始源索引升序排列。每个 `InstanceSegmentationInstance` 包含原始候选索引、类别、标签、分数、裁剪后的源框、自有稠密二值掩码、可选外部 ID/元数据及可选 RLE。掩码访问、复制、前景计数与 SHA-256 均确定，并在后端输出/session 释放后仍有效。

The default `Independent` mode preserves overlapping masks. `ScorePriorityOwnership` additionally emits an `InstanceMaskOwnershipMap`; background is `-1`, and an overlap is assigned to the higher score or, on a tie, the smaller source index. Independent masks are never erased. / 默认 `Independent` 模式保留重叠掩码。`ScorePriorityOwnership` 额外输出 `InstanceMaskOwnershipMap`；背景为 `-1`，重叠像素归分数较高者，同分时归源索引较小者。独立掩码绝不会被擦除。

`InstanceMaskRle` stores foreground `(start,length)` runs in row-major order with format ID `deploysharp-row-major-foreground-runs-v1`. It is not COCO compressed RLE and must not be submitted to a COCO API without an explicit conversion. / `InstanceMaskRle` 按行优先顺序存储前景 `(start,length)` 游程，格式 ID 为 `deploysharp-row-major-foreground-runs-v1`。它不是 COCO 压缩 RLE，未经显式转换不得提交给 COCO API。

## Bounds, cancellation, and performance / 边界、取消与性能

`InstanceSegmentationDecoderOptions` independently bounds candidates, retained instances, prototype channels, tensor/result mask pixels, estimated result bytes, RLE runs, and workspace bytes. Invalid names, ranks, shapes, types, non-finite values, declared semantics, or bounds produce `DS-VISUAL-3001`/`DS-VISUAL-3002` with profile/model/tensor context. Cancellation is checked through candidate, NMS, prototype, source-row, and ownership work. Decoders are immutable and concurrent calls use no shared mutable mask workspace. / `InstanceSegmentationDecoderOptions` 分别限制候选、保留实例、原型通道、张量/结果掩码像素、估算结果字节、RLE 游程及工作区字节。名称、rank、形状、类型、非有限值、声明语义或边界无效时，使用带 Profile/模型/张量上下文的 `DS-VISUAL-3001`/`DS-VISUAL-3002`。在候选、NMS、原型、源图行和所有权工作中检查取消。解码器不可变，并发调用不共享可变掩码工作区。

The performance entry test records Direct and Prototype decode ticks and dimensions but sets no absolute latency threshold. Backend inference, preprocessing, transfer, and postprocessing must be benchmarked separately on a named machine before making performance claims. / 性能入口测试记录 Direct 与 Prototype 解码 tick 和尺寸，但不设置绝对延迟阈值。在提出性能声明前，必须在明确机器上分别基准测试后端推理、预处理、传输与后处理。

## Verified fixtures and algorithm status / 已验证夹具与算法状态

The repository generates `direct-instance-segmentation.onnx` (884 bytes, SHA256 `f0ebe35673e59fd6efcdb516a1626763dc06f2ed0092cac7fc1d5ec27b118a25`) and `prototype-instance-segmentation.onnx` (965 bytes, SHA256 `447d3878ecae690eeb832c5874a12e4d7427cb089fc72c2a8ba26b884f50cc66`) with `onnx==1.22.0`, opset 13, deterministic serialization, and `onnx.checker`. OpenVINO `2026.2.1` converts Direct to XML (3146 bytes, SHA256 `8d89c4f742bda4b45d5d00aed4e857106028c7da409baf636c8823662dc79e0a`) plus BIN (264 bytes, SHA256 `3da068fcb3020481c9a47f38ddb189bf27ac30ea569f5f5a2dba87f7973728b9`). / 仓库使用 `onnx==1.22.0`、opset 13、确定性序列化及 `onnx.checker` 生成 `direct-instance-segmentation.onnx`（884 字节，SHA256 `f0ebe35673e59fd6efcdb516a1626763dc06f2ed0092cac7fc1d5ec27b118a25`）和 `prototype-instance-segmentation.onnx`（965 字节，SHA256 `447d3878ecae690eeb832c5874a12e4d7427cb089fc72c2a8ba26b884f50cc66`）。OpenVINO `2026.2.1` 将 Direct 转换为 XML（3146 字节，SHA256 `8d89c4f742bda4b45d5d00aed4e857106028c7da409baf636c8823662dc79e0a`）与 BIN（264 字节，SHA256 `3da068fcb3020481c9a47f38ddb189bf27ac30ea569f5f5a2dba87f7973728b9`）。

Real CPU tests execute Direct and Prototype through ONNX Runtime, the same Direct profile through OpenVINO ONNX and IR, and a real PNG through Visual.OpenCV into ONNX Runtime. The common Direct result SHA-256 is identical across ONNX Runtime and both OpenVINO representations. ModelPack checks file size/SHA and IR sidecars; ModelFactory selects offline Preview records for `instance-segmentation + onnx + onnxruntime/openvino` and `instance-segmentation + openvino-ir + openvino`. / 真实 CPU 测试通过 ONNX Runtime 执行 Direct 与 Prototype，通过 OpenVINO ONNX 和 IR 执行同一 Direct Profile，并将真实 PNG 通过 Visual.OpenCV 输入 ONNX Runtime。ONNX Runtime 与两种 OpenVINO 表示的通用 Direct 结果 SHA-256 相同。ModelPack 检查文件大小/SHA 与 IR sidecar；ModelFactory 为 `instance-segmentation + onnx + onnxruntime/openvino` 及 `instance-segmentation + openvino-ir + openvino` 选择离线 Preview 记录。

These Apache-2.0 constant graphs are `ContractVerified` adapter fixtures only. They are not YOLO segmentation, Mask R-CNN, official algorithm models, accuracy evidence, benchmarks, model catalog entries, or GitHub Release assets. `AlgorithmVerified` requires legal model assets and official preprocessing/postprocessing golden evidence. The embedded official catalog remains empty; no tag, Release, or asset upload is performed. TensorRT remains excluded until the user confirms `JYPPX.TensorRT.CSharp.API` is ready. / 这些 Apache-2.0 常量图仅是 `ContractVerified` 适配器夹具。它们不是 YOLO 分割、Mask R-CNN、官方算法模型、精度证据、性能基准、模型目录条目或 GitHub Release 资产。`AlgorithmVerified` 需要合法模型资产及官方预处理/后处理黄金证据。嵌入式官方目录保持为空；不执行 tag、Release 或资产上传。TensorRT 在用户确认 `JYPPX.TensorRT.CSharp.API` 就绪前继续排除。
