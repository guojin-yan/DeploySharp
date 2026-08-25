# Visual anomaly detection and segmentation / Visual 异常检测与异常分割

`JYPPX.DeploySharp.Visual` provides a backend-neutral `AnomalyPipeline` for an image-level anomaly score plus a pixel-level anomaly map. `JYPPX.DeploySharp.Visual.OpenCV` is optional and converts encoded images to owned `PreparedVisualInput` tensors. ONNX Runtime and OpenVINO only execute named tensors; they contain no anomaly-specific branch. / `JYPPX.DeploySharp.Visual` 提供后端无关的 `AnomalyPipeline`，同时返回图像级异常分数和像素级异常图。`JYPPX.DeploySharp.Visual.OpenCV` 是可选组件，用于把编码图像转换为自有 `PreparedVisualInput` 张量。ONNX Runtime 与 OpenVINO 只执行命名张量，不含异常模型特例。

## Contract / 契约

Alpha.1 requires exactly two outputs: a scalar score and one map. `AnomalyMapSchema` declares exact names, NCHW/NHWC/CHW/HWC layout, channel count, value semantics and coordinate space. `AnomalyDecoderOptions` declares channel aggregation, normalization, threshold, output size, interpolation and resource bounds. Shape-based guessing is rejected. / Alpha.1 要求恰好两个输出：一个标量分数和一张异常图。`AnomalyMapSchema` 显式声明名称、NCHW/NHWC/CHW/HWC layout、通道数、数值语义与坐标空间；`AnomalyDecoderOptions` 声明通道聚合、归一化、阈值、输出尺寸、插值和资源上限。禁止根据 shape 猜测语义。

| Area / 范围 | Supported / 支持 | Rejected or unsupported / 拒绝或暂不支持 |
| --- | --- | --- |
| Map element / Map 元素 | Float32, Float64 / Float32、Float64 | Integers and strings / 整数与字符串 |
| Value semantics / 数值语义 | `[0,1]` probability, non-negative distance, binary / 概率、非负距离、二值 | NaN, infinity, out-of-contract values / NaN、无穷和越界值 |
| Channels / 通道 | single, maximum, mean / 单通道、最大值、均值 | implicit channel meaning / 隐式通道含义 |
| Normalization / 归一化 | none, min-max, fixed range / none、min-max、固定范围 | hidden per-model behavior / 隐藏的逐模型行为 |
| Threshold / 阈值 | explicit fixed threshold / 显式固定阈值 | percentile and model-provided in alpha.1 / alpha.1 暂不支持百分位和模型提供阈值 |
| Resize / Resize | nearest, bilinear half-pixel / 最近邻、双线性 half-pixel | backend-dependent defaults / 后端相关默认值 |

Min-max normalization of a constant map deterministically returns zero and warning `anomaly.constant-map`; it never divides by zero. Binary masks use row-major bytes and `AnomalousPixelRatio` is computed from the final restored mask. / 常量图执行 min-max 时确定性返回零图和警告 `anomaly.constant-map`，不会除零。二值掩码使用行优先字节，`AnomalousPixelRatio` 基于最终恢复后的掩码计算。

## Basic use / 基础用法

```csharp
var decoder = new AnomalyDecoder(
    new AnomalyMapSchema(
        "image_score",
        "anomaly_map",
        AnomalyMapValueMode.Probabilities,
        AnomalyTensorLayout.Nchw,
        channels: 2),
    new AnomalyDecoderOptions(
        normalization: AnomalyNormalizationMode.FixedRange,
        threshold: 0.6f,
        channelAggregation: AnomalyChannelAggregation.Maximum,
        outputSizeMode: AnomalyOutputSizeMode.Source,
        interpolation: AnomalyMapInterpolation.BilinearHalfPixel));

var profile = new VisualModelProfile(
    "my-anomaly.v1", modelId, VisualTaskId.AnomalyDetection, "1.0", "onnx",
    inputBinding, outputBindings, Array.Empty<VisualLabel>(), decoder);

using var pipeline = new AnomalyPipeline(registry, selection, request);
using PreparedVisualInput input = imageFactory.CreateFromFile(imagePath, "images", preprocess);
AnomalyDetectionResult result = await pipeline.RunAsync(input, executionOptions, cancellationToken);
```

`RawMap` represents the aggregated map before normalization; `NormalizedMap` and `Mask` use the requested output space. All arrays are copied into owned managed storage and remain usable after the inference request is released. `ComputeSha256()` provides a canonical regression fingerprint, not a cryptographic proof of model provenance. / `RawMap` 表示归一化前的聚合图；`NormalizedMap` 与 `Mask` 使用请求的输出空间。所有数组都复制到自有托管存储，在 inference request 释放后仍可使用。`ComputeSha256()` 提供规范回归指纹，不是模型来源的密码学证明。

## Geometry and fidelity / 几何与保真

For model-space maps the decoder first resizes to the model input, then restores each source pixel through the recorded `ImageTransform`. Resize, letterbox and crop therefore follow the same invertible geometry as other Visual tasks. A source-space exporter must provide exact source dimensions. / 对模型空间异常图，解码器先 resize 到模型输入尺寸，再通过记录的 `ImageTransform` 恢复每个源图像素。因此 resize、letterbox 与 crop 遵循其他 Visual 任务相同的可逆几何。声明源图空间的 exporter 必须输出与源图完全一致的尺寸。

