# 音频与语音

DeploySharp.Visual 提供工件绑定的音频输入和语音识别合同。当前有可执行路径的是 Wav2Vec2 base-960h CTC，以及已具备 Encoder、Prefill、KV Decode 三张图的 Whisper tiny.en。HuBERT 和 pyannote 只有任务合同，不能当作可直接转写或说话人分离的实现。

## Wav2Vec2 CTC

AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx 使用 16 kHz 单声道 Float32 波形，输出按帧执行 argmax、blank 删除和重复 token 折叠。输入边界只做一次归一化，后端不重复处理。

~~~csharp
AudioUnderstandingProfile profile =
    AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx();
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
var backend = OnnxRuntimeBackendProvider.BackendId;
var contract = profile.GetArtifact(AudioArtifactRole.CtcEncoderHead);
var bundle = new AudioUnderstandingBundle(profile, new[]
{
    new AudioArtifactBinding(
        AudioArtifactRole.CtcEncoderHead,
        contract.CreateArtifact(modelPath, backend))
});
Wav2Vec2CtcVocabulary vocabulary =
    Wav2Vec2CtcVocabulary.Load(vocabularyPath, profile.Tokenizer!);
using var session = new AudioUnderstandingSession(
    registry, bundle,
    vocabulary,
    new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
using PreparedAudioInput input =
    new OpenCvAudioInputFactory().CreateFromWavFile(
        wavPath, profile, "sample", "source");
session.SetAudio(input);
AudioTranscriptionResult result = session.Transcribe(
    new AudioTranscriptionRequest(
        AudioUnderstandingTask.CtcTranscription, "en"));
~~~

实际 Bundle 的角色名称必须与 Profile 的工件合同一致；部署前请参阅测试项目中的完整绑定示例。

## Whisper 三图

CreateWhisperTinyEnglishOnnx 绑定 Encoder、Decoder Prefill 和具名 Past/Present KV Decode。使用 WhisperLogMelExtractor 复用 Mel 滤波器和 FFT 工作区，再由 OpenCvAudioInputFactory.CreateWhisperFromWavFile 创建固定 [1,80,3000] 特征。WhisperTokenizer 负责固定转写提示，WhisperUnderstandingSession 提供同步和异步 Transcribe。

Whisper 的三张图、tokenizer、processor 配置和 KV 形状必须来自同一导出；不能用一张普通 CTC 图替代 decoder，也不能跨模型复用 KV。需要视频/流式场景时，按音频块组织有限队列并使用独立 session，避免多个线程同时修改同一解码状态。

## 生命周期与性能

AudioUnderstandingSession 和 WhisperUnderstandingSession 都是有状态单写者对象。使用 TranscribeAsync 或 SetAudioAsync 可避免阻塞调用线程；并发吞吐请创建有限的独立 session。音频解码、Mel 提取和模型推理的耗时应分别测量，前处理提取器应长期复用，避免每个音频块重复分配 FFT/Mel 工作区。

## 支持边界

模型与后端状态以[模型支持指南](model-support.md)和[模型后端验证矩阵](../model-backend-verification-matrix.md)为准。完整设备耗时与测试条件见[设备性能实测](device-performance-benchmarks.md)；当前 Whisper 三图 Bundle 尚未作为公共 Release 资产自动下载。
