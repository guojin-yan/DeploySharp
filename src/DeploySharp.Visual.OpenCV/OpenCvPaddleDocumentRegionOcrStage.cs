using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Runs the existing recognizer-only OcrPipeline on layout regions while decoding/preparing the page once. / 仅对版面区域运行已有 OcrPipeline 识别器，页面只解码和准备一次。</summary>
    public sealed class OpenCvPaddleDocumentRegionOcrStage : IPaddleDocumentPipelineStage
    {
        private readonly OcrPipeline _ocrPipeline;
        private readonly OpenCvOcrImageInputFactory _inputFactory;
        private readonly Func<PaddleDocumentPage, OpenCvImageSource> _sourceFactory;
        private readonly OpenCvPreprocessOptions _detectionOptions;
        private readonly PaddleDocumentModelDescriptor _model;
        private readonly string _backend;

        /// <summary>Initializes a region OCR stage. The OcrPipeline remains caller-owned. / 初始化区域 OCR 阶段；OcrPipeline 仍由调用方拥有。</summary>
        public OpenCvPaddleDocumentRegionOcrStage(
            OcrPipeline ocrPipeline,
            Func<PaddleDocumentPage, OpenCvImageSource> sourceFactory,
            OpenCvPreprocessOptions detectionOptions,
            PaddleDocumentModelDescriptor model,
            string backend = "opencv-dnn-cpu",
            OpenCvOcrImageInputFactory? inputFactory = null)
        {
            _ocrPipeline = ocrPipeline ?? throw new ArgumentNullException(nameof(ocrPipeline));
            _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
            _detectionOptions = detectionOptions ?? throw new ArgumentNullException(nameof(detectionOptions));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            _backend = backend;
            _inputFactory = inputFactory ?? new OpenCvOcrImageInputFactory();
            Module = PaddleDocumentModule.TextRecognition;
        }

        /// <summary>Gets the text-recognition module. / 获取文本识别模块。</summary>
        public PaddleDocumentModule Module { get; }

        /// <summary>Decodes/prepares the source once, submits all layout regions to recognition-only OCR, and maps text back to page bounds. / 解码并准备源图一次，将所有版面区域送入识别器-only OCR，并将文本映射回页面区域。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            PaddleDocumentRegionResult layout = context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection)
                ?? throw new InvalidOperationException("Region OCR requires a preceding LayoutDetection result.");
            OpenCvImageSource source = _sourceFactory(context.Page) ?? throw new InvalidOperationException("The OpenCV source callback returned null.");
            Stopwatch watch = Stopwatch.StartNew();
            var items = new List<PaddleDocumentTextItem>(layout.Regions.Count);
            using (OpenCvOcrImageInput input = _inputFactory.Create(source, _ocrPipeline.DetectionSelection.Profile.Input.Name, _detectionOptions, source.Sha256, cancellationToken))
            {
                var textRegions = new List<TextRegion>(layout.Regions.Count);
                for (int index = 0; index < layout.Regions.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PaddleDocumentRegion region = layout.Regions[index];
                    RectangleF clipped = Clip(region.Bounds, input.SourceSize.Width, input.SourceSize.Height);
                    PointF topLeft = new PointF(clipped.X, clipped.Y);
                    PointF topRight = new PointF(clipped.Right, clipped.Y);
                    PointF bottomRight = new PointF(clipped.Right, clipped.Bottom);
                    PointF bottomLeft = new PointF(clipped.X, clipped.Bottom);
                    TextQuadrilateral quad = new TextQuadrilateral(topLeft, topRight, bottomRight, bottomLeft, TextCornerOrder.TopLeftClockwise);
                    textRegions.Add(new TextRegion(index, region.Score, quad.Polygon, quad));
                }
                IReadOnlyList<OcrRegionResult> recognized = await _ocrPipeline.RecognizeOnlyAsync(input, textRegions, cancellationToken).ConfigureAwait(false);
                for (int index = 0; index < recognized.Count; index++)
                {
                    OcrRegionResult value = recognized[index];
                    var metadata = new Dictionary<string, string> { ["recognizerOnly"] = "true" };
                    items.Add(new PaddleDocumentTextItem(index, value.Recognition.Text, value.Recognition.Confidence, layout.Regions[index].Bounds, metadata));
                }
            }
            watch.Stop();
            var resultMetadata = new PaddleDocumentResultMetadata(_model, _backend, watch.Elapsed, source.Sha256, context.Page.PageIndex);
            return new PaddleDocumentTextResult(resultMetadata, items);
        }

        private static RectangleF Clip(RectangleF bounds, int width, int height)
        {
            int left = Math.Max(0, Math.Min(width - 1, (int)Math.Floor(bounds.X)));
            int top = Math.Max(0, Math.Min(height - 1, (int)Math.Floor(bounds.Y)));
            int right = Math.Max(left + 1, Math.Min(width, (int)Math.Ceiling(bounds.X + bounds.Width)));
            int bottom = Math.Max(top + 1, Math.Min(height, (int)Math.Ceiling(bounds.Y + bounds.Height)));
            return new RectangleF(left, top, right - left, bottom - top);
        }
    }
}
