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

核心模型目录现在也提供了统一的 `PaddleOcrModelCatalog`：v4 包含 mobile/server DET、REC 和 legacy CLS，v5 包含 mobile/server DET、REC、text-line CLS，v6 包含 tiny/small/medium DET、REC。目录中的每一行都映射到现有的 `CreateDetection`、`CreateRecognition`、`CreateLegacyClassification` 或 `CreateTextLineOrientationClassification` 工厂，因此这些模型不是“只下载并转换”；代码合同、张量名称、字典需求和模型资产状态分别可审计。v6 官方没有独立的版本化 CLS 归档，继续复用 PP-LCNet text-line orientation 分类器。

模型资产采集入口为 `eng/models/paddle-ocr/scripts/Acquire-PaddleOcrModels.ps1`，PP-Structure 采集入口为 `eng/models/paddle-document/scripts/Acquire-PaddleDocumentModels.ps1`，统一模型 Release 使用稳定标签 `models-paddleocr`。Release 将 v4/v5/v6 的核心 DET/REC/CLS 与已转换的 PP-Structure 模型按单文件资产提供，用户可以只下载所需模型，不必下载整包；目录、采集摘要和 SHA256 清单记录每个资产的来源与状态。模型资产可下载不等同于所有后端均已验证，每个后端仍需按目标运行时单独验证，不能将 ONNX 存在等同于 TensorRT、OpenVINO 或 OpenCV DNN 已验证。

运行时下载使用 `JYPPX.DeploySharp.ModelFactory.PaddleOcrReleaseClient`。它读取 Release 目录后只下载所选模型和必需字典，按目录中的大小与 SHA-256 校验并缓存；核心 OCR 可使用 `paddleocr/ppocrv5/mobile-rec` 等代码 ID，PP-Structure 可使用 `paddle-doc/pp-doclayout-s`、`paddle-table/slanext-wired`、`paddle-formula/unimernet` 等代码 ID。下载后将 `ModelPath` 传给对应的 `PaddleOcrModelCatalog` 或 `PaddleDocumentProfiles` Profile；模型下载、Profile 构造和后端 Session 创建仍是三个明确步骤。

如果希望直接运行完整三模型流水线，可使用 `demos/paddleocr` 的 `--release` 模式；它会根据 `--version`/`--variant` 下载 DET、CLS、REC 和字典，绑定准确的 opset、张量输出名与 SHA-256，再执行现有 `OcrPipeline`。`--offline` 可强制只使用已经校验的缓存。TensorRT 仍需额外准备与当前设备匹配的 engine，Release 中的 ONNX 不会自动冒充 engine。

| 合同 | 输入 | 输出 | 语义 |
| --- | --- | --- | --- |
| Legacy 方向分类 | BGR、`[3,48,192]` | `[1,2]` | `0` / `180` |
| PP-LCNet 文本行方向 | RGB、`[1,3,80,160]` | `[1,2]` | `0_degree` / `180_degree` |
| 四方向分类 | 按实际 Profile 声明 | `[1,4]` | `0` / `90` / `180` / `270` |

`OcrOrientationSchema` 要求显式声明类别顺序、输出名称、类型和形状，不会从文件名或 rank 推断角度。拒识时可选择 `Fail` 或显式的 `UseZeroDegrees`，不能把低置信度结果静默当作正向分类。

检测 Profile 的 `PaddleDbPostprocessOptions` 默认采用 PaddleOCR 官方 DB 阈值（概率阈值 `0.3`、框阈值 `0.6`、unclip `1.5`）。默认 `PaddleDbBoxType.Quadrilateral` 为识别裁剪提供稳定的四点框；需要保留不规则文本边界时可选择 `PaddleDbBoxType.Polygon`，此时结果保留连通区域凸包，`CropQuadrilateral` 明确为空，调用方应使用 `Region.Polygon`。两种模式共用有限工作区、非有限值检查和源坐标还原，避免用四边形元数据冒充多边形结果。

## Batch、Session 池和性能

检测通常使用一个 Session；方向分类和识别可以分别配置独立 Session 池，并使用动态 batch 一次处理多条文本行。`maximumRecognitionBatch` 控制单批行数，Session 池大小控制并发通道数；剩余批次等待空闲通道，不共享 native predictor 或 TensorRT execution context。

推荐的调度顺序是：检测完成后立即把 crop 描述排入有界队列；方向分类和识别各自从队列取满一个 batch；一个阶段的 Session 忙碌时让其他独立 Session 接管，不增加无限线程。设备显存不足时优先降低 Session 数，再降低 batch；batch 太小会增加提交开销，batch 太大则会增加 padding 和尾批等待。

在 RTX 2060 的 TensorRT CUDA 实测中，`demo_1.jpg` 的稳定最优组合为：v4 mobile `batch=8 / 2` 个阶段 Session、v5 mobile `batch=8 / 2`、v6 tiny `batch=8 / 1`、v6 small `batch=8 / 2`、v6 medium `batch=8 / 2`。对应完整流水线 P50/P95 为 v4 `32.505/37.394 ms`、v5 `46.090/50.015 ms`、v6 tiny `19.527/20.339 ms`、v6 small `31.511/36.351 ms`、v6 medium `80.692/85.247 ms`；这些数字只适用于该设备、Engine、输入和测试协议，详见[设备性能实测](device-performance-benchmarks.md)。

运行时应同时记录 `detection_inference_ms`、`detection_postprocess_ms`、`recognition_prepare_work_ms`、`recognition_inference_work_ms` 和 `recognition_postprocess_work_ms`。如果前处理或 crop 时间突然超过推理时间，优先检查图像是否重复解码、透视采样是否反复分配 Mat、尾批是否强行 padding，以及是否误把冷启动编译计入稳态。

