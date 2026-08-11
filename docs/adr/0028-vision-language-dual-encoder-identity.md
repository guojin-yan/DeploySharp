# ADR 0028: Vision-language dual-encoder identity / 视觉语言双编码器 Identity

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-09

CLIP and SigLIP expose similar ranks but differ in tokenizer ports, pooling, projection dimension, logit scale/bias, and probability ownership. SigLIP 2 cannot inherit either contract. Therefore filenames and ranks cannot select a decoder or scoring rule. / CLIP 与 SigLIP 的 Rank 相似，但 Tokenizer 端口、池化、投影维度、Scale/Bias 和概率所有权不同；SigLIP 2 也不能继承旧合同。因此不得按文件名或 Rank 选择合同。

One immutable Profile binds both artifacts, exact ports, tokenizer SHA, preprocessing, projection, L2 normalization, score semantics, capacities, provenance, license, opset, size, and SHA. The session owns both backend sessions and only publishes defensive embeddings whose identity includes the complete artifact set. Candidate-set normalization belongs to `VisionLanguageScorer`, not a backend. ModelFactory rejects mixed bundle fields. / 单一不可变 Profile 绑定双工件、精确端口、Tokenizer、前处理、投影、归一化、评分、容量与供应链；Session 拥有两条 Backend Session，只发布完整工件集绑定的防御性 Embedding；候选集归一化由 Scorer 拥有，ModelFactory 拒绝混配。

OpenCV geometry is implemented, but its bicubic kernel is not represented as Pillow-exact. Exact backend fidelity uses official Pillow pixel goldens and the discrepancy remains visible. SigLIP 2 stays External until a complete official native export is audited. No Python daemon, model redistribution, catalog promotion, or TensorRT fallback is accepted. / OpenCV 已实现几何，但不冒充 Pillow 逐像素一致；精确后端保真使用官方 Pillow Golden 并公开差异。SigLIP 2 在完整官方 Native 导出审计前保持 External；不接受 Python 守护进程、模型再分发、目录晋级或 TensorRT 回退。
