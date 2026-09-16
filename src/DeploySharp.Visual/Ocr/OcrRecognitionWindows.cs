using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Bounds long-line recognition and conservative token seam matching. / 限制长行识别与保守的 token 接缝匹配。</summary>
    public sealed class OcrRecognitionWindowOptions
    {
        /// <summary>Initializes window bounds; overlap must be positive and at most one half. / 初始化窗口边界；重叠比例必须为正且最多为一半。</summary>
        public OcrRecognitionWindowOptions(double overlapRatio = 0.2, int maximumWindowsPerRegion = 32, int maximumWindowsPerImage = 1024, int maximumMergedCharacters = 16384, int maximumOverlapTokens = 64, int minimumOverlapTokens = 2, int maximumMergedTimesteps = 65536)
        {
            if (!(overlapRatio > 0 && overlapRatio <= 0.5)) throw new ArgumentOutOfRangeException(nameof(overlapRatio));
            if (maximumWindowsPerRegion < 2 || maximumWindowsPerRegion > 256) throw new ArgumentOutOfRangeException(nameof(maximumWindowsPerRegion));
            if (maximumWindowsPerImage < 1 || maximumWindowsPerImage > 4096) throw new ArgumentOutOfRangeException(nameof(maximumWindowsPerImage));
            if (maximumMergedCharacters < 1 || maximumMergedCharacters > 65536) throw new ArgumentOutOfRangeException(nameof(maximumMergedCharacters));
            if (minimumOverlapTokens < 1 || maximumOverlapTokens < minimumOverlapTokens || maximumOverlapTokens > 256) throw new ArgumentOutOfRangeException(nameof(maximumOverlapTokens));
            if (maximumMergedTimesteps < 1 || maximumMergedTimesteps > 262144) throw new ArgumentOutOfRangeException(nameof(maximumMergedTimesteps));
            OverlapRatio = overlapRatio;
            MaximumWindowsPerRegion = maximumWindowsPerRegion;
            MaximumWindowsPerImage = maximumWindowsPerImage;
            MaximumMergedCharacters = maximumMergedCharacters;
            MaximumOverlapTokens = maximumOverlapTokens;
            MinimumOverlapTokens = minimumOverlapTokens;
            MaximumMergedTimesteps = maximumMergedTimesteps;
        }

        /// <summary>Gets the fraction of each window retained as the next window's context. / 获取保留为下一窗口上下文的窗口比例。</summary>
        public double OverlapRatio { get; }
        /// <summary>Gets the maximum windows for one detected line. / 获取单个检测行的最大窗口数。</summary>
        public int MaximumWindowsPerRegion { get; }
        /// <summary>Gets the total window limit, including unsliced lines, before REC allocation. / 获取 REC 分配前包括未切片行的窗口总数限制。</summary>
        public int MaximumWindowsPerImage { get; }
        /// <summary>Gets the maximum merged UTF-16 character count per line. / 获取每行合并后的最大 UTF-16 字符数。</summary>
        public int MaximumMergedCharacters { get; }
        /// <summary>Gets the bounded suffix/prefix search length in tokens. / 获取以 token 计的有界后缀/前缀搜索长度。</summary>
        public int MaximumOverlapTokens { get; }
        /// <summary>Gets the minimum exact matching token count before deduplication. / 获取去重所需的最少完全匹配 token 数。</summary>
        public int MinimumOverlapTokens { get; }
        /// <summary>Gets the total raw timestep limit per merged line, including blank and suppressed tokens. / 获取每个合并行包括 blank 与被抑制 token 的原始时间步总数限制。</summary>
        public int MaximumMergedTimesteps { get; }
    }

    /// <summary>Describes a source-space crop and its normalized horizontal interval after orientation. / 描述源图裁剪及定向后归一化水平区间。</summary>
    public sealed class OcrRecognitionWindow
    {
        internal OcrRecognitionWindow(int index, double start, double end, TextCropRequest crop)
        { Index = index; Start = start; End = end; Crop = crop; }
        /// <summary>Gets zero-based position within the original detected line. / 获取原检测行内的从零开始的序号。</summary>
        public int Index { get; }
        /// <summary>Gets the inclusive start in oriented rectified-line coordinates. / 获取定向后校正行坐标中的包含起点。</summary>
        public double Start { get; }
        /// <summary>Gets the end in oriented rectified-line coordinates; the last window ends at one. / 获取定向后校正行坐标中的终点；最后一个窗口终点为一。</summary>
        public double End { get; }
        /// <summary>Gets the bounded source crop, consumable by existing CPU/GPU image adapters. / 获取既有 CPU/GPU 图像适配器可使用的有界源图裁剪。</summary>
        public TextCropRequest Crop { get; }
    }

    /// <summary>Plans bounded windows without allocating images or tensors. / 规划有界窗口，不分配图像或张量。</summary>
    public static class OcrRecognitionWindowPlanner
    {
        /// <summary>Plans recognition crops in reading direction, preserving unsliced requests exactly. / 按阅读方向规划识别裁剪，完整保留未切片请求。</summary>
        public static IReadOnlyList<OcrRecognitionWindow> Plan(TextRegion region, TextCropProfile profile, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            cancellationToken.ThrowIfCancellationRequested();
            TextQuadrilateral corners = region.CropQuadrilateral ?? throw new VisualException(VisualErrorCodes.CapabilityUnavailable, "Window recognition requires explicit quadrilateral corner roles.", profileId: profile.ProfileId);
            OcrRecognitionWidthInfo width = profile.DescribeWidth(corners, region.Orientation);
            if (profile.OverflowMode != RecognitionOverflowMode.SlidingWindow || !width.WidthClamped)
                return Array.AsReadOnly(new[] { new OcrRecognitionWindow(0, 0, 1, new TextCropRequest(region, profile)) });

            // A homography, not linear source-edge interpolation, partitions perspective-distorted lines.
            // 使用单应变换划分透视行，而不是在源图边上做线性插值。
            var unit = new VisualSize(1, 1);
            ImageTransform transform = ImageTransform.Perspective(unit, unit,
                new[] { corners.TopLeft, corners.TopRight, corners.BottomRight, corners.BottomLeft },
                new[] { new PointF(0, 0), new PointF(1, 0), new PointF(1, 1), new PointF(0, 1) });
            var windows = new List<OcrRecognitionWindow>();
            double start = 0;
            double nominalSpan = (double)width.TargetWidth / width.NaturalWidth;
            while (start < 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (windows.Count >= profile.RecognitionWindows.MaximumWindowsPerRegion)
                    throw Limit(region, "Recognition windows exceed the per-region limit.");
                // Leave a small geometric margin for float coordinates and ceil(width).
                double end = Math.Min(1, start + nominalSpan * 0.99);
                TextRegion? slice = null;
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    var quad = new TextQuadrilateral(Map(transform, region.Orientation, start, 0), Map(transform, region.Orientation, end, 0),
                        Map(transform, region.Orientation, end, 1), Map(transform, region.Orientation, start, 1), TextCornerOrder.TopLeftClockwise);
                    if (!profile.DescribeWidth(quad, TextOrientation.Degrees0).WidthClamped)
                    {
                        slice = new TextRegion(region.SourceIndex, region.Score, quad.Polygon, quad, TextOrientation.Degrees0,
                            region.AngleRadians, region.Language, region.Script, region.ExternalId, region.Metadata);
                        break;
                    }
                    end = start + (end - start) * 0.5;
                }
                if (slice == null || end - start < 1e-8) throw Limit(region, "The recognition window cannot satisfy width and geometry bounds.");
                windows.Add(new OcrRecognitionWindow(windows.Count, start, end, new TextCropRequest(slice, profile)));
                if (end >= 1) break;
                start = end - (end - start) * profile.RecognitionWindows.OverlapRatio;
            }
            return windows.AsReadOnly();
        }

        private static PointF Map(ImageTransform transform, TextOrientation orientation, double x, double y)
        {
            switch (orientation)
            {
                case TextOrientation.Clockwise90: return transform.ToSource(new PointF((float)y, (float)(1 - x)));
                case TextOrientation.CounterClockwise90: return transform.ToSource(new PointF((float)(1 - y), (float)x));
                case TextOrientation.Degrees180: return transform.ToSource(new PointF((float)(1 - x), (float)(1 - y)));
                default: return transform.ToSource(new PointF((float)x, (float)y));
            }
        }

        internal static OcrPipelineException Limit(TextRegion region, string message)
            => new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, message, OcrPipelineStage.CropAndBatch, regionIndex: region.SourceIndex);
    }

    /// <summary>Retains raw per-window tokens and conservative seam decisions, without image ownership. / 保留逐窗口原始 token 与保守接缝决策，不持有图像。</summary>
    public sealed class OcrRecognitionWindowResult
    {
        /// <summary>Initializes the raw result for one planned window and its actual batch width. / 使用实际批宽度初始化一个已规划窗口的原始结果。</summary>
        public OcrRecognitionWindowResult(OcrRecognitionWindow window, RecognizedText recognition, OcrRecognitionWidthInfo width)
            : this(window?.Index ?? throw new ArgumentNullException(nameof(window)), window.Start, window.End, recognition, width, 0, false)
        {
            if (recognition.SourceRegionIndex != window.Crop.Region.SourceIndex) throw new ArgumentException("The recognition belongs to another region.", nameof(recognition));
            if (width.NaturalWidth != window.Crop.WidthInfo.NaturalWidth || width.TargetWidth != window.Crop.WidthInfo.TargetWidth || width.WindowCount != 1)
                throw new ArgumentException("Width provenance does not match the planned crop.", nameof(width));
        }

        internal OcrRecognitionWindowResult(int index, double start, double end, RecognizedText recognition, OcrRecognitionWidthInfo width, int removedPrefixTokens, bool seamUncertain)
        { Index = index; Start = start; End = end; Recognition = recognition ?? throw new ArgumentNullException(nameof(recognition)); Width = width; RemovedPrefixTokens = removedPrefixTokens; SeamUncertain = seamUncertain; }
        /// <summary>Gets the original window index. / 获取原窗口序号。</summary>
        public int Index { get; }
        /// <summary>Gets the normalized rectified start. / 获取归一化校正起点。</summary>
        public double Start { get; }
        /// <summary>Gets the normalized rectified end. / 获取归一化校正终点。</summary>
        public double End { get; }
        /// <summary>Gets the raw recognition, including its unchanged local CTC trace. / 获取包括未改变的局部 CTC 追踪的原始识别结果。</summary>
        public RecognizedText Recognition { get; }
        /// <summary>Gets actual per-window width and batch padding. / 获取实际逐窗口宽度与批填充。</summary>
        public OcrRecognitionWidthInfo Width { get; }
        /// <summary>Gets emitted prefix tokens removed after exact overlap matching. / 获取完全重叠匹配后移除的已发射前缀 token 数。</summary>
        public int RemovedPrefixTokens { get; }
        /// <summary>Gets whether no reliable seam match was found; unmatched text is retained, not discarded. / 获取是否未找到可靠接缝匹配；保留未匹配文本而不丢弃。</summary>
        public bool SeamUncertain { get; }
    }

    /// <summary>Merges bounded CTC windows without running CTC collapse across independent windows. / 合并有界 CTC 窗口，不跨独立窗口执行 CTC 折叠。</summary>
    public static class OcrRecognitionWindowMerger
    {
        /// <summary>Merges exact token overlaps only inside shared geometric intervals, retaining uncertain seams for review. / 仅在共享几何区间内合并完全匹配 token 重叠，保留不确定接缝供复核。</summary>
        public static OcrRegionResult Merge(TextRegion region, TextCropProfile profile, IReadOnlyList<OcrRecognitionWindowResult> windows, CtcConfidenceAggregation confidenceAggregation = CtcConfidenceAggregation.Mean, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (windows == null) throw new ArgumentNullException(nameof(windows));
            if (!Enum.IsDefined(typeof(CtcConfidenceAggregation), confidenceAggregation)) throw new ArgumentOutOfRangeException(nameof(confidenceAggregation));
            if (windows.Count < 2 || windows.Count > profile.RecognitionWindows.MaximumWindowsPerRegion) throw new ArgumentOutOfRangeException(nameof(windows));
            long totalTimesteps = 0;
            foreach (OcrRecognitionWindowResult window in windows)
            {
                if (window == null) throw new ArgumentException("Windows cannot contain null.", nameof(windows));
                totalTimesteps += window.Recognition.Tokens.Count;
                if (totalTimesteps > profile.RecognitionWindows.MaximumMergedTimesteps)
                    throw new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, "Merged OCR trace exceeds its timestep limit.", OcrPipelineStage.Merge, regionIndex: region.SourceIndex);
            }
            var trace = new List<OcrToken>();
            var diagnostics = new List<OcrRecognitionWindowResult>(windows.Count);
            var text = new StringBuilder();
            RecognizedText first = windows[0].Recognition;
            List<OcrToken>? previous = null;
            int emitted = 0;
            int targetWidth = 0;
            int tensorWidth = 0;
            double sum = 0;
            double logSum = 0;
            float minimum = 1;
            for (int index = 0; index < windows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OcrRecognitionWindowResult window = windows[index];
                RecognizedText raw = window.Recognition;
                if (window.Index != index || raw.SourceRegionIndex != region.SourceIndex || raw.CharacterSetSha256 != first.CharacterSetSha256
                    || raw.CharacterSetId != first.CharacterSetId || raw.CharacterSetVersion != first.CharacterSetVersion
                    || window.Width.WidthClamped || (index == 0 && window.Start != 0) || (index == windows.Count - 1 && window.End != 1)
                    || (index > 0 && (window.Start <= windows[index - 1].Start || window.Start >= windows[index - 1].End || window.End <= windows[index - 1].End)))
                    throw new ArgumentException("Window order, coverage, dictionary or width provenance is inconsistent.", nameof(windows));
                var current = new List<OcrToken>();
                var rawText = new StringBuilder();
                for (int tokenIndex = 0; tokenIndex < raw.Tokens.Count; tokenIndex++)
                {
                    if ((tokenIndex & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                    OcrToken token = raw.Tokens[tokenIndex];
                    if (token.Timestep != tokenIndex) throw new ArgumentException("Window merging requires a complete ordered CTC trace.", nameof(windows));
                    if (token.Emitted)
                    {
                        if ((long)rawText.Length + token.Text!.Length > profile.RecognitionWindows.MaximumMergedCharacters)
                            throw new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, "OCR window text exceeds its character limit.", OcrPipelineStage.Merge, regionIndex: region.SourceIndex);
                        current.Add(token); rawText.Append(token.Text);
                    }
                }
                if (!string.Equals(rawText.ToString(), raw.Text, StringComparison.Ordinal)) throw new ArgumentException("CTC emissions do not reproduce the raw text.", nameof(windows));
                int remove = index == 0 ? 0 : Match(windows[index - 1], window, previous!, current, profile);
                bool uncertain = index > 0 && remove == 0;
                int ordinal = 0;
                int offset = trace.Count;
                for (int tokenIndex = 0; tokenIndex < raw.Tokens.Count; tokenIndex++)
                {
                    if ((tokenIndex & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                    OcrToken token = raw.Tokens[tokenIndex];
                    bool keep = token.Emitted && ordinal++ >= remove;
                    trace.Add(new OcrToken(checked(offset + tokenIndex), token.ClassIndex, token.Confidence, token.Text, token.IsBlank, token.IsCollapsedRepeat, token.IsUnknown, keep));
                    if (!keep) continue;
                    if ((long)text.Length + token.Text!.Length > profile.RecognitionWindows.MaximumMergedCharacters)
                        throw new OcrPipelineException(VisualErrorCodes.OcrLimitExceeded, "Merged OCR text exceeds its character limit.", OcrPipelineStage.Merge, regionIndex: region.SourceIndex);
                    text.Append(token.Text);
                    emitted++;
                    sum += token.Confidence;
                    logSum += Math.Log(token.Confidence);
                    minimum = Math.Min(minimum, token.Confidence);
                }
                diagnostics.Add(new OcrRecognitionWindowResult(index, window.Start, window.End, raw, window.Width, remove, uncertain));
                previous = current;
                targetWidth = Math.Max(targetWidth, window.Width.TargetWidth);
                tensorWidth = Math.Max(tensorWidth, window.Width.TensorWidth);
            }
            float confidence = emitted == 0 ? 0 : confidenceAggregation == CtcConfidenceAggregation.Minimum ? minimum
                : (float)(confidenceAggregation == CtcConfidenceAggregation.GeometricMean ? Math.Exp(logSum / emitted) : sum / emitted);
            RecognizedText merged = RecognizedText.CreateDecoded(region.SourceIndex, text.ToString(), confidence, trace, first.CharacterSetId, first.CharacterSetVersion, first.CharacterSetSha256);
            long naturalWidth = profile.DescribeWidth(region.CropQuadrilateral!, region.Orientation).NaturalWidth;
            return new OcrRegionResult(region, merged, new OcrRecognitionWidthInfo(naturalWidth, targetWidth, tensorWidth, RecognitionOverflowMode.SlidingWindow, windows.Count), diagnostics);
        }

        private static int Match(OcrRecognitionWindowResult left, OcrRecognitionWindowResult right, List<OcrToken> suffix, List<OcrToken> prefix, TextCropProfile profile)
        {
            int maximum = Math.Min(profile.RecognitionWindows.MaximumOverlapTokens, Math.Min(suffix.Count, prefix.Count));
            double overlap = left.End - right.Start;
            double leftTolerance = StepWidth(left, profile) * 0.5;
            double rightTolerance = StepWidth(right, profile) * 0.5;
            for (int count = maximum; count >= profile.RecognitionWindows.MinimumOverlapTokens; count--)
            {
                bool match = true;
                for (int index = 0; index < count; index++)
                {
                    OcrToken a = suffix[suffix.Count - count + index];
                    OcrToken b = prefix[index];
                    double x = Position(left, a, profile);
                    double y = Position(right, b, profile);
                    if (a.IsUnknown || b.IsUnknown || a.IsBlank || b.IsBlank || a.ClassIndex != b.ClassIndex || !string.Equals(a.Text, b.Text, StringComparison.Ordinal)
                        || x < right.Start - leftTolerance || x > left.End + leftTolerance
                        || y < right.Start - rightTolerance || y > left.End + rightTolerance || Math.Abs(x - y) > overlap * 0.5)
                    { match = false; break; }
                }
                if (match) return count;
            }
            return 0;
        }

        private static double Position(OcrRecognitionWindowResult window, OcrToken token, TextCropProfile profile)
            => window.Start + (token.Timestep + 0.5) * StepWidth(window, profile);

        private static double StepWidth(OcrRecognitionWindowResult window, TextCropProfile profile)
        {
            // CTC time spans the padded tensor; convert it to the content interval before
            // mapping into the rectified line. This remains an approximate character position.
            // CTC 时间跨度覆盖填充张量；先转换到内容区间，再映射到校正行。这仍是近似字符位置。
            double content = Math.Min(window.Width.TargetWidth, Math.Max(profile.WidthMode == OcrRecognitionWidthMode.Dynamic ? profile.MinimumWidth : 1, window.Width.NaturalWidth));
            return (window.End - window.Start) / Math.Max(1, window.Recognition.Tokens.Count) * window.Width.TensorWidth / content;
        }
    }
}
