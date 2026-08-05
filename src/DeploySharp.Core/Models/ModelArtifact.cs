using System;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Models
{
    /// <summary>
    /// Describes one concrete model file or directory consumable by a backend. / 描述一个可由后端使用的具体模型文件或目录。
    /// </summary>
    public sealed class ModelArtifact
    {
        /// <summary>Initializes a model artifact. / 初始化模型工件。</summary>
        public ModelArtifact(
            ModelId modelId,
            string format,
            string location,
            string? sha256 = null,
            BackendId? preferredBackend = null)
        {
            if (modelId.IsEmpty)
            {
                throw new ArgumentException("A model identifier is required.", nameof(modelId));
            }

            ModelId = modelId;
            Format = Guard.Identifier(format, nameof(format));
            Location = Guard.NotNullOrWhiteSpace(location, nameof(location));
            Sha256 = string.IsNullOrWhiteSpace(sha256) ? null : sha256;
            PreferredBackend = preferredBackend;
        }

        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识符。</summary>
        public ModelId ModelId { get; }

        /// <summary>Gets the normalized model format, such as <c>onnx</c> or <c>gguf</c>. / 获取规范化模型格式，例如 <c>onnx</c> 或 <c>gguf</c>。</summary>
        public string Format { get; }

        /// <summary>Gets the file or directory location supplied by the application. / 获取应用提供的文件或目录位置。</summary>
        public string Location { get; }

        /// <summary>Gets the optional lowercase or uppercase SHA256 value. / 获取可选的大小写形式 SHA256 值。</summary>
        public string? Sha256 { get; }

        /// <summary>Gets the optional backend preferred by this artifact. / 获取此工件首选的可选后端。</summary>
        public BackendId? PreferredBackend { get; }
    }
}
