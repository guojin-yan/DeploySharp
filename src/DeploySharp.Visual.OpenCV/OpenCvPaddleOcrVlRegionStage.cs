#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Runs a VL task on layout regions, decoding each page once and retaining its original provenance. / 每页只解码一次，对版面区域执行 VL 任务并保留原始来源。</summary>
    /// <remarks>The adapter owns the decoded page only during RunAsync. The caller owns the generator/session. Concurrent pages get independent decoded owners; one shared VL Session still queues generation. / 适配器仅在 RunAsync 期间拥有已解码页面；生成器/Session 由调用方拥有。并发页面使用独立底图，一个共享 VL Session 的生成仍排队执行。</remarks>
    public sealed class OpenCvPaddleOcrVlRegionStage : IPaddleDocumentPipelineStage
    {
        private readonly PaddleDocumentModelDescriptor _model;
        private readonly string _backend;
        private readonly PaddleOcrVlTask _task;
        private readonly Func<PaddleDocumentPage, OpenCvImageSource> _sourceFactory;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> _generate;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? _fallback;
        private readonly bool _continueOnRegionFailure;
        private readonly OpenCvPaddleOcrVlInputFactory _inputFactory;
        private readonly int? _minimumPixels;
        private readonly int? _maximumPixels;

        /// <summary>Creates a region stage with a caller-owned asynchronous VL generator. / 使用调用方拥有的异步 VL 生成器创建区域阶段。</summary>
        public OpenCvPaddleOcrVlRegionStage(PaddleDocumentModelDescriptor model, string backend,
            PaddleOcrVlTask task, Func<PaddleDocumentPage, OpenCvImageSource> sourceFactory,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            OpenCvPaddleOcrVlInputFactory? inputFactory = null, int? minimumPixels = null, int? maximumPixels = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            Module = PaddleDocumentVisionLanguageResult.ModuleForTask(task);
            if (model.Module != Module) throw new ArgumentException("The model module does not match the PaddleOCR-VL task.", nameof(model));
            // Validate budgets immediately, rather than failing once per region.
            PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(28, 28), task, minimumPixels, maximumPixels);
            _backend = backend;
            _task = task;
            _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
            _generate = generate ?? throw new ArgumentNullException(nameof(generate));
            _inputFactory = inputFactory ?? new OpenCvPaddleOcrVlInputFactory();
            _minimumPixels = minimumPixels;
            _maximumPixels = maximumPixels;
            _fallback = fallback;
            _continueOnRegionFailure = continueOnRegionFailure;
        }

        /// <summary>Gets the document module associated with the configured VL task. / 获取配置的 VL 任务对应的文档模块。</summary>
        public PaddleDocumentModule Module { get; }

        /// <summary>Creates a region stage around a caller-owned production Session and tokenizer. / 使用调用方拥有的生产 Session 和 Tokenizer 创建区域阶段。</summary>
        public static OpenCvPaddleOcrVlRegionStage FromSession(PaddleDocumentModelDescriptor model, string backend,
            PaddleOcrVlTask task, PaddleOcrVlOnnxSession session, PaddleOcrVlTokenizer tokenizer,
            Func<PaddleDocumentPage, OpenCvImageSource> sourceFactory, PaddleOcrVlGenerationOptions? generationOptions = null,
            OpenCvPaddleOcrVlInputFactory? inputFactory = null, int? minimumPixels = null, int? maximumPixels = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            PaddleOcrVlGenerationOptions options = generationOptions ?? new PaddleOcrVlGenerationOptions();
            return new OpenCvPaddleOcrVlRegionStage(model, backend, task, sourceFactory,
                (context, region, index, input, token) => session.GenerateAsync(input, tokenizer, options, token),
                inputFactory, minimumPixels, maximumPixels, fallback, continueOnRegionFailure);
        }

        /// <summary>Decodes one page, prepares its actual RGB crops and routes each region through the VL generator. / 解码一页，准备实际 RGB 裁剪并将每个区域送入 VL 生成器。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            if (context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection) == null)
                throw new InvalidOperationException("PaddleOCR-VL region routing requires a preceding LayoutDetection result.");
            OpenCvImageSource source = _sourceFactory(context.Page) ?? throw new InvalidOperationException("The OpenCV source callback returned null.");
            using (OpenCvDecodedRoiImage page = new OpenCvVisualInputFactory().DecodeForRois(source, cancellationToken))
            {
                if (page.SourceSize != context.Page.SourceSize)
                    throw new InvalidOperationException("The decoded image dimensions do not match the document page dimensions.");
                var stage = new PaddleOcrVlRegionStage(_model, _backend, _task, _ => source.Sha256,
                    (current, region, index, token) => Task.FromResult(_inputFactory.CreateRegion(page, region.Bounds,
                        _task, _minimumPixels, _maximumPixels, token)), _generate, fallback: _fallback,
                    continueOnRegionFailure: _continueOnRegionFailure);
                return await stage.RunAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
#endif
