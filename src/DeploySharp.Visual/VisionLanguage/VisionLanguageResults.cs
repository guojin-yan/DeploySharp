using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Binds an embedding to the exact profile, artifacts, and source content. / 将 Embedding 绑定到精确 Profile、工件与源内容。</summary>
    public sealed class VisionLanguageEmbeddingIdentity : IEquatable<VisionLanguageEmbeddingIdentity>
    {
        /// <summary>Initializes an embedding identity. / 初始化 Embedding Identity。</summary>
        public VisionLanguageEmbeddingIdentity(string profileId, string artifactIdentity, string contentSha256, int dimension)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(artifactIdentity) || artifactIdentity.Length != 64 || string.IsNullOrWhiteSpace(contentSha256) || contentSha256.Length != 64 || dimension <= 0) throw new VisualException(VisualErrorCodes.VisionLanguageIdentityMismatch, "Embedding identity is incomplete or is not SHA256-bound.");
            ProfileId = profileId;
            ArtifactIdentity = artifactIdentity;
            ContentSha256 = contentSha256.ToLowerInvariant();
            Dimension = dimension;
        }

        /// <summary>Gets the profile identity. / 获取 Profile Identity。</summary>
        public string ProfileId { get; }
        /// <summary>Gets the complete encoder artifact identity. / 获取完整 Encoder 工件 Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets source image or token-batch SHA256. / 获取源图或 Token 批次 SHA256。</summary>
        public string ContentSha256 { get; }
        /// <summary>Gets embedding width. / 获取 Embedding 宽度。</summary>
        public int Dimension { get; }
        /// <summary>Compares exact identity fields. / 比较精确 Identity 字段。</summary>
        public bool Equals(VisionLanguageEmbeddingIdentity? other) => other != null && Dimension == other.Dimension && string.Equals(ProfileId, other.ProfileId, StringComparison.Ordinal) && string.Equals(ArtifactIdentity, other.ArtifactIdentity, StringComparison.Ordinal) && string.Equals(ContentSha256, other.ContentSha256, StringComparison.Ordinal);
        /// <summary>Compares this identity with an object. / 将此 Identity 与对象比较。</summary>
        public override bool Equals(object? obj) => Equals(obj as VisionLanguageEmbeddingIdentity);
        /// <summary>Gets a stable hash code for the identity fields. / 获取 Identity 字段的稳定哈希码。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(ProfileId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ArtifactIdentity);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ContentSha256);
                return (hash * 397) ^ Dimension;
            }
        }
    }

    /// <summary>Owns projected, L2-normalized image embeddings. / 拥有投影并执行 L2 归一化的图像 Embedding。</summary>
    public sealed class VisionLanguageImageEmbedding
    {
        private readonly float[] _values;

        internal VisionLanguageImageEmbedding(VisionLanguageEmbeddingIdentity identity, int batchSize, float[] values, TimeSpan encoderTime)
        {
            Identity = identity;
            BatchSize = batchSize;
            _values = (float[])values.Clone();
            EncoderTime = encoderTime;
            Sha256 = VisionLanguageHash.Floats(_values);
        }

        /// <summary>Gets exact embedding identity. / 获取精确 Embedding Identity。</summary>
        public VisionLanguageEmbeddingIdentity Identity { get; }
        /// <summary>Gets image batch size. / 获取图像批次大小。</summary>
        public int BatchSize { get; }
        /// <summary>Gets embedding dimension. / 获取 Embedding 维度。</summary>
        public int Dimension => Identity.Dimension;
        /// <summary>Gets one encoder timing observation. / 获取一次 Encoder Timing 观测。</summary>
        public TimeSpan EncoderTime { get; }
        /// <summary>Gets embedding payload SHA256. / 获取 Embedding Payload SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Returns a defensive value copy. / 返回数值防御性副本。</summary>
        public float[] CopyValues() => (float[])_values.Clone();
        internal float Value(int batch, int dimension) => _values[(batch * Dimension) + dimension];
    }

    /// <summary>Owns projected, L2-normalized text embeddings and prompt provenance. / 拥有投影并执行 L2 归一化的文本 Embedding 与提示来源。</summary>
    public sealed class VisionLanguageTextEmbedding
    {
        private readonly float[] _values;
        private readonly IReadOnlyList<string> _texts;

        internal VisionLanguageTextEmbedding(VisionLanguageEmbeddingIdentity identity, IEnumerable<string> texts, float[] values, TimeSpan encoderTime)
        {
            Identity = identity;
            _texts = new ReadOnlyCollection<string>(new List<string>(texts));
            _values = (float[])values.Clone();
            EncoderTime = encoderTime;
            Sha256 = VisionLanguageHash.Floats(_values);
        }

        /// <summary>Gets exact embedding identity. / 获取精确 Embedding Identity。</summary>
        public VisionLanguageEmbeddingIdentity Identity { get; }
        /// <summary>Gets ordered original prompt text. / 获取有序原始提示文本。</summary>
        public IReadOnlyList<string> Texts => _texts;
        /// <summary>Gets text batch size. / 获取文本批次大小。</summary>
        public int BatchSize => _texts.Count;
        /// <summary>Gets embedding dimension. / 获取 Embedding 维度。</summary>
        public int Dimension => Identity.Dimension;
        /// <summary>Gets one encoder timing observation. / 获取一次 Encoder Timing 观测。</summary>
        public TimeSpan EncoderTime { get; }
        /// <summary>Gets embedding payload SHA256. / 获取 Embedding Payload SHA256。</summary>
        public string Sha256 { get; }
        /// <summary>Returns a defensive value copy. / 返回数值防御性副本。</summary>
        public float[] CopyValues() => (float[])_values.Clone();
        internal float Value(int batch, int dimension) => _values[(batch * Dimension) + dimension];
    }

    /// <summary>Contains raw logits and semantics-specific probabilities for an image/text matrix. / 包含图文矩阵的原始 Logit 与语义特定概率。</summary>
    public sealed class VisionLanguageScoreMatrix
    {
        private readonly float[] _logits;
        private readonly float[] _probabilities;

        internal VisionLanguageScoreMatrix(int imageCount, int textCount, VisionLanguageScoreSemantics semantics, float[] logits, float[] probabilities)
        {
            ImageCount = imageCount;
            TextCount = textCount;
            Semantics = semantics;
            _logits = (float[])logits.Clone();
            _probabilities = (float[])probabilities.Clone();
        }

        /// <summary>Gets image row count. / 获取图像行数。</summary>
        public int ImageCount { get; }
        /// <summary>Gets text column count. / 获取文本列数。</summary>
        public int TextCount { get; }
        /// <summary>Gets exact scoring semantics. / 获取精确评分语义。</summary>
        public VisionLanguageScoreSemantics Semantics { get; }
        /// <summary>Returns a defensive row-major logit copy. / 返回行优先 Logit 防御性副本。</summary>
        public float[] CopyLogits() => (float[])_logits.Clone();
        /// <summary>Returns a defensive row-major probability copy. / 返回行优先概率防御性副本。</summary>
        public float[] CopyProbabilities() => (float[])_probabilities.Clone();
        /// <summary>Gets one raw pair logit. / 获取一个原始图文对 Logit。</summary>
        public float GetLogit(int imageIndex, int textIndex) => _logits[(imageIndex * TextCount) + textIndex];
        /// <summary>Gets one probability under the matrix semantics. / 获取矩阵语义下的一个概率。</summary>
        public float GetProbability(int imageIndex, int textIndex) => _probabilities[(imageIndex * TextCount) + textIndex];
    }

    /// <summary>Contains canonical classification predictions plus prompt aggregation provenance. / 包含规范分类预测及提示聚合来源。</summary>
    public sealed class VisionLanguageClassificationResult
    {
        private readonly IReadOnlyList<ZeroShotLabelPrompt> _labels;

        internal VisionLanguageClassificationResult(ClassificationResult classification, IEnumerable<ZeroShotLabelPrompt> labels, VisionLanguageScoreSemantics semantics)
        {
            Classification = classification;
            _labels = new ReadOnlyCollection<ZeroShotLabelPrompt>(new List<ZeroShotLabelPrompt>(labels));
            Semantics = semantics;
        }

        /// <summary>Gets the existing canonical classification result. / 获取现有规范分类结果。</summary>
        public ClassificationResult Classification { get; }
        /// <summary>Gets class-to-template provenance. / 获取类别到模板的来源关系。</summary>
        public IReadOnlyList<ZeroShotLabelPrompt> Labels => _labels;
        /// <summary>Gets the probability semantics. / 获取概率语义。</summary>
        public VisionLanguageScoreSemantics Semantics { get; }
    }

    /// <summary>Represents one deterministic cross-modal retrieval match. / 表示一个确定性的跨模态检索匹配。</summary>
    public sealed class VisionLanguageRetrievalMatch
    {
        internal VisionLanguageRetrievalMatch(int index, string label, float logit, float score)
        {
            Index = index;
            Label = label;
            Logit = logit;
            Score = score;
        }

        /// <summary>Gets candidate index. / 获取候选索引。</summary>
        public int Index { get; }
        /// <summary>Gets candidate text or stable image label. / 获取候选文本或稳定图像标签。</summary>
        public string Label { get; }
        /// <summary>Gets raw pair logit. / 获取原始图文对 Logit。</summary>
        public float Logit { get; }
        /// <summary>Gets softmax or sigmoid score according to the profile. / 获取按 Profile 定义的 Softmax 或 Sigmoid 分数。</summary>
        public float Score { get; }
    }

    /// <summary>Scores compatible owned embeddings without backend or tokenizer access. / 在不访问 Backend 或 Tokenizer 的情况下对兼容自有 Embedding 评分。</summary>
    public static class VisionLanguageScorer
    {
        /// <summary>Computes raw logits and semantics-specific image-to-text probabilities. / 计算原始 Logit 与语义特定的图像到文本概率。</summary>
        public static VisionLanguageScoreMatrix Score(VisionLanguageEmbeddingProfile profile, VisionLanguageImageEmbedding images, VisionLanguageTextEmbedding texts)
        {
            Validate(profile, images, texts);
            var logits = new float[checked(images.BatchSize * texts.BatchSize)];
            var probabilities = new float[logits.Length];
            for (int image = 0; image < images.BatchSize; image++)
            {
                for (int text = 0; text < texts.BatchSize; text++)
                {
                    double dot = 0;
                    for (int dimension = 0; dimension < profile.EmbeddingDimension; dimension++) dot += images.Value(image, dimension) * texts.Value(text, dimension);
                    logits[(image * texts.BatchSize) + text] = checked((float)((dot * profile.LogitScale) + profile.LogitBias));
                }
                if (profile.ScoreSemantics == VisionLanguageScoreSemantics.ClipSoftmax) Softmax(logits, probabilities, image * texts.BatchSize, texts.BatchSize);
                else for (int text = 0; text < texts.BatchSize; text++) probabilities[(image * texts.BatchSize) + text] = Sigmoid(logits[(image * texts.BatchSize) + text]);
            }
            return new VisionLanguageScoreMatrix(images.BatchSize, texts.BatchSize, profile.ScoreSemantics, logits, probabilities);
        }

        /// <summary>Aggregates one or more normalized prompt embeddings per label, then returns the existing canonical classification result. / 每个标签聚合一个或多个归一化提示 Embedding，再返回现有规范分类结果。</summary>
        public static VisionLanguageClassificationResult Classify(VisionLanguageEmbeddingProfile profile, VisionLanguageImageEmbedding image, VisionLanguageTextEmbedding prompts, IEnumerable<ZeroShotLabelPrompt> labels)
        {
            if (image.BatchSize != 1) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Zero-shot classification requires one image.", profileId: profile.ProfileId);
            var groups = labels == null ? throw new ArgumentNullException(nameof(labels)) : labels.ToList();
            if (groups.Count == 0 || groups.Select(value => value.Label).Distinct(StringComparer.Ordinal).Count() != groups.Count) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "Zero-shot labels must be non-empty and unique.", profileId: profile.ProfileId);
            Validate(profile, image, prompts);
            var aggregated = new float[checked(groups.Count * profile.EmbeddingDimension)];
            for (int label = 0; label < groups.Count; label++)
            {
                ZeroShotLabelPrompt group = groups[label];
                if (group.PromptIndexes.Any(index => index >= prompts.BatchSize)) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A template index is outside the text embedding batch.", profileId: profile.ProfileId);
                double norm = 0;
                for (int dimension = 0; dimension < profile.EmbeddingDimension; dimension++)
                {
                    double sum = 0;
                    foreach (int index in group.PromptIndexes) sum += prompts.Value(index, dimension);
                    float mean = (float)(sum / group.PromptIndexes.Count);
                    aggregated[(label * profile.EmbeddingDimension) + dimension] = mean;
                    norm += mean * mean;
                }
                if (norm <= 0) throw new VisualException(VisualErrorCodes.VisionLanguageContractInvalid, "A template aggregate has zero norm.", profileId: profile.ProfileId);
                float reciprocal = (float)(1.0 / Math.Sqrt(norm));
                for (int dimension = 0; dimension < profile.EmbeddingDimension; dimension++) aggregated[(label * profile.EmbeddingDimension) + dimension] *= reciprocal;
            }
            var identity = new VisionLanguageEmbeddingIdentity(prompts.Identity.ProfileId, prompts.Identity.ArtifactIdentity, VisionLanguageHash.Text(prompts.Identity.ContentSha256 + "|" + string.Join("|", groups.Select(value => value.Label + ":" + string.Join(",", value.PromptIndexes)))), prompts.Dimension);
            var aggregateTexts = new VisionLanguageTextEmbedding(identity, groups.Select(value => value.Label), aggregated, TimeSpan.Zero);
            VisionLanguageScoreMatrix matrix = Score(profile, image, aggregateTexts);
            float[] values = matrix.CopyProbabilities();
            var predictions = groups.Select((group, index) => new LabelScore(index, group.Label, values[index])).OrderByDescending(value => value.Score).ThenBy(value => value.Index).ToList();
            return new VisionLanguageClassificationResult(new ClassificationResult(predictions), groups, profile.ScoreSemantics);
        }

        /// <summary>Returns deterministic top-k text candidates for one image. / 返回一个图像的确定性 Top-k 文本候选。</summary>
        public static IReadOnlyList<VisionLanguageRetrievalMatch> RetrieveTexts(VisionLanguageEmbeddingProfile profile, VisionLanguageImageEmbedding image, VisionLanguageTextEmbedding texts, int topK)
        {
            if (image.BatchSize != 1 || topK <= 0 || topK > texts.BatchSize) throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "Text retrieval batch or top-k is invalid.", profileId: profile.ProfileId);
            VisionLanguageScoreMatrix matrix = Score(profile, image, texts);
            return new ReadOnlyCollection<VisionLanguageRetrievalMatch>(Enumerable.Range(0, texts.BatchSize).Select(index => new VisionLanguageRetrievalMatch(index, texts.Texts[index], matrix.GetLogit(0, index), matrix.GetProbability(0, index))).OrderByDescending(value => value.Logit).ThenBy(value => value.Index).Take(topK).ToList());
        }

        /// <summary>Returns deterministic top-k image candidates for one text; CLIP softmax is recomputed across image candidates. / 返回一个文本的确定性 Top-k 图像候选；CLIP Softmax 在图像候选维度重新计算。</summary>
        public static IReadOnlyList<VisionLanguageRetrievalMatch> RetrieveImages(VisionLanguageEmbeddingProfile profile, VisionLanguageImageEmbedding images, VisionLanguageTextEmbedding texts, int textIndex, int topK)
        {
            if (textIndex < 0 || textIndex >= texts.BatchSize || topK <= 0 || topK > images.BatchSize) throw new VisualException(VisualErrorCodes.VisionLanguageLimitExceeded, "Image retrieval text index or top-k is invalid.", profileId: profile.ProfileId);
            VisionLanguageScoreMatrix matrix = Score(profile, images, texts);
            var logits = new float[images.BatchSize];
            var scores = new float[images.BatchSize];
            for (int image = 0; image < images.BatchSize; image++) logits[image] = matrix.GetLogit(image, textIndex);
            if (profile.ScoreSemantics == VisionLanguageScoreSemantics.ClipSoftmax) Softmax(logits, scores, 0, scores.Length);
            else for (int image = 0; image < images.BatchSize; image++) scores[image] = Sigmoid(logits[image]);
            return new ReadOnlyCollection<VisionLanguageRetrievalMatch>(Enumerable.Range(0, images.BatchSize).Select(index => new VisionLanguageRetrievalMatch(index, "image-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), logits[index], scores[index])).OrderByDescending(value => value.Logit).ThenBy(value => value.Index).Take(topK).ToList());
        }

        private static void Validate(VisionLanguageEmbeddingProfile profile, VisionLanguageImageEmbedding images, VisionLanguageTextEmbedding texts)
        {
            if (profile == null || images == null || texts == null) throw new ArgumentNullException(profile == null ? nameof(profile) : images == null ? nameof(images) : nameof(texts));
            if (images.Dimension != profile.EmbeddingDimension || texts.Dimension != profile.EmbeddingDimension || !string.Equals(images.Identity.ProfileId, profile.ProfileId, StringComparison.Ordinal) || !string.Equals(texts.Identity.ProfileId, profile.ProfileId, StringComparison.Ordinal) || !string.Equals(images.Identity.ArtifactIdentity, profile.ArtifactIdentity, StringComparison.Ordinal) || !string.Equals(texts.Identity.ArtifactIdentity, profile.ArtifactIdentity, StringComparison.Ordinal)) throw new VisualException(VisualErrorCodes.VisionLanguageIdentityMismatch, "Image and text embeddings must belong to the exact same profile and artifact bundle.", profileId: profile.ProfileId);
        }

        private static void Softmax(float[] logits, float[] destination, int offset, int count)
        {
            float maximum = float.NegativeInfinity;
            for (int index = 0; index < count; index++) maximum = Math.Max(maximum, logits[offset + index]);
            double sum = 0;
            for (int index = 0; index < count; index++) sum += Math.Exp(logits[offset + index] - maximum);
            for (int index = 0; index < count; index++) destination[offset + index] = (float)(Math.Exp(logits[offset + index] - maximum) / sum);
        }

        private static float Sigmoid(float value)
        {
            if (value >= 0) return (float)(1.0 / (1.0 + Math.Exp(-value)));
            double exponential = Math.Exp(value);
            return (float)(exponential / (1.0 + exponential));
        }
    }
}
