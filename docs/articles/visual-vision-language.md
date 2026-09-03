# 视觉语言嵌入

DeploySharp.Visual 提供后端无关的双编码器会话，用于图像/文本嵌入、零样本分类和跨模态检索。模型的图像预处理、tokenizer、池化方式和评分语义必须由同一个 Profile 明确规定。

## 最小示例

~~~csharp
VisionLanguageEmbeddingProfile profile =
    VisionLanguageProfiles.CreateClipVitB32();
var backend = OnnxRuntimeBackendProvider.BackendId;
var bundle = new VisionLanguageArtifactBundle(
    profile,
    profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder,
        imageOnnx, backend),
    profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder,
        textOnnx, backend));

using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new VisionLanguageEmbeddingSession(
    registry, bundle,
    new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
using PreparedVisualInput image =
    new OpenCvVisionLanguageInputFactory().CreateFromFile(imagePath, profile);
VisionLanguageImageEmbedding imageEmbedding = session.EncodeImage(image);
VisionLanguageTextEmbedding textEmbedding = session.EncodeText(tokenBatch);
VisionLanguageScoreMatrix scores =
    VisionLanguageScorer.Score(profile, imageEmbedding, textEmbedding);
~~~

tokenBatch 必须由 Profile 指定的官方 tokenizer 生成；DeploySharp 不会从字符串猜测 token，也不会允许不同 Profile 的 token 或 embedding 混用。

## 模型差异

| Profile | 图像输入 | 文本输入 | 评分方式 |
| --- | --- | --- | --- |
| CLIP ViT-B/32 | RGB，224×224，NCHW，FP32 | INT64，长度 77 | 对候选集做缩放余弦相似度和 softmax |
| SigLIP base patch16-224 | RGB，224×224，NCHW，FP32 | INT64，长度 64，无 attention mask 端口 | 每个图文对独立 sigmoid，不做 CLIP 式候选集 softmax |
| SigLIP 2 | 当前只保留合同占位 | 需要官方 tokenizer 与对应导出 | 未形成完整本地运行路径 |

CLIP 使用 RGB、最短边缩放、中心裁剪和官方 mean/std；SigLIP 使用固定 224 缩放和自身的归一化参数。不要把两种模型的 tokenizer、池化或阈值互换。

## 缓存、并发与批量

VisionLanguageEmbeddingSession 拥有图像和文本两条后端会话。单个 session 的状态操作是单写者；需要并发时创建独立 session。成功结果是调用方拥有的托管对象，取消或超时不会发布半成品。大量文本或图像可按模型支持的动态 batch 组织，或由外层会话池分发。

## 支持边界

具体模型和后端状态以[模型支持指南](model-support.md)与[模型后端验证矩阵](../model-backend-verification-matrix.md)为准。TensorRT 需要匹配的双编码器 Engine；未有完整 Profile 的模型不应通过通用 CLIP 代码强行运行。性能比较请使用[设备性能实测](device-performance-benchmarks.md)中的统一条件。
