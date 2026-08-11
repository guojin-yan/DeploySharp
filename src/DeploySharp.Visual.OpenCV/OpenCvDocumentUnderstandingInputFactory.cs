using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Core;

namespace JYPPX.DeploySharp.Visual.OpenCV
{
    /// <summary>Creates typed document pages from one OpenCV decode per PNG/JPEG/byte source. / 从每个 PNG/JPEG/Byte Source 的一次 OpenCV Decode 创建 Typed Document Page。</summary>
    public sealed class OpenCvDocumentUnderstandingInputFactory
    {
        /// <summary>Decodes one page once and applies the exact profile processor; OCR-free profiles reject layout and caller-owned OCR profiles require it. / 单次 Decode 一页并应用精确 Profile Processor；OCR-free Profile 拒绝 Layout，调用方 OCR Profile 要求 Layout。</summary>
        public PreparedDocumentPage CreatePage(OpenCvImageSource source, DocumentUnderstandingProfile profile, int pageIndex = 0, DocumentLayoutInput? layout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null || profile == null) throw new ArgumentNullException(source == null ? nameof(source) : nameof(profile));
            if (!profile.Executable && profile.Family != DocumentUnderstandingFamily.LayoutLmV3) throw new VisualException(VisualErrorCodes.DocumentUnderstandingCapabilityUnavailable, profile.Blocker ?? "The document processor is unavailable.", profileId: profile.ProfileId);
            if (pageIndex < 0 || pageIndex >= profile.Processor.MaximumPages) throw new VisualException(VisualErrorCodes.DocumentUnderstandingLimitExceeded, "Document page index exceeds profile capacity.", profileId: profile.ProfileId);
            if (source.Length > profile.Processor.MaximumImageBytes) throw new VisualException(VisualErrorCodes.DocumentUnderstandingLimitExceeded, "Encoded document page exceeds profile byte capacity.", profileId: profile.ProfileId);
            if (profile.OcrOwnership == DocumentOcrOwnership.NoneOcrFree && layout != null) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "OCR-free document processors reject OCR words and boxes.", profileId: profile.ProfileId);
            if (profile.OcrOwnership == DocumentOcrOwnership.Caller && layout == null) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "This document profile requires caller-owned OCR words, boxes, and alignment.", profileId: profile.ProfileId);
            if (profile.Processor.Mode != DocumentProcessorMode.DonutThumbnailPad) throw new VisualException(VisualErrorCodes.DocumentUnderstandingCapabilityUnavailable, "This adapter currently executes only the audited Donut thumbnail/pad path; LayoutLMv3 and Pix2Struct remain explicit contracts.", profileId: profile.ProfileId);
            OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
            OpenCvRuntimePreflight.Check();
            var watch = Stopwatch.StartNew();
            try
            {
                using (Mat decoded = OpenCvImageLoader.Decode(source))
                {
                    OpenCvImageLoader.Validate(decoded, source);
                    OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                    var sourceSize = new VisualSize(decoded.Cols, decoded.Rows);
                    var options = new OpenCvPreprocessOptions(sourceSize, OpenCvResizeMode.Resize, VisualColorOrder.Rgb, OpenCvAlphaMode.Drop, layout: VisualTensorLayout.Nchw, outputType: OpenCvOutputType.UInt8);
                    byte[] rgb = OpenCvVisualInputFactory.ConvertChannels(OpenCvVisualInputFactory.CopyRows(decoded), decoded.Cols, decoded.Rows, decoded.Channels, options);
                    int canvasWidth = profile.Processor.ModelSize.Width;
                    int canvasHeight = profile.Processor.ModelSize.Height;
                    int shortest = Math.Min(canvasWidth, canvasHeight);
                    int resizedWidth;
                    int resizedHeight;
                    if (sourceSize.Width <= sourceSize.Height)
                    {
                        resizedWidth = shortest;
                        resizedHeight = Math.Max(1, checked((int)Math.Floor((double)sourceSize.Height * shortest / sourceSize.Width)));
                    }
                    else
                    {
                        resizedHeight = shortest;
                        resizedWidth = Math.Max(1, checked((int)Math.Floor((double)sourceSize.Width * shortest / sourceSize.Height)));
                    }
                    byte[] resized = OpenCvVisualInputFactory.PillowBilinearResize(rgb, sourceSize.Width, sourceSize.Height, 3, resizedWidth, resizedHeight, cancellationToken);
                    int thumbnailHeight = Math.Min(resizedHeight, canvasHeight);
                    int thumbnailWidth = Math.Min(resizedWidth, canvasWidth);
                    if (resizedHeight > resizedWidth) thumbnailWidth = Math.Max(1, checked((int)((long)resizedWidth * thumbnailHeight / resizedHeight)));
                    else if (resizedWidth > resizedHeight) thumbnailHeight = Math.Max(1, checked((int)((long)resizedHeight * thumbnailWidth / resizedWidth)));
                    byte[] thumbnail = resizedWidth == thumbnailWidth && resizedHeight == thumbnailHeight ? resized : OpenCvVisualInputFactory.PillowBicubicResize(resized, resizedWidth, resizedHeight, 3, thumbnailWidth, thumbnailHeight, cancellationToken);
                    int left = (canvasWidth - thumbnailWidth) / 2;
                    int top = (canvasHeight - thumbnailHeight) / 2;
                    var pixels = new float[checked(3 * canvasHeight * canvasWidth)];
                    for (int index = 0; index < pixels.Length; index++) pixels[index] = -1f;
                    for (int y = 0; y < thumbnailHeight; y++)
                    {
                        if ((y & 31) == 0) OpenCvVisualInputFactory.ObserveCancellation(cancellationToken);
                        for (int x = 0; x < thumbnailWidth; x++)
                        {
                            int sourceOffset = ((y * thumbnailWidth) + x) * 3;
                            int spatial = ((top + y) * canvasWidth) + left + x;
                            int plane = canvasHeight * canvasWidth;
                            pixels[spatial] = (thumbnail[sourceOffset] / 127.5f) - 1f;
                            pixels[plane + spatial] = (thumbnail[sourceOffset + 1] / 127.5f) - 1f;
                            pixels[(2 * plane) + spatial] = (thumbnail[sourceOffset + 2] / 127.5f) - 1f;
                        }
                    }
                    var tensor = new Tensor<float>(new TensorShape(1, 3, canvasHeight, canvasWidth), pixels, TensorBufferOwnership.Transfer);
                    var modelSize = new VisualSize(canvasWidth, canvasHeight);
                    var transform = new ImageTransform(ImageTransformKind.Letterbox, sourceSize, modelSize, (float)thumbnailWidth / sourceSize.Width, (float)thumbnailHeight / sourceSize.Height, left, top);
                    var descriptor = new VisualPreprocessingDescriptor(VisualColorOrder.Rgb, new[] { 127.5f, 127.5f, 127.5f }, new[] { 1f / 127.5f, 1f / 127.5f, 1f / 127.5f }, "Official Donut: shortest-edge Pillow bilinear resize, Pillow bicubic thumbnail, centered zero pad, RGB [-1,1]; one OpenCV decode.");
                    var prepared = new PreparedVisualInput("pixel_values", tensor, sourceSize, modelSize, 1, VisualTensorLayout.Nchw, transform, descriptor, source.Sha256);
                    watch.Stop();
                    return new PreparedDocumentPage(profile.ProfileId, pageIndex, prepared, layout, watch.Elapsed);
                }
            }
            catch (OpenCvVisualException) { throw; }
            catch (OperationCanceledException exception) { throw new OpenCvVisualException(OpenCvErrorCodes.Cancelled, "Document preprocessing was cancelled.", exception); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw new OpenCvVisualException(OpenCvErrorCodes.OperationFailed, "The document page could not be prepared.", exception, "sourceKind=" + source.Kind); }
        }

        /// <summary>Creates one page from an absolute PNG/JPEG file. / 从绝对 PNG/JPEG 文件创建一页。</summary>
        public PreparedDocumentPage CreatePageFromFile(string path, DocumentUnderstandingProfile profile, int pageIndex = 0, DocumentLayoutInput? layout = null, CancellationToken cancellationToken = default(CancellationToken)) => CreatePage(OpenCvImageSource.FromFile(path), profile, pageIndex, layout, cancellationToken);
        /// <summary>Creates one page from copied PNG/JPEG bytes. / 从复制的 PNG/JPEG Byte 创建一页。</summary>
        public PreparedDocumentPage CreatePageFromBytes(byte[] bytes, DocumentUnderstandingProfile profile, int pageIndex = 0, DocumentLayoutInput? layout = null, CancellationToken cancellationToken = default(CancellationToken)) => CreatePage(OpenCvImageSource.FromBytes(bytes), profile, pageIndex, layout, cancellationToken);
        /// <summary>Creates an ordered multi-page document by decoding each supplied page exactly once; partial pages are disposed on failure. / 逐页 Exactly-once Decode 创建有序多页文档；失败时释放部分页面。</summary>
        public PreparedDocument CreateDocument(IEnumerable<OpenCvImageSource> pages, DocumentUnderstandingProfile profile, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (pages == null || profile == null) throw new ArgumentNullException(pages == null ? nameof(pages) : nameof(profile));
            var sources = pages.ToList();
            if (sources.Count == 0 || sources.Count > profile.Processor.MaximumPages) throw new VisualException(VisualErrorCodes.DocumentUnderstandingLimitExceeded, "Document page capacity was exceeded.", profileId: profile.ProfileId);
            var prepared = new List<PreparedDocumentPage>(sources.Count);
            try
            {
                for (int index = 0; index < sources.Count; index++) prepared.Add(CreatePage(sources[index], profile, index, cancellationToken: cancellationToken));
                return new PreparedDocument(profile, prepared);
            }
            catch { foreach (PreparedDocumentPage page in prepared) page.Dispose(); throw; }
        }
    }
}
