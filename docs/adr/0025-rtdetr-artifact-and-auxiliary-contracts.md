# ADR 0025: RT-DETR artifact and auxiliary contracts / RT-DETR 工件与辅助输入合同

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-08

## Context / 背景

RT-DETR exports with similar names can expose incompatible contracts: Paddle decoded rows plus scalar or vector `boxes_num`, Paddle raw query logits/boxes, or PyTorch RT-DETRv2 deploy triplets. The Stage 18 local ONNX also fails at `p2o.Tile.3`. Tensor rank, filename, and adapter-side geometry reconstruction cannot safely distinguish these cases. / 名称相近的 RT-DETR 导出可能具有不兼容合同；阶段 18 本机 ONNX 还会在 `p2o.Tile.3` 失败。tensor rank、文件名与 adapter 侧重复计算几何均不能安全区分这些情况。

## Decision / 决策

1. Each immutable profile binds exact artifact SHA/opset/provenance, named inputs/outputs, batch/count shape, box format/space, threshold, capacity, NMS owner, and processing versions. / 每个不可变 Profile 绑定精确工件与全部执行语义。
2. Typed auxiliary contracts are the only source for `im_shape`, `scale_factor`, and `orig_target_sizes`; OpenCV creates owned Core tensors and backends only consume them. / typed 辅助合同是唯一事实源，OpenCV 生成自有 Core tensor，后端只消费。
3. Decoded Paddle, raw Paddle, and RT-DETRv2 use distinct family/output kinds. The old failed artifact remains a separate manifest and test; runnable artifacts do not overwrite it. / 三种合同使用不同 family/output kind；旧失败工件保留独立清单与测试，可执行工件不覆盖它。
4. Reuse `DetectionResult`, `ImageTransform`, `PreparedVisualInput`, common pipeline/session lifecycle, and backend adapters. No model-specific package, DTO, coordinate-restoration copy, fallback name guessing, or TensorRT placeholder is allowed. / 复用现有结果、几何、输入、生命周期与后端；不新增单模型包、重复 DTO/坐标实现、名称猜测或 TensorRT 占位。

## Consequences / 后果

The V1 execution row can close using exact alternative artifacts while failure evidence remains reproducible. Dynamic batch metadata is expressible, but the current image pipeline intentionally executes batch one. RT-DETRv2 is a complete contract with an explicit external gate until an exact artifact is acquired; missing evidence cannot be promoted to a manifest or `AlgorithmVerified`. / V1 执行行可通过精确替代工件闭合，同时保留可复现失败证据。动态 batch 元数据可表达，但当前图像管线明确只执行 batch 1。RT-DETRv2 合同完整，但在取得精确工件前保持外部门控；缺失证据不得升级为 manifest 或 AlgorithmVerified。
