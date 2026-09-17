using System;
using System.Collections.Generic;
using System.Threading;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Enables bounded diagnostics of rectified and resized recognition content. / 启用校正后与缩放后识别内容的有界诊断。</summary>
    public sealed class OcrCropProcessingOptions
    {
        /// <summary>Initializes per-stage sampling and physical-crop limits including padded rows and retries. / 初始化每阶段采样与物理裁剪限制，包含填充行和重试。</summary>
        public OcrCropProcessingOptions(int maximumSamplesPerStage = 1024, int maximumCropsPerCall = 1024)
        {
            if (maximumSamplesPerStage < 1 || maximumSamplesPerStage > 65536) throw new ArgumentOutOfRangeException(nameof(maximumSamplesPerStage));
            if (maximumCropsPerCall < 1 || maximumCropsPerCall > 4096) throw new ArgumentOutOfRangeException(nameof(maximumCropsPerCall));
            MaximumSamplesPerStage = maximumSamplesPerStage; MaximumCropsPerCall = maximumCropsPerCall;
        }
        /// <summary>Gets the maximum centers at each sampled crop stage. / 获取每个裁剪采样阶段的最大中心数。</summary>
        public int MaximumSamplesPerStage { get; }
        /// <summary>Gets physical crops across the original recognition and retries. / 获取初次识别及重试的物理裁剪总上限。</summary>
        public int MaximumCropsPerCall { get; }
    }

    /// <summary>Records crop-local evidence with its original source request; no pixel buffers are retained. / 记录裁剪局部证据及原始源请求，不持有像素缓冲。</summary>
    public sealed class OcrCropDiagnostics
    {
        /// <summary>Initializes evidence before normalization and tensor padding. / 初始化归一化及张量补齐之前的证据。</summary>
        public OcrCropDiagnostics(TextCropRequest request, OcrPixelQualityDiagnostics rectified, OcrPixelQualityDiagnostics content)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Rectified = rectified ?? throw new ArgumentNullException(nameof(rectified));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            if (rectified.InputPolygon != null || content.InputPolygon != null || rectified.InputRegionIndex != null || content.InputRegionIndex != null ||
                content.InputSize.Height != request.TargetHeight || content.InputSize.Width > request.TargetWidth)
                throw new ArgumentException("Crop diagnostics must use local full-content coordinates before padding.");
            InputRegionIndex = request.Region.SourceIndex; InputQuadrilateral = request.Quadrilateral;
            Orientation = request.Region.Orientation; TensorSize = new VisualSize(request.TargetWidth, request.TargetHeight);
        }
        /// <summary>Gets the evaluation source index, unchanged after ROI renumbering. / 获取评估源索引，ROI 重新编号后不变。</summary>
        public int InputRegionIndex { get; }
        /// <summary>Gets actual source corners, including a window's crop. / 获取实际源角点，包含窗口自身的裁剪。</summary>
        public TextQuadrilateral InputQuadrilateral { get; }
        /// <summary>Gets applied right-angle orientation. / 获取应用的直角方向。</summary>
        public TextOrientation Orientation { get; }
        /// <summary>Gets padded tensor dimensions, distinct from content dimensions. / 获取补齐张量尺寸，与有效内容尺寸区分。</summary>
        public VisualSize TensorSize { get; }
        /// <summary>Gets statistics after geometric warp/rotation and before resize. / 获取几何校正及旋转后、缩放前的统计。</summary>
        public OcrPixelQualityDiagnostics Rectified { get; }
        /// <summary>Gets statistics after resize, excluding tensor padding/normalization. / 获取缩放后统计，不含张量补齐及归一化。</summary>
        public OcrPixelQualityDiagnostics Content { get; }
    }

    /// <summary>Owns a prepared recognition batch and immutable per-row crop evidence. / 拥有准备好的识别批次与不可变逐行裁剪证据。</summary>
    public sealed class OcrPreparedCropBatch : IDisposable
    {
        /// <summary>Transfers input ownership on successful construction only. / 仅在构造成功时转移输入所有权。</summary>
        public OcrPreparedCropBatch(PreparedVisualInput input, IReadOnlyList<OcrCropDiagnostics> diagnostics)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            if (diagnostics.Count != input.BatchSize) throw new ArgumentException("One crop diagnostic is required per physical batch row.", nameof(diagnostics));
            var copy = new List<OcrCropDiagnostics>(diagnostics.Count);
            foreach (OcrCropDiagnostics item in diagnostics) copy.Add(item ?? throw new ArgumentException("Crop diagnostics cannot contain null.", nameof(diagnostics)));
            Diagnostics = copy.AsReadOnly();
        }
        /// <summary>Gets the owned prepared input. / 获取自有准备输入。</summary>
        public PreparedVisualInput Input { get; }
        /// <summary>Gets evidence in physical batch order including repeated padding rows. / 获取物理批次顺序的证据，包含补齐重复行。</summary>
        public IReadOnlyList<OcrCropDiagnostics> Diagnostics { get; }
        /// <summary>Releases only the input; evidence remains readable. / 仅释放输入，证据仍可读取。</summary>
        public void Dispose() => Input.Dispose();
    }

    /// <summary>Optionally prepares recognition crops with bounded diagnostics. / 可选准备带有界诊断的识别裁剪。</summary>
    public interface IOcrCropProcessingInput : IOcrImageInput
    {
        /// <summary>Returns an owned batch with one evidence item per request; callers must dispose it. / 返回每请求一份证据的自有批次，调用方必须释放。</summary>
        public OcrPreparedCropBatch PrepareProcessedRecognitionBatch(string inputName, IReadOnlyList<TextCropRequest> requests, CancellationToken cancellationToken);
    }
}
