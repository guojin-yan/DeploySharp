# Stage 30 API changes / 阶段 30 API 变更

Stage 30 adds no public .NET API and makes no ModelFactory catalog change. The new `eng/models/llm/Test-GgufAdmission.ps1` is a repository-owned, read-only admission command; it checks exact GGUF evidence before any environment-gated model execution and does not load a model itself. / 阶段 30 不新增公共 .NET API，也不更改 ModelFactory catalog。新增的 `eng/models/llm/Test-GgufAdmission.ps1` 是仓库内只读准入命令；它在任何环境门控模型执行前核查精确 GGUF 证据，且自身不会加载模型。

Existing LLM contracts, `DS-LLM-4004`, `DS-LLM-4005`, caller-owned native runtime ownership, and the stable clean-consumer missing-model marker are unchanged. / 既有 LLM 合同、`DS-LLM-4004`、`DS-LLM-4005`、调用方持有原生运行时，以及 clean-consumer 稳定的缺模型标记均不变。
