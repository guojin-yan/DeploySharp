using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Defines the two output tensors emitted by SLANeXt and its token map. / 定义 SLANeXt 输出的两个张量及 token 映射。</summary>
    public sealed class PaddleDocumentTableStructureSchema
    {
        public PaddleDocumentTableStructureSchema(string structureOutputName, string locationOutputName, IEnumerable<string> tokens, int startTokenIndex = 0, int endTokenIndex = -1, int padTokenIndex = -1, int unknownTokenIndex = -1, IEnumerable<string>? cellTokens = null)
        {
            if (string.IsNullOrWhiteSpace(structureOutputName)) throw new ArgumentException("A structure output name is required.", nameof(structureOutputName));
            if (string.IsNullOrWhiteSpace(locationOutputName)) throw new ArgumentException("A location output name is required.", nameof(locationOutputName));
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            var tokenValues = new List<string>(tokens);
            if (tokenValues.Count == 0 || tokenValues.Any(string.IsNullOrEmpty)) throw new ArgumentException("Table structure tokens cannot be empty.", nameof(tokens));
            if (startTokenIndex < 0 || startTokenIndex >= tokenValues.Count) throw new ArgumentOutOfRangeException(nameof(startTokenIndex));
            if (endTokenIndex < 0) endTokenIndex = tokenValues.Count - 1;
            if (endTokenIndex < 0 || endTokenIndex >= tokenValues.Count) throw new ArgumentOutOfRangeException(nameof(endTokenIndex));
            ValidateOptionalIndex(padTokenIndex, tokenValues.Count, nameof(padTokenIndex));
            ValidateOptionalIndex(unknownTokenIndex, tokenValues.Count, nameof(unknownTokenIndex));
            StructureOutputName = structureOutputName;
            LocationOutputName = locationOutputName;
            Tokens = tokenValues.AsReadOnly();
            StartTokenIndex = startTokenIndex;
            EndTokenIndex = endTokenIndex;
            PadTokenIndex = padTokenIndex;
            UnknownTokenIndex = unknownTokenIndex;
            var cells = cellTokens == null ? new[] { "<td>", "<td", "<td></td>" } : new List<string>(cellTokens).ToArray();
            if (cells.Length == 0 || cells.Any(string.IsNullOrEmpty)) throw new ArgumentException("At least one table-cell token is required.", nameof(cellTokens));
            CellTokens = cells;
        }

        public string StructureOutputName { get; }
        public string LocationOutputName { get; }
        public IReadOnlyList<string> Tokens { get; }
        public int StartTokenIndex { get; }
        public int EndTokenIndex { get; }
        public int PadTokenIndex { get; }
        public int UnknownTokenIndex { get; }
        public IReadOnlyList<string> CellTokens { get; }

        /// <summary>Creates the exact 50-class dictionary used by the official PaddleOCR 3.x SLANeXt inference export. / 创建官方 PaddleOCR 3.x SLANeXt 导出使用的 50 类字典。</summary>
        public static PaddleDocumentTableStructureSchema Standard(string structureOutputName = "fetch_name_1", string locationOutputName = "fetch_name_0")
        {
            var tokens = new List<string>
            {
                "<SOS>", "<thead>", "</thead>", "<tbody>", "</tbody>", "<tr>", "</tr>", "<td>", "<td", ">", "</td>",
                " colspan=\"2\"", " colspan=\"3\"", " colspan=\"4\"", " colspan=\"5\"", " colspan=\"6\"", " colspan=\"7\"", " colspan=\"8\"", " colspan=\"9\"", " colspan=\"10\"", " colspan=\"11\"", " colspan=\"12\"", " colspan=\"13\"", " colspan=\"14\"", " colspan=\"15\"", " colspan=\"16\"", " colspan=\"17\"", " colspan=\"18\"", " colspan=\"19\"", " colspan=\"20\"",
                " rowspan=\"2\"", " rowspan=\"3\"", " rowspan=\"4\"", " rowspan=\"5\"", " rowspan=\"6\"", " rowspan=\"7\"", " rowspan=\"8\"", " rowspan=\"9\"", " rowspan=\"10\"", " rowspan=\"11\"", " rowspan=\"12\"", " rowspan=\"13\"", " rowspan=\"14\"", " rowspan=\"15\"", " rowspan=\"16\"", " rowspan=\"17\"", " rowspan=\"18\"", " rowspan=\"19\"", " rowspan=\"20\"", "<EOS>"
            };
            return new PaddleDocumentTableStructureSchema(structureOutputName, locationOutputName, tokens, startTokenIndex: 0, endTokenIndex: tokens.Count - 1, padTokenIndex: -1, unknownTokenIndex: -1);
        }

        private static void ValidateOptionalIndex(int index, int count, string name)
        {
            if (index >= count) throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>Controls SLANeXt sequence limits and coordinate restoration. / 控制 SLANeXt 序列上限和坐标还原。</summary>
    public sealed class PaddleDocumentTableStructureDecoderOptions
    {
        public PaddleDocumentTableStructureDecoderOptions(int maximumSequenceLength = 512, float minimumTokenScore = 0, bool normalizedCoordinates = true, bool clipToSource = true)
        {
            if (maximumSequenceLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSequenceLength));
            if (float.IsNaN(minimumTokenScore) || float.IsInfinity(minimumTokenScore) || minimumTokenScore < 0 || minimumTokenScore > 1) throw new ArgumentOutOfRangeException(nameof(minimumTokenScore));
            MaximumSequenceLength = maximumSequenceLength;
            MinimumTokenScore = minimumTokenScore;
            NormalizedCoordinates = normalizedCoordinates;
            ClipToSource = clipToSource;
        }

        public int MaximumSequenceLength { get; }
        public float MinimumTokenScore { get; }
        public bool NormalizedCoordinates { get; }
        public bool ClipToSource { get; }
    }

    /// <summary>Decodes the two-output SLANeXt table structure graph into HTML-like structure tokens and source-space cells. / 将双输出 SLANeXt 表格结构图解码为 HTML 结构 token 与源图单元格。</summary>
    public sealed class PaddleDocumentTableStructureDecoder : IVisualDecoder
    {
        public PaddleDocumentTableStructureDecoder(PaddleDocumentModelDescriptor descriptor, PaddleDocumentTableStructureSchema? schema = null, PaddleDocumentTableStructureDecoderOptions? options = null)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Module != PaddleDocumentModule.TableStructureRecognition) throw new ArgumentException("The descriptor must identify table-structure recognition.", nameof(descriptor));
            Schema = schema ?? PaddleDocumentTableStructureSchema.Standard();
            Options = options ?? new PaddleDocumentTableStructureDecoderOptions();
        }

        public PaddleDocumentModelDescriptor Descriptor { get; }
        public PaddleDocumentTableStructureSchema Schema { get; }
        public PaddleDocumentTableStructureDecoderOptions Options { get; }
        public VisualTaskId Task => VisualTaskId.TableStructureRecognition;

        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            ITensor structure = Required(context, Schema.StructureOutputName);
            ITensor locations = Required(context, Schema.LocationOutputName);
            ValidateRank3(context, structure, Schema.StructureOutputName);
            ValidateRank3(context, locations, Schema.LocationOutputName);
            int batch = checked((int)structure.Shape[0]);
            int sequence = checked((int)structure.Shape[1]);
            int classes = checked((int)structure.Shape[2]);
            if (batch != context.Input.BatchSize) throw Failure(context, "Table structure output batch does not match the prepared input batch.", Schema.StructureOutputName);
            if (sequence <= 0 || sequence > Options.MaximumSequenceLength) throw Failure(context, "Table structure sequence exceeds its configured bound.", Schema.StructureOutputName);
            if (classes != Schema.Tokens.Count) throw Failure(context, "Table structure class count does not match its token dictionary.", Schema.StructureOutputName, "classes=" + classes + ";expected=" + Schema.Tokens.Count);
            if (locations.Shape[0] != batch || locations.Shape[1] != sequence || locations.Shape[2] != LocationsWidth) throw Failure(context, "SLANeXt location output must be [batch,sequence,8].", Schema.LocationOutputName, locations.Shape.ToString());
            float[] scores = VisualTensorReader.ReadFiniteScores(structure, context.Profile.ProfileId, Schema.StructureOutputName);
            float[] boxes = VisualTensorReader.ReadFiniteScores(locations, context.Profile.ProfileId, Schema.LocationOutputName);
            var results = new List<PaddleDocumentTableResult>(batch);
            for (int row = 0; row < batch; row++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                results.Add(DecodeRow(context, row, sequence, classes, scores, boxes));
            }
            return batch == 1 ? (object)results[0] : new PaddleDocumentTableBatchResult(results);
        }

        private PaddleDocumentTableResult DecodeRow(VisualDecodeContext context, int row, int sequence, int classes, float[] scores, float[] boxes)
        {
            var markup = new StringBuilder(sequence * 4);
            var tokens = new List<PaddleDocumentTableToken>(sequence);
            var cells = new List<PaddleDocumentRegion>();
            var warnings = new List<string>();
            double scoreSum = 0;
            int scoreCount = 0;
            for (int step = 0; step < sequence; step++)
            {
                int scoreOffset = checked(((row * sequence + step) * classes));
                int selected = 0;
                float selectedScore = scores[scoreOffset];
                for (int index = 1; index < classes; index++)
                {
                    float candidate = scores[scoreOffset + index];
                    if (candidate > selectedScore) { selected = index; selectedScore = candidate; }
                }
                if (step > 0 && selected == Schema.EndTokenIndex) break;
                if (selected == Schema.StartTokenIndex || selected == Schema.PadTokenIndex || selected == Schema.UnknownTokenIndex) continue;
                if (selectedScore < Options.MinimumTokenScore) { warnings.Add("token-score-below-threshold:" + step.ToString(CultureInfo.InvariantCulture)); continue; }
                string token = Schema.Tokens[selected];
                markup.Append(token);
                tokens.Add(new PaddleDocumentTableToken(step, token, selectedScore));
                scoreSum += selectedScore;
                scoreCount++;
                if (Schema.CellTokens.Contains(token, StringComparer.Ordinal))
                {
                    int boxOffset = checked((row * sequence + step) * LocationsWidth);
                    IReadOnlyList<PointF> points = DecodePoints(boxes, boxOffset, context.Input.BatchFrames[row]);
                    RectangleF bounds = Bounds(points);
                    var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["sequence-index"] = step.ToString(CultureInfo.InvariantCulture),
                        ["token"] = token,
                        ["polygon"] = string.Join(",", points.Select(point => point.X.ToString("R", CultureInfo.InvariantCulture) + ":" + point.Y.ToString("R", CultureInfo.InvariantCulture)))
                    };
                    cells.Add(new PaddleDocumentRegion("table-cell", selectedScore, bounds, metadata));
                }
            }
            if (tokens.Count == 0) warnings.Add("empty-structure-sequence");
            float average = scoreCount == 0 ? 0 : (float)(scoreSum / scoreCount);
            var metadataResult = new PaddleDocumentResultMetadata(Descriptor, "backend-neutral-decoder", TimeSpan.Zero, context.Input.InputId ?? "input-not-hashed");
            return new PaddleDocumentTableResult(PaddleDocumentModule.TableStructureRecognition, metadataResult, markup.ToString(), cells, "html", warnings, tokens, average);
        }

        private IReadOnlyList<PointF> DecodePoints(float[] values, int offset, VisualInputFrame frame)
        {
            var points = new PointF[4];
            for (int point = 0; point < 4; point++)
            {
                float x = values[offset + (point * 2)];
                float y = values[offset + (point * 2) + 1];
                if (Options.NormalizedCoordinates) { x *= frame.ModelSize.Width; y *= frame.ModelSize.Height; }
                PointF source = frame.Transform.ToSource(new PointF(x, y));
                points[point] = Options.ClipToSource ? ClipPoint(source, frame.SourceSize) : source;
            }
            return points;
        }

        private static PointF ClipPoint(PointF point, VisualSize size) => new PointF(Math.Max(0, Math.Min(size.Width, point.X)), Math.Max(0, Math.Min(size.Height, point.Y)));

        private static RectangleF Bounds(IReadOnlyList<PointF> points)
        {
            float minX = points[0].X, minY = points[0].Y, maxX = points[0].X, maxY = points[0].Y;
            for (int index = 1; index < points.Count; index++) { minX = Math.Min(minX, points[index].X); minY = Math.Min(minY, points[index].Y); maxX = Math.Max(maxX, points[index].X); maxY = Math.Max(maxY, points[index].Y); }
            return new RectangleF(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
        }

        private static ITensor Required(VisualDecodeContext context, string name)
        {
            try { return context.Outputs.GetRequired(name); }
            catch (KeyNotFoundException exception) { throw new VisualException(VisualErrorCodes.TensorInvalid, "A required table-structure output is missing.", exception, context.Profile.ProfileId, name, modelId: context.Profile.ModelId); }
        }

        private static void ValidateRank3(VisualDecodeContext context, ITensor tensor, string name)
        {
            if (tensor.Shape.Rank != 3 || tensor.Shape[0] <= 0 || tensor.Shape[1] <= 0 || tensor.Shape[2] <= 0) throw new VisualException(VisualErrorCodes.TensorInvalid, "A table-structure output must be a non-empty rank-three tensor.", profileId: context.Profile.ProfileId, tensorName: name, modelId: context.Profile.ModelId, technicalDetails: tensor.Shape.ToString());
        }

        private static VisualException Failure(VisualDecodeContext context, string message, string tensorName, string? details = null) => new VisualException(VisualErrorCodes.DecodeFailed, message, profileId: context.Profile.ProfileId, tensorName: tensorName, modelId: context.Profile.ModelId, technicalDetails: details);

        private const int LocationsWidth = 8;
    }
}

#pragma warning restore CS1591
