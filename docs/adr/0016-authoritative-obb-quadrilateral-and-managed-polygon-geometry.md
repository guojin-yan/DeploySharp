# ADR 0016: Authoritative OBB quadrilateral and managed polygon geometry / 权威 OBB 四边形与托管多边形几何

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

OBB exporters disagree on center component order, angle unit, positive direction, period, width axis, long-side normalization, and four-corner order. Guessing any of these changes vertices, rotated IoU, NMS, and accuracy. Resize, Letterbox, and Crop must restore every vertex; a non-uniform Resize can turn a model-space rotated rectangle into a source-space non-orthogonal parallelogram. Fitting that result back to a rotated rectangle loses exact geometry. / OBB 导出器在中心分量顺序、角度单位、正方向、周期、宽轴、长边规范及四角点顺序上并不一致。猜测任一语义都会改变顶点、rotated IoU、NMS 与精度。Resize、Letterbox 和 Crop 必须恢复每个顶点；非等比 Resize 会把模型空间旋转矩形变成源图空间非正交平行四边形，重新拟合为旋转矩形会丢失精确几何。

Core already exposes compact rotated-rectangle result types, but changing Core would broaden the dependency-free contract and still could not represent a general restored parallelogram. OpenCV geometry would add a forbidden image-library dependency to Visual and could introduce platform-specific numeric behavior. / Core 已公开精简旋转矩形结果类型，但修改 Core 会扩大零依赖契约，且仍无法表示一般恢复后的平行四边形。OpenCV 几何会给 Visual 增加被禁止的图像库依赖，并可能引入平台相关数值行为。

## Decision / 决策

1. `JYPPX.DeploySharp.Visual` owns explicit center-size-angle and four-corner schemas. Names, component/vertex order, coordinate space, angle unit/direction/range, width convention, and boundary behavior are mandatory data rather than heuristics. / `JYPPX.DeploySharp.Visual` 拥有显式中心宽高角与四角点 Schema。名称、分量/顶点顺序、坐标空间、角度单位/方向/范围、宽度约定及边界行为均为必须数据，而非启发式猜测。
2. `OrientedQuadrilateral` is the authoritative source-space representation. It contains exactly four finite, distinct points in canonical positive-area order and rejects concave, zero-area, or self-intersecting input. Every point is restored independently through `ImageTransform`. / `OrientedQuadrilateral` 是权威源图空间表示，精确包含四个有限互异点并使用规范正面积顺序，拒绝凹形、零面积或自交输入；每个点均独立通过 `ImageTransform` 恢复。
3. An exact angle is reported only when center-size-angle output passes through an angle-preserving transform. Non-uniform transforms retain the exact quadrilateral and do not fit or claim a rotated rectangle. Four-corner angles are derived metadata and do not set `HasExactRotatedRectangle`. / 仅当中心宽高角输出经过保持角度的变换时才报告精确角度。非等比变换保留精确四边形，不拟合或宣称旋转矩形。四角点角度是派生元数据，不设置 `HasExactRotatedRectangle`。
4. Default boundary behavior preserves out-of-source vertices. The alternative rejects them. Clipping is not exposed because rectangle clipping can create more than four vertices, which the canonical quadrilateral cannot represent without loss. / 默认边界行为保留源图外顶点，替代策略为拒绝。由于矩形裁剪可能产生超过四个顶点且规范四边形无法无损表示，因此不公开裁剪。
5. Polygon intersection, area, IoU, and rotated NMS are managed and image-library-neutral. AABB is an early reject only. Bounded Sutherland-Hodgman clipping uses at most eight intermediate vertices; touching with zero area returns zero IoU. Candidate/result/workspace bounds and cancellation apply inside NMS work. / 多边形相交、面积、IoU 与 rotated NMS 使用托管实现且不绑定图像库。AABB 仅作快速拒绝。有界 Sutherland-Hodgman 裁剪最多使用八个中间顶点；零面积接触返回零 IoU。候选/结果/workspace 限制与取消贯穿 NMS 工作。
6. The richer type remains `JYPPX.DeploySharp.Visual.OrientedDetectionResult` as required by the stage contract. It coexists with Core's compact same-named result in another namespace; consumers importing both use an alias. Core and all backends remain unchanged. / 按阶段契约要求，丰富类型保持为 `JYPPX.DeploySharp.Visual.OrientedDetectionResult`。它与 Core 另一命名空间中的同名精简结果共存；同时导入时消费者使用别名。Core 与全部后端保持不变。

## Consequences / 影响

Visual preserves exact restored geometry and deterministic postprocessing across ONNX Runtime and OpenVINO without vendor types. Applications must configure exporter semantics explicitly and handle the namespace alias when both result families are used. A future general clipped-polygon type or an explicitly named approximate fitted rectangle can be added without weakening the authoritative quadrilateral. / Visual 在不使用厂商类型的情况下，跨 ONNX Runtime 与 OpenVINO 保持精确恢复几何和确定性后处理。应用必须显式配置导出器语义，并在使用两类结果时处理命名空间别名。后续可添加一般裁剪多边形类型或显式命名的近似拟合矩形，而无需削弱权威四边形。

Repository constant ONNX/IR fixtures establish `ContractVerified` behavior only. Named algorithm support requires official preprocessing, exact angle/export/postprocessing semantics, legal assets, and reproducible golden evidence. / 仓库常量 ONNX/IR 夹具仅建立 `ContractVerified` 行为；命名算法支持仍需官方预处理、精确角度/导出/后处理语义、合法资产及可复现黄金证据。
