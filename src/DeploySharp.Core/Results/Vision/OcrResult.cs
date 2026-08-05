using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>
    /// Represents one recognized text region and polygon. / 表示一个识别出的文本区域及多边形。
    /// </summary>
    public sealed class TextRegion
    {
        private readonly IReadOnlyList<PointF> _polygon;

        /// <summary>Initializes a recognized text region. / 初始化识别文本区域。</summary>
        public TextRegion(IEnumerable<PointF> polygon, string text, float score)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (float.IsNaN(score) || float.IsInfinity(score)) throw new ArgumentOutOfRangeException(nameof(score));
            var points = new List<PointF>(polygon);
            if (points.Count < 3) throw new ArgumentException("A text polygon requires at least three points.", nameof(polygon));
            _polygon = points.AsReadOnly();
            Text = Guard.NotNullOrWhiteSpace(text, nameof(text));
            Score = score;
        }

        /// <summary>Gets the source-image polygon. / 获取源图像多边形。</summary>
        public IReadOnlyList<PointF> Polygon => _polygon;

        /// <summary>Gets recognized text. / 获取识别文本。</summary>
        public string Text { get; }

        /// <summary>Gets the recognition score. / 获取识别分数。</summary>
        public float Score { get; }
    }

    /// <summary>
    /// Contains OCR regions in reading order. / 按阅读顺序包含 OCR 区域。
    /// </summary>
    public sealed class OcrResult
    {
        private readonly IReadOnlyList<TextRegion> _regions;

        /// <summary>Initializes an OCR result. / 初始化 OCR 结果。</summary>
        public OcrResult(IEnumerable<TextRegion> regions)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            var values = new List<TextRegion>();
            foreach (TextRegion region in regions)
            {
                if (region == null) throw new ArgumentException("Regions cannot contain null values.", nameof(regions));
                values.Add(region);
            }

            _regions = values.AsReadOnly();
        }

        /// <summary>Gets recognized regions in reading order. / 按阅读顺序获取识别区域。</summary>
        public IReadOnlyList<TextRegion> Regions => _regions;
    }
}
