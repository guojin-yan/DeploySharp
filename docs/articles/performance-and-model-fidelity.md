# Performance and model fidelity / 性能与模型保真

DeploySharp optimizes two independent requirements: fast execution and faithful reproduction of the official model pipeline. A fast result with different preprocessing is not accurate; an accurate decoder wrapped around avoidable copies is not a complete deployment solution. / DeploySharp 同时优化两个独立要求：快速执行与忠实复现官方模型流程。使用不同预处理得到的快速结果并不准确；在可避免复制之上实现准确解码也不是完整部署方案。

## Evidence levels / 证据等级

| Level / 等级 | Meaning / 含义 | Allowed claim / 允许声明 |
| --- | --- | --- |
| Contract verified / 合同已验证 | Deterministic fixtures prove tensor names/types/shapes, numerical transport, lifecycle, cancellation, and decoder rules. / 确定性夹具证明张量名称/类型/形状、数值传输、生命周期、取消与解码规则。 | Backend or decoder contract works / 后端或解码器契约可用 |
| Algorithm verified / 算法已验证 | A specific legal model artifact matches the official reference pipeline on golden inputs within documented tolerances. / 特定合法模型工件在黄金输入上按记录容差匹配官方参考流程。 | The exact model/exporter/backend combination is supported / 该精确模型、导出器、后端组合受支持 |

Tiny identity, constant, classification, detection, and segmentation graphs in `tests/assets` are contract fixtures. They do not establish official algorithm accuracy or performance and never enter the official ModelFactory catalog. / `tests/assets` 中的微型 identity、constant、分类、检测和分割图都是合同夹具，不建立正式算法精度或性能证据，也不会进入 ModelFactory 官方目录。

## Official fidelity checklist / 官方保真清单

A formal model profile records the model SHA256, source/license, exporter and version, opset, input/output signatures, official label set, and reference implementation commit. Preprocessing records EXIF/orientation behavior, decode color space, channel order, alpha handling, resize interpolation, aspect-ratio rounding, crop and padding alignment/value, input range, mean/std or scale, dtype, layout, batch, and quantization. / 正式模型 Profile 记录模型 SHA256、来源/许可证、导出器及版本、opset、输入输出签名、官方标签集与参考实现 commit。预处理记录 EXIF/方向行为、解码色彩空间、通道顺序、alpha 处理、缩放插值、宽高比取整、裁剪与填充对齐/数值、输入范围、mean/std 或 scale、dtype、layout、batch 与量化。

Postprocessing records activation, tensor schema/layout, confidence semantics, thresholds, deterministic tie-breaks, coordinate and clipping rules, NMS/OKS/mask behavior, label mapping, and official rounding. Golden tests compare both intermediate prepared tensors and final results. Exact values are preferred; floating comparisons declare absolute/relative tolerance and explain the source of numerical drift. / 后处理记录激活、张量 Schema/layout、置信度语义、阈值、确定性 tie-break、坐标与裁剪规则、NMS/OKS/mask 行为、标签映射与官方取整。黄金测试同时比较中间已准备张量与最终结果。优先精确比较；浮点比较必须声明绝对/相对容差并解释数值漂移来源。

## Performance checklist / 性能清单

Measure Release builds after warmup and report P50/P95 latency, throughput, managed allocations, input/model size, TFM, backend/runtime, hardware, precision, thread/stream settings, and cache state. Report cold load/build separately from warm preprocessing, host-to-device copy, backend execution, device-to-host copy, postprocessing, and end-to-end latency. / 使用 Release 构建并在预热后测量，报告 P50/P95 延迟、吞吐、托管分配、输入/模型尺寸、TFM、后端/runtime、硬件、精度、线程/stream 设置与缓存状态。冷加载/构建必须与热预处理、主机到设备复制、后端执行、设备到主机复制、后处理及端到端延迟分别报告。

Hot paths should use backend/native primitives, contiguous buffers, pooled reusable workspaces, SIMD or bounded parallelism where measured. Avoid reflection, per-element LINQ, repeated shape parsing, and copies that do not establish an ownership boundary. Any zero-copy path must prove pinning and lifetime safety; the default remains owned output. / 热路径应在经过测量后使用后端/native 原语、连续缓冲区、池化可复用工作区、SIMD 或有界并行。避免反射、逐元素 LINQ、重复 shape 解析及不能建立所有权边界的复制。任何零拷贝路径都必须证明固定和生命周期安全；默认仍返回自有输出。

Legacy TFMs use correct compatibility implementations. Modern `net8.0`/`net10.0` paths may use newer runtime features behind compile-time branches, but every branch retains the same model semantics and golden results. / 旧 TFM 使用正确的兼容实现。现代 `net8.0`/`net10.0` 路径可在编译分支中使用新运行时特性，但所有分支必须保持相同模型语义与黄金结果。
