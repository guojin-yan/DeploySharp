# 异常检测

本页说明 PaDiM、PatchCore 等异常检测模型在 DeploySharp 中的输入、输出和运行方式。异常检测不是普通分类：除了图像级分数，模型可能还返回特征距离图，阈值、特征统计量和输出空间必须与训练时保持一致。

## 处理流程

`AnomalyPipeline` 按以下顺序执行：

1. 图像适配器只解码一次，并按照 Profile 的尺寸、颜色顺序和归一化规则生成 `PreparedVisualInput`。
2. 后端 Session 执行特征提取或距离计算。PaDiM 通常需要随模型发布的均值、协方差或等价统计量；PatchCore 还需要参考特征库。
3. `AnomalyDecoder` 将图像分数、原始分数图和归一化分数图恢复到源图坐标。
4. 使用 Profile 中声明的阈值生成 `AnomalyBinaryMask`，同时计算异常像素比例。

统计量、输入尺寸和阈值不是可互换的参数。更换训练集、特征库或导出尺寸后，必须重新生成并验证 Profile。

## 最小示例

```csharp
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.OpenCV;

var model = new ModelArtifact(
    new ModelId("anomalib/padim/mvtec-bottle"),
    "onnx", modelPath,
    artifactSha256: modelSha256,
    preferredBackend: OnnxRuntimeBackendProvider.BackendId);
var profile = AnomalibProfiles.CreatePadim(
    model.ModelId,
    new AnomalibArtifactContract(14, modelSha256, upstreamCommit, exporterVersion));
var profiles = new VisualProfileRegistry();
profiles.Register(profile.VisualProfile);
profiles.Freeze();
using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");
using var pipeline = new AnomalyPipeline(
    backends, profiles.Select(model, backends, request, VisualTaskId.AnomalyDetection), request);
using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
    imagePath, profile.VisualProfile.Input.Name,
    OpenCvStage19Preprocessing.CreateAnomalibOptions(profile));
AnomalyDetectionResult result = pipeline.Run(input);
Console.WriteLine($"score={result.ImageScore:R}; ratio={result.AnomalousPixelRatio:R}");
```

`modelSha256`、`upstreamCommit` 和 `exporterVersion` 必须来自实际模型工件或 ModelPack，不能使用示例占位值。仓库中可直接运行的下载、绑定和输出示例见 `samples/06-models/release-inference`。

## 输出与坐标

`ImageScore` 是模型级分数；`NormalizedMap` 是已还原到源图尺寸的连续分数图；`Mask` 是应用阈值后的自有二值掩码。`Transform` 是唯一的几何还原依据，不要在结果外再次缩放或翻转坐标。调用 `ToArray()` 会创建托管副本，长期处理时应避免在每一帧重复复制整张图。

## Batch、并发与生命周期

只有输入和输出合同同时声明有界动态 batch 时，decoder 才会返回 `AnomalyDetectionBatchResult`。batch-one 模型应使用 `VisualPipeline.RunManyAsync` 或配置 `SessionOptions(maxConcurrency: n)` 创建 n 个独立后端 Session；这不是把多个样本拼进一个未经声明的张量。每个 native Session 都必须从头创建，不能复制托管包装器。

批量调用应保留 `VisualPipeline` 实例并调用其 `RunManyAsync`；`AnomalyPipeline` 适合单模型的同步/异步调用。完整接口和真正 Batch 的区别见 [Batch、Session 池与并发](batch-session-concurrency.md)。有状态的统计量和 Session 不能在多个线程中同时修改。

## 性能测量

应分别记录图像解码、预处理、后端推理、分数图还原、阈值化和结果分配。稳态测试复用已经准备的输入；冷启动测试才包含解码和预处理。模型加载、OpenVINO 编译、TensorRT Engine 构建和 CUDA 初始化必须排除在计时区间外。大图或视频场景可以使用异步预取，但窗口切片检测不适用于需要全图统计量的 PaDiM，除非重新定义训练和统计范围。

## 支持边界

当前公开目录包含 `anomalib/padim/mvtec-bottle`。具体后端和精确工件状态以[模型支持指南](model-support.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准；设备耗时以[设备性能实测](device-performance-benchmarks.md)为准。OpenCV DNN 的导入限制、动态 shape 和辅助输入问题不应被转换成“模型不支持”。
