# ADR 0031: Document layout, schema, and generation state / 文档版面、Schema 与生成状态

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-10

Document models can look similar while owning fundamentally different inputs. LayoutLMv3 consumes caller OCR words, normalized boxes, token-word alignment, pixels, and attention. Donut owns OCR-free pixels, a task prompt, autoregressive CORD tags, and self/cross KV. Pix2Struct owns OCR-free dynamic flattened patches with row/column coordinates and a T5 decoder whose audited official configuration disables cache. Tensor rank or family name cannot establish compatibility. / 文档模型即使 Rank 相似，OCR、Box、Patch、Prompt、Schema 与 KV 所有权也完全不同，不能按名称推断兼容。

DeploySharp therefore uses one immutable artifact-bound `DocumentUnderstandingProfile` and role bundle, typed single-source page/document contracts, one bounded structured-result tree, and one single-writer `DocumentUnderstandingSession`. Profile identity includes exact artifacts, Processor, Tokenizer, OCR ownership, Schema, page/token/patch capacities, named ports, and KV schema. The Processor derives each pixel/layout/patch field once; Backends only execute named tensors. / 因此采用统一不可变 Profile/Bundle、Typed 单一来源输入、容量受限结果树与 Single-writer Session；Processor 只派生一次，Backend 只执行具名张量。

The Session owns its child backend sessions and an encoded state bound to ordered source pages and the complete identity above. Prepared documents, tokenizer, registry, and external assets remain caller-owned. Mutable KV is request-local; results own raw tokens/text, parse status, schema/field provenance, JSON, timing, and an immutable KV summary. Set, Generate, Clear, cancellation, callback failure, and Dispose publish state atomically. / Session 拥有子 Session 与完整 Identity 绑定的编码状态；可变 KV 仅存在于单次请求，结果自有。所有状态变更原子发布。

The first executable profile is explicitly single-page Donut CORD-v2 with Greedy generation and bounded balanced-tag parsing. It does not generalize to arbitrary JSON repair, Beam/sampling, multi-page merging, caller OCR injection, LayoutLMv3 task heads, or Pix2Struct dynamic export. Incomplete official families remain blockers. Successful local ORT/OpenVINO execution does not authorize publication or `AlgorithmVerified`. TensorRT remains outside the decision. / 首个可执行 Profile 仅为单页 Donut CORD-v2；不泛化到未验证能力。阻断家族不借用替代实现，本机成功不等于发布授权或 AlgorithmVerified，TensorRT 不在本决策范围。
