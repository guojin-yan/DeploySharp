# Visual YOLO classification, segmentation, Pose, and OBB / Visual YOLO 分类、分割、Pose 与 OBB

Stage 17 completes the remaining twelve YOLO rows from the V1 inventory inside the existing `JYPPX.DeploySharp.Visual` package. It does not create `Visual.Ultralytics`, `Visual.Yolo`, or a model-specific NuGet package. The package contains artifact-bound Profiles and decoders; image decoding stays in `JYPPX.DeploySharp.Visual.OpenCV`, and execution stays in the selected Core backend. / 阶段 17 在现有 `JYPPX.DeploySharp.Visual` 包中完成 V1 清单剩余十二个 YOLO 行，不创建 `Visual.Ultralytics`、`Visual.Yolo` 或模型专用 NuGet 包。该包只包含工件绑定 Profile 与 Decoder；图像读取由 `JYPPX.DeploySharp.Visual.OpenCV` 完成，推理由 Core 选定的后端完成。

## Package boundary / 包边界

Install `JYPPX.DeploySharp.Core`, `JYPPX.DeploySharp.Visual`, `JYPPX.DeploySharp.Visual.OpenCV`, one of `JYPPX.DeploySharp.Backend.OnnxRuntime` or `JYPPX.DeploySharp.Backend.OpenVINO`, and the native runtime explicitly selected for the application. `Visual` itself has no OpenCV, ONNX Runtime, OpenVINO, Python, PyTorch, or model-weight dependency. TensorRT is intentionally not part of this stage. / 用户安装 `Core`、`Visual`、`Visual.OpenCV`、`Backend.OnnxRuntime` 或 `Backend.OpenVINO` 之一，以及应用显式选择的 native runtime。`Visual` 本身不依赖 OpenCV、ONNX Runtime、OpenVINO、Python、PyTorch 或模型权重。TensorRT 仍不在本阶段实现。

The stable abstraction is `YoloMultiTaskProfile`. It records the family, upstream revision, exporter version, opset, input size, output names/layouts/shapes, label set, preprocessing version, postprocessing version, and SHA256. A profile cannot be created from a model name or by guessing from a shape. / 稳定抽象是 `YoloMultiTaskProfile`，记录模型族、上游 revision、导出器版本、opset、输入尺寸、输出名称/布局/形状、标签集、前后处理版本和 SHA256。Profile 不能仅凭模型名称创建，也不能从 shape 猜测任务。

## Supported contracts / 支持合同

| Task / 任务 | Families / 模型族 | Exact exported contract / 精确导出合同 | Local evidence / 本机证据 |
| --- | --- | --- | --- |
| Classification / 分类 | YOLOCls (V1 sample), YOLOv8 classification | `[1,1000]` probabilities, 224x224 RGB NCHW center crop | ORT CPU + OpenVINO CPU |
| Instance segmentation / 实例分割 | YOLOv5, v8, v9, v11 | raw packed rows plus `[1,32,160,160]` prototypes; v5 `[1,25200,117]` candidate-major/objectness, v8/v9/v11 `[1,116,8400]` attribute-major | ORT CPU + OpenVINO CPU |
| Instance segmentation / 实例分割 | YOLOv26 | `[1,300,38]` exporter-selected rows plus `[1,32,160,160]` prototypes; no second NMS | ORT CPU + OpenVINO CPU |
| Pose / 姿态 | YOLOv8, YOLOv11 | `[1,56,8400]`, one COCO person class, 17 decoded `(x,y,visibility)` keypoints, raw box NMS | ORT CPU + OpenVINO CPU |
| Pose / 姿态 | YOLOv26 | `[1,300,57]` end-to-end rows, 17 decoded keypoints, no second NMS | ORT CPU + OpenVINO CPU |
| Oriented detection / 旋转框 | YOLOv8, YOLOv11 | `[1,20,21504]`, 15 DOTA classes, `xywhr`, probabilistic-IoU rotated NMS | ORT CPU + OpenVINO CPU |
| Oriented detection / 旋转框 | YOLOv26 | `[1,300,7]` end-to-end `xyxy,score,class,angle`, no second NMS | ORT CPU + OpenVINO CPU |

Raw outputs are decoded in model coordinates. The decoder applies official-style score selection, class-aware/class-agnostic NMS, prototype mask multiplication, crop-before-resize, bilinear half-pixel interpolation and threshold-after-resize, then uses `ImageTransform` to restore source coordinates. End-to-end exports are trusted for selection and never suppressed a second time. / Raw 输出在模型坐标中解码。Decoder 按官方语义完成分数选择、按类/忽略类别 NMS、prototype mask 矩阵乘法、先裁剪后缩放、bilinear half-pixel 插值及缩放后阈值化，再通过 `ImageTransform` 恢复源图坐标。端到端导出直接信任其筛选结果，绝不重复 NMS。

## Example / 示例

The following uses the V1 `YOLOCls` meaning audited against the existing sample: the concrete artifact is an Ultralytics YOLOv8 classification export with probability output. / 以下示例使用已对照 V1 sample 审核的 `YOLOCls` 语义：实际工件是输出概率的 Ultralytics YOLOv8 分类导出。

```csharp
YoloMultiTaskProfile profile = YoloMultiTaskProfiles.CreateClassification(
    new ModelId("models/yolov8s-cls"),
    "6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee",
    Enumerable.Range(0, 1000).Select(index => "class" + index),
    "ef141af4b837e0a1c34ff187ac40ef36af56c135",
    "8.1.6",
    new YoloClassificationProfileOptions(17));

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var profiles = new VisualProfileRegistry();
profiles.Register(profile.VisualProfile);
profiles.Freeze();
ModelArtifact artifact = profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.ImageClassification), request);
using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
    imagePath, profile.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(profile));
ClassificationResult result = pipeline.Run(input).GetValue<ClassificationResult>();
```

