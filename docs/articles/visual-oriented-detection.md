# Visual oriented detection (OBB) / Visual 旋转框检测（OBB）

`JYPPX.DeploySharp.Visual` provides backend-neutral oriented-object detection over owned `PreparedVisualInput` tensors. The module does not reference OpenCV, ONNX Runtime, OpenVINO, or vendor geometry. Install a backend separately; install `JYPPX.DeploySharp.Visual.OpenCV` only for encoded-image input. / `JYPPX.DeploySharp.Visual` 基于自有 `PreparedVisualInput` 张量提供后端无关的旋转目标检测。该模块不引用 OpenCV、ONNX Runtime、OpenVINO 或厂商几何类型。推理后端需单独安装；仅在需要编码图像输入时安装 `JYPPX.DeploySharp.Visual.OpenCV`。

## Exact schemas / 精确 Schema

| Contract / 契约 | Geometry / 几何输出 | Other outputs / 其他输出 | Supported values / 支持数值 |
| --- | --- | --- | --- |
| `CenterSizeAngleOutputSchema` | named `[1,N,5]` center-x, center-y, width, height, angle / 命名 `[1,N,5]` 中心、宽高、角度 | named scores/classes `[1,N]` | Float32 or Float64 / Float32 或 Float64 |
| `FourCornerOutputSchema` | named `[1,N,8]` four ordered vertices / 命名 `[1,N,8]` 四个有序顶点 | named scores/classes `[1,N]` | Float32 or Float64 / Float32 或 Float64 |

Both schemas require exactly three named outputs and batch one. They reject extra/missing names, wrong rank/shape/type/element count, non-finite values, non-positive sizes, invalid scores/classes, and malformed quadrilaterals. They never infer a format from numeric ranges. / 两种 Schema 均要求精确三个命名输出和 batch 1；拒绝额外/缺失名称、错误 rank/shape/type/元素数、非有限值、非正尺寸、非法分数/类别及畸形四边形，绝不根据数值范围猜测格式。

```csharp
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

var profile = new VisualModelProfile(
    "example/obb.v1", modelId, VisualTaskId.OrientedObjectDetection,
    "1.0", "onnx", inputBinding, outputBindings, labels, decoder);
```

The accepted angle interval is half-open: the lower endpoint is included and the upper endpoint is rejected. Unit, positive image-coordinate direction, period, and whether width follows the supplied axis or is normalized to the long side are independent declarations. A long-side width/height swap rotates the declared axis by 90 degrees. Equal sides are not swapped. / 允许的角度区间为半开区间：包含下界并拒绝上界。单位、图像坐标中的正方向、周期以及 width 是沿输入轴还是规范为长边，均为相互独立的声明。long-side 宽高交换会把声明轴旋转 90 度；等边不交换。

## Canonical quadrilateral and source restoration / 规范四边形与源图恢复

`OrientedQuadrilateral` owns exactly four finite, distinct, strictly convex, non-self-intersecting points. Public canonicalization requires the input direction and selects a deterministic first vertex; it does not sort an unknown exporter layout. Positive signed area means counter-clockwise in the numeric mathematical x/y plane. Because image y increases downward, its visual appearance is reversed. / `OrientedQuadrilateral` 自有且仅包含四个有限、互异、严格凸、不自交的点。公共规范化 API 要求声明输入方向并选择确定性的首顶点，不会对未知导出布局自动排序。正有符号面积表示数值数学 x/y 平面中的逆时针；由于图像 y 轴向下，其视觉方向相反。

Every model-space vertex is restored through `ImageTransform.ToSource`, so Resize, Letterbox padding removal, Crop offset, dynamic source sizes, normalized coordinates, and model-pixel coordinates use one path. A non-uniform Resize can turn a rotated rectangle into a non-orthogonal parallelogram. The source quadrilateral remains authoritative; no fitted rotated rectangle is fabricated. `HasExactRotatedRectangle` is true only for center-size-angle output restored through an angle-preserving transform. / 每个模型空间顶点均通过 `ImageTransform.ToSource` 恢复，因此 Resize、Letterbox 去 padding、Crop 偏移、动态源图尺寸、归一化坐标及模型像素坐标共用一条路径。非等比 Resize 可能把旋转矩形变成非正交平行四边形；源图四边形仍是权威结果，不伪造拟合旋转矩形。仅当中心宽高角输出通过保持角度的变换恢复时，`HasExactRotatedRectangle` 才为 true。

The default boundary mode preserves out-of-source vertices. `RejectOutsideSource` rejects the candidate. Alpha.1 does not expose clipping because clipping a quadrilateral to the image can create more than four vertices; silently dropping those vertices would corrupt area and IoU. / 默认边界模式保留源图外顶点；`RejectOutsideSource` 拒绝候选。alpha.1 不公开裁剪，因为四边形与图像裁剪后可能产生超过四个顶点，静默丢点会破坏面积与 IoU。

## Polygon IoU and rotated NMS / 多边形 IoU 与 rotated NMS

`OrientedQuadrilateral.IntersectionOverUnion` uses a managed, bounded Sutherland-Hodgman intersection of two canonical convex quadrilaterals. AABB rejection runs first, then exact convex intersection/union area. Edge-only or vertex-only contact has zero intersection. Epsilon is explicit and positive. / `OrientedQuadrilateral.IntersectionOverUnion` 使用托管、有界的 Sutherland-Hodgman 算法求两个规范凸四边形交集。先执行 AABB 快速拒绝，再计算精确凸多边形交并面积；仅边或顶点接触的交面积为零，epsilon 必须显式且为正。

