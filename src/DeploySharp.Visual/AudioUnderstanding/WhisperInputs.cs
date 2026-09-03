using System;
using System.Linq;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Contains one caller-owned or borrowed Whisper log-Mel tensor prepared exactly once. / 包含严格一次准备的调用方所有或借用 Whisper log-Mel 张量。</summary>
    public sealed class PreparedWhisperInput : IDisposable
    {
        private readonly IDisposable? _ownedResource;
        private bool _disposed;

        /// <summary>Initializes a fixed `[1,80,3000]` Whisper feature tensor. / 初始化固定 `[1,80,3000]` Whisper Feature 张量。</summary>
        public PreparedWhisperInput(AudioUnderstandingProfile profile, string inputName, Tensor<float> tensor, string sourceId, string sourceSha256, string featureSha256, TimeSpan preprocessTime, PreparedInputOwnership ownership = PreparedInputOwnership.Borrowed, IDisposable? ownedResource = null)
        {
            if (profile == null || tensor == null) throw new ArgumentNullException(profile == null ? nameof(profile) : nameof(tensor));
            if (!profile.Executable || profile.Family != AudioUnderstandingFamily.Whisper || profile.Generation == null) throw new VisualException(VisualErrorCodes.AudioCapabilityUnavailable, "A source-only or non-Whisper profile cannot accept Whisper features.", profileId: profile.ProfileId);
            AudioArtifactContract artifact = profile.GetArtifact(AudioArtifactRole.WhisperEncoder);
            AudioTensorContract? contract = artifact.Inputs.SingleOrDefault(value => string.Equals(value.Name, inputName, StringComparison.Ordinal));
            if (contract == null || tensor.ElementType != TensorElementType.Float32 || tensor.Shape.Rank != 3 || tensor.Shape[0] != 1 || tensor.Shape[1] != 80 || tensor.Shape[2] != profile.Generation.MaximumMelFrames || tensor.Length > contract.MaximumElements) throw AudioFailure.Contract("Prepared Whisper features differ from the profile-bound `[1,80,3000]` contract.", profile.ProfileId, inputName);
            if (string.IsNullOrWhiteSpace(sourceId) || !AudioUnderstandingHash.IsSha256(sourceSha256) || !AudioUnderstandingHash.IsSha256(featureSha256) || preprocessTime < TimeSpan.Zero || !Enum.IsDefined(typeof(PreparedInputOwnership), ownership)) throw AudioFailure.Contract("Prepared Whisper feature provenance is invalid.", profile.ProfileId);
            if (ownership == PreparedInputOwnership.Owned && ownedResource == null) throw AudioFailure.Contract("Owned Whisper features require a disposable resource.", profile.ProfileId);
            if (ownership == PreparedInputOwnership.Borrowed && ownedResource != null) throw AudioFailure.Contract("Borrowed Whisper features cannot accept an owned resource.", profile.ProfileId);
            float[] values = (float[])tensor.Buffer;
            if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new VisualException(VisualErrorCodes.AudioNonFinite, "Prepared Whisper features contain NaN or Infinity.", profileId: profile.ProfileId, tensorName: inputName);
            ProfileId = profile.ProfileId; ProfileIdentity = profile.Identity; InputName = inputName; Tensor = tensor; SourceId = sourceId.Trim(); SourceSha256 = sourceSha256.ToLowerInvariant(); FeatureSha256 = featureSha256.ToLowerInvariant(); PreprocessTime = preprocessTime; Ownership = ownership; _ownedResource = ownedResource;
            Identity = AudioUnderstandingHash.Text(ProfileIdentity + "|" + SourceId + "|" + SourceSha256 + "|" + FeatureSha256 + "|" + tensor.Shape[1] + "|" + tensor.Shape[2]);
        }

        /// <summary>Gets profile ID. / 获取 Profile ID。</summary>
        public string ProfileId { get; }
        /// <summary>Gets profile identity. / 获取 Profile Identity。</summary>
        public string ProfileIdentity { get; }
        /// <summary>Gets exact encoder input name. / 获取精确 Encoder 输入名。</summary>
        public string InputName { get; }
        /// <summary>Gets `[1,80,3000]` log-Mel features. / 获取 `[1,80,3000]` log-Mel Features。</summary>
        public Tensor<float> Tensor { get; }
        /// <summary>Gets source identity. / 获取源 Identity。</summary>
        public string SourceId { get; }
        /// <summary>Gets source SHA-256. / 获取源 SHA-256。</summary>
        public string SourceSha256 { get; }
        /// <summary>Gets feature SHA-256. / 获取 Feature SHA-256。</summary>
        public string FeatureSha256 { get; }
        /// <summary>Gets feature preprocessing time. / 获取 Feature 预处理耗时。</summary>
        public TimeSpan PreprocessTime { get; }
        /// <summary>Gets resource ownership. / 获取资源所有权。</summary>
        public PreparedInputOwnership Ownership { get; }
        /// <summary>Gets complete feature identity. / 获取完整 Feature Identity。</summary>
        public string Identity { get; }
        /// <summary>Gets whether this input has released its owned resource. / 获取是否已释放自有资源。</summary>
        public bool IsDisposed => _disposed;
        /// <summary>Idempotently releases only an explicitly owned resource. / 幂等释放且仅释放显式拥有的资源。</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _ownedResource?.Dispose(); }
        internal void EnsureUsable() { if (_disposed) throw new VisualException(VisualErrorCodes.AudioDisposed, "The prepared Whisper input is disposed.", profileId: ProfileId); }
    }
}
