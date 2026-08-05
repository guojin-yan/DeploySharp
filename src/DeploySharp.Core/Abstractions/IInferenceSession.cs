using System;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp
{
    /// <summary>
    /// Runs named-tensor inference against one loaded model artifact. / 对一个已加载模型工件执行命名张量推理。
    /// </summary>
    public interface IInferenceSession : IDisposable
    {
        /// <summary>Gets metadata discovered while loading the model. / 获取加载模型时发现的元数据。</summary>
        public ModelMetadata Metadata { get; }

        /// <summary>Runs synchronous inference. / 执行同步推理。</summary>
        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken);

        /// <summary>Runs asynchronous inference or a documented backend fallback. / 执行异步推理，或使用后端已说明的回退方式。</summary>
        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken);
    }
}
