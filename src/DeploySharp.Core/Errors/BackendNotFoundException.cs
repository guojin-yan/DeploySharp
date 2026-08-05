using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Errors
{
    /// <summary>
    /// Indicates that an explicitly selected backend is not registered. / 指示显式选择的后端尚未注册。
    /// </summary>
    public sealed class BackendNotFoundException : DeploySharpException
    {
        /// <summary>Initializes the exception. / 初始化异常。</summary>
        public BackendNotFoundException(BackendId backendId, ModelId? modelId = null)
            : base(
                DeploySharpErrorCodes.BackendNotFound,
                $"The backend '{backendId}' is not registered.",
                backendId: backendId,
                modelId: modelId)
        {
        }
    }
}
