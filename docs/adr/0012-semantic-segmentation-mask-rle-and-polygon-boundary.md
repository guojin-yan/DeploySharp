# ADR 0012: semantic masks, RLE and polygon boundary / 语义掩码、RLE 与多边形边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

Semantic segmentation backends commonly return multi-channel logits/probabilities or integer label maps. The existing `VisualOutputBinding` admitted only Float32/Float64, even though Core tensors already represent integer outputs. Dense masks can also allocate much more memory than classification or detection results, and polygons require explicit hole and multi-component semantics. / 语义分割后端通常返回多通道 logits/概率或整数标签图。现有 `VisualOutputBinding` 只允许 Float32/Float64，但 Core 张量已经能够表示整数输出。稠密掩码的内存分配也可能远大于分类或检测结果，而多边形必须明确孔洞与多连通域语义。

## Decision / 决策

Semantic segmentation remains entirely in `JYPPX.DeploySharp.Visual`; Core gains no segmentation contract or image dependency. `VisualOutputBinding` admits backend-neutral non-unknown/non-string output types, while each decoder validates its exact types. `SemanticSegmentationDecoder` explicitly distinguishes logits, probabilities, and label maps and never applies an implicit sigmoid or softmax. Equal multiclass values choose the lowest class index. / 语义分割完全保留在 `JYPPX.DeploySharp.Visual`；Core 不新增分割契约或图像依赖。`VisualOutputBinding` 允许后端无关的非 Unknown/非 String 输出类型，各解码器再验证自身精确类型。`SemanticSegmentationDecoder` 显式区分 logits、概率与标签图，绝不隐式应用 sigmoid 或 softmax；多类同值时选择最小类别索引。

The canonical result owns a row-major `ushort` class-index mask. Source restoration uses nearest-neighbor sampling through the recorded affine transform for resize, letterbox, crop, and custom mappings. Optional probability retention is canonical HWC at tensor resolution and is available only for declared probability outputs. A configurable memory estimate rejects oversized tensors/results before large allocations. / 规范结果拥有行优先 `ushort` 类别索引掩码。源图恢复通过记录的仿射变换对 resize、letterbox、crop 与 custom 映射进行最近邻采样。可选概率保留采用张量分辨率的规范 HWC 顺序，且仅适用于声明为概率的输出。可配置内存估算会在大规模分配前拒绝过大的张量或结果。

`SegmentationRle` is a complete contiguous sequence of `(start, length, classIndex)` runs over the row-major mask. It is deterministic and round-trippable, but explicitly not COCO compressed RLE. Polygon extraction is `Unsupported` in alpha.1 and a request fails with `DS-VISUAL-3003`; no unreliable contour approximation is advertised. / `SegmentationRle` 是覆盖完整行优先掩码的连续 `(start, length, classIndex)` 游程序列，具有确定性且可往返，但明确不是 COCO 压缩 RLE。alpha.1 中多边形提取为 `Unsupported`，请求时稳定返回 `DS-VISUAL-3003`；不声明不可靠的轮廓近似能力。

## Consequences / 影响

ONNX Runtime and OpenVINO can use the same profile and decoder without backend-specific Visual code. OpenCV remains an optional input adapter. Applications needing COCO RLE or polygon topology must convert from the owned dense mask in an integration package that defines those semantics. / ONNX Runtime 与 OpenVINO 可以使用同一 Profile 和解码器，Visual 中没有后端特例。OpenCV 仍是可选输入适配器。需要 COCO RLE 或多边形拓扑的应用必须在定义相应语义的集成包中从自有稠密掩码转换。
