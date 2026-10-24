# OCR 检测与识别

DeploySharp 的 OCR Pipeline 将文字检测、透视裁剪、可选方向处理和 CTC 文字识别组合为一个可复用流程。检测器和识别器可以使用同一个后端，也可以分别选择 ONNX Runtime、OpenVINO、OpenCV DNN 或 TensorRT 中已经注册的后端。

Pipeline 只处理模型张量，图像解码和几何裁剪由可选的 <code>JYPPX.DeploySharp.Visual.OpenCV</code> 适配器完成。模型的输入输出名称、尺寸、归一化、字符表和 CTC 参数必须与实际导出文件一致。

## 流程

1. 图像适配器只解码一次源图，并准备检测器输入。
2. 检测 Decoder 校验 polygon 和 score，执行阈值过滤、精确 polygon NMS，并把保留区域按阅读顺序排列。
3. 每个区域生成裁剪请求；显式启用超长行滑窗时生成多个有界窗口。适配器完成透视裁剪、直角旋转、resize、padding、颜色转换和归一化。
4. 文本行按目标宽度分组，以有界 batch 提交识别器；CTC Decoder 执行 argmax、repeat collapse、blank 移除和置信度计算。
5. 结果中的坐标通过同一个 <code>ImageTransform</code> 还原到原图，并带有阶段耗时和模型来源信息。

方向校正可以来自显式的 0、90、180、270 度配置、可选 CLS Pipeline，或竖排长宽比策略。四边形透视裁剪可处理显式倾斜角点；可选几何检查报告基线角度和透视风险，不自动猜测文字是否倒置。低置信度 REC 结果可显式启用有界方向重试。方向来源和 polygon 角点顺序必须明确。

## 快速使用

下面示例展示 OpenCV 图像输入和一个动态宽度识别器；检测器和识别器的 Profile、Artifact、输入输出名称需要替换为实际模型配置。

~~~csharp
var detectorOptions = new OpenCvPreprocessOptions(
    new VisualSize(960, 544),
    OpenCvResizeMode.Letterbox,
    VisualColorOrder.Rgb,
    outputType: OpenCvOutputType.Float32);

using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory()
    .CreateFromFile(imagePath, "images", detectorOptions);

var cropProfile = new TextCropProfile(
    "my-ocr/crop.v1",
    targetHeight: 48,
    widthMode: OcrRecognitionWidthMode.Dynamic,
    fixedWidth: 320,
    maximumWidth: 640,
    widthAlignment: 8,
    colorOrder: VisualColorOrder.Rgb,
    layout: VisualTensorLayout.Nchw,
    means: new[] { 127.5f },
    scales: new[] { 1f / 127.5f });

using var ocr = new OcrPipeline(
    backendRegistry,
    detectorSelection,
    detectorRequest,
    recognizerSelection,
    recognizerRequest,
    cropProfile,
    new OcrPipelineOptions(
        maximumConcurrency: 2,
        maximumRegions: 128,
        maximumRecognitionBatch: 16));

OcrResult result = await ocr.RunAsync(
    input,
    new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(10)),
    cancellationToken);

foreach (OcrRegionResult region in result.Regions)
    Console.WriteLine($"{region.Recognition.Text} ({region.Recognition.Confidence:P1})");
~~~

<code>OpenCvOcrImageInputFactory</code> 在输入释放前保留源图；Pipeline 释放所有临时 transform、warp、旋转和 ROI。调用方负责释放自己创建的图像输入、Registry 和其他资源。

## 模型和后端配置

检测模型通常按单图 `batch=1` 编排，但 Paddle DB 检测 Profile 现在也可以通过 `maximumBatch > 1` 声明真正的动态 `[batch,1,H,W]` 概率图；Decoder 会按 `PreparedVisualInput.BatchFrames` 逐行恢复源坐标。是否能在目标后端实际执行仍取决于 ONNX 导出图和后端 importer。识别模型可声明动态宽度和动态 batch。`OcrPipelineOptions.MaximumRecognitionBatch` 限制一次提交的文本行数量；同一配置里的 `MaximumConcurrency` 限制并发的完整 OCR 调用，**不等于 REC Session 数量**。通过构造 `OcrPipeline` 时的 `recognitionSessionOptions: new SessionOptions(2)` 配置两个独立识别通道，再由 Pipeline 将有界批任务分派给空闲通道。

2026-10-13 已在 TensorRT 11/RTX 3060 Laptop 上将动态 Batch→DB Decoder 合同扩展到 PP-OCRv4 Mobile/Server、v5 Mobile/Server、v6 Tiny/Small/Medium 共 7 个 DET 工件：每个模型由 DeploySharp Builder 创建 `min=1,opt=max=2` Engine，ORT CPU 与 TensorRT CUDA 均运行两个不同 ROI，并由同一 DB Decoder 恢复两行源坐标。七个精确工件均通过最大/平均概率差 `≤0.10/≤0.001` 的图级合同；所有已解码区域均一对一匹配且 polygon IoU ≥ `0.90`，本次最低匹配 IoU 为 `0.99676`。v5 Server 最大 raw 差 `0.0516`、均值差 `1.91e-5`。此证据不代表检测召回、CER/WER、完整 OCR 流水线、正式吞吐或其他设备/版本 TensorRT 支持，详见[七模型验证报告](../../eng/models/paddle-ocr/verification/paddleocr-core-detectors-tensorrt-dynamic-batch-decoder-20261013.md)。

随后先完成一条精确的 TensorRT 端到端 OCR 验证：v6 Small DET + v5 Mobile CLS + v6 Small REC 在 `demo_1.jpg` 上执行 `det → crop → cls → rec → merge`，TensorRT 10.11/CUDA 12.9/cuDNN 9.22 与 ORT CPU 均返回 16 个区域且有序文本 SHA 一致。该次单次总耗时观察为 ORT `1157.2269 ms`、TensorRT `1288.2198 ms`，详见[v6 Small 端到端报告](../../eng/models/paddle-ocr/verification/paddleocr-v6-small-tensorrt-full-pipeline-20261013.md)。

同日又按相同完整路径覆盖 v4 Mobile/Server、v5 Mobile/Server、v6 Tiny/Small/Medium 七组核心组合。`7/7` 均完成 TensorRT 10.11 Engine 构建与完整执行；每组 ORT CPU/TensorRT 都返回 16 个已识别区域，并且组内有序文本 SHA 一致。逐模型 ONNX/Engine SHA 和执行结果见[七组合完整流水线报告](../../eng/models/paddle-ocr/verification/paddleocr-core-tensorrt-full-pipeline-20261013.md)及[机器 JSON](../../eng/models/paddle-ocr/verification/paddleocr-core-tensorrt-full-pipeline-20261013.json)。其中耗时是排除 Engine 构建、但包含 Session/Pipeline 创建的一次调用观察，没有预热和分位数分布，GPU 也未锁频；因此不能用作正式 P50/P95 排名。该证据只关闭所列工件和单台 Windows 主机的执行/文本 parity，不关闭 CER/WER、检测召回、多页质量、跨设备 Engine 可移植性或其它运行时组合。

随后在同一设备上以正式 `5 次预热 + 50 次测量` 协议补做 TensorRT 稳态性能矩阵。七组核心组合均通过校验，统一使用设备本地 Builder Engine、Batch=8、独立阶段通道=2、复用已准备输入；完整流水线 P50/P95 为 v4 Mobile `191.256/200.739 ms`、v4 Server `243.536/258.356 ms`、v5 Mobile `196.894/210.507 ms`、v5 Server `488.820/522.446 ms`、v6 Tiny `80.169/91.488 ms`、v6 Small `143.523/152.854 ms`、v6 Medium `326.212/369.506 ms`。详见[正式性能报告](../../eng/models/paddle-ocr/verification/paddleocr-core-tensorrt10.11-steady-formal-20261013.md)、[CSV](../../eng/models/paddle-ocr/verification/paddleocr-core-tensorrt10.11-steady-formal-20261013.csv)和[协议校验 JSON](../../eng/models/paddle-ocr/verification/paddleocr-core-tensorrt10.11-steady-formal-20261013.validation.json)。这些是未锁频单机稳态观察，不能外推为跨设备排名、冷启动耗时或 OCR 准确率。

2026-10-17 又按同一 `5+50` 协议和当前源码复跑七组核心组合：TensorRT 10.11/CUDA 12.9/cuDNN 9.22、Batch=8、阶段通道=2、prepared-input reuse，`7/7` 通过。P50/P95 为 v4 Mobile `153.249/167.140 ms`、v4 Server `199.291/230.132 ms`、v5 Mobile `173.940/191.950 ms`、v5 Server `427.819/533.568 ms`、v6 Tiny `68.461/79.338 ms`、v6 Small `130.262/156.516 ms`、v6 Medium `276.124/293.220 ms`。见[当前复跑报告](../../eng/models/paddle-ocr/verification/paddleocr-core-tensorrt10.11-steady-formal-current-20261017.md)和[汇总 JSON](../../eng/models/paddle-ocr/verification/paddleocr-core-tensorrt10.11-steady-formal-current-20261017.json)。GPU 未锁频，这仍是单机精确 Engine/profile 的稳态观察，不代表跨设备排名、冷启动、吞吐或 OCR 质量。

对应的[ORT 1.28/OpenVINO CPU 正式矩阵](../../eng/models/paddle-ocr/verification/paddleocr-core-ort1.28-openvino-steady-formal-20261013.md)使用同一张图、prepared-input、Batch=8、两阶段通道和 `5+50` 协议覆盖七组核心组合。`14/14` 行通过，每行 50 次调用保持一个严格合同变体，逐模型在两个后端上的有序文本 SHA 和语义合同相同。P50/P95、阶段拆分和模型 SHA 见[原始 CSV](../../eng/models/paddle-ocr/verification/paddleocr-core-ort1.28-openvino-steady-formal-20261013.csv)；schema 3 [环境记录](../../eng/models/paddle-ocr/verification/paddleocr-core-ort1.28-openvino-steady-formal-20261013.csv.environment.json)保存实际加载的 ORT `1.28.0.0`、OpenVINO API `3.3.1.0`、OpenCV API `5.0.0.0` 版本/哈希，并明确该非 TensorRT 运行不包含 Engine 证据。CPU 未锁频且只有一个输入，不能把本轮数值当作跨设备排名、吞吐或 OCR 质量结论。

同一协议现已补齐[ORT 1.23.2 CUDA 正式矩阵](../../eng/models/paddle-ocr/verification/paddleocr-core-ort-cuda12-steady-formal-20261013.md)。七组核心组合 `7/7` 通过，每行 50 次调用保持一个严格合同变体，并逐模型匹配 CPU 矩阵的有序文本 SHA 和语义合同 SHA。完整流水线 P50/P95 为 v4 Mobile `69.960/160.443 ms`、v4 Server `117.107/201.558 ms`、v5 Mobile `70.545/117.800 ms`、v5 Server `105.702/168.476 ms`、v6 Tiny `22.658/28.969 ms`、v6 Small `43.704/50.460 ms`、v6 Medium `72.872/78.681 ms`。schema 3 [环境记录](../../eng/models/paddle-ocr/verification/paddleocr-core-ort-cuda12-steady-formal-20261013.csv.environment.json)保存 CUDA 12.9、cuDNN 9.22、DLL 搜索目录及实际 ORT 程序集哈希；[GPU 遥测](../../eng/models/paddle-ocr/verification/paddleocr-core-ort-cuda12-steady-formal-20261013.gpu.csv)显示 P0、活跃采样平均 1800 MHz，热降频和硬件 slowdown 均为 0。GPU/CPU 均未锁频，且输入只有一张图，因此仍不能当作 CER/WER、吞吐、跨设备排名或 PP-Structure 全模型支持。

[OpenCV DNN 动态宽度正式矩阵](../../eng/models/paddle-ocr/verification/paddleocr-core-opencv-dnn-steady-formal-20261013.md)也覆盖七组核心组合和 `5+50` 计时规则，但必须遵守这些图的静态 batch=1 合同，并用两个独立 Session 并发调度 16 个识别 batch。当前版本的[同 Batch=1 三图对照](../../eng/models/paddle-ocr/verification/paddleocr-core-three-image-opencv-ort-20261013.md)已证明七模型 `21/21` ORT/OpenCV 组合文本完全一致、置信度差 `≤0.01`、顶点差 `≤0.5 px`，所以与 Batch=8 矩阵的差异来自 Batch/动态宽度合同，不应归因于 OpenCV importer。

后续[固定宽度 320 正式矩阵](../../eng/models/paddle-ocr/verification/paddleocr-core-opencv-dnn-fixed320-steady-formal-20261013.md)避免了 crop 宽度变化时重复专门化和导入整张 ONNX 图。七组 `5+50` 均通过：P50 降低 `26.0%–49.0%`，报告的托管分配降低 `96.04%–99.76%` 至约 `17.5–46.9 MiB/次`；同固定宽度下，七组 ORT/OpenCV 的有序文本和语义合同 SHA 均一致。进一步的[同后端十页 SROIE 质量对照](../../eng/models/paddle-ocr/verification/sroie-v6-small-opencv-fixed320-quality-20261013.md)只改变 v6 Small 的宽度策略：固定 320 的诊断总耗时 P50/P95 下降 `68.0%/69.2%`，但 matched CER/WER 上升 `0.87/2.16` 个百分点，`531` 个对齐区域中有 `101` 个文字不同；检测数量和 polygon 不变。固定宽度因此仍是显式性能选项，不是默认值。使用时先在代表性标注数据上验证：`TextCropProfile fastCrop = cropProfile.WithFixedWidth(320);`。裁剪内容保持原宽高比，剩余列填充，不会把文字横向拉伸。两份 OpenCV 数据都不能与 Batch=8 后端直接排名。

动态宽度会产生 padding。<code>MaximumRecognitionPaddingRatio</code> 默认为 1.0，只把等宽文本行放在同一批次。目标后端经过实测后，可以适当调高该值以减少 batch 数，但要同时观察填充计算和显存占用。

P3 的并发与生命周期合同由[集中验证报告](../../eng/models/paddle-ocr/verification/paddleocr-p3-concurrency-contract-20261013.md)维护。当前回归覆盖动态 REC Batch、独立单通道 Session 池、批次顺序恢复、排队/活动取消、异常后复用、预取输入释放和幂等关闭；Visual 合同 `95/95`、ORT CPU OCR/lifecycle `5/5`、ORT provider/session-pool `12/12`、OpenVINO CPU OCR/lifecycle `4/4`，另有 OpenVINO provider/session-pool `9/9` 通过。两 Session 的真实设备对照仍需按后端调优，不能从 ORT 的收益外推到 OpenVINO。该证据不等于长时间 soak、跨设备吞吐或 PP-Structure 全模型并发通过。

补充的[PP-OCRv6 Small 真实 Session 池/预取合同](../../eng/models/paddle-ocr/verification/paddleocr-v6-small-session-pool-prefetch-20261018.md)在三张本地图像上实际创建 DET/CLS/REC 各两路独立 ORT Session，并以 `OcrPipeline.RunPrefetchedAsync(prefetch: 2)` 同时准备后续输入。测试验证三项结果按输入顺序返回、均有识别区域、结果合同互不塌缩，并验证成功路径由 Pipeline 释放回调创建的输入；峰值准备并发为 3。这是单主机的执行/所有权证据，不是吞吐排名、精度、5+50 性能、soak、GPU 或其它后端结论。

同一合同随后在远端 Ubuntu 22.04.5 / RTX 2060 主机上复跑；先补齐与项目匹配的 Ubuntu `JYPPX.OpenCV.Native` 原生运行时，再使用三张不同的公开 JPEG。详见 [Linux RTX 2060 证据](../../eng/models/paddle-ocr/verification/paddleocr-v6-small-session-pool-prefetch-linux-rtx2060-20261018.md)。该次执行仍使用 ORT CPU，显卡只作为设备元数据，不是 CUDA/TensorRT 性能证据；它关闭的是远端真实 DeploySharp 执行、顺序、预取和输入所有权切片，不关闭第二设备正式性能、锁频、soak 或全模型后端矩阵。

另有一份独立的[三图异步预取 5+50 性能报告](../../eng/models/paddle-ocr/verification/paddleocr-v6-small-session-pool-prefetch-linux-rtx2060-benchmark-20261018.md)：每个计时样本处理同组三张图并包含文件读取、预处理、完整 OCR 和结果合并，P50/P95 为 `2513.460/2857.975 ms` 每组三图。它是 ORT CPU 上的批次墙钟观察，CPU governor 为 `powersave` 且没有锁频或运行期时钟遥测；不能与单图或 GPU 数据直接比较，也不代表全模型性能矩阵。

当前 revision 的[P7 非外部回归记录](../../eng/models/paddle-ocr/verification/paddleocr-ppstructure-non-external-regression-20261013.md)覆盖 Core、Extensibility、ModelFactory、ModelPack.Json、Visual、Visual.OpenCV、ONNX Runtime、OpenVINO、OpenCV 和 TensorRT，共 `912 passed / 14 skipped / 0 failed`；同时 `14/14` 源码项目和 `10/10` 示例项目构建通过，维护范围 `671/671` 个本地 Markdown 链接有效。该记录来自 dirty working tree，14 个跳过项仍是明确的外部模型、native runtime、网络或平台门控，不能换算成支持结论，也不替代 clean-checkout、质量、性能、打包或 soak 门禁。

随后在提交 `d02638e` 的 detached clean checkout 中完成了[完整解决方案构建](../../eng/models/paddle-ocr/verification/paddleocr-ppstructure-clean-checkout-build-20261014.md)：`DeploySharp.sln` 的 `37` 个项目还原和 Release 构建均通过，编译错误为 `0`。该门禁排除 `applications`，并只说明源码可复现构建，不代表 native runtime、外部模型、准确率或性能支持。

针对这次 clean 回归发现的 OpenCV DNN 兼容合同和 PaddleOCR release-admission 漂移，已在提交 `f23d19f` 的干净 checkout 中完成[聚焦复测](../../eng/models/paddle-ocr/verification/paddleocr-clean-checkout-focused-tests-20261014.md)：OpenCV `32/32`、ModelFactory `63/65`（2 个显式外部门控跳过），无失败。它只确认这两组修复已进入提交，不替代完整回归、外部模型质量或性能验证。

之后在提交 `a3c3fdc` 的 detached clean checkout 中完成了[完整非外部回归](../../eng/models/paddle-ocr/verification/paddleocr-clean-checkout-non-external-regression-20261014.md)：14 个测试项目共 `938 passed / 0 failed / 15 skipped`（953 项），命令退出码为 `0`。跳过项仍是外部模型、公开下载、checkpoint 或运行时门控，不转换为模型或后端支持结论；本轮关闭的是 P7 clean-checkout 回归门，不是质量、正式性能、soak、跨设备或 PP-Structure 全后端门。维护范围链接审计为 `684` 条、断链 `0`。

