# Stage 21 API changes / 阶段 21 API 变更

Stage 21 extends the existing portable-detector API; no single-model package or duplicate result DTO is introduced. Existing Stage 18 profiles remain source compatible because new option parameters are appended with defaults. / 阶段 21 扩展既有便携检测器 API，不新增单模型包或重复结果 DTO。新选项参数均以默认值追加，阶段 18 Profile 保持源码兼容。

## Public additions / 公共新增

- `PortableDetectorFamily.RTDETRRawDet` and `RTDETRv2Det` separate raw Paddle queries and official PyTorch v2 deploy output from decoded Paddle rows. / 分离 Paddle raw query、官方 PyTorch v2 deploy 输出与 Paddle 已解码行。
- `PortableDetectorOutputKind.RtDetrRaw` and `RtDetrV2Decoded`, plus `PortableDetectorBoxFormat`, `PortableDetectorCoordinateSpace`, `PortableDetectorNmsOwnership`, and `PortableDetectorCountShape`, bind box/count/NMS semantics to the artifact. / 将框、count 与 NMS 语义绑定到工件。
- `PortableDetectorBatchContract` records fixed/dynamic batch metadata and the supported executable range. / 记录固定/动态 batch 元数据及支持的执行范围。
- `PortableDetectorAuxiliaryInputContract` with `PortableDetectorAuxiliaryInputKind`, `PortableDetectorAuxiliarySizeSpace`, and `PortableDetectorSizeOrder` generates typed `im_shape`, `scale_factor`, or `orig_target_sizes` tensors from one prepared-input geometry source. / 从单一 prepared-input 几何源生成 typed 辅助 tensor。
- `PortableDetectorProfiles.CreateRTDETRRaw` and `CreateRTDETRv2` create the new immutable profiles. `CreateRTDETR` now binds scalar or batch-vector count shape through options. / 创建新不可变 Profile；既有 CreateRTDETR 可绑定 scalar 或 batch-vector count。
- `PortableDetectorProfile` exposes immutable thresholds, capacity bounds, processing versions, batch contract, auxiliary contracts, and `CreateAuxiliaryInputs`. / 暴露不可变阈值、容量、处理版本、batch/辅助合同与生成入口。
- `OpenCvPortableDetectorPreprocessing.CreateFromBytes` and generic `Create(OpenCvImageSource, ...)` add PNG/JPEG/byte input paths while retaining single decode. / 增加 PNG/JPEG/bytes 入口并保持单次解码。

## Behavior and ownership / 行为与所有权

Paddle decoded graphs own decode/NMS and return source-pixel xyxy rows. Paddle raw queries use sigmoid, global bounded top-k, normalized cxcywh restoration, and no NMS. RT-DETRv2 graph outputs are already source-pixel xyxy because `orig_target_sizes` is source width then height; managed code does not restore twice. / Paddle 已解码图拥有 decode/NMS 并返回源图 xyxy；Paddle raw query 使用 sigmoid、全局有界 top-k、归一化 cxcywh 恢复且不做 NMS；RT-DETRv2 因 `orig_target_sizes` 为源图宽后高而直接输出源图 xyxy，托管代码不会重复恢复。

Auxiliary arrays are copied into owned Core tensors held by `PreparedVisualInput`; backend sessions never borrow OpenCV memory. The common pipeline preserves cancellation, async execution, bounded concurrency, one managed decode, result ownership after backend outputs are disposed, deterministic capacity failures, and stable disposed/backend diagnostics. / 辅助数组复制到由 PreparedVisualInput 持有的 Core tensor；backend session 不借用 OpenCV 内存。通用管线继续保证取消、异步、有界并发、单次托管解码、输出释放后结果自有、确定性容量失败与稳定释放/backend 诊断。

See [the RT-DETR family guide](visual-portable-detectors.md) for exact tensor names, auxiliary values, dynamic axes, backend evidence, and blockers. / 精确 tensor 名、辅助值、动态轴、后端证据与 blocker 参阅模型族指南。
