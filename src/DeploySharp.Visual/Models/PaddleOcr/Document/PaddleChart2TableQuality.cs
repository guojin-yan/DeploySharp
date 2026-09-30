using System;
using System.Collections.Generic;
using System.Text;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Describes the structural shape of a pipe-delimited Chart2Table result. / 描述 Chart2Table 管道分隔结果的结构形状。</summary>
    public sealed class PaddleChart2TableStructureMetrics
    {
        internal PaddleChart2TableStructureMetrics(
            int rowCount,
            int dataRowCount,
            int columnCount,
            int separatorRowCount,
            int malformedRowCount,
            int inconsistentRowCount,
            int emptyCellCount,
            int nonEmptyCellCount)
        {
            RowCount = rowCount;
            DataRowCount = dataRowCount;
            ColumnCount = columnCount;
            SeparatorRowCount = separatorRowCount;
            MalformedRowCount = malformedRowCount;
            InconsistentRowCount = inconsistentRowCount;
            EmptyCellCount = emptyCellCount;
            NonEmptyCellCount = nonEmptyCellCount;
        }

        /// <summary>Gets all non-separator rows. The first row is treated as the header for DataRowCount. / 获取所有非分隔行；DataRowCount 将第一行视为表头。</summary>
        public int RowCount { get; }
        /// <summary>Gets rows after the first row. / 获取除第一行外的数据行数。</summary>
        public int DataRowCount { get; }
        /// <summary>Gets the expected column count from the first row. / 根据第一行获取预期列数。</summary>
        public int ColumnCount { get; }
        /// <summary>Gets Markdown separator rows that were ignored. / 获取被忽略的 Markdown 分隔行数。</summary>
        public int SeparatorRowCount { get; }
        /// <summary>Gets non-empty lines that do not contain a table delimiter. / 获取不含表格分隔符的非空行数。</summary>
        public int MalformedRowCount { get; }
        /// <summary>Gets rows whose cell count differs from the first row. / 获取列数与第一行不同的行数。</summary>
        public int InconsistentRowCount { get; }
        /// <summary>Gets cells containing no non-whitespace characters. / 获取不含非空白字符的单元格数。</summary>
        public int EmptyCellCount { get; }
        /// <summary>Gets cells containing at least one non-whitespace character. / 获取至少含一个非空白字符的单元格数。</summary>
        public int NonEmptyCellCount { get; }
        /// <summary>Gets all parsed cells. / 获取解析出的全部单元格数。</summary>
        public int CellCount => EmptyCellCount + NonEmptyCellCount;
        /// <summary>Gets whether the text is a non-empty, rectangular table without malformed rows. / 获取文本是否为非空且无畸形行的矩形表格。</summary>
        public bool IsStructurallyValid => RowCount > 0 && ColumnCount > 0 && MalformedRowCount == 0 && InconsistentRowCount == 0;
    }

    /// <summary>Compares expected and generated Chart2Table text at table, row and cell granularity. / 按表格、行和单元格粒度比较期望与生成的 Chart2Table 文本。</summary>
    public sealed class PaddleChart2TableQualityComparison
    {
        internal PaddleChart2TableQualityComparison(
            PaddleChart2TableStructureMetrics expected,
            PaddleChart2TableStructureMetrics actual,
            bool exactTextMatch,
            int rowExactMatchCount,
            int cellExactMatchCount)
        {
            Expected = expected;
            Actual = actual;
            ExactTextMatch = exactTextMatch;
            RowExactMatchCount = rowExactMatchCount;
            CellExactMatchCount = cellExactMatchCount;
        }

        /// <summary>Gets expected text metrics. / 获取期望文本结构指标。</summary>
        public PaddleChart2TableStructureMetrics Expected { get; }
        /// <summary>Gets generated text metrics. / 获取生成文本结构指标。</summary>
        public PaddleChart2TableStructureMetrics Actual { get; }
        /// <summary>Gets whether trimmed source text is byte-for-byte equal. / 获取去首尾空白后的文本是否完全相同。</summary>
        public bool ExactTextMatch { get; }
        /// <summary>Gets the number of expected rows whose normalized cells all match. / 获取单元格归一化后完全匹配的期望行数。</summary>
        public int RowExactMatchCount { get; }
        /// <summary>Gets the number of expected cells with a matching normalized value. / 获取归一化值匹配的期望单元格数。</summary>
        public int CellExactMatchCount { get; }
        /// <summary>Gets whether both tables are rectangular with equal dimensions. / 获取两张表是否均为矩形且行列维度相同。</summary>
        public bool StructureMatches => Expected.IsStructurallyValid && Actual.IsStructurallyValid && Expected.RowCount == Actual.RowCount && Expected.ColumnCount == Actual.ColumnCount;
        /// <summary>Gets the fraction of expected rows matched exactly. / 获取完全匹配的期望行比例。</summary>
        public double RowAccuracy => Expected.RowCount == 0 ? 0 : (double)RowExactMatchCount / Expected.RowCount;
        /// <summary>Gets the fraction of expected cells matched exactly. / 获取完全匹配的期望单元格比例。</summary>
        public double CellAccuracy => Expected.CellCount == 0 ? 0 : (double)CellExactMatchCount / Expected.CellCount;
        /// <summary>Gets whether structure and every normalized cell match. / 获取结构以及所有归一化单元格是否均匹配。</summary>
        public bool IsExactStructureAndContent => StructureMatches && RowExactMatchCount == Expected.RowCount && CellExactMatchCount == Expected.CellCount;
    }

    /// <summary>Provides deterministic, backend-neutral quality metrics for generated pipe-delimited tables. / 提供生成管道分隔表格的确定性、后端无关质量指标。</summary>
    public static class PaddleChart2TableQualityEvaluator
    {
        /// <summary>Analyzes table structure without assigning model accuracy. / 分析表格结构，但不赋予模型准确率含义。</summary>
        public static PaddleChart2TableStructureMetrics Analyze(string text)
        {
            return ToMetrics(Parse(text));
        }

        /// <summary>Compares normalized rows and cells while retaining exact text equality separately. / 比较归一化行和单元格，同时单独保留原始文本相等结果。</summary>
        public static PaddleChart2TableQualityComparison Compare(string expected, string actual)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (actual == null) throw new ArgumentNullException(nameof(actual));
            ParsedTable expectedTable = Parse(expected);
            ParsedTable actualTable = Parse(actual);
            int matchingRows = 0;
            int matchingCells = 0;
            int commonRows = Math.Min(expectedTable.Rows.Count, actualTable.Rows.Count);
            for (int row = 0; row < commonRows; row++)
            {
                IReadOnlyList<string> expectedCells = expectedTable.Rows[row];
                IReadOnlyList<string> actualCells = actualTable.Rows[row];
                bool rowMatches = expectedCells.Count == actualCells.Count;
                int commonCells = Math.Min(expectedCells.Count, actualCells.Count);
                for (int cell = 0; cell < commonCells; cell++)
                {
                    bool matches = string.Equals(NormalizeCell(expectedCells[cell]), NormalizeCell(actualCells[cell]), StringComparison.Ordinal);
                    if (matches) matchingCells++;
                    else rowMatches = false;
                }
                if (rowMatches) matchingRows++;
            }

            bool exactText = string.Equals(expected.Trim(), actual.Trim(), StringComparison.Ordinal);
            return new PaddleChart2TableQualityComparison(ToMetrics(expectedTable), ToMetrics(actualTable), exactText, matchingRows, matchingCells);
        }

        private static ParsedTable Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var result = new ParsedTable();
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line == "```" || line.StartsWith("```", StringComparison.Ordinal)) continue;
                if (line.IndexOf('|') < 0) { result.MalformedRowCount++; continue; }
                IReadOnlyList<string> cells = SplitCells(line);
                if (cells.Count == 0) { result.MalformedRowCount++; continue; }
                if (IsSeparatorRow(cells)) { result.SeparatorRowCount++; continue; }
                if (result.ColumnCount == 0) result.ColumnCount = cells.Count;
                else if (cells.Count != result.ColumnCount) result.InconsistentRowCount++;
                result.Rows.Add(cells);
            }
            return result;
        }

        private static IReadOnlyList<string> SplitCells(string line)
        {
            int start = line.Length > 0 && line[0] == '|' ? 1 : 0;
            int end = line.Length > start && line[line.Length - 1] == '|' ? line.Length - 1 : line.Length;
            var cells = new List<string>();
            var current = new StringBuilder();
            bool escaped = false;
            for (int index = start; index < end; index++)
            {
                char value = line[index];
                if (escaped) { current.Append(value); escaped = false; continue; }
                if (value == '\\' && index + 1 < end && line[index + 1] == '|') { escaped = true; continue; }
                if (value == '|') { cells.Add(current.ToString().Trim()); current.Clear(); continue; }
                current.Append(value);
            }
            cells.Add(current.ToString().Trim());
            return cells.AsReadOnly();
        }

        private static bool IsSeparatorRow(IReadOnlyList<string> cells)
        {
            if (cells.Count == 0) return false;
            foreach (string cell in cells)
            {
                string value = cell.Trim();
                if (value.Length < 3) return false;
                int start = value[0] == ':' ? 1 : 0;
                int end = value.Length - (value[value.Length - 1] == ':' ? 1 : 0);
                if (end - start < 3) return false;
                for (int index = start; index < end; index++) if (value[index] != '-') return false;
            }
            return true;
        }

        private static PaddleChart2TableStructureMetrics ToMetrics(ParsedTable table)
        {
            int empty = 0, nonEmpty = 0;
            foreach (IReadOnlyList<string> row in table.Rows)
                foreach (string cell in row)
                    if (string.IsNullOrWhiteSpace(cell)) empty++; else nonEmpty++;
            return new PaddleChart2TableStructureMetrics(table.Rows.Count, Math.Max(0, table.Rows.Count - 1), table.ColumnCount, table.SeparatorRowCount, table.MalformedRowCount, table.InconsistentRowCount, empty, nonEmpty);
        }

        private static string NormalizeCell(string value)
        {
            var builder = new StringBuilder(value.Length);
            bool whitespace = false;
            foreach (char character in value.Trim())
            {
                if (char.IsWhiteSpace(character)) { whitespace = builder.Length > 0; continue; }
                if (whitespace) { builder.Append(' '); whitespace = false; }
                builder.Append(character);
            }
            return builder.ToString();
        }

        private sealed class ParsedTable
        {
            public readonly List<IReadOnlyList<string>> Rows = new List<IReadOnlyList<string>>();
            public int ColumnCount;
            public int SeparatorRowCount;
            public int MalformedRowCount;
            public int InconsistentRowCount;
        }
    }
}

#pragma warning restore CS1591
