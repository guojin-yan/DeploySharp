# Stage 25 API changes / 阶段 25 API 变更

- Added immutable `GenerativeVisionLanguageProfile`, artifact/tensor/processor/tokenizer/generation contracts, artifact bindings/bundles, exact BLIP family factory Profiles, and explicit source-only blocker Profiles. / 新增不可变生成模型族 Profile、各组件合同、Bundle 与精确可执行/阻断 Profile。
- Added `BlipBertTokenizer` backed by `Microsoft.ML.Tokenizers`, typed requests/token sequences, image/generation identities, owned token-score/result/timing data, and common `GenerationResult` reuse. / 新增基于官方词表的 Tokenizer、typed 请求/Token、状态 Identity、自有结果与通用生成结果复用。
- Added stateful `GenerativeVisionLanguageSession` for set-image, repeated generation, streaming callback, clear, async cancellation/timeout, deterministic concurrency rejection, and ordered disposal. / 新增有状态多 Session 生命周期、重复生成、流式回调、清除、异步取消/超时、并发拒绝与有序释放。
- Added stable `DS-VISUAL-4801` through `4807`, exact named-port and capacity validation, full-prefix no-KV generation, EOS/min/max logic, finite logits, and artifact/tokenizer/config identity checks. / 新增稳定错误码、具名端口、容量、完整前缀生成、EOS 与 Identity 校验。
- Added `OpenCvInterpolation.PillowBicubic` and `OpenCvGenerativeVisionLanguageInputFactory` for single-decode RGB/gray/alpha file/byte inputs with profile-bound normalization and source SHA. / 新增 Pillow-compatible Bicubic 与 BLIP OpenCV 输入工厂。
- ModelFactory artifacts and queries now bind vision backbone, Q-Former, language model, prompt template, generation config/mode, and KV schema; bundle selection rejects mixed identities. / ModelFactory 新增生成 Bundle Identity 和混配拒绝。

No existing public type was removed. There is no V1 shim, single-model NuGet, bundled model/tokenizer/native runtime, Python fallback, official-catalog admission, Release publication, or TensorRT implementation. / 未删除既有 API；不提供 V1 Shim、单模型包、内置资产、Python 回退、目录准入、Release 发布或 TensorRT。
