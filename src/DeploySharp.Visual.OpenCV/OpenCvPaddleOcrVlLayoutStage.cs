#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Decodes a page once and routes its RGB crops to task-specific VL generation. / 每页解码一次，并将 RGB 裁剪路由到对应的 VL 生成任务。</summary>
    /// <remarks>Retains original indices and optionally computes a reading-order view, including skips. Does not detect layout. The session/tokenizer remain caller-owned. / 保留原索引，可计算包含跳过项的阅读顺序视图；不执行版面检测，Session/Tokenizer 仍由调用方拥有。</remarks>
    public sealed class OpenCvPaddleOcrVlLayoutStage : IPaddleDocumentPipelineStage
    {
        private readonly PaddleDocumentModelDescriptor _model;
        private readonly string _backend;
        private readonly PaddleOcrVlTaskRouter _router;
        private readonly Func<PaddleDocumentPage, OpenCvImageSource> _sourceFactory;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> _generate;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? _fallback;
        private readonly bool _continueOnRegionFailure;
        private readonly PaddleDocumentReadingOrderOptions? _readingOrderOptions;
        private readonly OpenCvPaddleOcrVlInputFactory _inputFactory;
        private readonly int? _minimumPixels;
        private readonly int? _maximumPixels;

        /// <summary>Retains the original constructor signature with source-order output. / 保留使用源顺序输出的原构造函数签名。</summary>
        public OpenCvPaddleOcrVlLayoutStage(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            Func<PaddleDocumentPage, OpenCvImageSource> sourceFactory,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            OpenCvPaddleOcrVlInputFactory? inputFactory, int? minimumPixels, int? maximumPixels,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback,
            bool continueOnRegionFailure)
            : this(model, backend, router, sourceFactory, generate, inputFactory, minimumPixels, maximumPixels, fallback, continueOnRegionFailure, null) { }

        /// <summary>Creates a mixed-task stage with a caller-owned VL generator and immutable category router. / 使用调用方拥有的 VL 生成器及不可变类别路由器创建混合任务阶段。</summary>
        public OpenCvPaddleOcrVlLayoutStage(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            Func<PaddleDocumentPage, OpenCvImageSource> sourceFactory,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            OpenCvPaddleOcrVlInputFactory? inputFactory = null, int? minimumPixels = null, int? maximumPixels = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false, PaddleDocumentReadingOrderOptions? readingOrderOptions = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (model.Module != Module) throw new ArgumentException("The aggregate model module must be VisionLanguageParsing.", nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(28, 28), PaddleOcrVlTask.TextRecognition, minimumPixels, maximumPixels);
            _backend = backend;
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
            _generate = generate ?? throw new ArgumentNullException(nameof(generate));
            _inputFactory = inputFactory ?? new OpenCvPaddleOcrVlInputFactory();
            _minimumPixels = minimumPixels;
            _maximumPixels = maximumPixels;
            _fallback = fallback;
            _continueOnRegionFailure = continueOnRegionFailure;
            _readingOrderOptions = readingOrderOptions;
        }

        /// <summary>Gets the mixed-task VL parsing module. / 获取混合任务 VL 解析模块。</summary>
        public PaddleDocumentModule Module => PaddleDocumentModule.VisionLanguageParsing;

        /// <summary>Retains the original Session factory signature with source-order output. / 保留使用源顺序输出的原 Session 工厂签名。</summary>
        public static OpenCvPaddleOcrVlLayoutStage FromSession(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            PaddleOcrVlOnnxSession session, PaddleOcrVlTokenizer tokenizer, Func<PaddleDocumentPage, OpenCvImageSource> sourceFactory,
            PaddleOcrVlGenerationOptions? generationOptions, OpenCvPaddleOcrVlInputFactory? inputFactory,
            int? minimumPixels, int? maximumPixels,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback,
            bool continueOnRegionFailure)
            => FromSession(model, backend, router, session, tokenizer, sourceFactory, generationOptions, inputFactory,
                minimumPixels, maximumPixels, fallback, continueOnRegionFailure, null);

        /// <summary>Creates a layout stage using a caller-owned Session and tokenizer. / 使用调用方拥有的 Session 和 Tokenizer 创建版面阶段。</summary>
        public static OpenCvPaddleOcrVlLayoutStage FromSession(PaddleDocumentModelDescriptor model, string backend, PaddleOcrVlTaskRouter router,
            PaddleOcrVlOnnxSession session, PaddleOcrVlTokenizer tokenizer, Func<PaddleDocumentPage, OpenCvImageSource> sourceFactory,
            PaddleOcrVlGenerationOptions? generationOptions = null, OpenCvPaddleOcrVlInputFactory? inputFactory = null,
            int? minimumPixels = null, int? maximumPixels = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false, PaddleDocumentReadingOrderOptions? readingOrderOptions = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            PaddleOcrVlGenerationOptions options = generationOptions ?? new PaddleOcrVlGenerationOptions();
            return new OpenCvPaddleOcrVlLayoutStage(model, backend, router, sourceFactory,
                (context, region, index, input, token) => session.GenerateAsync(input, tokenizer, options, token),
                inputFactory, minimumPixels, maximumPixels, fallback, continueOnRegionFailure, readingOrderOptions);
        }

        /// <summary>Holds one decoded page while all mapped regions are prepared and generated. / 在准备并生成全部已映射区域期间持有一张解码底图。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            if (context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection) == null)
                throw new InvalidOperationException("PaddleOCR-VL layout routing requires a preceding LayoutDetection result.");
            OpenCvImageSource source = _sourceFactory(context.Page) ?? throw new InvalidOperationException("The OpenCV source callback returned null.");
            using (OpenCvDecodedRoiImage page = new OpenCvVisualInputFactory().DecodeForRois(source, cancellationToken))
            {
                if (page.SourceSize != context.Page.SourceSize)
                    throw new InvalidOperationException("The decoded image dimensions do not match the document page dimensions.");
                var stage = new PaddleOcrVlLayoutStage(_model, _backend, _router, _ => source.Sha256,
                    (current, region, index, task, token) => Task.FromResult(_inputFactory.CreateRegion(page, region.Bounds,
                        task, _minimumPixels, _maximumPixels, token)), _generate, fallback: _fallback,
                    continueOnRegionFailure: _continueOnRegionFailure, readingOrderOptions: _readingOrderOptions);
                return await stage.RunAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
#endif
