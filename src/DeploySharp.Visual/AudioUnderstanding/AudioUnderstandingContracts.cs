using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies an audited audio architecture family. / 标识已审计的音频架构族。</summary>
    public enum AudioUnderstandingFamily
    {
        /// <summary>OpenAI Whisper log-Mel Encoder/Decoder ASR. / OpenAI Whisper log-Mel Encoder/Decoder 语音识别。</summary>
        Whisper = 1,
        /// <summary>Wav2Vec2 convolutional frontend, Transformer, and CTC head. / Wav2Vec2 卷积前端、Transformer 与 CTC Head。</summary>
        Wav2Vec2 = 2,
        /// <summary>HuBERT self-supervised speech representation. / HuBERT 自监督语音表征。</summary>
        Hubert = 3,
        /// <summary>pyannote VAD, segmentation, embedding, and clustering pipeline. / pyannote VAD、分割、Embedding 与聚类流水线。</summary>
        PyannoteSpeakerDiarization = 4
    }

    /// <summary>Identifies one backend-neutral audio task. / 标识一个后端无关音频任务。</summary>
    public enum AudioUnderstandingTask
    {
        /// <summary>Automatic speech recognition. / 自动语音识别。</summary>
        AutomaticSpeechRecognition = 1,
        /// <summary>Greedy CTC speech transcription. / Greedy CTC 语音转录。</summary>
        CtcTranscription = 2,
        /// <summary>Frame-aligned CTC token timestamps. / 帧对齐 CTC Token 时间戳。</summary>
        CtcTimestampAlignment = 3,
        /// <summary>Speech representation extraction without a transcription head. / 不含转录 Head 的语音表征提取。</summary>
        SpeechRepresentation = 4,
        /// <summary>Speaker embedding extraction. / 说话人 Embedding 提取。</summary>
        SpeakerEmbedding = 5,
        /// <summary>Voice activity detection. / 语音活动检测。</summary>
        VoiceActivityDetection = 6,
        /// <summary>Speaker diarization. / 说话人分离。</summary>
        SpeakerDiarization = 7
    }

    /// <summary>Identifies one exact audio artifact role. / 标识一个精确音频工件角色。</summary>
    public enum AudioArtifactRole
    {
        /// <summary>A complete Wav2Vec2/HuBERT CTC graph. / 完整 Wav2Vec2/HuBERT CTC 图。</summary>
        CtcEncoderHead = 1,
        /// <summary>A Whisper log-Mel Encoder graph. / Whisper log-Mel Encoder 图。</summary>
        WhisperEncoder = 2,
        /// <summary>A Whisper Decoder Prefill graph. / Whisper Decoder Prefill 图。</summary>
        WhisperDecoderPrefill = 3,
        /// <summary>A Whisper named Past/Present KV Decode graph. / Whisper 具名 Past/Present KV Decode 图。</summary>
        WhisperDecoderWithPast = 4,
        /// <summary>A representation-only HuBERT Encoder. / 仅表征 HuBERT Encoder。</summary>
        RepresentationEncoder = 5,
        /// <summary>A speaker segmentation or VAD graph. / 说话人分割或 VAD 图。</summary>
        SpeakerSegmentation = 6,
        /// <summary>A speaker embedding graph. / 说话人 Embedding 图。</summary>
        SpeakerEmbedding = 7,
        /// <summary>An upstream source checkpoint that is not an executable backend graph. / 不是可执行后端图的上游源 Checkpoint。</summary>
        SourceCheckpoint = 8
    }

    /// <summary>Identifies accepted PCM encodings before model preprocessing. / 标识模型预处理前可接受的 PCM 编码。</summary>
    public enum AudioPcmEncoding
    {
        /// <summary>Signed little-endian 16-bit PCM. / 有符号小端 16-bit PCM。</summary>
        SignedInt16LittleEndian = 1,
        /// <summary>Little-endian IEEE float32 PCM. / 小端 IEEE float32 PCM。</summary>
        Float32LittleEndian = 2
    }

    /// <summary>Identifies decoded channel layout. / 标识已解码声道布局。</summary>
    public enum AudioChannelLayout
    {
        /// <summary>One mono channel. / 单声道。</summary>
        Mono = 1,
        /// <summary>Two interleaved stereo channels. / 两个交错立体声声道。</summary>
        StereoInterleaved = 2
    }

    /// <summary>Identifies who owns sample-rate conversion. / 标识采样率转换所有者。</summary>
    public enum AudioResamplingOwnership
    {
        /// <summary>The model requires native rate; the processor rejects mismatches. / 模型要求原生采样率，Processor 拒绝不匹配。</summary>
        RequireNativeRate = 1,
        /// <summary>The media processor performs one artifact-bound conversion. / 媒体 Processor 执行一次工件绑定转换。</summary>
        Processor = 2,
        /// <summary>The caller supplies already resampled audio. / 调用方提供已重采样音频。</summary>
        Caller = 3
    }

    /// <summary>Identifies timestamp ownership. / 标识时间戳所有者。</summary>
    public enum AudioTimestampOwnership
    {
        /// <summary>No timestamp claim is available. / 不提供时间戳声明。</summary>
        None = 0,
        /// <summary>CTC frame stride determines token spans. / CTC 帧步长决定 Token 区间。</summary>
        CtcFrameStride = 1,
        /// <summary>Whisper timestamp tokens determine spans. / Whisper 时间戳 Token 决定区间。</summary>
        WhisperTokens = 2,
        /// <summary>The caller supplies and owns segment times. / 调用方提供并拥有片段时间。</summary>
        Caller = 3
    }

    /// <summary>Identifies speaker-segmentation ownership. / 标识说话人分段所有者。</summary>
    public enum AudioSpeakerOwnership
    {
        /// <summary>The profile makes no speaker claim. / Profile 不声明说话人能力。</summary>
        None = 0,
        /// <summary>The caller supplies speaker segments and labels. / 调用方提供说话人片段与标签。</summary>
        Caller = 1,
        /// <summary>The bound pipeline owns VAD, embeddings, clustering, and labels. / 绑定流水线拥有 VAD、Embedding、聚类与标签。</summary>
        ModelPipeline = 2
    }

    /// <summary>Defines one exact named audio tensor contract. / 定义一个精确具名音频张量合同。</summary>
    public sealed class AudioTensorContract
    {
        /// <summary>Initializes a bounded named tensor contract. / 初始化受限具名张量合同。</summary>
        public AudioTensorContract(string name, TensorElementType elementType, TensorShape shapePattern, long maximumElements)
        {
            if (string.IsNullOrWhiteSpace(name)) throw AudioFailure.Contract("A tensor name is required.");
            if (shapePattern == null || shapePattern.Rank == 0) throw AudioFailure.Contract("A tensor shape pattern is required.", tensorName: name);
            if (maximumElements <= 0) throw AudioFailure.Limit("Tensor capacity must be positive.", tensorName: name);
            Name = name.Trim(); ElementType = elementType; ShapePattern = new TensorShape(shapePattern.ToArray()); MaximumElements = maximumElements;
        }

        /// <summary>Gets the exact port name. / 获取精确端口名称。</summary>
        public string Name { get; }
        /// <summary>Gets the exact element type. / 获取精确元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets fixed and dynamic dimensions; negative dimensions are dynamic. / 获取固定与动态维度；负维度表示动态。</summary>
        public TensorShape ShapePattern { get; }
        /// <summary>Gets the maximum accepted element count. / 获取最大允许元素数。</summary>
        public long MaximumElements { get; }

        internal bool Matches(TensorShape actual)
        {
            if (actual == null || actual.Rank != ShapePattern.Rank) return false;
            for (int index = 0; index < actual.Rank; index++) if (ShapePattern[index] > 0 && actual[index] > 0 && ShapePattern[index] != actual[index]) return false;
            return true;
        }
    }

    /// <summary>Binds an exact source/export artifact to named audio ports. / 将精确源文件或导出工件绑定到具名音频端口。</summary>
    public sealed class AudioArtifactContract
    {
        private readonly IReadOnlyList<AudioTensorContract> _inputs;
        private readonly IReadOnlyList<AudioTensorContract> _outputs;

        /// <summary>Initializes an immutable audio artifact contract. / 初始化不可变音频工件合同。</summary>
        public AudioArtifactContract(AudioArtifactRole role, ModelId modelId, string format, string sha256, long fileSize, int opset, IEnumerable<AudioTensorContract>? inputs, IEnumerable<AudioTensorContract>? outputs, string upstreamRevision, string exporter, string license, string sourceUri, bool executable = true, string? sidecarSha256 = null, long? sidecarFileSize = null)
        {
            if (!Enum.IsDefined(typeof(AudioArtifactRole), role) || modelId.IsEmpty) throw AudioFailure.Contract("An audio artifact role and model identifier are required.");
            if (string.IsNullOrWhiteSpace(format) || !AudioUnderstandingHash.IsSha256(sha256) || fileSize <= 0 || opset < 0 || string.IsNullOrWhiteSpace(upstreamRevision) || string.IsNullOrWhiteSpace(exporter) || string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(sourceUri)) throw AudioFailure.Contract("Audio artifact provenance is incomplete.", modelId: modelId);
            var inputList = new List<AudioTensorContract>(inputs ?? new AudioTensorContract[0]);
            var outputList = new List<AudioTensorContract>(outputs ?? new AudioTensorContract[0]);
            if ((sidecarSha256 == null) != !sidecarFileSize.HasValue || (sidecarSha256 != null && (!AudioUnderstandingHash.IsSha256(sidecarSha256) || sidecarFileSize <= 0))) throw AudioFailure.Contract("Audio artifact sidecar metadata is incomplete.", modelId: modelId);
            if (executable && (opset <= 0 || inputList.Count == 0 || outputList.Count == 0)) throw AudioFailure.Contract("Executable audio artifacts require opset and named ports.", modelId: modelId);
            if (!executable && (inputList.Count != 0 || outputList.Count != 0)) throw AudioFailure.Contract("Source-only artifacts cannot claim executable ports.", modelId: modelId);
            if (inputList.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != inputList.Count || outputList.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != outputList.Count) throw AudioFailure.Contract("Audio artifact port names must be unique.", modelId: modelId);
            Role = role; ModelId = modelId; Format = format.Trim().ToLowerInvariant(); Sha256 = sha256.ToLowerInvariant(); FileSize = fileSize; Opset = opset;
            _inputs = new ReadOnlyCollection<AudioTensorContract>(inputList); _outputs = new ReadOnlyCollection<AudioTensorContract>(outputList);
            UpstreamRevision = upstreamRevision.Trim(); Exporter = exporter.Trim(); License = license.Trim(); SourceUri = sourceUri.Trim(); Executable = executable; SidecarSha256 = sidecarSha256?.ToLowerInvariant(); SidecarFileSize = sidecarFileSize;
            Identity = AudioUnderstandingHash.Text(Role + "|" + ModelId.Value + "|" + Format + "|" + Sha256 + "|" + FileSize + "|" + SidecarSha256 + "|" + SidecarFileSize + "|" + Opset + "|" + string.Join(",", _inputs.Select(value => value.Name)) + "|" + string.Join(",", _outputs.Select(value => value.Name)) + "|" + UpstreamRevision + "|" + Exporter + "|" + License);
        }

        /// <summary>Gets the bundle role. / 获取 Bundle 角色。</summary>
        public AudioArtifactRole Role { get; }
        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识符。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the concrete format. / 获取具体格式。</summary>
        public string Format { get; }
        /// <summary>Gets the artifact SHA-256. / 获取工件 SHA-256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets the exact file size. / 获取精确文件大小。</summary>
        public long FileSize { get; }
        /// <summary>Gets an optional required sidecar SHA-256, such as OpenVINO BIN. / 获取可选必需 Sidecar SHA-256，例如 OpenVINO BIN。</summary>
        public string? SidecarSha256 { get; }
        /// <summary>Gets optional required sidecar size. / 获取可选必需 Sidecar 大小。</summary>
        public long? SidecarFileSize { get; }
        /// <summary>Gets the ONNX opset; source-only checkpoints use zero. / 获取 ONNX Opset；仅源 Checkpoint 使用零。</summary>
        public int Opset { get; }
        /// <summary>Gets ordered input contracts. / 获取有序输入合同。</summary>
        public IReadOnlyList<AudioTensorContract> Inputs => _inputs;
        /// <summary>Gets ordered output contracts. / 获取有序输出合同。</summary>
        public IReadOnlyList<AudioTensorContract> Outputs => _outputs;
        /// <summary>Gets the immutable upstream revision. / 获取不可变上游修订。</summary>
        public string UpstreamRevision { get; }
        /// <summary>Gets exporter identity. / 获取导出器 Identity。</summary>
        public string Exporter { get; }
        /// <summary>Gets the artifact license. / 获取工件许可证。</summary>
        public string License { get; }
        /// <summary>Gets the official source URI. / 获取官方来源 URI。</summary>
        public string SourceUri { get; }
        /// <summary>Gets whether a backend may execute the artifact. / 获取后端是否可执行该工件。</summary>
        public bool Executable { get; }
        /// <summary>Gets the artifact contract identity. / 获取工件合同 Identity。</summary>
        public string Identity { get; }

        /// <summary>Creates an application-owned model artifact at an explicit external path. / 在显式外部路径创建应用所有的模型工件。</summary>
        public ModelArtifact CreateArtifact(string location, BackendId preferredBackend) => new ModelArtifact(ModelId, Format, location, Sha256, preferredBackend);
    }

    /// <summary>Defines exactly-once waveform preprocessing. / 定义严格一次的波形预处理。</summary>
    public sealed class AudioProcessorContract
    {
        private readonly IReadOnlyList<AudioPcmEncoding> _encodings;

        /// <summary>Initializes a bounded processor contract. / 初始化受限 Processor 合同。</summary>
        public AudioProcessorContract(string processorId, string sidecarSha256, int sampleRate, int maximumChannels, int maximumSamples, IEnumerable<AudioPcmEncoding> encodings, AudioResamplingOwnership resamplingOwnership, string channelMixIdentity, string normalizationIdentity, bool normalizeWaveform, string featureIdentity)
        {
            if (string.IsNullOrWhiteSpace(processorId) || !AudioUnderstandingHash.IsSha256(sidecarSha256) || sampleRate <= 0 || maximumChannels <= 0 || maximumChannels > 8 || maximumSamples <= 0 || string.IsNullOrWhiteSpace(channelMixIdentity) || string.IsNullOrWhiteSpace(normalizationIdentity) || string.IsNullOrWhiteSpace(featureIdentity) || !Enum.IsDefined(typeof(AudioResamplingOwnership), resamplingOwnership)) throw AudioFailure.Contract("Audio processor metadata is invalid.");
            var copied = new List<AudioPcmEncoding>(encodings ?? throw new ArgumentNullException(nameof(encodings)));
            if (copied.Count == 0 || copied.Any(value => !Enum.IsDefined(typeof(AudioPcmEncoding), value)) || copied.Distinct().Count() != copied.Count) throw AudioFailure.Contract("Audio processor encodings are invalid.");
            ProcessorId = processorId.Trim(); SidecarSha256 = sidecarSha256.ToLowerInvariant(); SampleRate = sampleRate; MaximumChannels = maximumChannels; MaximumSamples = maximumSamples; _encodings = new ReadOnlyCollection<AudioPcmEncoding>(copied); ResamplingOwnership = resamplingOwnership; ChannelMixIdentity = channelMixIdentity.Trim(); NormalizationIdentity = normalizationIdentity.Trim(); NormalizeWaveform = normalizeWaveform; FeatureIdentity = featureIdentity.Trim();
            Identity = AudioUnderstandingHash.Text(ProcessorId + "|" + SidecarSha256 + "|" + SampleRate + "|" + MaximumChannels + "|" + MaximumSamples + "|" + string.Join(",", copied) + "|" + ResamplingOwnership + "|" + ChannelMixIdentity + "|" + NormalizationIdentity + "|" + NormalizeWaveform + "|" + FeatureIdentity);
        }

        /// <summary>Gets processor identity. / 获取 Processor Identity。</summary>
        public string ProcessorId { get; }
        /// <summary>Gets processor sidecar SHA-256. / 获取 Processor Sidecar SHA-256。</summary>
        public string SidecarSha256 { get; }
        /// <summary>Gets required model sample rate. / 获取模型要求的采样率。</summary>
        public int SampleRate { get; }
        /// <summary>Gets maximum decoded source channels. / 获取最大已解码源声道数。</summary>
        public int MaximumChannels { get; }
        /// <summary>Gets maximum post-mix sample count. / 获取混音后的最大样本数。</summary>
        public int MaximumSamples { get; }
        /// <summary>Gets accepted PCM encodings. / 获取接受的 PCM 编码。</summary>
        public IReadOnlyList<AudioPcmEncoding> Encodings => _encodings;
        /// <summary>Gets resampling ownership. / 获取重采样所有权。</summary>
        public AudioResamplingOwnership ResamplingOwnership { get; }
        /// <summary>Gets channel-mix identity. / 获取声道混合 Identity。</summary>
        public string ChannelMixIdentity { get; }
        /// <summary>Gets waveform normalization identity. / 获取波形归一化 Identity。</summary>
        public string NormalizationIdentity { get; }
        /// <summary>Gets whether one mean/variance normalization is applied. / 获取是否应用一次均值/方差归一化。</summary>
        public bool NormalizeWaveform { get; }
        /// <summary>Gets the waveform or log-Mel feature identity. / 获取波形或 log-Mel Feature Identity。</summary>
        public string FeatureIdentity { get; }
        /// <summary>Gets complete processor identity. / 获取完整 Processor Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Defines an artifact-bound CTC or Whisper tokenizer. / 定义工件绑定的 CTC 或 Whisper Tokenizer。</summary>
    public sealed class AudioTokenizerContract
    {
        /// <summary>Initializes an immutable tokenizer contract. / 初始化不可变 Tokenizer 合同。</summary>
        public AudioTokenizerContract(string tokenizerId, string vocabularySha256, int vocabularySize, int blankTokenId, int unknownTokenId, int wordDelimiterTokenId, string wordDelimiterToken, string language, string mode)
        {
            if (string.IsNullOrWhiteSpace(tokenizerId) || !AudioUnderstandingHash.IsSha256(vocabularySha256) || vocabularySize <= 0 || blankTokenId < 0 || blankTokenId >= vocabularySize || unknownTokenId < 0 || unknownTokenId >= vocabularySize || wordDelimiterTokenId < 0 || wordDelimiterTokenId >= vocabularySize || string.IsNullOrEmpty(wordDelimiterToken) || string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(mode)) throw AudioFailure.Contract("Audio tokenizer metadata is invalid.");
            TokenizerId = tokenizerId.Trim(); VocabularySha256 = vocabularySha256.ToLowerInvariant(); VocabularySize = vocabularySize; BlankTokenId = blankTokenId; UnknownTokenId = unknownTokenId; WordDelimiterTokenId = wordDelimiterTokenId; WordDelimiterToken = wordDelimiterToken; Language = language.Trim().ToLowerInvariant(); Mode = mode.Trim().ToLowerInvariant();
            Identity = AudioUnderstandingHash.Text(TokenizerId + "|" + VocabularySha256 + "|" + VocabularySize + "|" + BlankTokenId + "|" + UnknownTokenId + "|" + WordDelimiterTokenId + "|" + WordDelimiterToken + "|" + Language + "|" + Mode);
        }

        /// <summary>Gets tokenizer identity. / 获取 Tokenizer Identity。</summary>
        public string TokenizerId { get; }
        /// <summary>Gets vocabulary SHA-256. / 获取词表 SHA-256。</summary>
        public string VocabularySha256 { get; }
        /// <summary>Gets vocabulary size. / 获取词表大小。</summary>
        public int VocabularySize { get; }
        /// <summary>Gets CTC blank token ID. / 获取 CTC Blank Token ID。</summary>
        public int BlankTokenId { get; }
        /// <summary>Gets unknown token ID. / 获取 Unknown Token ID。</summary>
        public int UnknownTokenId { get; }
        /// <summary>Gets word delimiter token ID. / 获取单词分隔 Token ID。</summary>
        public int WordDelimiterTokenId { get; }
        /// <summary>Gets word delimiter token text. / 获取单词分隔 Token 文本。</summary>
        public string WordDelimiterToken { get; }
        /// <summary>Gets declared language. / 获取声明语言。</summary>
        public string Language { get; }
        /// <summary>Gets decoding mode. / 获取解码模式。</summary>
        public string Mode { get; }
        /// <summary>Gets complete tokenizer identity. / 获取完整 Tokenizer Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Defines frame-to-time conversion without claiming word timestamps. / 定义帧到时间转换且不宣称单词时间戳。</summary>
    public sealed class AudioTimestampContract
    {
        /// <summary>Initializes a timestamp contract. / 初始化时间戳合同。</summary>
        public AudioTimestampContract(string timestampId, AudioTimestampOwnership ownership, int frameStrideSamples, int sampleRate)
        {
            if (string.IsNullOrWhiteSpace(timestampId) || !Enum.IsDefined(typeof(AudioTimestampOwnership), ownership) || sampleRate <= 0 || (ownership == AudioTimestampOwnership.CtcFrameStride && frameStrideSamples <= 0) || (ownership != AudioTimestampOwnership.CtcFrameStride && frameStrideSamples < 0)) throw AudioFailure.Contract("Audio timestamp metadata is invalid.");
            TimestampId = timestampId.Trim(); Ownership = ownership; FrameStrideSamples = frameStrideSamples; SampleRate = sampleRate; SecondsPerFrame = frameStrideSamples == 0 ? 0 : (double)frameStrideSamples / sampleRate; Identity = AudioUnderstandingHash.Text(TimestampId + "|" + Ownership + "|" + FrameStrideSamples + "|" + SampleRate);
        }

        /// <summary>Gets timestamp identity. / 获取时间戳 Identity。</summary>
        public string TimestampId { get; }
        /// <summary>Gets timestamp ownership. / 获取时间戳所有权。</summary>
        public AudioTimestampOwnership Ownership { get; }
        /// <summary>Gets input samples represented by one output frame. / 获取每个输出帧代表的输入样本数。</summary>
        public int FrameStrideSamples { get; }
        /// <summary>Gets sample rate used by frame conversion. / 获取帧转换使用的采样率。</summary>
        public int SampleRate { get; }
        /// <summary>Gets seconds per CTC frame. / 获取每个 CTC 帧的秒数。</summary>
        public double SecondsPerFrame { get; }
        /// <summary>Gets complete timestamp identity. / 获取完整时间戳 Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Defines whether VAD, embeddings, clustering, and labels are available and who owns them. / 定义 VAD、Embedding、聚类和标签是否可用及其所有者。</summary>
    public sealed class AudioSpeakerContract
    {
        /// <summary>Initializes speaker ownership metadata. / 初始化说话人所有权元数据。</summary>
        public AudioSpeakerContract(string speakerId, AudioSpeakerOwnership ownership, bool ownsVad, bool ownsEmbeddings, bool ownsClustering, bool ownsLabels)
        {
            if (string.IsNullOrWhiteSpace(speakerId) || !Enum.IsDefined(typeof(AudioSpeakerOwnership), ownership)) throw AudioFailure.Contract("Audio speaker metadata is invalid.");
            if (ownership == AudioSpeakerOwnership.None && (ownsVad || ownsEmbeddings || ownsClustering || ownsLabels)) throw AudioFailure.Contract("A no-speaker profile cannot own speaker stages.");
            SpeakerId = speakerId.Trim(); Ownership = ownership; OwnsVad = ownsVad; OwnsEmbeddings = ownsEmbeddings; OwnsClustering = ownsClustering; OwnsLabels = ownsLabels; Identity = AudioUnderstandingHash.Text(SpeakerId + "|" + Ownership + "|" + ownsVad + "|" + ownsEmbeddings + "|" + ownsClustering + "|" + ownsLabels);
        }

        /// <summary>Gets speaker contract identity. / 获取说话人合同 Identity。</summary>
        public string SpeakerId { get; }
        /// <summary>Gets speaker ownership. / 获取说话人所有权。</summary>
        public AudioSpeakerOwnership Ownership { get; }
        /// <summary>Gets whether VAD is model-owned. / 获取 VAD 是否由模型拥有。</summary>
        public bool OwnsVad { get; }
        /// <summary>Gets whether embeddings are model-owned. / 获取 Embedding 是否由模型拥有。</summary>
        public bool OwnsEmbeddings { get; }
        /// <summary>Gets whether clustering is model-owned. / 获取聚类是否由模型拥有。</summary>
        public bool OwnsClustering { get; }
        /// <summary>Gets whether speaker labels are model-owned. / 获取说话人标签是否由模型拥有。</summary>
        public bool OwnsLabels { get; }
        /// <summary>Gets complete speaker identity. / 获取完整说话人 Identity。</summary>
        public string Identity { get; }
    }

    /// <summary>Defines an exact Whisper prompt, stop, timestamp-token, and KV schema even when executable exports are blocked. / 即使可执行导出受阻，也定义精确 Whisper Prompt、停止、时间戳 Token 与 KV Schema。</summary>
    public sealed class AudioGenerationContract
    {
        /// <summary>Initializes an immutable greedy-generation contract. / 初始化不可变 Greedy 生成合同。</summary>
        public AudioGenerationContract(string generationId, string tokenizerSha256, string generationConfigSha256, int vocabularySize, int decoderStartTokenId, int eosTokenId, int padTokenId, int noTimestampsTokenId, int timestampBeginTokenId, int? languageTokenId, int? taskTokenId, string language, string task, int maximumMelFrames, int maximumEncoderFrames, int maximumTokens, string kvSchemaId, int kvLayers, int kvHeads, int kvHeadDimension)
        {
            if (string.IsNullOrWhiteSpace(generationId) || !AudioUnderstandingHash.IsSha256(tokenizerSha256) || !AudioUnderstandingHash.IsSha256(generationConfigSha256) || vocabularySize <= 0 || decoderStartTokenId < 0 || decoderStartTokenId >= vocabularySize || eosTokenId < 0 || eosTokenId >= vocabularySize || padTokenId < 0 || padTokenId >= vocabularySize || noTimestampsTokenId < 0 || noTimestampsTokenId >= vocabularySize || timestampBeginTokenId < 0 || timestampBeginTokenId >= vocabularySize || (languageTokenId.HasValue && (languageTokenId.Value < 0 || languageTokenId.Value >= vocabularySize)) || (taskTokenId.HasValue && (taskTokenId.Value < 0 || taskTokenId.Value >= vocabularySize)) || string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(task) || maximumMelFrames <= 0 || maximumEncoderFrames <= 0 || maximumTokens <= 0 || string.IsNullOrWhiteSpace(kvSchemaId) || kvLayers <= 0 || kvHeads <= 0 || kvHeadDimension <= 0) throw AudioFailure.Contract("Audio generation metadata is invalid.");
            GenerationId = generationId.Trim(); TokenizerSha256 = tokenizerSha256.ToLowerInvariant(); GenerationConfigSha256 = generationConfigSha256.ToLowerInvariant(); VocabularySize = vocabularySize; DecoderStartTokenId = decoderStartTokenId; EosTokenId = eosTokenId; PadTokenId = padTokenId; NoTimestampsTokenId = noTimestampsTokenId; TimestampBeginTokenId = timestampBeginTokenId; LanguageTokenId = languageTokenId; TaskTokenId = taskTokenId; Language = language.Trim().ToLowerInvariant(); Task = task.Trim().ToLowerInvariant(); MaximumMelFrames = maximumMelFrames; MaximumEncoderFrames = maximumEncoderFrames; MaximumTokens = maximumTokens; KvSchemaId = kvSchemaId.Trim(); KvLayers = kvLayers; KvHeads = kvHeads; KvHeadDimension = kvHeadDimension;
            Identity = AudioUnderstandingHash.Text(GenerationId + "|" + TokenizerSha256 + "|" + GenerationConfigSha256 + "|" + VocabularySize + "|" + DecoderStartTokenId + "|" + EosTokenId + "|" + PadTokenId + "|" + NoTimestampsTokenId + "|" + TimestampBeginTokenId + "|" + LanguageTokenId + "|" + TaskTokenId + "|" + Language + "|" + Task + "|" + MaximumMelFrames + "|" + MaximumEncoderFrames + "|" + MaximumTokens + "|" + KvSchemaId + "|" + KvLayers + "|" + KvHeads + "|" + KvHeadDimension);
        }

        /// <summary>Gets generation identity. / 获取生成 Identity。</summary>
        public string GenerationId { get; }
        /// <summary>Gets tokenizer JSON SHA-256. / 获取 Tokenizer JSON SHA-256。</summary>
        public string TokenizerSha256 { get; }
        /// <summary>Gets generation-config SHA-256. / 获取生成配置 SHA-256。</summary>
        public string GenerationConfigSha256 { get; }
        /// <summary>Gets vocabulary size. / 获取词表大小。</summary>
        public int VocabularySize { get; }
        /// <summary>Gets Decoder start token ID. / 获取 Decoder 起始 Token ID。</summary>
        public int DecoderStartTokenId { get; }
        /// <summary>Gets EOS token ID. / 获取 EOS Token ID。</summary>
        public int EosTokenId { get; }
        /// <summary>Gets padding token ID. / 获取 Padding Token ID。</summary>
        public int PadTokenId { get; }
        /// <summary>Gets no-timestamps token ID. / 获取 No-timestamps Token ID。</summary>
        public int NoTimestampsTokenId { get; }
        /// <summary>Gets the first timestamp token ID. / 获取首个时间戳 Token ID。</summary>
        public int TimestampBeginTokenId { get; }
        /// <summary>Gets optional language token; English-only checkpoints use null. / 获取可选语言 Token；English-only Checkpoint 使用 null。</summary>
        public int? LanguageTokenId { get; }
        /// <summary>Gets optional task token; this English-only checkpoint uses null. / 获取可选 Task Token；此 English-only Checkpoint 使用 null。</summary>
        public int? TaskTokenId { get; }
        /// <summary>Gets bound language. / 获取绑定语言。</summary>
        public string Language { get; }
        /// <summary>Gets bound task. / 获取绑定 Task。</summary>
        public string Task { get; }
        /// <summary>Gets maximum log-Mel input frames. / 获取最大 log-Mel 输入帧数。</summary>
        public int MaximumMelFrames { get; }
        /// <summary>Gets maximum Encoder frames. / 获取最大 Encoder 帧数。</summary>
        public int MaximumEncoderFrames { get; }
        /// <summary>Gets maximum generated token count. / 获取最大生成 Token 数。</summary>
        public int MaximumTokens { get; }
        /// <summary>Gets named KV schema identity. / 获取具名 KV Schema Identity。</summary>
        public string KvSchemaId { get; }
        /// <summary>Gets KV layer count. / 获取 KV 层数。</summary>
        public int KvLayers { get; }
        /// <summary>Gets KV head count. / 获取 KV Head 数。</summary>
        public int KvHeads { get; }
        /// <summary>Gets KV head dimension. / 获取 KV Head 维度。</summary>
        public int KvHeadDimension { get; }
        /// <summary>Gets complete generation identity. / 获取完整生成 Identity。</summary>
        public string Identity { get; }

        /// <summary>Gets one exact named past KV port. / 获取一个精确具名 Past KV 端口。</summary>
        public string Past(int layer, bool decoder, bool key) { ValidateLayer(layer); return "past_key_values." + layer + "." + (decoder ? "decoder" : "encoder") + "." + (key ? "key" : "value"); }
        /// <summary>Gets one exact named present KV port. / 获取一个精确具名 Present KV 端口。</summary>
        public string Present(int layer, bool decoder, bool key) { ValidateLayer(layer); return "present." + layer + "." + (decoder ? "decoder" : "encoder") + "." + (key ? "key" : "value"); }
        private void ValidateLayer(int layer) { if (layer < 0 || layer >= KvLayers) throw new ArgumentOutOfRangeException(nameof(layer)); }
    }

    /// <summary>Preserves one precise non-executable boundary. / 保留一个精确不可执行边界。</summary>
    public sealed class AudioExecutionBlocker
    {
        private readonly IReadOnlyList<AudioArtifactRole> _missingRoles;

        /// <summary>Initializes a reproducible blocker. / 初始化可复现 Blocker。</summary>
        public AudioExecutionBlocker(string blockerId, string reason, IEnumerable<AudioArtifactRole> missingRoles, string reproduction)
        {
            if (string.IsNullOrWhiteSpace(blockerId) || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(reproduction)) throw AudioFailure.Contract("Audio blocker metadata is incomplete.");
            var roles = new List<AudioArtifactRole>(missingRoles ?? throw new ArgumentNullException(nameof(missingRoles)));
            if (roles.Count == 0 || roles.Any(value => !Enum.IsDefined(typeof(AudioArtifactRole), value))) throw AudioFailure.Contract("Audio blocker roles are invalid.");
            BlockerId = blockerId.Trim(); Reason = reason.Trim(); _missingRoles = new ReadOnlyCollection<AudioArtifactRole>(roles.Distinct().ToList()); Reproduction = reproduction.Trim();
        }

        /// <summary>Gets blocker identity. / 获取 Blocker Identity。</summary>
        public string BlockerId { get; }
        /// <summary>Gets exact reason. / 获取精确原因。</summary>
        public string Reason { get; }
        /// <summary>Gets missing executable roles. / 获取缺失的可执行角色。</summary>
        public IReadOnlyList<AudioArtifactRole> MissingRoles => _missingRoles;
        /// <summary>Gets a reproducible acquisition or export boundary. / 获取可复现获取或导出边界。</summary>
        public string Reproduction { get; }
    }

    /// <summary>Describes one immutable artifact-bound audio family profile. / 描述一个不可变且绑定工件的音频模型族 Profile。</summary>
    public sealed class AudioUnderstandingProfile
    {
        private readonly IReadOnlyList<AudioUnderstandingTask> _tasks;
        private readonly IReadOnlyList<AudioArtifactContract> _artifacts;

        /// <summary>Initializes an audio profile. / 初始化音频 Profile。</summary>
        public AudioUnderstandingProfile(string profileId, AudioUnderstandingFamily family, string modelName, string upstreamRevision, string license, AudioProcessorContract processor, AudioTokenizerContract? tokenizer, AudioTimestampContract timestamps, AudioSpeakerContract speaker, IEnumerable<AudioUnderstandingTask> tasks, IEnumerable<AudioArtifactContract> artifacts, bool executable, AudioExecutionBlocker? blocker = null, AudioGenerationContract? generation = null)
        {
            if (string.IsNullOrWhiteSpace(profileId) || !Enum.IsDefined(typeof(AudioUnderstandingFamily), family) || string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(upstreamRevision) || string.IsNullOrWhiteSpace(license) || processor == null || timestamps == null || speaker == null) throw AudioFailure.Contract("Audio profile metadata is incomplete.", profileId);
            var taskList = new List<AudioUnderstandingTask>(tasks ?? throw new ArgumentNullException(nameof(tasks)));
            var artifactList = new List<AudioArtifactContract>(artifacts ?? throw new ArgumentNullException(nameof(artifacts)));
            if (taskList.Count == 0 || taskList.Any(value => !Enum.IsDefined(typeof(AudioUnderstandingTask), value)) || taskList.Distinct().Count() != taskList.Count) throw AudioFailure.Contract("Audio profile tasks are invalid.", profileId);
            if (artifactList.Count == 0 || artifactList.Select(value => value.Role).Distinct().Count() != artifactList.Count) throw AudioFailure.Contract("Audio artifact roles must be present and unique.", profileId);
            if (executable && (blocker != null || artifactList.Any(value => !value.Executable))) throw AudioFailure.Contract("Executable audio profiles cannot contain source-only artifacts or blockers.", profileId);
            if (!executable && blocker == null) throw AudioFailure.Contract("Non-executable audio profiles require an exact blocker.", profileId);
            if (executable && family == AudioUnderstandingFamily.Wav2Vec2 && (tokenizer == null || artifactList.All(value => value.Role != AudioArtifactRole.CtcEncoderHead))) throw AudioFailure.Contract("Executable Wav2Vec2 requires tokenizer and CTC graph.", profileId);
            if (timestamps.SampleRate != processor.SampleRate) throw AudioFailure.Contract("Timestamp and processor sample rates differ.", profileId);
            if (family == AudioUnderstandingFamily.Whisper && generation == null) throw AudioFailure.Contract("Whisper profiles require an exact generation contract.", profileId);
            if (family != AudioUnderstandingFamily.Whisper && generation != null) throw AudioFailure.Contract("Only Whisper profiles may declare autoregressive generation.", profileId);
            ProfileId = profileId.Trim(); Family = family; ModelName = modelName.Trim(); UpstreamRevision = upstreamRevision.Trim(); License = license.Trim(); Processor = processor; Tokenizer = tokenizer; Timestamps = timestamps; Speaker = speaker; Generation = generation; _tasks = new ReadOnlyCollection<AudioUnderstandingTask>(taskList); _artifacts = new ReadOnlyCollection<AudioArtifactContract>(artifactList); Executable = executable; Blocker = blocker;
            ArtifactIdentity = AudioUnderstandingHash.Text(string.Join("|", artifactList.OrderBy(value => value.Role).Select(value => value.Identity)));
            Identity = AudioUnderstandingHash.Text(ProfileId + "|" + Family + "|" + ModelName + "|" + UpstreamRevision + "|" + License + "|" + Processor.Identity + "|" + (Tokenizer == null ? "none" : Tokenizer.Identity) + "|" + Timestamps.Identity + "|" + Speaker.Identity + "|" + (Generation == null ? "none" : Generation.Identity) + "|" + string.Join(",", taskList) + "|" + ArtifactIdentity + "|" + Executable + "|" + (Blocker == null ? "none" : Blocker.BlockerId));
        }

        /// <summary>Gets stable profile identity. / 获取稳定 Profile Identity。</summary>
        public string ProfileId { get; }
        /// <summary>Gets architecture family. / 获取架构族。</summary>
        public AudioUnderstandingFamily Family { get; }
        /// <summary>Gets official model name. / 获取官方模型名。</summary>
        public string ModelName { get; }
        /// <summary>Gets immutable upstream revision. / 获取不可变上游修订。</summary>
        public string UpstreamRevision { get; }
        /// <summary>Gets model license. / 获取模型许可证。</summary>
        public string License { get; }
        /// <summary>Gets waveform/feature processor contract. / 获取波形/特征 Processor 合同。</summary>
        public AudioProcessorContract Processor { get; }
        /// <summary>Gets optional tokenizer contract. / 获取可选 Tokenizer 合同。</summary>
        public AudioTokenizerContract? Tokenizer { get; }
        /// <summary>Gets timestamp ownership. / 获取时间戳所有权。</summary>
        public AudioTimestampContract Timestamps { get; }
        /// <summary>Gets speaker ownership. / 获取说话人所有权。</summary>
        public AudioSpeakerContract Speaker { get; }
        /// <summary>Gets optional Whisper prompt/KV generation contract. / 获取可选 Whisper Prompt/KV 生成合同。</summary>
        public AudioGenerationContract? Generation { get; }
        /// <summary>Gets supported contract tasks. / 获取支持的合同任务。</summary>
        public IReadOnlyList<AudioUnderstandingTask> Tasks => _tasks;
        /// <summary>Gets exact artifacts or source checkpoints. / 获取精确工件或源 Checkpoint。</summary>
        public IReadOnlyList<AudioArtifactContract> Artifacts => _artifacts;
        /// <summary>Gets whether audited backend execution is available. / 获取是否提供已审计后端执行。</summary>
        public bool Executable { get; }
        /// <summary>Gets exact blocker for a source-only profile. / 获取仅源 Profile 的精确 Blocker。</summary>
        public AudioExecutionBlocker? Blocker { get; }
        /// <summary>Gets aggregate artifact identity. / 获取聚合工件 Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets complete profile identity. / 获取完整 Profile Identity。</summary>
        public string Identity { get; }

        /// <summary>Gets one exact role or fails without guessing. / 获取一个精确角色；不存在时失败且不猜测。</summary>
        public AudioArtifactContract GetArtifact(AudioArtifactRole role)
        {
            AudioArtifactContract? match = _artifacts.SingleOrDefault(value => value.Role == role);
            if (match == null) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "The required audio artifact role is unavailable.", profileId: ProfileId, technicalDetails: role.ToString());
            return match;
        }
    }

    /// <summary>Binds an audio role to one application-selected external artifact. / 将音频角色绑定到应用选择的外部工件。</summary>
    public sealed class AudioArtifactBinding
    {
        /// <summary>Initializes a role binding. / 初始化角色绑定。</summary>
        public AudioArtifactBinding(AudioArtifactRole role, ModelArtifact artifact) { Role = role; Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact)); }
        /// <summary>Gets role. / 获取角色。</summary>
        public AudioArtifactRole Role { get; }
        /// <summary>Gets external artifact. / 获取外部工件。</summary>
        public ModelArtifact Artifact { get; }
    }

    /// <summary>Validates a complete executable audio bundle. / 验证完整可执行音频 Bundle。</summary>
    public sealed class AudioUnderstandingBundle
    {
        private readonly IReadOnlyDictionary<AudioArtifactRole, ModelArtifact> _artifacts;

        /// <summary>Initializes an exact executable bundle. / 初始化精确可执行 Bundle。</summary>
        public AudioUnderstandingBundle(AudioUnderstandingProfile profile, IEnumerable<AudioArtifactBinding> bindings)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (!profile.Executable) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "The audio profile is source-only.", profileId: profile.ProfileId, technicalDetails: profile.Blocker?.Reason);
            var map = new Dictionary<AudioArtifactRole, ModelArtifact>();
            foreach (AudioArtifactBinding binding in bindings ?? throw new ArgumentNullException(nameof(bindings)))
            {
                if (binding == null || map.ContainsKey(binding.Role)) throw AudioFailure.Contract("Audio bundle roles must be unique.", profile.ProfileId);
                AudioArtifactContract contract = profile.GetArtifact(binding.Role);
                if (contract.ModelId != binding.Artifact.ModelId || !string.Equals(contract.Format, binding.Artifact.Format, StringComparison.OrdinalIgnoreCase) || !string.Equals(contract.Sha256, binding.Artifact.Sha256, StringComparison.OrdinalIgnoreCase)) throw new VisualException(VisualErrorCodes.AudioIdentityMismatch, "Audio artifact binding differs from the profile.", profileId: profile.ProfileId, modelId: binding.Artifact.ModelId);
                map.Add(binding.Role, binding.Artifact);
            }
            foreach (AudioArtifactContract contract in profile.Artifacts.Where(value => value.Executable)) if (!map.ContainsKey(contract.Role)) throw AudioFailure.Contract("An executable audio artifact role is missing.", profile.ProfileId, modelId: contract.ModelId);
            _artifacts = new ReadOnlyDictionary<AudioArtifactRole, ModelArtifact>(map); Identity = AudioUnderstandingHash.Text(profile.Identity + "|" + string.Join("|", map.OrderBy(value => value.Key).Select(value => value.Value.Location)));
        }

        /// <summary>Gets profile. / 获取 Profile。</summary>
        public AudioUnderstandingProfile Profile { get; }
        /// <summary>Gets bundle identity. / 获取 Bundle Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets one bound artifact. / 获取一个绑定工件。</summary>
        public ModelArtifact GetArtifact(AudioArtifactRole role) { if (!_artifacts.TryGetValue(role, out ModelArtifact? artifact)) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "Audio bundle role is unavailable.", profileId: Profile.ProfileId, technicalDetails: role.ToString()); return artifact; }
    }

    internal static class AudioUnderstandingHash
    {
        internal static bool IsSha256(string? value) => value != null && value.Length == 64 && value.All(current => (current >= '0' && current <= '9') || (current >= 'a' && current <= 'f') || (current >= 'A' && current <= 'F'));
        internal static string Text(string value)
        {
            using (SHA256 hash = SHA256.Create()) return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2")));
        }
        internal static string Floats(float[] values)
        {
            var bytes = new byte[checked(values.Length * sizeof(float))]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using (SHA256 hash = SHA256.Create()) return string.Concat(hash.ComputeHash(bytes).Select(item => item.ToString("x2")));
        }
    }

    internal static class AudioFailure
    {
        internal static VisualException Contract(string message, string? profileId = null, string? tensorName = null, ModelId? modelId = null) => new VisualException(VisualErrorCodes.AudioContractInvalid, message, profileId: profileId, tensorName: tensorName, modelId: modelId);
        internal static VisualException Limit(string message, string? profileId = null, string? tensorName = null) => new VisualException(VisualErrorCodes.AudioLimitExceeded, message, profileId: profileId, tensorName: tensorName);
    }
}
