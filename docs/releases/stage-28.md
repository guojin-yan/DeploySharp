# Stage 28: Audio speech family / 阶段 28：音频语音模型族

Stage 28 adds artifact-bound audio contracts and a real Wav2Vec2 base-960h CTC path through DeploySharp ONNX Runtime CPU, OpenVINO FP32 IR CPU, OpenCV input preparation, ModelFactory identity, four ModelPack manifests, and a package-only clean consumer. / 阶段 28 增加工件绑定的音频合同，并通过 DeploySharp ORT CPU、OpenVINO FP32 IR CPU、OpenCV 输入准备、ModelFactory Identity、四份 Manifest 和纯包 Consumer 闭合 Wav2Vec2 base-960h CTC 路径。

The exact Wav2Vec2 revision is `22aad52d435eb6dbaf354bdad9b0da84ce7d6156`; the ONNX graph and OpenVINO XML/BIN have recorded SHA256 values and agree with the official/Python evidence on the same sample. The consumer emits a stable skip marker without external files and `DEPLOYSHARP_AUDIO_SPEECH_FAMILY_CONSUMER_OK` with the authorized local model root. / 精确 Wav2Vec2 revision 如上；ONNX 与 OpenVINO XML/BIN 均记录 SHA256，并与同一音频样本的官方/Python 证据一致。没有外部文件时 Consumer 输出稳定 skip，配置授权模型根目录时输出成功标记。

Whisper tiny.en, HuBERT base-ls960, and pyannote speaker-diarization 3.1 are source-contract blockers for missing complete portable graphs, task heads, or access/redistribution terms. All four records remain External, `redistributionAllowed:false`, `uploaded:false`, and `downloadable:false`; the official catalog remains empty. / Whisper、HuBERT、pyannote 因完整可移植图、任务头或访问/再分发条款缺失保持 source-contract blocker。四条记录均为 External 且不可上传/下载，官方 catalog 为空。

No existing public type was removed. The relevant packages remain `JYPPX.DeploySharp.Visual` and `JYPPX.DeploySharp.Visual.OpenCV`; no model-specific package or bundled weight was introduced. / 未删除既有公共类型；相关能力仍位于现有两个包中，没有模型专用包或内置权重。
