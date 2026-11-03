# RF-DETR：检测、实例分割与人体关键点

DeploySharp 保留既有 RF-DETR 检测和分割入口，当前源码新增官方 **RF-DETR 1.11.2 Keypoint Preview** 的 Profile、精度矩阵迹分数融合 Decoder，以及与官方浮点缩放顺序一致的图像输入工厂；同时补齐 **Seg Nano 1.11.2** 的真实图片案例。这些新增 API 尚未发布到 NuGet，本文使用源码项目引用。上游版本固定为 [7433d591](https://github.com/roboflow/rf-detr/tree/7433d5919d0f7e72cc6cb78663e0a731afd499e0)。

## 选择精确任务与工件

| 任务 | 内置入口 | 结果 | 本文验证范围 |
| --- | --- | --- | --- |
| 既有目标检测 | `PortableDetectorProfiles.CreateRFDETR` | `DetectionResult` | 既有公开 `rf-detr/detect`，沿用原工件的矩阵 |
| 实例分割 | `PortableDetectorProfiles.CreateRFDETRSeg` | `InstanceSegmentationResult` | 新 `rf-detr/segment/nano`：312×312、100 Query、91 列稀疏 COCO |
| 人体关键点 | `RFDETRKeypointProfiles.CreatePreview` | `PoseEstimationResult` | 新 `rf-detr/keypoint-preview`：576×576、100 Query、COCO-17、Preview |

新工件目前通过[官方权重下载/导出工具](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/eng/models/rf-detr/README.md)生成并从本地路径加载，尚无 DeploySharp Release 自动下载记录。旧公开 `rf-detr/segment` 是另一份 432×432、200 Query、`4245` mask 端口的模型；它的 SHA-256、类别合同和后端状态继续单独保留。模型名相近不能替代精确文件身份。

## 运行完整案例

先按工具说明安装独立 Python 环境并导出到仓库外，然后从 Git 仓库根目录运行：

```powershell
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/keypoint/rfdetr.json --backend ort --output ../results/rf-detr/keypoint

dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/segment-nano/rfdetr.json --backend ort --output ../results/rf-detr/segment-nano
```

样例校验模型与 ModelPack，默认获取带大小/SHA-256 校验的 `bus.jpg`，保存 JSON 和叠加图；分割另存每实例掩码。`--image <图片>` 替换输入，`--model <模型位置>` 更换同一模型文件的位置。换权重时应重新生成合同。完整的后端、取消、参考校验和 Engine 参数见[案例 README](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/06-models/rf-detr/README.md)。

## 在应用中使用关键点 Profile

以下代码使用当前源码的 `Visual`、`Visual.OpenCV` 与 `Backend.OnnxRuntime` 项目引用及与设备匹配的原生包。`modelPath`、`imagePath` 是应用提供的本地路径，`modelSha256` 从同次导出的 `rfdetr.json` 读取：

```csharp
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.OpenCV;

var id = new ModelId("rf-detr/keypoint-preview");
var profile = RFDETRKeypointProfiles.CreatePreview(id,
    new PortableDetectorProfileOptions(
        17, new VisualSize(576, 576), new[] { "background", "person" },
        artifactSha256: modelSha256,
        scoreThreshold: 0.4f, topK: 100, maximumResults: 100,
        rfDetrQueryCount: 100));
var artifact = new ModelArtifact(id, "onnx", Path.GetFullPath(modelPath), modelSha256);
using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var request = new BackendRequest(BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");
var profiles = new VisualProfileRegistry();
profiles.Register(profile);
profiles.Freeze();
using var pipeline = new VisualPipeline(backends,
    profiles.Select(artifact, backends, request, profile.Task), request);
using var input = new RFDETRInputFactory().Create(
    OpenCvImageSource.FromFile(Path.GetFullPath(imagePath)), profile,
    cancellationToken: cancellationToken);
PoseEstimationResult poses = (await pipeline.RunAsync(input,
    cancellationToken: cancellationToken)).GetValue<PoseEstimationResult>();
```

Profile 按名称绑定 `input`、`dets`、`labels`、`keypoints`；可用 options 修改前两个输出名称，`keypointsOutputName` 修改关键点输出名。Preview 严格要求输入 `[1,3,576,576]`，输出 `[1,100,4]`、`[1,100,2]`、`[1,100,34,8]`，均为 float32。动态 Batch、不同类别/关键点模式应使用自定义 Profile，不会被此便捷入口默认为兼容。结果拥有自己的框、点与元数据，输入释放后仍有效。

关键点原始输出包含两个 17 点槽，前槽为背景、后槽为人体。Decoder 对全部 query/class 的 sigmoid 分数做全局 top-100，再保留人体槽，按官方默认 `traceAlpha=0.2` 融合关键点 precision-trace 与类别分数，最后执行严格的 `score > threshold`、融合分数排序和结果上限。不额外执行 NMS。`traceAlpha: 0` 可显式关闭融合，但不属于本文默认参考结论。

每点 x/y 还原到源图坐标，`Score` 是 findability 的 sigmoid。`Visibility` 为 `Unknown`，不会把预测的 findability 或额外原始字段冒充人工标注的可见性；绘图或筛选时可按点分数设置阈值。骨架使用标准 COCO-17 顺序与连接。

## 分割 Profile 与掩码

Seg Nano 复用已有 `CreateRFDETRSeg`，配置 `ModelSize=312×312`、`RfDetrQueryCount=100`、输出 `dets/labels/masks`，类别表采用导出合同中的完整 91 列稀疏 COCO ID，`RfDetrIncludesNoObjectClass=false`。占位名称会加索引后缀以保持标签唯一；不能把 `Coco80` 直接传入，也不能把最后一列当成额外 no-object 删除。完整配置由样例读取 `rfdetr.json` 后创建。

Decoder 使用 sigmoid 全局 top-k、严格分数阈值和归一化 cxcywh，不额外执行 NMS。每个选中 query 的 mask logit 使用 bilinear、`align_corners=false` 恢复到源图，再以严格 `>0` 转为二值掩码；返回自有 `InstanceBinaryMask` 和 RLE。`maximumMaskPixels` 限制所有已选实例的源图掩码总像素量，大图应显式设置预算或先裁 ROI。

## 前处理与自定义几何

官方默认值为：RGB、像素除以 255、浮点 bilinear Resize（不抗锯齿）、ImageNet mean `[0.485,0.456,0.406]` / std `[0.229,0.224,0.225]`，NCHW float32、固定 Batch=1。`RFDETRInputFactory` 先把源像素转成浮点再插值，避免“先在 UInt8 图上缩放再转 float”的量化差异。`Create(OpenCvBgrImage, profile)` 可直接消费已解码的紧凑 BGR 图像，不重复解码；返回张量不保留源像素缓冲。

`PortableDetectorProfileOptions.Preprocessing` 可保留官方画布尺寸并显式覆盖 RGB/BGR、归一化、Resize/Letterbox/CenterCrop、插值和 Alpha 合成。编码输入重载在其它几何、插值或 Alpha 策略下复用通用 OpenCV 工厂；已解码 BGR 重载限定 Resize/Linear/Drop-alpha，并对其它组合明确报错。更改默认预处理需要重新评估质量，不适用本文的官方默认数值结论。关键点和分割框通过同一个 `ImageTransform` 回到源图，分割 mask 也使用同一几何。

也可自行实现输入预处理或 `IVisualDecoder`，继续使用通用 Pipeline；见[自定义模型与预处理](visual-custom-model.md)。

## Batch、异步与 ROI

本次两份 ONNX 均固定 Batch=1，Profile 不声称真 Batch。`VisualPipeline.RunAsync` 和 `RunManyAsync` 复用已有调度器；`SessionOptions(2)` 使用两个独立原生 Session。案例以三张独立输入检查有序结果及预取消，每份 `PreparedVisualInput` 应保持存活至异步任务完成，然后释放。

关键点复用 `VisualRoiPoseFilter` 和 Pose 合并器，分割复用 `VisualRoiInstanceSegmentationFilter` 与实例分割合并器。可采用两种坐标接法：

- ROI 输入携带完整源图的 `ImageTransform`：Decoder 已还原完整源图坐标，直接进行 ROI 过滤，不要再次投影。
- 先裁出局部 BGR 图并用浮点工厂推理：结果坐标属于局部图；构造局部到完整源图的 `RoiProjection`，通过 `ModelSpacePoseRoiProjector` 或 `ModelSpaceInstanceSegmentationRoiProjector` 投影一次，再过滤/合并。投影的 `ModelSize` 此时为局部图尺寸，完整图为 `SourceSize`。

已用非正方形 Letterbox、带偏移 ROI、源图框/点/掩码和 Include/Exclude 过滤验证几何合同；不据此声称任意 ROI 裁剪后的任务精度。`VisibleKeypointRatio` 只统计明确的 `Visible` 点，RF-DETR 点的可见性是 `Unknown`，应选普通关键点覆盖模式或自行按 `Score` 筛选。[ROI 教程](visual-roi-configuration.md)给出过滤与合并的通用用法。检测滑动窗口继续使用已有检测入口，不将其套用到人体关键点。

## 后端与验证范围

两份精确工件均已在 ORT CPU、ORT CUDA（关闭 TF32）、OpenVINO CPU、TensorRT FP32 上完成真实推理及官方参考对照。OpenCV DNN 5.0 在 `Greater` 的整数/浮点类型组合处导入失败。验证包含全部原始输出的 shape/有限值/容差、数量与类别、分数和坐标，分割另检查二值 mask。它是固定图片的 parity 证据，不是数据集准确率或性能测评。

精确 SHA-256、运行时、设备、容差、实测误差以及复现命令见[RF-DETR 后端矩阵](../model-backend-verification-matrix.md#rf-detr-1112-本地工件)。Seg Small/Medium/Large/XL/2XL、其它 Preview 或微调权重尚未沿用这些后端状态。
