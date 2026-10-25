#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Routes each layout region to its selected VL task while retaining every original region index. / 将每个版面区域路由到对应的 VL 任务并保留所有原始区域索引。</summary>
    /// <remarks>Executes in source order and optionally computes a separate reading-order view. Skipped regions do not allocate inputs or call generation. Callbacks and sessions are caller-owned. / 按源顺序执行，可另行计算阅读顺序视图；跳过区域不分配输入、不生成；委托及会话由调用方拥有。</remarks>
    public sealed class PaddleOcrVlLayoutStage : IPaddleDocumentPipelineStage
    {
        private readonly PaddleDocumentModelDescriptor _model;
        private readonly string _backend;
        private readonly PaddleOcrVlTaskRouter _router;
        private readonly Func<PaddleDocumentPipelineContext, string> _pageInputIdentity;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, CancellationToken, Task<PaddleOcrVlPreparedInput>> _prepare;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> _generate;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? _inputIdentity;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? _fallback;
        private readonly bool _continueOnRegionFailure;
        private readonly PaddleDocumentReadingOrderOptions? _readingOrderOptions;
        private readonly IReadOnlyDictionary<PaddleOcrVlTask, PaddleDocumentModelDescriptor> _taskModels;

        /// <summary>Retains the original constructor signature with source-order output. / 保留使用源顺序输出的原构造函数签名。</summary>
        public PaddleOcrVlLayoutStage(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            Func<PaddleDocumentPipelineContext, string> pageInputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? inputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback,
            bool continueOnRegionFailure)
            : this(model, backend, router, pageInputIdentity, prepare, generate, inputIdentity, fallback, continueOnRegionFailure, null) { }

        /// <summary>Creates a category-routed VL stage; the aggregate model module must be VisionLanguageParsing. / 创建按类别路由的 VL 阶段；聚合模型模块必须为 VisionLanguageParsing。</summary>
        public PaddleOcrVlLayoutStage(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            Func<PaddleDocumentPipelineContext, string> pageInputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? inputIdentity = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false, PaddleDocumentReadingOrderOptions? readingOrderOptions = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (model.Module != Module) throw new ArgumentException("The aggregate model module must be VisionLanguageParsing.", nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            _backend = backend;
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _pageInputIdentity = pageInputIdentity ?? throw new ArgumentNullException(nameof(pageInputIdentity));
            _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            _generate = generate ?? throw new ArgumentNullException(nameof(generate));
            _inputIdentity = inputIdentity;
            _fallback = fallback;
            _continueOnRegionFailure = continueOnRegionFailure;
            _readingOrderOptions = readingOrderOptions;
            var models = new Dictionary<PaddleOcrVlTask, PaddleDocumentModelDescriptor>();
            foreach (PaddleOcrVlTask task in Enum.GetValues(typeof(PaddleOcrVlTask)))
                models.Add(task, new PaddleDocumentModelDescriptor(model.ModelId, model.OfficialName,
                    PaddleDocumentVisionLanguageResult.ModuleForTask(task), model.SourceUrl, model.ModelFormat, model.Status, model.ArtifactPath, model.Sha256));
            _taskModels = models;
        }

        /// <summary>Gets the aggregate VL parsing module. / 获取聚合 VL 解析模块。</summary>
        public PaddleDocumentModule Module => PaddleDocumentModule.VisionLanguageParsing;

        /// <summary>Retains the original Session factory signature with source-order output. / 保留使用源顺序输出的原 Session 工厂签名。</summary>
        public static PaddleOcrVlLayoutStage FromSession(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            PaddleOcrVlOnnxSession session, PaddleOcrVlTokenizer tokenizer, PaddleOcrVlGenerationOptions? generationOptions,
            Func<PaddleDocumentPipelineContext, string> pageInputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? inputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback,
            bool continueOnRegionFailure)
            => FromSession(model, backend, router, session, tokenizer, generationOptions, pageInputIdentity, prepare,
                inputIdentity, fallback, continueOnRegionFailure, null);

        /// <summary>Creates a layout stage around a caller-owned Session and tokenizer. / 使用调用方拥有的 Session 和 Tokenizer 创建版面阶段。</summary>
        public static PaddleOcrVlLayoutStage FromSession(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            PaddleOcrVlOnnxSession session, PaddleOcrVlTokenizer tokenizer, PaddleOcrVlGenerationOptions? generationOptions,
            Func<PaddleDocumentPipelineContext, string> pageInputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? inputIdentity = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false, PaddleDocumentReadingOrderOptions? readingOrderOptions = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            PaddleOcrVlGenerationOptions options = generationOptions ?? new PaddleOcrVlGenerationOptions();
            return new PaddleOcrVlLayoutStage(model, backend, router, pageInputIdentity, prepare,
                (context, region, index, input, token) => session.GenerateAsync(input, tokenizer, options, token),
                inputIdentity, fallback, continueOnRegionFailure, readingOrderOptions);
        }

        /// <summary>Executes mixed tasks sequentially within one page and reports skips/failures explicitly. / 在一页内依序执行混合任务并明确报告跳过和失败。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            PaddleDocumentRegionResult layout = context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection)
                ?? throw new InvalidOperationException("PaddleOCR-VL layout routing requires a preceding LayoutDetection result.");
            string pageSha = _pageInputIdentity(context);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(pageSha)) throw new InvalidOperationException("The page input identity callback returned an empty value.");
            var watch = Stopwatch.StartNew();
            IReadOnlyList<int> readingOrder = PaddleDocumentReadingOrder.GetIndices(layout.Regions, context.Page.SourceSize, _readingOrderOptions, cancellationToken);
            var executions = new List<PaddleDocumentVisionLanguageRegionExecution>(layout.Regions.Count);
            var warnings = new List<string> { "vl-regional-output-no-coordinates" };
            for (int index = 0; index < layout.Regions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PaddleDocumentRegion region = layout.Regions[index];
                if (!_router.TryGetTask(region.Category, out PaddleOcrVlTask task))
                {
                    executions.Add(PaddleDocumentVisionLanguageRegionExecution.CreateSkipped(index, region, "no-vl-task-for-layout-category"));
                    continue;
                }
                try
                {
                    PaddleDocumentVisionLanguageResult result = await PaddleOcrVlRegionExecutor.RunAsync(_taskModels[task], _backend, task,
                        context, region, index, (current, source, regionIndex, token) => _prepare(current, source, regionIndex, task, token),
                        _generate, _inputIdentity, _fallback == null ? null : (current, source, regionIndex, exception, token) =>
                            _fallback(current, source, regionIndex, task, exception, token), cancellationToken).ConfigureAwait(false);
                    executions.Add(new PaddleDocumentVisionLanguageRegionExecution(index, region, result));
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!_continueOnRegionFailure) throw;
                    warnings.Add("vl-region-failure:" + index);
                    executions.Add(new PaddleDocumentVisionLanguageRegionExecution(index, region, task,
                        exception.GetType().FullName ?? exception.GetType().Name,
                        exception.Message.Length == 0 ? "The region failed without a diagnostic message." : exception.Message));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            watch.Stop();
            return new PaddleDocumentVisionLanguageRoutedPageResult(
                new PaddleDocumentResultMetadata(_model, _backend, watch.Elapsed, pageSha, context.Page.PageIndex), executions, warnings, readingOrder);
        }
    }
}
#endif
