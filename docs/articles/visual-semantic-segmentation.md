# Visual semantic segmentation / Visual 语义分割

`JYPPX.DeploySharp.Visual` provides a backend-neutral semantic segmentation vertical slice. It accepts already prepared tensors, runs any Core tensor backend, and returns an owned class-index mask. It does not reference OpenCV, ONNX Runtime, OpenVINO, or another image library/backend. / `JYPPX.DeploySharp.Visual` 提供后端无关的语义分割垂直切片。它接收已准备张量，通过任意 Core 张量后端运行，并返回自有类别索引掩码；不引用 OpenCV、ONNX Runtime、OpenVINO 或其他图像库/后端。

## Output contracts / 输出契约

| Kind / 类型 | Types / 元素类型 | Layouts / 布局 | Rule / 规则 |
| --- | --- | --- | --- |
| `Logits` | Float32, Float64 | NCHW, NHWC, CHW, HWC | No implicit activation; multiclass argmax or explicit/default binary threshold 0 / 不隐式激活；多类 argmax 或显式/默认二值阈值 0 |
| `Probabilities` | Float32, Float64 | NCHW, NHWC, CHW, HWC | Every value must be `[0,1]`; binary default threshold 0.5 / 每个值必须在 `[0,1]`；二值默认阈值 0.5 |
| `LabelMap` | Int8/UInt8 through Int64/UInt64 | NCHW, NHWC, CHW, HWC, NHW, HW | One channel; non-negative class indices below `ClassCount` / 单通道；非负且小于 `ClassCount` 的类别索引 |

Batch size is one in alpha.1. A single score channel represents two classes; the configured background class receives values below threshold. Multiclass ties select the lowest class index. Probability retention is optional, is allowed only for `Probabilities`, and remains at tensor resolution in canonical row-major HWC order. / alpha.1 仅支持 batch 1。单分数通道表示两个类别，低于阈值的值归入配置的背景类。多类同值时选择最小类别索引。概率保留是可选项，仅允许用于 `Probabilities`，并以规范行优先 HWC 顺序保留在张量分辨率。

## Minimal profile / 最小 Profile

```csharp
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

var profile = new VisualModelProfile(
    "samples/segmenter.v1",
    modelId,
    VisualTaskId.SemanticSegmentation,
    "1.0",
    "onnx",
    new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 512, 512), VisualTensorLayout.Nchw),
    new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(1, 3, 512, 512)) },
    new[] { new VisualLabel(0, "background"), new VisualLabel(1, "road"), new VisualLabel(2, "person") },
    decoder);
```

Register `JYPPX.DeploySharp.Backend.OnnxRuntime` or `JYPPX.DeploySharp.Backend.OpenVINO`, select the profile through `VisualProfileRegistry`, then call `VisualPipeline.Run`/`RunAsync`. For encoded images, install `JYPPX.DeploySharp.Visual.OpenCV` and the matching application-owned native runtime to create `PreparedVisualInput`; manual tensors remain fully supported. / 注册 `JYPPX.DeploySharp.Backend.OnnxRuntime` 或 `JYPPX.DeploySharp.Backend.OpenVINO`，通过 `VisualProfileRegistry` 选择 Profile，然后调用 `VisualPipeline.Run`/`RunAsync`。若输入编码图像，请安装 `JYPPX.DeploySharp.Visual.OpenCV` 及匹配的应用自有 native runtime 来创建 `PreparedVisualInput`；仍完整支持手工张量。

## Geometry and ownership / 几何与所有权

`Source` output mode is the default. The decoder first aligns the output mask to model input size, then samples source pixel centers through `ImageTransform` using nearest-neighbor semantics. Letterbox padding is not returned as source content. Crop pixels outside the crop become the configured background class. `Model` and `Tensor` modes keep their named resolutions. / `Source` 是默认输出模式。解码器先把输出掩码对齐到模型输入尺寸，再通过 `ImageTransform` 按最近邻语义采样源图像素中心。letterbox 填充不会作为源图内容返回；crop 区域外像素变为配置的背景类。`Model` 与 `Tensor` 模式保留各自命名的分辨率。