A production Profile must match official orientation, color order, resize rounding, padding, interpolation, normalization, dtype/layout, output activation, score reduction, anomaly-map semantics, calibration set, threshold and metric implementation. The repository fixture proves tensor and lifecycle contracts only. Use the [AlgorithmVerified anomaly template](../templates/anomaly-algorithm-verification-template.md) before official admission. / 正式 Profile 必须匹配官方方向、颜色顺序、resize 取整、padding、插值、归一化、dtype/layout、输出激活、分数归约、异常图语义、校准集、阈值与指标实现。仓库夹具只证明张量与生命周期合同。官方准入前请填写[异常模型 AlgorithmVerified 模板](../templates/anomaly-algorithm-verification-template.md)。

## Cancellation, limits, and performance / 取消、边界与性能

Cancellation and one optional timeout span backend execution and postprocessing. Invalid schema/value/shape errors use `DS-VISUAL-4201`, bounded pixel/workspace/result failures use `DS-VISUAL-4202`, and unavailable semantic policies use `DS-VISUAL-4203`. The pipeline is reusable, obeys `SessionOptions.MaxConcurrency`, rejects work after disposal, cancels active work during disposal and releases its backend session idempotently. / 调用方取消和可选超时覆盖后端执行及后处理。无效 Schema/数值/shape 使用 `DS-VISUAL-4201`，像素/workspace/结果边界超限使用 `DS-VISUAL-4202`，不可用语义策略使用 `DS-VISUAL-4203`。Pipeline 可复用、遵守 `SessionOptions.MaxConcurrency`、释放后拒绝新任务、释放时取消活动任务并幂等释放后端 session。

Performance evidence must separate image decode/preprocess, backend execution, map aggregation/normalization/restore/threshold and end-to-end P50/P95. Report image/model/map size, channels, interpolation, hardware/runtime, concurrency, allocations and official accuracy metrics. The tiny constant graph is not a production speed result. / 性能证据必须拆分图像解码/前处理、后端执行、异常图聚合/归一化/恢复/阈值及端到端 P50/P95，并报告图像/模型/异常图尺寸、通道、插值、硬件/runtime、并发、分配与官方精度指标。微型常量图不是正式性能结果。

## Reproducible contract evidence / 可复现合同证据

| Asset / 工件 | Bytes | SHA256 |
| --- | ---: | --- |
| `anomaly-detection.onnx` | 896 | `1892fa25b754f9e5e7b16686649a95a5ae035eef17f3f3cbc460d8a771c70833` |
| `anomaly-detection.xml` | 5524 | `c8cd5634ab6ced3c4d175e193a575c4fdf53b634d692cb3805444605aff4b628` |
| `anomaly-detection.bin` | 152 | `55a9f61e669f263046975877c8701ce1ec2f2aee3c1960e8190e2d1dd156683d` |
| `anomaly.png` | 114 | `ef340a12f371ff77c4357350b6c78304abb2c08a07d61e7c5a7569bd7991e3d0` |
| canonical result / 规范结果 | — | `f418bc5e06bb64863b38860375335aa9fdde1c6cd706ac3776457dbf53dbf7da` |

ONNX Runtime 1.28.0 executes the ONNX fixture. The audited OpenVINO combination remains managed API 3.3.0 plus Windows runtime 2026.2.1 and executes both ONNX and generated IR on CPU. Visual.OpenCV uses API/runtime 5.0.0-preview.1 for the real PNG path. Newer OpenVINO 3.3.1/2026.3.0 publications were observed on 2026-08-06 but are not silently substituted for the locked, previously audited combination. TensorRT is intentionally not implemented. / ONNX Runtime 1.28.0 执行 ONNX 夹具。经审计的 OpenVINO 组合仍为 managed API 3.3.0 与 Windows runtime 2026.2.1，并在 CPU 上执行 ONNX 和生成的 IR。Visual.OpenCV 使用 API/runtime 5.0.0-preview.1 完成真实 PNG 路径。2026-08-06 已观察到 OpenVINO 3.3.1/2026.3.0 发布，但不会静默替换已锁定、此前审计的组合。TensorRT 按要求不实现。

ModelPack validates ONNX plus IR XML/BIN sizes and SHA256. ModelFactory exposes PaDiM and BRIA RMBG as opt-in Preview entries for `onnx + onnxruntime/openvino`; the existing [Vision alpha preview Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-20260817.vision.1) contains the PaDiM model and BRIA RMBG 1.4/2.0 ModelPacks plus fp32/dynamic-int8 ONNX assets. BRIA 1.4 is `176153355` bytes (`8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f`), BRIA 2.0 fp32 is `1024331469` bytes (`5b486f08200f513f460da46dd701db5fbb47d79b4be4b708a19444bcd4e79958`), and BRIA 2.0 dynamic-int8 is `366087549` bytes (`fcea23951a378f92634834888896cc1eec54655366ae6e949282646ce17c5420`). These records remain Preview and are not `AlgorithmVerified` or GA. / ModelPack 校验 ONNX 以及 IR XML/BIN 的大小和 SHA256。ModelFactory 现在将 PaDiM 与 BRIA RMBG 作为 `onnx + onnxruntime/openvino` 的可选 Preview 条目；已有的 [Vision alpha preview Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-20260817.vision.1) 包含 PaDiM 模型、BRIA RMBG 1.4/2.0 ModelPack 以及 fp32/dynamic-int8 ONNX 资产。BRIA 1.4 为 `176153355` 字节（`8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f`），BRIA 2.0 fp32 为 `1024331469` 字节（`5b486f08200f513f460da46dd701db5fbb47d79b4be4b708a19444bcd4e79958`），BRIA 2.0 dynamic-int8 为 `366087549` 字节（`fcea23951a378f92634834888896cc1eec54655366ae6e949282646ce17c5420`）。这些记录仍是 Preview，不是 `AlgorithmVerified` 或 GA。
