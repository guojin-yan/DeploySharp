# Published vision model collection / 已发布视觉模型集合

DeploySharp publishes the first reviewed vision assets under one immutable GitHub prerelease instead of creating one Release per model. All entries use tag `models-20260817.vision.1`, fixed source commit `93947165a7d6bd474b4acfc30b18ca38e4dd468c`, and independent ModelPacks, licenses, file paths, sizes, and SHA-256 hashes. / DeploySharp 将首批审核后的视觉资产放在同一个不可变 GitHub 预发布中，不再为每个模型分别创建 Release。六个条目共享同一 tag 与源提交，但各自保留独立 ModelPack、许可证、文件路径、大小和 SHA-256。

| ModelFactory ID | Purpose / 用途 | Runtime assets / 运行资产 | License |
| --- | --- | --- | --- |
| `vision-language/clip-vit-b-32` | image/text embeddings, zero-shot classification, cross-modal retrieval / 图文嵌入、零样本分类、跨模态检索 | image encoder, text encoder, tokenizer and preprocessing sidecars / 图像编码器、文本编码器、分词与预处理文件 | MIT |
| `segmentation/sam-v1-vit-b` | point, box, mask-feedback, and multimask segmentation / 点、框、Mask 反馈与多 Mask 分割 | image encoder and prompt-mask decoder / 图像编码器与提示 Mask 解码器 | Apache-2.0 |
| `generative-vision-language/blip-caption-base` | image captioning / 图像描述 | vision encoder, full-prefix language decoder, vocabulary and generation sidecars / 视觉编码器、全前缀语言解码器、词表与生成配置 | BSD-3-Clause |
| `anomalib/padim/mvtec-bottle` | image anomaly detection / 图像异常检测 | PaDiM ONNX model for MVTec AD bottle / MVTec AD bottle PaDiM ONNX 模型 | Apache-2.0 |
| `bria/rmbg-1.4` | foreground matting / 前景抠图 | BRIA RMBG 1.4 fp32 ONNX alpha model / BRIA RMBG 1.4 fp32 ONNX alpha 模型 | LicenseRef-BRIA-RMBG-1.4 |
| `bria/rmbg-2.0` | foreground matting / 前景抠图 | BRIA RMBG 2.0 fp32 and dynamic-int8 ONNX alpha variants / BRIA RMBG 2.0 fp32 与 dynamic-int8 ONNX alpha 变体 | LicenseRef-BRIA-RMBG-2.0 |

The collection contains 27 catalog assets totaling `3,718,633,898` bytes. ModelFactory downloads only the assets for the selected entry and materializes each file at its catalog `relativePath`; sharing a Release does not require downloading the other five models. / 集合包含 27 个目录资产，总计 `3,718,633,898` 字节。ModelFactory 只下载所选条目的资产，并按目录中的 `relativePath` 落盘；共享 Release 不会强制下载另外五个模型。

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
