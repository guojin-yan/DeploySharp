using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual.Models.Yolo;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines an immutable artifact-bound open-vocabulary detector contract. / 定义不可变且绑定工件的开放词汇检测器合同。</summary>
    public sealed class OpenVocabularyDetectionProfile
    {
        private readonly IReadOnlyList<OpenVocabularyArtifactContract> _artifacts;
        private readonly IReadOnlyList<OpenVocabularyTokenizationEntry> _tokenization;

        /// <summary>Initializes an exact executable or blocker-only family profile. / 初始化精确可执行或仅 Blocker 的模型族 Profile。</summary>
        public OpenVocabularyDetectionProfile(
            string profileId,
            OpenVocabularyModelFamily family,
            string version,
            OpenVocabularyPromptMode promptMode,
            IEnumerable<OpenVocabularyArtifactContract> artifacts,
            VocabularyPrompt? vocabulary,
            IEnumerable<OpenVocabularyTokenizationEntry>? tokenization,
            OpenVocabularyEmbeddingIdentity? embeddingIdentity,
            YoloDetectionProfile? detectorProfile,
            int maximumPromptEntries = 256,
            int maximumTokensPerEntry = 77,
            int maximumDetections = 300,
            string preprocessingVersion = "artifact-defined",
            string postprocessingVersion = "artifact-defined",
            string? blocker = null)
        {
            ProfileId = VisualGuard.Identifier(profileId, nameof(profileId));
            if (!Enum.IsDefined(typeof(OpenVocabularyModelFamily), family) || !Enum.IsDefined(typeof(OpenVocabularyPromptMode), promptMode)) throw Invalid("The family or prompt mode is invalid.");
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(preprocessingVersion) || string.IsNullOrWhiteSpace(postprocessingVersion)) throw Invalid("Versioned family contracts are required.");
            if (maximumPromptEntries <= 0 || maximumTokensPerEntry <= 0 || maximumDetections <= 0) throw new VisualException(VisualErrorCodes.OpenVocabularyLimitExceeded, "Open-vocabulary capacities must be positive.", profileId: ProfileId);
            Family = family;
            Version = version.Trim();
            PromptMode = promptMode;
            _artifacts = CopyArtifacts(artifacts);
            Vocabulary = vocabulary;
            _tokenization = CopyTokenization(tokenization, vocabulary, maximumTokensPerEntry);
            EmbeddingIdentity = embeddingIdentity;
            DetectorProfile = detectorProfile;
            MaximumPromptEntries = maximumPromptEntries;
            MaximumTokensPerEntry = maximumTokensPerEntry;
            MaximumDetections = maximumDetections;
            PreprocessingVersion = preprocessingVersion.Trim();
            PostprocessingVersion = postprocessingVersion.Trim();
            Blocker = string.IsNullOrWhiteSpace(blocker) ? null : blocker!.Trim();
            bool executable = detectorProfile != null;

            if (vocabulary != null && vocabulary.Entries.Count > maximumPromptEntries) throw new VisualException(VisualErrorCodes.OpenVocabularyLimitExceeded, "The profile vocabulary exceeds its capacity.", profileId: ProfileId);
            if (promptMode == OpenVocabularyPromptMode.FixedVocabulary && executable)
            {
                if (vocabulary == null || embeddingIdentity == null) throw Invalid("A fixed-vocabulary profile requires vocabulary and embedding identity.");
                embeddingIdentity.Validate(vocabulary);
            }
            else if (vocabulary != null && embeddingIdentity != null) embeddingIdentity.Validate(vocabulary);
            if (executable)
            {
                OpenVocabularyArtifactContract detector = GetArtifact(OpenVocabularyArtifactRole.Detector);
                if (!detector.Executable || detector.ModelId != detectorProfile!.VisualProfile.ModelId || !string.Equals(detector.Sha256, detectorProfile.ArtifactSha256, StringComparison.Ordinal)) throw Invalid("The executable detector profile does not match its artifact contract.");
                if (Blocker != null) throw Invalid("An executable profile cannot carry a complete-pipeline blocker.");
            }
            else if (Blocker == null) throw Invalid("A non-executable family profile requires a reproducible blocker.");
        }

        /// <summary>Gets stable profile ID. / 获取稳定 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets upstream family. / 获取上游模型族。</summary>
        public OpenVocabularyModelFamily Family { get; }
        /// <summary>Gets exact family/export version. / 获取精确模型族/导出版本。</summary>
        public string Version { get; }
        /// <summary>Gets runtime prompt mode. / 获取运行时提示模式。</summary>
        public OpenVocabularyPromptMode PromptMode { get; }
        /// <summary>Gets all source and runtime artifacts. / 获取全部源工件与运行时工件。</summary>
        public IReadOnlyList<OpenVocabularyArtifactContract> Artifacts => _artifacts;
        /// <summary>Gets exact fixed or default vocabulary, when present. / 获取精确固定或默认词汇（如果有）。</summary>
        public VocabularyPrompt? Vocabulary { get; }
        /// <summary>Gets official tokenizer evidence in vocabulary order. / 获取按词汇顺序排列的官方 Tokenizer 证据。</summary>
        public IReadOnlyList<OpenVocabularyTokenizationEntry> Tokenization => _tokenization;
        /// <summary>Gets fixed prompt embedding identity, when applicable. / 获取固定提示 Embedding Identity（如果适用）。</summary>
        public OpenVocabularyEmbeddingIdentity? EmbeddingIdentity { get; }
        /// <summary>Gets executable detector profile or null for blocker-only contracts. / 获取可执行检测器 Profile；仅 Blocker 合同时为 null。</summary>
        public YoloDetectionProfile? DetectorProfile { get; }
        /// <summary>Gets whether this exact profile has a complete native detector path. / 获取此精确 Profile 是否具有完整 native 检测器路径。</summary>
        public bool Executable => DetectorProfile != null;
        /// <summary>Gets maximum vocabulary entries. / 获取最大词汇条目数。</summary>
        public int MaximumPromptEntries { get; }
        /// <summary>Gets maximum tokens per entry. / 获取每条目的最大 Token 数。</summary>
        public int MaximumTokensPerEntry { get; }
        /// <summary>Gets maximum decoded detections. / 获取最大解码检测数。</summary>
        public int MaximumDetections { get; }
        /// <summary>Gets preprocessing version. / 获取前处理版本。</summary>
        public string PreprocessingVersion { get; }
        /// <summary>Gets postprocessing version and NMS ownership. / 获取后处理版本与 NMS 所有权。</summary>
        public string PostprocessingVersion { get; }
        /// <summary>Gets complete-pipeline blocker. / 获取完整 Pipeline Blocker。</summary>
        public string? Blocker { get; }
        /// <summary>Gets executable Visual profile. / 获取可执行 Visual Profile。</summary>
        public VisualModelProfile VisualProfile => DetectorProfile?.VisualProfile ?? throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "This open-vocabulary profile is contract-only: " + Blocker + ".", profileId: ProfileId);

        /// <summary>Gets one required artifact by role. / 按角色获取一个必需工件。</summary>
        public OpenVocabularyArtifactContract GetArtifact(OpenVocabularyArtifactRole role)
        {
            OpenVocabularyArtifactContract? value = _artifacts.FirstOrDefault(item => item.Role == role);
            return value ?? throw Invalid("The required artifact role is missing: " + role + ".");
        }

        /// <summary>Creates the exact detector artifact; blocker-only profiles are rejected. / 创建精确检测器工件；拒绝仅 Blocker Profile。</summary>
        public ModelArtifact CreateArtifact(string path, BackendId? preferredBackend = null)
        {
            if (!Executable) throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "This open-vocabulary profile is contract-only: " + Blocker + ".", profileId: ProfileId);
            return GetArtifact(OpenVocabularyArtifactRole.Detector).CreateArtifact(path, preferredBackend);
        }

        private static IReadOnlyList<OpenVocabularyArtifactContract> CopyArtifacts(IEnumerable<OpenVocabularyArtifactContract> artifacts)
        {
            if (artifacts == null) throw new ArgumentNullException(nameof(artifacts));
            var values = new List<OpenVocabularyArtifactContract>();
            var roles = new HashSet<OpenVocabularyArtifactRole>();
            foreach (OpenVocabularyArtifactContract artifact in artifacts)
            {
                if (artifact == null || !roles.Add(artifact.Role)) throw Invalid("Artifact roles must be non-null and unique.");
                values.Add(artifact);
            }
            if (values.Count == 0) throw Invalid("At least one source or runtime artifact is required.");
            return new ReadOnlyCollection<OpenVocabularyArtifactContract>(values);
        }

        private static IReadOnlyList<OpenVocabularyTokenizationEntry> CopyTokenization(IEnumerable<OpenVocabularyTokenizationEntry>? tokenization, VocabularyPrompt? vocabulary, int maximumTokens)
        {
            var values = tokenization == null ? new List<OpenVocabularyTokenizationEntry>() : new List<OpenVocabularyTokenizationEntry>(tokenization);
            var indices = new HashSet<int>();
            foreach (OpenVocabularyTokenizationEntry entry in values)
            {
                if (entry == null || !indices.Add(entry.VocabularyIndex)) throw Invalid("Tokenization entries must be non-null and unique by vocabulary index.");
                if (entry.TokenIds.Count > maximumTokens) throw new VisualException(VisualErrorCodes.OpenVocabularyLimitExceeded, "A tokenized entry exceeds profile capacity.");
                if (vocabulary == null || entry.VocabularyIndex >= vocabulary.Entries.Count) throw Invalid("Tokenization evidence references an absent vocabulary entry.");
            }
            return new ReadOnlyCollection<OpenVocabularyTokenizationEntry>(values.OrderBy(value => value.VocabularyIndex).ToList());
        }

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.OpenVocabularyContractInvalid, message);
    }

    /// <summary>Associates one canonical detection with exact phrase and token provenance. / 将一个规范检测与精确短语及 Token 来源关联。</summary>
    public sealed class OpenVocabularyDetectionMatch
    {
        private readonly IReadOnlyList<int> _tokenIds;

        internal OpenVocabularyDetectionMatch(int detectionIndex, int vocabularyIndex, string phrase, IEnumerable<int> tokenIds)
        {
            DetectionIndex = detectionIndex;
            VocabularyIndex = vocabularyIndex;
            Phrase = phrase;
            _tokenIds = new ReadOnlyCollection<int>(new List<int>(tokenIds));
        }

        /// <summary>Gets index in the canonical detection list. / 获取规范检测列表中的索引。</summary>
        public int DetectionIndex { get; }
        /// <summary>Gets exported vocabulary/class index. / 获取导出的词汇/类别索引。</summary>
        public int VocabularyIndex { get; }
        /// <summary>Gets exact phrase text. / 获取精确短语文本。</summary>
        public string Phrase { get; }
        /// <summary>Gets official token IDs when audited. / 获取已审计的官方 Token ID。</summary>
        public IReadOnlyList<int> TokenIds => _tokenIds;
    }

    /// <summary>Extends the existing canonical detection result with owned open-vocabulary provenance. / 使用自有开放词汇来源扩展现有规范检测结果。</summary>
    public sealed class OpenVocabularyDetectionResult
    {
        private readonly IReadOnlyList<OpenVocabularyDetectionMatch> _matches;

        internal OpenVocabularyDetectionResult(DetectionResult detections, IEnumerable<OpenVocabularyDetectionMatch> matches, string profileId, string vocabularySha256, OpenVocabularyPromptMode promptMode)
        {
            Detections = detections ?? throw new ArgumentNullException(nameof(detections));
            var values = new List<OpenVocabularyDetectionMatch>(matches ?? throw new ArgumentNullException(nameof(matches)));
            if (values.Count != detections.Detections.Count) throw new ArgumentException("Every detection requires exactly one phrase match.", nameof(matches));
            _matches = new ReadOnlyCollection<OpenVocabularyDetectionMatch>(values);
            ProfileId = profileId;
            VocabularySha256 = vocabularySha256;
            PromptMode = promptMode;
        }

        /// <summary>Gets reusable canonical boxes, labels, scores, ordering, and source geometry. / 获取可复用的规范框、标签、分数、排序与源图几何。</summary>
        public DetectionResult Detections { get; }
        /// <summary>Gets phrase/token provenance in canonical detection order. / 获取按规范检测顺序排列的短语/Token 来源。</summary>
        public IReadOnlyList<OpenVocabularyDetectionMatch> Matches => _matches;
        /// <summary>Gets profile identity. / 获取 Profile Identity。</summary>
        public string ProfileId { get; }
        /// <summary>Gets exact ordered vocabulary identity. / 获取精确有序词汇 Identity。</summary>
        public string VocabularySha256 { get; }
        /// <summary>Gets runtime prompt mode. / 获取运行时提示模式。</summary>
        public OpenVocabularyPromptMode PromptMode { get; }
    }

    internal sealed class OpenVocabularyDetectionDecoder : IVisualDecoder
    {
        private readonly IVisualDecoder _inner;
        private readonly string _profileId;
        private readonly VocabularyPrompt _vocabulary;
        private readonly IReadOnlyDictionary<int, OpenVocabularyTokenizationEntry> _tokenization;
        private readonly OpenVocabularyPromptMode _promptMode;

        internal OpenVocabularyDetectionDecoder(IVisualDecoder inner, string profileId, VocabularyPrompt vocabulary, IEnumerable<OpenVocabularyTokenizationEntry> tokenization, OpenVocabularyPromptMode promptMode)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _profileId = profileId;
            _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
            _tokenization = tokenization.ToDictionary(value => value.VocabularyIndex);
            _promptMode = promptMode;
        }

        public VisualTaskId Task => VisualTaskId.ObjectDetection;

        public object Decode(VisualDecodeContext context)
        {
            DetectionResult detections = _inner.Decode(context) as DetectionResult ?? throw new VisualException(VisualErrorCodes.DecodeFailed, "The bound detector did not return the canonical DetectionResult.", profileId: _profileId);
            var matches = new List<OpenVocabularyDetectionMatch>(detections.Detections.Count);
            for (int index = 0; index < detections.Detections.Count; index++)
            {
                int classIndex = detections.Detections[index].Label.Index;
                if (classIndex < 0 || classIndex >= _vocabulary.Entries.Count) throw new VisualException(VisualErrorCodes.OpenVocabularyIdentityMismatch, "A decoded class index is outside the artifact-bound vocabulary.", profileId: _profileId, technicalDetails: "classIndex=" + classIndex);
                OpenVocabularyTokenizationEntry? tokens;
                matches.Add(new OpenVocabularyDetectionMatch(index, classIndex, _vocabulary.Entries[classIndex].Text, _tokenization.TryGetValue(classIndex, out tokens) ? tokens.TokenIds : Array.Empty<int>()));
            }
            return new OpenVocabularyDetectionResult(detections, matches, _profileId, _vocabulary.Sha256, _promptMode);
        }
    }
}
