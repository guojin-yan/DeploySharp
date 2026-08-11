using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Binds a cached image embedding to exact encoded content, profile, artifacts, and geometry. / 将缓存的图像 Embedding 绑定到精确编码内容、Profile、工件与几何。</summary>
    public sealed class PromptableImageIdentity : IEquatable<PromptableImageIdentity>
    {
        /// <summary>Initializes an immutable image identity. / 初始化不可变图像 Identity。</summary>
        public PromptableImageIdentity(string profileId, string artifactIdentity, string contentSha256, VisualSize sourceSize, VisualSize modelSize)
        {
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? throw Invalid("A profile identity is required.") : profileId.Trim();
            ArtifactIdentity = string.IsNullOrWhiteSpace(artifactIdentity) ? throw Invalid("An artifact identity is required.") : artifactIdentity.Trim();
            ContentSha256 = PromptableSegmentationArtifactContract.NormalizeSha256(contentSha256, nameof(contentSha256));
            SourceSize = sourceSize;
            ModelSize = modelSize;
        }

        /// <summary>Gets the profile identifier. / 获取 Profile 标识符。</summary>
        public string ProfileId { get; }
        /// <summary>Gets the ordered artifact-role identity. / 获取有序工件角色 Identity。</summary>
        public string ArtifactIdentity { get; }
        /// <summary>Gets the exact encoded-image SHA256. / 获取精确编码图像 SHA256。</summary>
        public string ContentSha256 { get; }
        /// <summary>Gets the original image size. / 获取原图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the encoder canvas size. / 获取 Encoder 画布尺寸。</summary>
        public VisualSize ModelSize { get; }

        /// <summary>Compares two image identities exactly. / 精确比较两个图像 Identity。</summary>
        /// <remarks>Compares content, profile, artifact bundle, and both sizes exactly. / 精确比较内容、Profile、工件 Bundle 与两种尺寸。</remarks>
        public bool Equals(PromptableImageIdentity? other)
        {
            return other != null && string.Equals(ProfileId, other.ProfileId, StringComparison.Ordinal) && string.Equals(ArtifactIdentity, other.ArtifactIdentity, StringComparison.Ordinal) && string.Equals(ContentSha256, other.ContentSha256, StringComparison.Ordinal) && SourceSize == other.SourceSize && ModelSize == other.ModelSize;
        }

        /// <summary>Compares this image identity with another object. / 将此图像 Identity 与另一个对象比较。</summary>
        public override bool Equals(object? obj) => Equals(obj as PromptableImageIdentity);
        /// <summary>Returns the hash code for the exact image identity. / 返回精确图像 Identity 的哈希码。</summary>
        public override int GetHashCode() => unchecked((((StringComparer.Ordinal.GetHashCode(ProfileId) * 397) ^ StringComparer.Ordinal.GetHashCode(ArtifactIdentity)) * 397) ^ StringComparer.Ordinal.GetHashCode(ContentSha256));

        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.PromptableSegmentationIdentityMismatch, message);
    }

    /// <summary>Contains an owned, deterministic summary of one cached embedding tensor without exposing mutable backend buffers. / 包含一个缓存 Embedding 张量的自有确定性摘要，不公开可变 Backend Buffer。</summary>
    public sealed class PromptableImageEmbeddingSummary
    {
        /// <summary>Initializes an embedding summary. / 初始化 Embedding 摘要。</summary>
        public PromptableImageEmbeddingSummary(string outputName, TensorShape shape, long elementCount, float minimum, float maximum, double mean, string sha256)
        {
            if (string.IsNullOrWhiteSpace(outputName)) throw new ArgumentException("An output name is required.", nameof(outputName));
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (elementCount <= 0 || !Finite(minimum) || !Finite(maximum) || double.IsNaN(mean) || double.IsInfinity(mean)) throw new ArgumentOutOfRangeException(nameof(elementCount));
            OutputName = outputName.Trim();
            Shape = new TensorShape(shape.ToArray());
            ElementCount = elementCount;
            Minimum = minimum;
            Maximum = maximum;
            Mean = mean;
            Sha256 = PromptableSegmentationArtifactContract.NormalizeSha256(sha256, nameof(sha256));
        }

        /// <summary>Gets the exact encoder output name. / 获取精确 Encoder 输出名。</summary>
        public string OutputName { get; }
        /// <summary>Gets the runtime shape. / 获取运行时 Shape。</summary>
        public TensorShape Shape { get; }
        /// <summary>Gets the element count. / 获取元素数量。</summary>
        public long ElementCount { get; }
        /// <summary>Gets the minimum finite value. / 获取最小有限值。</summary>
        public float Minimum { get; }
        /// <summary>Gets the maximum finite value. / 获取最大有限值。</summary>
        public float Maximum { get; }
        /// <summary>Gets the arithmetic mean. / 获取算术平均值。</summary>
        public double Mean { get; }
        /// <summary>Gets SHA256 over little-endian single-precision values. / 获取对小端单精度值计算的 SHA256。</summary>
        public string Sha256 { get; }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Describes the active cached embedding and its single encoder execution. / 描述活动缓存 Embedding 及其单次 Encoder 执行。</summary>
    public sealed class PromptableImageEmbedding
    {
        private readonly IReadOnlyList<PromptableImageEmbeddingSummary> _summaries;

        internal PromptableImageEmbedding(PromptableImageIdentity identity, IEnumerable<PromptableImageEmbeddingSummary> summaries, TimeSpan encoderTime)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            var copy = new List<PromptableImageEmbeddingSummary>(summaries ?? throw new ArgumentNullException(nameof(summaries)));
            if (copy.Count == 0 || copy.Any(value => value == null)) throw new ArgumentException("At least one embedding summary is required.", nameof(summaries));
            _summaries = new ReadOnlyCollection<PromptableImageEmbeddingSummary>(copy);
            EncoderTime = encoderTime;
        }

        /// <summary>Gets exact image/profile/artifact identity. / 获取精确图像/Profile/工件 Identity。</summary>
        public PromptableImageIdentity Identity { get; }
        /// <summary>Gets owned embedding summaries in declared output order. / 获取按声明输出顺序排列的自有 Embedding 摘要。</summary>
        public IReadOnlyList<PromptableImageEmbeddingSummary> Summaries => _summaries;
        /// <summary>Gets the single encoder invocation time; it is not a benchmark statistic. / 获取单次 Encoder 调用时间；该值不是基准统计。</summary>
        public TimeSpan EncoderTime { get; }
    }

    /// <summary>Stores owned row-major low-resolution mask logits bound to one image embedding. / 存储绑定到一个图像 Embedding 的自有行优先低分辨率 Mask Logit。</summary>
    public sealed class PromptableMaskLogits
    {
        private readonly float[] _values;

        /// <summary>Initializes finite owned logits by defensive copy. / 通过防御性复制初始化有限的自有 Logit。</summary>
        public PromptableMaskLogits(int width, int height, float[] values, PromptableImageIdentity imageIdentity)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (values == null || (long)width * height != values.LongLength) throw new ArgumentException("Mask-logit dimensions do not match the value count.", nameof(values));
            for (int index = 0; index < values.Length; index++) if (float.IsNaN(values[index]) || float.IsInfinity(values[index])) throw Invalid("Mask feedback cannot contain NaN or Infinity.");
            Width = width;
            Height = height;
            _values = (float[])values.Clone();
            ImageIdentity = imageIdentity ?? throw new ArgumentNullException(nameof(imageIdentity));
        }

        /// <summary>Gets logit-grid width. / 获取 Logit 网格宽度。</summary>
        public int Width { get; }
        /// <summary>Gets logit-grid height. / 获取 Logit 网格高度。</summary>
        public int Height { get; }
        /// <summary>Gets the exact embedding identity. / 获取精确 Embedding Identity。</summary>
        public PromptableImageIdentity ImageIdentity { get; }
        /// <summary>Returns a defensive row-major copy. / 返回行优先防御性副本。</summary>
        public float[] ToArray() => (float[])_values.Clone();
        /// <summary>Creates typed feedback for a later decode against the same image/profile/artifacts. / 为同一图像/Profile/工件的后续解码创建类型化反馈。</summary>
        public PromptableMaskFeedback CreateFeedback() => new PromptableMaskFeedback(this);

        internal float[] CopyValues() => (float[])_values.Clone();
        private static VisualException Invalid(string message) => new VisualException(VisualErrorCodes.PromptableSegmentationContractInvalid, message);
    }

    /// <summary>Represents low-resolution mask feedback whose ownership and image identity are explicit. / 表示所有权与图像 Identity 均明确的低分辨率 Mask Feedback。</summary>
    public sealed class PromptableMaskFeedback
    {
        /// <summary>Initializes feedback from owned logits. / 从自有 Logit 初始化反馈。</summary>
        public PromptableMaskFeedback(PromptableMaskLogits logits)
        {
            Logits = logits ?? throw new ArgumentNullException(nameof(logits));
        }

        /// <summary>Gets owned low-resolution logits. / 获取自有低分辨率 Logit。</summary>
        public PromptableMaskLogits Logits { get; }
        /// <summary>Gets the required image/profile/artifact identity. / 获取必需的图像/Profile/工件 Identity。</summary>
        public PromptableImageIdentity ImageIdentity => Logits.ImageIdentity;
    }

    /// <summary>Records prompt provenance without retaining caller collections. / 记录提示来源且不保留调用方集合。</summary>
    public sealed class PromptablePromptProvenance
    {
        internal PromptablePromptProvenance(int pointCount, bool hasBox, bool hasMaskFeedback, bool requestedMultipleMasks, string? promptId)
        {
            PointCount = pointCount;
            HasBox = hasBox;
            HasMaskFeedback = hasMaskFeedback;
            RequestedMultipleMasks = requestedMultipleMasks;
            PromptId = promptId;
        }

        /// <summary>Gets the source point count. / 获取源图点数量。</summary>
        public int PointCount { get; }
        /// <summary>Gets whether a box was supplied. / 获取是否提供了框。</summary>
        public bool HasBox { get; }
        /// <summary>Gets whether low-resolution feedback was supplied. / 获取是否提供了低分辨率反馈。</summary>
        public bool HasMaskFeedback { get; }
        /// <summary>Gets whether all candidates were requested. / 获取是否请求全部候选。</summary>
        public bool RequestedMultipleMasks { get; }
        /// <summary>Gets optional application prompt identity. / 获取可选应用提示 Identity。</summary>
        public string? PromptId { get; }
    }

    /// <summary>Associates one canonical instance with raw quality and owned low-resolution feedback logits. / 将一个规范实例与原始质量及自有低分辨率反馈 Logit 关联。</summary>
    public sealed class PromptableMaskCandidate
    {
        internal PromptableMaskCandidate(int sourceIndex, float quality, PromptableMaskQualityKind qualityKind, PromptableMaskLogits lowResolutionLogits)
        {
            if (sourceIndex < 0 || float.IsNaN(quality) || float.IsInfinity(quality)) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (!Enum.IsDefined(typeof(PromptableMaskQualityKind), qualityKind)) throw new ArgumentOutOfRangeException(nameof(qualityKind));
            SourceIndex = sourceIndex;
            Quality = quality;
            QualityKind = qualityKind;
            LowResolutionLogits = lowResolutionLogits ?? throw new ArgumentNullException(nameof(lowResolutionLogits));
        }

        /// <summary>Gets the original graph candidate index. / 获取原始图候选索引。</summary>
        public int SourceIndex { get; }
        /// <summary>Gets the unmodified finite graph quality value. / 获取未修改的有限图质量值。</summary>
        public float Quality { get; }
        /// <summary>Gets quality semantics. / 获取质量语义。</summary>
        public PromptableMaskQualityKind QualityKind { get; }
        /// <summary>Gets owned feedback logits for refinement. / 获取用于细化的自有反馈 Logit。</summary>
        public PromptableMaskLogits LowResolutionLogits { get; }
    }

    /// <summary>Contains per-stage timing from one decode; every value is a single observation. / 包含一次解码的分阶段 Timing；每个值均为单次观测。</summary>
    public sealed class PromptableSegmentationTiming
    {
        internal PromptableSegmentationTiming(TimeSpan promptPreparation, TimeSpan promptDecode, TimeSpan restore)
        {
            PromptPreparation = promptPreparation;
            PromptDecode = promptDecode;
            Restore = restore;
        }

        /// <summary>Gets typed prompt tensor preparation time. / 获取类型化提示张量准备时间。</summary>
        public TimeSpan PromptPreparation { get; }
        /// <summary>Gets prompt encoder/mask decoder backend time. / 获取 Prompt Encoder/Mask Decoder Backend 时间。</summary>
        public TimeSpan PromptDecode { get; }
        /// <summary>Gets source-mask materialization, ordering, bounds, and RLE time. / 获取源图掩码物化、排序、边界与 RLE 时间。</summary>
        public TimeSpan Restore { get; }
    }

    /// <summary>Extends the canonical instance-segmentation result with prompt provenance, quality, and feedback logits. / 使用提示来源、质量与反馈 Logit 扩展规范实例分割结果。</summary>
    public sealed class PromptableSegmentationResult
    {
        private readonly IReadOnlyList<PromptableMaskCandidate> _candidates;

        internal PromptableSegmentationResult(InstanceSegmentationResult segmentation, IEnumerable<PromptableMaskCandidate> candidates, PromptableImageIdentity imageIdentity, PromptablePromptProvenance prompt, PromptableSegmentationTiming timing)
        {
            Segmentation = segmentation ?? throw new ArgumentNullException(nameof(segmentation));
            var copy = new List<PromptableMaskCandidate>(candidates ?? throw new ArgumentNullException(nameof(candidates)));
            if (copy.Any(value => value == null)) throw new ArgumentException("Candidates cannot contain null.", nameof(candidates));
            foreach (PromptableMaskCandidate candidate in copy)
            {
                if (!segmentation.Instances.Any(value => value.SourceIndex == candidate.SourceIndex)) throw new ArgumentException("Every promptable candidate must reference one canonical instance.", nameof(candidates));
            }
            _candidates = new ReadOnlyCollection<PromptableMaskCandidate>(copy);
            ImageIdentity = imageIdentity ?? throw new ArgumentNullException(nameof(imageIdentity));
            Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        }

        /// <summary>Gets reusable canonical masks, RLE, geometry, ordering, and result ownership. / 获取可复用的规范掩码、RLE、几何、排序与结果所有权。</summary>
        public InstanceSegmentationResult Segmentation { get; }
        /// <summary>Gets candidates in the same deterministic score/source-index order as canonical instances. / 获取与规范实例相同的确定性分数/源索引顺序候选。</summary>
        public IReadOnlyList<PromptableMaskCandidate> Candidates => _candidates;
        /// <summary>Gets exact active image/profile/artifact identity. / 获取精确活动图像/Profile/工件 Identity。</summary>
        public PromptableImageIdentity ImageIdentity { get; }
        /// <summary>Gets owned prompt provenance. / 获取自有提示来源。</summary>
        public PromptablePromptProvenance Prompt { get; }
        /// <summary>Gets single-observation staged timing. / 获取单次观测分阶段 Timing。</summary>
        public PromptableSegmentationTiming Timing { get; }
    }

    internal static class PromptableSegmentationHash
    {
        public static string Floats(float[] values)
        {
            using (var stream = new MemoryStream(values.Length * sizeof(float)))
            using (var writer = new BinaryWriter(stream))
            {
                for (int index = 0; index < values.Length; index++) writer.Write(values[index]);
                writer.Flush();
                using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(stream.ToArray()));
            }
        }

        private static string Hex(byte[] bytes)
        {
            var characters = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = alphabet[bytes[index] >> 4];
                characters[(index * 2) + 1] = alphabet[bytes[index] & 15];
            }
            return new string(characters);
        }
    }
}
