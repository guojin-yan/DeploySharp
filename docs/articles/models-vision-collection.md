# 已发布视觉模型集合

本页是视觉 ModelFactory 条目的快速索引。多个模型可以共享同一个 GitHub Release，但每个条目的 ModelPack、相对路径和校验信息仍然独立；客户端下载时只获取所选条目。

| ModelFactory ID | 任务 | 主要资产 |
| --- | --- | --- |
| vision-language/clip-vit-b-32 | 图文嵌入、零样本分类、检索 | image encoder、text encoder、tokenizer、预处理 sidecar |
| segmentation/sam-v1-vit-b | 点/框/Mask feedback 分割 | image encoder、prompt-mask decoder |
| generative-vision-language/blip-caption-base | 图像描述 | vision encoder、语言 decoder、词表和生成配置 |
| anomalib/padim/mvtec-bottle | 图像异常检测 | PaDiM ONNX 模型 |
| bria/rmbg-1.4 | 前景抠图 | RMBG 1.4 ONNX alpha 模型 |
| bria/rmbg-2.0 | 前景抠图 | RMBG 2.0 fp32 和 dynamic-int8 ONNX 变体 |

~~~csharp
ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
ModelSelection selection = ModelCatalogQuery.Select(
    catalog,
    new ModelQuery(
        modelId: "vision-language/clip-vit-b-32",
        backend: "onnxruntime",
        format: "onnx",
        includePreview: true)).Single();
~~~

这些条目属于 Alpha Preview。具体下载地址、版本和当前后端状态以[官方模型目录](model-catalog.md)和[模型支持指南](model-support.md)为准；未列出的 SigLIP 2、SAM 2/3、BLIP VQA、BLIP-2、InstructBLIP 等模型不能用集合中的其他条目替代。
