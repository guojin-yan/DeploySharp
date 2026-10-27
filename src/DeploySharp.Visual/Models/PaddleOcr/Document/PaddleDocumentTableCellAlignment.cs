using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using JYPPX.DeploySharp.Geometry;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Associates one structural table cell with a separately detected source-space region. / 将一个表格结构单元格关联到独立检测的源图区域。</summary>
    public sealed class PaddleDocumentTableCellMatch
    {
        internal PaddleDocumentTableCellMatch(int rowIndex, int cellIndexInRow, int structureCellIndex, int detectionIndex, PaddleDocumentRegion detection)
        {
            RowIndex = rowIndex;
            CellIndexInRow = cellIndexInRow;
            StructureCellIndex = structureCellIndex;
            DetectionIndex = detectionIndex;
            Bounds = detection.Bounds;
            Score = detection.Score;
        }

        public int RowIndex { get; }
        public int CellIndexInRow { get; }
        public int StructureCellIndex { get; }
        public int DetectionIndex { get; }
        public RectangleF Bounds { get; }
        public float Score { get; }
    }

    /// <summary>Reports whether row-order geometry was sufficient to associate all detected cells. / 报告行序几何是否足以关联全部检测单元格。</summary>
    public sealed class PaddleDocumentTableCellAlignmentResult
    {
        internal PaddleDocumentTableCellAlignmentResult(int rowCount, int structureCellCount, int detectionCount, IReadOnlyList<PaddleDocumentTableCellMatch> matches, IReadOnlyList<string> diagnostics, PaddleDocumentTableResult? structure)
        {
            RowCount = rowCount;
            StructureCellCount = structureCellCount;
            DetectionCount = detectionCount;
            Matches = matches;
            Diagnostics = diagnostics;
            Structure = structure;
        }

        public bool Succeeded => Diagnostics.Count == 0;
        public int RowCount { get; }
        public int StructureCellCount { get; }
        public int DetectionCount { get; }
        public IReadOnlyList<PaddleDocumentTableCellMatch> Matches { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        internal PaddleDocumentTableResult? Structure { get; }
    }

    /// <summary>Matches SLANeXt structure cells to independent detector boxes only when source, counts and row geometry agree. / 仅在来源、数量与行几何一致时匹配 SLANeXt 结构单元格和独立检测框。</summary>
    public static class PaddleDocumentTableCellAligner
    {
        /// <summary>Fills verified structure slots with OCR text, escaping cell content as HTML. / 将 OCR 文字写入已验证的结构槽位，并转义单元格内容。</summary>
        public static string FillMarkup(PaddleDocumentTableResult structure, PaddleDocumentTableCellAlignmentResult alignment, IReadOnlyList<string> texts)
        {
            if (structure == null) throw new ArgumentNullException(nameof(structure));
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (texts == null) throw new ArgumentNullException(nameof(texts));
            if (!alignment.Succeeded || !ReferenceEquals(alignment.Structure, structure) ||
                alignment.Matches.Count != alignment.StructureCellCount || texts.Count != alignment.StructureCellCount)
                throw new InvalidOperationException("Complete cell alignment and one OCR text per structure cell are required.");
            bool hasRowSpan;
            int[] rows = ParseRowLengths(structure.Markup, out hasRowSpan);
            if (hasRowSpan || rows.Length != alignment.RowCount || rows.Sum() != alignment.StructureCellCount)
                throw new InvalidOperationException("The table structure no longer matches the verified cell alignment.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65552 };
            using var reader = XmlReader.Create(new StringReader("<table>" + structure.Markup + "</table>"), settings);
            XElement root = XElement.Load(reader);
            XElement[] cells = root.Descendants().Where(cell => cell.Name == "td" || cell.Name == "th").ToArray();
            if (cells.Length != texts.Count) throw new InvalidOperationException("The table cell count changed during rendering.");
            foreach (PaddleDocumentTableCellMatch match in alignment.Matches)
                cells[match.StructureCellIndex].Value = texts[match.StructureCellIndex]
                    ?? throw new ArgumentException("OCR text cannot be null.", nameof(texts));
            return root.ToString(SaveOptions.DisableFormatting);
        }

        public static PaddleDocumentTableCellAlignmentResult Align(PaddleDocumentTableResult structure, PaddleDocumentRegionResult detections, int sourceWidth, int sourceHeight)
        {
            if (structure == null) throw new ArgumentNullException(nameof(structure));
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (structure.Module != PaddleDocumentModule.TableStructureRecognition) throw new ArgumentException("A table-structure result is required.", nameof(structure));
            if (detections.Module != PaddleDocumentModule.TableCellDetection) throw new ArgumentException("A table-cell detection result is required.", nameof(detections));
            if (sourceWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth));
            if (sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceHeight));

            PaddleDocumentTableCellAlignmentResult Failure(string diagnostic, int rows = 0, int cells = 0)
                => new PaddleDocumentTableCellAlignmentResult(rows, cells, detections.Regions.Count,
                    Array.Empty<PaddleDocumentTableCellMatch>(), new[] { diagnostic }, null);

            if (structure.Metadata.PageIndex != detections.Metadata.PageIndex ||
                !string.Equals(structure.Metadata.InputSha256, detections.Metadata.InputSha256, StringComparison.OrdinalIgnoreCase))
                return Failure("source-mismatch");
            if (structure.Warnings.Any(value => value.StartsWith("missing-eos:", StringComparison.Ordinal)))
                return Failure("incomplete-structure");
            if (structure.Markup.Length > 65536) return Failure("structure-too-long");

            int[] rowLengths;
            bool hasRowSpan;
            try { rowLengths = ParseRowLengths(structure.Markup, out hasRowSpan); }
            catch (XmlException) { return Failure("invalid-structure-markup"); }
            catch (InvalidDataException) { return Failure("invalid-structure-markup"); }
            int cellCount = rowLengths.Sum();
            if (rowLengths.Length == 0 || cellCount == 0) return Failure("empty-structure", rowLengths.Length, cellCount);
            if (hasRowSpan) return Failure("rowspan-not-validated", rowLengths.Length, cellCount);
            if (cellCount != structure.Regions.Count) return Failure("structure-region-count-mismatch", rowLengths.Length, cellCount);
            if (cellCount != detections.Regions.Count) return Failure("detection-count-mismatch", rowLengths.Length, cellCount);

            var ordered = detections.Regions.Select((region, index) => new { Region = region, Index = index }).ToArray();
            foreach (var item in ordered)
            {
                RectangleF box = item.Region.Bounds;
                if (!IsFinite(box.X) || !IsFinite(box.Y) || !IsFinite(box.Width) || !IsFinite(box.Height) ||
                    box.Width <= 0 || box.Height <= 0 || box.X < -1 || box.Y < -1 ||
                    box.Right > sourceWidth + 1 || box.Bottom > sourceHeight + 1)
                    return Failure("invalid-detection-geometry:" + item.Index.ToString(CultureInfo.InvariantCulture), rowLengths.Length, cellCount);
            }
            Array.Sort(ordered, (left, right) =>
            {
                int y = left.Region.Bounds.Y.CompareTo(right.Region.Bounds.Y);
                return y != 0 ? y : left.Region.Bounds.X.CompareTo(right.Region.Bounds.X);
            });

            var matches = new List<PaddleDocumentTableCellMatch>(cellCount);
            int offset = 0;
            float previousRowTop = float.NegativeInfinity;
            for (int row = 0; row < rowLengths.Length; row++)
            {
                int length = rowLengths[row];
                var rowItems = ordered.Skip(offset).Take(length).OrderBy(item => item.Region.Bounds.X).ToArray();
                float firstTop = rowItems.Min(item => item.Region.Bounds.Y);
                float lastTop = rowItems.Max(item => item.Region.Bounds.Y);
                float minimumHeight = rowItems.Min(item => item.Region.Bounds.Height);
                if (lastTop - firstTop > Math.Max(2f, minimumHeight * 0.4f) || firstTop <= previousRowTop + 1f)
                    return Failure("ambiguous-row-geometry:" + row.ToString(CultureInfo.InvariantCulture), rowLengths.Length, cellCount);
                for (int column = 1; column < rowItems.Length; column++)
                {
                    RectangleF prior = rowItems[column - 1].Region.Bounds;
                    RectangleF current = rowItems[column].Region.Bounds;
                    float overlap = prior.Right - current.X;
                    if (overlap > Math.Min(prior.Width, current.Width) * 0.2f)
                        return Failure("overlapping-cells:" + row.ToString(CultureInfo.InvariantCulture), rowLengths.Length, cellCount);
                }
                for (int column = 0; column < rowItems.Length; column++)
                    matches.Add(new PaddleDocumentTableCellMatch(row, column, offset + column, rowItems[column].Index, rowItems[column].Region));
                previousRowTop = firstTop;
                offset += length;
            }
            return new PaddleDocumentTableCellAlignmentResult(rowLengths.Length, cellCount, detections.Regions.Count,
                matches.AsReadOnly(), Array.Empty<string>(), structure);
        }

        private static int[] ParseRowLengths(string markup, out bool hasRowSpan)
        {
            hasRowSpan = false;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65552 };
            using var reader = XmlReader.Create(new StringReader("<table>" + markup + "</table>"), settings);
            XElement root = XElement.Load(reader);
            if (root.Nodes().OfType<XText>().Any(value => !string.IsNullOrWhiteSpace(value.Value)))
                throw new InvalidDataException("Text outside table rows is not a structure token.");
            var parsedRows = new List<XElement>();
            foreach (XElement child in root.Elements())
            {
                if (child.Name == "tr") parsedRows.Add(child);
                else if (child.Name == "thead" || child.Name == "tbody" || child.Name == "tfoot")
                {
                    if (child.HasAttributes || child.Elements().Any(value => value.Name != "tr") ||
                        child.Nodes().OfType<XText>().Any(value => !string.IsNullOrWhiteSpace(value.Value)))
                        throw new InvalidDataException("Invalid table section.");
                    parsedRows.AddRange(child.Elements());
                }
                else throw new InvalidDataException("Unsupported table structure tag.");
            }
            XElement[] rows = parsedRows.ToArray();
            if (rows.Length > 1024 || root.Descendants().Count() > 8192) throw new InvalidDataException("The table exceeds the structural limit.");
            var lengths = new int[rows.Length];
            for (int row = 0; row < rows.Length; row++)
            {
                XElement[] cells = rows[row].Elements().ToArray();
                if (rows[row].HasAttributes || rows[row].Nodes().OfType<XText>().Any(value => !string.IsNullOrWhiteSpace(value.Value)) ||
                    cells.Length == 0 || cells.Length > 1024 || cells.Any(cell => cell.Name != "td" && cell.Name != "th"))
                    throw new InvalidDataException("A row must contain only table cells.");
                foreach (XElement cell in cells)
                {
                    if (cell.HasElements || !string.IsNullOrWhiteSpace(cell.Value)) throw new InvalidDataException("Structure cells must be empty.");
                    foreach (XAttribute attribute in cell.Attributes())
                    {
                        if ((attribute.Name != "colspan" && attribute.Name != "rowspan") ||
                            !int.TryParse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int span) || span < 1 || span > 1024)
                            throw new InvalidDataException("Invalid table cell span.");
                        if (attribute.Name == "rowspan" && span > 1) hasRowSpan = true;
                    }
                }
                lengths[row] = cells.Length;
            }
            if (lengths.Sum() > 4096) throw new InvalidDataException("The table exceeds the cell limit.");
            return lengths;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

#pragma warning restore CS1591
