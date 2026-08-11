# Audio speech model acquisition / 音频语音模型获取

This guide records provenance and local verification for Stage 28. It never downloads into the repository, changes the embedded official catalog, or implies redistribution permission. / 本文记录阶段 28 的来源与本地验证；不会把模型下载进仓库、修改内置官方 catalog，也不代表再分发许可。

## Warehouse layout / Warehouse 布局

The development warehouse is `E:\DeploySharp-Models`. The four audited directories are `wav2vec2-base-960h`, `whisper-tiny.en`, `hubert-base-ls960`, and `pyannote-speaker-diarization-3.1`. Keep these directories outside Git and read-only during tests. The exact artifact list and hashes live in the four ModelPack manifests under `eng/models/audio-speech/manifests`. / 开发 warehouse 为 `E:\DeploySharp-Models`，四个审计目录分别为上述四个名称。目录保持在 Git 之外，测试期间按只读处理；精确工件清单和 hash 位于 `eng/models/audio-speech/manifests` 下四个 ModelPack Manifest。

## Reproduce the executable check / 重现可执行检查

Build the package and run the clean consumer:

```powershell
dotnet pack src\DeploySharp.Visual\DeploySharp.Visual.csproj --no-restore -c Release
dotnet pack src\DeploySharp.Visual.OpenCV\DeploySharp.Visual.OpenCV.csproj --no-restore -c Release
dotnet restore tests\clean-consumer\visual-audio-speech-family\VisualAudioSpeechFamilyConsumer.csproj --force-evaluate --no-cache
$env:DEPLOYSHARP_AUDIO_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_AUDIO_MODEL_ROOT = 'E:\DeploySharp-Models\wav2vec2-base-960h'
dotnet run --project tests\clean-consumer\visual-audio-speech-family\VisualAudioSpeechFamilyConsumer.csproj -c Release
```

The expected stable marker is `DEPLOYSHARP_AUDIO_SPEECH_FAMILY_CONSUMER_OK`. Without the model root the consumer emits `DEPLOYSHARP_AUDIO_SPEECH_FAMILY_CONSUMER_SKIP missing-external-file`; this is an intentional environment-gated result, not a hidden pass. / 期望的稳定成功标记为 `DEPLOYSHARP_AUDIO_SPEECH_FAMILY_CONSUMER_OK`。没有模型根目录时输出明确的 skip 标记，不是隐藏通过。

## Blockers / 阻断

Whisper `tiny.en` has a traceable source checkpoint but no audited portable encoder/prefill/KV decoder bundle. HuBERT `base-ls960` has no selected downstream task head. pyannote `speaker-diarization-3.1` is gated and its multi-component pipeline lacks an authorized portable release bundle. These records stay non-downloadable and must not be replaced by an approximate graph. / Whisper、HuBERT、pyannote 的阻断分别是完整三图缺失、任务头缺失以及 gated 多组件 bundle/授权缺失；它们保持不可下载，不以近似图替代。
