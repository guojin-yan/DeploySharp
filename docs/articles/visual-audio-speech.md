# 音频与语音

`DeploySharp.Visual` 把音频解码、特征处理、模型工件和 CTC/自回归解码分开。当前有完整可执行证据的是 Wav2Vec2 base-960h CTC，以及由本地导出三张 ONNX 图组成的 Whisper tiny.en。HuBERT 和 pyannote 只保留任务合同；它们没有被目录或示例伪装成可直接转写、表征或说话人分离的模型。

## Wav2Vec2 CTC

`CreateWav2Vec2Base960hOnnx` 使用 16 kHz 单声道 Float32 波形，输出按帧执行 argmax、blank 删除和重复 token 折叠。输入边界只做一次归一化，后端不会再次处理。下面的示例展示了完整的工件绑定和一次转写：

```csharp
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

AudioUnderstandingProfile profile =
    AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx();
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
BackendId backend = OnnxRuntimeBackendProvider.BackendId;
AudioArtifactContract contract =
    profile.GetArtifact(AudioArtifactRole.CtcEncoderHead);
var bundle = new AudioUnderstandingBundle(profile, new[]
{
    new AudioArtifactBinding(
        AudioArtifactRole.CtcEncoderHead,
        contract.CreateArtifact(modelPath, backend))
});
Wav2Vec2CtcVocabulary vocabulary =
    Wav2Vec2CtcVocabulary.Load(vocabularyPath, profile.Tokenizer!);
using var session = new AudioUnderstandingSession(
    registry, bundle, vocabulary,
    new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
using PreparedAudioInput input =
    new OpenCvAudioInputFactory().CreateFromWavFile(
        wavPath, profile, "licensed-fixture", "sample-01");
session.SetAudio(input);
AudioTranscriptionResult result = session.Transcribe(
    new AudioTranscriptionRequest(
        AudioUnderstandingTask.CtcTranscription, "en"));
Console.WriteLine(result.Text);
```

实际 Bundle 的角色名称、词表顺序和 blank/unknown ID 必须与 Profile 完全一致。OpenVINO 使用 `CreateWav2Vec2Base960hOpenVino` 并绑定同目录 XML/BIN；不要将 ONNX 工件的后端 ID 改成 OpenVINO 来绕过格式检查。

## Whisper tiny.en 三图

Whisper 不是一个单独的 CTC 图，而是一个有状态的三图生成 Bundle：

| 角色 | 默认文件名 | 输入/输出要点 |
| --- | --- | --- |
| Encoder | `whisper-tiny.en-encoder.onnx` | `[1,80,3000]` log-Mel -> `[1,1500,384]` hidden state |
| Decoder Prefill | `whisper-tiny.en-decoder-prefill.onnx` | Prompt + Encoder hidden state，产生 logits 和完整 Past/Present KV |
| Decoder with Past | `whisper-tiny.en-decoder-with-past.onnx` | 单 token + 命名 Past KV，逐 token 输出 logits 和 Present KV |

三张图必须来自同一 checkpoint、同一 opset-17 导出和同一 KV 命名合同。`checkpoint` 目录至少需要经过 SHA-256 校验的 `tokenizer.json`、`generation_config.json`、`preprocessor_config.json`；不能从文件名猜测端口或拿普通 CTC 图替代 decoder。

