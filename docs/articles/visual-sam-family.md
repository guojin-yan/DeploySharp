# 提示分割与开放词汇

JYPPX.DeploySharp.Visual 提供后端无关、工件绑定的提示分割合同。SAM v1 可以对同一图像只做一次编码，然后重复提交点、框和掩码反馈；开放词汇检测可将文本提示交给检测器，再把候选框交给分割器。

## SAM v1 图像流程

~~~csharp
var profile = PromptableSegmentationProfiles.CreateSamV1(
    "external/sam-v1-vit-b",
    new ModelId("external/sam-v1-vit-b-encoder"),
    new ModelId("external/sam-v1-vit-b-decoder"),
    encoderSha256,
    decoderSha256,
    "dictionary-identity",
    "encoder-preprocess-identity",
    "decoder-export-identity");

var bundle = new PromptableSegmentationArtifactBundle(profile, new[]
{
    new PromptableSegmentationArtifact(
        PromptableSegmentationArtifactRole.ImageEncoder,
        profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder)
            .CreateArtifact(encoderPath, OnnxRuntimeBackendProvider.BackendId)),
    new PromptableSegmentationArtifact(
        PromptableSegmentationArtifactRole.PromptMaskDecoder,
        profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder)
            .CreateArtifact(decoderPath, OnnxRuntimeBackendProvider.BackendId))
});

using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new PromptableSegmentationImageSession(
    registry, bundle,
    new BackendRequest(BackendCapabilities.TensorInference,
        OnnxRuntimeBackendProvider.BackendId, "cpu"));
using PreparedVisualInput image =
    new OpenCvPromptableSegmentationInputFactory().CreateSamV1FromFile(imagePath);
session.SetImage(image);
PromptableSegmentationResult result = session.Predict(
    new PromptableSegmentationPrompt(
        new[] { new PromptPoint(430, 280, PromptPointLabel.Foreground) },
        new RectangleF(200, 80, 450, 480),
        returnMultipleMasks: true));
~~~

返回的源图 mask、RLE、质量值和反馈 tensor 归调用方所有。图像切换前调用 Clear，最终调用 Dispose。同一 session 的 set-image、predict、clear 是单写者操作；并发调用会返回稳定的 Visual 错误，而不会交叉覆盖缓存。

## 开放词汇与 Grounded-SAM

开放词汇模型的文本 tokenizer、词表、嵌入和检测输出布局必须与 Profile 完全匹配。检测结果交给 Grounded-SAM 时，应使用 GroundedSamImageSession，由会话负责坐标变换和生命周期，不要手工复制框或低分辨率 mask。

当前版本对 SAM v1 图像路径提供 ORT/OpenVINO CPU 合同；SAM 2/SAM 3 的视频 memory/tracker 尚没有完整的官方原生 Bundle，因此不提供伪造的视频兼容 API。CLIP/SigLIP 等图文模型请参阅视觉语言页面。

## 输入与坐标

OpenCV 输入工厂只解码一次，并记录源图尺寸和可逆的 ImageTransform。点和框使用同一个变换映射到模型空间；结果 mask 已还原到源图尺寸。不同图像、Profile 或工件的 embedding/feedback 不能混用。

## 后端与性能

SAM 图像路径可按模型合同选择 ONNX Runtime 或 OpenVINO；TensorRT 需要与设备、输入形状匹配的 Engine。后端是否支持某个具体模型，以[模型后端验证矩阵](../model-backend-verification-matrix.md)为准；设备耗时和测试条件见[设备性能实测](device-performance-benchmarks.md)。
