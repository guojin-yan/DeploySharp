# Visual YOLO detection / Visual YOLO 检测

Stage 16 closes the reusable detection contract for the ten V1 YOLO detection families inside `JYPPX.DeploySharp.Visual`. It does not create an Ultralytics-named DeploySharp package. Ultralytics and the historical YOLO repositories are upstream references for export and preprocessing semantics; image decoding remains in the optional `JYPPX.DeploySharp.Visual.OpenCV` adapter. / 阶段 16 在 `JYPPX.DeploySharp.Visual` 内关闭 V1 十个 YOLO 检测模型族的可复用检测合同，不创建任何 Ultralytics 命名的 DeploySharp 包。Ultralytics 与历史 YOLO 仓库是导出和前处理语义的上游参考；图像解码仍由可选的 `JYPPX.DeploySharp.Visual.OpenCV` 适配器完成。

## Quick start / 快速开始

Install `JYPPX.DeploySharp.Core`, `JYPPX.DeploySharp.Visual`, `JYPPX.DeploySharp.Visual.OpenCV`, one backend such as `JYPPX.DeploySharp.Backend.OnnxRuntime` or `JYPPX.DeploySharp.Backend.OpenVINO`, and the backend's user-selected native runtime. The following example uses a YOLOv8n ONNX export and OpenCV 5 preview input. / 安装 `JYPPX.DeploySharp.Core`、`JYPPX.DeploySharp.Visual`、`JYPPX.DeploySharp.Visual.OpenCV`、一个后端（例如 `JYPPX.DeploySharp.Backend.OnnxRuntime` 或 `JYPPX.DeploySharp.Backend.OpenVINO`）以及由用户选择的后端 native runtime。下面示例使用 YOLOv8n ONNX 导出和 OpenCV 5 preview 输入。

```csharp
YoloDetectionProfile profile = YoloDetectionProfiles.Create(
    YoloDetectionFamily.YoloV8,
    new ModelId("models/yolov8n-detect"),
    "50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4",
    YoloLabelSets.Coco80,
    "1367566337fb8056223a1aeb469360747f1b1bcd",
    "8.3.78",
    new YoloDetectionProfileOptions(19));

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var profiles = new VisualProfileRegistry();
profiles.Register(profile.VisualProfile);
profiles.Freeze();
ModelArtifact artifact = profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.ObjectDetection), request);
using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
    imagePath, profile.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(profile));
DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
```

`YoloDetectionProfile` is artifact-bound. The factory requires the model SHA256, upstream commit/release, exporter version, opset, output contract, and preprocessing/postprocessing contract versions. A missing or contradictory field fails profile construction. / `YoloDetectionProfile` 与工件绑定。工厂要求模型 SHA256、上游 commit/release、导出器版本、opset、输出合同以及前后处理合同版本；字段缺失或矛盾时拒绝构建 Profile。

An ONNX-derived OpenVINO IR uses the same decoder but must use a separate artifact-bound profile: set `modelFormat: "openvino-ir"` and bind the XML SHA256. The `.bin` sidecar is independently recorded by ModelPack. / 从 ONNX 转换的 OpenVINO IR 复用同一 Decoder，但必须使用独立的工件绑定 Profile：设置 `modelFormat: "openvino-ir"` 并绑定 XML SHA256；`.bin` sidecar 由 ModelPack 单独记录。

## Output contracts / 输出合同

| Family / 模型族 | Output / 输出 | Shape / 形状 | Score semantics / 分数语义 | NMS |
| --- | --- | --- | --- | --- |
| YOLOv5, YOLOv6 | raw candidate-major | `[1,N,5+C]` | `objectness * class score` | DeploySharp class-aware or agnostic NMS |
| YOLOv7 | batched end-to-end | `[N,7]` = batch, xyxy, class, score | model-provided score | no second NMS |
| YOLOv8, v9, v11, v12, v13 | raw attribute-major | `[1,4+C,N]` | class score | DeploySharp class-aware or agnostic NMS |
| YOLOv10, YOLO26 | end-to-end | `[1,N,6]` = xyxy, score, class | model-provided score | no second NMS |

The decoder rejects NaN/Infinity, wrong rank/field counts, invalid class indices, inverted boxes, non-probability scores, missing output names, and unsupported batches. Raw-head NMS runs in model coordinates before inverse letterbox restoration; end-to-end rows retain exporter order and are never NMS'ed again. / Decoder 会拒绝 NaN/Infinity、错误 rank/字段数量、越界类别、反向框、非概率分数、缺失输出名和不支持的 batch。Raw head 在逆 letterbox 前使用模型坐标执行 NMS；端到端行保留导出器顺序，不重复 NMS。

