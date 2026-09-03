# 原生多模态与文档理解

本页介绍需要 tokenizer、图像占位符和生成状态的原生视觉语言模型，以及 Donut、LayoutLMv3、Pix2Struct 等文档模型。它们不是普通的图像分类接口：模型图、tokenizer、processor、KV cache 和输出 schema 必须作为一个完整 Bundle 管理。

## LLaVA-OneVision 流程

当前有端到端合同和外部实测证据的是 LLaVA-OneVision Qwen2 0.5B。Vision/Projector、Token Embedding 和 Language Decoder 是三个独立工件，图像 anyres 网格和 image-newline sidecar 也属于输入合同。

```csharp
using System.Collections.Generic;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

string modelRoot = @"models\llava-onevision";
NativeMultimodalProfile profile =
    NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
BackendId backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new NativeMultimodalArtifactBundle(profile, new[]
{
    Bind(profile, GenerativeVisionLanguageArtifactRole.VisionEncoder,
        Path.Combine(modelRoot, "vision_encoder.onnx"), backend),
    Bind(profile, GenerativeVisionLanguageArtifactRole.TokenEmbedding,
        Path.Combine(modelRoot, "embed_tokens_int8.onnx"), backend),
    Bind(profile, GenerativeVisionLanguageArtifactRole.LanguageDecoder,
        Path.Combine(modelRoot, "decoder_model_merged_int8.onnx"), backend)
});
var tokenizer = new Qwen2NativeMultimodalTokenizer(modelRoot, profile.Tokenizer);
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new NativeMultimodalSession(
    registry, bundle,
    new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"),
    Path.Combine(modelRoot, "image_newline.f32"));
using NativeMultimodalPreparedImage image =
    new OpenCvNativeMultimodalInputFactory().CreateFromFile(imagePath, profile);
session.SetImage(image);
NativeMultimodalResult answer = session.Generate(
    GenerativeVisionLanguageRequest.Question("What is visible in this image?"),
    tokenizer);
Console.WriteLine(answer.Generation.Generation.Text);

static GenerativeVisionLanguageArtifactBinding Bind(
    NativeMultimodalProfile profile,
    GenerativeVisionLanguageArtifactRole role,
    string path, BackendId backend)
    => new GenerativeVisionLanguageArtifactBinding(
        role, profile.CreateArtifact(role, path, backend));
```

`SetImage` 只执行一次 Vision/Projector 和 anyres packing；同一图像上的多次提问可以复用已缓存的 image state。`GenerateAsync` 可用于视频或 UI 线程，取消、超时和 callback 失败不会发布部分 KV 状态。单个 Session 是 single-writer；并发请求必须创建有限数量的完整 Session，每个 Session 都拥有自己的 Vision、Embedding 和 Decoder 通道。

## 家族差异和资产布局

| 家族 | 输入所有权 | 生成方式 | 当前代码边界 |
| --- | --- | --- | --- |
| LLaVA-OneVision Qwen2 | Processor 负责 RGB/anyres 和 image sentinel | Embedding + Prefill/Decode + Past/Present KV | LLaVA 0.5B 的 ORT/OpenVINO CPU 外部证据；应用提供完整资产 |
| Qwen2.5-VL | 视觉旋转位置、动态视频/图像网格和 Qwen KV | 模型特有多模态 projector 与生成图 | 当前没有完整 Profile、Tokenizer 和发布 Bundle，不可宣称可用 |
| Phi Vision | Phi 专用 image placeholder、视觉投影和语言头 | 模型特有 Prefill/Decode 合同 | 当前没有完整 Profile 和可下载三图 Bundle |
| BLIP/BLIP-2/InstructBLIP | Processor、Q-Former、投影和语言模型必须同源 | 任务特有 encoder/decoder 组合 | 当前没有完整 VQA/生成 Bundle |

LLaVA 的典型目录如下；文件名只是示例，运行时仍按 Profile 的 SHA-256 和命名端口校验：

```text
llava-onevision/
  tokenizer.json
  vocab.json
  merges.txt
  image_newline.f32
  vision_encoder.onnx
  embed_tokens_int8.onnx
  decoder_model_merged_int8.onnx
```

OpenVINO 可以复用相同的 Profile 语义，但必须绑定经过验证的 XML/BIN 工件；不能把 ONNX 的后端 ID 改成 OpenVINO 来绕过格式检查。TensorRT 需要与 GPU、CUDA、输入 profile 匹配的专用 Engine，当前没有把 LLaVA 生成路径列为公开 TensorRT Bundle。

