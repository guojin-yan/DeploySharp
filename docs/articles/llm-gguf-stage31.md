# Authorized Qwen GGUF CPU evidence / 授权 Qwen GGUF CPU 实证

Stage 31 admits one exact, user-authorized local GGUF through the Stage 30 read-only gate. The selected file is `E:\DeploySharp-Models\qwen2.5-0.5b-instruct-q4_k_m\qwen2.5-0.5b-instruct-q4_k_m.gguf`, sourced from Qwen's immutable Hugging Face revision `9217f5db79a29953eb74d5343926648285ec7e67`. It is 491,400,032 bytes with SHA256 `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db` and Apache-2.0 license evidence. / 阶段 31 通过阶段 30 只读门禁准入一个用户明确授权的本地 GGUF。文件路径、固定 revision、大小、哈希与许可证证据均如上。

## Admission sequence / 准入顺序

Before native loading, `Test-GgufAdmission.ps1` verified the absolute path, file size/SHA256, GGUF magic, immutable source/revision, license, publisher-declared Q4_K_M/context, upstream tokenizer/chat-template/generation identities, and the managed/native runtime versions. It returned `DEPLOYSHARP_LLAMA_ADMISSION_READY` with only runtime evidence fields missing. After the real CPU matrix produced hash-protected evidence and the exact Manifest was updated, `-RequireAdmitted` returned `DEPLOYSHARP_LLAMA_ADMISSION_ADMITTED ... missing=none`. / 原生加载前先得到 `READY`；真实 CPU 矩阵生成哈希保护证据并回写 Manifest 后，`-RequireAdmitted` 返回 `ADMITTED` 且无缺失字段。

## Exact runtime identity / 精确运行时身份

LLamaSharp runtime metadata confirmed GGUF V3, architecture `qwen2`, Q4_K Medium, context 32,768, 630,167,424 parameters, embedding length 896, vocabulary size 151,936, tokenizer model `gpt2` with Qwen2 pre-tokenization, BOS 151643, EOS 151645, PAD 151643, and the embedded Qwen ChatML template. Generation used a 512-token CPU context and zero GPU layers. The caller-owned runtime is `LLamaSharp.Backend.Cpu 0.27.0`, llama.cpp revision `3f7c29d318e317b63f54c558bc69803963d7d88c`, Windows x64 AVX2. / LLamaSharp 运行时从精确 GGUF 读取并确认上述身份；测试上下文为 512，GPU layer 为 0，原生运行时由测试项目显式持有。

## Real operation matrix / 真实操作矩阵

The environment-gated test executed real CPU generation rather than fixed output or a proxy. Generate returned non-empty text, Stream emitted ordered chunks and a terminal result, cancellation occurred after generation began and terminated with `Cancelled`, Repeat produced the same hash for the same prompt/seed/options, a concurrent writer was rejected with `DS-LLM-4004`, Dispose was idempotent and guarded later use, and Embedding returned a normalized 896-dimensional vector. The exact results and loaded native DLL hashes are retained at `E:\DeploySharp-Models\qwen2.5-0.5b-instruct-q4_k_m\evidence\deploysharp-stage31-runtime.json` (7,364 bytes, SHA256 `68f2b1e144c3d4537cb2f7c91473554296bda97a52bc5e5b5e9517dfb0dfc973`). / 环境门控测试运行真实 CPU 推理，覆盖生成、流式、运行中取消、重复、单写入冲突、释放与 Embedding；证据路径、大小和哈希如上。

## Admission boundary / 准入边界

The exact Manifest is `eng/models/llm/manifests/qwen2.5-0.5b-instruct-q4-k-m.modelpack.json`. It remains External, `portable:false`, `redistributionAllowed:false`, `AlgorithmVerified=false`, `uploaded:false`, and `downloadable:false`. Stage 31 does not add the model to the official catalog, does not publish a package or Release asset, and does not claim benchmark or official-output fidelity. / 精确 Manifest 仍为 External；本阶段不进入 official catalog、不发布、不上传，也不声称算法或官方输出保真。
