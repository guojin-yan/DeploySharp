# ADR 0033: LLM/GGUF source-contract admission / LLM/GGUF 来源合同准入

## Status

Accepted for Stage 29. The executable GGUF decision is deferred.

## Decision

DeploySharp keeps the LLM contract backend-neutral and binds reproducibility metadata through `LanguageModelProfile`. Bundles must share model, version, quantization, tokenizer, chat-template, generation, context, and backend identities. LLamaSharp remains an application-selected adapter and never supplies a model license or release asset.

Because no exact authorized GGUF was available on 2026-08-10, the Stage 29 manifest is an external source-contract blocker. The official catalog remains empty and all upload/download flags remain false.

## Consequences

The clean consumer has deterministic `DEPLOYSHARP_LLAMA_CONSUMER_SKIP` and an environment-gated real path. A future executable promotion must replace the blocker metadata with an exact model file and independent runtime evidence; contract tests alone cannot promote it.