### 连续帧异步预取

视频或摄像头场景可以把下一帧的解码、缩放和输入张量准备，与当前帧的 OCR 推理重叠。`OcrPipeline.RunPrefetchedAsync` 接收输入描述和一个准备回调，按输入顺序返回 `OcrResult`；`prefetch` 控制最多提前保留的准备任务数量，`OcrPipelineOptions.MaximumConcurrency` 仍限制同时进入 OCR 执行阶段的帧数。

```csharp
IReadOnlyList<OcrResult> frames = await ocr.RunPrefetchedAsync(
    framePaths,
    async (path, token) =>
    {
        token.ThrowIfCancellationRequested();
        return new OpenCvOcrImageInputFactory()
            .CreateFromFile(path, "images", detectorOptions);
    },
    prefetch: 2,
    options: new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(2)),
    cancellationToken);
```

同步准备回调也有同名重载。回调创建的 `IOcrImageInput` 由该方法临时拥有，在成功、取消或异常后都会释放；回调返回的输入不应在调用完成前由其它线程释放。该接口只是**帧级流水线重叠**，不会把多帧拼成一个模型 Batch；需要真正的张量 Batch 时，应使用识别器的动态 batch 或底层 `VisualPipeline.RunManyAsync`。取消会停止后续准备并等待已启动任务观察结束，避免解码图像和 native Mat 留在后台。

一个 ModelPack 可以同时携带检测和识别 ONNX，或对应的 OpenVINO IR XML/BIN，并通过 <code>deploysharp.ocr.*</code> 扩展键绑定 Profile、字符集和预处理版本。字符表的 blank、unknown、Unicode 顺序必须和 logits 导出一致。

## 可选源像素质量诊断

质量诊断用于定位低对比度、小区域或采样清晰度不足等线索，不自动增强图像、不拒绝低分行、不改变识别配置。当前源码提供 `OcrExecutionOptions.WithPixelQuality(...)`，默认关闭；旧 NuGet 包需升级到包含该接口的版本。

```csharp
var execution = new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(10))
    .WithPixelQuality(new OcrPixelQualityOptions(
        maximumSamplesPerArea: 4096,
        maximumRegions: 128,
        maximumSamplesPerCall: 1048576));
OcrResult result = await ocr.RunAsync(input, execution, cancellationToken);
Console.WriteLine($"源图亮度均值: {result.PixelQuality?.MeanLuminance}");
foreach (OcrRegionResult line in result.Regions)
{
    OcrPixelQualityDiagnostics? q = line.PixelQuality;
    Console.WriteLine($"{line.Region.SourceIndex}: samples={q?.SampleCount}, " +
        $"contrast={q?.LuminanceStandardDeviation}, laplacianVariance={q?.LaplacianVariance}");
}
```

### 采样对象与结果含义

整图与每个原 DET 多边形各评估一次，直接读取解码后的**源像素**，不是 REC resize、padding 或归一化后的张量。区域指标保留原始清晰度信息，但不代表校正后的 crop 质量，更不是单字质量；滑窗和方向重试不会反复采样。

| 字段 | 定义与限制 |
| --- | --- |
| `MeanLuminance` | 8 位亮度均值，范围 0～255 |
| `LuminanceStandardDeviation` | 亮度总体标准差，反映采样对比度，不是识别率 |
| `DarkPixelFraction` / `LightPixelFraction` | 亮度 ≤8 / ≥247 的比例；黑字白纸也可能很高，不能据此断言欠曝或过曝 |
| `LaplacianVariance` | `L+R+U+D−4C` 的总体方差；使用原始相邻 1 像素，不把采样间距当作像素尺度；噪声也可能使其升高 |
| `MeanAbsoluteGradient` | `(|R−L|+|D−U|)/4` 的均值；是纹理/边缘线索，不区分文字、噪声和背景 |
| `RegionMinimumEdgePixels` | DET 多边形最短边；整图为 null，不应冒充字形或笔画高度 |
| `PlannedSampleCount` / `SampleCount` / `NeighborhoodCount` | 计划网格中心数、在区域内的有效中心数、四邻域均有效的中心数 |

中心以有界规则网格分布在多边形与图像边界相交的包围框内，只接受像素中心落在多边形中的样本；不创建 mask 或整图灰度副本。邻域只在四个相邻像素也属于区域时参与计算。没有有效中心时像素指标为 null；不足两个有效邻域时拉普拉斯方差为 null，不把缺失数据记为“质量好”。规则采样可能漏掉窄小结构或与周期纹理重合，不能与不同采样预算/分辨率的报告直接比较。

OpenCV 适配器使用 `Y=(77R+150G+29B+128)>>8`，支持 8 位 Gray/BGR/BGRA 和非连续 Mat，Alpha 与现有 OCR 预处理一致被忽略。`OpenCvOcrPixelQuality.Analyze(mat, ...)` 可独立检查借用的 Mat；调用期间必须防止其他线程修改或释放它。对 `OpenCvOcrImageInput` 的调用在其读锁内执行，复用已解码图像，不读取 GPU 张量。

### 资源、坐标和后端

- 每区域最多 4096 个中心，每中心最多读取 5 个亮度值。默认每次最多 128 个 DET 区域，另加整图；保守预算为 `(区域数+1)×maximumSamplesPerArea`。超出区域/总中心/结果内存限制返回 `DS-VISUAL-4102`，不静默漏报后面的区域。
- 检查与推理共享取消、超时、输入所有权和调用并发限制。统计计入 `CropAndBatch` 及端到端总时间，不藏在计时之外。不开启时不产生逐像素工作。
- `InputSize/InputPolygon/InputRegionIndex` 表示**评估时**的输入坐标与索引。整图方向恢复、ROI 投影和区域重新编号仍保留原证据，不将旧统计冒充新坐标的重新测量。多 ROI 合并保留各获选行的证据，合并后的整图 `PixelQuality` 为 null；可查看各原 ROI 结果的整图指标。
- `IOcrPixelQualityInput` 是可选输入能力，自定义适配器可调用纯托管 `OcrPixelQualityAnalyzer.Analyze` 提供亮度读取器。原 `IOcrImageInput` 接口不变；显式启用但适配器不支持时返回 `DS-VISUAL-4106`，不静默忽略。使用 OpenCV 输入时可配合不同推理后端；这不表示纯 CUDA 输入已经实现设备端质量分析。

当前没有通用质量总分、默认模糊阈值、噪声/JPEG 退化分类、精确字高或自动增强。需要在自己的图像分辨率、采样配置与标注数据上确定业务阈值。检查对文本/坐标/token 指纹没有影响；报告属于诊断而不是识别准确率证明。

## 识别裁剪的分阶段质量诊断

源图 `PixelQuality` 只描述原 DET 区域，不能代表透视校正与缩放后真正送入 REC 的内容。需要比较这两者时，在识别裁剪 Profile 上显式配置：

```csharp
TextCropProfile crop = recognitionProfile.CropProfile!
    .WithCropProcessing(new OcrCropProcessingOptions(
        maximumSamplesPerStage: 1024,
        maximumCropsPerCall: 1024));
// 将 crop 传给 OcrPipeline；图像输入使用 OpenCvOcrImageInputFactory。
```

每行 `OcrRegionResult.CropDiagnostics` 按实际识别窗口顺序保存：

- `Rectified`：透视/仿射校正与直角旋转完成后的局部图像，尚未 resize。
- `Content`：resize 后的有效内容，不包含右侧 tensor padding，也未做归一化。
- `TensorSize`：实际补齐张量的宽高，可能宽于 `Content.InputSize`。
- `InputRegionIndex`、`InputQuadrilateral`、`Orientation`：评估当时的区域、实际裁剪四角和附加旋转。滑窗四角已编码父行方向，因此窗口自身的附加旋转可为 0；不能再次套用父行旋转。

这些统计采用与源像素诊断相同的定义，但坐标是 crop 局部空间；其内部 `InputPolygon/InputRegionIndex` 为 null，不是缺少来源。ROI 投影、结果重新编号、全图方向恢复不改写已采集的证据。启用方向重试时，每个 `OrientationRetry.Attempts` 都保留自身诊断，最终行只引用获选尝试；不把多个窗口的统计平均后冒充一个真实裁剪。

默认 `CropProcessing=null`，不增加采样。只启用诊断不会改变张量、文字或既有结果 SHA。适配器复用既有 warp/resize 缓冲，不再次裁剪或保存像素；结果仅持有不可变数值。物理裁剪上限包含初次 REC、滑窗、重试以及 minimum-batch 重复补齐行，超限报 `DS-VISUAL-4102`，不静默截断。逐裁剪保守内存预算也计入 `MaximumResultBytes`，采样计入 `RecognitionPreparationWork` 与流水线总时间，不藏在计时外。

自定义输入可实现 `IOcrCropProcessingInput`，返回 `OcrPreparedCropBatch`，一条物理输入对应一份诊断。成功构造后 wrapper 拥有 `PreparedVisualInput`，调用方必须释放；诊断不允许持有 native/GPU 缓冲。未实现可选接口时显式开启返回 `DS-VISUAL-4107`。OpenCV 输入的 `PrepareRecognitionBatch` 只接受未启用该功能的 Profile，启用后应使用 `PrepareProcessedRecognitionBatch`，防止直接调用时静默丢弃配置。TensorRT device-only 输入尚未实现该可选接口；OpenCV 输入与推理后端是独立选择。

采样上限每阶段 1～65536、每调用物理 crop 1～4096。使用固定图像、插值、角点、采样预算与尺寸做对照；resize 本身就会改变锐度和方差，因此不能直接把两阶段数值之差等同于识别质量损失。当前没有经过标注数据标定的噪声/JPEG 分类、精确字高或通用拒绝分数。

### 可选局部增强

诊断与增强独立：不配置 `WithEnhancement` 就不改像素。当前提供八种局部操作，每个 Profile 选择一种。对比度类操作和自适应阈值仅在校正后 crop 的亮度标准差处于 `[minimumContrast, lowContrastThreshold)` 时应用；高斯去噪、反遮罩锐化、阴影归一化和 JPEG 伪影抑制各自使用显式质量门限；局部放大只在明确配置或作为重试候选时执行：

```csharp
var processing = new OcrCropProcessingOptions(1024, 1024)
    .WithEnhancement(new OcrCropEnhancementOptions(
        OcrCropEnhancementMode.ContrastNormalize,
        lowContrastThreshold: 32,
        minimumContrast: 1,
        targetStandardDeviation: 64,
        maximumGain: 3,
        maximumPixelsPerCrop: 1024 * 1024));
TextCropProfile crop = recognitionProfile.CropProfile!.WithCropProcessing(processing);
// 若明确需要灰度局部均衡，改用 GrayClahe，并配置 claheClipLimit/claheGridSize。
// 含脉冲/高频噪声的候选可改用 GaussianDenoise，并设置 noiseThreshold/denoiseKernelSize。
// 光照不均且对比度不足的候选可改用 AdaptiveThreshold，并配置 adaptiveBlockSize/adaptiveConstant。
// 低清晰度候选可改用 UnsharpMask，并设置 sharpnessThreshold/sharpenAmount。
// 小字候选可改用 LocalUpscale，并设置 upscaleFactor/upscaleInterpolation。
// 缓慢变化光照候选可改用 ShadowNormalize，并设置 shadowVariationThreshold/shadowKernelSize。
// 块边界或孤立高频伪影候选可改用 JpegArtifactSuppress，并设置 jpegArtifactThreshold/jpegKernelSize。
```

- `ContrastNormalize`：`gain=min(maximumGain, targetStandardDeviation/实测标准差)`，以实测亮度均值为中心对颜色通道执行线性映射、四舍五入和 0～255 饱和。保留 RGB/BGR 的通道关系，不转换成灰度；BGRA 的 alpha 不变且仍不参与 REC。颜色饱和可能改变色差，输出标准差不保证等于目标。
- `GrayClahe`：先按 OCR 同一整数亮度系数转为灰度，再执行 OpenCV CLAHE。`claheClipLimit` 默认2，`claheGridSize` 默认8表示每轴8块，不是每块8像素。RGB/BGR 模型输入中会复制灰度到三个通道，之后依然使用模型自己的均值、尺度和补边。对于依赖颜色的文本，必须验证后再选择。
- `GaussianDenoise`：当 `Rectified.LaplacianVariance > noiseThreshold` 且中间裁剪像素不超限时，使用 OpenCV `GaussianBlur` 在原通道数上执行一次轻度平滑；默认 `denoiseKernelSize=3`、`denoiseSigma=0`（由 OpenCV 根据核尺寸选择）。低于或缺少该指标时不修改像素并报告 `SufficientQuality`。拉普拉斯方差同时受文字边缘、纹理、采样和分辨率影响，不是经过标定的噪声分类器；默认不启用，建议只作为保留原文的重试候选。
- `AdaptiveThreshold`：低对比度且校正 crop 的宽高都不小于 `adaptiveBlockSize` 时，先按 OCR 亮度系数转灰度，再使用 OpenCV Gaussian adaptive threshold 输出二值图；默认 `adaptiveBlockSize=15`、`adaptiveConstant=5`，奇数邻域范围3～31。灰度结果会按模型通道数复制，颜色和细灰笔画可能丢失；尺寸不足或对比度已足够时报告 `SufficientQuality`/`SufficientContrast`，不修改像素。
- `UnsharpMask`：当校正 crop 的亮度标准差不低于 `minimumContrast` 且 `Rectified.LaplacianVariance < sharpnessThreshold` 时，使用一次 GaussianBlur 和 `AddWeighted` 恢复局部边缘；默认 `sharpnessThreshold=512`、`sharpenKernelSize=3`、`sharpenAmount=0.5`、`sharpenSigma=1`。低于最小变化、缺少指标或已经超过清晰度门限时报告 `SufficientQuality`，不修改像素；该指标不是标定的模糊分类器。
- `LocalUpscale`：使用 `upscaleFactor`（1～4 之间的有界倍数，默认2）和独立的 `upscaleInterpolation`（默认 `Cubic`）将校正后的中间 crop 放大，再进入原有 resize、padding 和归一化。放大后的像素数受 `maximumPixelsPerCrop` 限制，超限报告 `DS-VISUAL-4102`；这是插值候选，不是超分辨率模型，也不保证增加信息量或准确率。`Enhanced.InputSize` 会记录放大后的尺寸，便于与原始 `Rectified` 对照。
- `ShadowNormalize`：当校正 crop 的亮度标准差不低于 `shadowVariationThreshold`（默认16）时，以奇数 `shadowKernelSize`（默认31，范围3～127）做 Gaussian 背景估计，再以 `source-background+128` 的饱和高通形式削弱缓慢变化光照。操作保持原通道和尺寸，复用线程本地 Mat；背景核可能同时削弱大字笔画或低频纹理，亮度标准差也不能证明图中存在阴影。
- `JpegArtifactSuppress`：当 `Rectified.LaplacianVariance > jpegArtifactThreshold`（默认256）时，在原通道上应用一次奇数 `jpegKernelSize`（默认3，范围3～7）中值滤波，作为块边界、振铃或孤立高频伪影的保守候选。某些输入执行后可能逐值不变；拉普拉斯门限不是 JPEG 编码检测，滤波也可能损失细字，必须通过原文保留重试和标注数据决定是否采用。

执行顺序是 `warp/rotate → Rectified采样 → 质量门限 → 可选增强/Enhanced采样 → resize → Content采样 → 原有padding/normalize`。不重复 DET/CLS、不改变检测区域、识别宽度、坐标或字典。超低变化（纯色等）和已达到阈值的裁剪不增强，也不分配增强像素缓冲。OpenCV 延迟创建线程本地工作区，复用 LUT、Mat 和 CLAHE 对象；不产生每像素托管对象，输入销毁时释放工作区。应用增强多一次 crop 内处理和可选灰度缓冲，并非零开销。

`CropDiagnostics.EnhancementDecision` 区分 `NotConfigured`、`InsufficientVariation`、`SufficientContrast`、`SufficientQuality`、`Applied`；`Enhancement` 保存参数，`Enhanced` 仅在实际应用时保存增强后、resize 前的统计，`AppliedGain` 仅在线性方式应用时非空。每次方向重试各自计算质量门限并保存证据。增强实际应用时张量/文字/置信度/SHA均可能改变，不能继续宣称与原始路径逐值等价。

门限是可配置启发式，不是通用质量判据。默认32/1只是起始参数，白底小文字可能因空白占比高而被判低对比度；噪声也可能被增强。请在自己的标注集上比较逐行准确率、CER/WER和端到端P50/P95，不能只看锐度/置信度上升。最大增益8，CLAHE clip≤16、每轴分块2～16，每裁剪增强像素数默认1048576、最大16777216，超限 `DS-VISUAL-4102`，不静默漏处理。像素循环检查取消；单次 native CLAHE 调用在前后检查取消，不能中断其内部执行。

当前仍无经过标定的自动噪声、阴影或 JPEG 退化分类，也没有换模型重试；八种操作都是有界、显式候选，不能冒充退化分类器、超分辨率模型或准确率保证。已实现对应门限路径和下面的有限低置信度增强重试，全部默认关闭。

功能回归示例：2026-09-17，在 Windows RTX3060 Laptop、ORT1.23.2 CPU/CUDA 下，使用同一 `demo_1.jpg`，v4 mobile/v5 mobile/v6 tiny、B4/单通道、Clamp320，对 Report/ContrastNormalize/GrayClahe 共18组运行成功。Report 的文字和完整合同SHA与关闭增强基线一致；本图各模型均仅第12号区域触发默认门限。线性方式文字不变但置信度/token使合同SHA改变；v5 CLAHE 将 `(成品包材)` 变成 `（成品包材）`，置信度从CPU基线0.848219升到0.9010367，不能据此判断更准确。该轮只有1次预热/3次采样且存在开发负载，不作为稳定性能或准确率结论；复现时请保存 sidecar 和原文差异，而非仅比较置信度。

### 低置信度单候选增强重试

需要“先保留原始识别，再检查局部增强是否值得采用”时，不要在首轮 `CropProcessing` 中设置增强，而应显式启用：

