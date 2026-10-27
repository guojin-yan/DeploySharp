# PaddleOCR 三模型流水线

PaddleOCR 的检测、方向分类和文字识别是三个独立工件。`OcrPipeline` 将它们按“检测 → 文本行裁剪 → 可选方向分类 → 识别 → 坐标还原”串联，支持 ONNX Runtime、OpenVINO、OpenCV DNN，以及具备匹配 Engine 和 CUDA 环境时的 TensorRT。逐模型合同和工件版本见[验证矩阵](../model-backend-verification-matrix.md)。

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

核心模型目录现在也提供了统一的 `PaddleOcrModelCatalog`：v4 包含 mobile/server DET、REC 和 legacy CLS，v5 包含 mobile/server DET、REC、text-line CLS，v6 包含 tiny/small/medium DET、REC。目录中的每一行都映射到现有的 `CreateDetection`、`CreateRecognition`、`CreateLegacyClassification` 或 `CreateTextLineOrientationClassification` 工厂，因此这些模型不是“只下载并转换”；代码合同、张量名称、字典需求和模型资产状态分别可审计。v6 官方没有独立的版本化 CLS 归档，继续复用 PP-LCNet text-line orientation 分类器。

模型资产采集入口为 `eng/models/paddle-ocr/scripts/Acquire-PaddleOcrModels.ps1`，PP-Structure 采集入口为 `eng/models/paddle-document/scripts/Acquire-PaddleDocumentModels.ps1`，统一模型 Release 使用稳定标签 `models-paddleocr`。Release 将 v4/v5/v6 的核心 DET/REC/CLS 与已转换的 PP-Structure 模型按单文件资产提供，用户可以只下载所需模型，不必下载整包；目录、采集摘要和 SHA256 清单记录每个资产的来源与状态。模型资产可下载不等同于所有后端均已验证，每个后端仍需按目标运行时单独验证，不能将 ONNX 存在等同于 TensorRT、OpenVINO 或 OpenCV DNN 已验证。

运行时下载使用 `JYPPX.DeploySharp.ModelFactory.PaddleOcrReleaseClient`。它读取 Release 目录后只下载所选模型和必需字典，按目录中的大小与 SHA-256 校验并缓存；核心 OCR 可使用 `paddleocr/ppocrv5/mobile-rec` 等代码 ID，PP-Structure 可使用 `paddle-doc/pp-doclayout-s`、`paddle-table/slanext-wired`、`paddle-formula/unimernet` 等代码 ID。下载后将 `ModelPath` 传给对应的 `PaddleOcrModelCatalog` 或 `PaddleDocumentProfiles` Profile；模型下载、Profile 构造和后端 Session 创建仍是三个明确步骤。

需要从 Release 获取模型时，使用 `PaddleOcrReleaseClient` 按模型 ID 下载 DET、CLS、REC 和字典，再将校验后的 `ModelPath` 交给对应 Profile。离线模式只读取已经完整校验的缓存。完整流水线的命令行复现与性能采集见 [PaddleOCR Benchmark](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark/README.md)。TensorRT 仍需在目标设备为所选 ONNX 和 shape profile 构建 Engine，Release 中的 ONNX 不会被当作可移植 Engine。

| 合同 | 输入 | 输出 | 语义 |
| --- | --- | --- | --- |
| Legacy 方向分类 | BGR、`[3,48,192]` | `[1,2]` | `0` / `180` |
| PP-LCNet 文本行方向 | RGB、`[1,3,80,160]` | `[1,2]` | `0_degree` / `180_degree` |
| 四方向分类 | 按实际 Profile 声明 | `[1,4]` | `0` / `90` / `180` / `270` |

`OcrOrientationSchema` 要求显式声明类别顺序、输出名称、类型和形状，不会从文件名或 rank 推断角度。拒识时可选择 `Fail` 或显式的 `UseZeroDegrees`，不能把低置信度结果静默当作正向分类。

检测 Profile 的 `PaddleDbPostprocessOptions` 默认采用 PaddleOCR 官方 DB 阈值（概率阈值 `0.3`、框阈值 `0.6`、unclip `1.5`）。默认 `PaddleDbBoxType.Quadrilateral` 为识别裁剪提供稳定的四点框；需要保留不规则文本边界时可选择 `PaddleDbBoxType.Polygon`，此时结果保留连通区域凸包，`CropQuadrilateral` 明确为空，调用方应使用 `Region.Polygon`。两种模式共用有限工作区、非有限值检查和源坐标还原，避免用四边形元数据冒充多边形结果。

## Batch、Session 池和性能