```csharp
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

string checkpoint = @"models\whisper-tiny.en\checkpoint";
string graphRoot = @"models\whisper-tiny.en\onnx-whisper-three-graph";
AudioUnderstandingProfile profile =
    AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx();
BackendId backend = OnnxRuntimeBackendProvider.BackendId;

using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
var bundle = new AudioUnderstandingBundle(profile, new[]
{
    Bind(profile, AudioArtifactRole.WhisperEncoder,
        Path.Combine(graphRoot, "whisper-tiny.en-encoder.onnx"), backend),
    Bind(profile, AudioArtifactRole.WhisperDecoderPrefill,
        Path.Combine(graphRoot, "whisper-tiny.en-decoder-prefill.onnx"), backend),
    Bind(profile, AudioArtifactRole.WhisperDecoderWithPast,
        Path.Combine(graphRoot, "whisper-tiny.en-decoder-with-past.onnx"), backend)
});

using var extractor = new WhisperLogMelExtractor(checkpoint, profile.Processor);
using PreparedWhisperInput input =
    new OpenCvAudioInputFactory().CreateWhisperFromWavFile(
        wavPath, profile, extractor, "my-recording");
using var session = new WhisperUnderstandingSession(
    registry, bundle,
    new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
session.SetAudio(input);
var tokenizer = new WhisperTokenizer(checkpoint, profile.Generation!);
WhisperTranscriptionResult result = session.Transcribe(
    tokenizer,
    new WhisperTranscriptionRequest(maximumTokens: 64, requestId: "transcribe-01"));
Console.WriteLine(result.Text);

static AudioArtifactBinding Bind(
    AudioUnderstandingProfile profile, AudioArtifactRole role,
    string path, BackendId backend)
{
    AudioArtifactContract contract = profile.GetArtifact(role);
    return new AudioArtifactBinding(role, contract.CreateArtifact(path, backend));
}
```

`WhisperLogMelExtractor` 会读取 Profile 绑定的 Mel filter、FFT 和采样率配置，固定输出 `[1,80,3000]`。提取器应长期复用；它为并发工作线程维护独立 FFT 工作区，避免每个音频块重新创建滤波器和大数组。`OpenCvAudioInputFactory` 只执行一次 WAV 解码、必要的立体声混音和特征提取，不负责重采样、VAD、语言识别或说话人分离。

## 异步、流式和状态

`WhisperUnderstandingSession` 是 single-writer 有状态对象，Encoder 状态和 Decoder KV 只属于当前音频。视频或实时音频应使用有界生产者/消费者队列：生产者提前解码和提取下一块，消费者调用 `SetAudioAsync`、`TranscribeAsync`；需要并发吞吐时创建有限数量的完整三图 Session，每个 Session 只能有一个活动写入者。

```csharp
await session.SetAudioAsync(input, cancellationToken: token);
WhisperTranscriptionResult result = await session.TranscribeAsync(
    tokenizer, new WhisperTranscriptionRequest(maximumTokens: 64),
    cancellationToken: token);
```

测量时应分别记录 WAV 解码、Mel 预处理、Encoder、Prefill、逐 token Decode 和文本解码。`result.Timing` 已提供 `Preprocess`、`Encode`、`Prefill`、`DecodeTotal` 和 `Total`；模型加载、CUDA 初始化和 OpenVINO 编译不要混入稳态延迟。

## 资产与验证

推荐的外部目录如下，文件由应用或测试包提供，不由 Visual 包静默下载：

```text
whisper-tiny.en/
  checkpoint/
    tokenizer.json
    generation_config.json
    preprocessor_config.json
  onnx-whisper-three-graph/
    whisper-tiny.en-encoder.onnx
    whisper-tiny.en-decoder-prefill.onnx
    whisper-tiny.en-decoder-with-past.onnx
```

可复现验证入口是 `tests/DeploySharp.Visual.OpenCV.Tests/Stage28WhisperWavExternalIntegrationTests.cs`。设置 `DEPLOYSHARP_RUN_EXTERNAL_MODELS=1`，并按测试中的环境变量指向上述目录，即可复现带 token、文本、Feature SHA-256 和分阶段耗时的 LibriSpeech fixture。当前三图 Bundle 尚未作为公共 Release 资产自动下载；`CreateWhisperTinyEnglishContract` 仍明确表示源模型合同和发布阻断。

## 支持边界

模型与后端状态以[模型支持指南](model-support.md)和[模型后端验证矩阵](../model-backend-verification-matrix.md)为准。当前 Whisper 可执行证据集中在 Windows x64 的 ONNX Runtime CPU；没有因此宣称 OpenCV DNN、OpenVINO、TensorRT 或其他设备已经完成同等级验证。设备耗时与测试条件见[设备性能实测](device-performance-benchmarks.md)。