```csharp
var retry = new OcrEnhancementRetryOptions(
    new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe),
    confidenceThreshold: 0.9f,
    selectionPolicy: OcrEnhancementSelectionPolicy.PreserveOriginal,
    minimumConfidenceGain: 0.05f,
    maximumRegionsPerImage: 16,
    maximumCropsPerImage: 128);

TextCropProfile crop = recognitionProfile.CropProfile!
    .WithCropProcessing(new OcrCropProcessingOptions(1024, 1024))
    .WithEnhancementRetry(retry);
// 将 crop 传给 OcrPipeline。通过结果行的 EnhancementRetry 查看对照。
```

`WithEnhancementRetry` 在未配置 `CropProcessing` 时自动启用默认有界裁剪诊断，首轮仍不增强。可以先设置诊断预算，但首轮直接增强与增强重试不能组合，任一配置顺序都明确抛出 `ArgumentException`，避免把已经增强的结果称为“原始结果”。没有调用此方法时，不引入重试或额外诊断。

执行顺序为 `DET → CLS/初次REC → 可选方向重试 → 可选动态宽度重试 → 增强候选 → 最终结果`。`RecognizeOnlyAsync` 从调用方区域的初次 REC 开始，跳过 DET/CLS，但执行同一组已配置的方向、宽度和增强重试。它会保留初次与候选裁剪诊断，并与完整流水线共享物理 crop、结果字节、Session 池和取消边界；配置了裁剪处理或增强重试时，输入必须实现 `IOcrCropProcessingInput`，否则明确报告 `OcrCropProcessingUnavailable`，不会静默跳过策略。

增强只考虑当前所选识别结果为空或置信度低于阈值的行；默认阈值0.8，示例0.9是业务配置，不是通用推荐值。再读取该行已有的 `Rectified` 统计，至少一个裁剪满足增强质量门限才生成候选。没有合格裁剪就标记 `NoEligibleCrop`，不再跑REC。对于滑窗行，重试整行窗口，只有质量合格的窗口应用增强；其他窗口保持原像素，以保证完整行合并与接缝上下文。候选从原图重新裁剪，不在上次增强像素上叠加处理，也不重新解码、DET或CLS。

候选保留每个原始窗口的实际 `TensorWidth`，只与相同宽度候选组批，不因单行重试而缩小或扩大原始补边上下文；模型的batch维仍可能变化，minimum-batch仍需补齐。这样避免把宽度改变导致的分数变化误当作增强效果，但不同batch/backend数值仍不保证逐位一致。固定宽度引擎合同不变。

每行最多一个候选，不尝试所有增强组合或不断循环。`PreserveOriginal` 是默认选择策略：实际运行候选并留下结果，但最终文字和原始合同SHA不变。选择 `ConfidenceGain` 后，非空候选仅在严格优于当前非空结果且增量达到 `minimumConfidenceGain` 时替换；平局、增量不足和空候选保留原结果，非空候选可以替换空原文。`ConsensusConfidenceGain` 只对多配方生效：候选必须与至少另一个配方产生完全相同的非空文本，并通过同一置信度增益门槛；单配方或文本不一致时保留原文。两种替换策略都只是可选启发式，不能保证准确率，尤其不能保证全角/半角和业务字段符合预期。

结果行 `EnhancementRetry` 为 null 表示未启用或当前识别置信度足够；非空时保存：

| 字段/决策 | 含义 |
|---|---|
| `Options` | 实际门限、操作、选择策略和资源上限 |
| `Original` | 增强前所选结果，位于可选方向重试之后；包括原文、token、置信度、宽度、窗口和裁剪诊断 |
| `Candidate` | 唯一候选的相同字段；null表示未运行额外REC |
| `NoEligibleCrop` | 没有裁剪满足质量门限 |
| `RegionLimit` | 前面的合格行用完接收额度；不丢原文 |
| `PreservedByPolicy` | 已运行候选，按显式策略保留原文 |
| `InsufficientGain` | 候选为空、平局或增量不足 |
| `CandidateSelected` | 按配置启发式选中候选，不是正确性判定 |
| `ConsensusMatchCount` | 与 `Candidate` 完全相同的非空文本候选数；`ConsensusConfidenceGain` 至少要求为 `2` |

若先做方向重试，顶层 `OrientationRetry` 仍保留该阶段的全部尝试，其 `SelectedIndex` 表示增强之前的方向胜者；最终文字是否来自增强，应看 `EnhancementRetry.Decision`。顶层 `CropDiagnostics` 对应最终选中的识别。ROI重新编号同步更新两份识别结果和窗口的SourceRegionIndex，但诊断四角/旋转/评估索引保持原来源；全图方向恢复也不将旧证据冒充恢复后的坐标。

资源上限有两层：`maximumRegionsPerImage` 按阅读顺序接收质量合格的低分行；超出保留原文并标记。`maximumCropsPerImage` 限制增强阶段额外物理行，包含滑窗和模型minimum-batch重复补齐，超限整次调用报 `DS-VISUAL-4102`。此外，初次REC、方向重试、增强重试共享 `CropProcessing.MaximumCropsPerCall`、`MaximumResultBytes`、取消和超时。原始和全部候选诊断/识别trace一并计入保守结果预算，错误不返回部分成功；Batch/Session池复用现有模型并发边界，不创建无上限的任务或新模型会话。

候选规划、采样、增强、REC、合并计入 `Recognition` 墙钟和端到端总时间；各批次准备/推理/后处理累计到详细work计时，`RecognitionBatchCount`包含额外批次。阶段work在并发时可能大于墙钟。启用重试即使选择保留原文，也会产生真实计算成本；不要把它当作免费精度开关。单候选与多配方都不改变 DET/CLS 或模型会话合同，真实准确率仍需标注集验证。

### 有界多配方候选（B3b）

当一种增强无法覆盖全部现场退化时，可以把最多四种**彼此独立**的增强操作声明为有序候选。每种配方都从原始裁剪重新取样；不会把第一种配方的输出再交给第二种配方，也不会产生无限重试链：

```csharp
var retry = OcrEnhancementRetryOptions.CreateMany(
    new OcrCropEnhancementOptions[]
    {
        new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe),
        new OcrCropEnhancementOptions(OcrCropEnhancementMode.LocalUpscale, upscaleFactor: 2),
        new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask, sharpnessThreshold: 768)
    },
    confidenceThreshold: 0.9f,
    selectionPolicy: OcrEnhancementSelectionPolicy.PreserveOriginal,
    maximumRegionsPerImage: 8,
    maximumCropsPerImage: 32);

TextCropProfile crop = recognitionProfile.CropProfile!
    .WithCropProcessing(new OcrCropProcessingOptions(1024, 1024))
    .WithEnhancementRetry(retry);
```

`CreateMany` 会复制输入集合，拒绝空集合、空元素和超过四种配方；第一个配方仍通过兼容属性 `Enhancement` 暴露。Pipeline 先按阅读顺序接收低置信度行，再按配方顺序批量执行。每个配方只提交本行真正通过该配方质量门限的裁剪，空配方不会创建 REC batch；每个配方内部继续复用现有宽度分组、minimum-batch 补齐、Session 池和取消令牌。所有配方共享同一 `maximumRegionsPerImage`、`maximumCropsPerImage`、`MaximumCropsPerCall` 与结果字节预算，任何物理 crop 或结果预算超限都整次失败，不返回部分成功。

结果行的 `EnhancementRetry.Candidates` 按实际执行顺序保存候选，`CandidateRecipeIndices` 给出对应的 `Options.Enhancements` 索引。某行不满足第一种配方而只满足第二种时，索引仍为 `[1]`，不能假定候选列表位置就是配置位置。`Candidate` 为兼容快捷属性：选择策略替换成功时返回被选候选；`PreserveOriginal` 时返回第一个已执行候选；没有候选时为 `null`。`SelectedCandidateIndex` 只有 `ConfidenceGain` 或 `ConsensusConfidenceGain` 真的替换原文时才有值。

`PreserveOriginal` 适合先收集真实现场证据，最终文字保持首轮结果但保留每个候选的文字、token、置信度、窗口和裁剪诊断。`ConfidenceGain` 会在所有候选完成后，从非空且超过原文 `MinimumConfidenceGain` 的候选中选择置信度最高者；`ConsensusConfidenceGain` 还要求最高候选的文字至少被另一配方逐字复现，避免把单一增强配方的置信度误当成正确性。两者都不是准确率判定，也不理解业务字段语义。多配方按顺序串行执行以保持确定性，墙钟 `Recognition` 只记录一次外层耗时，详细 work 计时和 `RecognitionBatchCount` 累加每个配方的真实 batch；即使保留原文，也应将额外成本纳入 P50/P95 基准。

### B2/B3 安全与成本证据

对原始低分辨率噪声扫描图的最新验证见 [FUNSD 自然扫描报告](../../eng/models/paddle-ocr/verification/paddleocr-funsd-natural-low-quality-enhancement-20261023.md)。固定十张测试页与此前 40 页受控增强父图不重叠，共 1,769 个有效词区，未生成退化或调参；ORT/OpenVINO 的原文、全部候选和最终文本全部一致，2 处浮点边界选择元数据差异单独保留。两后端的 CER 编辑数均从 `307/8,582` 增至 `311/8,582`，WER 从 `214/1,775` 增至 `219/1,775`，因此不能将三配方共识视为准确率保证。来源仅给出噪声扫描的整体描述，没有逐图严重度标签。

增强成本现有独立的[配对 5+50 测量](../../eng/models/paddle-ocr/verification/paddleocr-funsd-paired-enhancement-cost-20261023.md)：固定第一页前 16 个词区，独立创建禁用增强和三配方增强 Pipeline，按 AB/BA 交替执行，两后端各 50/50 文本合同稳定。每 16 词调用的 baseline/enhanced P50 为 ORT `69.17/258.29 ms`、OpenVINO `91.53/364.82 ms`；配对耗时比中位数为 `3.68×/3.93×`。这包含 crop、REC、CTC 与重试，不含页面输入准备、DET/CLS；是强制三个候选的成本切片，不是整页时延或生产策略。该 16 词子集 CER/WER 无收益，默认增强继续关闭；更细的自然质量分层、业务字段误纠预算和代表性成本门仍待验收。

增强策略不能只看置信度提升。仓库脚本 `eng/models/paddle-ocr/scripts/Summarize-OcrEnhancementEvidence.py` 可对集成测试输出的逐行 JSON 计算：候选被选中的数量、候选 CER/WER 高于原文的误纠率、全部记录和已选候选的 P50/P95 额外耗时、多后端决策/最终文本一致性，以及共识策略的置信度增益阈值曲线。例如 [2026-10-11 汇总](../../eng/models/paddle-ocr/verification/ocr-enhancement-safety-cost-20261011.md) 中，SROIE parent-linked crop 的已选子集没有 CER 变差行，而 FUNSD word-crop 子集有 `5/14` 行 CER 变差（`35.71%`）。这正是保持 `ConfidenceGain` 和 `ConsensusConfidenceGain` 显式 opt-in 的依据；该报告是有标签 crop 的边界诊断，不是页/行级鲁棒性、准确率或默认阈值批准。新增退化数据后应重新生成机器 JSON 与 Markdown，并把最大允许误纠率、延迟预算和有效样本量作为业务决策门，而不是只比较平均置信度。

2026-10-23 又使用 PP-OCRv6 Small，在 SROIE 十页的 542 个带词级真值 crop 上比较 `ContrastNormalize`、`GrayClahe`、`UnsharpMask` 三候选共识；ORT CPU 与 OpenVINO CPU 的原文、候选文本、最终选择和候选索引 `542/542` 一致，但被选择的区域 CER 改善/恶化/相同为 `4/5/344`，WER 为 `3/4/346`，整体 CER/WER 编辑数都各增加 1。之后穷举 ORT `348` 个、OpenVINO `350` 个非空候选置信度增益切点，没有找到同时满足“零 CER 误纠且至少一项 CER 改善”的阈值；零误纠点只保留一两个 CER 持平区域。该事后扫描仅描述此样本，不能用来定生产阈值。此样本是自然来源收据，但数据没有成像质量标签，所以不能称为自然低质量验证；它进一步说明后端一致和置信度共识不等于正确。详见[报告](../../eng/models/paddle-ocr/verification/paddleocr-sroie-natural-source-enhancement-20261023.md)和[逐区域 JSON](../../eng/models/paddle-ocr/verification/paddleocr-sroie-natural-source-enhancement-20261023.json)。默认增强保持关闭，B1b/B2/B3 门禁未关闭。
当前还提供独立的 `eng/models/paddle-ocr/scripts/Evaluate-PaddleOcrEnhancementSafetyGate.ps1`，可对汇总报告执行 fail-closed 门禁。以 FUNSD 40 页、240 条 parent-linked 记录为例，[2026-10-17 门禁报告](../../eng/models/paddle-ocr/verification/ocr-enhancement-safety-gate-funsd40-20261017.md) 在 ORT/OpenVINO 均为 `blocked`：样本量和 P95 成本满足示例阈值，但 CER/WER 误纠率分别为 `31.25%/12.50%`。这不是自然质量或业务准确率结论，而是明确阻止把受控退化数据当作默认增强批准的安全边界。

[2026-10-13 FUNSD 40-crop 共识报告](../../eng/models/paddle-ocr/verification/paddleocr-funsd40-consensus-safety-cost-20261013.md) 改用真值完整 crop 的 `RecognizeOnlyAsync`，避免检测失败掩盖增强候选。修复识别-only 原先不执行重试的接口缺口后，ORT CPU/OpenVINO CPU 在 240 条记录上的决策、选择标记、最终文本、候选索引和共识计数全部一致。`ContrastNormalize + GrayClahe + UnsharpMask` 在 `minimumGain=0` 时形成 177 条候选共识并选择 115 条，其中 2 条 CER 改善、4 条恶化、109 条不变，平均 CER `0.3060 → 0.3067`；P50/P95 候选墙钟观察分别为 ORT `76.21/144.57 ms`、OpenVINO `34.87/51.64 ms`。全量事后阈值扫描没有找到“零 CER 恶化且仍有至少一条 CER 改善”的档位；进一步按父 crop 做确定性 5 折验证，保证同一原图的六种条件不跨校准/验证集，五折都因校准集不存在安全且有收益的阈值而弃权。该结果只关闭接口和受控数据的分组验证缺口，不批准默认配方或阈值；自然低质量独立数据集、业务字段误纠门和正式 5+50 成本协议仍未完成。当前耗时来自顺序质量运行，不是正式性能排名。

多配方覆盖全部 `OcrCropEnhancementMode`，包括 `LocalUpscale`、`ShadowNormalize` 和 `JpegArtifactSuppress`；每种模式仍只按自身显式统计门限决定是否执行，不自动判断真实退化类别，不切换 DET/CLS/REC 模型，也不替代标注集评估。真实部署建议先用 `PreserveOriginal` 导出候选差异，再按 CER/WER、字段校验通过率、召回率和端到端延迟选择是否启用 `ConfidenceGain` 或更保守的 `ConsensusConfidenceGain`；后者需要至少两个配方，额外成本和候选不一致率必须单独纳入评测。

验证示例（2026-09-17）：同一Windows RTX3060 Laptop、ORT1.23.2 CPU/CUDA、demo_1.jpg、B4/单通道、Clamp320、1次预热/3次短测，v4 mobile/v5 mobile/v6 tiny的关闭/保留/显式选择共18组通过，前两种模式的完整合同SHA均与既有基线一致。置信度阈值0.9时，v5的第6号区域因质量门限不合格不重试，第12号区域生成一个候选；保持原始TensorWidth=282时，该行CPU置信度0.848219→0.9010367，显式选择把半角括号改为全角，默认保留策略不改原文。v4/v6本图没有低分行触发。另以v6 tiny、SlidingWindow320、方向/增强阈值1、最多接收1行、增强质量阈值128作组合边界测试，两后端均保留2窗口候选及原始方向证据；这些强制参数是测试用例，不是生产推荐值。没有标注真值，不据此声称准确率提高。

### 动态宽度低置信度重试（B3b-width）

长文本在首轮 `MaximumWidth` 限制下可能被压缩。对于声明了动态宽度输入的识别器，可以在首轮结果为空或置信度低于阈值时，**有界地再跑一次更宽的 REC 候选**：

```csharp
var widthRetry = new OcrWidthRetryOptions(
    candidateMaximumWidth: 1280,
    confidenceThreshold: 0.8f,
    selectionPolicy: OcrWidthRetrySelectionPolicy.PreserveOriginal,
    minimumConfidenceGain: 0.05f,
    maximumRegionsPerImage: 8,
    maximumCropsPerImage: 64);

TextCropProfile crop = recognitionProfile.CropProfile!
    .WithWidthRetry(widthRetry);
// 识别器的输入宽度必须是动态 shape；将 crop 传给 OcrPipeline。
```

`WithWidthRetry` 只接受 `OcrRecognitionWidthMode.Dynamic`，并要求候选上限严格大于首轮 `MaximumWidth`。Pipeline 会复用现有的宽度分组、minimum-batch 补齐、Session 池和取消/结果预算；不会创建新的模型会话，也不会修改 DET、CLS 或首轮裁剪。滑窗区域会按候选宽度重新规划窗口，窗口数和 batch padding 都计入 `MaximumCropsPerImage`。固定宽度模型、固定 TensorRT engine 或超出模型 Profile 的候选会在输入合同阶段以 `DS-VISUAL-4103`/`ProfileInvalid` 拒绝，不能通过配置掩盖 engine shape 限制。

候选默认只做证据收集，不改变业务结果：`PreserveOriginal` 会保留首轮文本，同时在 `OcrRegionResult.WidthRetry` 中保存候选。若明确选择 `ConfidenceGain`，只有候选非空、置信度严格更高且增益达到 `MinimumConfidenceGain` 时才替换；这仍是启发式选择，不是准确率判定。`WidthRetry.Original`、`Candidate` 都包含文本、CTC trace、实际 `TensorWidth`、窗口和裁剪诊断，但不持有图像或 GPU 缓冲。`Candidate=null` 且决策为 `RegionLimit` 表示因行数预算未执行候选，不表示候选失败。

重试最多发生一次；现已可将 `LocalUpscale` 作为该单候选增强策略。启用后即使最终保留原文也会增加裁剪、REC 和合并耗时，必须在带标注的业务集上比较 CER/WER、召回率及端到端 P50/P95，不能仅根据置信度上涨宣称优化有效。当前单元测试覆盖候选选择、动态/固定 shape 合同、行数与物理 crop 限制，以及 OpenCV 局部放大尺寸和全流程候选证据；真实模型和各后端的收益矩阵仍需单独测量。

## 字符集覆盖审计

在创建推理会话前，先检查模型字典能否表示业务所需的字符。`OcrCharacterSetAuditor` 位于 `JYPPX.DeploySharp.Visual`，可复用同一个只读索引检查多个业务字符范围，无需图片或推理后端。以下接口以当前源码为准。

