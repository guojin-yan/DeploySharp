# Stage 29 API changes / 阶段 29 API 变更

- Added immutable `LanguageModelProfile` and `LanguageModelBundle` in `JYPPX.DeploySharp.LLM`.
- Added `LanguageModelMetadata.Profile` as an optional artifact-bound profile; LLamaSharp supplies an explicit unverified profile for caller-owned GGUF files.
- Added `DS-LLM-4004` (`LanguageModelSessionBusy`) for single-writer contention and `DS-LLM-4005` (`LanguageModelBundleMismatch`) for mixed bundle identities.
- LLamaSharp stream cancellation and timeout now emit a terminal `GenerationChunk` with `GenerationFinishReason.Cancelled`; concurrent operations are rejected instead of queued.

No LLamaSharp, native runtime, tokenizer, model file, or Python type crosses Core/LLM public contracts. Existing chat, generation, streaming, embedding, and prompt formatter APIs remain source-compatible.
