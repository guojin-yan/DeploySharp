# ADR 0030: Native multimodal Prefill/KV state / 原生多模态 Prefill/KV 状态

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-09

Native multimodal models combine image processors, variable crop grids, vision towers, projectors/resamplers, image sentinels, chat templates, tokenizers, embeddings, position/attention rules, language decoders, and mutable KV caches. Similar tensor rank or a familiar family name cannot establish compatibility. / 原生多模态模型组合大量子组件与可变状态；相似 Rank 或模型族名称不能证明兼容。

DeploySharp therefore uses one immutable artifact-bound `NativeMultimodalProfile`, exact role bundle, typed single-source prepared image/token contracts, and a single-writer `NativeMultimodalSession`. The Session owns three exact named backend sessions and one source/Profile/artifact-bound image state. Generate owns transient KV tensors; only an immutable summary is published, so one request cannot reuse stale Prompt/Image/Layout/Generation state. / 因此使用不可变 Profile、精确角色 Bundle、typed 单一来源输入与 Single-writer Session。Generate 拥有临时 KV，仅公开不可变摘要，避免复用失配状态。

The first executable Profile is explicitly single-image LLaVA OneVision. It does not generalize to Qwen Vision RoPE, Phi HD transforms, multi-image ordering, video, regions, tools, Beam, or JSON mode. Incomplete official families are blockers. Runtime support and model publication are independent; successful local CPU execution does not authorize uploading or `AlgorithmVerified`. / 首个可执行 Profile 仅为单图 LLaVA OneVision，不泛化到其他家族/能力。不完整路径保持 blocker；本机运行成功不等于发布授权或 AlgorithmVerified。
