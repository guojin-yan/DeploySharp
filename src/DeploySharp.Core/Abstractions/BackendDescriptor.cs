using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp
{
    /// <summary>
    /// Provides immutable identity and capability metadata for a backend provider. / 提供后端提供程序的不可变标识与能力元数据。
    /// </summary>
    public sealed class BackendDescriptor
    {
        private readonly IReadOnlyList<string> _supportedFormats;

        /// <summary>
        /// Initializes backend metadata. / 初始化后端元数据。
        /// </summary>
        public BackendDescriptor(
            BackendId id,
            string displayName,
            string version,
            BackendCapabilities capabilities,
            IEnumerable<string>? supportedFormats = null)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("A backend identifier is required.", nameof(id));
            }

            Id = id;
            DisplayName = Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
            Version = Guard.NotNullOrWhiteSpace(version, nameof(version));
            Capabilities = capabilities;

            var formats = new List<string>();
            if (supportedFormats != null)
            {
                foreach (string format in supportedFormats)
                {
                    formats.Add(Guard.Identifier(format, nameof(supportedFormats)));
                }
            }

            _supportedFormats = formats.AsReadOnly();
        }

        /// <summary>Gets the stable backend identifier. / 获取稳定的后端标识符。</summary>
        public BackendId Id { get; }

        /// <summary>Gets the user-facing backend name. / 获取面向用户的后端名称。</summary>
        public string DisplayName { get; }

        /// <summary>Gets the managed backend adapter version. / 获取托管后端适配器版本。</summary>
        public string Version { get; }

        /// <summary>Gets the capabilities declared by this backend. / 获取此后端声明的能力。</summary>
        public BackendCapabilities Capabilities { get; }

        /// <summary>Gets normalized model formats accepted by this backend. / 获取此后端接受的规范化模型格式。</summary>
        public IReadOnlyList<string> SupportedFormats => _supportedFormats;

        /// <summary>
        /// Determines whether the descriptor includes every requested capability. / 确定描述信息是否包含全部请求能力。
        /// </summary>
        public bool Supports(BackendCapabilities requiredCapabilities)
        {
            return (Capabilities & requiredCapabilities) == requiredCapabilities;
        }
    }
}
