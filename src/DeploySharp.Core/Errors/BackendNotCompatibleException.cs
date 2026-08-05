using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Errors
{
    /// <summary>
    /// Indicates that no registered backend can satisfy a model artifact and capability request. / 指示没有已注册后端能够满足模型工件和能力请求。
    /// </summary>
    public sealed class BackendNotCompatibleException : DeploySharpException
    {
        /// <summary>Initializes the exception. / 初始化异常。</summary>
        public BackendNotCompatibleException(ModelId modelId, BackendId? backendId = null)
            : base(
                DeploySharpErrorCodes.BackendNotCompatible,
                backendId.HasValue
                    ? $"The backend '{backendId.Value}' cannot load model '{modelId}'."
                    : $"No registered backend can load model '{modelId}'.",
                backendId: backendId,
                modelId: modelId)
        {
        }
    }
}
