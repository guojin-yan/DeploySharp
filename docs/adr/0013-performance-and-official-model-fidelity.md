# ADR 0013: performance and official-model fidelity / 性能与官方模型保真

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

DeploySharp is a deployment library, so merely producing plausible output is insufficient. End-to-end latency includes decode, resize, channel conversion, normalization, layout conversion, host/device transfer, backend inference, output decode, NMS or dense-map restoration. Model accuracy can also change when interpolation, resize rounding, padding, color order, normalization, activation, coordinate convention, threshold, or NMS differs from the official implementation. / DeploySharp 是部署库，因此仅产生看似合理的输出并不充分。端到端延迟包含解码、缩放、通道转换、归一化、布局转换、主机/设备传输、后端推理、输出解码、NMS 或稠密图恢复。插值方式、缩放取整、填充、颜色顺序、归一化、激活、坐标约定、阈值或 NMS 与官方实现不一致时，也会改变模型精度。

Synthetic ONNX/IR fixtures are valuable for deterministic adapter contracts, but cannot prove the speed or accuracy of an actual algorithm. / 合成 ONNX/IR 夹具适合验证确定性的适配器契约，但不能证明真实算法的速度或精度。

## Decision / 决策

DeploySharp separates two evidence levels. `ContractVerified` means tensor/backend/lifecycle behavior is proven with deterministic fixtures. `AlgorithmVerified` means a specific model artifact/exporter/backend combination also matches the official reference preprocessing and postprocessing with documented golden inputs, expected outputs, tolerances, model hash, source, license, opset/export settings, and runtime versions. Public model support tables must not collapse these levels. / DeploySharp 区分两级证据。`ContractVerified` 表示使用确定性夹具证明张量、后端与生命周期行为；`AlgorithmVerified` 表示特定模型工件、导出器和后端组合还通过了官方参考预处理/后处理对照，并记录黄金输入、期望输出、容差、模型哈希、来源、许可证、opset/导出设置和运行时版本。公开模型支持表不得混淆这两级证据。

Every formal model profile must encode official preprocessing exactly: decode orientation, color order, alpha policy, resize mode and interpolation, aspect-ratio rounding, crop/padding value and alignment, input range, mean/std or scale, dtype, layout, batch, and quantization. Postprocessing must likewise encode activation, output schema/layout, coordinate convention, threshold, tie-break, NMS/OKS parameters, mask resize, label mapping, and official rounding/clipping behavior. Unknown values block `AlgorithmVerified` status rather than being guessed. / 每个正式模型 Profile 必须精确编码官方预处理：解码方向、颜色顺序、alpha 策略、缩放模式与插值、宽高比取整、裁剪/填充值与对齐、输入范围、mean/std 或 scale、dtype、layout、batch 与量化。后处理同样必须编码激活、输出 Schema/layout、坐标约定、阈值、tie-break、NMS/OKS 参数、mask 缩放、标签映射及官方取整/裁剪行为。未知值会阻止 `AlgorithmVerified`，不得猜测。

Performance evidence separates cold model/build time, warm preprocessing, host-to-device transfer, backend execution, device-to-host transfer, postprocessing, and end-to-end latency. Benchmarks record warmup, sample count, P50/P95, throughput, allocations, model/input size, TFM, build mode, backend/runtime, CPU/GPU, power/precision settings, and cache state. Engine build and first-run cache creation are never hidden inside warm inference numbers. / 性能证据分别记录冷启动模型/构建时间、热预处理、主机到设备传输、后端执行、设备到主机传输、后处理与端到端延迟。基准记录预热、样本数、P50/P95、吞吐、分配量、模型/输入尺寸、TFM、构建模式、后端/runtime、CPU/GPU、功耗/精度设置与缓存状态。engine 构建和首次缓存创建绝不隐藏在热推理数字中。

Hot preprocessing/postprocessing paths avoid reflection, per-element LINQ, unnecessary tensor copies, and repeated allocations. Modern TFMs may use `Span<T>`, SIMD, pooling, pinned buffers, native library primitives, and bounded parallelism behind compatibility branches; legacy TFMs retain correct fallbacks. Ownership and safety are not weakened for an unmeasured zero-copy claim. / 热点前后处理路径避免反射、逐元素 LINQ、不必要张量复制与重复分配。现代 TFM 可通过兼容分支使用 `Span<T>`、SIMD、池化、固定缓冲区、native 库原语和有界并行；旧 TFM 保留正确 fallback。不得为了未经测量的零拷贝声明而削弱所有权与安全性。

## Consequences / 影响

Every new formal model interface requires both official-fidelity tests and representative performance measurements before it enters the supported model table or ModelFactory official catalog. Backend adapters can complete contract verification independently, but their micro-fixture results are labeled accordingly. Regressions that exceed a documented tolerance or performance budget block release unless the baseline and rationale are explicitly revised. / 每个新的正式模型接口在进入支持模型表或 ModelFactory 官方目录前，都需要官方保真测试和代表性性能测量。后端适配器可以独立完成合同验证，但微型夹具结果必须按此标注。超过已记录容差或性能预算的回归会阻止发布，除非显式修订基线并说明理由。
