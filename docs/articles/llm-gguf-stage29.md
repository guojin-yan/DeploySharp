# LLM and GGUF Stage 29 / LLM 与 GGUF 阶段 29

Stage 29 closes the backend-neutral language-model contract without declaring an unreviewed GGUF executable. `LanguageModelProfile` binds the Core artifact to model version, quantization, tokenizer, chat-template, generation, context, embedding, backend, and license identities. `LanguageModelBundle` rejects mixed identities with `DS-LLM-4005`.

`ILanguageModelSession` remains the only public execution boundary. LLamaSharp types stay inside `JYPPX.DeploySharp.Backend.LlamaSharp`; generated text, chunks, token usage, and embeddings are copied into DeploySharp result DTOs. A session is single-writer: a concurrent operation is rejected with `DS-LLM-4004`, while cancellation and timeout produce a terminal `Cancelled` chunk.

The current runtime profile is explicitly `caller-owned-unverified` because no exact GGUF exists in `E:\DeploySharp-Models` and `DEPLOYSHARP_LLAMA_MODEL` is unset. The source-contract manifest records that blocker and the hash of its evidence file; it does not invent a model size, GGUF SHA256, quantization, tokenizer, chat template, or license.

The supported surface is text generation, streaming, cancellation, plain-text chat formatting, and optional LLamaSharp embeddings. Beam search, tool calling, JSON schema, speculative decode, LoRA, GPU/TensorRT promotion, and model routing remain out of scope.