```csharp
OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(
    dictionaryPath, "my-ocr/dictionary", "1", useSpaceCharacter: true);
var auditor = new OcrCharacterSetAuditor(characters);
OcrCharacterCoverageReport coverage = auditor.Audit("订单编号ABCabc0123456789-_/￥€㎡");
foreach (OcrMissingCharacter missing in coverage.MissingCharacters)
    Console.WriteLine($"{missing.CodePoint} '{missing.Text}', count={missing.Occurrences}, " +
        $"firstUtf16Offset={missing.FirstUtf16Offset}, inCompound={missing.AppearsInCompoundToken}");

// 业务要求每个字符均可独立输出时，在创建 Pipeline 前执行。
coverage.EnsureCovered();
// 把同一个 characters 传给 PaddleOcrProfiles.CreateRecognition(...)。
```

### 覆盖的含义

审计按 **Unicode 标量**逐个匹配独立字典 token。`😀` 或扩展区汉字各算一个标量；报告偏移使用 .NET 字符串的 UTF-16 索引。重复需求按标量去重后计算覆盖率，同时保留出现次数。缺失列表按 Unicode 值排序，便于稳定比较。

- 字典只有 `"AB"` 时，`A`、`B` 标记为缺少独立 token，`AppearsInCompoundToken=true`；完整词条仍可作为模型输出。这个接口不做词条分词，也不判定一个完整短语能否由多个词条组合出来。
- `A`、`a`、`Ａ` 分开检查，`é` 与 `e` 加组合重音也分开检查。空格、全角空格、换行和制表符均保留；不会自动 trim、转大小写、NFC/NFKC 或全半角替换。
- `Coverage` 是不同需求标量的覆盖比例。空需求的 `Coverage=null`、`IsCovered=false`；`EnsureCovered()` 对空需求或缺失字符抛出 `DS-VISUAL-4105`。
- 字典覆盖只是必要条件，不是实际识别准确率。模型缺少某字符时可能输出另一个已知字符，不能依靠预测结果反推完整字典覆盖。

常用汉字应从业务文本、人工标注或业务自选字表导入。仓库示例中的 47 个中文业务字符是演示样本，不代表覆盖全部常用汉字。

### 字典索引、重复项和身份

`PaddleOcrProfiles.LoadCharacterSet` 接受严格 UTF-8，可带文件头 BOM；首个 BOM 不计入第一项，非法 UTF-8 或内部空行明确失败。它保留重复词条、原始顺序和词条空白，不能通过去重或给重复项加后缀“修复”字典，否则模型类别映射会发生变化。`useSpaceCharacter` 必须与模型导出一致；启用时追加一个空格类别，即使文件已含空格也不自行去重。

`DuplicateTokens` 列出重复项的原始文本、首次索引及重复索引。索引从 0 开始，属于字典，不包含 CTC blank/unknown 保留类别。两个类别可合法映射为同一文字；CTC 仍按类别索引折叠相邻重复。

报告同时保留现有 `CharacterSetId/Version/Sha256`，并增加 `TokenMappingSha256`。新指纹区分词条边界、顺序和重复次数，例如 `["AB","C"]` 与 `["A","BC"]` 会得到不同指纹；身份名称不参与新指纹。其序列格式为 .NET `BinaryWriter` 的 UTF-8 字符串前缀 `DeploySharp.OcrTokenMapping/v1`、小端 Int32 词条数、逐项长度前缀 UTF-8 字符串。原文件 SHA 单独记录，三种哈希用途不同。

`GreedyCtcDecoder.ExpectedClassCount` 继续严格校验模型输出类别数。显式 unknown 类别的 `Throw/Skip/Replace` 仍按配置执行，trace 的 `IsUnknown` 可查；unknown 替换文字不会因此成为字典支持的独立字符。审计不会改写 CTC 输出或原 OCR 结果哈希。

### 资源边界和运行工具

审计默认最多索引 4,194,304 个字典 UTF-16 单元，每次需求最多 65,536 个 UTF-16 单元，超限抛出 `ArgumentOutOfRangeException`，无截断或部分成功。两个限制可在审计器构造时配置；取消抛出 `OperationCanceledException`。索引只保留字符和字典信息，可并发调用 `Audit`，不接触图片、张量或 GPU 缓冲。把审计放在启动阶段，避免逐帧重复创建索引。

现有 [PaddleOCR Benchmark](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark) 提供 `--audit-characters` 命令，以及推理前的 `Report/Require` 模式。需求 JSON 将分组名映射为所需原始字符，覆盖汉字样本、大小写、数字、标点、全半角、单位和业务编号；缺失信息与字典文件 SHA 全部导出。完整运行时，审计在加载图片和创建后端会话之前完成，不计入推理时间。

### 本机字典样例（2026-09-17）

使用工具的 `character-requirements.example.json` 对本机五份字典检查，均按模型约定追加空格。表中只列该示例涉及的缺失项，不表示完整字符覆盖矩阵：

| 本机字典 | token 数（含追加空格） | 重复项数 | 示例中缺少的独立字符 |
| --- | ---: | ---: | --- |
| v4 | 6624 | 28 | `＠` U+FF20、`¢` U+00A2、`£` U+00A3、`¥` U+00A5、`€` U+20AC |
| v5 | 18384 | 0 | `‘` U+2018、`‰` U+2030、`㎏` U+338F、`㎡` U+33A1 |
| v6 tiny | 6905 | 0 | `㎏` U+338F、`㎡` U+33A1 |
| v6 small | 18709 | 0 | `㎏` U+338F、`㎡` U+33A1 |
| v6 medium | 18709 | 0 | `㎏` U+338F、`㎡` U+33A1 |

本次使用的 v4 文件含 28 个重复项，工具已统一使用主库加载器保留原索引和文字。示例里的中文样本、ASCII 大小写、数字和 URL/邮箱/电话/编号字符在五份字典中均有独立 token。`¥` 与 `￥`、`㎏` 与两个字符 `kg` 是不同需求，报告不会擅自替换。

## 文本规范化（可选、保留原文）

OCR 解码器只负责还原模型输出，不应在 CTC 阶段偷偷改变字符。业务需要统一 Unicode、全角/半角、空白或领域别名时，可以在 OCR 完成后显式调用 `OcrTextNormalizer`。该层不修改 `OcrResult`、`OcrRegionResult` 或 `RecognizedText`，因此原始文本、token trace、置信度和既有 `OcrResult.ComputeSha256()` 始终可回看。

```csharp
var normalization = new OcrTextNormalizationOptions(
    unicodeForm: OcrUnicodeNormalizationForm.Nfc,
    convertFullWidthAscii: true,
    normalizeLineEndings: true,
    collapseWhitespace: true,
    trimWhitespace: true,
    replacements: new[]
    {
        // id 必须稳定且唯一；替换按声明顺序、Ordinal 精确匹配。
        new OcrTextReplacementRule("invoice-prefix", "发票号：", "发票号码:")
    },
    maximumLength: 512,
    overflowMode: OcrTextNormalizationOverflowMode.Reject);

OcrNormalizedResult view = OcrTextNormalizer.Normalize(result, normalization, cancellationToken);
foreach (OcrNormalizedRegionResult line in view.Regions)
{
    Console.WriteLine($"raw={line.Text.RawText}; normalized={line.Text.NormalizedText}; " +
        $"rules={string.Join(",", line.Text.AppliedRules)}");
}
```

### 执行顺序和规则

每一行按固定顺序执行：Unicode NFC/NFKC → 全角 ASCII 与 U+3000 转换 → CRLF/CR 转 LF → 控制字符清理 → 空白折叠 → 首尾空白裁剪 → 自定义精确替换 → Unicode 标量长度限制。只有实际改变文本的规则才出现在 `AppliedRules`；配置中未命中的替换仍包含在 `ConfigurationSha256`，避免“看起来相同但配置不同”的结果无法复现。

- `Nfc` 只做规范组合；`Nfkc` 还会执行 Unicode 兼容折叠，可能改变单位、圈号或格式字符，需先在业务样本上确认。
- `convertFullWidthAscii` 将 U+FF01～U+FF5E 和全角空格 U+3000 映射为半角；不会转换大小写、中文标点或货币符号。
- `collapseWhitespace` 将连续 Unicode 空白（包括换行、制表符）压成一个 ASCII 空格；`trimWhitespace` 单独控制首尾裁剪。需要保留段落时不要启用折叠。
- `removeControlCharacters` 移除控制字符，但保留 `CR`、`LF` 和制表符，便于后续选择是否折叠。
- 自定义规则不是正则表达式，不会执行代码；最多 64 条，按 Ordinal 精确替换。若规则互相重叠，应显式排列顺序并在结果中检查应用记录。
- `maximumLength` 按 Unicode 标量计数，零表示不限制；`Reject` 抛出 `DS-VISUAL-4108`，`Truncate` 在标量边界截断并记录 `length:truncate`。默认不设长度上限，不会静默截断。

`OcrTextNormalizationResult` 同时保存 `RawText`、`NormalizedText`、原始/规范化标量长度、`SourceRegionIndex`、应用规则、配置 SHA 和结果 SHA。`OcrNormalizedResult.Source` 指向原始不可变结果，规范化视图的 hash 额外包含源结果 hash 和配置 hash；它不是新的检测或识别结果，不应把规范化后的字符串回写到原 token trace 中。空文本也会经过相同规则和长度策略，取消令牌在每个阶段检查。

规范化不是准确率或字段合法性判断：全角转半角可能不适合中文地址，NFKC 可能合并业务上有意义的符号，自定义替换也可能掩盖模型错误。日期、金额、电话、URL、发票号等格式校验应放在独立字段验证器中；先保留原文和规范化视图，再由业务决定采用哪一个。默认不调用 `OcrTextNormalizer` 时，现有 OCR 行为和结果指纹完全不变。

## 结构化字段校验（独立于 OCR）

字段校验不嵌入检测、CTC 解码或模型字典。先选择业务需要的规范化视图，再把同一个 `OcrTextNormalizationResult` 交给一个或多个 `IOcrFieldValidator`；校验器只读文本，不回写 `OcrResult`。

```csharp
OcrNormalizedRegionResult line = view.Regions[0];
IOcrFieldValidator invoice = new InvoiceNumberOcrFieldValidator();
OcrFieldValidationResult check = invoice.Validate(line.Text, cancellationToken);

if (!check.IsValid)
    Console.WriteLine($"region={check.SourceRegionIndex}, code={check.Code}, reason={check.Message}");
else
    Console.WriteLine($"invoice={check.CanonicalValue}");

// 需要同时检查多个候选字段时，结果顺序与声明顺序一致，并且每个结果都保留原文。
OcrFieldValidationReport report = OcrFieldValidators.ValidateAll(
    line.Text,
    new IOcrFieldValidator[] { new DateOcrFieldValidator(), new AmountOcrFieldValidator() },
    cancellationToken);
```

当前内置校验器及其边界如下：

| 类型 | 默认规则 | 规范值/限制 |
| --- | --- | --- |
| `Date` | `yyyy-MM-dd`、斜线/点分隔、中文年月日和 `yyyyMMdd` | 输出 `yyyy-MM-dd`；只校验日期存在性 |
| `Amount` | 可带 `¥ ￥ $ € £`、千分位、正负号，最多两位小数 | 输出不带货币符号的 InvariantCulture 十进制；不解析中文大写金额 |
| `Phone` | 中国大陆手机、区号座机和可选 `+86`/空格/括号 | 保留可识别格式；不查询运营商或号码归属 |
| `ChineseIdCard` | 18 位或旧 15 位身份证，生日和 MOD-11 校验码 | 输出 18 位大写 `X`；不验证行政区划真实性 |
| `Url` | 绝对 `http`/`https`，非空主机，最长 2048 | 输出 `Uri.AbsoluteUri`；不发起网络请求 |
| `Email` | 常规 `local@host.tld` 形状，最长 254 且无连续点 | 保留原大小写；不做 DNS/SMTP 验证 |
| `Identifier` | 字母/数字及 `._-/`，可配置标量长度和 Unicode 字母 | 默认 ASCII 工业编号；必须含字母或数字 |
| `Address` | 有界长度、至少含字母/数字/CJK、无控制字符 | 仅启发式，不验证地址是否真实存在 |
| `InvoiceNumber` | 8 或 20 位 ASCII 字母数字 | 输出大写；不包含发票真伪或校验码验证 |
| `DeviceSerialNumber` | `Identifier` 规则，默认至少 4 个标量 | 可自定义最大长度和 Unicode 字母 |

每个 `OcrFieldValidationResult` 都提供 `Status`（`Valid`/`Invalid`/`Empty`）、稳定 `Code`、消息、可选 `CanonicalValue`、`RawText`、`NormalizedText`、`SourceRegionIndex`、规范化结果哈希和校验结果哈希。`OcrFieldValidators.CreateDefault()` 返回 10 个独立校验器；应用也可以继承 `OcrFieldValidatorBase` 或直接实现 `IOcrFieldValidator`，为设备号、订单号等项目定义自己的规则。校验器集合最多 64 个且 ID 不可重复，调用共享取消令牌。

格式通过不等于业务事实成立：OCR 误识别后的字符串可能恰好满足日期、身份证或金额格式，规范值也不应覆盖原文。生产流程应同时保存 OCR 原始/规范化文本、字段校验 `Code` 和人工或数据库复核结果；需要跨行一致性、金额合计、地址库或发票平台验证时，在应用层组合这些结果。

## 跨字段一致性（独立于 OCR）

字段格式通过后，票据和设备表单通常还需要检查日期先后、明细金额合计或两个字段是否相同。`OcrFieldConsistencyContext` 将应用字段名绑定到已有的 `OcrFieldValidationResult`，规则只读取规范值，不回写 `OcrResult`，也不会把业务不一致误报为模型推理失败：

```csharp
OcrFieldConsistencyContext fields = new OcrFieldConsistencyContext(new[]
{
    new OcrFieldBinding("start", startValidation),
    new OcrFieldBinding("end", endValidation),
    new OcrFieldBinding("line_1", line1Validation),
    new OcrFieldBinding("line_2", line2Validation),
    new OcrFieldBinding("total", totalValidation)
});

OcrFieldConsistencyReport report = OcrFieldConsistency.EvaluateAll(fields, new IOcrFieldConsistencyRule[]
{
    new DateOrderOcrFieldConsistencyRule("start", "end"),
    new AmountTotalOcrFieldConsistencyRule(new[] { "line_1", "line_2" }, "total", tolerance: .01m)
});

if (!report.IsConsistent)
    foreach (OcrFieldConsistencyResult item in report.Results)
        Console.WriteLine(item.Code + ": " + item.Message);
```

内置规则包括 `EqualOcrFieldConsistencyRule`、`DateOrderOcrFieldConsistencyRule` 和有界的 `AmountTotalOcrFieldConsistencyRule`。结果会保留规则 ID、字段顺序、观察到的规范值、上下文哈希和结果哈希；`Consistent` 表示关系成立，`Inconsistent` 表示值存在但关系不成立，`Missing` 表示字段没有绑定，`InvalidDependency` 表示依赖字段未通过格式校验。字段和规则 ID 均会规范化且分别限制为 128 个字段、64 个字段引用和 64 条规则，规则集合中的 ID 必须唯一。

这些规则只验证文本之间的可计算关系，不查询数据库、不验证发票真伪、不判断地址或设备归属，也不替代金额税率、币种、跨页合计和人工审核。需要更多业务关系时，继承 `OcrFieldConsistencyRuleBase`，在自定义规则中返回稳定原因码并保留同一上下文即可。

## 行、段落与多栏布局视图

检测器返回的是带 polygon 的文本行；当页面还需要段落或多栏阅读顺序时，可以在 OCR 完成后使用 `OcrTextLayoutBuilder`。它只读取源图边界和文本，不重新裁剪、不重新推理，也不修改 `OcrResult`。如果已经创建 C1 规范化视图，可将它一并传入，版面结果会使用规范化字符串，同时保留 `OcrTextLayoutResult.Source` 供追溯。

```csharp
OcrTextLayoutOptions layoutOptions = new OcrTextLayoutOptions(
    readingOrder: TextReadingOrder.LeftToRightThenTopToBottom,
    lineCenterToleranceRatio: .5f,
    paragraphGapRatio: 1.5f,
    columnGapRatio: 2.5f,
    regionSeparator: " ");

OcrTextLayoutResult layout = OcrTextLayoutBuilder.Build(
    result,
    normalized,
    layoutOptions,
    cancellationToken);

foreach (OcrTextColumn column in layout.Columns)
    foreach (OcrTextParagraph paragraph in column.Paragraphs)
        Console.WriteLine(paragraph.Text);
```

布局器先用文本 polygon 的轴对齐边界按垂直重叠/中心距离聚类成行，再用横向重叠和间隙聚类成栏，最后在每栏内按垂直间距聚类成段落。`LineCenterToleranceRatio`、`MinimumVerticalOverlapRatio`、`MaximumInlineGapRatio`、`ParagraphGapRatio` 和 `ColumnGapRatio` 都是显式有界参数；默认值适合规则文档起步，不会自动学习页面版式。行内区域按 X 排序并插入 `RegionSeparator`，段落行和段落之间分别使用 `ParagraphLineSeparator`/`ParagraphSeparator`。

`Lines` 和 `Paragraphs` 按 `TextReadingOrder` 返回，`Columns` 始终按从左到右保存，且每个行/段落/栏都带源坐标 `Bounds`。默认的 `TopToBottomThenLeftToRight` 适合单栏或跨栏逐行读取；双栏文档通常应选择 `LeftToRightThenTopToBottom`，并在自己的页面样本上确认栏间隙。跨栏标题、表格、竖排文本、复杂阅读顺序和真正的版面语义仍需要专用 layout/table 模型，不能仅靠几何阈值推断。

所有集合和拼接文本都受 `MaximumLines`、`MaximumParagraphs`、`MaximumColumns` 限制，超限返回 `DS-VISUAL-4102`；输入为空时返回空视图，取消令牌在分组循环中生效。`OcrTextLayoutResult.ComputeSha256()` 包含源 OCR hash、布局配置 hash、文本和来源索引，规范化视图的配置也会纳入；它是布局证据，不是新的模型推理指纹。不要把按几何启发式拼出的段落当作字段、表格或语义实体。

## 长文本宽度诊断与超宽策略

本节新增接口以当前开发源码为准；旧 NuGet 包不会自动获得这些 API，使用前应确认所安装版本包含此能力。