完整流水线的最佳 batch/通道组合和具名设备耗时见[设备性能实测](device-performance-benchmarks.md)。不同后端的推理时间不能互相替代，部署时应在目标设备上重新测量。

## 核心模型完整流水线实测

`PaddleOcrAllCorePipelineOrtIntegrationTests` 已覆盖当前本机全部核心 DET/REC 工件，并把可用 CLS 组合接入同一条真实流水线：v4 mobile、v4 server、v5 mobile、v5 server、v6 tiny、v6 small、v6 medium，共 7 组。v6 官方没有独立版本化 CLS，因此测试明确使用 PP-OCRv5 mobile text-line CLS；这不是把 v6 模型伪装成 v5，而是目录中记录的官方复用策略。每组都使用 `E:\Data\ocr\demo_1.jpg`，真实执行检测、透视裁剪、方向分类、CTC 识别和坐标合并；每组返回 16 个检测区域且 16 个区域均有识别文本。

| 组合 | ORT CPU 单次端到端耗时 | 区域/识别数 | 结果 SHA-256 |
| --- | ---: | ---: | --- |
| PP-OCRv4 mobile | 676.431 ms | 16 / 16 | `049eb7563013bf5d7b4938c28112bfd4aa74081cfeffe7c8cb2a435b0e170c15` |
| PP-OCRv4 server | 2840.955 ms | 16 / 16 | `981cb5a5007c41be6171c9a01586284aee371adb1ebb7ae6e18c49e32eaefc82` |
| PP-OCRv5 mobile | 911.419 ms | 16 / 16 | `978d82249162cef011a99e31bfec0f3e17eed986b9746b678272a015d0278212` |
| PP-OCRv5 server | 1890.864 ms | 16 / 16 | `45921c49f5f006705eab60cf0675d02b5de11a4b0625d95005d3811768a8335a` |
| PP-OCRv6 tiny | 356.106 ms | 16 / 16 | `85f00c5ad8ee587bf316fc1966e01430bffaf0c38bd2dc280c539fc5aeb1856e` |
| PP-OCRv6 small | 1044.636 ms | 16 / 16 | `2fdf5b2687d02b06704aa2bd6c39652aa85647174e2e4017a98c356ebbe4313f` |
| PP-OCRv6 medium | 2286.589 ms | 16 / 16 | `fbac8dcb3469d95702f3fec67188a71b1424eee8a15ae7d631d9395f38cf3b47` |

这些是单次冷启动结果，包含模型 Session 创建、第一次图执行和 OCR 后处理，不应与已预热的 P50/P95 混用。复现实验需要显式授权本机外部模型：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_ORT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrAllCorePipelineOrtIntegrationTests
```

## 复现和限制

模型文件、字典和原生 runtime 由应用提供；仓库不把它们嵌入 Visual 包。模型/后端逐项状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)。如果输入输出名称、字典或方向类别不匹配，Pipeline 会在执行前返回带稳定错误码的诊断。

OpenVINO CPU 的完整流水线也已完成独立验证：`PaddleOcrAllCorePipelineOpenVinoIntegrationTests` 在 Windows x64、`E:\Data\ocr\demo_1.jpg` 和本机 `E:\Model\paddleocr` 模型上，逐组执行 PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium，共 7 组，测试 1/1 通过（测试内部每组都要求检测区域和识别文本非空）。该证据只说明当前 OpenVINO CPU runtime 能完成这些已登记工件的流水线编排，不代表其它版本、设备或 TensorRT/OpenCV DNN 自动兼容。

复现 OpenVINO 全流水线：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_OPENVINO_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrAllCorePipelineOpenVinoIntegrationTests
```

此前同一图片上 OpenCV DNN 显示 8 区域、ORT 显示 16 区域。复核发现不是 OpenCV DNN importer 或 DB 后处理失败，而是外部测试的输出合同把概率图高度错误地固定为 `128`；实际准备张量为 `[1,3,512,512]`，PP-OCRv5 mobile 的概率图为 `[1,1,512,512]`。OpenCV Session 按合同把同样数量的输出值解释成 `[1,1,128,2048]`，改变了解码几何。测试现改为从准备输入绑定输出高度，并增加 OpenCV/ORT 的同张量原始输出和检测框对照。当前 v5 mobile 全流程两边均返回 16 区域、逐区域识别文本一致；坐标容差 0.5 px、分数容差 0.001，原始概率输出 max/mean 绝对差 `4.2915344e-5` / `8.6187186e-8`。最新一次 OpenCV `pipeline.Run` 观察为 `4117.808 ms`，前一次为 `5655.803 ms`；均为单次诊断观察，不是稳定性能基准，也不含模型和图像输入初始化。独立 benchmark 已将 v4 mobile/server、v5 mobile/server、v6 tiny/small/medium 七组写入 [`paddleocr-core-opencv-20260924.json`](../../eng/models/paddle-ocr/verification/paddleocr-core-opencv-20260924.json)。复现入口为 `PaddleOcrOpenCvFullPipelineIntegrationTests`，设置 `DEPLOYSHARP_PADDLEOCR_OPENCV_RUN_EXTERNAL=1`；该测试仍专门验证 v5 mobile 的逐元素/逐区域对照，七组 benchmark 结果不能外推为多图准确率。TensorRT CUDA 的设备侧 crop/CTC 优化是可选路径，ORT、OpenVINO 和 OpenCV DNN 仍各走各自后端流程。
