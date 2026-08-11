# Stage 19 API changes / 阶段 19 API 变更

Stage 19 adds PaddleOCR detection/recognition, Anomalib export, and BRIA alpha contracts to the existing Visual packages. All additions are backend-neutral and additive. / 阶段 19 在现有 Visual 包中加入 PaddleOCR 检测/识别、Anomalib 导出和 BRIA alpha 合同。全部新增均为后端无关的增量 API。

## Public additions / 公共新增

- `PaddleOcrArtifactContract`, `PaddleOcrProfile`, `PaddleOcrProfiles`, `PaddleDbPostprocessOptions`, and `PaddleDbTextDetectionDecoder` bind PP-OCR named tensors, dictionary evidence, bounded DB thresholds/unclip, and source geometry. / 这些类型绑定 PP-OCR 具名 tensor、字典证据、有界 DB threshold/unclip 与源图几何。
- `OcrCharacterSet` now accepts ordered non-empty Unicode tokens as well as scalar sequences, allowing official multi-scalar dictionary entries while retaining validation and canonical SHA256. / `OcrCharacterSet` 现在同时接受有序非空 Unicode token 与标量序列，支持官方多标量字典条目，并保留验证与规范 SHA256。
- `AnomalibArtifactContract`, `AnomalibProfile`, `AnomalibProfiles`, and `AnomalibExportDecoder` validate the exact four-output export and reuse `AnomalyDetectionResult`. / 这些 API 验证精确四输出导出并复用 `AnomalyDetectionResult`。
- `AlphaMask`, `BackgroundRemovalResult`, `AlphaMattingDecoder`, `BriaRmbgProfileOptions`, `BriaRmbgProfile`, and `BriaRmbgProfiles` represent owned semantic alpha output, source restoration, composition, provenance, and deterministic alpha SHA256. / 这些 API 表达自有语义 alpha 输出、源图恢复、合成、来源与确定性 alpha SHA256。
- `OpenCvStage19Preprocessing` creates exact PaddleOCR, Anomalib, RMBG 1.4, and artifact-bound RMBG 2.0 image preparation contracts. / `OpenCvStage19Preprocessing` 创建精确 PaddleOCR、Anomalib、RMBG 1.4 与工件绑定 RMBG 2.0 图像准备合同。

## Compatibility and status / 兼容性与状态

Core gains no OpenCV, Paddle, PyTorch, Anomalib, BRIA, ONNX Runtime, or OpenVINO dependency. Existing OCR/anomaly/geometry/pipeline results remain authoritative; no duplicate result DTO is introduced. Profiles reject incompatible tensor names and shapes rather than guessing a family from rank. / Core 不新增 OpenCV、Paddle、PyTorch、Anomalib、BRIA、ONNX Runtime 或 OpenVINO 依赖。既有 OCR、异常、几何与 pipeline 结果仍是权威类型，不新增重复 DTO。Profile 拒绝不兼容的 tensor 名称与 shape，不根据 rank 猜测模型族。

All Stage 19 candidates remain External and outside the official catalog. See [the Stage 19 guide](visual-ocr-anomaly-rmbg.md) for exact contracts, tolerances, native ownership, and blockers. / 阶段 19 全部候选保持 External，不进入官方 catalog。精确合同、容差、native 所有权和 blocker 参阅[阶段 19 指南](visual-ocr-anomaly-rmbg.md)。
