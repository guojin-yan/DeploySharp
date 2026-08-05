namespace JYPPX.DeploySharp.Errors
{
    /// <summary>
    /// Defines stable error codes intended for diagnostics and support tooling. / 定义用于诊断和支持工具的稳定错误码。
    /// </summary>
    public static class DeploySharpErrorCodes
    {
        /// <summary>The requested backend is not registered. / 请求的后端尚未注册。</summary>
        public const string BackendNotFound = "DS-BACKEND-5001";

        /// <summary>No registered backend can satisfy the requested capabilities and artifact. / 没有已注册后端能够满足请求能力和工件。</summary>
        public const string BackendNotCompatible = "DS-BACKEND-5002";

        /// <summary>A backend identifier was registered more than once. / 同一后端标识符被重复注册。</summary>
        public const string BackendAlreadyRegistered = "DS-BACKEND-5003";

        /// <summary>The runtime or registry has already been disposed. / 运行时或注册中心已释放。</summary>
        public const string ObjectDisposed = "DS-CORE-1001";

        /// <summary>An inference operation failed. / 推理操作失败。</summary>
        public const string InferenceFailed = "DS-BACKEND-5004";

        /// <summary>A model artifact is invalid or cannot be loaded. / 模型工件无效或无法加载。</summary>
        public const string ModelArtifactInvalid = "DS-MODEL-2001";

        /// <summary>A language-model operation failed. / 大语言模型操作失败。</summary>
        public const string LanguageModelFailed = "DS-LLM-4001";

        /// <summary>The requested language-model capability is unavailable. / 请求的大语言模型能力不可用。</summary>
        public const string LanguageModelCapabilityUnavailable = "DS-LLM-4002";

        /// <summary>The model context window cannot satisfy the request. / 模型上下文窗口无法满足请求。</summary>
        public const string ContextLimitExceeded = "DS-LLM-4003";

        /// <summary>A required native runtime is unavailable. / 所需原生运行时不可用。</summary>
        public const string NativeRuntimeUnavailable = "DS-NATIVE-6001";
    }
}
