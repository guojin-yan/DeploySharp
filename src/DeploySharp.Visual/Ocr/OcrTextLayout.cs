using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Defines bounded geometry and reading-order rules for OCR layout grouping. / 定义 OCR 版面分组的有界几何与阅读顺序规则。</summary>
    public sealed class OcrTextLayoutOptions
    {
        /// <summary>Initializes layout options. / 初始化版面选项。</summary>
        public OcrTextLayoutOptions(
            TextReadingOrder readingOrder = TextReadingOrder.TopToBottomThenLeftToRight,
            float lineCenterToleranceRatio = .5f,
            float minimumVerticalOverlapRatio = .2f,
            float maximumInlineGapRatio = 8f,
            float paragraphGapRatio = 1.5f,
            float columnGapRatio = 2.5f,
            string regionSeparator = "",
            string paragraphLineSeparator = "\n",
            string paragraphSeparator = "\n\n",
            int maximumLines = 4096,
            int maximumParagraphs = 2048,
            int maximumColumns = 32)
        {
            if (!Enum.IsDefined(typeof(TextReadingOrder), readingOrder)) throw new ArgumentOutOfRangeException(nameof(readingOrder));
            ValidateRatio(lineCenterToleranceRatio, 0, 8, nameof(lineCenterToleranceRatio));
            ValidateRatio(minimumVerticalOverlapRatio, 0, 1, nameof(minimumVerticalOverlapRatio));
            ValidateRatio(maximumInlineGapRatio, 0, 128, nameof(maximumInlineGapRatio));
            ValidateRatio(paragraphGapRatio, 0, 32, nameof(paragraphGapRatio));
            ValidateRatio(columnGapRatio, 0, 64, nameof(columnGapRatio));
            if (regionSeparator == null) throw new ArgumentNullException(nameof(regionSeparator));
            if (paragraphLineSeparator == null) throw new ArgumentNullException(nameof(paragraphLineSeparator));
            if (paragraphSeparator == null) throw new ArgumentNullException(nameof(paragraphSeparator));
            if (regionSeparator.Length > 8 || paragraphLineSeparator.Length > 8 || paragraphSeparator.Length > 16) throw new ArgumentOutOfRangeException(nameof(regionSeparator));
            if (maximumLines <= 0 || maximumLines > 16384) throw new ArgumentOutOfRangeException(nameof(maximumLines));
            if (maximumParagraphs <= 0 || maximumParagraphs > 8192) throw new ArgumentOutOfRangeException(nameof(maximumParagraphs));
            if (maximumColumns <= 0 || maximumColumns > 128) throw new ArgumentOutOfRangeException(nameof(maximumColumns));
            ReadingOrder = readingOrder;
            LineCenterToleranceRatio = lineCenterToleranceRatio;
            MinimumVerticalOverlapRatio = minimumVerticalOverlapRatio;
            MaximumInlineGapRatio = maximumInlineGapRatio;
            ParagraphGapRatio = paragraphGapRatio;
            ColumnGapRatio = columnGapRatio;
            RegionSeparator = regionSeparator;
            ParagraphLineSeparator = paragraphLineSeparator;
            ParagraphSeparator = paragraphSeparator;
            MaximumLines = maximumLines;
            MaximumParagraphs = maximumParagraphs;
            MaximumColumns = maximumColumns;
            ConfigurationSha256 = ComputeConfigurationSha256();
        }

        /// <summary>Gets the global reading-order policy. / 获取全局阅读顺序策略。</summary>
        public TextReadingOrder ReadingOrder { get; }
        /// <summary>Gets the accepted line center distance as a multiple of line height. / 获取允许的行中心距离与行高的倍数。</summary>
        public float LineCenterToleranceRatio { get; }
        /// <summary>Gets the minimum vertical overlap ratio for one line. / 获取归入同一行的最小垂直重叠比例。</summary>
        public float MinimumVerticalOverlapRatio { get; }
        /// <summary>Gets the maximum inline horizontal gap as a multiple of line height. / 获取行内最大水平间隙与行高的倍数。</summary>
        public float MaximumInlineGapRatio { get; }
        /// <summary>Gets the maximum vertical paragraph gap as a multiple of line height. / 获取同一段落允许的最大垂直间隙与行高倍数。</summary>
        public float ParagraphGapRatio { get; }
        /// <summary>Gets the maximum gap used to cluster lines into a column. / 获取将行聚类到同一栏的最大间隙倍数。</summary>
        public float ColumnGapRatio { get; }
        /// <summary>Gets text inserted between regions on one line. / 获取同一行区域之间插入的文本。</summary>
        public string RegionSeparator { get; }
        /// <summary>Gets text inserted between lines in one paragraph. / 获取同一段落行之间插入的文本。</summary>
        public string ParagraphLineSeparator { get; }
        /// <summary>Gets text inserted between paragraphs. / 获取段落之间插入的文本。</summary>
        public string ParagraphSeparator { get; }
        /// <summary>Gets the line capacity. / 获取行容量。</summary>
        public int MaximumLines { get; }
        /// <summary>Gets the paragraph capacity. / 获取段落容量。</summary>
        public int MaximumParagraphs { get; }
        /// <summary>Gets the column capacity. / 获取栏容量。</summary>
        public int MaximumColumns { get; }
        /// <summary>Gets a stable hash of the complete layout policy. / 获取完整版面策略的稳定哈希。</summary>
        public string ConfigurationSha256 { get; }

        private string ComputeConfigurationSha256()
        {
            var builder = new StringBuilder();
            builder.Append((int)ReadingOrder).Append('|').Append(LineCenterToleranceRatio.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            builder.Append(MinimumVerticalOverlapRatio.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            builder.Append(MaximumInlineGapRatio.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            builder.Append(ParagraphGapRatio.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            builder.Append(ColumnGapRatio.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            Append(builder, RegionSeparator); Append(builder, ParagraphLineSeparator); Append(builder, ParagraphSeparator);
            builder.Append(MaximumLines).Append('|').Append(MaximumParagraphs).Append('|').Append(MaximumColumns);
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void ValidateRatio(float value, float minimum, float maximum, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum) throw new ArgumentOutOfRangeException(parameterName);
        }

        private static void Append(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Contains OCR regions grouped into one visual text line. / 包含分组到同一视觉文本行的 OCR 区域。</summary>
    public sealed class OcrTextLine
    {
        private readonly IReadOnlyList<OcrRegionResult> _regions;

        internal OcrTextLine(IEnumerable<OcrRegionResult> regions, string text, RectangleF bounds, int columnIndex)
        {
            var copy = new List<OcrRegionResult>();
            foreach (OcrRegionResult region in regions) copy.Add(region ?? throw new ArgumentException("A line cannot contain null regions.", nameof(regions)));
            _regions = new ReadOnlyCollection<OcrRegionResult>(copy);
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Bounds = bounds;
            ColumnIndex = columnIndex;
        }

        /// <summary>Gets regions in horizontal reading order. / 获取按水平阅读顺序排列的区域。</summary>
        public IReadOnlyList<OcrRegionResult> Regions => _regions;
        /// <summary>Gets concatenated text for this line. / 获取此行拼接文本。</summary>
        public string Text { get; }
        /// <summary>Gets the union of source-space bounds. / 获取源图空间边界并集。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets the zero-based column index. / 获取从零开始的栏索引。</summary>
        public int ColumnIndex { get; }
    }

    /// <summary>Contains adjacent OCR lines grouped into one paragraph. / 包含分组到同一段落的相邻 OCR 行。</summary>
    public sealed class OcrTextParagraph
    {
        private readonly IReadOnlyList<OcrTextLine> _lines;

        internal OcrTextParagraph(IEnumerable<OcrTextLine> lines, string text, RectangleF bounds, int columnIndex)
        {
            var copy = new List<OcrTextLine>();
            foreach (OcrTextLine line in lines) copy.Add(line ?? throw new ArgumentException("A paragraph cannot contain null lines.", nameof(lines)));
            _lines = new ReadOnlyCollection<OcrTextLine>(copy);
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Bounds = bounds;
            ColumnIndex = columnIndex;
        }

        /// <summary>Gets lines in paragraph order. / 获取按段落顺序排列的行。</summary>
        public IReadOnlyList<OcrTextLine> Lines => _lines;
        /// <summary>Gets paragraph text. / 获取段落文本。</summary>
        public string Text { get; }
        /// <summary>Gets paragraph source-space bounds. / 获取段落源图空间边界。</summary>
        public RectangleF Bounds { get; }
        /// <summary>Gets the zero-based column index. / 获取从零开始的栏索引。</summary>
        public int ColumnIndex { get; }
    }

    /// <summary>Contains paragraphs belonging to one detected text column. / 包含属于同一检测文本栏的段落。</summary>
    public sealed class OcrTextColumn
    {
        private readonly IReadOnlyList<OcrTextParagraph> _paragraphs;
        private readonly IReadOnlyList<OcrTextLine> _lines;

        internal OcrTextColumn(IEnumerable<OcrTextParagraph> paragraphs, IEnumerable<OcrTextLine> lines, string text, RectangleF bounds)
        {
            var paragraphCopy = new List<OcrTextParagraph>();
            foreach (OcrTextParagraph paragraph in paragraphs) paragraphCopy.Add(paragraph ?? throw new ArgumentException("A column cannot contain null paragraphs.", nameof(paragraphs)));
            _paragraphs = new ReadOnlyCollection<OcrTextParagraph>(paragraphCopy);
            var lineCopy = new List<OcrTextLine>();
            foreach (OcrTextLine line in lines) lineCopy.Add(line ?? throw new ArgumentException("A column cannot contain null lines.", nameof(lines)));
            _lines = new ReadOnlyCollection<OcrTextLine>(lineCopy);
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Bounds = bounds;
        }

        /// <summary>Gets paragraphs in this column. / 获取此栏中的段落。</summary>
        public IReadOnlyList<OcrTextParagraph> Paragraphs => _paragraphs;
        /// <summary>Gets lines in this column. / 获取此栏中的行。</summary>
        public IReadOnlyList<OcrTextLine> Lines => _lines;
        /// <summary>Gets column text. / 获取栏文本。</summary>
        public string Text { get; }
        /// <summary>Gets column source-space bounds. / 获取栏源图空间边界。</summary>
        public RectangleF Bounds { get; }
    }

    /// <summary>Contains deterministic OCR line, paragraph, and column groups. / 包含确定性的 OCR 行、段落和栏分组。</summary>
    public sealed class OcrTextLayoutResult
    {
        private readonly IReadOnlyList<OcrTextLine> _lines;
        private readonly IReadOnlyList<OcrTextParagraph> _paragraphs;
        private readonly IReadOnlyList<OcrTextColumn> _columns;

        internal OcrTextLayoutResult(OcrResult source, IEnumerable<OcrTextLine> lines, IEnumerable<OcrTextParagraph> paragraphs, IEnumerable<OcrTextColumn> columns, string text, OcrTextNormalizationOptions? normalization, OcrTextLayoutOptions options)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _lines = Copy(lines, nameof(lines));
            _paragraphs = Copy(paragraphs, nameof(paragraphs));
            _columns = Copy(columns, nameof(columns));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Normalization = normalization;
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Gets the unchanged source OCR result. / 获取未修改的源 OCR 结果。</summary>
        public OcrResult Source { get; }
        /// <summary>Gets lines in configured document reading order. / 获取按配置文档阅读顺序排列的行。</summary>
        public IReadOnlyList<OcrTextLine> Lines => _lines;
        /// <summary>Gets paragraphs in configured document reading order. / 获取按配置文档阅读顺序排列的段落。</summary>
        public IReadOnlyList<OcrTextParagraph> Paragraphs => _paragraphs;
        /// <summary>Gets detected columns from left to right. / 获取从左到右检测到的栏。</summary>
        public IReadOnlyList<OcrTextColumn> Columns => _columns;
        /// <summary>Gets document text assembled with the configured separators. / 获取按配置分隔符拼接的文档文本。</summary>
        public string Text { get; }
        /// <summary>Gets the optional text-normalization policy used for this view. / 获取此视图可选使用的文本规范化策略。</summary>
        public OcrTextNormalizationOptions? Normalization { get; }
        /// <summary>Gets the geometry and ordering policy. / 获取几何与排序策略。</summary>
        public OcrTextLayoutOptions Options { get; }

        /// <summary>Computes a stable hash over the source OCR result and layout view. / 对源 OCR 结果和版面视图计算稳定哈希。</summary>
        public string ComputeSha256()
        {
            var builder = new StringBuilder();
            Append(builder, Source.ComputeSha256());
            Append(builder, Options.ConfigurationSha256);
            Append(builder, Normalization == null ? string.Empty : Normalization.ConfigurationSha256);
            Append(builder, Text);
            foreach (OcrTextParagraph paragraph in _paragraphs)
            {
                builder.Append(paragraph.ColumnIndex).Append(';');
                Append(builder, paragraph.Text);
                foreach (OcrTextLine line in paragraph.Lines)
                {
                    Append(builder, line.Text);
                    foreach (OcrRegionResult region in line.Regions) builder.Append(region.Region.SourceIndex).Append(';');
                }
            }
            using (SHA256 sha = SHA256.Create()) return OcrCharacterSet.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameterName) where T : class
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            var copy = new List<T>();
            foreach (T value in values) copy.Add(value ?? throw new ArgumentException("Layout collections cannot contain null.", parameterName));
            return new ReadOnlyCollection<T>(copy);
        }

        private static void Append(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('|');
    }

    /// <summary>Builds bounded OCR layout groups from source-space text polygons. / 根据源图空间文本多边形构建有界 OCR 版面分组。</summary>
    public static class OcrTextLayoutBuilder
    {
        /// <summary>Builds a layout view from an OCR result and an optional normalized view. / 从 OCR 结果和可选规范化视图构建版面视图。</summary>
        public static OcrTextLayoutResult Build(OcrResult result, OcrNormalizedResult? normalized = null, OcrTextLayoutOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (normalized != null && !ReferenceEquals(result, normalized.Source)) throw new ArgumentException("The normalized view must originate from the supplied OCR result.", nameof(normalized));
            OcrTextLayoutOptions limits = options ?? new OcrTextLayoutOptions();
            var items = new List<WorkItem>(result.Regions.Count);
            for (int index = 0; index < result.Regions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OcrRegionResult region = result.Regions[index];
                string text = normalized == null ? region.Recognition.Text : normalized.Regions[index].Text.NormalizedText;
                items.Add(new WorkItem(index, region, text));
            }
            if (items.Count == 0) return new OcrTextLayoutResult(result, Array.Empty<OcrTextLine>(), Array.Empty<OcrTextParagraph>(), Array.Empty<OcrTextColumn>(), string.Empty, normalized == null ? null : normalized.Options, limits);
            List<WorkLine> workLines = GroupLines(items, limits, cancellationToken);
            if (workLines.Count > limits.MaximumLines) throw new VisualException(VisualErrorCodes.OcrLimitExceeded, "OCR layout line count exceeds its configured limit.", technicalDetails: "lines=" + workLines.Count + ";maximum=" + limits.MaximumLines);
            List<WorkColumn> workColumns = GroupColumns(workLines, limits, cancellationToken);
            if (workColumns.Count > limits.MaximumColumns) throw new VisualException(VisualErrorCodes.OcrLimitExceeded, "OCR layout column count exceeds its configured limit.", technicalDetails: "columns=" + workColumns.Count + ";maximum=" + limits.MaximumColumns);

            var columns = new List<OcrTextColumn>(workColumns.Count);
            var allParagraphs = new List<OcrTextParagraph>();
            var allLines = new List<OcrTextLine>();
            for (int columnIndex = 0; columnIndex < workColumns.Count; columnIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WorkColumn workColumn = workColumns[columnIndex];
                List<WorkLine> orderedLines = SortLines(workColumn.Lines);
                var lineResults = new List<OcrTextLine>(orderedLines.Count);
                foreach (WorkLine line in orderedLines)
                {
                    lineResults.Add(new OcrTextLine(SortItems(line.Items), JoinItems(line.Items, limits.RegionSeparator), line.Bounds, columnIndex));
                }
                List<List<OcrTextLine>> paragraphLines = GroupParagraphs(lineResults, limits, cancellationToken);
                if (allParagraphs.Count + paragraphLines.Count > limits.MaximumParagraphs) throw new VisualException(VisualErrorCodes.OcrLimitExceeded, "OCR layout paragraph count exceeds its configured limit.", technicalDetails: "paragraphs=" + (allParagraphs.Count + paragraphLines.Count) + ";maximum=" + limits.MaximumParagraphs);
                var columnParagraphs = new List<OcrTextParagraph>(paragraphLines.Count);
                foreach (List<OcrTextLine> lines in paragraphLines)
                {
                    OcrTextParagraph paragraph = new OcrTextParagraph(lines, JoinLines(lines, limits.ParagraphLineSeparator), UnionLines(lines), columnIndex);
                    columnParagraphs.Add(paragraph);
                    allParagraphs.Add(paragraph);
                }
                string columnText = JoinParagraphs(columnParagraphs, limits.ParagraphSeparator);
                OcrTextColumn column = new OcrTextColumn(columnParagraphs, lineResults, columnText, workColumn.Bounds);
                columns.Add(column);
                allLines.AddRange(lineResults);
            }

            List<OcrTextLine> orderedAllLines = SortOutputLines(allLines, limits.ReadingOrder);
            List<OcrTextParagraph> orderedParagraphs = SortOutputParagraphs(allParagraphs, limits.ReadingOrder);
            string textOutput = JoinParagraphs(orderedParagraphs, limits.ParagraphSeparator);
            return new OcrTextLayoutResult(result, orderedAllLines, orderedParagraphs, columns, textOutput, normalized == null ? null : normalized.Options, limits);
        }

        private static List<WorkLine> GroupLines(List<WorkItem> items, OcrTextLayoutOptions options, CancellationToken cancellationToken)
        {
            var ordered = new List<WorkItem>(items);
            ordered.Sort(CompareItemsTopLeft);
            var lines = new List<WorkLine>();
            foreach (WorkItem item in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WorkLine? best = null;
                float bestScore = float.MaxValue;
                foreach (WorkLine line in lines)
                {
                    float overlap = VerticalOverlapRatio(item.Bounds, line.Bounds);
                    float centerDistance = Math.Abs(CenterY(item.Bounds) - CenterY(line.Bounds));
                    float height = Math.Max(item.Bounds.Height, line.Bounds.Height);
                    float gap = HorizontalGap(item.Bounds, line.Bounds);
                    bool sameBand = overlap >= options.MinimumVerticalOverlapRatio || centerDistance <= options.LineCenterToleranceRatio * height;
                    bool inline = HorizontalOverlap(item.Bounds, line.Bounds) > 0 || gap <= options.MaximumInlineGapRatio * height;
                    if (!sameBand || !inline) continue;
                    float score = centerDistance + (gap / Math.Max(1f, height)) * .01f;
                    if (score < bestScore) { best = line; bestScore = score; }
                }
                if (best == null) { best = new WorkLine(); lines.Add(best); }
                best.Items.Add(item);
                best.Bounds = Union(best.Bounds, item.Bounds, best.Items.Count == 1);
            }
            lines.Sort(CompareLinesTopLeft);
            return lines;
        }

        private static List<WorkColumn> GroupColumns(List<WorkLine> lines, OcrTextLayoutOptions options, CancellationToken cancellationToken)
        {
            var ordered = new List<WorkLine>(lines);
            ordered.Sort((first, second) => first.Bounds.X.CompareTo(second.Bounds.X));
            var columns = new List<WorkColumn>();
            foreach (WorkLine line in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WorkColumn? best = null;
                float bestScore = float.MaxValue;
                foreach (WorkColumn column in columns)
                {
                    float overlap = HorizontalOverlapRatio(line.Bounds, column.Bounds);
                    float gap = HorizontalGap(line.Bounds, column.Bounds);
                    float height = Math.Max(line.Bounds.Height, column.Bounds.Height);
                    if (overlap < .15f && gap > options.ColumnGapRatio * height) continue;
                    float score = gap + (1f - overlap) * height;
                    if (score < bestScore) { best = column; bestScore = score; }
                }
                if (best == null) { best = new WorkColumn(); columns.Add(best); }
                best.Lines.Add(line);
                best.Bounds = Union(best.Bounds, line.Bounds, best.Lines.Count == 1);
            }
            columns.Sort((first, second) => first.Bounds.X.CompareTo(second.Bounds.X));
            return columns;
        }

        private static List<List<OcrTextLine>> GroupParagraphs(List<OcrTextLine> lines, OcrTextLayoutOptions options, CancellationToken cancellationToken)
        {
            var paragraphs = new List<List<OcrTextLine>>();
            foreach (OcrTextLine line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<OcrTextLine>? current = paragraphs.Count == 0 ? null : paragraphs[paragraphs.Count - 1];
                OcrTextLine? previous = current == null || current.Count == 0 ? null : current[current.Count - 1];
                if (current == null || previous == null)
                {
                    current = new List<OcrTextLine>();
                    paragraphs.Add(current);
                }
                else
                {
                    float gap = Math.Max(0, line.Bounds.Y - previous.Bounds.Bottom);
                    float height = Math.Max(line.Bounds.Height, previous.Bounds.Height);
                    bool horizontal = HorizontalOverlap(line.Bounds, previous.Bounds) > 0 || HorizontalGap(line.Bounds, previous.Bounds) <= options.MaximumInlineGapRatio * height;
                    if (gap > options.ParagraphGapRatio * height || !horizontal)
                    {
                        current = new List<OcrTextLine>();
                        paragraphs.Add(current);
                    }
                }
                current.Add(line);
            }
            return paragraphs;
        }

        private static List<WorkLine> SortLines(List<WorkLine> lines)
        {
            var copy = new List<WorkLine>(lines);
            copy.Sort(CompareLinesTopLeft);
            return copy;
        }

        private static List<OcrRegionResult> SortItems(List<WorkItem> items)
        {
            var copy = new List<WorkItem>(items);
            copy.Sort((first, second) => first.Bounds.X.CompareTo(second.Bounds.X));
            var result = new List<OcrRegionResult>(copy.Count);
            foreach (WorkItem item in copy) result.Add(item.Region);
            return result;
        }

        private static string JoinItems(List<WorkItem> items, string separator)
        {
            var copy = new List<WorkItem>(items);
            copy.Sort((first, second) => first.Bounds.X.CompareTo(second.Bounds.X));
            var builder = new StringBuilder();
            for (int index = 0; index < copy.Count; index++) { if (index != 0) builder.Append(separator); builder.Append(copy[index].Text); }
            return builder.ToString();
        }

        private static string JoinLines(List<OcrTextLine> lines, string separator)
        {
            var builder = new StringBuilder();
            for (int index = 0; index < lines.Count; index++) { if (index != 0) builder.Append(separator); builder.Append(lines[index].Text); }
            return builder.ToString();
        }

        private static string JoinParagraphs(List<OcrTextParagraph> paragraphs, string separator)
        {
            var builder = new StringBuilder();
            for (int index = 0; index < paragraphs.Count; index++) { if (index != 0) builder.Append(separator); builder.Append(paragraphs[index].Text); }
            return builder.ToString();
        }

        private static RectangleF UnionLines(List<OcrTextLine> lines)
        {
            RectangleF result = lines[0].Bounds;
            for (int index = 1; index < lines.Count; index++) result = Union(result, lines[index].Bounds, false);
            return result;
        }

        private static List<OcrTextLine> SortOutputLines(List<OcrTextLine> lines, TextReadingOrder order)
        {
            var copy = new List<OcrTextLine>(lines);
            copy.Sort((first, second) => order == TextReadingOrder.LeftToRightThenTopToBottom
                ? Compare(first.Bounds.X, second.Bounds.X, first.Bounds.Y, second.Bounds.Y)
                : Compare(first.Bounds.Y, second.Bounds.Y, first.Bounds.X, second.Bounds.X));
            return copy;
        }

        private static List<OcrTextParagraph> SortOutputParagraphs(List<OcrTextParagraph> paragraphs, TextReadingOrder order)
        {
            var copy = new List<OcrTextParagraph>(paragraphs);
            copy.Sort((first, second) => order == TextReadingOrder.LeftToRightThenTopToBottom
                ? Compare(first.Bounds.X, second.Bounds.X, first.Bounds.Y, second.Bounds.Y)
                : Compare(first.Bounds.Y, second.Bounds.Y, first.Bounds.X, second.Bounds.X));
            return copy;
        }

        private static int Compare(float primaryFirst, float primarySecond, float secondaryFirst, float secondarySecond)
        {
            int result = primaryFirst.CompareTo(primarySecond);
            return result != 0 ? result : secondaryFirst.CompareTo(secondarySecond);
        }

        private static int CompareItemsTopLeft(WorkItem first, WorkItem second)
        {
            int result = first.Bounds.Y.CompareTo(second.Bounds.Y);
            return result != 0 ? result : first.Bounds.X.CompareTo(second.Bounds.X);
        }

        private static int CompareLinesTopLeft(WorkLine first, WorkLine second)
        {
            int result = first.Bounds.Y.CompareTo(second.Bounds.Y);
            return result != 0 ? result : first.Bounds.X.CompareTo(second.Bounds.X);
        }

        private static float CenterY(RectangleF bounds) => bounds.Y + bounds.Height * .5f;
        private static float HorizontalOverlap(RectangleF first, RectangleF second) => Math.Max(0, Math.Min(first.Right, second.Right) - Math.Max(first.X, second.X));
        private static float HorizontalGap(RectangleF first, RectangleF second) => first.Right < second.X ? second.X - first.Right : second.Right < first.X ? first.X - second.Right : 0;
        private static float VerticalOverlapRatio(RectangleF first, RectangleF second)
        {
            float overlap = Math.Max(0, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Y, second.Y));
            return overlap / Math.Max(1f, Math.Min(first.Height, second.Height));
        }
        private static float HorizontalOverlapRatio(RectangleF first, RectangleF second) => HorizontalOverlap(first, second) / Math.Max(1f, Math.Min(first.Width, second.Width));
        private static RectangleF Union(RectangleF first, RectangleF second, bool firstEmpty)
        {
            if (firstEmpty) return second;
            float left = Math.Min(first.X, second.X);
            float top = Math.Min(first.Y, second.Y);
            float right = Math.Max(first.Right, second.Right);
            float bottom = Math.Max(first.Bottom, second.Bottom);
            return new RectangleF(left, top, right - left, bottom - top);
        }

        private sealed class WorkItem
        {
            internal WorkItem(int index, OcrRegionResult region, string text) { Index = index; Region = region; Text = text; Bounds = region.Region.AxisAlignedBounds; }
            internal int Index { get; }
            internal OcrRegionResult Region { get; }
            internal string Text { get; }
            internal RectangleF Bounds { get; }
        }

        private sealed class WorkLine
        {
            internal readonly List<WorkItem> Items = new List<WorkItem>();
            internal RectangleF Bounds;
        }

        private sealed class WorkColumn
        {
            internal readonly List<WorkLine> Lines = new List<WorkLine>();
            internal RectangleF Bounds;
        }
    }
}
