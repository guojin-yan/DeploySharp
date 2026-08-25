# Visual audio speech / Visual 音频语音

Stage 28 adds an artifact-bound audio contract to the existing Visual package. The executable path is the Wav2Vec2 base-960h CTC profile; Whisper, HuBERT, and pyannote remain explicit source-contract blockers until their complete portable graphs and legal asset terms are available. / 阶段 28 在现有 Visual 包中增加工件绑定的音频合同。当前可执行路径是 Wav2Vec2 base-960h CTC；Whisper、HuBERT 与 pyannote 在获得完整可移植图和合法资产条款前保持明确 blocker。

## Executable profile / 可执行 Profile

`AudioUnderstandingProfiles.Wav2Vec2Base960hCtc` binds the exact Wav2Vec2 revision `22aad52d435eb6dbaf354bdad9b0da84ce7d6156`, the fixed CTC vocabulary, the processor identity, and the named `input_values => logits` ports. The model consumes one mono `float32` waveform at 16 kHz. Normalize once at the input boundary; do not normalize again in a backend. The output frame stride is 320 samples and the vocabulary decoder performs argmax, blank removal, and repeated-token collapse. / `AudioUnderstandingProfiles.Wav2Vec2Base960hCtc` 绑定精确 revision、固定 CTC 词表、Processor Identity 与 `input_values => logits` 具名端口。模型输入为 16 kHz 单声道 `float32` 波形；只在输入边界归一化一次，Backend 不得重复归一化。输出帧步长为 320 samples，词表解码执行 argmax、blank 删除和重复 token 折叠。

The profile accepts the ONNX graph `onnx/wav2vec2-base-960h-ctc.onnx` for ONNX Runtime and the exact XML/BIN IR pair for OpenVINO. The ModelPack manifest is the source of truth for size, SHA256, port names, conversion metadata, and evidence. / Profile 接受 ONNX Runtime 的 ONNX 图，以及 OpenVINO 的精确 XML/BIN IR 对。ModelPack Manifest 是大小、SHA256、端口、转换元数据和证据的唯一事实源。

## Lifecycle and ownership / 生命周期与所有权

`AudioUnderstandingSession` owns the backend session and immutable profile. `AudioTranscriptionRequest`, `PreparedAudioInput`, `AudioStateSummary`, `AudioTranscriptionResult`, CTC spans, and diagnostics are managed values. Registry, processor/tokenizer, source audio, VAD segmentation, speaker IDs, and model files remain caller-owned. A session is single-writer; concurrent mutation is rejected with the stable visual error code. / `AudioUnderstandingSession` 拥有 Backend Session 和不可变 Profile；请求、准备输入、状态摘要、转写结果、CTC spans 与诊断是托管值。Registry、Processor/Tokenizer、源音频、VAD 分段、speaker ID 与模型文件由调用方拥有。Session 是 single-writer，并以稳定 Visual 错误码拒绝并发修改。

Use `OpenCvAudioInputFactory` when the source is an OpenCV `Mat` or encoded audio fixture. It produces a typed prepared input once; the Backend receives only the declared tensor. The clean NuGet consumer under `tests/clean-consumer/visual-audio-speech-family` demonstrates package-only compilation, a deterministic skip when external files are absent, and a real Wav2Vec2 transcript when `DEPLOYSHARP_AUDIO_MODEL_ROOT` is configured. / OpenCV `Mat` 或编码音频 fixture 使用 `OpenCvAudioInputFactory`，只生成一次 Typed Prepared Input，Backend 只接收声明的 Tensor。`tests/clean-consumer/visual-audio-speech-family` 展示纯包编译、缺外部文件时的稳定 skip，以及配置 `DEPLOYSHARP_AUDIO_MODEL_ROOT` 后的真实转写。

## Admission status / 准入状态

Wav2Vec2 is External and locally backend-verified, not `AlgorithmVerified`: the official catalog remains empty and no binary is uploaded or downloadable. Whisper needs three audited graphs (encoder, decoder prefill, named KV decode); HuBERT needs a downstream task head; pyannote needs an authorized complete diarization bundle and access grant. No Python runtime proxy, invented KV, or substitute task head is used. / Wav2Vec2 为 External 且完成本机后端验证，但不是 `AlgorithmVerified`；官方 catalog 继续为空，未上传或提供下载。Whisper 需要三张审计图，HuBERT 需要下游任务头，pyannote 需要完整授权的 diarization bundle 和访问许可。不使用 Python 运行时代理、虚构 KV 或替代任务头。

See [audio model acquisition](model-acquisition-audio-speech.md), [Stage 28 API changes](../history/api-changes-stage28.md), and the repository file `eng/models/audio-speech/audio-speech-family-support.json`. / 参阅[音频模型获取](model-acquisition-audio-speech.md)、[阶段 28 API 变更](../history/api-changes-stage28.md)与仓库文件 `eng/models/audio-speech/audio-speech-family-support.json`。
