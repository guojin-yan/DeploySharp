#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Routes layout regions through one PaddleOCR-VL task and preserves page-space provenance. / 将版面区域路由到一项 PaddleOCR-VL 任务并保留页面坐标来源。</summary>
    /// <remarks>Regions are processed in layout order. The preparation callback owns crop/ROI materialization and must set a source SHA on each prepared input or provide an input-identity callback. The VL task output itself still has no model-returned coordinates. / 区域按版面顺序处理；预处理委托负责裁剪/ROI，并且必须为每个输入设置源 SHA 或提供身份回调。VL 任务输出本身仍不包含模型返回的坐标。</remarks>
    public sealed class PaddleOcrVlRegionStage : IPaddleDocumentPipelineStage
    {
        private readonly PaddleDocumentModelDescriptor _model;
        private readonly string _backend;
        private readonly PaddleOcrVlTask _task;
        private readonly Func<PaddleDocumentPipelineContext, string> _pageInputIdentity;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, CancellationToken, Task<PaddleOcrVlPreparedInput>> _prepare;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> _generate;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? _inputIdentity;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? _fallback;
        private readonly bool _continueOnRegionFailure;

        /// <summary>Initializes a region-routing stage with caller-owned preparation and generation. / 使用调用方拥有的预处理和生成委托初始化区域路由阶段。</summary>
        public PaddleOcrVlRegionStage(
            PaddleDocumentModelDescriptor model,
            string backend,
            PaddleOcrVlTask task,
            Func<PaddleDocumentPipelineContext, string> pageInputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? inputIdentity = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            if (!Enum.IsDefined(typeof(PaddleOcrVlTask), task)) throw new ArgumentOutOfRangeException(nameof(task));
            if (model.Module != PaddleDocumentVisionLanguageResult.ModuleForTask(task))
                throw new ArgumentException("The model module does not match the PaddleOCR-VL task.", nameof(model));
            _backend = backend;
            _task = task;
            _pageInputIdentity = pageInputIdentity ?? throw new ArgumentNullException(nameof(pageInputIdentity));
            _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            _generate = generate ?? throw new ArgumentNullException(nameof(generate));
            _inputIdentity = inputIdentity;
            _fallback = fallback;
            _continueOnRegionFailure = continueOnRegionFailure;
            Module = PaddleDocumentVisionLanguageResult.ModuleForTask(task);
        }

        /// <summary>Gets the existing document module used by this task. / 获取本任务使用的现有文档模块。</summary>
        public PaddleDocumentModule Module { get; }

        /// <summary>Creates a region stage around one production VL Session. / 使用一个生产 VL Session 创建区域阶段。</summary>
        public static PaddleOcrVlRegionStage FromSession(
            PaddleDocumentModelDescriptor model,
            string backend,
            PaddleOcrVlTask task,
            PaddleOcrVlOnnxSession session,
            PaddleOcrVlTokenizer tokenizer,
            PaddleOcrVlGenerationOptions? generationOptions,
            Func<PaddleDocumentPipelineContext, string> pageInputIdentity,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, CancellationToken, Task<PaddleOcrVlPreparedInput>> prepare,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, string>? inputIdentity = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            PaddleOcrVlGenerationOptions options = generationOptions ?? new PaddleOcrVlGenerationOptions();
            return new PaddleOcrVlRegionStage(model, backend, task, pageInputIdentity, prepare,
                (context, region, index, input, cancellationToken) => session.GenerateAsync(input, tokenizer, options, cancellationToken),
                inputIdentity, fallback, continueOnRegionFailure);
        }

        /// <summary>Runs all preceding layout regions in order, optionally isolating individual failures. / 按顺序运行所有前置版面区域，并可选择隔离单个区域失败。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            PaddleDocumentRegionResult layout = context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection)
                ?? throw new InvalidOperationException("PaddleOCR-VL region routing requires a preceding LayoutDetection result.");
            string pageSha = _pageInputIdentity(context);
            if (string.IsNullOrWhiteSpace(pageSha)) throw new InvalidOperationException("The page input identity callback returned an empty value.");

            Stopwatch watch = Stopwatch.StartNew();
            var executions = new List<PaddleDocumentVisionLanguageRegionExecution>(layout.Regions.Count);
            var warnings = new List<string> { "vl-regional-output-no-coordinates" };
            for (int index = 0; index < layout.Regions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PaddleDocumentRegion region = layout.Regions[index];
                try
                {
                    ValidateRegion(region, context.Page.SourceSize, index);
                    PaddleOcrVlPreparedInput? prepared = await _prepare(context, region, index, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (prepared == null) throw new InvalidOperationException("The PaddleOCR-VL region preparation callback returned null.");
                    if (prepared.ImagePlan.Task != _task)
                        throw new InvalidOperationException("The prepared PaddleOCR-VL task does not match the region stage.");
                    PaddleOcrVlGenerationResult generation;
                    bool usedFallback = false;
                    try
                    {
                        generation = await _generate(context, region, index, prepared, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (generation == null) throw new InvalidOperationException("The PaddleOCR-VL region generation callback returned null.");
                    }
                    catch (Exception exception) when (!cancellationToken.IsCancellationRequested && _fallback != null)
                    {
                        generation = await _fallback(context, region, index, exception, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (generation == null) throw new InvalidOperationException("The PaddleOCR-VL region fallback callback returned null.");
                        usedFallback = true;
                    }

                    if (generation.Task != _task)
                        throw new InvalidOperationException("The PaddleOCR-VL generation task does not match the region stage. expected=" + _task + ";actual=" + generation.Task);
                    if (prepared.SourceSha256 != null && generation.SourceSha256 != null
                        && !string.Equals(prepared.SourceSha256, generation.SourceSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The PaddleOCR-VL generation source does not match the prepared region.");
                    string? inputSha = _inputIdentity?.Invoke(context, region, index, prepared) ?? prepared.SourceSha256 ?? generation.SourceSha256;
                    if (string.IsNullOrWhiteSpace(inputSha)) throw new InvalidOperationException("PaddleOCR-VL region results require a source/input SHA-256.");
                    var resultWarnings = new List<string> { "vl-regional-output-no-coordinates" };
                    if (generation.FinishReason == GenerationFinishReason.MaxTokens) resultWarnings.Add("missing-eos:sequence-may-be-truncated");
                    if (usedFallback) resultWarnings.Add("vl-region-fallback-used");
                    var metadata = new PaddleDocumentResultMetadata(_model, _backend, generation.Elapsed, inputSha, context.Page.PageIndex);
                    var result = new PaddleDocumentVisionLanguageResult(_task, metadata, generation.Text, generation.TokenIds, generation.FinishReason,
                        resultWarnings, regionIndex: index, sourceRegion: region);
                    executions.Add(new PaddleDocumentVisionLanguageRegionExecution(index, region, result));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!_continueOnRegionFailure) throw;
                    warnings.Add("vl-region-failure:" + index);
                    executions.Add(new PaddleDocumentVisionLanguageRegionExecution(index, region,
                        exception.GetType().FullName ?? exception.GetType().Name,
                        exception.Message.Length == 0 ? "The region failed without a diagnostic message." : exception.Message));
                }
            }
            watch.Stop();
            var pageMetadata = new PaddleDocumentResultMetadata(_model, _backend, watch.Elapsed, pageSha, context.Page.PageIndex);
            return new PaddleDocumentVisionLanguagePageResult(_task, pageMetadata, executions, warnings);
        }

        private static void ValidateRegion(PaddleDocumentRegion region, VisualSize sourceSize, int index)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (float.IsNaN(region.Bounds.X) || float.IsInfinity(region.Bounds.X) || float.IsNaN(region.Bounds.Y) || float.IsInfinity(region.Bounds.Y)
                || float.IsNaN(region.Bounds.Width) || float.IsInfinity(region.Bounds.Width) || float.IsNaN(region.Bounds.Height) || float.IsInfinity(region.Bounds.Height)
                || region.Bounds.Width <= 0 || region.Bounds.Height <= 0)
                throw new InvalidOperationException("The layout region is not a finite positive rectangle. index=" + index);
            if (region.Bounds.Right <= 0 || region.Bounds.Bottom <= 0 || region.Bounds.X >= sourceSize.Width || region.Bounds.Y >= sourceSize.Height)
                throw new InvalidOperationException("The layout region does not intersect the page. index=" + index);
        }
    }
}
#endif
