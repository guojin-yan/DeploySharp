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

## 阶段和数据所有权

| 阶段 | 输入 | 输出 | 性能注意事项 |
| --- | --- | --- | --- |
| Detection | 一次解码的源图，按检测 Profile 归一化 | 原图坐标 Polygon | 通常一个检测 Session；不要为每个文本行重复运行检测 |
| Crop | Detection Polygon + 同一源图 | 有序文本行 crop | 透视采样直接写入预分配工作区，按 `maximumRegions` 限制在途数量 |
| Orientation | 文本行 crop batch | 角度类别/置信度 | 每个独立 Session 可处理一批 crop；低置信度必须走明确拒识策略 |
| Recognition | 原始或旋转后的 crop batch | CTC token、文本、置信度 | 复用 batch buffer 和字符字典；按 batch 满载提交，不为最后一批复制整图 |
| Merge | 检测 Polygon、文本和角度 | 原图坐标 `OcrResult` | 只搬运必要的标量和字符串，释放阶段性 native buffer |

源图只解码一次，crop 阶段不再产生第二份完整源图。调用方如果需要长期保存像素，应显式复制 `Prepared` 输入；否则由 Pipeline 在请求结束后释放临时工作区。

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

推荐的调度顺序是：检测完成后立即把 crop 描述排入有界队列；方向分类和识别各自从队列取满一个 batch；一个阶段的 Session 忙碌时让其他独立 Session 接管，不增加无限线程。设备显存不足时优先降低 Session 数，再降低 batch；batch 太小会增加提交开销，batch 太大则会增加 padding 和尾批等待。

在 RTX 2060 的 TensorRT CUDA 实测中，`demo_1.jpg` 的稳定最优组合为：v4 mobile `batch=8 / 2` 个阶段 Session、v5 mobile `batch=8 / 2`、v6 tiny `batch=8 / 1`、v6 small `batch=8 / 2`、v6 medium `batch=8 / 2`。对应完整流水线 P50/P95 为 v4 `32.505/37.394 ms`、v5 `46.090/50.015 ms`、v6 tiny `19.527/20.339 ms`、v6 small `31.511/36.351 ms`、v6 medium `80.692/85.247 ms`；这些数字只适用于该设备、Engine、输入和测试协议，详见[设备性能实测](device-performance-benchmarks.md)。

运行时应同时记录 `detection_inference_ms`、`detection_postprocess_ms`、`recognition_prepare_work_ms`、`recognition_inference_work_ms` 和 `recognition_postprocess_work_ms`。如果前处理或 crop 时间突然超过推理时间，优先检查图像是否重复解码、透视采样是否反复分配 Mat、尾批是否强行 padding，以及是否误把冷启动编译计入稳态。

完整流水线的最佳 batch/通道组合和具名设备耗时见[设备性能实测](device-performance-benchmarks.md)。不同后端的推理时间不能互相替代，部署时应在目标设备上重新测量。

## 复现和限制

模型文件、字典和原生 runtime 由应用提供；仓库不把它们嵌入 Visual 包。模型/后端逐项状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。如果输入输出名称、字典或方向类别不匹配，Pipeline 会在执行前返回带稳定错误码的诊断。

OpenCV DNN 的完整流水线是否可用取决于每个 det/cls/rec 图能否被当前 importer 导入；动态 DB、CTC 或辅助输入限制会在阶段级报告中标记 `unsupported`，不应被解释为 OCR 算法本身失败。TensorRT CUDA 的设备侧 crop/CTC 优化是可选路径，ONNX Runtime、OpenVINO 和 OpenCV DNN 继续使用各自的 CPU/后端流程。