## Preprocessing / 前处理

`OpenCvYoloPreprocessing.CreateOptions` expresses the common official contract as RGB, NCHW, Float32, centered letterbox, padding value 114, and division by 255. The source image is copied into an owned managed tensor before native `Mat` disposal. `PreparedVisualInput.Transform` restores xyxy boxes to source-image coordinates. / `OpenCvYoloPreprocessing.CreateOptions` 将常见官方合同表达为 RGB、NCHW、Float32、居中 letterbox、114 填充值和除以 255。native `Mat` 释放前会把像素复制到托管自有张量；`PreparedVisualInput.Transform` 将 xyxy 框恢复到源图像坐标。

The current adapter intentionally rejects `scaleUp=false`, because the preview OpenCV contract does not yet expose a geometry flag that can preserve the exact official no-upscale path. / 当前适配器有意拒绝 `scaleUp=false`，因为 preview OpenCV 合同尚未提供可保持官方“不放大”几何路径的标志。

## V1 evidence matrix / V1 证据矩阵

The following local artifacts were read-only inputs from `E:\Model\yolo`; none is copied into Git, NuGet, the embedded catalog, or a GitHub Release. The validation image was the user-authorized `E:\Data\image\bus.jpg` (SHA256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`). The exact RGB/NCHW/Float32/640x640 prepared tensor SHA256 was `48af3a194d046f683585c8c8deffa953d415122ec0f2398bd27d8a67f34978df`. / 下列本机工件仅从 `E:\Model\yolo` 只读使用；没有复制到 Git、NuGet、内置 catalog 或 GitHub Release。验证图片是用户授权的 `E:\Data\image\bus.jpg`（SHA256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`）。精确 RGB/NCHW/Float32/640x640 prepared tensor SHA256 为 `48af3a194d046f683585c8c8deffa953d415122ec0f2398bd27d8a67f34978df`。

| Family | Model bytes / SHA256 | Opset | ORT CPU | OpenVINO CPU |
| --- | --- | ---: | --- | --- |
| YOLOv5n | 7,903,142 / `1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d` | 12 | 4, top person 0.823105 | 4, top person 0.823104 |
| YOLOv6s | 69,046,664 / `f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6` | 12 | 5, top person 0.948000 | 5, top person 0.948000 |
| YOLOv7 | 147,764,877 / `8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641` | 12 | 7, top person 0.943969 | 7, top person 0.943969 |
| YOLOv8n | 12,836,453 / `50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4` | 19 | 5, top person 0.900904 | 5, top person 0.900904 |
| YOLOv9s | 29,153,318 / `e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7` | 19 | 5, top bus 0.969499 | 5, top bus 0.969499 |
| YOLOv10n | 9,454,100 / `908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81` | 19 | 5, top bus 0.939992 | 5, top bus 0.939992 |
| YOLO11n | 10,720,330 / `7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3` | 19 | 5, top bus 0.938091 | 5, top bus 0.938091 |
| YOLO12n | 10,671,234 / `9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80` | 19 | 5, top bus 0.903900 | 5, top bus 0.903900 |
| YOLO13n | 10,310,579 / `a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80` | 17 | 5, top bus 0.936024 | 5, top bus 0.936024 |
| YOLO26n | 9,941,955 / `bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4` | 19 | 5, top person 0.924566 | 5, top person 0.924566 |

These are local backend and cross-backend reproducibility results, not official accuracy claims. The rows are `ContractVerified + LocalBackendVerified`; they remain below `AlgorithmVerified` until an independently runnable official predictor/reference script produces prepared-tensor and canonical-result golden comparisons, and each exact weight/image license permits redistribution. / 这些是本机后端和跨后端可重复结果，不是官方精度声明。当前各行状态为 `ContractVerified + LocalBackendVerified`；在独立可运行的官方 predictor/参考脚本完成 prepared tensor 与 canonical result 黄金对照，且每个精确权重/图片许可证允许再分发前，不提升为 `AlgorithmVerified`。

## Reproducible OpenVINO IR / 可复现 OpenVINO IR

The checked-in conversion entry point does not download a model and writes outside the repository. It was verified with OpenVINO OVC 2025.4.0 and the exact YOLOv8n ONNX row above. / 纳入仓库的转换入口不下载模型，且输出到仓库之外。已使用 OpenVINO OVC 2025.4.0 和上表精确 YOLOv8n ONNX 工件验证。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\models\yolo\Convert-YoloOnnxToOpenVinoIr.ps1 `
  -ModelPath E:\Model\yolo\yolov8\yolov8n.onnx `
  -OutputDirectory E:\GitSpace\DeploySharp-V2.0\.yolo-probe\ir
```

The FP32 conversion produced XML `224,246` bytes with SHA256 `065b06a5d8c60ab18bf0ccd0baa285e21f31c9e517042b79cd5d78971b1551a1` and BIN `12,708,660` bytes with SHA256 `b4497767a70f3a165b8204e0be05d5f3d59325a1ec9d6d51f2b802f93316b6ce`. OpenVINO CPU returned 5 detections with top `person` score `0.900904`, matching the ONNX path at the recorded precision. These generated files remain local and are not release assets. / FP32 转换生成 XML `224,246` 字节，SHA256 为 `065b06a5d8c60ab18bf0ccd0baa285e21f31c9e517042b79cd5d78971b1551a1`；BIN `12,708,660` 字节，SHA256 为 `b4497767a70f3a165b8204e0be05d5f3d59325a1ec9d6d51f2b802f93316b6ce`。OpenVINO CPU 返回 5 个检测，Top 为 `person`、分数 `0.900904`，在记录精度下与 ONNX 路径一致。生成文件仅保留在本机，不是 Release 资产。

## ModelPack and ModelFactory / ModelPack 与 ModelFactory

`eng/models/yolo/manifests/*.modelpack.json` contains ten candidate manifests. They record the exact model size/SHA256, opset, tensor names/shapes, output family, upstream source/license, prepared-tensor evidence, and verified backend identifiers. The YOLOv8 candidate additionally records the reproducible OpenVINO IR XML/BIN as a two-file artifact, and ModelFactory offline queries distinguish `onnx + onnxruntime/openvino` from `openvino-ir + openvino`. `Write-YoloModelPackCandidates.ps1 -Check` validates that the generated files are current. The candidate manifests set `redistributionAllowed=false` and an explicit blocked admission reason. They are not the official catalog. / `eng/models/yolo/manifests/*.modelpack.json` 包含十份候选清单，记录精确模型大小/SHA256、opset、张量名称/形状、输出族、上游来源/许可证、prepared tensor 证据和已验证后端标识。YOLOv8 候选还把可复现 OpenVINO IR XML/BIN 记录为双文件工件，ModelFactory 离线查询会区分 `onnx + onnxruntime/openvino` 与 `openvino-ir + openvino`。`Write-YoloModelPackCandidates.ps1 -Check` 检查生成文件是否最新。候选清单设置 `redistributionAllowed=false` 和明确阻断原因，不属于官方 catalog。

TensorRT `.engine`/`.plan` is never a portable candidate. No GitHub Release, tag, asset, model download URL, or write operation was created in this stage. / TensorRT `.engine`/`.plan` 永远不是可移植候选。本阶段没有创建 GitHub Release、tag、asset、模型下载链接或任何写操作。

## Performance and diagnostics / 性能与诊断

On the development machine, the ten-model matrix completed in approximately 3 seconds through ONNX Runtime CPU and 8 seconds through OpenVINO CPU including model load, SHA check, OpenCV preprocessing, inference and decoder work. These wall-clock values include startup and model compilation and are not cross-machine performance claims. A production benchmark must report preprocess, backend execute, decode/NMS, end-to-end latency, throughput and peak memory separately. / 在开发机上，十模型矩阵通过 ONNX Runtime CPU 约 3 秒、OpenVINO CPU 约 8 秒完成，包含模型加载、SHA 校验、OpenCV 前处理、推理和解码。这些墙钟时间包含启动和模型编译，不是跨机器性能结论。生产基准必须分别报告前处理、后端执行、decode/NMS、端到端延迟、吞吐和峰值内存。

Stable diagnostics identify invalid family/exporter/profile, tensor names/rank/shape/type, score/class/box values, candidate limits, cancellation and unsupported capabilities. See `VisualErrorCodes.YoloContractInvalid` and `VisualErrorCodes.YoloLimitExceeded`. / 稳定诊断覆盖无效模型族/导出器/Profile、张量名称/rank/形状/类型、分数/类别/框值、候选上限、取消和不支持能力。参见 `VisualErrorCodes.YoloContractInvalid` 与 `VisualErrorCodes.YoloLimitExceeded`。