## Donut 文档抽取

Donut 是 OCR-free 的单页图像到结构化标签生成模型。页面经过 thumbnail、居中 padding 和 RGB `[-1,1]` 处理，再依次执行 Encoder、Decoder Prefill 和带 Past/Present KV 的 Decode。下面示例使用当前已验证的 ONNX/ORT 路径：

```csharp
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

DocumentUnderstandingProfile profile =
    DocumentUnderstandingProfiles.CreateDonutCordV2Onnx();
BackendId backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new DocumentUnderstandingBundle(profile, new[]
{
    Bind(profile, DocumentArtifactRole.DocumentEncoder,
        Path.Combine(root, "onnx", "encoder_model.onnx"), backend),
    Bind(profile, DocumentArtifactRole.DecoderPrefill,
        Path.Combine(root, "onnx", "decoder_model.onnx"), backend),
    Bind(profile, DocumentArtifactRole.DecoderWithPast,
        Path.Combine(root, "onnx", "decoder_with_past_model.onnx"), backend)
});
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new DocumentUnderstandingSession(
    registry, bundle,
    new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
var tokenizer = new DonutDocumentTokenizer(
    Path.Combine(root, "checkpoint"), profile.Tokenizer);
using PreparedDocumentPage page =
    new OpenCvDocumentUnderstandingInputFactory().CreatePageFromFile(
        imagePath, profile);
using var document = new PreparedDocument(profile, new[] { page });
session.SetDocument(document);
DocumentUnderstandingResult result = session.Generate(
    DocumentTaskRequest.StructuredExtraction(profile.Schema.SchemaId), tokenizer);
Console.WriteLine(result.StructuredOutput.Json);

static DocumentArtifactBinding Bind(
    DocumentUnderstandingProfile profile, DocumentArtifactRole role,
    string path, BackendId backend)
    => new DocumentArtifactBinding(
        role, profile.CreateArtifact(role, path, backend));
```

多页输入不把页面强行拼成模型 batch。`DocumentUnderstandingPageBatchSession` 为每个并发槽位创建完整的 Encoder/Prefill/Decode Session，并按输入顺序返回结果：

```csharp
using var pages = new DocumentUnderstandingPageBatchSession(
    registry, bundle, request, maximumConcurrency: 2);
IReadOnlyList<DocumentUnderstandingResult> results = pages.Run(
    new[]
    {
        new DocumentPageInferenceRequest(firstDocument, task, tokenizer),
        new DocumentPageInferenceRequest(secondDocument, task, tokenizer)
    });
```

页面结果包含 schema、parse 状态、字段 provenance、token 和分阶段 timing。解析成功不代表所有字段都可信，应用仍应检查 `DocumentParseStatus`、schema identity 和 page identity。

## LayoutLMv3 与 Pix2Struct

LayoutLMv3 要求调用方提供 OCR words、0..1000 normalized boxes 以及 token-to-word alignment；它的官方 base checkpoint 没有任务头，当前 Profile 仅用于合同和输入校验。Pix2Struct DocVQA 是 OCR-free flattened patch + row/column ID + T5 生成合同，但官方配置的 cache/export 条件尚未形成完整的动态 Encoder/Prefill/Decode Bundle。两者都不能用 Donut 的图或 tokenizer 代替。

```csharp
DocumentUnderstandingProfile layout =
    DocumentUnderstandingProfiles.CreateLayoutLmV3BaseContract();
DocumentUnderstandingProfile pix =
    DocumentUnderstandingProfiles.CreatePix2StructDocVqaContract();
// 两个 Profile 当前 Executable=false；准备输入会在能力边界返回稳定错误码。
```

## 后端、并发与支持状态

文档生成模型的 Session 都是有状态 single-writer。吞吐优化顺序应为：复用解码后的页面输入；用独立 Session 池并发页级任务；只有在模型合同明确允许时才使用真正 batch；最后根据 GPU 显存和上下文长度调整队列。不要在一个 Session 上并发调用 `SetDocument`/`Generate` 或 `SetImage`/`Generate`。

当前 Qwen/Phi/LLaVA、Donut、LayoutLMv3 和 Pix2Struct 的后端状态不同，不能因接口或 Profile 存在就宣称所有平台可用。请查看[模型支持指南](model-support.md)、[模型后端验证矩阵](../model-backend-verification-matrix.md)和[设备性能实测](device-performance-benchmarks.md)；完整 Bundle、Tokenizer、sidecar 和 native runtime 均由应用负责部署。
