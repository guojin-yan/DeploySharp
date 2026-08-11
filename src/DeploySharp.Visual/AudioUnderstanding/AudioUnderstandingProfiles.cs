using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Creates pinned Wav2Vec2, Whisper, HuBERT, and pyannote profiles without inferring contracts from filenames. / 创建固定 Wav2Vec2、Whisper、HuBERT 与 pyannote Profile，且不从文件名推断合同。</summary>
    public static class AudioUnderstandingProfiles
    {
        private const string Wav2Vec2Revision = "22aad52d435eb6dbaf354bdad9b0da84ce7d6156";
        private const string Wav2Vec2Source = "https://huggingface.co/facebook/wav2vec2-base-960h";
        private const string WhisperRevision = "87c7102498dcde7456f24cfd30239ca606ed9063";
        private const string WhisperSource = "https://huggingface.co/openai/whisper-tiny.en";
        private const string HubertRevision = "dba3bb02fda4248b6e082697eee756de8fe8aa8a";
        private const string HubertSource = "https://huggingface.co/facebook/hubert-base-ls960";
        private const string PyannoteRevision = "84fd25912480287da0247647c3d2b4853cb3ee5d";
        private const string PyannoteSource = "https://huggingface.co/pyannote/speaker-diarization-3.1";

        /// <summary>Creates the audited FP32 ONNX Wav2Vec2 CTC profile for ORT CPU. / 创建供 ORT CPU 使用的已审计 FP32 ONNX Wav2Vec2 CTC Profile。</summary>
        public static AudioUnderstandingProfile CreateWav2Vec2Base960hOnnx()
        {
            return CreateWav2Vec2(
                "audio.wav2vec2.base-960h.onnx",
                new AudioArtifactContract(
                    AudioArtifactRole.CtcEncoderHead,
                    new ModelId("external/audio/wav2vec2-base-960h/ctc/onnx"),
                    "onnx",
                    "7cdbfb05e27e5f7cf6149bd13ff657655347afee5dc41df40174af7ad13483fd",
                    377814575,
                    17,
                    CtcInputs(),
                    CtcOutputs(),
                    Wav2Vec2Revision,
                    "torch-2.9.1+cpu legacy torch.onnx.export dynamo=false constant-folding=true",
                    "Apache-2.0; external artifact redistribution not approved",
                    Wav2Vec2Source));
        }

        /// <summary>Creates the audited FP32 OpenVINO XML/BIN Wav2Vec2 CTC profile. / 创建已审计 FP32 OpenVINO XML/BIN Wav2Vec2 CTC Profile。</summary>
        public static AudioUnderstandingProfile CreateWav2Vec2Base960hOpenVino()
        {
            return CreateWav2Vec2(
                "audio.wav2vec2.base-960h.openvino-fp32",
                new AudioArtifactContract(
                    AudioArtifactRole.CtcEncoderHead,
                    new ModelId("external/audio/wav2vec2-base-960h/ctc/openvino"),
                    "openvino-ir",
                    "1d2b1cdefff94b21020e639ebfe3322fa64cbabe31a918acb5655d5c74668098",
                    735783,
                    17,
                    CtcInputs(),
                    CtcOutputs(),
                    Wav2Vec2Revision,
                    "OpenVINO 2026.2.1 read_model/save_model FP32 from audited ONNX",
                    "Apache-2.0; external artifact redistribution not approved",
                    Wav2Vec2Source,
                    sidecarSha256: "b5f086a228f79416658ff7d4ac2ab897183e3719ba994453506b8aef408ec803",
                    sidecarFileSize: 377585984));
        }

        /// <summary>Creates the source-only Whisper tiny.en contract and its exact three-graph blocker. / 创建仅源 Whisper tiny.en 合同及其精确三图 Blocker。</summary>
        public static AudioUnderstandingProfile CreateWhisperTinyEnglishContract()
        {
            var processor = new AudioProcessorContract(
                "openai-whisper-feature-extractor-tiny.en-87c7102",
                "9b5cd03a36fbb8a627c64d98a5b5b126ead95a77720723944487311f0110b666",
                16000, 2, 480000,
                new[] { AudioPcmEncoding.SignedInt16LittleEndian, AudioPcmEncoding.Float32LittleEndian },
                AudioResamplingOwnership.RequireNativeRate,
                "arithmetic-stereo-mean-once-v1",
                "whisper-log-mel-dynamic-range-v1",
                false,
                "whisper-log-mel-80-nfft400-hop160-frames3000-v1");
            var generation = new AudioGenerationContract(
                "whisper-tiny.en-greedy-no-timestamps-v1",
                "5eb60cec1e77aeeb6869a2bb5a8e01a84c3fe5d072d75369343021fe6f5310d0",
                "38744c19d5cede6ff4dab5079c6d6ddc02ca726960bbef208fb602ad5a030eab",
                51864, 50257, 50256, 50256, 50362, 50363, null, null,
                "en", "transcribe", 3000, 1500, 448,
                "whisper-optimum-named-past-present-4x6x64-v1", 4, 6, 64);
            var source = SourceArtifact(AudioArtifactRole.SourceCheckpoint, "external/audio/whisper-tiny.en/source", "safetensors", "db59695928ded6043adaef491a53ef4e12da9611184d77c53baa691a60b958ad", 151060136, WhisperRevision, WhisperSource);
            var blocker = new AudioExecutionBlocker(
                "stage28-whisper-three-graph-export-not-admitted",
                "The official source checkpoint is acquired, but this stage has no verified Encoder, Decoder Prefill, and named Past/Present KV Decode export with per-token parity; no Whisper execution is claimed.",
                new[] { AudioArtifactRole.WhisperEncoder, AudioArtifactRole.WhisperDecoderPrefill, AudioArtifactRole.WhisperDecoderWithPast },
                "Export the pinned source into three isolated graphs at opset 17, preserve forced no-timestamps token 50362 and 4x6x64 named KV, then compare every greedy token and stop condition before changing Executable.");
            return new AudioUnderstandingProfile(
                "audio.whisper.tiny.en.source-contract", AudioUnderstandingFamily.Whisper, "openai/whisper-tiny.en", WhisperRevision, "Apache-2.0",
                processor, null, new AudioTimestampContract("whisper-timestamp-token-0.02s-v1", AudioTimestampOwnership.WhisperTokens, 320, 16000),
                NoSpeaker("whisper-no-speaker"), new[] { AudioUnderstandingTask.AutomaticSpeechRecognition }, new[] { source }, false, blocker, generation);
        }

        /// <summary>Creates the source-only HuBERT representation contract and explicit missing-export blocker. / 创建仅源 HuBERT 表征合同及显式缺失导出 Blocker。</summary>
        public static AudioUnderstandingProfile CreateHubertBaseLs960Contract()
        {
            var processor = new AudioProcessorContract(
                "facebook-hubert-feature-extractor-dba3bb0",
                "4a93853b74278b7c769d07f5a861e5d12ceb5db2bced5620d335f87238cb9e86",
                16000, 2, 480000,
                new[] { AudioPcmEncoding.SignedInt16LittleEndian, AudioPcmEncoding.Float32LittleEndian },
                AudioResamplingOwnership.RequireNativeRate,
                "arithmetic-stereo-mean-once-v1",
                "wav2vec2-zero-mean-unit-variance-epsilon-1e-7-v1",
                true,
                "hubert-convolutional-waveform-representation-v1");
            var source = SourceArtifact(AudioArtifactRole.SourceCheckpoint, "external/audio/hubert-base-ls960/source", "pytorch", "062249fffb353eab67547a2fbc129f7c31a2f459faf641b19e8fb007cc5c48ad", 377569754, HubertRevision, HubertSource);
            var blocker = new AudioExecutionBlocker(
                "stage28-hubert-representation-export-not-admitted",
                "The pinned checkpoint is a feature-extraction model without an official CTC vocabulary/head, and no audited dynamic-frame representation export was admitted in this stage.",
                new[] { AudioArtifactRole.RepresentationEncoder },
                "Export HubertModel hidden states at opset 17 in an isolated directory and verify layer/normalization identity; do not label it ASR unless an independently licensed CTC head and vocabulary are bound.");
            return new AudioUnderstandingProfile(
                "audio.hubert.base-ls960.source-contract", AudioUnderstandingFamily.Hubert, "facebook/hubert-base-ls960", HubertRevision, "Apache-2.0",
                processor, null, new AudioTimestampContract("hubert-no-timestamp-claim", AudioTimestampOwnership.None, 0, 16000),
                NoSpeaker("hubert-no-speaker"), new[] { AudioUnderstandingTask.SpeechRepresentation }, new[] { source }, false, blocker);
        }

        /// <summary>Creates the gated pyannote diarization contract without bypassing model conditions. / 创建受门控 pyannote 说话人分离合同且不绕过模型条件。</summary>
        public static AudioUnderstandingProfile CreatePyannoteSpeakerDiarization31Contract()
        {
            var processor = new AudioProcessorContract(
                "pyannote-speaker-diarization-3.1-model-card",
                "8af30a33044bf34a51bbef3d87695c204894d172e9aae6f018417ce0066f0cb5",
                16000, 2, 172800000,
                new[] { AudioPcmEncoding.SignedInt16LittleEndian, AudioPcmEncoding.Float32LittleEndian },
                AudioResamplingOwnership.Processor,
                "pyannote-stereo-arithmetic-mean-model-card",
                "pyannote-pipeline-gated-unresolved",
                false,
                "pyannote-segmentation-embedding-clustering-gated-v1");
            var source = SourceArtifact(AudioArtifactRole.SourceCheckpoint, "external/audio/pyannote-speaker-diarization-3.1/model-card", "model-card", "8af30a33044bf34a51bbef3d87695c204894d172e9aae6f018417ce0066f0cb5", 10985, PyannoteRevision, PyannoteSource);
            var blocker = new AudioExecutionBlocker(
                "stage28-pyannote-gated-submodels",
                "The pipeline and pyannote/segmentation-3.0 require user-condition acceptance and authentication; unauthenticated config and requirements requests return access-restricted responses, so no submodel, VAD, embedding, clustering, or speaker-label execution is claimed.",
                new[] { AudioArtifactRole.SpeakerSegmentation, AudioArtifactRole.SpeakerEmbedding },
                "After the caller accepts both model conditions and supplies authorization, acquire every immutable referenced submodel and license, then export and verify segmentation, embedding, clustering, overlap, and labels as separate owned stages.");
            return new AudioUnderstandingProfile(
                "audio.pyannote.speaker-diarization-3.1.gated-contract", AudioUnderstandingFamily.PyannoteSpeakerDiarization, "pyannote/speaker-diarization-3.1", PyannoteRevision, "MIT; gated user conditions",
                processor, null, new AudioTimestampContract("pyannote-segment-timeline-model-owned", AudioTimestampOwnership.None, 0, 16000),
                new AudioSpeakerContract("pyannote-vad-embedding-clustering-labels-model-owned", AudioSpeakerOwnership.ModelPipeline, true, true, true, true),
                new[] { AudioUnderstandingTask.VoiceActivityDetection, AudioUnderstandingTask.SpeakerEmbedding, AudioUnderstandingTask.SpeakerDiarization }, new[] { source }, false, blocker);
        }

        private static AudioUnderstandingProfile CreateWav2Vec2(string profileId, AudioArtifactContract artifact)
        {
            var processor = new AudioProcessorContract(
                "facebook-wav2vec2-base-960h-feature-extractor-22aad52",
                "b225d617c025463b9e157e06afea8b90dc7078fc70b013c533328423e0486b4a",
                16000, 2, 480000,
                new[] { AudioPcmEncoding.SignedInt16LittleEndian, AudioPcmEncoding.Float32LittleEndian },
                AudioResamplingOwnership.RequireNativeRate,
                "arithmetic-stereo-mean-once-v1",
                "wav2vec2-zero-mean-unit-variance-epsilon-1e-7-v1",
                true,
                "wav2vec2-raw-normalized-waveform-v1");
            var tokenizer = new AudioTokenizerContract(
                "facebook-wav2vec2-base-960h-vocab-22aad52",
                "19727f8944fe6459fc3f240ae2c198395b740f6a029bd23e06656266b83bcf64",
                32, 0, 3, 4, "|", "en", "greedy-ctc-collapse-repeats-lowest-index-tie");
            return new AudioUnderstandingProfile(
                profileId, AudioUnderstandingFamily.Wav2Vec2, "facebook/wav2vec2-base-960h", Wav2Vec2Revision, "Apache-2.0",
                processor, tokenizer, new AudioTimestampContract("wav2vec2-inputs-to-logits-ratio-320-v1", AudioTimestampOwnership.CtcFrameStride, 320, 16000),
                NoSpeaker("wav2vec2-no-speaker"),
                new[] { AudioUnderstandingTask.AutomaticSpeechRecognition, AudioUnderstandingTask.CtcTranscription, AudioUnderstandingTask.CtcTimestampAlignment },
                new[] { artifact }, true);
        }

        private static AudioSpeakerContract NoSpeaker(string id) => new AudioSpeakerContract(id, AudioSpeakerOwnership.None, false, false, false, false);

        private static AudioArtifactContract SourceArtifact(AudioArtifactRole role, string modelId, string format, string sha, long size, string revision, string source)
            => new AudioArtifactContract(role, new ModelId(modelId), format, sha, size, 0, null, null, revision, "official-source-only", "external source license; redistribution not approved", source, executable: false);

        private static IEnumerable<AudioTensorContract> CtcInputs()
            => new[] { new AudioTensorContract("input_values", TensorElementType.Float32, new TensorShape(1, -1), 480000) };

        private static IEnumerable<AudioTensorContract> CtcOutputs()
            => new[] { new AudioTensorContract("logits", TensorElementType.Float32, new TensorShape(1, -1, 32), 48000) };
    }
}
