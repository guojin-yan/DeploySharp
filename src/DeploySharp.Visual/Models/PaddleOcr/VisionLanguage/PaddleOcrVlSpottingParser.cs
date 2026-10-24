#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Results.Language;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Identifies an exact, unscaled rectangular crop within its page. / 标识页面内未经缩放的精确矩形裁剪。</summary>
    /// <remarks>Use actual integer pixel bounds, not the requested floating-point ROI. Warped/rotated crops require a different mapping and must not use this frame. / 使用实际整数像素边界而非请求的浮点 ROI；透视或旋转裁剪不得使用此映射。</remarks>
    public sealed class PaddleOcrVlSourceFrame
    {
        /// <summary>Creates a validated translation from source-crop pixels to page pixels. / 创建经验证的源裁剪像素到页面像素的平移。</summary>
        public PaddleOcrVlSourceFrame(VisualSize pageSize, VisualSize sourceSize, int offsetX = 0, int offsetY = 0)
        {
            if (pageSize.Width <= 0 || pageSize.Height <= 0) throw new ArgumentException("A positive page size is required.", nameof(pageSize));
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) throw new ArgumentException("A positive source size is required.", nameof(sourceSize));
            if (offsetX < 0 || (long)offsetX + sourceSize.Width > pageSize.Width) throw new ArgumentOutOfRangeException(nameof(offsetX));
            if (offsetY < 0 || (long)offsetY + sourceSize.Height > pageSize.Height) throw new ArgumentOutOfRangeException(nameof(offsetY));
            PageSize = pageSize; SourceSize = sourceSize; OffsetX = offsetX; OffsetY = offsetY;
        }
        /// <summary>Gets the original page dimensions. / 获取原页面尺寸。</summary>
        public VisualSize PageSize { get; }
        /// <summary>Gets exact crop dimensions before VL preprocessing. / 获取 VL 预处理之前的精确裁剪尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the crop's integer left edge on the page. / 获取裁剪在页面内的整数左边界。</summary>
        public int OffsetX { get; }
        /// <summary>Gets the crop's integer top edge on the page. / 获取裁剪在页面内的整数上边界。</summary>
        public int OffsetY { get; }
    }

    /// <summary>Describes Spotting parsing independently of generation completion. / 独立于生成完成状态描述 Spotting 解析。</summary>
    public enum PaddleOcrVlSpottingParseStatus
    {
        /// <summary>All text and quadrilaterals are complete and valid. / 全部文本和四边形完整有效。</summary>
        Succeeded,
        /// <summary>Markers, pairing, coordinate range or polygon geometry are invalid. / 标记、配对、坐标范围或多边形几何非法。</summary>
        InvalidStructure,
        /// <summary>Generation did not end with EOS. / 生成未以 EOS 结束。</summary>
        IncompleteGeneration,
        /// <summary>A character or region budget was exceeded. / 超出字符或区域预算。</summary>
        LimitExceeded
    }

    /// <summary>Bounds Spotting parsing before output allocation. / 在输出分配前限制 Spotting 解析。</summary>
    public sealed class PaddleOcrVlSpottingParseOptions
    {
        /// <summary>Creates immutable character and region limits. / 创建不可变字符与区域上限。</summary>
        public PaddleOcrVlSpottingParseOptions(int maximumInputCharacters = 1024 * 1024, int maximumRegions = 4096)
        {
            if (maximumInputCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(maximumInputCharacters));
            if (maximumRegions <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRegions));
            MaximumInputCharacters = maximumInputCharacters; MaximumRegions = maximumRegions;
        }
        /// <summary>Gets the UTF-16 input budget. / 获取 UTF-16 输入预算。</summary>
        public int MaximumInputCharacters { get; }
        /// <summary>Gets the maximum number of text quadrilaterals. / 获取文本四边形数量上限。</summary>
        public int MaximumRegions { get; }
    }

    /// <summary>A double-precision pixel point in an explicitly named coordinate frame. / 明确坐标系中的双精度像素点。</summary>
    public readonly struct PaddleOcrVlSpottingPoint
    {
        internal PaddleOcrVlSpottingPoint(double x, double y) { X = x; Y = y; }
        /// <summary>Gets the horizontal pixel position. / 获取水平像素位置。</summary>
        public double X { get; }
        /// <summary>Gets the vertical pixel position. / 获取垂直像素位置。</summary>
        public double Y { get; }
    }

    /// <summary>One recognized text and its model-ordered quadrilateral, without an invented confidence. / 一项识别文本及按模型顺序排列的四边形，不虚构置信度。</summary>
    public sealed class PaddleOcrVlSpottingRegion
    {
        internal PaddleOcrVlSpottingRegion(string text, int[] values, VisualSize sourceSize, PaddleOcrVlSourceFrame? frame)
        {
            Text = text; QuantizedCoordinates = Array.AsReadOnly(values);
            var source = new PaddleOcrVlSpottingPoint[4];
            var page = frame == null ? null : new PaddleOcrVlSpottingPoint[4];
            for (int index = 0; index < 4; index++)
            {
                // Match the pinned official operation order; do not scale by prepared/resized dimensions.
                double x = values[2 * index] / 1000.0 * sourceSize.Width;
                double y = values[2 * index + 1] / 1000.0 * sourceSize.Height;
                source[index] = new PaddleOcrVlSpottingPoint(x, y);
                if (page != null) page[index] = new PaddleOcrVlSpottingPoint(x + frame!.OffsetX, y + frame.OffsetY);
            }
            SourcePolygon = Array.AsReadOnly(source);
            PagePolygon = page == null ? null : Array.AsReadOnly(page);
        }
        /// <summary>Gets text with outer whitespace trimmed as upstream. / 获取按上游规则去除两端空白的文本。</summary>
        public string Text { get; }
        /// <summary>Gets eight original integer values in [0,1000], in model order. / 获取模型顺序的八个 [0,1000] 原始整数。</summary>
        public IReadOnlyList<int> QuantizedCoordinates { get; }
        /// <summary>Gets four points in the original source-crop pixels, before spotting upscaling. / 获取 Spotting 放大前的原始源裁剪像素四点。</summary>
        public IReadOnlyList<PaddleOcrVlSpottingPoint> SourcePolygon { get; }
        /// <summary>Gets page-pixel points only when an exact source frame was supplied. / 仅在提供精确源坐标系时获取页面像素点。</summary>
        public IReadOnlyList<PaddleOcrVlSpottingPoint>? PagePolygon { get; }
    }

    /// <summary>Immutable complete Spotting output, or an explicit failure with no partial regions. / 不可变完整 Spotting 输出，或不包含部分区域的明确失败。</summary>
    public sealed class PaddleOcrVlSpottingParseResult
    {
        internal PaddleOcrVlSpottingParseResult(string rawText, GenerationFinishReason finishReason, VisualSize sourceSize,
            PaddleOcrVlSourceFrame? sourceFrame, PaddleOcrVlSpottingParseStatus status, string? diagnostic,
            List<PaddleOcrVlSpottingRegion>? regions = null)
        {
            RawText = rawText; FinishReason = finishReason; SourceSize = sourceSize; SourceFrame = sourceFrame; Status = status;
            Diagnostics = Array.AsReadOnly(diagnostic == null ? Array.Empty<string>() : new[] { diagnostic });
            Regions = (regions ?? new List<PaddleOcrVlSpottingRegion>()).AsReadOnly();
            Text = status == PaddleOcrVlSpottingParseStatus.Succeeded ? string.Join("\n\n", Regions.Select(region => region.Text)) : null;
        }
        /// <summary>Gets unchanged generated text. / 获取未改变的生成原文。</summary>
        public string RawText { get; }
        /// <summary>Gets the explicit generation finish reason. / 获取明确的生成结束原因。</summary>
        public GenerationFinishReason FinishReason { get; }
        /// <summary>Gets original source dimensions, not resized tensor dimensions. / 获取原始源尺寸而非缩放张量尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets optional exact source-to-page mapping. / 获取可选的精确源到页面映射。</summary>
        public PaddleOcrVlSourceFrame? SourceFrame { get; }
        /// <summary>Gets independent parsing status. / 获取独立解析状态。</summary>
        public PaddleOcrVlSpottingParseStatus Status { get; }
        /// <summary>Gets whether all regions are complete and valid. / 获取所有区域是否完整有效。</summary>
        public bool Succeeded => Status == PaddleOcrVlSpottingParseStatus.Succeeded;
        /// <summary>Gets whether complete polygons also have exact page coordinates. / 获取完整多边形是否同时具有精确页面坐标。</summary>
        public bool PageCoordinatesAvailable => Succeeded && SourceFrame != null;
        /// <summary>Gets complete regions in generated order, or an empty list on any failure. / 获取生成顺序的完整区域，任何失败时均为空列表。</summary>
        public IReadOnlyList<PaddleOcrVlSpottingRegion> Regions { get; }
        /// <summary>Gets recognized texts joined with two newlines, or null on failure. / 获取以双换行连接的识别文本，失败时为空。</summary>
        public string? Text { get; }
        /// <summary>Gets stable failure diagnostics. / 获取稳定的失败诊断。</summary>
        public IReadOnlyList<string> Diagnostics { get; }
    }

    /// <summary>Parses official block or flat Spotting markers with explicit completeness and geometry validation. / 解析官方块式或平铺 Spotting 标记，明确验证完整性与几何。</summary>
    /// <remarks>Matches PaddleX 4c968d30 post_process_for_spotting for well-formed output. Malformed or extra data is rejected rather than silently dropped; points are neither reordered nor clipped. / 良构输出遵循 PaddleX 4c968d30；非法或多余数据会报错而非静默丢弃，不重排或裁切点。</remarks>
    public static class PaddleOcrVlSpottingParser
    {
        private const string TextStart = "<|TEXT_START|>", TextEnd = "<|TEXT_END|>", LocBegin = "<|LOC_BEGIN|>", LocEnd = "<|LOC_END|>", LocPrefix = "<|LOC_";
        private static readonly PaddleOcrVlSpottingParseOptions DefaultOptions = new PaddleOcrVlSpottingParseOptions();

        /// <summary>Parses EOS output in original source pixels; page mapping is optional and explicit. / 在原始源像素坐标中解析 EOS 输出；页面映射可选且须明确提供。</summary>
        public static PaddleOcrVlSpottingParseResult Parse(string rawText, GenerationFinishReason finishReason, VisualSize sourceSize,
            PaddleOcrVlSourceFrame? sourceFrame = null, PaddleOcrVlSpottingParseOptions? options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (rawText == null) throw new ArgumentNullException(nameof(rawText));
            if (!Enum.IsDefined(typeof(GenerationFinishReason), finishReason)) throw new ArgumentOutOfRangeException(nameof(finishReason));
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) throw new ArgumentException("A positive source size is required.", nameof(sourceSize));
            if (sourceFrame != null && sourceFrame.SourceSize != sourceSize) throw new ArgumentException("The source frame does not match the original crop dimensions.", nameof(sourceFrame));
            cancellationToken.ThrowIfCancellationRequested();
            options ??= DefaultOptions;
            PaddleOcrVlSpottingParseResult Failure(PaddleOcrVlSpottingParseStatus status, string diagnostic)
                => new PaddleOcrVlSpottingParseResult(rawText, finishReason, sourceSize, sourceFrame, status, diagnostic);
            if (rawText.Length > options.MaximumInputCharacters) return Failure(PaddleOcrVlSpottingParseStatus.LimitExceeded, "input-character-limit");
            if (finishReason != GenerationFinishReason.EndOfSequence) return Failure(PaddleOcrVlSpottingParseStatus.IncompleteGeneration, "generation-not-eos:" + finishReason);

            var texts = new List<string>();
            var coordinates = new List<int[]>();
            bool blockFormat = rawText.Contains(TextStart, StringComparison.Ordinal) || rawText.Contains(TextEnd, StringComparison.Ordinal)
                || rawText.Contains(LocBegin, StringComparison.Ordinal) || rawText.Contains(LocEnd, StringComparison.Ordinal);
            int cursor = 0;
            while (cursor < rawText.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (blockFormat)
                {
                    SkipWhitespace(rawText, ref cursor, cancellationToken);
                    if (cursor == rawText.Length) break;
                    if (At(rawText, cursor, TextStart))
                    {
                        if (texts.Count >= options.MaximumRegions) return Failure(PaddleOcrVlSpottingParseStatus.LimitExceeded, "region-limit");
                        int start = cursor + TextStart.Length;
                        int end = rawText.IndexOf(TextEnd, start, StringComparison.Ordinal);
                        if (end < 0) return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "missing-text-end");
                        string text = rawText.Substring(start, end - start);
                        if (text.Contains("<|", StringComparison.Ordinal)) return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "nested-or-unknown-text-marker");
                        texts.Add(text.Trim()); cursor = end + TextEnd.Length; continue;
                    }
                    if (!At(rawText, cursor, LocBegin)) return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "data-outside-spotting-block");
                    cursor += LocBegin.Length;
                }
                else
                {
                    int first = rawText.IndexOf("<|", cursor, StringComparison.Ordinal);
                    if (first < 0)
                    {
                        if (!string.IsNullOrWhiteSpace(rawText.Substring(cursor))) return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "text-without-location");
                        break;
                    }
                    if (texts.Count >= options.MaximumRegions) return Failure(PaddleOcrVlSpottingParseStatus.LimitExceeded, "region-limit");
                    texts.Add(rawText.Substring(cursor, first - cursor).Trim()); cursor = first;
                }
                if (coordinates.Count >= options.MaximumRegions) return Failure(PaddleOcrVlSpottingParseStatus.LimitExceeded, "region-limit");
                var values = new int[8];
                for (int index = 0; index < 8; index++)
                {
                    SkipWhitespace(rawText, ref cursor, cancellationToken);
                    if (!TryReadLocation(rawText, ref cursor, out values[index], cancellationToken))
                        return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "invalid-or-incomplete-location:region=" + coordinates.Count + ";value=" + index);
                }
                if (blockFormat)
                {
                    SkipWhitespace(rawText, ref cursor, cancellationToken);
                    if (!At(rawText, cursor, LocEnd)) return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "missing-location-end-or-extra-values");
                    cursor += LocEnd.Length;
                }
                if (!IsSimpleQuadrilateral(values)) return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "invalid-quadrilateral:region=" + coordinates.Count);
                coordinates.Add(values);
            }
            if (coordinates.Count == 0 || coordinates.Count != texts.Count)
                return Failure(PaddleOcrVlSpottingParseStatus.InvalidStructure, "empty-or-unpaired-spotting-output");
            cancellationToken.ThrowIfCancellationRequested();
            var regions = new List<PaddleOcrVlSpottingRegion>(texts.Count);
            for (int index = 0; index < texts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                regions.Add(new PaddleOcrVlSpottingRegion(texts[index], coordinates[index], sourceSize, sourceFrame));
            }
            return new PaddleOcrVlSpottingParseResult(rawText, finishReason, sourceSize, sourceFrame, PaddleOcrVlSpottingParseStatus.Succeeded, null, regions);
        }

        private static bool At(string text, int offset, string marker)
            => offset <= text.Length - marker.Length && string.CompareOrdinal(text, offset, marker, 0, marker.Length) == 0;

        private static void SkipWhitespace(string text, ref int cursor, CancellationToken cancellationToken)
        {
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
            { if ((cursor & 255) == 0) cancellationToken.ThrowIfCancellationRequested(); cursor++; }
        }

        private static bool TryReadLocation(string text, ref int cursor, out int value, CancellationToken cancellationToken)
        {
            value = 0;
            if (!At(text, cursor, LocPrefix)) return false;
            cursor += LocPrefix.Length;
            int start = cursor;
            while (cursor < text.Length && text[cursor] >= '0' && text[cursor] <= '9')
            {
                if ((cursor & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                value = value * 10 + text[cursor++] - '0';
                if (value > 1000) return false;
            }
            if (cursor == start || !At(text, cursor, "|>")) return false;
            cursor += 2; return true;
        }

        private static bool IsSimpleQuadrilateral(int[] values)
        {
            long area = 0;
            for (int a = 0; a < 4; a++)
            {
                for (int b = a + 1; b < 4; b++)
                    if (values[2 * a] == values[2 * b] && values[2 * a + 1] == values[2 * b + 1]) return false;
                int next = (a + 1) % 4;
                area += (long)values[2 * a] * values[2 * next + 1] - (long)values[2 * next] * values[2 * a + 1];
            }
            return area != 0 && !Intersects(values, 0, 1, 2, 3) && !Intersects(values, 1, 2, 3, 0);
        }

        private static bool Intersects(int[] v, int a, int b, int c, int d)
        {
            long Cross(int x, int y, int z) => (long)(v[2 * y] - v[2 * x]) * (v[2 * z + 1] - v[2 * x + 1])
                - (long)(v[2 * y + 1] - v[2 * x + 1]) * (v[2 * z] - v[2 * x]);
            bool On(int x, int y, int z) => v[2 * z] >= Math.Min(v[2 * x], v[2 * y]) && v[2 * z] <= Math.Max(v[2 * x], v[2 * y])
                && v[2 * z + 1] >= Math.Min(v[2 * x + 1], v[2 * y + 1]) && v[2 * z + 1] <= Math.Max(v[2 * x + 1], v[2 * y + 1]);
            long p = Cross(a, b, c), q = Cross(a, b, d), r = Cross(c, d, a), s = Cross(c, d, b);
            return (Math.Sign(p) != Math.Sign(q) && p != 0 && q != 0 && Math.Sign(r) != Math.Sign(s) && r != 0 && s != 0)
                || (p == 0 && On(a, b, c)) || (q == 0 && On(a, b, d)) || (r == 0 && On(c, d, a)) || (s == 0 && On(c, d, b));
        }
    }
}
#endif
