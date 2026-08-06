# Visual OCR detection and recognition / Visual OCR 检测与识别

`JYPPX.DeploySharp.Visual` provides a backend-neutral, two-model OCR pipeline over owned tensors. `JYPPX.DeploySharp.Visual.OpenCV` is optional and supplies encoded-image decoding plus perspective crops. Backends only execute named tensors; neither Visual nor Core contains an OpenCV or inference-runtime dependency. / `JYPPX.DeploySharp.Visual` 基于自有张量提供后端无关的双模型 OCR Pipeline。可选的 `JYPPX.DeploySharp.Visual.OpenCV` 提供编码图像解码与透视裁剪。后端只执行命名张量；Visual 与 Core 均不依赖 OpenCV 或具体推理运行时。

For four-class text orientation, confidence rejection, one-decode correction and original-coordinate restoration, see [OCR orientation and automatic correction](visual-ocr-orientation.md). / 四分类文本方向、置信度拒绝、单次解码纠正和原图坐标恢复请参阅 [OCR 方向与自动纠正](visual-ocr-orientation.md)。

## Pipeline / Pipeline 流程

1. An `IOcrImageInput` supplies one detector `PreparedVisualInput`. / `IOcrImageInput` 提供一个检测器 `PreparedVisualInput`。
2. `ExplicitTextDetectionDecoder` validates named `[1,N,P,2]` polygons and `[1,N]` scores, restores every vertex through `ImageTransform`, applies exact convex-polygon NMS, and produces deterministic reading order. / `ExplicitTextDetectionDecoder` 校验命名 `[1,N,P,2]` polygon 与 `[1,N]` score，通过 `ImageTransform` 恢复每个顶点，执行精确凸多边形 NMS，并生成确定性阅读顺序。
3. Each retained quadrilateral becomes an explicit `TextCropRequest`. The image adapter performs crop, configured right-angle rotation, resize, padding, color conversion, and normalization. / 每个保留四边形生成显式 `TextCropRequest`。图像适配器负责裁剪、配置的直角旋转、resize、padding、颜色转换与归一化。
4. Crops are grouped by target width and sent in bounded batches. `GreedyCtcDecoder` converts strict named logits or probabilities to traceable tokens and text. / Crop 按目标宽度分组并按有界批次提交。`GreedyCtcDecoder` 将严格命名的 logits 或 probabilities 转换为可追踪 token 与文本。
5. `OcrResult` restores the original reading positions and owns geometry, text, tokens, profile/model provenance, stage timing, and canonical SHA256. / `OcrResult` 恢复原始阅读位置，并自有几何、文本、token、Profile/模型来源、阶段耗时及规范 SHA256。

The detector and recognizer may select the same backend or different registered backends. Their sessions, requests, options, and diagnostics remain independent. / 检测器和识别器可选择相同后端，也可选择两个不同的已注册后端；两者的 session、request、选项与诊断彼此独立。

## Strict contracts / 严格契约

| Area / 范围 | Contract / 契约 |
| --- | --- |
| Detection / 检测 | Exact names, batch one, Float32/Float64, finite values, positive strictly convex polygon, explicit coordinate space, vertex order, boundary policy, score threshold, exact polygon IoU/NMS / 精确名称、batch 1、Float32/Float64、有限值、正面积严格凸 polygon、显式坐标空间/顶点顺序/边界策略/阈值及精确 polygon IoU/NMS |
| Geometry / 几何 | `TextPolygon` owns 3–32 canonical vertices; `TextQuadrilateral` requires explicit TL/TR/BR/BL roles; no point-order guessing or `minAreaRect` / `TextPolygon` 自有 3–32 个规范顶点；`TextQuadrilateral` 要求显式 TL/TR/BR/BL 角色；不猜测点顺序，也不使用 `minAreaRect` |
| Crop / 裁剪 | Fixed or aligned dynamic width, bounded pixels/width, nearest/linear/cubic interpolation, RGB/BGR/gray, NCHW/NHWC, `(pixel - mean) * scale`, explicit padding color / 固定或对齐动态宽度、像素/宽度上限、插值、颜色、布局、`(pixel - mean) * scale` 及显式填充色 |
| CTC / CTC | `[batch,time,classes]` or `[time,batch,classes]`, Float32/Float64, explicit blank index, optional unknown index, stable softmax, lowest-index tie break, repeat collapse, blank removal, confidence aggregation, Unicode scalar character set / 两种 layout、Float32/Float64、显式 blank/可选 unknown、稳定 softmax、同分最小索引、repeat collapse、blank 移除、置信度聚合与 Unicode scalar 字符表 |
| Limits / 边界 | Source/crop pixels, polygon points, candidates/regions, width, batch, sequence, characters, workspace, result bytes, and concurrency are bounded with checked arithmetic / 源图/crop 像素、polygon 点数、候选/区域、宽度、批次、序列、字符、workspace、结果字节和并发均有界并使用 checked 算术 |

