# Stage 24 API changes / 阶段 24 API 变更

- Added immutable `VisionLanguageEmbeddingProfile`, `VisionLanguageArtifactContract`, `VisionLanguageArtifactBundle`, `VisionLanguageTokenizerContract`, and exact CLIP/SigLIP/SigLIP 2 factory Profiles. / 新增不可变、工件绑定的双编码器与 Tokenizer 合同及三个模型族 Profile。
- Added owned `TextTokenBatch`, `VisionLanguageImageEmbedding`, `VisionLanguageTextEmbedding`, `VisionLanguageScoreMatrix`, canonical-classification wrapper, retrieval match, label-template provenance, and deterministic `VisionLanguageScorer`. / 新增自有 Token/Embedding/评分/检索与模板来源结果，并复用规范 `ClassificationResult`。
- Added `VisionLanguageEmbeddingSession` with two-session ownership, dynamic-batch validation, exact named ports, atomic caches, async/cancel/timeout/concurrency/dispose rules, and stable `DS-VISUAL-4701` through `4705`. / 新增双 Session 生命周期、动态批次、精确端口、原子缓存及稳定错误码。
- Added `OpenCvInterpolation`, shortest-edge center crop, and `OpenCvVisionLanguageInputFactory`; Pillow/OpenCV bicubic differences are documented, not hidden. / 新增显式插值、最短边中心裁剪与 VLM OpenCV Factory，并公开 Pillow/OpenCV 差异。
- ModelFactory artifacts now bind embedding dimension, image preprocessing, projection, normalization, score semantics, language, and resolution. Single-artifact and bundle queries expose tokenizer/language/resolution/score filters; bundle validation rejects mixed values. / ModelFactory 工件新增上述 Identity；单工件与 Bundle 查询公开 Tokenizer/语言/分辨率/评分筛选并拒绝混配。

No existing public type was removed. There is no V1 compatibility shim, single-model package, bundled tokenizer/model/native runtime, official-catalog admission, or TensorRT implementation. / 未删除既有公共类型；不提供 V1 Shim、单模型包、内置 Tokenizer/模型/Native Runtime、official catalog 准入或 TensorRT。
