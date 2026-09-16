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

SAM v1 默认使用官方最长边缩放、右下补零和 ImageNet 均值/标准差。对于重新训练或重新导出的 Encoder，可以通过最后一个 `preprocessing` 参数显式传入 `VisualPreprocessingOptions`，切换到 `Resize`、`Letterbox` 或 `CenterCrop` 以及新的归一化；Factory 仍强制模型尺寸、RGB/NCHW、Float32 和 Batch=1。原始官方 SAM 不应随意覆盖，否则已有 Encoder 数值证据失效，点和框也必须按新 `ImageTransform` 映射。

点提示中的前景、背景标签必须和导出图合同一致；框提示的两个角点也不能在缩放后重新排序或截断。建议先用图像四角、中心点和一个已知目标框做坐标回归，再接入鼠标或触摸交互。若 mask 整体偏移，优先检查源图到模型图的缩放与补边；若 mask 位置正确但边界粗糙，再检查低分辨率 mask 的上采样和阈值。

## 一次编码、多次提示

SAM 的吞吐优势来自 Encoder embedding 复用。`SetImage` 完成图像解码、预处理和 Encoder 推理，后续每次 `Predict` 只运行 Prompt/Mask Decoder。交互式应用应在图像未变化时保留 session 和 embedding，只在切换图像、Profile 或 Encoder 工件时调用 `Clear` 后重新编码。

掩码反馈用于迭代细化时，应把本次返回的 feedback tensor 原样交回同一个 Decoder 合同；不要先转成源图位图再缩小。需要同时处理多张图片时，为每个并发中的图像使用独立会话或受控的 Session 池，不能让多个请求共享一个可变 embedding 槽。

## 复现与结果验收

完整复现至少保存以下信息：

1. Encoder、Decoder 的工件 ID 和 SHA-256；
2. 源图片哈希、原始宽高和 EXIF 方向处理方式；
3. 点、框、上一轮 mask 等提示的源图坐标；
4. 缩放模式、补边、颜色顺序、归一化和模型输入尺寸；
5. 后端、设备、运行时版本，以及 Encoder/Decoder 分阶段耗时；
6. 选中 mask 的质量分数、面积、边界框和可视化结果。

验收不应只看“产生了一个 mask”。至少要验证空提示/非法坐标返回稳定错误，多候选 mask 的数量和排序固定，同一提示重复执行结果一致，`Clear` 后不会继续复用旧图 embedding。性能测试应分别报告首次 Encoder、稳态 Decoder 和端到端交互延迟；把一次编码平均摊进多次提示会掩盖真实的首次等待时间。

## 常见故障

| 现象 | 首先检查 |
| --- | --- |
| mask 与目标位置成比例偏移 | Prompt 是否从源图坐标按同一个 `ImageTransform` 映射 |
| mask 只覆盖图像左上区域 | Decoder 使用的原始尺寸或补边尺寸是否错误 |
| 第二张图仍出现第一张图轮廓 | 切图前是否执行 `Clear`/`SetImage`，是否错误共享 session |
| 首次很慢，后续很快 | Encoder 初始化和 embedding 计算属于冷启动，应分项记录 |
| 多线程偶发交叉结果 | 单写者 session 被并发使用，应改为独立会话或 Session 池 |
| 后端加载失败 | Encoder/Decoder 输入名、动态 Shape、算子和工件格式是否受该后端支持 |

## 后端与性能

SAM 图像路径可按模型合同选择 ONNX Runtime 或 OpenVINO；TensorRT 需要与设备、输入形状匹配的 Engine。后端是否支持某个具体模型，以[模型后端验证矩阵](../model-backend-verification-matrix.md)为准；设备耗时和测试条件见[设备性能实测](device-performance-benchmarks.md)。
