using System;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp
{
    /// <summary>
    /// Creates backend sessions without exposing native backend types to Core consumers. / 创建后端会话，同时避免向 Core 使用者公开原生后端类型。
    /// </summary>
    public interface IBackendProvider : IDisposable
    {
        /// <summary>Gets backend identity and capability metadata. / 获取后端标识与能力元数据。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether this provider can create the requested session. / 确定此提供程序能否创建请求的会话。</summary>
        public bool CanCreate(ModelArtifact artifact, BackendRequest request);

        /// <summary>Creates a loaded inference session owned by the caller. / 创建一个由调用方负责释放的已加载推理会话。</summary>
        public IInferenceSession CreateSession(
            ModelArtifact artifact,
            BackendRequest request,
            SessionOptions options);
    }
}
