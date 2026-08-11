# Stage 28 API changes / 阶段 28 API 变更

- Added `AudioUnderstandingProfile`, `AudioUnderstandingProfiles`, `AudioArtifactContract`/binding, audio task/role enums, and Wav2Vec2 CTC vocabulary/session contracts in `JYPPX.DeploySharp.Visual`. / 在 `JYPPX.DeploySharp.Visual` 中新增 Audio Profile、Wav2Vec2 CTC 词表与 Session 合同。
- Added typed `AudioTranscriptionRequest`, `PreparedAudioInput`, `AudioStateSummary`, `AudioTranscriptionResult`, frame spans, diagnostics, and single-writer lifecycle rules. / 新增转写请求、Prepared 输入、状态、结果、帧 spans、诊断和 single-writer 生命周期。
- Added `OpenCvAudioInputFactory` in `JYPPX.DeploySharp.Visual.OpenCV` for one-time source preparation and declared tensor ownership. / 在 Visual.OpenCV 中新增一次性音频输入准备工厂。
- Extended ModelFactory artifact JSON and `ModelQuery`/`ModelBundleQuery` with optional `vadId` and `speakerId`, preserving exact-match/bundle rejection semantics. / ModelFactory Artifact JSON 与 ModelQuery/ModelBundleQuery 增加可选 `vadId`、`speakerId`，保持精确匹配和混配拒绝。
- Added four Stage 28 ModelPack manifests, a package-only clean consumer, and stable audio diagnostics while retaining the empty official catalog. / 新增四份 Manifest、纯包 Consumer 和稳定音频诊断；官方 catalog 继续为空。

No existing public type was removed. No model-specific NuGet package, bundled model, Python fallback, TensorRT path, or downloadable release asset was added. / 未删除既有公共类型；未增加模型专用 NuGet、内置模型、Python fallback、TensorRT 路径或可下载发布资产。