`PaddleOcrProfiles.CreateRecognition` 的主库默认值为 `TargetHeight=48`、`MinimumWidth=48`、`MaximumWidth=3200`。基准工具历史默认的 320 只是测试参数，不是主库上限。提高宽度前，应确认模型、动态 shape 以及 TensorRT optimization profile 支持该范围；修改裁剪配置不会自动扩大 engine 的 shape 范围。

```csharp
// recognitionProfile 是 PaddleOcrProfiles.CreateRecognition(...) 的返回值。
TextCropProfile crop = recognitionProfile.CropProfile!
    .WithRecognitionOverflowMode(RecognitionOverflowMode.Reject);
// 将 crop 作为 OcrPipeline 的 cropProfile 参数。

foreach (OcrRegionResult item in result.Regions)
{
    if (item.RecognitionWidth is OcrRecognitionWidthInfo width)
        Console.WriteLine($"region={item.Region.SourceIndex}, natural={width.NaturalWidth}, " +
            $"target={width.TargetWidth}, tensor={width.TensorWidth}, compressed={width.WidthClamped}");
}
```

| 字段 | 含义 |
| --- | --- |
| `NaturalWidth` | 按已定向四边形长宽比计算的 `ceil(TargetHeight × width / height)`；在最小宽度、对齐和上限之前 |
| `TargetWidth` | 当前区域经最小值、对齐和上限处理后的规划宽度；固定宽度模式采用 `FixedWidth` |
| `TensorWidth` | 实际提交的 Batch 张量宽度，可能包含与其他更宽文本行共同补齐的 padding |
| `WidthClamped` | 单窗口自然宽度超过受限宽度时为 true；滑窗合并结果为 false，逐窗口都通过宽度检查 |
| `BatchPadded` | `TensorWidth > TargetWidth`，仅表示 Batch 补齐 |
| `WindowCount` | 普通行是 1；大于 1 时，`TargetWidth`/`TensorWidth` 分别报告所有窗口中的最大值，完整信息在 `RecognitionWindows` |

这些值由几何合同计算；OpenCV 透视中间图的边长会先取整，因此不是逐像素测得的 crop 栅格宽度。90°/270°方向先交换长宽，再做计算。宽度对齐产生的额外 padding 被上限裁掉时，只要原始内容仍可放下，就不会误报压缩。

- **Clamp（兼容默认）**：超长行仍缩放到上限，结果的 `RecognitionWidth.WidthClamped=true`。它不是切分或无损长文本识别。
- **Reject（显式启用）**：在识别 crop 分配和 REC 调用之前抛出 `OcrPipelineException`，错误码 `DS-VISUAL-4103`，阶段 `CropAndBatch`；包含 crop Profile、原 `RegionIndex` 和自然/受限宽度。DET 和可选 CLS 此时可能已经执行。一个区域超宽会使本次完整 OCR 调用失败，不静默丢掉该行；输入释放与取消语义不变。
- **SlidingWindow（显式启用）**：在校正并定向后的文字行中规划重叠窗口，逐窗口执行有界识别，再合并回原检测区域。不会增加 `OcrResult.Regions` 中的行数。
- **Split**：基于文字间隙的切分尚未提供，不含占位枚举值。

`crop.DescribeWidth(quadrilateral, orientation)` 只返回诊断，即使是 Reject 也不抛超宽异常，便于应用先规划；`CalculateWidth`、`TextCropRequest` 和实际 Pipeline 会执行 Reject。固定宽度模型的有效限制是 `FixedWidth`，不能通过更大的 `MaximumWidth` 绕过。

既有构造函数和默认策略保留；手工使用旧 `OcrRegionResult(region, recognition)` 构造结果时，`RecognitionWidth=null` 表示诊断未知，不代表没有压缩。方向恢复及 ROI 投影/合并保留宽度来源。既有 `OcrResult.ComputeSha256()` 继续针对识别内容与几何，不加入这些诊断，以便与历史结果对照。

基准工具可设置 `DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE=Clamp/Reject/SlidingWindow`、`DEPLOYSHARP_PADDLEOCR_MAXIMUM_WIDTH`、`DEPLOYSHARP_PADDLEOCR_CROP_TRANSFORM=Perspective/AffineWhenEquivalent` 和 `DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR`。报告在计时外导出输入/模型/程序集 SHA、每行文字、polygon、字典 SHA、宽度和窗口来源信息；完整操作见[基准工具说明](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark)。

## 超长行滑窗识别

优先在模型支持范围内选择合适的动态宽度。确实超过上限的 URL、订单号或长句可以启用滑窗。滑窗会增加 REC 工作量，适合解决宽度约束，不能作为普通行必然提速的开关。

### 模型路由边界

当前 OCR Pipeline 不会根据文字长度、置信度或语言自动切换 v4/v5/v6、mobile/server 或其它 recognizer。原因是现有公开标注和本地 smoke 证据不足以证明某个路由条件能稳定降低 CER/WER，自动路由还会改变字典、token 来源、Session 池和预算追溯。调用方如需路由，应显式创建多个已验证的 `OcrPipeline`/Profile，并在业务层固定：

- 路由输入特征与阈值（例如语言、脚本、长度桶），同时记录配置 SHA；
- 每个候选模型的模型/字典 SHA、后端、预处理和最大窗口/耗时预算；
- 最终选择、候选结果和未选择原因，不能只返回置信度最高的文字；
- 失败或超预算时的明确 fallback，不得在一次调用中无限创建新 Session。

在没有按模型、数据域和后端固定 CER/WER 证据之前，推荐保持单 Profile、单字典和有界 Session 池。当前仓库将模型路由列为未完成实验项，不把近似模型的单机结果外推成自动路由收益。

```csharp
TextCropProfile crop = recognitionProfile.CropProfile!.WithRecognitionWindows(
    new OcrRecognitionWindowOptions(
        overlapRatio: 0.2,
        maximumWindowsPerRegion: 32,
        maximumWindowsPerImage: 1024,
        maximumMergedCharacters: 16384,
        maximumOverlapTokens: 64,
        minimumOverlapTokens: 2,
        maximumMergedTimesteps: 65536)
        .WithMaximumOverlapEditDistance(2));

// 仍通过现有 OcrPipeline 构造函数传入 crop。
// REC Batch 和独立 Session 数量继续由既有参数控制。
foreach (OcrRegionResult line in result.Regions)
{
    bool needsReview = line.RecognitionWindows.Any(window => window.SeamUncertain);
    Console.WriteLine($"{line.Region.SourceIndex}: {line.Recognition.Text}, review={needsReview}");
    foreach (OcrRecognitionWindowResult window in line.RecognitionWindows)
        Console.WriteLine($"  window={window.Index}, range={window.Start:F3}..{window.End:F3}, " +
            $"raw={window.Recognition.Text}, removed={window.RemovedPrefixTokens}, " +
            $"seamEdit={window.OverlapEditDistance}, uncertain={window.SeamUncertain}");
}
```

### 裁剪、Batch 和坐标

`OcrRecognitionWindowPlanner.Plan(region, crop)` 可在创建任何图像或张量之前预览窗口。`Start`/`End` 是**定向后校正行的归一化水平区间**，不是原图 x 像素。规划器用单应变换把窗口角点映回源图，并将 90°/180°/270°校正编码到角点角色中，避免后续重复旋转。透视明显的区域会根据局部宽度缩小窗口；无法在几何和数量限制内覆盖时明确失败。

每个窗口使用原 `SourceIndex`，经过既有按宽度分组、Batch padding 和独立 Session 调度。原始 DET polygon、方向元数据和阅读顺序保留在最终行中。`RecognitionWindows` 只保留区间、逐窗口宽度、原始识别结果及接缝决策，不持有图像或张量。普通行的窗口列表为空，仍执行单次识别。

直接用超宽区域构造 `TextCropRequest` 不会自动生成多个请求：在 SlidingWindow 模式下会抛出 `DS-VISUAL-4103`；应使用 `OcrRecognitionWindowPlanner`、完整 `OcrPipeline.Run` 或 `OcrPipeline.RecognizeOnlyAsync`。后两条 Pipeline 入口会自动规划窗口、按 batch/session 池执行并归并回原检测区域。`DescribeWidth` 始终只规划；固定模型仍以 `FixedWidth` 为上限，不能绕过 engine shape 限制。

### 接缝匹配及不确定结果

`OcrRecognitionWindowMerger` 先尝试在相邻窗口之间匹配完全相同的 token 后缀/前缀；失败后才会启用有界模糊匹配。模糊回退只比较各自估算位置落在共享几何区间内的 token，并使用有上限的 token 编辑距离；位置映射会扣除输入 padding，容纳半个时间步的离散定位误差。token 可能是中文字符、emoji 或含多个 Unicode 标量的字典项，比较/编辑距离/删除均以完整 token 为单位，不拆 Unicode 字符。

精确匹配默认至少要求 2 个重叠 token。模糊回退至少要求 8 个 token，并受 `maximumOverlapTokens`、共享几何和编辑预算共同限制；默认 `maximumOverlapEditDistance=2`，可设为 0 关闭，上限为 8。先比较位置对齐片段的替换距离；几何边界若多包含左侧空白 token，空白也消耗同一预算，不能免费放宽替换。仍不匹配时，只允许后一窗口前缀成为前一窗口共享几何区间内的**唯一、有序、精确 token 子序列**：允许预算内的左侧间隙，全部左侧文字仍保留；后一窗口独有的 token 不会被此子序列规则跳过，预算内存在多条对齐路径也不接受。规则无法确认时保留后一窗口文本，可能留下重复前缀。

`minimumOverlapTokens: 1` 可让精确匹配处理单 token 短接缝，但不会降低模糊回退的 8-token 下限；重复编号场景更需要验证。增大 `overlapRatio`（最多 0.5）可提供更多上下文，同时增加推理量。CTC 时间位置不是精确字符框；包括原有替换在内的回退都是启发式，并不保证被保留的一侧文字一定正确。请用业务真值评估误删、重复和字符替换风险。

完全匹配时 `OverlapEditDistance=0` 且接缝不标记 uncertain；通过模糊回退时 `OverlapEditDistance` 记录编辑距离并设置 `SeamUncertain=true`，合并优先保留前一窗口的重叠文本，同时抑制后一窗口对应的前缀；无法通过门限时不删除后一窗口文本，也设置 `SeamUncertain=true`，因此**最终文本可能保留重复内容**。应用应复核所有 uncertain 接缝；当前 Pipeline 不自动选择重试结果。原始窗口文字与 CTC trace 一直可查。被抑制前缀在合并 trace 中标记为不发射，局部原始 trace 保持不变；合并 trace 的时间步是窗口序列串接索引，不是原模型一次推理的时间轴。

2026-09-29 的两种受控 3,600 字符样本用于验证接缝合同，不代表自然图片精度：PP-OCRv6 Small 在 ORT CPU 与 OpenVINO CPU 上均完成识别。周期重复短语样本用 18 窗口，CER `0.0556%`、WER `0.4556%`；变化词汇样本用 16 窗口，CER `0.0833%`、WER `0.7194%`；每个样本在两后端的文本 SHA 一致，模糊回退均处理了 1–2 token 编辑的接缝。样本图由脚本生成，不是公开数据集，也不应作为生产准确率结论。测试与边界见[受控长文本验证记录](../../eng/models/paddle-ocr/verification/synthetic-longtext-a2-20260929.md)。

### 资源与失败行为

窗口数、UTF-16 字符数、原始 CTC 时间步和 `OcrPipelineOptions.MaximumResultBytes` 都有边界。窗口总数超限在 REC 分配前失败；结果超限不会返回部分文本，错误码为 `DS-VISUAL-4102`（`OcrLimitExceeded`）。识别时对已保留结果预算进行检查，合并前再预算原始 trace 与合并 trace；最小 Batch 的补齐行按保守预算计入。结果字节数是近似托管开销，不等于整个进程的内存硬限额。

所有窗口使用同一次调用的取消和超时 token。一个窗口失败使该次调用失败，已创建的准备输入由调度器释放。Windows/OpenCV 的四方向真实像素裁剪已加入单元回归；TensorRT 和其他后端仍需各自验证模型 shape、CTC 合同及窗口后的吞吐，不能从 CPU/CUDA 的通过结果推断全部后端完成验收。

## 几何质量、倾斜角度与边界风险

`TextCropProfile.WithGeometryValidation(...)` 在 DET 返回后、CLS 和 REC 裁剪前，对**原检测区域**执行一次有界几何检查。滑窗子区域不重复计算这些行级诊断。既有构造函数默认 `Disabled`，不会增加逐行诊断计算，也不改变既有透视采样、CTC 和阅读顺序。

```csharp
// 先观察业务图片的风险分布，再设置拒绝阈值。
var crop = cropProfile.WithGeometryValidation(new OcrGeometryOptions(
    mode: OcrGeometryValidationMode.Report,
    minimumArea: 4,
    minimumEdgeLength: 1,
    maximumAspectRatio: 200,
    maximumOutsideFraction: 0.25,
    edgeMargin: 1,
    maximumPerspectiveCondition: 10000));

// 把 crop 传给 OcrPipeline 的识别裁剪参数。
// 已有区域也可单独分析，不必加载模型或图片：
OcrGeometryDiagnostics geometry = OcrGeometryAnalyzer.Analyze(
    textRegion, new VisualSize(imageWidth, imageHeight));
Console.WriteLine($"risks={geometry.Risks}; outside={geometry.OutsideFraction:P1}");
Console.WriteLine($"angle={geometry.BaselineAngleRadians}; condition={geometry.PerspectiveCondition}");
```

### 模式与错误处理

| 模式 | 行为 |
| --- | --- |
| `Disabled` | 兼容默认，结果 `Geometry=null`，不执行流水线几何检查 |
| `Report` | 保留全部行并提供诊断；不阻止原有 crop/模型合同检查 |
| `Reject` | 任一区域的风险命中 `RejectedRisks` 时，整次调用报 `DS-VISUAL-4104`，阶段 `CropAndBatch`，带原 `RegionIndex` 和 crop Profile |

Reject 不静默删行、不自动回退到另一组阈值。默认拒绝集合包括过小面积、过短边、极端宽高比、图外面积过大、病态透视和缺失角点；**不包含 `EdgeContact`**，因为贴边文字不一定被截断。可以只拒绝业务确定不接受的情况：

```csharp
var strictCrop = cropProfile.WithGeometryValidation(new OcrGeometryOptions(
    OcrGeometryValidationMode.Reject,
    maximumOutsideFraction: 0.1,
    rejectedRisks: OcrGeometryRisk.OutsideImage
        | OcrGeometryRisk.IllConditionedPerspective));
```

阈值均为启发式验收配置，不是所有模型通用的准确率标准。面积以输入像素平方计，边长/边界距离以像素计；输入缩放后应重新评估阈值。NaN、Infinity、负距离、不合法模式和风险位在配置时拒绝。

`OcrGeometryAnalyzer.Analyze` 是独立的纯分析接口：无论 options 的 Mode 如何都返回诊断，不执行 Reject；需要自行判断 `Risks & options.RejectedRisks`。`TextCropRequest` 没有源图尺寸，不能单独实施图外面积检查；直接调用适配器时，应先调用 Analyzer。`Report` 也不能让缺失明确角点的区域自动变成可裁剪区域。

### 诊断字段和坐标空间

| 字段 | 精确定义及用途 |
| --- | --- |
| `InputPolygon` / `InputSize` | 评估时 OCR 输入空间中的多边形/图像尺寸；不可变，后续 ROI/全图方向恢复不改写这份证据 |
| `Area` / `MinimumEdgeLength` | 凸多边形面积和最短边；不是轴对齐包围框面积 |
| `AspectRatio` | 四边形两组对边最大长度的长短比，与 90°旋转无关；缺失明确角点时为 null |
| `OutsideFraction` | 多边形与 `[0,width] × [0,height]` 的精确凸裁剪计算出的图外面积比例；不是包围框越界比例 |
| `BaselineAngleRadians` | 按显式 TL→TR、BL→BR 角色求和得到的校正前基线角度，图像顺时针为正；不是 CLS 预测值 |
| `RectificationAngleRadians` | 上述基线角度的负值，仅表示去倾斜分量，不是实测残余角度 |
| `PerspectiveCondition` | 输入包围框两轴归一化到单位正方形后，单应矩阵 `||H||F × ||H⁻¹||F`；恒等矩阵为 3，无法求解为 null 并标记病态风险 |
| `ParallelogramError` | `|TL + BR − TR − BL| / 最长边`；零表示平行四边形，不能据此宣称两种采样逐像素等价 |
| `Risks` | 可组合的阈值风险，`None` 只代表本次阈值没有触发，不代表文字一定识别正确 |

条件数通过平移/缩放归一化避免仅因图像分辨率变化就误报；细长文本的风险仍由 `AspectRatio` 和 `MinimumEdgeLength` 独立检查。接近或跨越任意边界都可报告 `EdgeContact`，它只是截断**风险**，无法知道原图外是否真的存在字符。

结果 `OcrRegionResult.Geometry` 保留上述来源。若使用全图方向校正，`InputPolygon` 位于纠正后空间，而最终 `Region.Polygon` 在原图；若经过 ROI 投影，前者仍在该 ROI 的 OCR 输入空间。绘制最终检测框必须使用 `Region.Polygon`。CLS 之后的直角校正仍由 `Region.Orientation` 和方向 metadata 说明，不覆盖校正前基线角度。

### 可选仿射快路径和为什么不能仅按角度切换

上下边几乎水平的梯形仍可能具有明显透视变化。仅按“小于 5°”选择仿射会破坏四角对应关系，因此默认保持**四角透视校正 → 可选直角旋转 → resize/padding**。如明确接受平行四边形近似，可用 `WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent)` 开启 OpenCV 的 `GetAffineTransform/WarpAffine` 路径；策略会同时检查归一化闭合误差和仿射基底条件数。梯形、非平行四边形或病态基底自动回退到透视，绝不只按角度切换。`ParallelogramError` 与 `OcrCropTransformPolicy.GetParallelogramError(...)` 提供可观察依据，但无法证明不同插值实现逐像素相同。OpenCV 批次的 `VisualPreprocessingDescriptor.Notes` 会记录实际的 `cropTransform=Perspective/Affine/Mixed`，便于在启用后确认是否真的命中快路径。

```csharp
var affineWhenSafe = cropProfile.WithTransformMode(OcrCropTransformMode.AffineWhenEquivalent);
```

该模式只改变 OpenCV OCR 适配器的采样算子；ONNX Runtime、OpenVINO 和 TensorRT 仍接收相同的识别张量合同。默认 `Perspective` 可用于跨后端逐像素回归；启用仿射时应固定插值、目标尺寸和输入图像，并对文本结果与 `cropTransform` 诊断分别验收。仿射判断使用平移/缩放不变的闭合误差与基底条件，不会为低角度梯形强行降级。

