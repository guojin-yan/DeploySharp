using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Declares required preceding modules before running a caller-owned document task. / 在运行调用方拥有的文档任务前声明必需的前置模块。</summary>
    public sealed class PaddleDocumentDependentStage : IPaddleDocumentPipelineStage
    {
        private readonly IReadOnlyList<PaddleDocumentModule> _dependencies;
        private readonly Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleDocumentModuleResult>> _handler;

        /// <summary>Initializes a task stage with explicit dependencies and a result handler. / 使用显式依赖和结果处理器初始化任务阶段。</summary>
        public PaddleDocumentDependentStage(
            PaddleDocumentModule module,
            IEnumerable<PaddleDocumentModule> dependencies,
            Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleDocumentModuleResult>> handler)
        {
            if (!Enum.IsDefined(typeof(PaddleDocumentModule), module) || module == PaddleDocumentModule.StructurePipeline)
                throw new ArgumentOutOfRangeException(nameof(module));
            if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
            var values = dependencies.Distinct().ToArray();
            if (values.Any(value => !Enum.IsDefined(typeof(PaddleDocumentModule), value) || value == PaddleDocumentModule.StructurePipeline || value == module))
                throw new ArgumentException("Dependencies must be valid modules different from the stage module.", nameof(dependencies));
            _dependencies = Array.AsReadOnly(values);
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Module = module;
        }

        /// <summary>Gets the stage module. / 获取阶段模块。</summary>
        public PaddleDocumentModule Module { get; }

        /// <summary>Gets the modules required before this stage can run. / 获取运行本阶段前必须存在的模块。</summary>
        public IReadOnlyList<PaddleDocumentModule> Dependencies => _dependencies;

        /// <summary>Validates dependencies and invokes the caller-owned task handler. / 校验依赖并调用调用方任务处理器。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            foreach (PaddleDocumentModule dependency in _dependencies)
                if (!context.Results.Any(result => result.Module == dependency))
                    throw new InvalidOperationException("The document stage requires a preceding result: " + dependency + "; stage=" + Module);
            cancellationToken.ThrowIfCancellationRequested();
            PaddleDocumentModuleResult result = await _handler(context, cancellationToken).ConfigureAwait(false);
            if (result == null) throw new InvalidOperationException("The document stage returned null: " + Module);
            if (result.Module != Module) throw new InvalidOperationException("The document stage returned a mismatched module. expected=" + Module + ";actual=" + result.Module);
            return result;
        }
    }
}