CTC collapse occurs before blank removal. A blank resets the repeated-class run. Ties choose the lowest class index. The character set excludes the explicit blank class and optional unknown class; its ID, version, scalar order, and SHA256 are part of every result. / CTC 先折叠 repeat，再移除 blank；blank 会重置重复类别序列；同分选择最小 class index。字符表不包含显式 blank 与可选 unknown 类；其 ID、版本、scalar 顺序与 SHA256 均进入结果。

## OpenCV image input / OpenCV 图像输入

```csharp
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
    detectorSelection, detectorRequest,
    recognizerSelection, recognizerRequest,
    cropProfile,
    new OcrPipelineOptions(maximumConcurrency: 1, maximumRegions: 128, maximumRecognitionBatch: 16));

OcrResult result = await ocr.RunAsync(
    input,
    new OcrExecutionOptions(timeout: TimeSpan.FromSeconds(10)),
    cancellationToken);
```

`OpenCvOcrImageInputFactory` decodes the source once and retains the source `Mat` only until the input is disposed. Each recognition batch is copied into an owned managed Float32 tensor before inference. Success, failure, cancellation, and repeated `Dispose` release temporary transforms, warps, rotations, ROIs, and the retained source. / `OpenCvOcrImageInputFactory` 只解码一次源图，并仅在 input 释放前保留源 `Mat`。每个识别批次在推理前复制为自有 managed Float32 张量。成功、失败、取消及重复 `Dispose` 路径都会释放临时变换、warp、旋转、ROI 与保留的源图。

Alpha.1 supports only detector/configuration-provided `0/90/180/270` orientation. It does not run an orientation classifier. Polygon points must already have explicit corner roles; the adapter never guesses them. / Alpha.1 仅支持检测器或配置提供的 `0/90/180/270` 方向，不运行方向分类器。Polygon 点必须已有显式角点角色，适配器绝不猜测。

## Model profiles and official fidelity / 模型 Profile 与官方保真

The repository constant graphs are `ContractVerified` fixtures, not PaddleOCR, DBNet, CRNN, SVTR, RapidOCR, accuracy evidence, or performance claims. A production profile must reproduce the selected model's official image orientation, resize rounding and bounds, padding, color order, mean/std/scale, dtype/layout, output activation, threshold, contour extraction, score, unclip, filtering, reading order, perspective crop, recognition width policy, logits layout, softmax, character set, blank/repeat/EOS/unknown semantics, and confidence aggregation. / 仓库常量图仅为 `ContractVerified` 夹具，不代表 PaddleOCR、DBNet、CRNN、SVTR、RapidOCR，也不构成精度或性能结论。正式 Profile 必须复现所选模型官方的图像方向、resize 取整与边界、padding、颜色、归一化、dtype/layout、输出 activation、阈值、轮廓提取、score、unclip、过滤、排序、透视 crop、识别宽度、logits layout、softmax、字符表、blank/repeat/EOS/unknown 及置信度语义。

Alpha.1 implements explicit polygon/score detection. Probability-map morphology and contour extraction are not advertised. Greedy CTC is implemented; beam search, language-model correction, layout analysis, table/formula recognition, translation, and VLM are unsupported. / Alpha.1 实现显式 polygon/score 检测，不声明 probability-map 形态学和轮廓提取能力；实现 greedy CTC，不支持 beam search、语言模型纠错、版面分析、表格/公式识别、翻译或 VLM。

Use the [OCR AlgorithmVerified template](ocr-algorithm-verification-template.md) before registering a production OCR suite. / 注册正式 OCR 套件前必须使用 [OCR AlgorithmVerified 模板](ocr-algorithm-verification-template.md)。

## ModelPack and ModelFactory suite / ModelPack 与 ModelFactory 套件

One ModelPack can carry four uniquely named artifacts: detector/recognizer ONNX and detector/recognizer OpenVINO IR. IR XML/BIN, character set, every model, and every test input are separate size/SHA256-protected files. Versioned `deploysharp.ocr.*` extension keys bind detector and recognizer artifact/profile IDs, character-set path/ID/version/SHA256, language/script, and preprocessing/postprocessing versions. File paths remain unique across the manifest. / 一个 ModelPack 可包含四个唯一命名工件：检测/识别 ONNX 与检测/识别 OpenVINO IR。IR XML/BIN、字符表、每个模型与每个测试输入都是独立的大小/SHA256 保护文件。版本化 `deploysharp.ocr.*` 扩展键绑定检测/识别工件及 Profile ID、字符表路径/ID/版本/SHA256、语言/脚本和前后处理版本；清单内文件路径必须全局唯一。