自交、重复顶点、非有限坐标及不符合声明顺序的几何在 `TextPolygon`/`TextQuadrilateral` 构造时拒绝，不会伪装成可识别区域。检查只读取少量顶点，不复制图片或张量；诊断工作计入 `CropAndBatch`，持有诊断的近似空间计入 `MaximumResultBytes`。超出预算报 `DS-VISUAL-4102`。既有文本/结果 SHA 不加入诊断，允许与关闭检查时对照。

## 低置信度方向重试

透视去倾斜不等于知道文字正反。可通过 `WithOrientationRetry` 对初次识别仍不可靠的行尝试其他直角方向；默认不启用，不额外执行 REC。

```csharp
var crop = cropProfile.WithOrientationRetry(new OcrOrientationRetryOptions(
    confidenceThreshold: 0.8f,
    minimumConfidenceGain: 0.05f,
    maximumRegionsPerImage: 16,
    rotations: new[] { TextOrientation.Degrees180 },
    maximumCropsPerImage: 1024));

// 把 crop 传给 OcrPipeline 后，结果保留原始与候选识别：
foreach (OcrRegionResult row in result.Regions)
{
    if (row.OrientationRetry is not { } retry) continue;
    Console.WriteLine($"selected={retry.SelectedIndex}; skipped={retry.SkippedByRegionLimit}");
    foreach (OcrOrientationAttempt attempt in retry.Attempts)
        Console.WriteLine($"{attempt.Orientation}: {attempt.Recognition.Text} ({attempt.Recognition.Confidence:F3})");
}
```

### 触发、方向和选择规则

- REC 置信度**严格低于** `ConfidenceThreshold`，或文本为空时触发。它不是 CLS 置信度阈值；CLS 的 Fail 策略仍优先执行，不能靠 REC 重试绕过。
- `Rotations` 可显式选择顺时针 90°、180°、逆时针 90°，最多三个、不重复且不含 0°；默认只试 180°。它们相对于**初次经过 CLS/竖排规则之后的裁剪方向**，不是相对于上次候选逐步累加。例如初次为 180°，再试相对 180°时，实际裁剪方向为 0°。
- 初次非空结果仅在候选非空、置信度严格更高且增量达到 `MinimumConfidenceGain` 时被替换；平局或增益不足保持当前结果。初次/当前为空时，非空候选可以替换它；空候选不会替换非空结果。
- 每轮都与当前获选结果比较；一旦获选结果非空且达到阈值，该行不再进入后续轮次。不同候选置信度不可视为经过校准的正确率，增益也可能选择错误文字；应结合业务真值评估。
- `Region.Orientation` 反映最终获选裁剪方向，polygon、SourceIndex 和阅读顺序不变。原 CLS metadata 保留其原始预测，不伪装成重试结果；完整决策见 `OrientationRetry`。

### Batch、滑窗与资源

每轮只重试仍符合条件且已接收的行，不重复 DET、CLS 或图像解码。复用原识别器的独立 Session 池、动态宽度分组、真 Batch、最小批量补齐及输入释放；裁剪仍在工作槽位可用时准备。

`maximumRegionsPerImage` 按原阅读顺序接收，超出的符合条件行保留原结果，并给出 `SkippedByRegionLimit=true`；不要把它当成完成重试。`maximumCropsPerImage` 是**跨所有重试轮次**的额外裁剪硬上限，不含初次 REC，也不含满足模型 minimum batch 的重复行。默认 1024，上限 4096；达到硬限制时抛出 `DS-VISUAL-4102`，不返回部分成功。

在 SlidingWindow 模式下，会按候选方向重新规划有界窗口、执行识别并归并为原行；长行窗口数量也计入重试裁剪上限，并保留逐窗口文本和接缝不确定信息。既有每行窗口限制及每轮图像窗口限制仍生效。90°旋转可能改变自然宽度：在 Reject 模式下，超宽候选报 `DS-VISUAL-4103`，不会静默改成 Clamp；模型/engine 自身的 shape 限制也不放宽。

初次与候选 token、窗口及合并结果共同计入近似 `MaximumResultBytes`，包含 minimum-batch 重复行的保守预算。所有轮次共用一次总超时、取消和 Pipeline 生命周期；后端错误、取消、超时或硬限制失败会使**整个调用失败**，不悄悄吞掉后返回原结果。仅接收行数限制使用明确标记的跳过语义。

### 诊断、计时和复现

`OrientationRetry=null` 表示未开启或没有触发，不表示“尝试过但没改善”。非 null 时，`Attempts[0]` 始终保存初次原文/置信度/宽度/窗口，之后为实际执行的候选，`SelectedIndex` 标明选择；最多保留四次尝试。诊断引用不可变识别结果，不保留图像或 GPU 缓冲。

ROI 投影、合并重新编号、全图方向恢复都保留记录并同步原区域索引；尝试中的方向仍是 OCR 输入空间的裁剪方向。绘制最终框使用最终 `Region.Polygon`。`Geometry` 仍是初次 DET 后的几何证据，不被候选覆盖。

重试的规划、裁剪、REC 和合并墙钟时间计入 `Timing.Recognition`；`Timing.Details` 的识别准备/推理/后处理工作及 batch 数累加所有轮次，DET/CLS 不重复计时。最终结果的 SHA 仍只包含最终选中的文字/几何/方向；诊断本身不加入，所以保留初次结果时可以继续比较历史 SHA。

先测试默认 180°候选及少量低置信度行，再决定是否开启三个候选；不要为了更高置信度无限增加推理。该接口不是任意角度搜索、图像去噪/锐化或语义纠错器。

## 性能测量建议

### 几何检查验证（2026-09-16）

使用同一 `demo_1.jpg`、PP-OCRv4 mobile/v5 mobile/v6 tiny、ORT 1.23.2 CPU/CUDA、Batch=4/独立通道=1、Clamp/320、warmup=1/iterations=3：

- Disabled 和 Report 各 6 组完整调用成功，文本与结果合同 SHA 均与此前 Clamp 基线相同。
- 每组保留原 16 行并输出几何证据；此图默认风险均为 `None`。各版本最大归一化条件数约 3.003、3.025、3.010，同版本 CPU/CUDA 相同。
- 将 `minimumArea` 故意设为 100000000 后，6 组调用均按预期返回 `DS-VISUAL-4104`，并非后端不支持。
- 原生像素测试覆盖 0°、±5°、±15°、±30°、±45°、90°、180°和梯形透视；检查采样坐标、颜色通道、ROI 投影及方向恢复后的诊断来源。

这是正确性和兼容性回归，不是最佳性能或 CER/WER 测试。本轮 Report 的 `crop_ms`（几何检查加分组）约 0.037～0.237 ms，但开发机有其他任务且样本仅 3 次，不能由此做稳定性能结论。真实文字的多角度标注集和其他后端几何模式矩阵仍未完成；仿射快路径的安全策略及 OpenCV 适配器回归已在本轮补齐，仍需在目标设备上独立测量收益。后续方向重试验证见下节。

### 方向重试验证（2026-09-17）

使用同一图片、v4 mobile/v5 mobile/v6 tiny、ORT CPU/CUDA、Batch=4/通道=1、Clamp/320、warmup=1/iterations=3，对照关闭、默认启用、强制触发三组模式，共 18 组完整调用通过。

- 关闭时全部结果合同 SHA 与上一批几何 Report 基线一致。
- 默认阈值 0.8、最小增量 0.05、只试相对 180°：v4/v6 未触发，v5 触发 1 行且保留原结果；同版本 CPU/CUDA 的最终文字及决策一致。
- 将阈值设为 1 以强制试验时，三版本每个后端均对 16 行额外试一次 180°，没有候选被接受。此项证明调度和原始结果保留，不证明准确率改善。
- v6 tiny 的 SlidingWindow/320、最多重试 1 行、阈值 1 组合在 CPU/CUDA 上通过：1 行重试，15 行明确标记被行数限制跳过；重试行包含 2 个窗口，两后端最终文字一致。完整合同 SHA 可因浮点置信度不同而不同，不能据文字一致宣称逐位数值一致。
- 单元及原生测试覆盖改善候选被接受、多轮早停/平局、空文本、相对于 CLS 结果的旋转、旋转后 Reject 宽度限制、并发独立 Session、取消/失败/资源释放、ROI 重新编号、全图方向恢复。

这些是开发机短时行为验证，未形成有人工真值的“倒置文字修复率”、CER/WER 或最优速度结论。重试会额外执行模型；业务中应保留关闭时的基线，并根据触发比例评估收益与耗时。

### 宽度诊断与兼容性验证（2026-09-16）

使用同一 `demo_1.jpg`、v4 mobile/v5 mobile/v6 tiny，在 ONNX Runtime 1.23.2 CPU 与 CUDA 上验证了以下行为。设备为 Windows x64 / RTX3060 Laptop；这不是所有模型/后端的完整验收。

| 检查 | 实测结果 |
| --- | --- |
| 同样的 Clamp/320 设置，改动前后各 6 组 | 文本 SHA 与完整结果合同 SHA 均一致 |
| Clamp/320 的宽度记录 | v4 有 13/16 个区域压缩，v5/v6 各 12/16；两后端一致 |
| Reject/320，各 6 组负向调用 | 都返回 `DS-VISUAL-4103` 和源区域索引，未作为模型“不支持”掩盖 |
| Reject/3200，各 6 组 | 完整流水线通过；压缩区域为 0，同版本 CPU/CUDA 文本 SHA 一致 |

将宽度从 320 改为 3200 后，本图实际规划的最大宽度是 859～973，而不是给每行都分配 3200。文本指纹有变化，但目前没有人工标注真值，不能将变化直接称为准确率提升。warmup=1/iterations=3 的短测用于验证行为，不作为性能结论；性能调优与长文本完整性指标将在后续步骤单独验收。

### 滑窗调用与接缝诊断验证（2026-09-16）

