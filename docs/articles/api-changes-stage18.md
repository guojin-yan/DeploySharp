# Stage 18 API changes / 阶段 18 API 变更

Stage 18 adds portable non-YOLO detector contracts to the existing `JYPPX.DeploySharp.Visual` and `JYPPX.DeploySharp.Visual.OpenCV` packages. / 阶段 18 在现有 `JYPPX.DeploySharp.Visual` 与 `JYPPX.DeploySharp.Visual.OpenCV` 包中增加便携非 YOLO 检测器合同。

## Public additions / 公共新增

- `PortableDetectorFamily`, `PortableDetectorOutputKind`, `PortableDetectorOutputContract`, `PortableDetectorProfileOptions`, `PortableDetectorProfile`, and `PortableDetectorProfiles` describe and instantiate DEIMv2, RF-DETR detection/segmentation, RT-DETR, and PP-YOLOE artifact contracts. / 这些类型描述并实例化 DEIMv2、RF-DETR 检测/分割、RT-DETR 与 PP-YOLOE 的工件合同。
- `PortableDetectorDecoder` and `RFDETRInstanceSegmentationDecoder` decode bounded, named Core tensors into the existing detection and instance-segmentation results. / 这两个 Decoder 将有界、具名 Core tensor 解码为既有检测和实例分割结果。
- `OpenCvPortableDetectorPreprocessing.CreateOptions` and `CreateFromFile` map a portable profile to RGB Float32 NCHW image preparation and exact auxiliary geometry tensors. / 这两个 OpenCV API 将 portable Profile 映射为 RGB Float32 NCHW 图像准备和精确辅助几何 tensor。
- `OpenCvLetterboxRounding` and `OpenCvPreprocessOptions.LetterboxRounding` make letterbox dimension rounding explicit; DEIMv2 selects `Floor`. / `OpenCvLetterboxRounding` 与 `OpenCvPreprocessOptions.LetterboxRounding` 显式表达 letterbox 尺寸舍入；DEIMv2 使用 `Floor`。

## Compatibility and status / 兼容性与状态

Existing Core, backend registry, `VisualPipeline`, result DTOs, ModelPack schema and ModelFactory catalog schema are unchanged. The new APIs are additive. A generic RF-DETR profile uses every foreground logit column by default; the optional `rfDetrIncludesNoObjectClass` is artifact-bound and must be set only for an exporter that explicitly has that column. / 既有 Core、后端注册表、`VisualPipeline`、结果 DTO、ModelPack schema 和 ModelFactory catalog schema 均未改变。新 API 为增量。通用 RF-DETR Profile 默认使用全部前景 logit 列；可选的 `rfDetrIncludesNoObjectClass` 与工件绑定，只有导出器明确有该列时才可设置。

All five candidate manifests remain External and are not part of the embedded official catalog. See the [portable detector guide](visual-portable-detectors.md) for exact contracts and backend blockers. / 五个候选清单均保持 External，未进入内置官方 catalog。精确合同与后端阻断原因请参阅[便携检测器指南](visual-portable-detectors.md)。
