# PaddleOCR three-model pipeline / PaddleOCR 三模型流水线

Stage 20 closes the V1 `PaddleOcrCls` execution row by extending the existing detection and recognition pipeline with artifact-bound text-line orientation classification. It reuses `JYPPX.DeploySharp.Visual`, `Visual.OpenCV`, Core named tensors, `OcrOrientationPipeline`, `OcrOrientationWorkflow`, `OcrPipeline`, `TextRegion`, and `OcrResult`. No `Visual.PaddleOCR`, single-model package, bundled model, dictionary, image, native runtime, or TensorRT path is introduced. / 阶段 20 通过工件绑定的文本行方向分类扩展既有检测与识别流水线，关闭 V1 `PaddleOcrCls` 执行行。实现复用上述现有包与类型，不新增 `Visual.PaddleOCR`、单模型包、内置模型/字典/图片/native runtime 或 TensorRT 路径。

## Three-model quick start / 三模型快速开始

The application supplies three external ONNX files, the matching recognition dictionary, one backend, and matching native runtime packages. Profiles must bind the exact artifacts before selection. / 应用提供三个外部 ONNX、匹配的识别字典、一个后端和匹配的 native runtime 包，并在选择前用 Profile 精确绑定工件。

```csharp
PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(detectorId, detectorContract);
PaddleOcrProfile classifier = PaddleOcrProfiles.CreateTextLineOrientationClassification(
    classifierId, classifierContract, rejectionThreshold: 0.9f);
PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(
    recognizerId, recognizerContract, characters);

using var pipeline = new OcrPipeline(
    backends,
    profiles.Select(detectorArtifact, backends, request, VisualTaskId.TextDetection), request,
    profiles.Select(classifierArtifact, backends, request, VisualTaskId.TextOrientationClassification), request,
    classifier.CropProfile!,
    profiles.Select(recognizerArtifact, backends, request, VisualTaskId.TextRecognition), request,
    recognizer.CropProfile!,
    new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: 16),
    orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);

using OpenCvOcrImageInput input = imageFactory.CreateFromFile(
    imagePath, detector.VisualProfile.Input.Name,
    OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(sourceSize));
OcrResult result = pipeline.Run(input);
```

Each detected source-space polygon is perspective-cropped once for classification. An accepted `180` result becomes the region orientation; the recognition crop is then generated from the original image and rotated through the existing crop implementation. Recognition never consumes classifier-owned native memory, and returned regions, scores, text, polygons, and metadata are managed and owned by the result. / 每个源图 polygon 先透视裁剪用于分类。接受的 `180` 成为区域方向；识别 crop 随后重新从原图生成，并通过既有 crop 实现旋转。识别不消费分类器拥有的 native 内存；返回区域、分数、文本、polygon 与元数据均为托管自有数据。

The package-only example is `tests/clean-consumer/visual-paddle-ocr3`. Missing external files produce `DEPLOYSHARP_VISUAL_PADDLE_OCR3_CONSUMER_SKIP`; a real detector-classifier-recognizer run prints `DEPLOYSHARP_VISUAL_PADDLE_OCR3_CONSUMER_OK`. / 仅包示例位于上述目录。外部文件缺失时输出稳定 skip；三模型真实执行成功时输出成功标记。

## Exact classification contracts / 精确分类合同

| Contract / 合同 | Input and preparation / 输入与前处理 | Output and labels / 输出与标签 | Decision / 决策 |
| --- | --- | --- | --- |
| Legacy PaddleOCR cls | graph `x:float32[-1,3,-1,-1]`; profile locks BGR 48x192, `(byte-127.5)/127.5`, NCHW, execution batch 1 | `softmax_0.tmp_0:float32[-1,2]`, ordered `legacy-0`, `legacy-180` | argmax; artifact-bound rejection threshold |
| PP-LCNet text-line orientation | `x:float32[1,3,80,160]`, RGB, fixed resize, ImageNet mean/std, NCHW | `fetch_name_0:float32[1,2]`, ordered `0_degree`, `180_degree` | argmax; default reject threshold `0.9` |

`OcrOrientationSchema` accepts only an explicit two-class `0/180` mapping or an explicit four-class mapping. Ties select the first declared class deterministically. NaN, Infinity, incompatible names/types/shapes, and capacity excess fail before a result is returned. The decoder does not infer semantics from the filename or tensor rank. / `OcrOrientationSchema` 只接受显式二类 `0/180` 或显式四类映射；相同分数确定性选择先声明类别。NaN、Infinity、不兼容的名称/类型/shape 与容量超限会在返回结果前失败。Decoder 不从文件名或 tensor rank 推断语义。

The official PaddleOCR `configs/cls/cls_mv3.yml` legacy configuration declares BGR, `[3,48,192]`, and labels `0,180`. The separate PP-OCRv3 `ch_PP-OCRv3_rotnet.yml` declares four classes and `[3,48,320]`; it is not a two-class text-line model. The current official text classifier predictor uses `0/180` labels and a `0.9` threshold before applying a 180-degree correction. Document orientation, generic four-direction fixtures, and text-line 0/180 classification are separate contracts and must not be substituted. / 官方 legacy 配置声明 BGR、`[3,48,192]` 与 `0,180`；独立 PP-OCRv3 rotnet 声明四类和 `[3,48,320]`，并非二类文本行模型。当前官方文本分类 predictor 使用 `0/180` 与 `0.9` 阈值后执行 180 度纠正。文档方向、通用四方向 fixture 与文本行二分类是不同合同，不得替代。

