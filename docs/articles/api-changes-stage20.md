# Stage 20 API changes / 阶段 20 API 变更

Stage 20 adds PaddleOCR two-class text-line orientation profiles and an optional classifier stage to the existing OCR contracts. Changes are additive except that `OcrOrientationSchema` now deliberately supports either two or four declared classes instead of requiring exactly four. / 阶段 20 在既有 OCR 合同上增加 PaddleOCR 二类文本行方向 Profile 与可选分类阶段。除 `OcrOrientationSchema` 从仅四类扩展为显式二类或四类外，变更均为增量。

## Public additions / 公共新增

- `OcrOrientationStrategy` explicitly distinguishes no orientation, whole-image orientation, and per-text-region orientation. / 显式区分无方向、整图方向与逐文本行方向。
- `OcrOrientationRejectionPolicy` chooses stable failure or explicit zero-degree fallback for rejected region classifications. / 对拒绝的区域分类选择稳定失败或显式零度回退。
- `OcrPipelineStage.OrientationClassification` identifies three-stage failures without collapsing them into crop or recognition failures. / 独立标识三阶段中的分类错误。
- The new `OcrPipeline` overload owns detector, region classifier, and recognizer pipelines and records per-region orientation provenance. / 新重载拥有检测、逐区域分类与识别 pipeline，并记录区域方向来源。
- `OcrStageTiming.OrientationClassification` reports the accumulated classifier duration, and `Total` includes it. / 报告累计分类耗时，并计入总耗时。
- `PaddleOcrFamily.PaddleOcrCls`, `PaddleOcrProfiles.CreateLegacyClassification`, and `CreateTextLineOrientationClassification` bind the exact legacy and PP-LCNet contracts. / 绑定精确 legacy 与 PP-LCNet 分类合同。
- `OpenCvStage19Preprocessing.CreatePaddleOcrLegacyClassificationOptions` and `CreatePaddleOcrTextLineOrientationOptions` create the corresponding BGR/RGB prepared tensors. / 创建对应 BGR/RGB prepared tensor。

## Compatibility / 兼容性

The existing two-model `OcrPipeline` constructor remains the no-orientation path. Existing four-class `OcrOrientationWorkflow` behavior is retained as whole-image orientation. Results continue to use `OcrResult`, `OcrRegionResult`, `TextRegion`, and `OcrOrientationResult`; no duplicate Paddle-specific result or rotation implementation was added. / 既有双模型构造函数仍是无方向路径，既有四方向 workflow 保持整图方向行为。结果继续复用现有通用类型，没有新增 Paddle 专用结果或旋转实现。

Classifier candidates are External only. See [the three-model guide](visual-paddle-ocr3.md) for exact labels, preprocessing, rejection behavior, local backend evidence, native ownership, and blockers. / 分类器候选仅为 External；精确标签、前处理、拒绝行为、本地后端证据、native 所有权与 blocker 参阅[三模型指南](visual-paddle-ocr3.md)。
