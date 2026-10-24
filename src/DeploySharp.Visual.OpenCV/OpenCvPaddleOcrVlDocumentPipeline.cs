#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Runs a real layout detector followed by category-routed VL generation, sharing one decoded image per page. / 运行真实版面检测，再按类别执行 VL 生成；每页共享一张解码底图。</summary>
    /// <remarks>Owns transient decoded images only; detector, VL Session and tokenizer remain caller-owned. Preserves detector order, not inferred reading order. Concurrent calls require thread-safe caller callbacks. / 仅拥有临时解码图像；检测器、VL Session 和 Tokenizer 由调用方拥有。保留检测器顺序，不推断阅读顺序。并发调用要求委托线程安全。</remarks>
    public sealed class OpenCvPaddleOcrVlDocumentPipeline
    {
        private readonly VisualPipeline _layout;
        private readonly PaddleDocumentProfile _layoutProfile;
        private readonly PaddleDocumentModelDescriptor _vlModel;
        private readonly string _backend;
        private readonly PaddleOcrVlTaskRouter _router;
        private readonly OpenCvPaddleOcrVlInputFactory _inputs;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> _generate;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? _fallback;
        private readonly int _maximumRegions;
        private readonly int _maximumPagePixels;
        private readonly int? _minimumPixels;
        private readonly int? _maximumPixels;
        private readonly bool _continueOnRegionFailure;

        /// <summary>Creates a detector/VL pipeline with explicit page and region budgets. Uses the detector's actual selected preprocessing profile. / 使用明确页面及区域预算创建检测/VL 流水线，采用检测器实际选择的预处理 Profile。</summary>
        public OpenCvPaddleOcrVlDocumentPipeline(VisualPipeline layout, PaddleDocumentModelDescriptor layoutModel,
            PaddleDocumentModelDescriptor vlModel, string backend,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlPreparedInput, CancellationToken, Task<PaddleOcrVlGenerationResult>> generate,
            PaddleOcrVlTaskRouter? router = null, OpenCvPaddleOcrVlInputFactory? inputFactory = null,
            int maximumRegions = 64, int maximumPagePixels = 128 * 1024 * 1024, int? minimumPixels = null, int? maximumPixels = null,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, int, PaddleOcrVlTask, Exception, CancellationToken, Task<PaddleOcrVlGenerationResult>>? fallback = null,
            bool continueOnRegionFailure = false)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            if (layoutModel == null) throw new ArgumentNullException(nameof(layoutModel));
            if (layoutModel.Module != PaddleDocumentModule.LayoutDetection || layout.Selection.Profile.Task != VisualTaskId.LayoutDetection)
                throw new ArgumentException("The detector must be a LayoutDetection pipeline.", nameof(layout));
            _layoutProfile = new PaddleDocumentProfile(layoutModel, layout.Selection.Profile);
            if (_layoutProfile.VisualProfile.Preprocessing == null) throw new ArgumentException("The detector must declare image preprocessing.", nameof(layout));
            _vlModel = vlModel ?? throw new ArgumentNullException(nameof(vlModel));
            if (vlModel.Module != PaddleDocumentModule.VisionLanguageParsing) throw new ArgumentException("The VL model module must be VisionLanguageParsing.", nameof(vlModel));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A VL backend is required.", nameof(backend));
            if (maximumRegions <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRegions));
            if (maximumPagePixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPagePixels));
            PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(28, 28), PaddleOcrVlTask.TextRecognition, minimumPixels, maximumPixels);
            _backend = backend;
            _generate = generate ?? throw new ArgumentNullException(nameof(generate));
            _router = router ?? PaddleOcrVlTaskRouter.CreatePpStructure();
            _inputs = inputFactory ?? new OpenCvPaddleOcrVlInputFactory();
            _maximumRegions = maximumRegions; _maximumPagePixels = maximumPagePixels;
            _minimumPixels = minimumPixels; _maximumPixels = maximumPixels;
            _fallback = fallback; _continueOnRegionFailure = continueOnRegionFailure;
        }

        /// <summary>Creates a detector/VL pipeline around a caller-owned Session and tokenizer. / 使用调用方拥有的 Session 和 Tokenizer 创建检测/VL 流水线。</summary>
        public static OpenCvPaddleOcrVlDocumentPipeline FromSession(VisualPipeline layout, PaddleDocumentModelDescriptor layoutModel,
            PaddleDocumentModelDescriptor vlModel, string backend, PaddleOcrVlOnnxSession session, PaddleOcrVlTokenizer tokenizer,
            PaddleOcrVlGenerationOptions? generationOptions = null, PaddleOcrVlTaskRouter? router = null,
            int maximumRegions = 64, int maximumPagePixels = 128 * 1024 * 1024, int? minimumPixels = null, int? maximumPixels = null,
            bool continueOnRegionFailure = false)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            PaddleOcrVlGenerationOptions options = generationOptions ?? new PaddleOcrVlGenerationOptions();
            return new OpenCvPaddleOcrVlDocumentPipeline(layout, layoutModel, vlModel, backend,
                (context, region, index, input, token) => session.GenerateAsync(input, tokenizer, options, token), router,
                maximumRegions: maximumRegions, maximumPagePixels: maximumPagePixels, minimumPixels: minimumPixels, maximumPixels: maximumPixels,
                continueOnRegionFailure: continueOnRegionFailure);
        }

        /// <summary>Infers source dimensions by decoding once, then detects and generates a page. / 解码一次取得源尺寸，然后执行版面检测和页面生成。</summary>
        public Task<PaddleDocumentPipelineResult> RunAsync(OpenCvImageSource source, int pageIndex = 0, CancellationToken cancellationToken = default(CancellationToken))
            => RunCoreAsync(source ?? throw new ArgumentNullException(nameof(source)), pageIndex, null, cancellationToken);

        /// <summary>Runs a declared page, requiring an OpenCvImageSource and matching decoded dimensions. / 运行已声明页面，要求源为 OpenCvImageSource 且尺寸与解码结果一致。</summary>
        public Task<PaddleDocumentPipelineResult> RunAsync(PaddleDocumentPage page, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (!(page.Source is OpenCvImageSource source)) throw new ArgumentException("The page source must be an OpenCvImageSource.", nameof(page));
            return RunCoreAsync(source, page.PageIndex, page.SourceSize, cancellationToken);
        }

        private async Task<PaddleDocumentPipelineResult> RunCoreAsync(OpenCvImageSource source, int pageIndex, VisualSize? declaredSize, CancellationToken token)
        {
            if (pageIndex < 0) throw new ArgumentOutOfRangeException(nameof(pageIndex));
            token.ThrowIfCancellationRequested();
            using (OpenCvDecodedRoiImage decoded = new OpenCvVisualInputFactory().DecodeForRois(source, token))
            {
                if (declaredSize.HasValue && declaredSize.Value != decoded.SourceSize) throw new InvalidOperationException("The declared page dimensions do not match the decoded image.");
                if ((long)decoded.SourceSize.Width * decoded.SourceSize.Height > _maximumPagePixels) throw new InvalidOperationException("The decoded page exceeds maximumPagePixels.");
                var detector = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(PaddleDocumentModule.LayoutDetection, _layout,
                    (context, current) => OpenCvPaddleDocumentPreprocessing.CreateFromPage(decoded, _layoutProfile, source.Sha256, current),
                    (context, inference) =>
                    {
                        DetectionResult detected = inference.GetValue<DetectionResult>();
                        if (detected.Detections.Count > _maximumRegions)
                            throw new InvalidOperationException("The detector exceeded maximumRegions before VL generation. count=" + detected.Detections.Count + ";limit=" + _maximumRegions);
                        return new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                            new PaddleDocumentResultMetadata(_layoutProfile.Descriptor, inference.BackendId.Value, inference.Timing.Total, source.Sha256, context.Page.PageIndex),
                            detected.Detections.Select((item, index) => new PaddleDocumentRegion(item.Label.Label, item.Label.Score, item.Box,
                                new Dictionary<string, string> { ["layoutModelRegionIndex"] = index.ToString(CultureInfo.InvariantCulture) })));
                    });
                var vl = new PaddleOcrVlLayoutStage(_vlModel, _backend, _router, _ => source.Sha256,
                    (context, region, index, task, current) => Task.FromResult(_inputs.CreateRegion(decoded, region.Bounds, task, _minimumPixels, _maximumPixels, current)),
                    _generate, fallback: _fallback, continueOnRegionFailure: _continueOnRegionFailure);
                return await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { detector, vl })
                    .RunAsync(new PaddleDocumentPage(source, decoded.SourceSize, pageIndex), token).ConfigureAwait(false);
            }
        }

        /// <summary>Runs bounded page workers, retaining caller order and releasing each decoded page on completion/failure/cancellation. / 运行有界页面任务，保持输入顺序，并在完成/失败/取消时释放各自解码底图。</summary>
        public async Task<IReadOnlyList<PaddleDocumentPipelineResult>> RunManyAsync(IReadOnlyList<PaddleDocumentPage> pages, int maximumConcurrency = 1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            if (maximumConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
            PaddleDocumentPage[] copied = pages.ToArray();
            foreach (PaddleDocumentPage page in copied)
                if (page == null || !(page.Source is OpenCvImageSource)) throw new ArgumentException("All pages must contain OpenCvImageSource objects.", nameof(pages));
            cancellationToken.ThrowIfCancellationRequested();
            var results = new PaddleDocumentPipelineResult[copied.Length];
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int next = -1;
            async Task Worker()
            {
                try
                {
                    while (true)
                    {
                        stop.Token.ThrowIfCancellationRequested();
                        int index = Interlocked.Increment(ref next);
                        if (index >= copied.Length) return;
                        results[index] = await RunAsync(copied[index], stop.Token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // A throwing cancellation callback must not hide the original page failure.
                    try { stop.Cancel(); } catch (AggregateException) { }
                    throw;
                }
            }
            Task[] workers = Enumerable.Range(0, Math.Min(copied.Length, maximumConcurrency))
                .Select(_ => Task.Run(Worker, CancellationToken.None)).ToArray();
            await Task.WhenAll(workers).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return Array.AsReadOnly(results);
        }
    }
}
#endif
