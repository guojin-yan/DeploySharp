# 旋转框检测（OBB）

DeploySharp.Visual 提供后端无关的旋转目标检测 decoder。每个模型必须明确声明输出是中心点/宽高/角度，还是四个有序顶点；不根据数值范围猜测格式。

## 输出合同

| Schema | 几何输出 | 其他输出 | 数值 |
| --- | --- | --- | --- |
| CenterSizeAngleOutputSchema | [N,5]：中心 x/y、宽、高、角度 | scores、classes | Float32 或 Float64 |
| FourCornerOutputSchema | [N,8]：四个有序顶点 | scores、classes | Float32 或 Float64 |

两种 schema 均要求精确的三个命名输出和 batch-one。角度单位、正方向、范围、width/height 约定、坐标空间和边界策略必须绑定在 schema 中。

## 创建 decoder

~~~csharp
var schema = new CenterSizeAngleOutputSchema(
    "boxes", "scores", "classes",
    coordinateSpace: OrientedCoordinateSpace.ModelPixels,
    angleUnit: OrientedAngleUnit.Radians,
    angleDirection: OrientedAngleDirection.Clockwise,
    angleRange: OrientedAngleRange.MinusHalfPiToHalfPi,
    widthConvention: OrientedWidthConvention.WidthAxis,
    boundaryMode: OrientedDetectionBoundaryMode.Preserve);
var decoder = new DirectOrientedDetectionDecoder(
    schema,
    new OrientedDetectionDecoderOptions(
        scoreThreshold: 0.25f,
        iouThreshold: 0.45f,
        nmsMode: DetectionNmsMode.ClassAware,
        maximumCandidates: 3000,
        maximumDetections: 100));
~~~

将 decoder 绑定到 VisualTaskId.OrientedObjectDetection 的 VisualModelProfile。四边形必须四点有限、互异、严格凸且不自交；规范化要求调用方声明顶点方向，不会自动排序未知导出布局。

## 坐标、NMS 与 Batch

每个模型空间顶点通过 ImageTransform.ToSource 还原到源图。Resize、Letterbox、Crop 和归一化坐标共用同一变换；源图四边形是权威结果，不会额外拟合一个旋转矩形。默认 NMS 是确定性的，分数相同按源索引排序。

只有 Profile 和 decoder 同时声明 [B,...] 动态合同时才支持真正模型 Batch。否则使用 RunManyAsync 或独立 session 池；每个 native 上下文必须独立创建。

## 后端与限制

同一合同可选择 ONNX Runtime 或 OpenVINO；TensorRT 需要与输入形状匹配的 Engine。当前 Alpha 不提供跟踪、3D OBB 或不明确角度语义的自动兼容。具体模型状态见[模型支持指南](model-support.md)和[验证矩阵](../model-backend-verification-matrix.md)，设备性能见[设备性能实测](device-performance-benchmarks.md)。
