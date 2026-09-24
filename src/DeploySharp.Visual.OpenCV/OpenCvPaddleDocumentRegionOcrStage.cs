using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Runs the existing OcrPipeline on one encoded crop per layout region and maps text back to page bounds. / 对每个版面区域创建真实图像裁剪，调用现有 OcrPipeline，并将文本映射回页面区域。</summary>
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

        /// <summary>Decodes the source once, crops each layout region, and runs the caller-owned OCR pipeline. / 解码源图一次，裁剪每个版面区域并运行调用方拥有的 OCR Pipeline。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            PaddleDocumentRegionResult layout = context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection)
                ?? throw new InvalidOperationException("Region OCR requires a preceding LayoutDetection result.");
            OpenCvImageSource source = _sourceFactory(context.Page) ?? throw new InvalidOperationException("The OpenCV source callback returned null.");
            Stopwatch watch = Stopwatch.StartNew();
            var items = new List<PaddleDocumentTextItem>(layout.Regions.Count);
            using (Mat decoded = OpenCvImageLoader.Decode(source))
            {
                OpenCvImageLoader.Validate(decoded, source);
                for (int index = 0; index < layout.Regions.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PaddleDocumentRegion region = layout.Regions[index];
                    RectangleF clipped = Clip(region.Bounds, decoded.Cols, decoded.Rows);
                    using Mat view = decoded.SubMat(new Rect((int)clipped.X, (int)clipped.Y, (int)clipped.Width, (int)clipped.Height));
                    byte[] encoded = ImageCodecs.ImEncode(".png", view);
                    using var input = _inputFactory.Create(
                        OpenCvImageSource.FromBytes(encoded),
                        _ocrPipeline.DetectionSelection.Profile.Input.Name,
                        _detectionOptions,
                        inputId: source.Sha256 + ":layout-region:" + index);
                    OcrResult local = await _ocrPipeline.RunAsync(input, cancellationToken: cancellationToken).ConfigureAwait(false);
                    string text = string.Join(" ", local.Regions.Select(item => item.Recognition.Text).Where(value => !string.IsNullOrWhiteSpace(value)));
                    float confidence = local.Regions.Count == 0 ? 0f : local.Regions.Average(item => item.Recognition.Confidence);
                    var metadata = new Dictionary<string, string>
                    {
                        ["localRegionCount"] = local.Regions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["cropSourceSha256"] = OpenCvImageSource.FromBytes(encoded).Sha256
                    };
                    items.Add(new PaddleDocumentTextItem(index, text, confidence, clipped, metadata));
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
