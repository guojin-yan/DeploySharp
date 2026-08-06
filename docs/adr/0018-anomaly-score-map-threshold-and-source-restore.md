# ADR 0018: Anomaly score, map, threshold, and source restoration boundary / 异常分数、异常图、阈值与源图恢复边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-06

## Context / 背景

Anomaly models expose incompatible score ranges, map layouts, channel meanings, normalization rules, thresholds, and resize conventions. Guessing any of these from tensor shape can silently change accuracy. Returning backend tensors also couples result lifetime to a native request. / 异常模型的分数范围、异常图布局、通道含义、归一化规则、阈值与 resize 约定并不统一；根据张量形状猜测这些语义会静默改变精度。直接返回后端张量还会把结果生命周期绑定到 native request。

## Decision / 决策

1. Core remains unchanged. Visual owns the backend-neutral anomaly schema, decoder, typed pipeline, owned score maps and binary masks. / Core 保持不变；Visual 拥有后端无关的异常 Schema、解码器、类型化 Pipeline、自有分数图与二值掩码。
2. Alpha.1 requires exactly one named scalar image score and one named map. Layout, value semantics, channel aggregation and source/model coordinate space are explicit. Extra or missing outputs are rejected. / Alpha.1 要求恰好一个命名图像标量分数与一个命名异常图；layout、数值语义、通道聚合及源图/模型坐标空间必须显式声明；多余或缺失输出均拒绝。
3. Probability, non-negative distance and binary values are validated before postprocessing. Normalization supports none, min-max and fixed range. Constant min-max maps become zero maps with a stable warning. Percentile and model-provided threshold policies remain unsupported until their semantics are fully specified. / 后处理前校验概率、非负距离与二值语义。归一化支持 none、min-max 与固定范围；常量 min-max 图输出零图并给出稳定警告。百分位与模型提供阈值在语义完整定义前保持不支持。
4. Resize uses explicit nearest or bilinear half-pixel semantics. `ImageTransform` is the sole authority for restoring model-space maps to source space. Letterbox regions outside the model image become zero; crop restoration only covers the transformed source area. / resize 使用显式 nearest 或 bilinear half-pixel 语义；`ImageTransform` 是模型空间恢复到源图空间的唯一依据。Letterbox 位于模型图之外的区域填零；crop 恢复只覆盖变换后的源图区域。
5. Every returned map/mask owns managed storage and remains valid after backend request/session disposal. Work, result bytes and pixel counts use checked bounded arithmetic; long loops observe cancellation. / 所有返回 map/mask 均拥有托管存储，并在后端 request/session 释放后继续有效。工作量、结果字节与像素数使用 checked 有界算术，长循环观察取消。
6. ModelPack uses existing multi-artifact and extension support for ONNX and OpenVINO IR XML/BIN. ModelFactory Preview evidence may select verified portable formats, but the official catalog remains empty until legal assets and AlgorithmVerified evidence are approved. / ModelPack 使用既有多工件与扩展机制承载 ONNX 及 OpenVINO IR XML/BIN。ModelFactory Preview 证据可选择已验证的可移植格式，但合法资产与 AlgorithmVerified 证据获批前，官方目录保持为空。

## Consequences / 影响

- Model-family adapters must reproduce the official preprocessing, activation, score reduction, normalization, interpolation, threshold and source restoration exactly; no generic default establishes algorithm fidelity. / 模型族适配必须精确复现官方前处理、激活、分数归约、归一化、插值、阈值与源图恢复；通用默认值不能证明算法保真。
- ONNX Runtime and OpenVINO remain pure named-tensor executors. Visual.OpenCV remains an optional image adapter. No vendor types enter Core or Visual. / ONNX Runtime 与 OpenVINO 仍是纯命名张量执行器；Visual.OpenCV 仍是可选图像适配器；vendor 类型不进入 Core 或 Visual。
- Repository constant graphs are ContractVerified adapter fixtures only. They are not Anomalib, PatchCore, PaDiM, STFPM or accuracy/performance claims. / 仓库常量图仅为 ContractVerified 适配夹具，不代表 Anomalib、PatchCore、PaDiM、STFPM，也不构成精度或性能结论。
