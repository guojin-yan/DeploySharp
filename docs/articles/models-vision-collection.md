# 已发布视觉模型集合

本页是视觉 ModelFactory 条目的快速索引。多个模型可以共享同一个 GitHub Release，但每个条目的 ModelPack、相对路径和校验信息仍然独立；客户端下载时只获取所选条目。

| ModelFactory ID | 任务 | 主要资产 | 已验证后端 |
| --- | --- | --- | --- |
| vision-language/clip-vit-b-32 | 图文嵌入、零样本分类、检索 | image encoder、text encoder、tokenizer、预处理 sidecar | ONNX Runtime CPU、OpenVINO CPU |
| segmentation/sam-v1-vit-b | 点/框/Mask feedback 分割 | image encoder、prompt-mask decoder | ONNX Runtime CPU、OpenVINO CPU |
| generative-vision-language/blip-caption-base | 图像描述 | vision encoder、语言 decoder、词表和生成配置 | ONNX Runtime CPU、OpenVINO CPU |
| anomalib/padim/mvtec-bottle | 图像异常检测 | PaDiM ONNX 模型 | 见验证矩阵 |
| bria/rmbg-1.4 | 前景抠图 | RMBG 1.4 ONNX 模型 | 见验证矩阵 |
| bria/rmbg-2.0 | 前景抠图 | RMBG 2.0 FP32 和 Dynamic INT8 ONNX 变体 | 按精度变体读取验证矩阵 |

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

这些条目属于 Alpha Preview 发布通道；其中“Preview”和“已完成精确后端验证”可以同时成立。具体下载地址、版本和当前后端状态以[官方模型目录](model-catalog.md)、[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)为准；未列出的 SigLIP 2、SAM 2/3、BLIP VQA、BLIP-2、InstructBLIP 等模型不能用集合中的其他条目替代。

## 按任务选择资产

- 目标检测、分割和姿态优先选择带有明确输入尺寸、标签文件和 decoder schema 的 ONNX 条目；不要只按模型名称下载一个原始权重。
- SAM v1 需要 image encoder 与 prompt/mask decoder 的配套资产，同一图像的多个提示应复用 encoder 特征。
- CLIP 需要图像 encoder、文本 encoder、tokenizer 和相同的归一化/温度参数；只有图像 encoder 不能完成图文相似度。
- BLIP Caption 是生成式流程，需要视觉 encoder、语言 decoder、词表和生成配置；它不等同于 VQA。
- PaDiM 与 RMBG 的输出不是通用分类分数：前者返回异常图/异常分数，后者返回连续 alpha，应使用对应任务页的结果处理。

## 下载、校验和复现

```powershell
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- list --preview
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- show `
  --model-id vision-language/clip-vit-b-32 --preview
dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install `
  --model-id vision-language/clip-vit-b-32 --backend onnxruntime `
  --format onnx --preview --cache .\.cache\deploysharp
```

安装完成后记录 materialized root、目录版本和每个工件 SHA-256。端到端验证必须使用与模型目录相同的测试图片、文本 tokenizer、后处理阈值和后端；“模型文件下载成功”不能作为视觉任务通过证据。

## 目前不应替代的模型

集合中没有完整的 SAM2/SAM3 视频记忆、BLIP VQA/BLIP-2、Qwen2.5-VL、Phi Vision、SigLIP 2 或 LayoutLMv3 任务头。它们可能在源码中有合同或实验入口，但在完整 Bundle、后端和结果验证完成前，不应从本页的 Preview 状态推断可用。
