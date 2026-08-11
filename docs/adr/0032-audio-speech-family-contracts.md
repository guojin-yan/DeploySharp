# ADR 0032: Audio speech family contracts / 音频语音模型族合同

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-10

Audio families are not interchangeable. Wav2Vec2 CTC owns a normalized waveform and a fixed vocabulary; Whisper owns mel features, autoregressive decoder state, and timestamp-token semantics; HuBERT is a representation checkpoint without a task head; pyannote is a gated multi-component diarization pipeline. Family names or tensor rank cannot establish compatibility. / 音频模型族不可互换。Wav2Vec2 CTC 负责归一化波形和固定词表；Whisper 负责 mel 特征、自回归 Decoder 状态和 timestamp token；HuBERT 是没有任务头的表征 checkpoint；pyannote 是 gated 多组件 diarization pipeline。不能从族名或 Tensor rank 推断兼容。

DeploySharp therefore uses one immutable artifact-bound `AudioUnderstandingProfile` and role bundle. The Processor derives the waveform tensor once; the backend executes only the named ports. `AudioUnderstandingSession` owns backend sessions and request-local state, while source audio, processor/tokenizer, VAD segmentation, speaker IDs, registry, and model files remain caller-owned. / 因此 DeploySharp 采用不可变、工件绑定的 `AudioUnderstandingProfile` 与 role bundle。Processor 只派生一次波形 Tensor，Backend 只执行具名端口；Session 拥有 Backend 与请求状态，其余资源由调用方拥有。

The first executable profile is Wav2Vec2 base-960h CTC on ONNX Runtime CPU and OpenVINO FP32 IR CPU. Its local transcript parity and NuGet consumer are evidence of this exact bundle only. Whisper, HuBERT, and pyannote remain explicit blockers; no invented KV, Python proxy, or substitute head is permitted. Successful local execution does not authorize `AlgorithmVerified`, catalog admission, or redistribution. / 首个可执行 Profile 是 Wav2Vec2 base-960h CTC 双后端路径。其转写对齐和纯包 Consumer 证据只适用于该精确 bundle；其他族保持 blocker。不允许虚构 KV、Python 代理或替代任务头；本机成功不代表 AlgorithmVerified、目录准入或再分发。
