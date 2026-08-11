# Stage 17 API changes / 阶段 17 API 变更

Stage 17 adds a complete YOLO multitask model family to `JYPPX.DeploySharp.Visual` without creating a vendor-specific Ultralytics package. / 阶段 17 在 `JYPPX.DeploySharp.Visual` 中加入完整 YOLO 多任务模型族，不创建厂商特定的 Ultralytics 包。

## Public additions / 公共新增

- `YoloMultiTaskProfile`, `YoloMultiTaskProfiles`, `YoloPackedProfileOptions`, and `YoloClassificationProfileOptions` bind one exact artifact to its upstream commit, exporter version, opset, tensor names, shape, SHA256, preprocessing, and postprocessing semantics. / 这些类型将精确工件与上游提交、导出器版本、opset、张量名称、形状、SHA256、前处理和后处理语义绑定。
- `YoloInstanceSegmentationDecoder`, `YoloPoseDecoder`, and `YoloObbDecoder` implement official packed-output decoding, source-coordinate restoration, deterministic NMS/OKS suppression, and owned result data. / 三个解码器实现官方打包输出解码、源图坐标恢复、确定性 NMS/OKS 抑制及自有结果数据。
- `YoloPoseTopologies.Coco17` exposes the official COCO-17 keypoint topology and OKS metadata. / `YoloPoseTopologies.Coco17` 暴露官方 COCO-17 关键点拓扑和 OKS 元数据。
- `OpenCvYoloPreprocessing.CreateOptions(YoloMultiTaskProfile)` maps the profile's center-crop or letterbox contract to the image-library adapter. / 该重载把 Profile 的中心裁剪或 letterbox 契约映射到图像库适配器。

## Compatibility and status / 兼容性与状态

The implementation reuses `VisualPipeline`, generic Vision result types, `PreparedVisualInput`, and existing ONNX Runtime/OpenVINO backend contracts. Core, ModelPack, and ModelFactory public schemas are unchanged; the embedded official catalog remains empty. / 实现复用 `VisualPipeline`、通用视觉结果类型、`PreparedVisualInput` 以及现有 ONNX Runtime/OpenVINO 后端契约。Core、ModelPack 和 ModelFactory 公共 Schema 未改变，内置官方目录继续为空。

All twelve remaining V1 rows are locally verified with the exact artifacts listed in the multitask guide on ONNX Runtime CPU and OpenVINO CPU. These are contract and local-backend results, not a redistribution or production accuracy claim. / V1 剩余十二行均使用多任务指南列出的精确工件，在 ONNX Runtime CPU 和 OpenVINO CPU 上完成本地验证。这些是契约和本地后端结果，不构成再分发许可或生产精度声明。
