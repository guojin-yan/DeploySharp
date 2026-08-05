using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one deterministic profile and Core backend-descriptor selection. / 表示一次确定性的 Profile 与 Core 后端描述选择。</summary>
    public sealed class VisualProfileSelection
    {
        internal VisualProfileSelection(VisualModelProfile profile, ModelArtifact artifact, BackendDescriptor backend)
        {
            Profile = profile;
            Artifact = artifact;
            Backend = backend;
        }

        /// <summary>Gets the selected visual profile. / 获取选中的视觉 Profile。</summary>
        public VisualModelProfile Profile { get; }
        /// <summary>Gets the selected Core model artifact. / 获取选中的 Core 模型工件。</summary>
        public ModelArtifact Artifact { get; }
        /// <summary>Gets the selected Core backend descriptor. / 获取选中的 Core 后端描述。</summary>
        public BackendDescriptor Backend { get; }
    }

    /// <summary>Provides an instance-scoped, freezable registry of immutable visual profiles. / 提供实例范围、可冻结的不可变视觉 Profile 注册中心。</summary>
    public sealed class VisualProfileRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, VisualModelProfile> _profiles = new Dictionary<string, VisualModelProfile>(StringComparer.Ordinal);
        private bool _frozen;

        /// <summary>Registers one profile before the registry is frozen. / 在注册中心冻结前注册一个 Profile。</summary>
        public void Register(VisualModelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            lock (_gate)
            {
                if (_frozen) throw new VisualException(VisualErrorCodes.ProfileInvalid, "The visual profile registry is frozen.", profileId: profile.ProfileId);
                if (_profiles.ContainsKey(profile.ProfileId)) throw new VisualException(VisualErrorCodes.ProfileAlreadyRegistered, "The visual profile is already registered.", profileId: profile.ProfileId, modelId: profile.ModelId);
                _profiles.Add(profile.ProfileId, profile);
            }
        }

        /// <summary>Prevents subsequent registration and returns this registry. / 阻止后续注册并返回此注册中心。</summary>
        public VisualProfileRegistry Freeze()
        {
            lock (_gate) _frozen = true;
            return this;
        }

        /// <summary>Gets whether the registry is frozen. / 获取注册中心是否已冻结。</summary>
        public bool IsFrozen { get { lock (_gate) return _frozen; } }

        /// <summary>Gets a deterministic snapshot sorted by profile identifier. / 获取按 Profile 标识符排序的确定性快照。</summary>
        public IReadOnlyList<VisualModelProfile> GetProfiles()
        {
            lock (_gate) return _profiles.Values.OrderBy(value => value.ProfileId, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        /// <summary>Gets one required profile by identifier. / 按标识符获取一个必需 Profile。</summary>
        public VisualModelProfile GetRequired(string profileId)
        {
            string normalized = VisualGuard.Identifier(profileId, nameof(profileId));
            lock (_gate)
            {
                if (_profiles.TryGetValue(normalized, out VisualModelProfile? profile)) return profile;
            }
            throw new VisualException(VisualErrorCodes.ProfileNotFound, "The requested visual profile is not registered.", profileId: normalized);
        }

        /// <summary>Selects a compatible profile and registered Core backend without creating another backend registry. / 在不创建另一套后端注册表的情况下选择兼容 Profile 和已注册 Core 后端。</summary>
        public VisualProfileSelection Select(ModelArtifact artifact, BackendRegistry backendRegistry, BackendRequest request, VisualTaskId? task = null)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (backendRegistry == null) throw new ArgumentNullException(nameof(backendRegistry));
            if (request == null) throw new ArgumentNullException(nameof(request));
            IReadOnlyList<VisualModelProfile> profiles = GetProfiles();
            IReadOnlyList<BackendDescriptor> backends = backendRegistry.GetDescriptors();
            IEnumerable<VisualModelProfile> matchingProfiles = profiles.Where(profile => profile.ModelId == artifact.ModelId && string.Equals(profile.ModelFormat, artifact.Format, StringComparison.OrdinalIgnoreCase) && (!task.HasValue || profile.Task == task.Value));
            foreach (VisualModelProfile profile in matchingProfiles)
            {
                BackendCapabilities required = profile.RequiredCapabilities | request.RequiredCapabilities | BackendCapabilities.TensorInference;
                foreach (BackendDescriptor backend in backends)
                {
                    if (request.BackendId.HasValue && backend.Id != request.BackendId.Value) continue;
                    if (!request.BackendId.HasValue && artifact.PreferredBackend.HasValue && backend.Id != artifact.PreferredBackend.Value) continue;
                    if (!backend.Supports(required)) continue;
                    if (!backend.SupportedFormats.Any(format => string.Equals(format, artifact.Format, StringComparison.OrdinalIgnoreCase))) continue;
                    return new VisualProfileSelection(profile, artifact, backend);
                }
            }

            string candidates = string.Join(", ", profiles.Select(profile => profile.ProfileId));
            throw new VisualException(VisualErrorCodes.ProfileNotFound, "No visual profile and backend matched the model artifact.", backendId: request.BackendId, modelId: artifact.ModelId, technicalDetails: "candidates=" + candidates);
        }
    }
}
