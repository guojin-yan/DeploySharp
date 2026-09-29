using System;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>
    /// Adapts an existing Visual pipeline to one PP-Structure stage. The adapter
    /// keeps image preparation and result mapping in the application while the
    /// document pipeline owns ordering, provenance and cancellation.
    /// / 将现有 Visual Pipeline 适配为一个 PP-Structure 阶段；图像准备和结果映射
    /// 仍由应用控制，文档 Pipeline 负责顺序、来源和取消。
    /// </summary>
    public sealed class PaddleDocumentVisualPipelineStage : IPaddleDocumentPipelineStage
    {
        private readonly VisualPipeline _pipeline;
        private readonly Func<PaddleDocumentPipelineContext, CancellationToken, Task<PreparedVisualInput>> _prepare;
        private readonly Func<PaddleDocumentPipelineContext, VisualInferenceResult, PaddleDocumentModuleResult> _map;

        /// <summary>Creates an adapter over a caller-owned Visual pipeline. / 创建适配器。</summary>
        public PaddleDocumentVisualPipelineStage(
            PaddleDocumentModule module,
            VisualPipeline pipeline,
            Func<PaddleDocumentPipelineContext, CancellationToken, Task<PreparedVisualInput>> prepare,
            Func<PaddleDocumentPipelineContext, VisualInferenceResult, PaddleDocumentModuleResult> map)
        {
            if (!Enum.IsDefined(typeof(PaddleDocumentModule), module) || module == PaddleDocumentModule.StructurePipeline)
                throw new ArgumentOutOfRangeException(nameof(module));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            Module = module;
        }

        /// <inheritdoc />
        public PaddleDocumentModule Module { get; }

        /// <inheritdoc />
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            PreparedVisualInput? prepared = await _prepare(context, cancellationToken).ConfigureAwait(false);
            if (prepared == null) throw new InvalidOperationException("The PP-Structure visual stage preparation callback returned null.");

            // Owned inputs are transient stage resources. Borrowed inputs stay
            // with the caller, matching VisualPipeline's normal ownership rule.
            VisualInferenceResult inference = await _pipeline.RunAsync(
                prepared,
                new VisualExecutionOptions(disposeOwnedInputOnCompletion: true),
                cancellationToken).ConfigureAwait(false);
            PaddleDocumentModuleResult result = _map(context, inference);
            if (result == null) throw new InvalidOperationException("The PP-Structure visual stage mapping callback returned null.");
            if (result.Module != Module)
                throw new InvalidOperationException("The mapped PP-Structure result module does not match the stage. expected=" + Module + ";actual=" + result.Module);
            return result;
        }

        /// <summary>
        /// Creates a synchronous-preparation adapter without forcing callers to
        /// allocate a completed Task on every page.
        /// / 使用同步预处理委托创建适配器。
        /// </summary>
        public static PaddleDocumentVisualPipelineStage FromSynchronousPreparation(
            PaddleDocumentModule module,
            VisualPipeline pipeline,
            Func<PaddleDocumentPipelineContext, CancellationToken, PreparedVisualInput> prepare,
            Func<PaddleDocumentPipelineContext, VisualInferenceResult, PaddleDocumentModuleResult> map)
        {
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            return new PaddleDocumentVisualPipelineStage(module, pipeline, (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(prepare(context, token));
            }, map);
        }
    }
}