检测通常使用一个 Session；方向分类和识别可以分别配置独立 Session 池，并使用动态 batch 一次处理多条文本行。`maximumRecognitionBatch` 控制单批行数，Session 池大小控制并发通道数；剩余批次等待空闲通道，不共享 native predictor 或 TensorRT execution context。每个阶段使用固定数量 worker 调度批次，不会为一页中的全部识别批次预先创建 Task；准备、后端或解码失败/取消后会停止分配尚未开始的批次，并等待已进入后端的 batch 安全退出。

已有调用方提供的文本框时，可用 `OcrPipeline.CreateRecognitionOnly(registry, recognitionSelection, recognitionRequest, cropProfile)` 创建仅含 REC Session 的管线，再调用 `RecognizeOnlyAsync(input, regions)`；`Run/RunAsync` 仅适用于含 DET 的完整管线。多模块共用一张图片时，`OpenCvDecodedRoiImage.CreateRecognitionInput()` 会生成共享原生像素的识别视图，调用方分别释放视图和页面持有者；此路径不重新解码，也不准备无用的检测张量。[表格案例](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/02-visual/paddle-structure-table/README.md)展示了结构/检测框对齐后的批量识别和安全 HTML 填字。

推荐的调度顺序是：检测完成后立即把 crop 描述排入有界队列；方向分类和识别各自从队列取满一个 batch；一个阶段的 Session 忙碌时让其他独立 Session 接管，不增加无限线程。设备显存不足时优先降低 Session 数，再降低 batch；batch 太小会增加提交开销，batch 太大则会增加 padding 和尾批等待。

在 RTX 2060 的 TensorRT CUDA 实测中，`demo_1.jpg` 的稳定最优组合为：v4 mobile `batch=8 / 2` 个阶段 Session、v5 mobile `batch=8 / 2`、v6 tiny `batch=8 / 1`、v6 small `batch=8 / 2`、v6 medium `batch=8 / 2`。对应完整流水线 P50/P95 为 v4 `32.505/37.394 ms`、v5 `46.090/50.015 ms`、v6 tiny `19.527/20.339 ms`、v6 small `31.511/36.351 ms`、v6 medium `80.692/85.247 ms`；这些数字只适用于该设备、Engine、输入和测试协议，详见[设备性能实测](device-performance-benchmarks.md)。

运行时应同时记录 `detection_inference_ms`、`detection_postprocess_ms`、`recognition_prepare_work_ms`、`recognition_inference_work_ms` 和 `recognition_postprocess_work_ms`。如果前处理或 crop 时间突然超过推理时间，优先检查图像是否重复解码、透视采样是否反复分配 Mat、尾批是否强行 padding，以及是否误把冷启动编译计入稳态。

完整流水线的最佳 batch/通道组合和具名设备耗时见[设备性能实测](device-performance-benchmarks.md)。不同后端的推理时间不能互相替代，部署时应在目标设备上重新测量。

## 核心组合验证

真实完整流水线覆盖以下七组组合：PP-OCRv4 Mobile/Server、PP-OCRv5 Mobile/Server，以及 PP-OCRv6 Tiny/Small/Medium。每组都会执行检测、透视裁剪、方向分类、CTC 识别和坐标合并。v6 官方没有独立的版本化 CLS，因此按目录合同复用 PP-OCRv5 Mobile 文本行方向模型。

ONNX Runtime CPU 测试需要先准备模型根目录和测试图片，再显式开启外部模型门：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrAllCorePipelineOrtIntegrationTests
```

## 复现和限制

模型文件、字典和原生 runtime 由应用提供；仓库不把它们嵌入 Visual 包。模型/后端逐项状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。如果输入输出名称、字典或方向类别不匹配，Pipeline 会在执行前返回带稳定错误码的诊断。

OpenVINO CPU 使用同一组模型组合和结果非空合同，但仍需独立启用其外部模型测试：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrAllCorePipelineOpenVinoIntegrationTests
```

OpenCV DNN 的 DB 输出尺寸必须从实际准备输入绑定，不能把概率图高度写死；否则同一输出缓冲会被错误解释并改变检测几何。当前合同测试会比较原始概率图、区域数量、文本、分数和坐标。OpenCV 图通常固定为 Batch=1，需要用独立 Session 池调度文本行；动态宽度可能触发重复专门化，固定宽度或宽度分桶必须在目标数据上同时评估延迟和 CER/WER。

各后端的最新精确状态见[验证矩阵](../model-backend-verification-matrix.md)，正式 P50/P95 和最佳 Batch/Session 组合只在[设备性能实测](device-performance-benchmarks.md)维护。单次冷启动、演示图片一致或某个阶段通过都不能替代完整质量与性能验证。
