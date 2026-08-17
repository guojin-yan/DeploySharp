# Published vision model collection / 已发布视觉模型集合

DeploySharp publishes the first reviewed vision assets under one immutable GitHub prerelease instead of creating one Release per model. All entries use tag `models-20260817.vision.1`, fixed source commit `93947165a7d6bd474b4acfc30b18ca38e4dd468c`, and independent ModelPacks, licenses, file paths, sizes, and SHA-256 hashes. / DeploySharp 将首批审核后的视觉资产放在同一个不可变 GitHub 预发布中，不再为每个模型分别创建 Release。三个条目共享同一 tag 与源提交，但各自保留独立 ModelPack、许可证、文件路径、大小和 SHA-256。

| ModelFactory ID | Purpose / 用途 | Runtime assets / 运行资产 | License |
| --- | --- | --- | --- |
| `vision-language/clip-vit-b-32` | image/text embeddings, zero-shot classification, cross-modal retrieval / 图文嵌入、零样本分类、跨模态检索 | image encoder, text encoder, tokenizer and preprocessing sidecars / 图像编码器、文本编码器、分词与预处理文件 | MIT |
| `segmentation/sam-v1-vit-b` | point, box, mask-feedback, and multimask segmentation / 点、框、Mask 反馈与多 Mask 分割 | image encoder and prompt-mask decoder / 图像编码器与提示 Mask 解码器 | Apache-2.0 |
| `generative-vision-language/blip-caption-base` | image captioning / 图像描述 | vision encoder, full-prefix language decoder, vocabulary and generation sidecars / 视觉编码器、全前缀语言解码器、词表与生成配置 | BSD-3-Clause |

The collection contains 19 catalog assets totaling `1,975,410,631` bytes. `SHA256SUMS` is an additional human-verification asset and covers all 19 catalog files. ModelFactory downloads only the assets for the selected entry and materializes each file at its catalog `relativePath`; sharing a Release does not require downloading the other two models. / 集合包含 19 个目录资产，总计 `1,975,410,631` 字节；额外的 `SHA256SUMS` 覆盖全部目录文件。ModelFactory 只下载所选条目的资产，并按目录中的 `relativePath` 落盘；共享 Release 不会强制下载另外两个模型。

```csharp
ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
ModelSelection clip = ModelCatalogQuery.Select(
    catalog,
    new ModelQuery(
        modelId: "vision-language/clip-vit-b-32",
        backend: "onnxruntime",
        format: "onnx",
        includePreview: true)).Single();
```

These records are alpha-preview distribution entries. They preserve the recorded local ONNX Runtime/OpenVINO and official-golden evidence, but they are not promoted to `AlgorithmVerified`, do not include test images, and do not imply support for other family members such as SigLIP, SAM 2/3, BLIP VQA, BLIP-2, or InstructBLIP. / 这些条目属于 alpha-preview 分发记录，保留已记录的 ORT/OpenVINO 与官方 Golden 证据，但不提升为 `AlgorithmVerified`，不包含测试图片，也不代表支持 SigLIP、SAM 2/3、BLIP VQA、BLIP-2 或 InstructBLIP。