NMS orders candidates by score descending and original source index ascending. Class-aware mode compares only equal classes; class-agnostic mode compares all retained candidates. AABB is only an early reject and never replaces polygon IoU. Cancellation is checked in parsing, vertex restoration, clipping, and NMS loops. Decoders are immutable and do not share mutable workspaces. / NMS 按分数降序、原始来源索引升序排列。分类别模式只比较相同类别，忽略类别模式比较所有已保留候选。AABB 仅用于快速拒绝，绝不替代多边形 IoU。候选解析、顶点恢复、裁剪和 NMS 循环均检查取消；解码器不可变且不共享可变工作区。

## Result ownership and diagnostics / 结果所有权与诊断

`JYPPX.DeploySharp.Visual.OrientedDetectionResult` owns managed detections and canonical points, source/profile/model provenance, and deterministic SHA256. It remains valid after backend outputs and sessions are disposed. Core also has the older compact `JYPPX.DeploySharp.Results.Vision.OrientedDetectionResult`; code importing both namespaces must use a type alias or fully qualified name. / `JYPPX.DeploySharp.Visual.OrientedDetectionResult` 自有托管检测结果与规范顶点，包含源图/Profile/模型来源及确定性 SHA256，并在后端输出与 session 释放后保持有效。Core 还包含较早的精简 `JYPPX.DeploySharp.Results.Vision.OrientedDetectionResult`；同时导入两个命名空间时必须使用类型别名或完整限定名。

Candidate count, retained result count, and Float64 conversion workspace are bounded. Checked arithmetic protects tensor offsets and byte counts. Invalid tensor contracts use `DS-VISUAL-3001`; invalid decoded values and bounds use `DS-VISUAL-3002`; cancellation/timeout/disposal retain the existing stable Visual diagnostics with model, profile, and tensor context. / 候选数、保留结果数及 Float64 转换工作区均有界；张量 offset 与字节数使用 checked 算术。无效张量契约使用 `DS-VISUAL-3001`，无效解码值和边界使用 `DS-VISUAL-3002`；取消/超时/释放沿用现有稳定 Visual 诊断，并保留模型、Profile 与张量上下文。

## Reproducible evidence and performance / 可复现证据与性能

With `onnx==1.22.0`, opset 13, deterministic serialization, and `onnx.checker`, the repository generates `direct-obb.onnx` (578 bytes, SHA256 `7d0bd51c8f5c8aa48349b64d6e715b7130b2a1df9de25d0da6b377f0bbf3ce51`) and `corner-obb.onnx` (641 bytes, SHA256 `766df209349a3ccd0500319be524507c3d49a271348900898054481109e1917b`). OpenVINO 2026.2.1 converts Direct to XML (2515 bytes, SHA256 `6fce1acde833691776b6be72588c51e488baab22c1e89ea2351247095a5430cb`) and BIN (112 bytes, SHA256 `14ba19c1e664f33c3d0b0e9ab5c03a878b0a4addc6cb0b68aa3bbf470a6828ab`). Repeated generation is byte-identical. / 仓库使用 `onnx==1.22.0`、opset 13、确定性序列化和 `onnx.checker` 生成 `direct-obb.onnx`（578 字节，SHA256 `7d0bd51c8f5c8aa48349b64d6e715b7130b2a1df9de25d0da6b377f0bbf3ce51`）及 `corner-obb.onnx`（641 字节，SHA256 `766df209349a3ccd0500319be524507c3d49a271348900898054481109e1917b`）。OpenVINO 2026.2.1 将 Direct 转换为 XML（2515 字节，SHA256 `6fce1acde833691776b6be72588c51e488baab22c1e89ea2351247095a5430cb`）与 BIN（112 字节，SHA256 `14ba19c1e664f33c3d0b0e9ab5c03a878b0a4addc6cb0b68aa3bbf470a6828ab`）；重复生成字节一致。

Real CPU tests execute both ONNX contracts through ONNX Runtime, the Direct ONNX and IR through OpenVINO, and a real PNG through Visual.OpenCV into ONNX Runtime. ModelPack validates size/SHA and both IR sidecars; ModelFactory selects test-only offline Preview entries for all three required backend/format combinations. The performance entry records 128-candidate polygon/NMS elapsed time, throughput, and managed allocations without a fragile absolute threshold. / 真实 CPU 测试通过 ONNX Runtime 执行两类 ONNX，通过 OpenVINO 执行 Direct ONNX 与 IR，并把真实 PNG 经 Visual.OpenCV 输入 ONNX Runtime。ModelPack 校验大小/SHA 与两个 IR sidecar；ModelFactory 为三种所需后端/格式组合选择仅测试使用的离线 Preview。性能入口记录 128 候选 polygon/NMS 的耗时、吞吐和托管分配，不设置脆弱的绝对阈值。

These Apache-2.0 constant graphs are `ContractVerified` adapter fixtures only. They are not YOLO-OBB, RTMDet-R, Oriented R-CNN, official accuracy evidence, benchmarks, catalog entries, or GitHub Release assets. `AlgorithmVerified` admission requires legal model/test assets, SHA256, official preprocessing/export/postprocessing including angle semantics, intermediate/golden comparisons with quantified tolerances, named runner, Release P50/P95 and allocation evidence, and validation date. The embedded official catalog remains empty. TensorRT remains intentionally absent until its wrapper is explicitly confirmed ready. / 这些 Apache-2.0 常量图仅为 `ContractVerified` 适配器夹具，不是 YOLO-OBB、RTMDet-R、Oriented R-CNN、官方精度证据、基准、目录条目或 GitHub Release 资产。`AlgorithmVerified` 准入需要合法模型/测试资产、SHA256、包含角度语义的官方预处理/导出/后处理、带量化容差的中间/黄金对照、明确 runner、Release P50/P95 与分配证据及验证日期。内嵌官方目录保持为空；在 wrapper 被明确确认可用前，TensorRT 继续有意缺席。
