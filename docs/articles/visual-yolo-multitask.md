# YOLO 分类、分割、姿态与 OBB

`YoloMultiTaskProfile` 为分类、实例分割、姿态估计和旋转框检测保存精确的模型合同。它记录模型族、输入尺寸、输出布局、标签集、前后处理版本和模型身份；不能仅凭文件名或张量形状猜测任务。

## 支持的输出合同

| 任务 | 常见模型 | 输出与解码 |
| --- | --- | --- |
| 分类 | YOLOv8 | `[B, classes]` 概率或 Logits，按 TopK 返回。 |
| 实例分割 | YOLOv5、v8、v9、v11 | 检测行 + prototype mask，按系数恢复掩码并映射回源图。 |
| 实例分割 | YOLO26 | 端到端检测行 + prototype mask，不重复 NMS。 |
| 姿态 | YOLOv8、v11 | 检测框 + 17 个关键点，按可见性和坐标合同解码。 |
| 姿态 | YOLO26 | 端到端检测行 + 17 个关键点，不重复 NMS。 |
| 旋转框 | YOLOv8、v11 | `xywhr` 旋转框和旋转 IoU NMS。 |
| 旋转框 | YOLO26 | 端到端 `xyxy,score,class,angle` 行。 |

原始输出先在模型坐标中解码，再执行阈值、NMS、掩码恢复或关键点解析，最后通过 `ImageTransform` 还原源图坐标。端到端图的筛选结果不会再次 NMS。

## 分类示例

```csharp
YoloMultiTaskProfile profile = YoloMultiTaskProfiles.CreateClassification(
    new ModelId("models/yolov8s-cls"),
    modelSha256,
    labels,
    exporterCommit,
    exporterVersion,
    new YoloClassificationProfileOptions(17));

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
        VisualTaskId.ImageClassification), request);
using PreparedVisualInput input = imageAdapter.Prepare(imagePath);
ClassificationResult result = pipeline.Run(input)
    .GetValue<ClassificationResult>();
```

分割、姿态和 OBB 分别使用 `CreateInstanceSegmentation`、`CreatePose` 和 `CreateObb`，并传入对应工件的输出选项。

## Batch 与并发

输出布局允许动态首维且模型确实支持 batch 时，在对应 Profile Options 中设置 `maximumBatch > 1`，使用 `InferenceBatchScheduler` 获取 `*BatchResult`。固定 batch=1 或包含可变掩码/关键点/旋转几何的导出图，使用多个独立 Session 的 `RunManyAsync`，不要把多个样本拼接到未经声明的张量中。

## 前处理和后端

检测族通常使用 RGB、NCHW、Float32、除以 255 和 114 填充的居中 Letterbox；分类通常使用固定尺寸中心裁剪。OpenVINO、TensorRT 和 OpenCV DNN 必须分别绑定实际工件的输入输出合同，不能直接复用不兼容的导出 Profile。

模型状态和逐后端实测见[模型支持指南](model-support.md)与[验证矩阵](../model-backend-verification-matrix.md)。
