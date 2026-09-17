using System;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Bounds optional native-scale source-pixel diagnostics; no enhancement or rejection is applied. / 限制可选原始尺度源像素诊断，不执行增强或质量拒绝。</summary>
    public sealed class OcrPixelQualityOptions
    {
        /// <summary>Initializes per-area and per-call sampling bounds. / 初始化每区域和每次调用的采样限制。</summary>
        public OcrPixelQualityOptions(int maximumSamplesPerArea = 4096, int maximumRegions = 128, int maximumSamplesPerCall = 1048576)
        {
            if (maximumSamplesPerArea < 1 || maximumSamplesPerArea > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumSamplesPerArea));
            if (maximumRegions < 1 || maximumRegions > 4096) throw new ArgumentOutOfRangeException(nameof(maximumRegions));
            if (maximumSamplesPerCall < maximumSamplesPerArea || maximumSamplesPerCall > 16777216) throw new ArgumentOutOfRangeException(nameof(maximumSamplesPerCall));
            MaximumSamplesPerArea = maximumSamplesPerArea; MaximumRegions = maximumRegions; MaximumSamplesPerCall = maximumSamplesPerCall;
        }
        /// <summary>Gets maximum attempted sample centers per source/region. / 获取每个源图或区域最多尝试的采样中心数。</summary>
        public int MaximumSamplesPerArea { get; }
        /// <summary>Gets maximum detected regions; excess fails without truncation. / 获取最大检测区域数，超限失败而非截断。</summary>
        public int MaximumRegions { get; }
        /// <summary>Gets the conservative per-call center budget, including the source image. / 获取包含源图的每次调用保守采样中心预算。</summary>
        public int MaximumSamplesPerCall { get; }
    }

    /// <summary>Contains immutable sampled source-pixel evidence, not OCR accuracy or calibrated quality scores. / 包含不可变源像素采样证据，不表示 OCR 准确率或标定质量评分。</summary>
    public sealed class OcrPixelQualityDiagnostics
    {
        internal OcrPixelQualityDiagnostics(VisualSize size, TextRegion? region, int planned, int samples, int neighborhoods,
            double mean, double variance, double dark, double light, double laplacianVariance, double gradient)
        {
            InputSize = size; InputPolygon = region?.Polygon; InputRegionIndex = region?.SourceIndex;
            PlannedSampleCount = planned; SampleCount = samples; NeighborhoodCount = neighborhoods;
            MeanLuminance = samples == 0 ? (double?)null : mean;
            LuminanceStandardDeviation = samples == 0 ? (double?)null : Math.Sqrt(Math.Max(0, variance));
            DarkPixelFraction = samples == 0 ? (double?)null : dark / samples;
            LightPixelFraction = samples == 0 ? (double?)null : light / samples;
            LaplacianVariance = neighborhoods < 2 ? (double?)null : Math.Max(0, laplacianVariance);
            MeanAbsoluteGradient = neighborhoods == 0 ? (double?)null : gradient / neighborhoods;
            if (region != null)
            {
                double shortest = double.MaxValue;
                var points = region.Polygon.Vertices;
                for (int i = 0; i < points.Count; i++)
                {
                    PointF a = points[i], b = points[(i + 1) % points.Count];
                    double x = (double)b.X - a.X, y = (double)b.Y - a.Y;
                    shortest = Math.Min(shortest, Math.Sqrt(x * x + y * y));
                }
                RegionMinimumEdgePixels = shortest;
            }
        }
        /// <summary>Gets the evaluation coordinate-space size, retained after ROI/orientation projection. / 获取评估坐标空间尺寸，ROI 或方向投影后保留。</summary>
        public VisualSize InputSize { get; }
        /// <summary>Gets the evaluated source polygon; null denotes the whole image. / 获取评估源多边形，null 表示整图。</summary>
        public TextPolygon? InputPolygon { get; }
        /// <summary>Gets the original evaluation region index, not a later merged index. / 获取原评估区域索引，而非后续合并索引。</summary>
        public int? InputRegionIndex { get; }
        /// <summary>Gets attempted grid centers before polygon exclusion. / 获取多边形排除前尝试的网格中心数。</summary>
        public int PlannedSampleCount { get; }
        /// <summary>Gets accepted source-pixel centers. / 获取有效源像素采样中心数。</summary>
        public int SampleCount { get; }
        /// <summary>Gets centers whose four native one-pixel neighbors are also inside. / 获取原始一步像素的四邻域均在区域内的中心数。</summary>
        public int NeighborhoodCount { get; }
        /// <summary>Gets mean 8-bit luminance; null means no samples. / 获取 8 位亮度均值，无样本时为 null。</summary>
        public double? MeanLuminance { get; }
        /// <summary>Gets population luminance standard deviation in 8-bit units. / 获取 8 位单位的亮度总体标准差。</summary>
        public double? LuminanceStandardDeviation { get; }
        /// <summary>Gets fraction of samples at or below luminance 8, not proof of underexposure. / 获取亮度不高于 8 的样本比例，不作为欠曝证明。</summary>
        public double? DarkPixelFraction { get; }
        /// <summary>Gets fraction of samples at or above luminance 247, not proof of overexposure. / 获取亮度不低于 247 的样本比例，不作为过曝证明。</summary>
        public double? LightPixelFraction { get; }
        /// <summary>Gets native four-neighbor Laplacian population variance; null needs more neighborhoods. / 获取原始四邻域拉普拉斯总体方差，邻域不足时为 null。</summary>
        public double? LaplacianVariance { get; }
        /// <summary>Gets mean absolute central gradient, not a noise estimator. / 获取中心差分绝对梯度均值，不作为噪声估计。</summary>
        public double? MeanAbsoluteGradient { get; }
        /// <summary>Gets the polygon shortest edge in input pixels, not glyph height. / 获取输入像素中的多边形最短边，不表示字形高度。</summary>
        public double? RegionMinimumEdgePixels { get; }
    }

    /// <summary>Optionally reads decoded source pixels without changing the existing OCR input contract. / 可选读取解码源像素，不更改既有 OCR 输入合同。</summary>
    public interface IOcrPixelQualityInput : IOcrImageInput
    {
        /// <summary>Assesses source pixels inside a region, or the whole image for null; no image may be retained in the result. / 评估区域内源像素，null 表示整图；结果不得持有图像。</summary>
        public OcrPixelQualityDiagnostics AssessPixelQuality(TextRegion? region, OcrPixelQualityOptions options, CancellationToken cancellationToken);
    }

    /// <summary>Samples deterministic source-pixel centers without resampling or copying the image. / 确定性采样源像素中心，不重采样或复制图像。</summary>
    public static class OcrPixelQualityAnalyzer
    {
        /// <summary>Reads at most five luminance values per center; the reader must return stable 0–255 luminance until completion. / 每中心最多读取五个亮度值，读取器须在完成前提供稳定的 0～255 亮度。</summary>
        public static OcrPixelQualityDiagnostics Analyze(VisualSize size, Func<int, int, byte> readLuminance,
            OcrPixelQualityOptions? options = null, TextRegion? region = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (size.Width < 1 || size.Height < 1) throw new ArgumentOutOfRangeException(nameof(size));
            if (readLuminance == null) throw new ArgumentNullException(nameof(readLuminance));
            options = options ?? new OcrPixelQualityOptions();
            cancellationToken.ThrowIfCancellationRequested();
            RectangleF? box = region?.Polygon.AxisAlignedBounds;
            int left = box.HasValue ? Bound(Math.Floor(box.Value.X), size.Width) : 0;
            int top = box.HasValue ? Bound(Math.Floor(box.Value.Y), size.Height) : 0;
            int right = box.HasValue ? Bound(Math.Ceiling((double)box.Value.X + box.Value.Width), size.Width) : size.Width;
            int bottom = box.HasValue ? Bound(Math.Ceiling((double)box.Value.Y + box.Value.Height), size.Height) : size.Height;
            int width = right - left, height = bottom - top;
            int columns = 0, rows = 0;
            if (width > 0 && height > 0)
            {
                double scale = Math.Max(1, Math.Sqrt((double)width * height / options.MaximumSamplesPerArea));
                columns = Math.Min(options.MaximumSamplesPerArea, Math.Max(1, (int)(width / scale)));
                rows = Math.Min(options.MaximumSamplesPerArea / columns, Math.Max(1, (int)(height / scale)));
            }
            int count = 0, neighbors = 0, dark = 0, light = 0;
            double mean = 0, m2 = 0, lapMean = 0, lapM2 = 0, gradient = 0;
            for (int row = 0; row < rows; row++)
            {
                int y = top + (int)(((2L * row + 1) * height) / (2L * rows));
                for (int col = 0; col < columns; col++)
                {
                    if ((col & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                    int x = left + (int)(((2L * col + 1) * width) / (2L * columns));
                    if (!Inside(region, x, y, out bool neighborhoodInside)) continue;
                    int value = readLuminance(x, y);
                    count++; double delta = value - mean; mean += delta / count; m2 += delta * (value - mean);
                    if (value <= 8) dark++; if (value >= 247) light++;
                    if (x == 0 || y == 0 || x == size.Width - 1 || y == size.Height - 1 || !neighborhoodInside) continue;
                    int l = readLuminance(x - 1, y), r = readLuminance(x + 1, y), u = readLuminance(x, y - 1), d = readLuminance(x, y + 1);
                    int lap = l + r + u + d - 4 * value;
                    neighbors++; double lapDelta = lap - lapMean; lapMean += lapDelta / neighbors; lapM2 += lapDelta * (lap - lapMean);
                    gradient += (Math.Abs(r - l) + Math.Abs(d - u)) / 4.0;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new OcrPixelQualityDiagnostics(size, region, columns * rows, count, neighbors, mean, count == 0 ? 0 : m2 / count,
                dark, light, neighbors == 0 ? 0 : lapM2 / neighbors, gradient);
        }
        private static int Bound(double value, int limit) => (int)Math.Max(0, Math.Min(limit, value));
        private static bool Inside(TextRegion? region, int x, int y, out bool neighborhoodInside)
        {
            neighborhoodInside = true;
            if (region == null) return true;
            var vertices = region.Polygon.Vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                PointF a = vertices[i], b = vertices[(i + 1) % vertices.Count];
                double dx = (double)b.X - a.X, dy = (double)b.Y - a.Y;
                double cross = dx * (y + .5 - a.Y) - dy * (x + .5 - a.X);
                if (cross < 0) return false;
                // All four one-pixel neighbors stay inside this half-plane iff the
                // center margin exceeds its largest axis change; no four extra polygon walks.
                // 中心到半平面的裕量覆盖最大轴向变化时，四邻域均在内，无需重复遍历四次多边形。
                if (cross < Math.Max(Math.Abs(dx), Math.Abs(dy))) neighborhoodInside = false;
            }
            return true;
        }
    }
}
