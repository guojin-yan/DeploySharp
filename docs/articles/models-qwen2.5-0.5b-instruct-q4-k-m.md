# Qwen2.5 0.5B Instruct Q4_K_M alpha preview

The first DeploySharp ModelFactory release asset is the Qwen2.5 0.5B Instruct Q4_K_M GGUF. It is distributed as an alpha preview for `LLamaSharp` consumers; native runtimes remain application-owned.

## Source and integrity

- Upstream: `Qwen/Qwen2.5-0.5B-Instruct-GGUF`
- Immutable upstream revision: `9217f5db79a29953eb74d5343926648285ec7e67`
- Upstream license: `Apache-2.0`; the upstream `LICENSE` is included with the release assets.
- Model file: `qwen2.5-0.5b-instruct-q4_k_m.gguf`
- Size: `491400032` bytes
- SHA-256: `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db`

The ModelFactory catalog verifies the exact size and SHA-256 of every downloaded file before making the model available. The release also contains the ModelPack manifest, tokenizer/configuration sidecars, upstream license, and upstream GGUF README.

## Runtime

Install `JYPPX.DeploySharp.Backend.LlamaSharp` and the application-owned `LLamaSharp.Backend.Cpu` runtime. Select this alpha-preview entry with `includePreview: true`; the ModelFactory client then downloads and verifies the complete bundle into its content-addressed cache.

This record means the bundle has source, file-integrity, and local CPU runtime evidence. It does not claim universal quality or benchmark parity for every prompt, hardware target, or native runtime build.
