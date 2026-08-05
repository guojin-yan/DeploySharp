# ADR 0014: Pose result, coordinate, heatmap and OKS boundary / 姿态结果、坐标、热力图与 OKS 边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

Pose exports do not share one output representation. Common families emit direct candidate keypoints, per-keypoint heatmaps, SimCC vectors, part-affinity fields, or packed detector rows. Their coordinates may be model pixels, normalized values, tensor-grid centers, or exporter-specific corrected coordinates. Treating these forms heuristically changes both accuracy and deterministic behavior. Core already contains a small generic `JYPPX.DeploySharp.Results.Vision.PoseResult`, but it requires a detection and cannot express heatmap-only results, explicit validity, visibility, topology, source index, or OKS. / Pose 导出并不共享一种输出表示。常见家族会输出 direct 候选关键点、逐关键点热力图、SimCC 向量、部件亲和场或打包检测行；坐标可能是模型像素、归一化值、张量网格中心或导出器特有修正坐标。用启发式方式混用这些形式会改变精度与确定性。Core 已包含精简通用的 `JYPPX.DeploySharp.Results.Vision.PoseResult`，但它要求绑定检测结果，无法表示纯热力图结果、显式有效性、可见性、拓扑、来源索引或 OKS。

## Decision / 决策

Pose domain behavior remains in `JYPPX.DeploySharp.Visual`; Core and all backend adapters remain unchanged. The richer canonical result is `PoseEstimationResult`, avoiding a second public `PoseResult` name. It owns immutable topology and managed keypoint/instance arrays and never retains backend tensors or native request lifetimes. / Pose 领域行为保留在 `JYPPX.DeploySharp.Visual`；Core 与全部后端适配器保持不变。更丰富的规范结果命名为 `PoseEstimationResult`，避免第二个公共 `PoseResult` 名称。它拥有不可变拓扑及托管关键点/实例数组，不保留后端张量或 native request 生命周期。

Alpha.1 implements only two explicit schemas: direct `[1,N,K,C]` tensors with separately named optional boxes/scores, and single-instance NCHW/NHWC heatmaps. Profiles state component indices, value kind, coordinate space, tensor grid, half-pixel or align-corners mapping, and optional score outputs. Decoders reject missing or undeclared tensors, incompatible types/shapes, non-finite values, out-of-range probabilities, and configured memory-bound violations. No sigmoid, softmax, sub-pixel correction, layout guess, or coordinate heuristic is implicit. / alpha.1 仅实现两种显式 Schema：带独立命名可选 boxes/scores 的 direct `[1,N,K,C]` 张量，以及单实例 NCHW/NHWC 热力图。Profile 明确组件索引、值类型、坐标空间、张量网格、half-pixel 或 align-corners 映射及可选分数输出。解码器拒绝缺失或未声明张量、不兼容类型/形状、非有限值、越界概率和超过配置内存上限的结果；不隐式执行 sigmoid、softmax、亚像素修正、布局猜测或坐标启发式。

Source coordinates use pixel-center semantics and the recorded `ImageTransform`. Boundary policy is explicit: preserve, clip, or mark invalid. Stable direct ordering is score descending then source index ascending; heatmap ties choose the lowest row-major index. Visibility and validity are separate because an unknown visibility label is not equivalent to an invalid coordinate. / 源图坐标使用像素中心语义及已记录的 `ImageTransform`。边界策略显式选择保留、裁剪或标记无效。direct 稳定排序为分数降序后来源索引升序；热力图同分选择最小行优先索引。可见性与有效性分离，因为未知可见性标签不等同于无效坐标。

`PoseOks.CalculateSimilarity` and suppression implement a deterministic inference-time pairwise OKS variant using explicit per-keypoint sigmas and an explicit reference area. The equation follows the COCO exponential distance normalization, but this API is not COCO evaluator matching: it does not implement annotation-ignore, crowd, area-range, maximum-detection, matching, recall, or AP logic. / `PoseOks.CalculateSimilarity` 与抑制使用显式逐关键点 sigma 和显式参考面积，实现确定性的推理期成对 OKS 变体。公式遵循 COCO 指数距离归一化，但该 API 不等同于 COCO evaluator：不实现 annotation-ignore、crowd、面积区间、最大检测数、匹配、召回或 AP 逻辑。

## Consequences / 影响

ONNX Runtime and OpenVINO execute the same named tensors without Pose-specific backend code, and Visual.OpenCV remains an optional encoded-image adapter. SimCC, PAF/associative embedding, UDP/DARK, packed YOLO-Pose, tracking, 3D Pose, model-specific flip tests, and algorithm support claims require dedicated exact schemas and official golden evidence. The reproducible constant ONNX/IR graphs prove only contract behavior and supply-chain integrity. / ONNX Runtime 与 OpenVINO 无需 Pose 特例即可执行相同命名张量，Visual.OpenCV 仍是可选编码图像适配器。SimCC、PAF/关联嵌入、UDP/DARK、打包 YOLO-Pose、跟踪、3D Pose、模型特有翻转测试与算法支持声明，都需要专用精确 Schema 及官方黄金证据。可复现常量 ONNX/IR 图只证明契约行为与供应链完整性。
