# OCR 检测与识别

DeploySharp 的 OCR Pipeline 将文字检测、透视裁剪、可选方向处理和 CTC 文字识别组合为一个可复用流程。检测器和识别器可以使用同一个后端，也可以分别选择 ONNX Runtime、OpenVINO、OpenCV DNN 或 TensorRT 中已经注册的后端。

Pipeline 只处理模型张量，图像解码和几何裁剪由可选的 <code>JYPPX.DeploySharp.Visual.OpenCV</code> 适配器完成。模型的输入输出名称、尺寸、归一化、字符表和 CTC 参数必须与实际导出文件一致。

## 流程

1. 图像适配器只解码一次源图，并准备检测器输入。
2. 检测 Decoder 校验 polygon 和 score，执行阈值过滤、精确 polygon NMS，并把保留区域按阅读顺序排列。
3. 每个区域生成一个裁剪请求，完成透视裁剪、直角旋转、resize、padding、颜色转换和归一化。
4. 文本行按目标宽度分组，以有界 batch 提交识别器；CTC Decoder 执行 argmax、repeat collapse、blank 移除和置信度计算。
5. 结果中的坐标通过同一个 <code>ImageTransform</code> 还原到原图，并带有阶段耗时和模型来源信息。

Alpha.1 的方向策略由 Profile 或调用方配置提供，仅支持 0、90、180、270 度直角旋转。Pipeline 不会偷偷运行方向分类器，也不会猜测 polygon 的点顺序。

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

foreach (OcrRegion region in result.Regions)
    Console.WriteLine($"{region.Text} ({region.Confidence:P1})");
~~~

<code>OpenCvOcrImageInputFactory</code> 在输入释放前保留源图；Pipeline 释放所有临时 transform、warp、旋转和 ROI。调用方负责释放自己创建的图像输入、Registry 和其他资源。

## 模型和后端配置

检测模型通常使用 batch=1；识别模型可声明动态宽度和动态 batch。识别器的 <code>maximumRecognitionBatch</code> 决定一次提交的文本行数量，<code>maximumConcurrency</code> 决定独立推理通道数量。通道数量大于一时，Registry 会创建多个独立后端 Session，并把识别 batch 分派给空闲通道。

动态宽度会产生 padding。<code>MaximumRecognitionPaddingRatio</code> 默认为 1.0，只把等宽文本行放在同一批次。目标后端经过实测后，可以适当调高该值以减少 batch 数，但要同时观察填充计算和显存占用。

一个 ModelPack 可以同时携带检测和识别 ONNX，或对应的 OpenVINO IR XML/BIN，并通过 <code>deploysharp.ocr.*</code> 扩展键绑定 Profile、字符集和预处理版本。字符表的 blank、unknown、Unicode 顺序必须和 logits 导出一致。

## 性能和并发

- 检测、裁剪/warp、识别 batch 准备、后端推理、CTC 解码和合并应分别计时。
- 视频逐帧可使用 <code>VisualPipeline.RunPrefetchedAsync</code> 重叠下一帧准备与当前帧推理。
- 多张独立图片可用 <code>RunManyAsync</code>；它是独立 Session 并发，不会把 batch=1 模型变成真正 batch。
- GPU 后端应尽量复用输入缓冲区和 CUDA stream；TensorRT OCR 的设备侧前后处理边界见[TensorRT CUDA OCR](tensorrt-cuda-ocr.md)。

详细测速方法和批量调度方式见[推理性能测试](performance-benchmarking.md)，不同设备的实际结果见[设备性能实测](device-performance-benchmarks.md)。

## 常见问题

| 现象 | 处理 |
| --- | --- |
| 找不到输入或输出 | 用 Netron 检查名称、布局、dtype 和动态维度，并同步修改 Profile |
| 检测框位置偏移 | 检查 resize、letterbox、padding 和坐标空间是否与导出脚本一致 |
| 识别乱码 | 检查字符表版本、blank index、logits layout 和归一化参数 |
| batch 运行失败 | 确认识别输入声明动态 batch；静态 batch 模型应降低 batch 或创建多个 Session |
| 内存持续增长 | 复用 Pipeline，及时释放输入和结果；降低最大区域数、batch 数或并发通道 |

模型是否已经在某个后端完成真实验证，以[模型支持状态](model-support.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。
