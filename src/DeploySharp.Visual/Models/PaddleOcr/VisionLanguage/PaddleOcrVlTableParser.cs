#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using JYPPX.DeploySharp.Results.Language;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Controls explicit OTSL row-width repair. / 控制显式 OTSL 行宽修复。</summary>
    public enum PaddleOcrVlTableRepairMode
    {
        /// <summary>Rejects ragged or empty rows. / 拒绝不等宽或空行。</summary>
        None,
        /// <summary>Uses PaddleX's minimum-cost width, pads empty cells and removes surplus non-text slots and empty rows. Invalid merges still fail. / 按 PaddleX 最小代价行宽补空单元格、删除多余非文本槽及空行，非法合并仍失败。</summary>
        OfficialRowWidth
    }

    /// <summary>Describes table parsing independently from generation success. / 独立于生成成功状态描述表格解析结果。</summary>
    public enum PaddleOcrVlTableParseStatus
    {
        /// <summary>A complete rectangular table was parsed without repair. / 完整矩形表格无需修复即解析成功。</summary>
        Succeeded,
        /// <summary>A complete table was parsed after explicitly requested repair. / 在显式请求的修复后解析出完整表格。</summary>
        Repaired,
        /// <summary>The output is not a valid supported OTSL table. / 输出不是有效的受支持 OTSL 表格。</summary>
        InvalidStructure,
        /// <summary>Generation did not end with EOS; no completed table is published. / 生成未以 EOS 结束，不输出已完成表格。</summary>
        IncompleteGeneration,
        /// <summary>A source, row, column or grid budget was exceeded. / 超出原文、行、列或网格预算。</summary>
        LimitExceeded
    }

    /// <summary>Immutable limits checked before table/grid allocation. / 在表格和网格分配前检查的不可变限额。</summary>
    public sealed class PaddleOcrVlTableParseOptions
    {
        /// <summary>Creates bounded parsing options; repair is opt-in. / 创建有界解析选项，修复需显式启用。</summary>
        public PaddleOcrVlTableParseOptions(PaddleOcrVlTableRepairMode repairMode = PaddleOcrVlTableRepairMode.None,
            int maximumInputCharacters = 1024 * 1024, int maximumRows = 4096, int maximumColumns = 1024, int maximumGridCells = 65536)
        {
            if (!Enum.IsDefined(typeof(PaddleOcrVlTableRepairMode), repairMode)) throw new ArgumentOutOfRangeException(nameof(repairMode));
            if (maximumInputCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maximumInputCharacters));
            if (maximumRows < 1) throw new ArgumentOutOfRangeException(nameof(maximumRows));
            if (maximumColumns < 1) throw new ArgumentOutOfRangeException(nameof(maximumColumns));
            if (maximumGridCells < 1) throw new ArgumentOutOfRangeException(nameof(maximumGridCells));
            RepairMode = repairMode;
            MaximumInputCharacters = maximumInputCharacters;
            MaximumRows = maximumRows;
            MaximumColumns = maximumColumns;
            MaximumGridCells = maximumGridCells;
        }

        /// <summary>Gets the explicit repair policy. / 获取显式修复策略。</summary>
        public PaddleOcrVlTableRepairMode RepairMode { get; }
        /// <summary>Gets the UTF-16 source-character budget. / 获取 UTF-16 原文字符预算。</summary>
        public int MaximumInputCharacters { get; }
        /// <summary>Gets the raw row budget, including empty rows. / 获取原始行预算，包含空行。</summary>
        public int MaximumRows { get; }
        /// <summary>Gets the raw per-row slot budget. / 获取原始每行槽位预算。</summary>
        public int MaximumColumns { get; }
        /// <summary>Gets the budget for both raw slots and the repaired rectangular grid. / 获取原始槽位及修复后矩形网格各自的预算。</summary>
        public int MaximumGridCells { get; }
    }

    /// <summary>One logical table cell; indices and spans are not pixel coordinates. / 一个逻辑表格单元格，索引及跨度不是像素坐标。</summary>
    public sealed class PaddleOcrVlTableCell
    {
        internal PaddleOcrVlTableCell(int row, int column, int rowSpan, int columnSpan, string text, bool empty)
        { Row = row; Column = column; RowSpan = rowSpan; ColumnSpan = columnSpan; Text = text; IsEmpty = empty; }

        /// <summary>Gets the zero-based starting row. / 获取从零开始的起始行。</summary>
        public int Row { get; }
        /// <summary>Gets the zero-based starting column. / 获取从零开始的起始列。</summary>
        public int Column { get; }
        /// <summary>Gets the number of covered rows. / 获取覆盖行数。</summary>
        public int RowSpan { get; }
        /// <summary>Gets the number of covered columns. / 获取覆盖列数。</summary>
        public int ColumnSpan { get; }
        /// <summary>Gets cell text with outer whitespace trimmed as in the official converter. / 获取按官方转换器去掉两端空白后的单元格文本。</summary>
        public string Text { get; }
        /// <summary>Gets whether the anchor is an explicit empty cell, including repair padding. / 获取锚点是否为明确空单元格，包含修复补位。</summary>
        public bool IsEmpty { get; }
    }

    /// <summary>Immutable OTSL analysis retaining raw output and explicit diagnostics. / 保留原文和明确诊断的不可变 OTSL 分析结果。</summary>
    public sealed class PaddleOcrVlTableParseResult
    {
        internal PaddleOcrVlTableParseResult(string rawText, GenerationFinishReason finishReason, PaddleOcrVlTableParseStatus status,
            List<string> diagnostics, int rows = 0, int columns = 0, List<PaddleOcrVlTableCell>? cells = null, string? html = null)
        {
            RawText = rawText; FinishReason = finishReason; Status = status;
            Diagnostics = diagnostics.AsReadOnly(); RowCount = rows; ColumnCount = columns;
            Cells = (cells ?? new List<PaddleOcrVlTableCell>()).AsReadOnly(); Html = html;
        }

        /// <summary>Gets the unchanged generated OTSL text. / 获取未改变的生成 OTSL 原文。</summary>
        public string RawText { get; }
        /// <summary>Gets the generation termination reason. / 获取生成结束原因。</summary>
        public GenerationFinishReason FinishReason { get; }
        /// <summary>Gets parsing status; EOS alone does not imply a valid table. / 获取解析状态，仅 EOS 不代表有效表格。</summary>
        public PaddleOcrVlTableParseStatus Status { get; }
        /// <summary>Gets whether a complete table is available, including explicit repairs. / 获取是否有完整表格，包含显式修复成功的情况。</summary>
        public bool Succeeded => Status == PaddleOcrVlTableParseStatus.Succeeded || Status == PaddleOcrVlTableParseStatus.Repaired;
        /// <summary>Gets stable diagnostics, including every kind of requested repair. / 获取稳定诊断，包含每种请求的修复。</summary>
        public IReadOnlyList<string> Diagnostics { get; }
        /// <summary>Gets the logical row count, or zero when parsing failed. / 获取逻辑行数，解析失败时为零。</summary>
        public int RowCount { get; }
        /// <summary>Gets the logical column count, or zero when parsing failed. / 获取逻辑列数，解析失败时为零。</summary>
        public int ColumnCount { get; }
        /// <summary>Gets anchors in row-major order, without invented header roles, confidence or pixel bounds. / 获取行优先排列的锚点，不虚构表头角色、置信度或像素框。</summary>
        public IReadOnlyList<PaddleOcrVlTableCell> Cells { get; }
        /// <summary>Gets escaped table-only HTML, or null for incomplete/invalid/over-budget output. / 获取转义后的纯表格 HTML，未完成、非法或超预算时为空。</summary>
        public string? Html { get; }
    }

    /// <summary>Parses PaddleOCR-VL OTSL into validated logical cells and escaped HTML. / 将 PaddleOCR-VL OTSL 解析为经验证的逻辑单元格和已转义 HTML。</summary>
    /// <remarks>Span and opt-in row-width rules follow PaddleX 4c968d30, paddleocr_vl/uilts.py. Unlike its permissive converter, invalid/dangling/overlapping spans are rejected and repairs are reported. / 跨度和可选行宽规则参考 PaddleX 4c968d30 的 paddleocr_vl/uilts.py；额外拒绝非法、悬空和重叠跨度，明确报告修复。</remarks>
    public static class PaddleOcrVlTableParser
    {
        private static readonly PaddleOcrVlTableParseOptions DefaultOptions = new PaddleOcrVlTableParseOptions();
        private static readonly string[] Markers = { "<fcel>", "<ecel>", "<lcel>", "<ucel>", "<xcel>", "<nl>" };
        private enum Kind { Filled, Empty, Left, Up, Cross, Newline }
        private sealed class Slot
        {
            internal Slot(Kind kind, string text) { Kind = kind; Text = text; }
            internal readonly Kind Kind;
            internal readonly string Text;
        }

        /// <summary>Parses complete EOS output without discarding raw text. Cancellation propagates; data problems return diagnostics. / 解析完整 EOS 输出且保留原文；取消会传播，数据问题返回诊断。</summary>
        public static PaddleOcrVlTableParseResult Parse(string otsl, GenerationFinishReason finishReason,
            PaddleOcrVlTableParseOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (otsl == null) throw new ArgumentNullException(nameof(otsl));
            if (!Enum.IsDefined(typeof(GenerationFinishReason), finishReason)) throw new ArgumentOutOfRangeException(nameof(finishReason));
            cancellationToken.ThrowIfCancellationRequested();
            options ??= DefaultOptions;
            var diagnostics = new List<string>();
            PaddleOcrVlTableParseResult Failure(PaddleOcrVlTableParseStatus status, string diagnostic)
            { diagnostics.Add(diagnostic); return new PaddleOcrVlTableParseResult(otsl, finishReason, status, diagnostics); }
            if (otsl.Length > options.MaximumInputCharacters) return Failure(PaddleOcrVlTableParseStatus.LimitExceeded, "input-character-limit");
            if (finishReason != GenerationFinishReason.EndOfSequence) return Failure(PaddleOcrVlTableParseStatus.IncompleteGeneration, "generation-not-eos:" + finishReason);

            var rows = new List<List<Slot>>();
            var row = new List<Slot>();
            int offset = 0, slots = 0;
            int? previous = null;
            int textStart = 0;
            // Linear scan, no regex backtracking or full token-array copies.
            while (offset <= otsl.Length)
            {
                if ((offset & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                int marker = offset == otsl.Length ? -2 : MarkerAt(otsl, offset);
                if (marker == -1) { offset++; continue; }
                string text = otsl.Substring(textStart, offset - textStart);
                if (previous.HasValue)
                {
                    Kind kind = (Kind)previous.Value;
                    if (kind != Kind.Filled && !string.IsNullOrWhiteSpace(text))
                        return Failure(PaddleOcrVlTableParseStatus.InvalidStructure, "text-outside-filled-cell:row=" + rows.Count + ";column=" + row.Count);
                    if (kind == Kind.Filled && string.IsNullOrWhiteSpace(text))
                        return Failure(PaddleOcrVlTableParseStatus.InvalidStructure, "filled-cell-without-text:row=" + rows.Count + ";column=" + row.Count);
                    if (kind == Kind.Newline)
                    {
                        if (rows.Count >= options.MaximumRows) return Failure(PaddleOcrVlTableParseStatus.LimitExceeded, "row-limit");
                        rows.Add(row); row = new List<Slot>();
                    }
                    else
                    {
                        if (row.Count >= options.MaximumColumns) return Failure(PaddleOcrVlTableParseStatus.LimitExceeded, "column-limit");
                        if (++slots > options.MaximumGridCells) return Failure(PaddleOcrVlTableParseStatus.LimitExceeded, "raw-cell-limit");
                        row.Add(new Slot(kind, kind == Kind.Filled ? text.Trim() : string.Empty));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(text))
                    return Failure(PaddleOcrVlTableParseStatus.InvalidStructure, "text-before-first-marker");
                if (marker == -2) break;
                previous = marker;
                offset += Markers[marker].Length;
                textStart = offset;
            }
            if (row.Count != 0)
            {
                if (rows.Count >= options.MaximumRows) return Failure(PaddleOcrVlTableParseStatus.LimitExceeded, "row-limit");
                rows.Add(row);
            }
            if (options.RepairMode == PaddleOcrVlTableRepairMode.OfficialRowWidth)
            {
                int removed = rows.RemoveAll(value => value.Count == 0);
                if (removed > 0) diagnostics.Add("removed-empty-rows:" + removed);
            }
            if (rows.Count == 0 || rows.Any(value => value.Count == 0))
                return Failure(PaddleOcrVlTableParseStatus.InvalidStructure, "empty-table-or-row");
            int width = rows[0].Count;
            if (options.RepairMode == PaddleOcrVlTableRepairMode.OfficialRowWidth)
            {
                // The lower median minimizes sum(abs(rowLength - width)); the official
                // ascending search chooses its smallest minimizer, bounded by last fcel.
                int[] lengths = rows.Select(value => value.Count).OrderBy(value => value).ToArray();
                width = lengths[(lengths.Length - 1) / 2];
                foreach (List<Slot> values in rows)
                    for (int column = values.Count - 1; column >= 0; column--)
                        if (values[column].Kind == Kind.Filled) { width = Math.Max(width, column + 1); break; }
            }
            else if (rows.Any(value => value.Count != width))
                return Failure(PaddleOcrVlTableParseStatus.InvalidStructure, "ragged-row-widths");
            if ((long)width * rows.Count > options.MaximumGridCells)
                return Failure(PaddleOcrVlTableParseStatus.LimitExceeded, "rectangular-grid-limit");
            int padded = 0, trimmed = 0;
            foreach (List<Slot> values in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (values.Count > width) { trimmed += values.Count - width; values.RemoveRange(width, values.Count - width); }
                while (values.Count < width) { values.Add(new Slot(Kind.Empty, string.Empty)); padded++; }
            }
            if (padded > 0) diagnostics.Add("padded-empty-cells:" + padded);
            if (trimmed > 0) diagnostics.Add("trimmed-nontext-slots:" + trimmed);

            var occupied = new bool[checked(width * rows.Count)];
            var cells = new List<PaddleOcrVlTableCell>();
            for (int r = 0; r < rows.Count; r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int c = 0; c < width; c++)
                {
                    if (occupied[r * width + c]) continue;
                    Slot anchor = rows[r][c];
                    if (anchor.Kind != Kind.Filled && anchor.Kind != Kind.Empty)
                        return Failure(PaddleOcrVlTableParseStatus.InvalidStructure, "orphan-continuation:row=" + r + ";column=" + c);
                    int columnSpan = 1, rowSpan = 1;
                    while (c + columnSpan < width && rows[r][c + columnSpan].Kind == Kind.Left) columnSpan++;
                    while (r + rowSpan < rows.Count && rows[r + rowSpan][c].Kind == Kind.Up) rowSpan++;
                    for (int dr = 0; dr < rowSpan; dr++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        for (int dc = 0; dc < columnSpan; dc++)
                        {
                            int index = (r + dr) * width + c + dc;
                            Kind expected = dr == 0 ? (dc == 0 ? anchor.Kind : Kind.Left) : (dc == 0 ? Kind.Up : Kind.Cross);
                            if (occupied[index] || rows[r + dr][c + dc].Kind != expected)
                                return Failure(PaddleOcrVlTableParseStatus.InvalidStructure, "nonrectangular-or-overlapping-span:row=" + (r + dr) + ";column=" + (c + dc));
                            occupied[index] = true;
                        }
                    }
                    cells.Add(new PaddleOcrVlTableCell(r, c, rowSpan, columnSpan, anchor.Text, anchor.Kind == Kind.Empty));
                }
            }
            string html = ToHtml(rows.Count, cells, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new PaddleOcrVlTableParseResult(otsl, finishReason,
                diagnostics.Count == 0 ? PaddleOcrVlTableParseStatus.Succeeded : PaddleOcrVlTableParseStatus.Repaired,
                diagnostics, rows.Count, width, cells, html);
        }

        private static int MarkerAt(string text, int offset)
        {
            if (text[offset] != '<') return -1;
            for (int index = 0; index < Markers.Length; index++)
                if (text.AsSpan(offset).StartsWith(Markers[index].AsSpan(), StringComparison.Ordinal)) return index;
            return -1;
        }

        private static string ToHtml(int rows, List<PaddleOcrVlTableCell> cells, CancellationToken cancellationToken)
        {
            var html = new StringBuilder("<table>");
            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                html.Append("<tr>");
                while (index < cells.Count && cells[index].Row == row)
                {
                    PaddleOcrVlTableCell cell = cells[index++];
                    html.Append("<td");
                    if (cell.RowSpan > 1) html.Append(" rowspan=\"").Append(cell.RowSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
                    if (cell.ColumnSpan > 1) html.Append(" colspan=\"").Append(cell.ColumnSpan.ToString(CultureInfo.InvariantCulture)).Append('"');
                    html.Append('>');
                    for (int offset = 0; offset < cell.Text.Length; offset++)
                    {
                        if ((offset & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                        char value = cell.Text[offset];
                        switch (value)
                        {
                            case '&': html.Append("&amp;"); break;
                            case '<': html.Append("&lt;"); break;
                            case '>': html.Append("&gt;"); break;
                            case '"': html.Append("&quot;"); break;
                            case '\'': html.Append("&#x27;"); break;
                            default: html.Append(value); break;
                        }
                    }
                    html.Append("</td>");
                }
                html.Append("</tr>");
            }
            return html.Append("</table>").ToString();
        }
    }
}
#endif
