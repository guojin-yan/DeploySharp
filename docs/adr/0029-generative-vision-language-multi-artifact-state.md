# ADR 0029: Generative vision-language multi-artifact state / 生成式视觉语言多工件状态

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-09

BLIP, BLIP-2, and InstructBLIP combine processors, tokenizers, vision encoders, optional Q-Former/query tokens/projection, language models, prompt templates, generation policy, and optional KV state. Similar ranks do not establish compatibility. A filename or hidden-size guess cannot select graph ports, token IDs, stopping rules, or cache layout. / 三个模型族由多个组件和状态组成；相似 Rank 不代表兼容，文件名或 Hidden Size 不能决定端口、Token、停止或 Cache 合同。

One immutable Profile therefore binds all component identities, exact named tensors, capacities, upstream/export/license facts, prompt/tokenizer/generation/KV rules, and executable/blocker status. One stateful Session owns backend sessions and one image state bound to the complete artifact set and source SHA. KV reuse, when a future executable Profile provides it, must additionally bind prompt, tokenizer, generation config, and step identity. / 单一不可变 Profile 绑定全部组件、端口、容量、供应链和生成规则；Session 拥有 Backend 与完整 Identity 绑定的图像状态。未来 KV 还必须绑定 Prompt、Tokenizer、生成配置与 Step。

The first executable Profile uses official BLIP full-prefix greedy generation with no reusable KV. Dynamic backend metadata may be refined by fixed Profile dimensions, but concrete runtime tensors remain strict. Incomplete BLIP VQA/BLIP-2/InstructBLIP paths are explicit blockers; they cannot fall back to Python, remote services, another checkpoint, or fixed output. Model publication is independent of runtime support and remains blocked while redistribution is false. / 首个可执行 Profile 使用无 KV 的完整前缀 Greedy；Profile 可收紧动态元数据，但真实张量严格校验。不完整路径只记录 blocker；模型发布与运行支持分离，再分发为 false 时禁止发布。
