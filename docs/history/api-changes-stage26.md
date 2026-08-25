# Stage 26 API changes / 阶段 26 API 变更

- Added immutable `NativeMultimodalProfile`, processor/tokenizer/KV contracts, exact LLaVA OneVision factory, three-role bundle, prepared image/token sequence, image/KV summaries, result/timing wrappers, and explicit Qwen/Phi blocker metadata. / 新增不可变原生多模态合同、三角色 Bundle、输入/状态/结果与 blocker 元数据。
- Added `NativeMultimodalSession` set-image, Prefill, named past/present Decode, streaming callback, async cancellation/timeout, deterministic concurrency rejection, clear, and ordered disposal. / 新增三 Session 生命周期、Prefill/KV Decode、流式、异步取消/超时、并发拒绝、清除与释放。
- Added `Qwen2NativeMultimodalTokenizer` on net8/net9/net10 with verified official assets, exact chat template/image-sentinel expansion, default Caption prompt, EOS cleanup, and explicit older-TFM capability boundary. / 新增 Managed Qwen2 Tokenizer、官方资产校验、模板/Sentinel/默认 Caption 与旧 TFM 能力边界。
- Added `OpenCvNativeMultimodalInputFactory` for PNG/JPEG/bytes, BGR/RGB/gray/alpha, single decode, official anyres grid/base/crop/pad/normalize, source identity, and typed packed-token metadata. / 新增单次 Decode 的官方 Anyres OpenCV 工厂。
- Added stable `DS-VISUAL-4901..4908` and exact named-port/type/shape/capacity/NaN/identity/KV validation. / 新增稳定错误码及端口、类型、Shape、容量、NaN、Identity 与 KV 校验。
- Extended ModelFactory artifact/query JSON with positive `imageCount` and `contextLength`; bundle selection now filters and rejects mixed image/context identity. / ModelFactory 新增图像数与 Context 查询/混配拒绝。

No existing public type was removed. There is no V1 shim, single-model NuGet, bundled model/tokenizer/golden/native runtime, Python runtime fallback, official-catalog admission, Release upload, Actions invocation, or TensorRT implementation. / 未删除既有 API；无 V1 Shim、单模型包、内置资产、Python 运行时回退、Catalog 准入、Release 上传、Actions 或 TensorRT。
