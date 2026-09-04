# YOLO 检测

`JYPPX.DeploySharp.Visual` 为 YOLO 检测模型提供工件绑定的 Profile、预处理元数据和确定性解码。图像解码由可选的 `JYPPX.DeploySharp.Visual.OpenCV` 完成，推理由应用注册的后端执行。

## 快速使用

安装 `Core`、`Visual`、`Visual.OpenCV`、一个后端适配器和对应的原生运行时，然后使用与模型实际导出一致的 Profile：

```csharp
YoloDetectionProfile profile = YoloDetectionProfiles.Create(
    YoloDetectionFamily.YoloV8,
    new ModelId("models/yolov8n-detect"),
    modelSha256,
    YoloLabelSets.Coco80,
    exporterCommit,
    exporterVersion,
    new YoloDetectionProfileOptions(19));

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var profiles = new VisualProfileRegistry();
profiles.Register(profile.VisualProfile);
profiles.Freeze();

ModelArtifact artifact = profile.CreateArtifact(
    modelPath, OnnxRuntimeBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId,
    "cpu");
using var pipeline = new VisualPipeline(
    backends, profiles.Select(artifact, backends, request,
        VisualTaskId.ObjectDetection), request);
using PreparedVisualInput input =
    new OpenCvVisualInputFactory().CreateFromFile(
        imagePath, profile.VisualProfile.Input.Name,
        OpenCvYoloPreprocessing.CreateOptions(profile));

DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
```

Profile 必须绑定精确的模型身份、输入输出名称、opset、类别顺序以及前后处理版本。缺少或相互矛盾的字段会在创建 Profile 时拒绝。

## 输出布局

| 模型族 | 常见输出 | 解码方式 |
| --- | --- | --- |
| YOLOv5、YOLOv6 | `[B,N,5+C]` candidate-major | objectness × 最佳类别分数，再执行 NMS |
| YOLOv7 | `[N,7]` 端到端行 | 使用模型分数和导出顺序，不重复 NMS |
| YOLOv8、v9、v11、v12、v13 | `[B,4+C,N]` attribute-major | 还原候选维度后执行 NMS |
| YOLOv10、YOLO26 | `[B,N,6]` 端到端行 | 使用模型分数和类别，不重复 NMS |

Decoder 会拒绝 NaN/Infinity、错误 rank 或字段数、越界类别、反向框和不支持的 batch。应用不要根据文件名猜测布局；不同导出图应注册不同 Profile。

## 预处理和坐标

标准检测 Profile 使用 RGB、NCHW、Float32、除以 255 和居中 Letterbox（默认填充值 114）。`PreparedVisualInput.Transform` 保存源图到模型图的变换，Decoder 会将框裁剪并还原到源图坐标；应用不应再次缩放。

## 固定验证输入

发布候选的 YOLO Profile 使用固定本地验证图像和 `ultralytics-letterbox-rgb-nchw-v1` 预处理进行交叉后端核验：原图 SHA-256 为 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`，生成的 `Float32` NCHW 输入张量 SHA-256 为 `48af3a194d046f683585c8c8deffa953d415122ec0f2398bd27d8a67f34978df`。这是可追溯的验证证据，不代表该本地图片可以随模型包或项目重新分发。

| 候选工件 | ONNX SHA-256 |
| --- | --- |
| YOLOv5n Detect | `1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d` |
| YOLOv6s Detect | `f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6` |
| YOLOv7 Detect | `8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641` |
| YOLOv8n Detect | `50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4` |
| YOLOv9s Detect | `e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7` |
| YOLOv10n Detect | `908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81` |
| YOLO11n Detect | `7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3` |
| YOLO12n Detect | `9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80` |
| YOLOv13n Detect | `a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80` |
| YOLO26n Detect | `bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4` |

YOLOv8n 的已验证 OpenVINO IR 还绑定 XML SHA-256 `065b06a5d8c60ab18bf0ccd0baa285e21f31c9e517042b79cd5d78971b1551a1` 和 BIN SHA-256 `b4497767a70f3a165b8204e0be05d5f3d59325a1ec9d6d51f2b802f93316b6ce`。

OpenVINO IR 需要单独的工件绑定 Profile，并同时校验 XML/BIN Sidecar。TensorRT Engine 与 GPU、CUDA、TensorRT 版本和静态输入 Profile 绑定，不能把 ONNX Profile 直接当作 Engine Profile。

## 批量和并发

模型真实支持动态 batch 时，在 `YoloDetectionProfileOptions` 中设置 `maximumBatch > 1`，并使用 `InferenceBatchScheduler`。固定 batch=1 或输出包含可变掩码、关键点、旋转几何的多任务模型，应使用多个独立 Session 的 `RunManyAsync`。

## 相关文档

- [YOLO 多任务](visual-yolo-multitask.md)：分割、姿态和 OBB；
- [通用检测器与 RT-DETR](visual-portable-detectors.md)：DETR/RT-DETR/PP-YOLOE；
- [模型支持指南](model-support.md)：目录状态和可用边界；
- [模型 × 后端验证矩阵](../model-backend-verification-matrix.md)：逐模型、逐后端结果。
