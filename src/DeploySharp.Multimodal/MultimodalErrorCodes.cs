namespace JYPPX.DeploySharp.Multimodal
{
    /// <summary>Defines stable error codes for multimodal orchestration. / 定义多模态编排的稳定错误码。</summary>
    public static class MultimodalErrorCodes
    {
        /// <summary>The request violates the public multimodal contract. / 请求违反公共多模态合同。</summary>
        public const string RequestInvalid = "DS-MM-7001";
        /// <summary>The selected adapter cannot provide a required capability. / 所选适配器无法提供所需能力。</summary>
        public const string CapabilityUnavailable = "DS-MM-7002";
        /// <summary>A single-writer session already has an active operation. / 单写入会话已有活动操作。</summary>
        public const string SessionBusy = "DS-MM-7003";
        /// <summary>A backend returned an invalid result or stream. / 后端返回了无效结果或流。</summary>
        public const string BackendContractInvalid = "DS-MM-7004";
        /// <summary>The operation exceeded its configured timeout. / 操作超过配置的超时时间。</summary>
        public const string Timeout = "DS-MM-7005";
        /// <summary>The operation was cancelled. / 操作已取消。</summary>
        public const string Cancelled = "DS-MM-7006";
        /// <summary>A media identity or content hash is invalid. / 媒体身份或内容哈希无效。</summary>
        public const string MediaIdentityInvalid = "DS-MM-7007";
    }
}
