using System;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Multimodal
{
    /// <summary>Represents a stable multimodal orchestration failure. / 表示稳定的多模态编排故障。</summary>
    public sealed class MultimodalException : DeploySharpException
    {
        /// <summary>Initializes a multimodal exception without writing logs. / 初始化多模态异常且不写入日志。</summary>
        public MultimodalException(
            string errorCode,
            string message,
            Exception? innerException = null,
            ModelId? modelId = null,
            string? technicalDetails = null)
            : base(errorCode, message, innerException, modelId: modelId, technicalDetails: technicalDetails)
        {
        }
    }
}