ModelFactory must return both stage artifacts for an OCR query. The embedded official catalog remains empty until legal assets and `AlgorithmVerified` evidence are approved; test-only Preview entries are not downloadable releases. / ModelFactory 的 OCR 查询必须返回两个阶段工件。合法资产与 `AlgorithmVerified` 证据获批前，内置官方目录保持为空；测试专用 Preview 条目不是可下载 Release。

## Cancellation, lifetime, diagnostics, and performance / 取消、生命周期、诊断与性能

One timeout budget covers detection, decode, crop, queuing, recognition, CTC, and merge. Caller cancellation maps to `DS-VISUAL-2001`; timeout maps to `DS-VISUAL-2002`; OCR stage failures use `DS-VISUAL-5001`; bounded-limit failures use `DS-VISUAL-5002`. `OcrPipelineException` includes stage, model/profile, optional region/tensor, and inner exception. / 一个超时预算覆盖检测、解码、裁剪、排队、识别、CTC 与合并。调用方取消映射为 `DS-VISUAL-2001`，超时为 `DS-VISUAL-2002`，OCR 阶段失败为 `DS-VISUAL-5001`，边界超限为 `DS-VISUAL-5002`。`OcrPipelineException` 包含阶段、模型/Profile、可选 region/tensor 与 inner exception。

The pipeline is reusable and concurrency is bounded by `MaximumConcurrency`. `Dispose` rejects new calls, cancels active work, waits for orchestration slots, and idempotently releases recognizer then detector. Whether an individual native inference can be interrupted depends on its backend contract; synchronous boundaries still observe cancellation. / Pipeline 可复用，并发由 `MaximumConcurrency` 限制。`Dispose` 拒绝新调用、取消活动工作、等待编排槽位，并按识别器后检测器顺序幂等释放。单次 native 推理能否中断取决于后端契约；同步边界仍会观察取消。

Performance reports must separate decode, detector preprocessing/backend/postprocessing, crop/warp, recognizer batch preparation/backend, CTC, and end-to-end time. Record Release build, warmup, sample count, region/batch count, P50/P95, throughput, and managed allocation. Do not infer production speed from constant fixtures. / 性能报告必须拆分 decode、检测预处理/backend/后处理、crop/warp、识别 batch 准备/backend、CTC 与端到端耗时，并记录 Release、warmup、样本数、region/batch 数、P50/P95、吞吐与托管分配；不得从常量夹具推导生产性能。

## Reproducible contract evidence / 可复现合同证据

| Asset / 工件 | Bytes | SHA256 |
| --- | ---: | --- |
| `text-detection.onnx` | 753 | `195d996fdf299d70794f5f364ddd471caa6f1d61d9afc2a48f3804a9a53c5e45` |
| `text-recognition-ctc.onnx` | 790 | `58f79db0ac32ab2d00af1c41515487a4973f380ee47c9ad09cb0dc1b0631ad2c` |
| detector IR XML/BIN | 4698 / 136 | `bf9a7c2ba433c4f9855d8d9a59330be82e31f7f34f70caefab55082e285708bf` / `8e383fa7702b211c48f2b6473049b93b4deba788eb7cb44d1a125ce317f79c66` |
| recognizer IR XML/BIN | 4315 / 236 | `f0f1a0947afde99f31ac53a09da1d51cb32338080e3aaf375395938e11edc8ac` / `d98380b468c2139385904b6b2173ff5b76a1657a9b45f79caace56cb3d16fbc1` |
| `ocr.png` | 104 | `35027bc2ab811f72928ecd8a15d4fcd6c9a25784b7ce6e4f5d055d9719a3ac3a` |
| `charset.txt` (`ABC\n`) | 4 | `8470d56547eea6236d7c81a644ce74670ca0bbda998e13c629ef6bb3f0d60b69` |

The ONNX files use `onnx==1.22.0`, opset 13, checker validation, and deterministic serialization. OpenVINO 2026.2.1 converts them to IR. CPU tests execute both models through ONNX Runtime, both ONNX and IR through OpenVINO, and the real PNG through Visual.OpenCV into ONNX Runtime. No repository test asset is an official model or GitHub Release asset. / ONNX 使用 `onnx==1.22.0`、opset 13、checker 与确定性序列化；OpenVINO 2026.2.1 转换为 IR。CPU 测试通过 ONNX Runtime 执行双模型，通过 OpenVINO 执行 ONNX 与 IR，并将真实 PNG 经 Visual.OpenCV 输入 ONNX Runtime。仓库测试资产均不是官方模型或 GitHub Release 资产。
