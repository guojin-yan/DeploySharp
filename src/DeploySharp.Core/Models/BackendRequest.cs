using System;

namespace JYPPX.DeploySharp.Models
{
    /// <summary>
    /// Describes the capabilities and optional backend explicitly requested for a session. / 描述会话明确请求的能力及可选后端。
    /// </summary>
    public sealed class BackendRequest
    {
        /// <summary>Initializes a backend request. / 初始化后端请求。</summary>
        public BackendRequest(
            BackendCapabilities requiredCapabilities,
            BackendId? backendId = null,
            string? device = null)
        {
            if (backendId.HasValue && backendId.Value.IsEmpty)
            {
                throw new ArgumentException("An explicitly supplied backend identifier cannot be empty.", nameof(backendId));
            }

            RequiredCapabilities = requiredCapabilities;
            BackendId = backendId;
            Device = string.IsNullOrWhiteSpace(device) ? null : device;
        }

        /// <summary>Gets all capabilities required by the caller. / 获取调用方需要的全部能力。</summary>
        public BackendCapabilities RequiredCapabilities { get; }

        /// <summary>Gets the explicitly selected backend, when present. / 获取显式选择的后端（如果有）。</summary>
        public BackendId? BackendId { get; }

        /// <summary>Gets an optional backend-specific device name. / 获取可选的后端专用设备名称。</summary>
        public string? Device { get; }
    }
}
