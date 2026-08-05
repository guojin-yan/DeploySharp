using System;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.LLM
{
    /// <summary>Creates language-model sessions without exposing vendor types. / 创建语言模型会话且不暴露厂商类型。</summary>
    public interface ILanguageModelProvider : IDisposable
    {
        /// <summary>Gets shared Core backend identity and capabilities. / 获取共享的 Core 后端标识和能力。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether the provider can load the artifact and request. / 判断提供程序能否加载工件并满足请求。</summary>
        public bool CanCreate(ModelArtifact artifact, LanguageModelRequest request);

        /// <summary>Creates an owned language-model session. / 创建一个由调用方负责释放的语言模型会话。</summary>
        public ILanguageModelSession CreateSession(
            ModelArtifact artifact,
            LanguageModelRequest request,
            LanguageModelSessionOptions? options = null);
    }
}