This generic decoder does not represent models whose official reference first resizes logits or probabilities with bilinear, bicubic, align-corners, or another model-specific rule before argmax. Such a model requires a dedicated profile/decoder that reproduces the official order, interpolation, rounding, and tolerance against golden data before it can be marked `AlgorithmVerified`. / 对于官方参考实现在 argmax 前先按双线性、双三次、align-corners 或其他模型特有规则缩放 logits/概率的模型，本通用解码器不能代表其正式后处理。此类模型必须提供专用 Profile/decoder，并用黄金数据验证官方操作顺序、插值、取整与容差后，才能标记为 `AlgorithmVerified`。

`SemanticSegmentationMask`, optional `SegmentationProbabilityMap`, and decoded RLE arrays are defensive managed copies. They do not retain backend tensor, native request, or OpenCV Mat lifetimes. `CreateBinaryMask(classIndex)` derives a per-class byte mask on demand. / `SemanticSegmentationMask`、可选 `SegmentationProbabilityMap` 与解码后的 RLE 数组都是防御性托管副本，不保留后端张量、native request 或 OpenCV Mat 生命周期。`CreateBinaryMask(classIndex)` 可按需派生逐类别 byte 掩码。

## RLE, regions, and limits / RLE、区域与限制

DeploySharp RLE stores complete contiguous row-major runs. It is not COCO compressed RLE. `Encode(mask).Decode()` is deterministic and the mask SHA256 covers little-endian width, height, and row-major `ushort` indices. `MinimumRegionPixels` applies four-connected filtering to non-background/non-ignore regions. / DeploySharp RLE 存储完整连续的行优先游程，不是 COCO 压缩 RLE。`Encode(mask).Decode()` 具有确定性，掩码 SHA256 覆盖小端宽度、高度与行优先 `ushort` 索引。`MinimumRegionPixels` 对非背景、非 ignore 区域应用四连通过滤。

`MaximumOutputBytes` bounds the estimated tensor copy, result mask, worst-case RLE, retained probabilities, and region-filter workspace before large allocations. Invalid rank/layout/type/class values use stable `DS-VISUAL-3001`/`DS-VISUAL-3002` diagnostics. Polygon requests return `DS-VISUAL-3003`; polygon topology is not advertised in alpha.1. / `MaximumOutputBytes` 会在大规模分配前限制估算的张量副本、结果掩码、最坏情况 RLE、保留概率与区域过滤工作区。无效 rank/layout/type/class 值使用稳定的 `DS-VISUAL-3001`/`DS-VISUAL-3002` 诊断。多边形请求返回 `DS-VISUAL-3003`；alpha.1 不声明多边形拓扑能力。

## Verified fixtures and supply chain / 已验证夹具与供应链

The repository generates `semantic-segmentation.onnx`, `binary-segmentation.onnx`, and `semantic-label-map.onnx` with `onnx==1.22.0`, opset 13. OpenVINO `2026.2.1` converts the multiclass graph to `semantic-segmentation.xml + .bin`. The tiny Apache-2.0 fixtures have size/SHA256 manifests and are adapter contracts, not official algorithm models, benchmarks, or GitHub Release assets. / 仓库使用 `onnx==1.22.0`、opset 13 生成 `semantic-segmentation.onnx`、`binary-segmentation.onnx` 与 `semantic-label-map.onnx`。OpenVINO `2026.2.1` 将多类图转换为 `semantic-segmentation.xml + .bin`。这些 Apache-2.0 微型夹具带有大小/SHA256 清单，只是适配器契约，不是官方算法模型、性能基准或 GitHub Release 资产。

Real CPU tests cover ONNX Runtime logits/probability/Int64 label-map, OpenVINO ONNX/IR, and a PNG decoded by Visual.OpenCV. ModelPack and ModelFactory use an offline Preview entry in tests only. The embedded official catalog remains empty and no Release, tag, asset, model, or test image is uploaded by this stage. / 真实 CPU 测试覆盖 ONNX Runtime logits/概率/Int64 标签图、OpenVINO ONNX/IR，以及 Visual.OpenCV 解码的 PNG。ModelPack 与 ModelFactory 只在测试中使用离线 Preview 条目。嵌入式官方目录保持为空，本阶段不上传 Release、tag、asset、模型或测试图像。