同一 Windows x64 / RTX3060 Laptop 6 GB、驱动 576.02，使用 ORT 1.23.2 CPU/CUDA、OpenCV 5.0、完整流水线 Batch=4、独立通道=1、warmup=1、iterations=3。输入仍为 `demo_1.jpg`，SHA-256 为 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`。先验证 Clamp/320：6 组文本 SHA 和完整结果 SHA 与加入滑窗前相同。

SlidingWindow/320、20% 重叠、最少匹配 2 个 token 时，6 组调用都成功，原检测行仍为 16 个；同一版本 CPU/CUDA 的文本 SHA、切片数和接缝诊断一致。中间 crop 宽度不超过配置上限，几何诊断压缩数为 0。

| 模型 | 切片行数 | 总 REC 窗口（含普通行） | 不确定接缝 / 接缝总数 | CPU / CUDA 完整流水线 P50（ms） |
| --- | --- | --- | --- | --- |
| v4 mobile | 13/16 | 40 | 18/24 | 1005.012 / 531.559 |
| v5 mobile | 12/16 | 37 | 15/21 | 776.414 / 596.205 |
| v6 tiny | 12/16 | 35 | 11/19 | 224.065 / 359.944 |

将重叠调到 35% 后，6 组调用再次成功且同版本 CPU/CUDA 文字一致；v4/v5/v6 tiny 的总窗口变成 45/40/38，不确定接缝分别为 13/12/11。该图仍存在未匹配接缝，不能把诊断减少直接当作准确率提升。重复字、截断字符和相邻窗口识别分歧需要结合人工真值评估。

上表是繁忙开发设备上的短测，含构建与其他负载干扰，**不是最优性能记录**。SlidingWindow 增加了识别工作量；支持更大动态宽度的模型应同时对照 Reject/3200。当前验证覆盖 v4 mobile、v5 mobile、v6 tiny 的 CPU/CUDA；v6 small/medium、TensorRT/OpenVINO/OpenCV DNN 的滑窗矩阵和标注长文本 CER/WER 仍待补齐。

### PP-OCR 核心七模型跨后端证据（更新于 2026-10-23）

2026-10-23 在 Ubuntu 22.04.5 / RTX 2060 主机上用当前测试源复跑七组核心流水线，形成[三图 ORT/OpenVINO 对照报告](../../eng/models/paddle-ocr/verification/paddleocr-core-seven-models-ort-openvino-three-image-linux-rtx2060-20261023.md)。21/21 对的输入与模型文件 SHA、检测区域数、识别区域数及有序文本 SHA 一致；但完整 `OcrResult` 指纹是 0/21 一致，因此只可称文本和计数一致，不能称完整结果 parity。两后端均为 CPU，RTX 2060 只是主机配置；每对单次执行没有人工逐字真值，不提供 CER/WER，也不构成性能基准。原始 ORT/OpenVINO JSON 和可重跑比较脚本均已链接在报告中。

另在 Windows Ryzen 7 5800H 上将同一组三图、七个模型对 ONNX Runtime CPU 与 OpenVINO CPU 做了逐区域组件审计，见[组件与数值漂移报告](../../eng/models/paddle-ocr/verification/paddleocr-core-seven-models-ort-openvino-component-parity-windows-20261023.md)。文本、区域数及 CTC token 决策 `21/21` 一致，几何、检测分数和识别置信度均在声明容差内。逐字段归因显示坐标 `8/2,216` 个标量有位级差异，检测分数 `248/277`、识别置信度 `245/277`、对齐 token 置信度 `12,366/15,937` 有位级差异；最大坐标差 `0.00385 px`（门槛 `0.01 px`），最大 token 置信度差 `0.0003796`（门槛 `0.001`）。因此 `OcrResult` 的严格 SHA 不同代表浮点值并非位级相同，不表示文本/token 决策不同。该归因只定位输出字段与样本，不证明运行时内部数值差异的根因；这些无真值、单次运行的演示图仍不能用于准确率或性能结论。

### PaddleOCR 质量证据状态索引（2026-10-14）

为避免把大量分散的质量报告误读成“所有模型、所有后端都已通过”，新增了[PaddleOCR 质量证据状态索引](../../eng/models/paddle-ocr/verification/paddleocr-quality-status-20261014.md)及其[机器可读 JSON](../../eng/models/paddle-ocr/verification/paddleocr-quality-status-20261014.json)。索引把当前证据分成“诊断通过、受控诊断、部分通过、未完成”四类：核心 v4/v5/v6 七模型的 SROIE 十页、HierText v6 Small 的 1,557 条真实行级 crop、HierText v6 Medium 三后端 34 页、FUNSD v4/v6 40 页、v6 Small 三后端十页，以及本轮 A3 已知角度 retry 均有明确报告链接。

随着核心识别器行级证据补入，另有[2026-10-18 状态增量索引](../../eng/models/paddle-ocr/verification/paddleocr-quality-status-20261018.md)及其[机器可读 JSON](../../eng/models/paddle-ocr/verification/paddleocr-quality-status-20261018.json)。它只补充 v4/v5/v6 七个 REC 工件的 14 组 ORT/OpenVINO 诊断和当前开放门禁，基线索引与增量索引都不改变后端支持状态。

本轮新增的[2026-10-23 质量状态增量索引](../../eng/models/paddle-ocr/verification/paddleocr-quality-status-20261023.md)及其[机器可读 JSON](../../eng/models/paddle-ocr/verification/paddleocr-quality-status-20261023.json)记录了 SROIE 自然来源收据、FUNSD 原始噪声扫描的三配方增强共识诊断，以及独立 16 词选集的配对 5+50 成本。两组选集均未显示固定增强策略的总体 CER/WER 收益，默认增强仍关闭；CPU 成本测量不代替业务质量/延迟预算。

该索引同时保留质量边界：SROIE/FUNSD 的标注粒度与 PP-OCR 文本行输出不完全一致；OpenCV 在 v6 Medium 34 页上存在文本差异；一次测量的 P50/P95 不是正式 `5 warm-up + 50 measured` 性能；受控旋转、退化和增强实验不能替代自然角度/自然低质量真值。归一化识别宽度超过 `3,200 px` 的自然标注行及接缝质量（A2）、自然角度（A3）、B1b/B2/B3 鲁棒性与误纠成本、正式质量门以及完整模型/后端矩阵仍保持未完成，增强默认仍关闭。A2 的单位是像素宽度，历史记录中要求“自然连续 3,200 字符”的表述已更正。

新增的[核心识别器 HierText 行级质量证据](../../eng/models/paddle-ocr/verification/paddleocr-core-recognizer-quality-hiertext-20261018.md)把 v4/v5/v6 的七个 REC 工件放在同一批 256 个 source-linked 文本行 crop 上，显式跳过 DET，因此可单独观察识别器 CER/WER。ORT CPU 与 OpenVINO CPU 均完成 `14/14` 组合，v6 Small/Medium 在这批选择上的 CER/WER 为 `25.00%/48.17%` 与 `23.07%/45.23%`，两后端的逐行文本 SHA 完全一致。该结果是可复现的识别器诊断，不是完整页面准确率、检测召回率、自然长文本、鲁棒性或发布质量结论；机器 JSON 同时记录模型、字典、输入 crop 和逐行结果合同 SHA。

[历史文本长度盘点](../../eng/models/paddle-ocr/verification/paddleocr-longtext-coverage-a2-20261014.md)扫描 8 个 JSONL、243 页和 32,694 条有效标注行，最长自然行 132 字符、最长单页累计 2,348 字符。这些统计只描述字符数覆盖，不能用来判断是否达到 A2 的像素宽度要求；页面累计、拼接 crop 和合成文本也不能替代自然宽行准确率。

[HierText 自然宽行几何审计](../../eng/models/paddle-ocr/verification/hiertext-natural-wide-coverage-a2-20261017.md)以目标高度 48 找到 13 条宽度超过 3,200 像素的自然行，分布于 9 张已缓存并校验 SHA 的图像，最大投影宽度 4,725。[修复前真值四边形基线](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-gt-20261023.md)直接调用 `RecognizeOnlyAsync`，绕过 DET/CLS，以主库 `MaximumWidth=3200` 对照 Clamp 和 SlidingWindow。v6 Small/Medium × ORT/OpenVINO CPU 共 104 次行级调用通过执行、源坐标、窗口和 CTC 重建合同；52 对文本、发射类别和窗口决策一致。主质量层为官方可读且源图框高至少 2px 的 11 行；1 条官方不可读行与 1 条 1px 高行单独保留。修复前 Small 的 Clamp/滑窗 CER 为 `1.498%/4.430%`，Medium 为 `1.759%/6.450%`，部分未匹配接缝保留了重复前缀。

[接缝修复后复测](../../eng/models/paddle-ocr/verification/paddleocr-natural-wide-seam-comparison-20261023.md)在同组数据上重跑了 104 次真实识别，并用原始 CTC token 回放隔离合并逻辑。52 组滑窗的文字、指标和窗口决策与回放一致，原始窗口的几何/文字/发射类别摘要及 52 组 Clamp 结果不变。两后端 Small 的滑窗字符编辑数 `68→31`（CER `4.430%→2.020%`），Medium `99→29`（`6.450%→1.889%`）；主质量层没有行级 CER 恶化。这组诊断数据也用于定位修复，**不是独立验证**；Clamp 仍分别只有 23/27 个字符编辑，优于滑窗，因此默认继续使用 Clamp。A2 的自然重复编号/单字符接缝、其余后端及更广数据验证仍开放。合并阶段的 5+50 成本另行记录，不能解读为完整 OCR 提速；图像与原文仅保留在本机。[真实复测摘要](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-seam-followup-20261023.md)保留各模式和诊断层结果。

[旧 DET 关联报告](../../eng/models/paddle-ocr/verification/hiertext-natural-wide-a2-v6-small-20261017.md)使用的窗口上限实际是 **320px**，两后端各关联 8/13 候选。2026-10-23 从原始预测修正其 WER 算法后，匹配候选的宏平均 WER 为 `44.36%`，原 `291.20%` 无效；跨后端文本应记为 `8/8` 已关联对一致，5 对未关联的空占位不计作文本通过。旧时延未复测，不能将该 320px / DET 关联协议与新 3,200px / 真值裁剪的语料级 CER/WER 混用。

为避免把单张演示图当成完整覆盖，固定使用 `demo_1.jpg`、`demo_2.jpg`、`demo_3.jpg`，对 PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium 共 7 组 DET+CLS+REC 流水线分别运行 ONNX Runtime CPU 与 OpenVINO CPU。21 个组合在两个后端都完成检测、裁剪、方向、识别和合并；区域数与已识别数逐组合一致，按区域文本序列的 SHA-256 也为 `21/21` 一致。v6 没有独立版本化 CLS 归档，明确复用 PP-OCRv5 mobile CLS。

该轮只使用演示图，没有逐字人工真值，因此不能从它计算 CER/WER、召回率或准确率；每组仅一次端到端调用，耗时不替代 5 次预热 + 50 次正式性能协议。模型、输入、结果摘要和可复现命令见[三图跨后端机器可读记录](../../eng/models/paddle-ocr/verification/paddleocr-core-three-image-openvino-ort-20260930.md)，复现脚本为 `eng/models/paddle-ocr/scripts/Invoke-PaddleOcrCoreThreeImageEvidence.ps1`。

同一批输入还对七个 DET 工件保存了[原始 DB 概率图中间张量对照](../../eng/models/paddle-ocr/verification/paddleocr-core-det-intermediate-parity-20260930.md)。ORT/OpenVINO 的输入和输出形状在 21 组中全部一致，最大元素级绝对差为 `0.0067548752`（阈值 `0.01`）。这是后端合同证据，仍不包含人工框标注，因此不能替代检测召回率、IoU 或端到端 OCR 质量评测。

在同一 SROIE 十页 selection 上，PP-OCRv4 Mobile 也完成了 ORT/OpenVINO 端到端质量对照：两个后端均 `10/10` 页成功，检测 TP/FP/FN `502/53/40`（F1 `91.52%`），matched CER/WER `37.59%/71.88%`，端到端 CER/WER `43.78%/77.87%`；555 个索引区域和 `10/10` 页有序文本一致，polygon 漂移为 `0`，最大 confidence 漂移 `7.62e-5`。一次性总耗时 P50/P95 为 ORT `4347.26/5796.95 ms`、OpenVINO `2032.36/3267.12 ms`。这是 SROIE 词框与 PP-OCR 文本行粒度不一致下的 smoke 记录，不是官方准确率或正式 5/50 性能结论；详见 [PP-OCRv4 Mobile 质量记录](../../eng/models/paddle-ocr/verification/sroie-v4-mobile-10page-quality-parity-20261010.md)。

同样的十页协议也覆盖了 PP-OCRv4 Server：ORT/OpenVINO 均 `10/10` 页完成，检测 TP/FP/FN `482/52/60`（F1 `89.59%`），matched CER/WER `36.22%/75.55%`，端到端 CER/WER `44.89%/82.64%`；534 个索引区域和 `10/10` 页有序文本一致，polygon 漂移为 `0`，最大 confidence 漂移 `1.98e-5`。一次性总耗时 P50/P95 为 ORT `8063.73/10209.76 ms`、OpenVINO `5901.97/19871.63 ms`。该记录同样只用于 SROIE 词框/文本行 smoke 和跨后端合同对照，详见 [PP-OCRv4 Server 质量记录](../../eng/models/paddle-ocr/verification/sroie-v4-server-10page-quality-parity-20261010.md)。

PP-OCRv5 Server 也在同一十页选择上完成 ORT/OpenVINO 对照：两后端均 `10/10` 页完成，检测 TP/FP/FN `496/38/46`（F1 `92.19%`），matched CER/WER `38.99%/68.46%`，端到端 CER/WER `45.77%/74.38%`；534 个索引区域和 `10/10` 页有序文本一致，polygon 漂移为 `0`，最大 confidence 漂移 `5.804e-5`。一次性总耗时 P50/P95 为 ORT `5446.37/6584.76 ms`、OpenVINO `3537.17/4442.24 ms`。详见 [PP-OCRv5 Server 质量记录](../../eng/models/paddle-ocr/verification/sroie-v5-server-10page-quality-parity-20261010.md)；它仍是 SROIE 词框/文本行 smoke，不是正式准确率或 5/50 性能结论。

为统一当前源码和协议，PP-OCRv5 Mobile 又完成了[十页 ORT/OpenVINO 质量对照](../../eng/models/paddle-ocr/verification/sroie-v5-mobile-10page-quality-parity-20261010.md)。两后端均 `10/10` 页完成，IoU0.5 TP/FP/FN `492/46/50`（F1 `91.11%`），matched CER/WER `38.09%/69.80%`，端到端 CER/WER `45.11%/76.40%`；538 个索引区域和页面有序文本全部一致，polygon 坐标无漂移，最大 confidence 漂移 `2.25e-5`。一次性总耗时 P50/P95 为 ORT `2640.82/3289.84 ms`、OpenVINO `1113.67/1468.35 ms`。这是 one-warm-up/one-measured-iteration 的词框/文本行 smoke 证据，不是官方 SROIE 准确率或正式 5/50 性能结果；早期三后端记录仍单独保留用于 OpenCV DNN 边界，不能与本次 current-revision 证据混用。

随后对同一三图集合补做了[OpenCV DNN 与 ORT 的七组全流程对照](../../eng/models/paddle-ocr/verification/paddleocr-core-three-image-opencv-ort-20260930.md)。21/21 组合均完成 `det → crop → cls → rec → merge`，区域文本、置信度和多边形坐标全部匹配；v4 的精确输出名、legacy CLS shape 和 OpenCV batch=1 约束已写入复现合同。OpenCV 单次耗时明显高于 ORT，只作为兼容性证据，不替代正式性能矩阵。

随后补充了[v6 Medium HierText 质量记录](../../eng/models/paddle-ocr/verification/hiertext-v6-medium-public-ocr-20261002.md)：同一 24 页标注选择使用 ORT CPU 和 SlidingWindow，重叠 `0.2`、每区域最多 32 个窗口、识别 batch 16、单通道，24/24 页面完成且无失败或空输出。IoU 0.5 检测 TP/FP/FN 为 `459/478/561`（precision/recall/F1 为 `48.99%/45.00%/46.91%`），匹配区域 CER/WER 为 `12.12%/32.58%`，端到端 CER/WER 为 `71.76%/98.42%`；总耗时 P50/P95 为 `1928.53/4687.44 ms`。这是公开数据 smoke 证据，不是发布精度或正式 5/50 性能结果；未按归一化宽度超过 3,200px 与接缝真值单独验收 A2。

同一批次又以 OpenVINO CPU 复跑，并形成[ORT/OpenVINO 质量 parity 记录](../../eng/models/paddle-ocr/verification/hiertext-v6-medium-ort-openvino-quality-parity-20261002.md)。24 页文本序列、检测计数、CER/WER 全部一致；937 个对应预测区域的文本和 polygon 坐标一致，置信度最大绝对差 `2.57e-5`。OpenVINO 总耗时 P50/P95 为 `1325.58/2646.71 ms`，只能解释为当前主机和单次协议下的观察，不代表跨设备性能排名或准确率结论。

同一精确组合也完成了[OpenCV DNN 质量 smoke](../../eng/models/paddle-ocr/verification/hiertext-v6-medium-opencv-quality-20261002.md)。检测几何与 ORT 完全一致（TP/FP/FN `459/478/561`），但识别是后端相关的：23/24 页文本序列、937 个对应区域中的 174 个文本不同；OpenCV 匹配 CER/WER 为 `11.89%/30.48%`，端到端 CER/WER 为 `71.14%/96.89%`，总耗时 P50/P95 为 `11971.02/26997.76 ms`。这证明当前 OpenCV 导入器可以完成该组合，但不能把 ORT/OpenVINO 的文本 parity 或性能结论外推到 OpenCV。

随后将 sample-003 的 24 页与不重叠的 sample-002 10 页按同一模型、SlidingWindow 和测量协议合并，形成了[HierText v6 Medium 三后端 34 页汇总](../../eng/models/paddle-ocr/verification/hiertext-v6-medium-34page-three-backend-quality-20261003.md)。ORT、OpenVINO 和 OpenCV DNN 均完成 `34/34` 页，检测 TP/FP/FN 均为 `646/722/911`；ORT/OpenVINO 的页面文本序列和区域数 `34/34` 一致，OpenCV 与 ORT 的页面文本仅 `11/34` 一致、对应区域有 `178/1368` 个文本差异。该汇总的匹配 CER/WER 分别为 ORT/OpenVINO `13.04%/35.63%`、OpenCV `12.85%/33.91%`，总耗时 P50/P95 分别为 ORT `1687.40/3572.89 ms`、OpenVINO `1131.57/2255.66 ms`、OpenCV `9646.35/24180.50 ms`。34 页仍是公开数据 smoke，不能替代正式行级精度、5/50 性能协议或自然超长文本验收；旧的 24 页 OpenCV 记录保留为历史单批次结果。

同一质量 runner 还补充了[PP-OCRv6 Small SROIE 10 页 ORT/OpenVINO 记录](../../eng/models/paddle-ocr/verification/sroie-v6-small-10page-quality-parity-20261009.md)。两个五页分片先分别运行，再由 `Merge-PaddleOcrPublicDatasetRuns.py` 校验协议并合并，避免一次长进程占满资源；两后端均完成 `10/10` 页，IoU0.5 TP/FP/FN `489/42/53`（F1 `91.15%`），matched CER/WER `30.42%/46.03%`，端到端 CER/WER `37.63%/55.74%`，页面文本序列 `10/10` 一致，531 个区域索引文本差异为 `0`。该数据仍是词框/文本行异粒度的 smoke 诊断，21 个 ≥32 字符区域；未构成归一化宽度超过 3,200px 的 A2 专项。P50/P95 仅为 one-shot 观察，不能当作官方 SROIE 准确率或正式性能结果。

同一十页 SROIE 协议随后覆盖了[PP-OCRv6 Tiny ORT/OpenVINO 质量对照](../../eng/models/paddle-ocr/verification/sroie-v6-tiny-10page-quality-parity-20261010.md)。两后端均完成 `10/10` 页，IoU0.5 TP/FP/FN `475/61/67`（F1 `88.13%`），matched CER/WER `33.39%/56.81%`，端到端 CER/WER `43.73%/68.14%`；536 个索引区域和页面有序文本全部一致，polygon 坐标无漂移，最大 confidence 漂移 `7.57e-6`。一次性总耗时 P50/P95 为 ORT `336.38/488.29 ms`、OpenVINO `196.15/249.63 ms`。这是 one-warm-up/one-measured-iteration 的词框/文本行 smoke 证据，不是官方 SROIE 准确率或正式 5/50 性能结果，也不扩展到 CUDA、TensorRT 或 OpenCV DNN；自然长文本和正式质量门仍保持开放。完整机器摘要见[JSON](../../eng/models/paddle-ocr/verification/sroie-v6-tiny-10page-quality-parity-20261010.json)。

随后将同一十页 selection 扩展到[PP-OCRv6 Medium ORT/OpenVINO 质量对照](../../eng/models/paddle-ocr/verification/sroie-v6-medium-10page-quality-parity-20261010.md)。两后端均完成 `10/10` 页，IoU0.5 TP/FP/FN `490/51/52`（F1 `90.49%`），matched CER/WER `29.97%/44.33%`，端到端 CER/WER `37.41%/54.82%`；541 个索引区域和页面有序文本全部一致，polygon 坐标无漂移，最大 confidence 漂移 `2.63e-5`。一次性总耗时 P50/P95 为 ORT `4450.38/6082.79 ms`、OpenVINO `2533.57/3240.18 ms`。这仍是 one-warm-up/one-measured-iteration 的词框/文本行 smoke 证据，不是官方 SROIE 准确率或正式 5/50 性能结果，也不扩展到 CUDA、TensorRT 或 OpenCV DNN；自然长文本和正式质量门保持开放。完整机器摘要见[JSON](../../eng/models/paddle-ocr/verification/sroie-v6-medium-10page-quality-parity-20261010.json)。

随后以同一页选集、同一源码 revision 和 batch=1 协议补做了[PP-OCRv6 Small 三后端质量对照](../../eng/models/paddle-ocr/verification/sroie-v6-small-10page-three-backend-quality-20261009.md)。ORT、OpenVINO 和 OpenCV DNN 均完成 `10/10` 页，检测 TP/FP/FN `489/42/53`、531 个区域和页面有序文本全部一致；P50/P95 观察分别为 `1302.87/2534.95 ms`、`779.76/1054.15 ms`、`7722.31/10131.55 ms`。OpenCV 的 batch=1 是当前静态 importer 合同，不代表批量性能；该记录仍是 SROIE 词框/文本行异粒度的 smoke，不能关闭自然长文本、正式质量或 5/50 性能门。完整机器摘要见[JSON](../../eng/models/paddle-ocr/verification/sroie-v6-small-10page-three-backend-quality-20261009.json)。

为统一当前 revision，v6 Small 又完成了[十页 ORT/OpenVINO 质量对照](../../eng/models/paddle-ocr/verification/sroie-v6-small-10page-quality-parity-20261010.md)。两后端均 `10/10` 页完成，IoU0.5 TP/FP/FN `489/42/53`（F1 `91.15%`），matched CER/WER `30.42%/46.03%`，端到端 CER/WER `37.63%/55.74%`；531 个索引区域和页面有序文本全部一致，polygon 坐标无漂移，最大 confidence 漂移 `3.028e-5`。一次性总耗时 P50/P95 为 ORT `1140.60/1839.85 ms`、OpenVINO `647.60/781.49 ms`。这是 one-warm-up/one-measured-iteration 的词框/文本行 smoke 证据，不是官方准确率或正式 5/50 性能结果；早期三后端记录的 OpenCV 边界保持独立。七个核心模型的统一索引见[十页汇总](../../eng/models/paddle-ocr/verification/sroie-core-seven-models-10page-quality-summary-20261010.md)。

### 2026-09-29 受控与真实关联边界补充

- **滑窗接缝**：PP-OCRv6 Small 的两组受控 3,600 字符样本在 ORT/OpenVINO CPU 上分别使用 18/16 个窗口；等长、位置对齐的 token 替换回退后，CER/WER 为 `0.0556%/0.4556%` 与 `0.0833%/0.7194%`。这仍不是自然长文本准确率。
- **可归因长文本**：从 18 个 HierText 父图选取 102 个真实 text-line crop 拼接成 3,603 字符，ORT/OpenVINO 均使用 32 个窗口并保留每窗口来源 crop/字符区间映射；两后端文本 SHA 一致，CER/WER `19.789%/50.72%`。这是来源可追溯 composition，用于验证真实字形和映射，不是自然连续行准确率。详见 [HierText 长文本记录](../../eng/models/paddle-ocr/verification/hiertext-composed-long-a2-20260929.md)。
- **文本长度覆盖审计**：HierText `sample-002`/`sample-003` 两份 manifest 共 34 页、1,557 条有效行，最长行 132 字符；完整 validation 标注共 1,724 图、104,139 行，最长行 182 字符。详见[选集盘点](../../eng/models/paddle-document/verification/hiertext-longtext-coverage-a2-20261012.md)与[完整标注盘点](../../eng/models/paddle-document/verification/hiertext-full-annotation-longtext-a2-20261012.md)。这些历史字符统计不能充当 A2 像素宽度门禁；自然宽行应按 `DescribeWidth(...).NaturalWidth > 3200` 选取，拼接样本只用于受控测试。
- **自然宽行对照**：[Small/Medium 真值四边形记录](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-gt-20261023.md)覆盖 13 条自然宽行、两种宽度策略及 ORT/OpenVINO CPU，实际 104 次调用、52 个跨后端对均完成。11 条主质量行上的滑窗 CER 比压缩更高，未匹配接缝仍需改进；其余 2 条诊断行没有混入主质量指标。[旧 DET 关联记录](../../eng/models/paddle-ocr/verification/hiertext-natural-wide-a2-v6-small-20261017.md)另外注明 320px 窗口、WER 重算及仅 8/8 有效文本对；旧 one-shot 时延不是新 3,200px 协议的性能证据。自然数据已可用，A2 尚缺的是接缝质量与其余后端验收。
- **已知角度**：4 个不同 SROIE receipt 页的真实标注 crop 旋转到 8 个已知角度；0° case-folded CER/WER `0%/0%`，±12° 明显退化，±90° 全空输出，180° 仍有误差。该结果用于验证方向校正边界，不代表自然旋转分布成绩。详见 [SROIE 已知角度记录](../../eng/models/paddle-ocr/verification/sroie-known-angle-a3-20260929.md)。
- **正确四边形校正**：同一批真实旋转 crop 在提供正确 source quadrilateral 后，Perspective/Affine 两种变换在 ORT/OpenVINO 各 32/32 条 case-folded CER `0%`，polygon round-trip 为 0；这证明几何校正路径，不证明自动角度分类或自然旋转分布。详见 [A3 四边形校正记录](../../eng/models/paddle-ocr/verification/sroie-angle-rectification-a3-20260929.md)。
- **退化 smoke**：2026-09-29 的 4 页样本各生成 6 种退化，ORT/OpenVINO 各 24/24 通过；case-folded CER/WER 均为 `0%/0%`，说明退化强度不足以评估增强收益。2026-10-02 又扩展到 10 个父页、60 条 severe word-crop 记录：两后端识别文本 60/60 一致，blur 平均 case-folded CER/WER 为 `8%/10%`，增强候选 22 条选中但选中行仍为 `0% -> 0%`，默认增强继续关闭。两组都属于可追溯 word-crop smoke，不能替代行/页级准确率。详见 [B1b 扩展记录](../../eng/models/paddle-ocr/verification/synthetic-degraded-b1b-20261002.md) 和 [B1b 原始汇总](../../eng/models/paddle-ocr/verification/synthetic-degraded-b1b-20261002-summary.json)。
- **FUNSD 退化扩展**：在历史 32 个父页/192 条记录之外，又用 FUNSD `test/full-001` 的全部 40 个父页词 crop 生成 240 条 severe 记录；ORT/OpenVINO 文本 `240/240` 一致，blur 平均 case-folded CER/WER `92.61%/96.67%`、空输出 `19/40`。增强候选共选中 16 条，置信度选中行 `5/16` 的 CER 变差（`31.25%`），所以默认增强继续关闭。该结果仍是 word-crop smoke，不能当作 FUNSD 行/页准确率。详见 [FUNSD 40-crop B1b 记录](../../eng/models/paddle-ocr/verification/synthetic-degraded-funsd-b1b-20261012.md)、[汇总 JSON](../../eng/models/paddle-ocr/verification/synthetic-degraded-funsd-b1b-20261012-summary.json) 和 [增强安全/成本 JSON](../../eng/models/paddle-ocr/verification/ocr-enhancement-safety-cost-funsd40-20261012.json)。
- **识别-only 共识增强扩展**：同一 40 个 FUNSD 父 crop/六种条件改由真值完整区域进入 `RecognizeOnlyAsync`，修复后方向/宽度/增强重试均按 Profile 执行且不调用 DET。ORT/OpenVINO 的 240 条决策和最终文本完全一致；三配方共识在零增益门限选择 115 条，但 CER 改善/恶化仅为 `2/4`。全量阈值扫描没有找到零恶化且有改善的档位；按父 crop 隔离的 5 折检查中，五个校准折也全部弃权。详见[共识安全/成本报告](../../eng/models/paddle-ocr/verification/paddleocr-funsd40-consensus-safety-cost-20261013.md)。因此默认增强继续关闭，B2/B3 仍需自然低质量独立数据集、业务误纠门和正式成本协议。
- **整页退化扩展**：基于 10 张带 SROIE 多边形真值的页面生成 normal、low-contrast、blur、noise、shadow 和 JPEG 六种 severe 条件，共 60 条完整 `det → rec` 流程。ORT/OpenVINO 均 `60/60` 无失败或空输出，IoU0.5 检测 F1 和匹配 CER 分别随条件记录；两后端逐页区域数和文本 `60/60` 一致，3169 个对应区域文本无差异，多边形最大差 `0`、置信度最大差 `4.83e-5`。其中 blur 的检测 F1 为 `79.42%`、匹配 CER `58.94%`，normal 基线为 `90.49%`、`32.04%`；该结果明确了整页模糊退化的真实边界，但仍是受控生成 smoke，不是自然低质量分布或默认增强收益证明。详见 [ORT 整页报告](../../eng/models/paddle-ocr/verification/sroie-page-degraded-b1b-20261004-ort.md)、[OpenVINO 整页报告](../../eng/models/paddle-ocr/verification/sroie-page-degraded-b1b-20261004-openvino.md) 和 [跨后端 parity](../../eng/models/paddle-ocr/verification/sroie-page-degraded-b1b-20261004-ort-openvino-parity.md)。
- **真实文档阅读顺序**：FUNSD 三页 smoke 的预测文本行与词级标注 aggregate reading-order pair accuracy `70.92%`，三页均非单调，不能把通用几何 OCR 排序当作多栏版面模型。详见 [FUNSD C3 三页边界](../../eng/models/paddle-ocr/verification/funsd-reading-order-c3-3pages-20260929.md)。
- **自然角度数据边界**：HierText 缓存的旋转元数据有 476 个非零角度条目，但与 1,724 个官方标注图像 ID 的交集为 0；因此不能把无真值的 Open Images 图片当成自然角度 OCR 评测。详见 [HierText 旋转覆盖审计](../../eng/models/paddle-ocr/verification/hiertext-rotation-coverage-a3-20261002.md)，A3 仍需要合法可归因的自然或明确标注角度数据。
- **方向字段全量审计**：进一步扫描 `F:\OCRBenchmarkTesting` 当前全部 8 个 JSONL manifest（243 页、33,425 个实例）后，显式自然方向标签仍为 `0`；209 张图的方向为 unknown，23 个实例仅标记为 vertical，非零角度字段为 `0`。这只是数据覆盖审计，不能把竖排或几何推断当作 upright/inverted 真值。详见 [A3 方向字段全量审计](../../eng/models/paddle-ocr/verification/paddleocr-orientation-coverage-a3-20261014.md) 和 [机器 JSON](../../eng/models/paddle-ocr/verification/paddleocr-orientation-coverage-a3-20261014.json)。
- **自然竖排切片**：18 条官方 `vertical=true` 且有文本的 HierText 行在 ORT/OpenVINO 上分别执行 0°、顺时针 90°、逆时针 90° 候选；两后端 18/18 候选文本一致，按真值选择的 oracle 只有 6/18 exact、平均 CER `46.3356%`。这是自然竖排输入和方向候选合同证据，不是自动方向分类准确率。详见 [HierText 竖排 A3 记录](../../eng/models/paddle-ocr/verification/hiertext-vertical-a3-20261002.md)。
- **PP-LCNet 二分类方向合同**：使用 128 条可追溯水平 crop 构造 256 个已知变换行（原图 0°、OpenCV 旋转 180°），在 ORT/OpenVINO CPU 上以 `[2,3,80,160] → [2,2]` 动态 Batch 执行；两后端预测、类别和输入 digest `0` 差异。构造标签诊断为 `134/256`（`52.34%`），不是自然方向准确率，因为 HierText 没有 upright/inverted 真值且选集含极小 crop。该证据只关闭 PP-LCNet 的预处理、动态 Batch 和 CPU parity 合同；模型只有 0°/180° 两类，90°/270° 与 A3 自动方向质量门仍开放。详见 [动态 Batch 方向合同](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-orientation-classifier-dynamic-batch-20261014.md)。
- **重试策略实测**：在 4 个真实 SROIE 页的 32 个已知角度 crop 上启用 `OcrOrientationRetryOptions`（置信度阈值 `1`、候选 `90°/180°/-90°`），ORT/OpenVINO 都执行 `32/32`，右角可比较行 `16/16` 选中预期方向，所有逐行文本和方向结果一致。该结果只证明候选规划、重试调度、选择元数据和跨后端合同，不是自然旋转准确率，也不批准该阈值为默认生产策略。详见 [A3 重试策略记录](../../eng/models/paddle-ocr/verification/sroie-angle-orientation-retry-a3-20261014.md) 和 [机器 JSON](../../eng/models/paddle-ocr/verification/sroie-angle-orientation-retry-a3-20261014.json)。

- 检测、裁剪/warp、识别 batch 准备、后端推理、CTC 解码和合并应分别计时。
- PP-OCRv5 Mobile 与 v6 Small 的真实 REC batch=4 ORT/OpenVINO parity 已在[动态 Batch 记录](../../eng/models/paddle-ocr/verification/paddleocr-recognition-dynamic-batch-ort-openvino-20261002.md)中覆盖；这仍是 crop/decoder 合同，不是页面级准确率或 GPU 性能结果。
- 视频逐帧可使用 <code>VisualPipeline.RunPrefetchedAsync</code> 重叠下一帧准备与当前帧推理。
- 多张独立图片可用 <code>RunManyAsync</code>；它是独立 Session 并发。只有在 Profile、导出图和后端都声明支持时，才会形成真正的 detector batch；否则应继续使用独立 Session/页面并发。
- GPU 后端应尽量复用输入缓冲区和 CUDA stream；TensorRT OCR 的设备侧前后处理边界见[TensorRT CUDA OCR](tensorrt-cuda-ocr.md)。

详细测速方法和批量调度方式见[推理性能测试](performance-benchmarking.md)，不同设备的实际结果见[设备性能实测](device-performance-benchmarks.md)。

### PP-Structure 动态 Batch 当前证据（2026-10-14）

按外部模型门控运行 `PaddleDocumentDynamicBatchIntegrationTests`，本机得到 **23/23 通过、0 失败、0 跳过**。测试覆盖 PP-LCNet 文档/表格分类、PP-DocLayout/PP-DocBlockLayout、RT-DETR 版面与表格单元格、PP-OCRv4 mobile/server 印章、FormulaNet/UniMERNet、SLANeXt wired/wireless 和 UVDoc。ORT CPU 与 OpenVINO CPU 的真实动态 Batch 绑定、辅助几何输入、解码行隔离和顺序合同均通过；SLANeXt 的 OpenVINO 行使用独立的 alpha-renamed 兼容图，不代表原始 Loop 图可直接导入。

OpenCV DNN 只对两个分类图和两张印章图完成本轮动态 Batch 执行；版面、RT-DETR 单元格的精确组合在 `forward_many` 返回 `DS-OCV-8004`，六个公式图在 batch=2 以及随后 batch=1 探针中均在导入/输入专门化阶段返回 `DS-OCV-8002`。这些是当前适配器与 OpenCV 5.0.0 的精确阻断，不外推到所有 OpenCV 版本，也不继续追查第三方 importer。

完整的模型/后端/边界表见[聚合 Markdown 报告](../../eng/models/paddle-document/verification/paddle-document-dynamic-batch-current-20261014.md)，原始 JSON 见 [`dynamic-batch-20261014/`](../../eng/models/paddle-document/verification/dynamic-batch-20261014/)。该轮只证明动态 Batch 执行和结果行隔离，不证明数据集准确率、CER/WER、正式 P50/P95、GPU 吞吐、TensorRT 覆盖或跨设备可移植性；公式渲染/数学语义质量门仍保持开放。

## 常见问题

### PaddleOCR-VL 是否已经可用于模型推理

PaddleOCR-VL-1.6 已加入 `DeploySharp.Visual` 的实验性 ONNX Runtime CPU 单图路径。当前提供 `PaddleOcrVlInputPlanning` 的任务 Prompt、动态尺寸/图像 Token 数量规划、`PaddleOcrVlTokenizer` 的精确分词和单图 Prompt 展开，以及 `PaddleOcrVlOnnxSession` 的四图动态 Bundle 生成。Tokenizer 在 `net8.0/net9.0/net10.0` 可用，加载时校验官方 `tokenizer.model`、`tokenizer.json`、`tokenizer_config.json` 和 `chat_template.jinja`，保留空格、数字、表格/定位结构标记与字节回退；其它目标框架调用该内置 Tokenizer 会返回明确的能力不可用错误。[Tokenizer 合同验证](../../eng/models/paddle-ocr/verification/paddleocr-vl-tokenizer-contract-20261023.md)覆盖六类任务、全部 1,041 个新增 Token、103,424 个词表槽位和并发请求。

```csharp
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;

