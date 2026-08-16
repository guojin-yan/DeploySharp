# Official model catalog / 官方模型目录

This table is generated from `src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json`. Do not edit table rows by hand. / 本表由 `src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json` 生成，请勿手工编辑表格行。

| ModelId | Algorithm / Task | Artifact | Format | Backend | Precision / Quantization | Portable | Release tag | Size | SHA256 | Download | Test input | License |
|---|---|---|---|---|---|---|---|---:|---|---|---|---|
| llm/qwen2.5-0.5b-instruct-q4-k-m | qwen2.5 / language-generation-and-embedding | qwen2.5-0.5b-instruct.q4-k-m.gguf | gguf | llamasharp | mixed-integer / q4-k-m | True | models-20260817.qwen2.5-0.5b-instruct-q4-k-m.1 | 491400032 | 74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db | [manifest](https://github.com/guojin-yan/DeploySharp/releases/download/models-20260817.qwen2.5-0.5b-instruct-q4-k-m.1/qwen2.5-0.5b-instruct-q4-k-m.modelpack.json) | — | Apache-2.0 |

The catalog lists only models actually published in an immutable GitHub Release with source, license, exact size, and SHA-256 metadata. Preview entries require an explicit `includePreview: true` query. / 目录仅列出已在不可变 GitHub Release 中实际发布，且带有来源、许可证、精确大小与 SHA-256 元数据的模型。预览条目须在查询中显式设置 `includePreview: true`。