The official exporter entry point is `tools/export_model.py`; its command shape is `python tools/export_model.py -c <cls-config> -o Global.pretrained_model=<checkpoint> Global.save_inference_dir=<output>`. The checkpoint URI, exact local invocation, and exporter environment for the inspected ONNX files are unverified and are recorded as blockers rather than inferred from filenames. / 官方导出入口是 `tools/export_model.py`，命令形状如上。已检查 ONNX 的 checkpoint URI、精确本地命令与导出环境未核验，作为 blocker 记录，不从文件名推断。

Prepared-tensor golden SHA256 values for the authorized repository fixture are `59d42e08e5689df6f6dc9a4e79adc0cbcfe2a5bd4fdbb5b1710e2d53d6891307` for the legacy BGR contract and `47b84a19c734aed5ee428d58702a8457573d310749095fe35650c9d7b24c1dda` for the PP-LCNet RGB contract. / 授权仓库 fixture 的 prepared tensor golden SHA256 如上，分别对应 legacy BGR 与 PP-LCNet RGB 合同。

## Orientation strategies / 方向策略

| Strategy / 策略 | API / API | Ownership and use / 所有权与用途 |
| --- | --- | --- |
| `None` | Existing two-model `OcrPipeline` constructor | Detector regions go directly to recognition; no direction is guessed. / 检测区域直接识别，不猜测方向。 |
| `WholeImage` | `OcrOrientationWorkflow` | Classify once, rotate the logical whole image, then run OCR with source restoration. / 整图分类一次，逻辑纠正后运行 OCR 并恢复源图坐标。 |
| `PerTextRegion` | New three-model `OcrPipeline` constructor | Detect, crop and classify every region, apply accepted correction, then recognize. / 检测后逐区域裁剪分类，应用已接受纠正，再识别。 |

`OcrOrientationRejectionPolicy.Fail` maps a rejected region to `OcrPipelineStage.OrientationClassification`. `UseZeroDegrees` is an explicit fallback that records `ocr.orientation.rejected=true`; it never silently treats a rejected score as accepted. Region metadata also records strategy, profile/model/backend identity, class index, confidence, and canonical classifier result SHA256. / `Fail` 把拒绝区域映射到方向分类阶段错误；`UseZeroDegrees` 是显式零度回退并记录 rejected，不会把拒绝分数静默视为接受。区域 metadata 还记录策略、Profile/模型/后端身份、类别、置信度与分类结果规范 SHA256。

## Backend evidence and diagnostics / 后端证据与诊断

On 2026-08-08, local read-only artifacts executed on CPU through both ORT and OpenVINO with named input `x` and output `fetch_name_0`. The mobile classifier SHA256 was `dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2` (1,018,940 bytes); the inspected legacy artifact was `f4bb53707100c5f3d59ba834eb05bb400369f20aed35d4b26807b1bfadd2a70e` (582,663 bytes). Direct zero-tensor confidence differed by less than `1e-6`. A same-image three-stage test compared eight regions, label/direction/reject decisions, corrected recognition text/confidence, and source-space polygon coordinates across both backends. Recorded elapsed times are single-run diagnostics, not P50/P95, throughput, memory, accuracy, or cross-machine claims. / 2026-08-08 的只读本地工件在 ORT/OpenVINO CPU 上以具名 `x`/`fetch_name_0` 执行。工件 SHA/size 与同图八区域字段级对齐如上。时间仅是单次诊断，不构成性能或精度结论。

Cancellation is propagated through detection, every classification, preparation, and recognition call. Pipelines bound concurrent work, reject use after dispose, dispose owned sessions once, and map detector, orientation, crop/preparation, recognizer, cancellation, and backend failures to stable stages/codes. OpenCV decodes PNG/JPEG/bytes once into an owned source image, explicitly handles BGR/RGB/gray/alpha, and releases native temporaries before managed results escape. / 取消贯穿检测、每次分类、前处理与识别。Pipeline 限制并发、拒绝释放后使用、仅释放一次自有 session，并稳定映射各阶段错误。OpenCV 对 PNG/JPEG/bytes 单次解码为自有源图，显式处理 BGR/RGB/gray/alpha，在托管结果逃逸前释放 native 临时对象。

## TFM, RID, native, and admission / TFM、RID、native 与准入

| Component / 组件 | Managed surface / 托管面 | Application responsibility / 应用责任 |
| --- | --- | --- |
| `JYPPX.DeploySharp.Core` and `Visual` | Repository TFM matrix including `net46`, `netcoreapp3.1`, `net10.0` gates | Model files and contracts / 模型文件与合同 |
| `Visual.OpenCV` | Repository OpenCV TFM matrix | Matching `JYPPX.OpenCV.runtime.<rid>` / 匹配的 OpenCV native runtime |
| ONNX Runtime backend | Managed adapter | Matching official `Microsoft.ML.OnnxRuntime` CPU/native package / 匹配的 ORT native 包 |
| OpenVINO backend | Managed adapter | Matching OpenVINO runtime and CPU plugin / 匹配的 OpenVINO runtime 与 CPU plugin |

The official source snapshot is PaddleOCR commit `2661c7c0ef5c613e8f93c6e93b2e052399f0f854`, verified against remote HEAD on 2026-08-08, under Apache-2.0. The local ONNX exporter/checkpoint chain and redistribution rights are not independently proven. All three artifact manifests therefore remain `External`, `redistributionAllowed:false`, outside the official catalog, and below `AlgorithmVerified`. No local model, image, dictionary, IR sidecar, Python file, or native runtime is copied into Git or NuGet. / 官方源码快照为上述 PaddleOCR commit，2026-08-08 已与远端 HEAD 核验，许可证为 Apache-2.0。本地 ONNX 导出/checkpoint 链与再分发权利未独立证明，因此三份工件清单保持 External、禁止再分发、不进入官方 catalog，也不标记 AlgorithmVerified。
