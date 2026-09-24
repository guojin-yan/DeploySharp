using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Runs caller-owned OCR preparation/inference for each preceding layout region and binds text back to page coordinates. / 对前序版面区域运行调用方拥有的 OCR 准备/推理，并将文本绑定回页面坐标。</summary>
    public sealed class PaddleDocumentRegionTextStage : IPaddleDocumentPipelineStage
    {
        private readonly PaddleDocumentModelDescriptor _model;
        private readonly string _backend;
        private readonly Func<PaddleDocumentPipelineContext, string> _inputSha256;
        private readonly Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, CancellationToken, Task<PaddleDocumentTextItem>> _recognize;

        /// <summary>Initializes a region text stage with caller-owned recognition preparation and inference. / 使用调用方拥有的区域识别准备和推理委托初始化区域文本阶段。</summary>
        public PaddleDocumentRegionTextStage(
            PaddleDocumentModelDescriptor model,
            string backend,
            Func<PaddleDocumentPipelineContext, string> inputSha256,
            Func<PaddleDocumentPipelineContext, PaddleDocumentRegion, CancellationToken, Task<PaddleDocumentTextItem>> recognize)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(backend)) throw new ArgumentException("A backend is required.", nameof(backend));
            _backend = backend;
            _inputSha256 = inputSha256 ?? throw new ArgumentNullException(nameof(inputSha256));
            _recognize = recognize ?? throw new ArgumentNullException(nameof(recognize));
            Module = PaddleDocumentModule.TextRecognition;
        }

        /// <summary>Gets the text-recognition module identifier. / 获取文本识别模块标识。</summary>
        public PaddleDocumentModule Module { get; }

        /// <summary>Runs recognition for each preceding layout region in page order. / 按页面顺序为前序版面区域运行识别。</summary>
        public async Task<PaddleDocumentModuleResult> RunAsync(PaddleDocumentPipelineContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            PaddleDocumentRegionResult layout = context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection)
                ?? throw new InvalidOperationException("Region text recognition requires a preceding LayoutDetection result.");
            string inputSha = _inputSha256(context);
            if (string.IsNullOrWhiteSpace(inputSha)) throw new InvalidOperationException("The region text stage input SHA-256 callback returned an empty value.");
            var items = new List<PaddleDocumentTextItem>(layout.Regions.Count);
            Stopwatch watch = Stopwatch.StartNew();
            for (int index = 0; index < layout.Regions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PaddleDocumentRegion region = layout.Regions[index];
                PaddleDocumentTextItem item = await _recognize(context, region, cancellationToken).ConfigureAwait(false);
                if (item == null) throw new InvalidOperationException("The region text callback returned null.");
                if (item.RegionIndex != index) throw new InvalidOperationException("The region text callback returned a mismatched region index. expected=" + index + ";actual=" + item.RegionIndex);
                items.Add(item);
            }
            watch.Stop();
            var metadata = new PaddleDocumentResultMetadata(_model, _backend, watch.Elapsed, inputSha, context.Page.PageIndex);
            return new PaddleDocumentTextResult(metadata, items);
        }
    }
}
