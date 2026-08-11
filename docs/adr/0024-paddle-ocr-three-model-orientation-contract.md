# ADR 0024: PaddleOCR three-model orientation contract / PaddleOCR 三模型方向合同

- Status: Accepted for alpha.1 / 状态：alpha.1 接受
- Date: 2026-08-08
- Scope: PaddleOCR text-line classification and detector-classifier-recognizer ownership / 范围：PaddleOCR 文本行分类与检测-分类-识别所有权

## Decision / 决策

PaddleOCRCls is implemented as immutable artifact-bound `PaddleOcrProfile` instances in the existing Visual assembly. The profile binds input/output names, ordered labels and angles, rejection threshold, size, color order, normalization, opset, SHA256, exporter/upstream revision, license/provenance strings, and fixed capacity. The legacy BGR `[1,3,48,192]` and PP-LCNet RGB `[1,3,80,160]` contracts remain distinct. / PaddleOCRCls 在现有 Visual 程序集中实现为不可变、工件绑定 Profile，绑定全部名称、标签/角度、阈值、尺寸、颜色、归一化、opset、SHA、来源/许可证与容量。legacy BGR 与 PP-LCNet RGB 合同保持独立。

Direction semantics are an explicit strategy. The old pipeline means no classifier; `OcrOrientationWorkflow` means whole-image correction; the new three-model pipeline means per-region classification. A region classification is produced from a classifier-owned crop, then only its managed orientation/provenance crosses into recognition. Recognition creates its own corrected crop from the source image. Source polygons remain unchanged and owned by the final result. / 方向语义由显式策略决定：旧 pipeline 无分类，workflow 为整图纠正，新三模型 pipeline 为逐区域分类。分类 crop 由分类阶段拥有，只有托管方向与来源进入识别；识别重新从源图创建纠正 crop。源图 polygon 不变并由最终结果拥有。

Two-class and four-class orientation schemas share one validated decoder/result contract. Two-class schemas must contain 0 and 180 degrees; four-class mappings remain explicit. Document orientation, generic four-direction fixtures, and text-line orientation cannot be silently exchanged. Rejection is either an orientation-stage failure or an explicitly recorded zero-degree fallback. / 二类与四类方向 Schema 复用同一验证 Decoder/结果合同。二类必须包含 0 与 180，四类映射仍显式。文档方向、通用四方向 fixture 与文本行方向不得静默互换；拒绝只能成为分类阶段错误或被显式记录的零度回退。

## Consequences / 后果

- Cancellation, bounded concurrency, single decode, crop/session ownership, disposal, source restoration, and stable stage errors remain in the common OCR pipeline. / 取消、有界并发、单次解码、crop/session 所有权、释放、源图恢复与稳定阶段错误保留在通用 OCR pipeline。
- No `Visual.PaddleOCR`, single-model NuGet, duplicate result/rotation implementation, bundled artifact, TensorRT backend, or official catalog admission is created. / 不新增厂商包、单模型包、重复结果/旋转、内置工件、TensorRT 或官方 catalog 准入。
- ORT/OpenVINO local evidence proves the declared execution contract only; exact exporter/checkpoint provenance, official predictor goldens, licensing, and redistribution approval remain release blockers. / ORT/OpenVINO 本地证据仅证明声明的执行合同；精确来源、官方 golden、许可证与再分发批准仍是发布 blocker。
