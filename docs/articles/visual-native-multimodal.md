# 原生多模态与文档理解

本页介绍需要 tokenizer、图像占位符和生成状态的多模态模型，以及 Donut、LayoutLMv3、Pix2Struct 等文档模型。它们不是普通的图像分类接口：模型图、tokenizer、processor、KV cache 和输出 schema 必须作为一个完整 Bundle 管理。

## 原生多模态流程

~~~csharp
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

string modelRoot = @"models\llava-onevision";
string imagePath = @"images\input.jpg";
string imageNewlinePath = Path.Combine(modelRoot, "image_newline.f32");
NativeMultimodalProfile profile =
    NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
BackendId backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new NativeMultimodalArtifactBundle(profile, new[]
{
    new GenerativeVisionLanguageArtifactBinding(
        GenerativeVisionLanguageArtifactRole.VisionEncoder,
        profile.CreateArtifact(
            GenerativeVisionLanguageArtifactRole.VisionEncoder,
            Path.Combine(modelRoot, "vision_encoder.onnx"), backend)),
    new GenerativeVisionLanguageArtifactBinding(
        GenerativeVisionLanguageArtifactRole.TokenEmbedding,
        profile.CreateArtifact(
            GenerativeVisionLanguageArtifactRole.TokenEmbedding,
            Path.Combine(modelRoot, "embed_tokens_int8.onnx"), backend)),
    new GenerativeVisionLanguageArtifactBinding(
        GenerativeVisionLanguageArtifactRole.LanguageDecoder,
        profile.CreateArtifact(
            GenerativeVisionLanguageArtifactRole.LanguageDecoder,
            Path.Combine(modelRoot, "decoder_model_merged_int8.onnx"), backend))
});
var tokenizer = new Qwen2NativeMultimodalTokenizer(
    modelRoot, profile.Tokenizer!);
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new NativeMultimodalSession(
    registry, bundle,
    new BackendRequest(
        BackendCapabilities.TensorInference,
        OnnxRuntimeBackendProvider.BackendId,
        "cpu"),
    imageNewlinePath);
using NativeMultimodalPreparedImage image =
    new OpenCvNativeMultimodalInputFactory()
        .CreateFromFile(imagePath, profile);
session.SetImage(image);
NativeMultimodalResult answer = session.Generate(
    GenerativeVisionLanguageRequest.Question(
        "请描述图片中的主要内容。"), tokenizer);
session.Clear();
~~~

上例中的 `bundle` 必须由 Profile 为 VisionEncoder、TokenEmbedding 和 LanguageDecoder 创建对应的 `GenerativeVisionLanguageArtifactBinding`；`imageNewlinePath` 是与视觉编码器导出匹配的换行特征文件。不同 Qwen/Phi/LLaVA 工件不能互换。图像和 KV 状态只在同一 session 内有效，完成后调用 `Clear`，最终调用 `Dispose`。

## 文档理解流程

文档模型按页处理，页面顺序、图像尺寸、OCR words、token 与 box 对齐以及结构化 schema 都是输入合同的一部分。Donut/Pix2Struct 的 OCR-free 图不应额外塞入 LayoutLMv3 的 OCR token；LayoutLMv3 的 box/token 对齐也不能直接迁移到其他家族。

推荐流程：

1. 使用对应的 DocumentUnderstandingProfile 和 ArtifactBundle 声明 encoder、prefill/decode 图与 tokenizer。
2. 通过 OpenCV 文档输入工厂准备一页或一组页面，保留源图尺寸和页序。
3. 用 DocumentUnderstandingSession 或页面 batch session 执行结构化抽取。
4. 检查字段 provenance、parse 状态和 schema 版本，再释放页面与 session。

多页任务可以使用 DocumentUnderstandingPageBatchSession；当模型不支持真正的 batch 时，用独立 session 池并发处理页面，避免把一个有状态解码器同时交给多个线程。

## 并发与资源

有状态生成会修改 token/KV 状态，单个 session 只允许一个活动写入者。需要吞吐时创建有限数量的独立 session，并依据 GPU 显存、上下文长度和实测吞吐调节队列。取消会清理未完成状态，不返回部分结构化结果。

## 支持边界

当前 Qwen/Phi/LLaVA、Donut、LayoutLMv3 和 Pix2Struct 的具体后端状态并不相同，不能因合同已存在就宣称所有平台可用。请查看[模型支持指南](model-support.md)和[模型后端验证矩阵](../model-backend-verification-matrix.md)；性能数据见[设备性能实测](device-performance-benchmarks.md)。