For segmentation, Pose, and OBB call `CreateInstanceSegmentation`, `CreatePose`, or `CreateObb` with the exact artifact options. The same profile and decoder work with ORT and OpenVINO because the backend returns named Core tensors. / 对于分割、Pose 和 OBB，使用 `CreateInstanceSegmentation`、`CreatePose` 或 `CreateObb` 并传入精确工件选项。由于后端返回命名 Core tensor，同一个 Profile/Decoder 可复用于 ORT 与 OpenVINO。

## Fidelity and performance / 保真与性能

OpenCV preprocessing is RGB, Float32, NCHW and divides by 255. Detection-family tasks use centered 114 letterbox; classification uses the audited 224x224 center crop. The adapter copies pixels into an owned tensor before releasing the native image object. The local 640x640 prepared tensor for the bus image is SHA256 `48af3a194d046f683585c8c8deffa953d415122ec0f2398bd27d8a67f34978df`. / OpenCV 前处理为 RGB、Float32、NCHW 并除以 255。检测族任务使用居中 114 letterbox；分类使用已审核的 224x224 center crop。native 图像对象释放前，适配器会把像素复制到自有 tensor。bus 图片的本地 640x640 prepared tensor SHA256 为 `48af3a194d046f683585c8c8deffa953d415122ec0f2398bd27d8a67f34978df`。

The integration test `OpenCvYoloMultiTaskIntegrationTests` is gated by `DEPLOYSHARP_YOLO_RUN_EXTERNAL=1` for ORT and `DEPLOYSHARP_YOLO_RUN_EXTERNAL_OPENVINO=1` for OpenVINO. It reads `E:\Model\yolo` and `E:\Data\image\bus.jpg` by default, validates each declared SHA256 through `ModelArtifact`, runs all twelve rows, and reports inference/postprocessing timing. Missing models or runtimes are explicit inconclusive gates. / 集成测试 `OpenCvYoloMultiTaskIntegrationTests` 使用 `DEPLOYSHARP_YOLO_RUN_EXTERNAL=1` 门控 ORT，使用 `DEPLOYSHARP_YOLO_RUN_EXTERNAL_OPENVINO=1` 门控 OpenVINO。默认只读 `E:\Model\yolo` 与 `E:\Data\image\bus.jpg`，通过 `ModelArtifact` 校验每个声明的 SHA256，运行全部十二行并报告推理/后处理耗时。缺少模型或 runtime 时明确为 Inconclusive 门禁。

The four-task IR gate is explicitly pending: the current Windows workspace has no official OVC/Model Optimizer executable or OpenVINO Python converter, so this stage does not claim classification, segmentation, Pose, or OBB IR artifacts. The twelve ONNX paths are real OpenVINO CPU runs; the existing IR fixture remains limited to the earlier detection contract. No pseudo-IR or unlicensed converted asset is used. / 四类任务 IR 门禁明确保持待完成：当前 Windows 工作区没有官方 OVC/Model Optimizer 可执行文件或 OpenVINO Python 转换器，因此本阶段不宣称拥有分类、分割、Pose 或 OBB IR 工件。十二条 ONNX 路径均是真实 OpenVINO CPU 运行；既有 IR 夹具仍仅覆盖前一阶段检测合同。不使用伪 IR 或未授权转换资产。

Do not treat the local matrix as an accuracy claim. `ContractVerified + LocalBackendVerified` means the artifact metadata, backend execution and canonical result path are reproducible. `AlgorithmVerified` additionally requires an independently runnable official predictor, prepared-tensor and canonical-result golden comparisons, and redistribution permission for the exact checkpoint and test image. / 不要把本机矩阵当作精度声明。`ContractVerified + LocalBackendVerified` 仅表示工件元数据、后端执行和规范结果路径可复现。`AlgorithmVerified` 还要求可独立运行的官方 predictor、prepared tensor 与规范结果黄金对照，以及精确权重和测试图像的再分发许可。

The current stage records per-row inference and postprocessing timings in the integration output but does not publish P50/P95 or throughput claims. An independently pinned official predictor, field-level golden JSON, and warmed benchmark protocol are required before any row is promoted beyond local backend verification. / 当前阶段在集成输出中记录每行推理和后处理耗时，但不发布 P50/P95 或吞吐声明。在任何模型行升级超出本地后端验证前，还必须提供独立锁定版本的官方 predictor、字段级黄金 JSON 和预热基准协议。

## ModelFactory admission / ModelFactory 准入

The twelve artifacts are local `External` candidates only. No model is copied into Git, NuGet, the embedded official catalog or a GitHub Release. The official catalog remains empty until provenance, license notices, official goldens and explicit redistribution approval are complete. / 十二个工件仅作为本地 `External` 候选。没有模型被复制到 Git、NuGet、内置官方 catalog 或 GitHub Release。完成来源、许可声明、官方黄金对照和明确再分发授权前，官方 catalog 保持为空。

TensorRT `.engine`/`.plan` files are device and builder-version bound and are not portable catalog assets. This stage does not implement `JYPPX.DeploySharp.Backend.TensorRT`; the user-owned `JYPPX.TensorRT.CSharp.API 4.0.0-preview.1` remains a later adapter candidate. / TensorRT `.engine`/`.plan` 与设备和 builder 版本绑定，不是可移植 catalog 工件。本阶段不实现 `JYPPX.DeploySharp.Backend.TensorRT`；用户开发的 `JYPPX.TensorRT.CSharp.API 4.0.0-preview.1` 留待后续适配。
