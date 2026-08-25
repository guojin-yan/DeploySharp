# Stage 23 API changes / 阶段 23 API 变更

- Visual adds immutable vocabulary/token/embedding/artifact identities, `OpenVocabularyDetectionProfile`, executable fixed YOLO-Worldv2 plus honest Grounding DINO/MMYOLO/YOLOE blockers, and a provenance wrapper over existing `DetectionResult`. / Visual 新增完整开放词汇 Identity/Profile、可执行固定 YOLO-Worldv2、真实 blocker 及复用 DetectionResult 的来源扩展。
- `GroundedSamPreparedInput` and `GroundedSamImageSession` add single-decode, atomic set-image, sync/async box composition, cancellation, clear/reset, single-writer concurrency, owned results and deterministic disposal. / 新增单次解码、原子状态、同步/异步组合、取消、Reset、并发与释放合同。
- Visual.OpenCV adds `OpenCvOpenVocabularyInputFactory`, reusing existing YOLO letterbox and SAM longest-side implementations. / OpenCV 新增输入工厂并复用既有前处理。
- ModelFactory artifact JSON/schema and queries add `tokenizerId` and `vocabularyMode`; bundles reject mixed tokenizer/mode, roles, sidecars, versions and conversions. / ModelFactory 新增 Tokenizer/词汇模式查询与混合 Bundle 拒绝。
- Stable errors `DS-VISUAL-4601` through `4605` map contract, capacity, state, identity and concurrency. No single-model package, Python fallback, official catalog entry, TensorRT, or Release asset was added. / 新增稳定错误；未新增单模型包、Python 回退、目录条目、TensorRT 或 Release 资产。
