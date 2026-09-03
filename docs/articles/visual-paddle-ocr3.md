# PaddleOCR 三模型流水线

PaddleOCR 的检测、方向分类和文字识别是三个独立工件。`OcrPipeline` 将它们按“检测 → 文本行裁剪 → 可选方向分类 → 识别 → 坐标还原”串联，支持 ONNX Runtime、OpenVINO，以及具备匹配 Engine 和 CUDA 环境时的 TensorRT。

## 快速使用

应用提供三个外部模型、识别字典、图像适配器和后端：

```csharp
PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(
    detectorId, detectorContract);
PaddleOcrProfile classifier =
    PaddleOcrProfiles.CreateTextLineOrientationClassification(
        classifierId, classifierContract, rejectionThreshold: 0.9f);
PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(
    recognizerId, recognizerContract, characters);

using var pipeline = new OcrPipeline(
    backends,
    profiles.Select(detectorArtifact, backends, request,
        VisualTaskId.TextDetection), request,
    profiles.Select(classifierArtifact, backends, request,
        VisualTaskId.TextOrientationClassification), request,
    classifier.CropProfile!,
    profiles.Select(recognizerArtifact, backends, request,
        VisualTaskId.TextRecognition), request,
    recognizer.CropProfile!,
    new OcrPipelineOptions(
        maximumRegions: 32,
        maximumRecognitionBatch: 16),
    orientationRejectionPolicy:
        OcrOrientationRejectionPolicy.UseZeroDegrees);

using OpenCvOcrImageInput input = imageFactory.CreateFromFile(
    imagePath,
    detector.VisualProfile.Input.Name,
    OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(sourceSize));
OcrResult result = pipeline.Run(input);
```

文本行会从同一张源图透视裁剪。方向分类通过后，识别阶段使用旋转后的 crop；所有返回 polygon、文本和置信度都使用原图坐标，native `Mat` 不会泄漏到公共结果中。

## 模型版本和方向合同

仓库案例覆盖 PP-OCR v4/v5/v6 的检测、方向分类和识别组合。版本之间的输入尺寸、颜色顺序、字典和输出名称可能不同，必须为每个实际工件注册独立 Profile。常见方向合同包括：

| 合同 | 输入 | 输出 | 语义 |
| --- | --- | --- | --- |
| Legacy 方向分类 | BGR、`[3,48,192]` | `[1,2]` | `0` / `180` |
| PP-LCNet 文本行方向 | RGB、`[1,3,80,160]` | `[1,2]` | `0_degree` / `180_degree` |
| 四方向分类 | 按实际 Profile 声明 | `[1,4]` | `0` / `90` / `180` / `270` |

`OcrOrientationSchema` 要求显式声明类别顺序、输出名称、类型和形状，不会从文件名或 rank 推断角度。拒识时可选择 `Fail` 或显式的 `UseZeroDegrees`，不能把低置信度结果静默当作正向分类。

## Batch、Session 池和性能

检测通常使用一个 Session；方向分类和识别可以分别配置独立 Session 池，并使用动态 batch 一次处理多条文本行。`maximumRecognitionBatch` 控制单批行数，Session 池大小控制并发通道数；剩余批次等待空闲通道，不共享 native predictor 或 TensorRT execution context。

完整流水线的最佳 batch/通道组合和具名设备耗时见[设备性能实测](device-performance-benchmarks.md)。不同后端的推理时间不能互相替代，部署时应在目标设备上重新测量。

## 复现和限制

模型文件、字典和原生 runtime 由应用提供；仓库不把它们嵌入 Visual 包。模型/后端逐项状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。如果输入输出名称、字典或方向类别不匹配，Pipeline 会在执行前返回带稳定错误码的诊断。
