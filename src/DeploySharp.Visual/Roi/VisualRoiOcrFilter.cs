using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents one complete OCR region retained by ROI filtering. / 表示 ROI 过滤后保留的完整 OCR 区域。</summary>
    public sealed class RoiOcrRegion
    {
        private readonly IReadOnlyList<string> _roiIds;

        /// <summary>Initializes an OCR region with deterministic ROI provenance. / 使用确定性的 ROI 来源初始化 OCR 区域。</summary>
        public RoiOcrRegion(OcrRegionResult region, IEnumerable<string> roiIds)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            if (roiIds == null) throw new ArgumentNullException(nameof(roiIds));
            _roiIds = new ReadOnlyCollection<string>(roiIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList());
        }

        /// <summary>Gets the original source-space OCR region and recognized text. / 获取原始源图空间 OCR 区域及识别文本。</summary>
        public OcrRegionResult Region { get; }
        /// <summary>Gets matching Include ROI identifiers. / 获取命中的 Include ROI 标识。</summary>
        public IReadOnlyList<string> RoiIds => _roiIds;
        /// <summary>Gets the deterministic primary ROI identifier. / 获取确定性的主 ROI 标识。</summary>
        public string? PrimaryRoiId => _roiIds.Count == 0 ? null : _roiIds[0];
    }

    /// <summary>Contains a complete OCR result filtered by ROI while preserving timing and model provenance. / 包含经 ROI 过滤的完整 OCR 结果，并保留时序和模型来源。</summary>
    public sealed class RoiOcrResult
    {
        private readonly IReadOnlyList<RoiOcrRegion> _regions;

        internal RoiOcrResult(OcrResult result, long snapshotVersion, IEnumerable<RoiOcrRegion> regions)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (snapshotVersion <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            SourceSize = result.SourceSize;
            SnapshotVersion = snapshotVersion;
            _regions = new ReadOnlyCollection<RoiOcrRegion>(regions.ToList());
            Result = new OcrResult(_regions.Select(value => value.Region), result.SourceSize, result.DetectionProfileId, result.DetectionModelId, result.RecognitionProfileId, result.RecognitionModelId, result.Timing, result.Orientation);
        }

        /// <summary>Gets the source image dimensions. / 获取源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the ROI snapshot version used for filtering. / 获取过滤所用 ROI 快照版本。</summary>
        public long SnapshotVersion { get; }
        /// <summary>Gets retained OCR regions in original reading order. / 获取按原始阅读顺序保留的 OCR 区域。</summary>
        public IReadOnlyList<RoiOcrRegion> Items => _regions;
        /// <summary>Gets the canonical OCR result without ROI wrappers. / 获取不带 ROI 包装的规范 OCR 结果。</summary>
        public OcrResult Result { get; }
    }

    /// <summary>Filters complete OCR results using the detector polygon ROI contract. / 使用文本检测多边形 ROI 合同过滤完整 OCR 结果。</summary>
    public static class VisualRoiOcrFilter
    {
        /// <summary>Filters detection/recognition pairs once, without rerunning the recognizer. / 只过滤一次检测/识别对，不重新运行识别器。</summary>
        public static RoiOcrResult Filter(OcrResult result, VisualRoiSnapshot snapshot, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var detections = new TextDetectionResult(result.Regions.Select(value => value.Region), result.SourceSize, result.DetectionProfileId, result.DetectionModelId);
            RoiTextDetectionResult filtered = VisualRoiTextDetectionFilter.Filter(detections, snapshot, thresholdOverride, coordinateContext);
            var accepted = new Dictionary<TextRegion, IReadOnlyList<string>>();
            foreach (RoiTextRegion item in filtered.Items) accepted[item.Region] = item.RoiIds;
            var regions = result.Regions
                .Where(value => accepted.ContainsKey(value.Region))
                .Select(value => new RoiOcrRegion(value, accepted[value.Region]));
            return new RoiOcrResult(result, snapshot.Version, regions);
        }
    }
}