var tokenizer = new PaddleOcrVlTokenizer(modelDirectory);
var imagePlan = PaddleOcrVlInputPlanning.PlanOfficialV16(
    new VisualSize(imageWidth, imageHeight), PaddleOcrVlTask.TableRecognition);
PaddleOcrVlTokenSequence prompt = tokenizer.EncodePrompt(imagePlan,
    maximumNewTokens: 512, maximumContextTokens: 8192);
long[] inputIds = prompt.CopyTokenIds(); // 也可用于审查展开后的 Prompt。
```

生产 C# ORT 路径可以直接使用 `PaddleOcrVlImageProcessor` 准备紧密 RGB 字节；如果已经引用 `JYPPX.DeploySharp.Visual.OpenCV`，则 `OpenCvPaddleOcrVlInputFactory` 会单次解码 PNG/JPEG、保留编码源 SHA256，并调用同一官方 Processor 合同生成 `[grid_h, grid_w, 3, 14, 14]` Patch Tensor。随后绑定同一导出批次的 `vision.onnx`、`embedding.onnx`、`decoder.onnx` 和 `head.onnx`：

```csharp
PaddleOcrVlPreparedInput prepared = new OpenCvPaddleOcrVlInputFactory().CreateFromFile(
    imagePath, PaddleOcrVlTask.TableRecognition);
using var vl = PaddleOcrVlOnnxSession.Create(
    registry, bundleDirectory, onnxCpuRequest);
PaddleOcrVlGenerationResult result = await vl.GenerateAsync(
    prepared, tokenizer,
    new PaddleOcrVlGenerationOptions(maximumNewTokens: 512));
Console.WriteLine($"{result.Task}: {result.TokenIds.Count} tokens, " +
                  $"{result.FinishReason}, {result.Elapsed.TotalMilliseconds:F1} ms");
Console.WriteLine(result.Text);
```

如果 Bundle 来自 Release 或共享缓存，建议先调用 `PaddleOcrVlBundleManifest.LoadFromDirectory(bundleDirectory).VerifyFiles(bundleDirectory)`，再创建会话。`PaddleOcrVlOnnxSession.CreateVerified` 会在创建四个后端 Session 前校验固定 revision、四图和四个 tokenizer sidecar 的文件名、格式、大小与 SHA-256；`PaddleOcrReleaseClient.GetBundleAsync("paddleocr-vl/1.6")` 会按九个独立资产下载同一 Bundle。仓库内的 `Write-PaddleOcrVlBundleManifest.py` 可在真实图目录和官方 tokenizer 目录存在时生成本地 Bundle 与 manifest；当前 Release 尚未上传这些大体积 ONNX 文件。

Linux 上生产 C# ORT CPU 已对 OCR、Table、Formula、Chart、Spotting、Seal 六张真实图片完成生成：分别为 `162/71/80/137/286/29` Token，全部 EOS，Token 和文本与官方参考精确一致。对应证据见[托管 C# ORT 报告](../../eng/models/paddle-ocr/verification/paddleocr-vl-managed-csharp-ort-linux-20261024.json)及[运行日志](../../eng/models/paddle-ocr/verification/paddleocr-vl-managed-csharp-ort-linux-20261024.log)。四图导出元数据和 SHA 见 [vision](../../eng/models/paddle-ocr/verification/paddleocr-vl-vision-export-20261023.json)、[embedding](../../eng/models/paddle-ocr/verification/paddleocr-vl-embedding-export-20261023.json)、[decoder](../../eng/models/paddle-ocr/verification/paddleocr-vl-decoder-export-20261023.json) 和 [head](../../eng/models/paddle-ocr/verification/paddleocr-vl-head-export-20261023.json)。

图像准备也已补充真实对照：`PaddleOcrVlImageProcessor` 对同组六任务的 `6,818,448` 个 Patch 数值与固定官方 Processor 逐值一致，包含 Spotting 的 Lanczos → Bicubic 双重缩放。Linux C# ORT 随后从原始 RGB 运行该处理器，六任务的全部 `765` 个 Token 和文本仍与官方参考一致并正常 EOS；[详细结果与复现步骤](../../eng/models/paddle-ocr/verification/paddleocr-vl-managed-processor-parity-20261024.md)说明了图像 SHA、环境和测试入口。这里的 RGB 解码来自上游 Pillow；OpenCV 编码图像解码、多页版面裁剪和其它后端仍需分别验证。

普通 PP-OCR DET/REC/CLS 用法不变，不能用 CTC 识别器加载 VL 权重。当前已提供 `PaddleOcrVlDocumentStage` 的单图页面阶段，以及 `PaddleOcrVlRegionStage` 的版面区域路由阶段：后者按区域顺序准备 crop，保留页码、区域索引、源坐标、输入 SHA、Token 和 EOS/截断状态，并可在配置后隔离单区域失败；`PaddleDocumentVisionLanguageResult.CoordinatesAvailable` 和页面聚合结果的同名属性均明确为 `false`，不会伪造 HTML、单元格、Spotting 坐标或公式结构。该区域路由已经完成接口和合同测试，但裁剪由调用方提供，尚未完成真实 VL 多页页面样例、自动阅读顺序策略、结构化归一和质量结果；OpenVINO/TensorRT/OpenCV DNN/CUDA、GPU 性能、标注集质量和正式 P50/P95 也未完成。Vision 中间特征存在严格浮点差异边界，当前结论是最终 Token/Text 生成一致，不是所有中间张量逐元素一致；该固定 1.6 实验 Bundle 也没有加入可下载模型 catalog 或 Release。核心 Visual 包接收已解码 RGB，OpenCV 适配器负责编码图像解码；官方资产身份见[来源核对](../../eng/models/paddle-ocr/verification/paddleocr-vl-official-assets-20261023.md)，详细开发状态维护在仓库外的 `plan/paddleocr` 目录。
这里的“没有加入 Release”只针对当前实际二进制资产；manifest、下载客户端和校验工厂已经进入代码。等四图和 tokenizer 上传到 `models-paddleocr` 后，将把本段更新为可下载状态，并保留同一 revision/SHA 校验边界。

### 自然宽行有哪些真实后端证据

自然宽行的后端结果可查阅 [OpenCV DNN 真实记录](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-opencv-20261023.md)与 [ORT/OpenVINO 同 DLL 参考](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-cpu-reference-20261023.md)：Small/Medium × Clamp/SlidingWindow × 13 个真值四边形，OpenCV/ORT 的 52 对文本、CTC 发射类别、CER/WER 与接缝决策全部一致，52 个原始窗口文字/类别摘要也一致，详见[跨报告比较](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-opencv-parity-20261023.md)。这是绕过 DET/CLS 的识别质量诊断，仍不代表独立准确率或速度排名。[TensorRT 11 同范围尝试](../../eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-tensorrt-interrupted-20261023.md)虽已生成两个 Engine，但原生 enqueue 未完成，结束测试后没有有效结果报告；该精确组合保留未验证，不扩展成整个模型或后端不支持。

### 部署中的常见现象

| 现象 | 处理 |
| --- | --- |
| 找不到输入或输出 | 用 Netron 检查名称、布局、dtype 和动态维度，并同步修改 Profile |
| 检测框位置偏移 | 检查 resize、letterbox、padding 和坐标空间是否与导出脚本一致 |
| 识别乱码 | 检查字符表版本、blank index、logits layout 和归一化参数 |
| batch 运行失败 | 确认识别输入声明动态 batch；静态 batch 模型应降低 batch 或创建多个 Session |
| 内存持续增长 | 复用 Pipeline，及时释放输入和结果；降低最大区域数、batch 数或并发通道 |

模型是否已经在某个后端完成真实验证，以[模型支持状态](model-support.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。
