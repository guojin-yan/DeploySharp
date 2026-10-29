using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using RectangleF = JYPPX.DeploySharp.Geometry.RectangleF;
using PointF = JYPPX.DeploySharp.Geometry.PointF;
using TextRegion = JYPPX.DeploySharp.Visual.TextRegion;
using CoreOperations = JYPPX.OpenCvSharp.Core.Cv2;
using JYPPX.DeploySharp.Backends.OnnxRuntime;

internal sealed record DocumentJob(DocumentPage[] Pages, float MinimumTextConfidence = .8f);
internal sealed record DocumentPage(string Id, string Title, string Image, string Kind = "auto",
    string DocumentId = "default", bool AutoOrient = true, string CellTextMode = "rec");
internal sealed record ArchiveLine(string Text, float Confidence, PointF[] Polygon, PointF[]? SourcePolygon = null);
internal sealed record ArchiveRegion(int Index, string Category, RectangleF Bounds, string Status,
    ArchiveLine[] Lines, string? TablePath, string[] Diagnostics, string? Latex = null,
    string? ArtifactPath = null, string? Engine = null, PointF[]? SourcePolygon = null);
internal sealed record ArchivePage(string Id, string Title, int PageIndex, int Width, int Height,
    string Status, ArchiveRegion[] Regions, string? Error, string DocumentId = "default",
    int Rotation = 0, float OrientationConfidence = 1, int SourceWidth = 0, int SourceHeight = 0,
    string[]? ReviewReasons = null);

internal sealed class DocumentArchive(BackendRegistry backends, BackendRequest request, AppOptions options) : IDisposable
{
    private readonly Dictionary<string, VisualPipeline> _pipelines = new();
    private OcrService? _ocr;
    private float _minimumConfidence;
    private OcrService Ocr => _ocr ??= new OcrService(backends, request, options.Models);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly HashSet<string> TextLabels = new(StringComparer.Ordinal)
        { "text", "paragraph_title", "doc_title", "figure_title", "table_title", "abstract", "reference", "header", "footer", "footnote", "content" };

    public async Task<int> RunAsync(CancellationToken token)
    {
        var job = JsonSerializer.Deserialize<DocumentJob>(File.ReadAllText(options.Manifest)) ?? throw new InvalidDataException("Empty document job.");
        if (!float.IsFinite(job.MinimumTextConfidence) || job.MinimumTextConfidence is < 0 or > 1 ||
            job.Pages is not { Length: > 0 and <= 100 } || job.Pages.Any(p => p is null || !SafeId(p.Id) || !SafeId(p.DocumentId) ||
            string.IsNullOrWhiteSpace(p.Image) || string.IsNullOrWhiteSpace(p.Title) || p.Kind is not ("auto" or "table" or "text" or "formula") ||
            p.CellTextMode is not ("rec" or "lines")) ||
            job.Pages.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != job.Pages.Length)
            throw new InvalidDataException("Provide 1—100 pages, unique safe page IDs, safe DocumentId, image/title, Kind=auto|table|text|formula and CellTextMode=rec|lines.");
        _minimumConfidence = job.MinimumTextConfidence;
        string root = Path.Combine(options.Output, options.Backend);
        Directory.CreateDirectory(root);
        var pages = new List<ArchivePage>();
        for (int pageIndex = 0; pageIndex < job.Pages.Length; pageIndex++)
        {
            token.ThrowIfCancellationRequested();
            DocumentPage page = job.Pages[pageIndex];
            string directory = Path.Combine(root, page.Id);
            Directory.CreateDirectory(directory);
            try { pages.Add(await RunPageAsync(page, pageIndex, directory, token)); }
            catch (Exception error) when (error is not OperationCanceledException && !token.IsCancellationRequested)
            {
                var failed = new ArchivePage(page.Id, page.Title, pageIndex, 0, 0, "failed", [], error.Message, page.DocumentId);
                SaveJson(Path.Combine(directory, "page.json"), failed);
                pages.Add(failed);
                Console.Error.WriteLine($"{page.Id}: failed: {error.Message}");
            }
        }
        SaveJson(Path.Combine(root, "archive.json"), new { deploySharp = "2.0.0", options.Backend, job.MinimumTextConfidence, pages });
        WorkbenchOutput.Write(root, pages, job.MinimumTextConfidence);
        Console.WriteLine($"Completed: pages={pages.Count}; regions={pages.Sum(p => p.Regions.Length)}; tables={pages.Sum(p => p.Regions.Count(r => r.TablePath != null))}; output={root}");
        return pages.Any(p => p.Status == "failed") ? 1 : pages.Any(p => p.Status == "needs-review") ? 3 : 0;
    }

    private async Task<ArchivePage> RunPageAsync(DocumentPage page, int pageIndex, string directory, CancellationToken token)
    {
        string path = Path.GetFullPath(page.Image, Path.GetDirectoryName(options.Manifest)!);
        using var original = ImageCodecs.ImRead(path);
        if (original.Rows <= 0 || original.Cols <= 0) throw new InvalidDataException("Cannot decode " + path);
        // PNG fixes the decoded source coordinate system, including EXIF normalization.
        string sourcePath = Path.Combine(directory, "source.png");
        WriteImage(sourcePath, original);
        int rotation = 0; float orientationConfidence = 1;
        if (page.AutoOrient)
        {
            using var sourceImage = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromFile(sourcePath), token);
            var orientation = PaddleDocumentProfiles.CreateClassification(PaddleDocumentModelCatalog.Get("paddle-doc/pp-lcnet-x1-0-doc-ori"),
                PaddleDocumentProfiles.DocumentOrientationLabels, VisualTaskId.DocumentOrientation);
            var prediction = (await InferAsync(orientation, "pp-lcnet-x1-0-doc-ori.onnx", sourceImage,
                new RectangleF(0, 0, sourceImage.SourceSize.Width, sourceImage.SourceSize.Height), token)).GetValue<ClassificationResult>().Predictions[0];
            orientationConfidence = prediction.Score;
            if (prediction.Score >= .8f) rotation = int.Parse(prediction.Label.Split('_')[0]);
        }
        using var upright = rotation switch
        {
            90 => CoreOperations.Rotate(original, RotateFlags.Rotate90Counterclockwise),
            180 => CoreOperations.Rotate(original, RotateFlags.Rotate180),
            270 => CoreOperations.Rotate(original, RotateFlags.Rotate90Clockwise),
            _ => original.Clone()
        };
        string uprightPath = Path.Combine(directory, "upright.png");
        WriteImage(uprightPath, upright);
        using var image = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromFile(uprightPath), token);
        using var canvas = upright.Clone();
        IReadOnlyList<PaddleDocumentRegion> regions;
        if (page.Kind != "auto") regions = [new PaddleDocumentRegion(page.Kind, 1, new RectangleF(0, 0, image.SourceSize.Width, image.SourceSize.Height))];
        else
        {
            var layout = PaddleDocumentProfiles.CreatePaddleNmsRegions(PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"),
                PaddleDocumentProfiles.Layout23Labels, new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: .5f);
            var result = await InferAsync(layout, "pp-doclayout-l.onnx", image,
                new RectangleF(0, 0, image.SourceSize.Width, image.SourceSize.Height), token);
            regions = result.GetValue<DetectionResult>().Detections.Select(d => new PaddleDocumentRegion(d.Label.Label, d.Label.Score, d.Box)).ToArray();
        }
        bool layoutFallback = regions.Count == 0;
        if (layoutFallback) regions = [new PaddleDocumentRegion("text", 1, new RectangleF(0, 0, image.SourceSize.Width, image.SourceSize.Height))];
        var outputs = new List<ArchiveRegion>();
        for (int index = 0; index < regions.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            PaddleDocumentRegion region = regions[index];
            RectangleF bounds = Clip(region.Bounds, image.SourceSize);
            DrawBox(canvas, bounds, $"{index} {region.Category}", new Scalar(70, 175, 30));
            if (region.Category == "table")
            {
                string tableDir = Path.Combine(directory, $"table-{index:D2}");
                Directory.CreateDirectory(tableDir);
                try { outputs.Add(await RunTableAsync(index, pageIndex, image, upright, bounds, tableDir, page.CellTextMode, token)); }
                catch (Exception error) when (error is not OperationCanceledException && !token.IsCancellationRequested)
                { outputs.Add(new(index, region.Category, bounds, "needs-review", [], null, [error.Message])); }
            }
            else if (region.Category == "formula")
            {
                string formulaDir = Path.Combine(directory, $"formula-{index:D2}"); Directory.CreateDirectory(formulaDir);
                try { outputs.Add(await RunFormulaAsync(index, upright, bounds, formulaDir, token)); }
                catch (Exception error) when (error is not OperationCanceledException && !token.IsCancellationRequested)
                { outputs.Add(new(index, "formula", bounds, "needs-review", [], null, [error.Message])); }
            }
            else if (TextLabels.Contains(region.Category) || region.Category == "seal")
            {
                var text = await Ocr.RecognizeAsync(image, bounds, token);
                var lines = text.Regions.OrderBy(x => x.Region.Polygon.Vertices.Min(p => p.Y))
                    .ThenBy(x => x.Region.Polygon.Vertices.Min(p => p.X)).Select(x => new ArchiveLine(
                        x.Recognition.Text, x.Recognition.Confidence, x.Region.Polygon.Vertices.ToArray())).ToArray();
                outputs.Add(new(index, region.Category, bounds, lines.Length == 0 ? "needs-review" : "completed", lines, null,
                    lines.Length == 0 ? ["No text detected in this region."] : []));
                foreach (var line in lines)
                    for (int i = 0; i < line.Polygon.Length; i++)
                        Drawing.Line(canvas, CvPoint(line.Polygon[i]), CvPoint(line.Polygon[(i + 1) % line.Polygon.Length]), new Scalar(20, 90, 240), 1);
            }
            else outputs.Add(new(index, region.Category, bounds, "kept-as-image", [], null, []));
        }
        var mapping = new PageCoordinateMap(original.Cols, original.Rows, rotation);
        outputs = outputs.Select(r => r with { Engine = r.Engine ?? options.Backend, SourcePolygon = mapping.Map(r.Bounds),
            Lines = r.Lines.Select(l => l with { SourcePolygon = l.Polygon.Select(mapping.ToSource).ToArray() }).ToArray() }).ToList();
        var reasons = new List<string>();
        if (page.AutoOrient && orientationConfidence < .8f) reasons.Add("orientation-low-confidence");
        if (layoutFallback) reasons.Add("layout-empty-text-fallback");
        if (outputs.Any(r => r.Status == "needs-review")) reasons.Add("region-needs-review");
        if (outputs.Any(r => r.Lines.Any(l => string.IsNullOrWhiteSpace(l.Text) || l.Confidence < _minimumConfidence))) reasons.Add("text-needs-review");
        var report = new ArchivePage(page.Id, page.Title, pageIndex, image.SourceSize.Width, image.SourceSize.Height,
            reasons.Count > 0 ? "needs-review" : "completed", outputs.ToArray(), null, page.DocumentId,
            rotation, orientationConfidence, original.Cols, original.Rows, reasons.ToArray());
        SaveJson(Path.Combine(directory, "page.json"), report);
        Save(Path.Combine(directory, "page.txt"), string.Join(Environment.NewLine + Environment.NewLine,
            outputs.Where(r => r.Lines.Length > 0).Select(r => string.Join(Environment.NewLine, r.Lines.Select(l => l.Text)))));
        WriteImage(Path.Combine(directory, "regions.jpg"), canvas);
        Console.WriteLine($"{page.Id}: rotation={rotation}; regions={outputs.Count}; text-lines={outputs.Sum(r => r.Lines.Length)}; tables={outputs.Count(r => r.Category == "table")}; formulas={outputs.Count(r => r.Latex != null)}; status={report.Status}");
        return report;
    }

    private async Task<ArchiveRegion> RunTableAsync(int index, int pageIndex, OpenCvDecodedRoiImage image,
        Mat original, RectangleF bounds, string directory, string cellTextMode, CancellationToken token)
    {
        using var crop = original.SubMat(new Rect((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height));
        WriteImage(Path.Combine(directory, "crop.png"), crop);
        var cls = PaddleDocumentProfiles.CreateClassification(PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls"),
            ["wired_table", "wireless_table"], VisualTaskId.TableClassification);
        var prediction = (await InferAsync(cls, "pp-lcnet-x1-0-table-cls.onnx", image, bounds, token)).GetValue<ClassificationResult>().Predictions[0];
        string type = prediction.Label switch { "wired_table" => "wired", "wireless_table" => "wireless", _ => throw new InvalidDataException("Unknown table type.") };
        var cellProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(PaddleDocumentModelCatalog.Get($"paddle-table/rt-detr-l-{type}-cell-det"),
            ["table-cell"], new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: .3f);
        var cellInference = await InferAsync(cellProfile, $"rt-detr-l-{type}-cell-det.onnx", image, bounds, token);
        var cellResult = new PaddleDocumentRegionResult(PaddleDocumentModule.TableCellDetection,
            Metadata(cellProfile.Descriptor), cellInference.GetValue<DetectionResult>().Detections.Select(d => new PaddleDocumentRegion(d.Label.Label, d.Label.Score, d.Box)));
        var structure = PaddleDocumentProfiles.CreateTableStructure(PaddleDocumentModelCatalog.Get($"paddle-table/slanext-{type}"), modelSize: new VisualSize(512, 512));
        string suffix = options.Backend == "openvino" ? "-openvino-compat" : "";
        var table = (await InferAsync(structure, $"slanext-{type}{suffix}.onnx", image, bounds, token)).GetValue<PaddleDocumentTableResult>();
        var tableResult = new PaddleDocumentTableResult(PaddleDocumentModule.TableStructureRecognition, Metadata(structure.Descriptor),
            table.Markup, table.Regions, type, table.Warnings, table.Tokens, table.AverageScore);
        var alignment = PaddleDocumentTableCellAligner.Align(tableResult, cellResult, image.SourceSize.Width, image.SourceSize.Height);
        SaveJson(Path.Combine(directory, "structure.json"), new { type, tableResult.Markup, tableResult.Warnings, alignment });
        using var canvas = original.Clone();
        foreach (var match in alignment.Matches) DrawBox(canvas, match.Bounds, match.StructureCellIndex.ToString(), new Scalar(0, 130, 240));
        WriteImage(Path.Combine(directory, "cells.jpg"), canvas);
        if (!alignment.Succeeded)
        {
            Save(Path.Combine(directory, "table.html"), Html("<h1>表格需要复核</h1><pre>" + E(table.Markup) + "</pre><p>" + E(string.Join(", ", alignment.Diagnostics)) + "</p>"));
            return new(index, "table", bounds, "needs-review", [], $"table-{index:D2}/table.html", alignment.Diagnostics.ToArray());
        }
        var cellLines = new Dictionary<int, ArchiveLine>();
        if (cellTextMode == "lines")
        {
            foreach (var match in alignment.Matches)
            {
                var cellOcr = await Ocr.RecognizeAsync(image, match.Bounds, token);
                var sorted = cellOcr.Regions.OrderBy(l => l.Region.Polygon.Vertices.Min(p => p.Y)).ThenBy(l => l.Region.Polygon.Vertices.Min(p => p.X)).ToArray();
                cellLines[match.StructureCellIndex] = new ArchiveLine(string.Join("\n", sorted.Select(l => l.Recognition.Text)),
                    sorted.Length == 0 ? 0 : sorted.Min(l => l.Recognition.Confidence), CellRegion(match).Polygon.Vertices.ToArray());
            }
        }
        else
        {
            using var recognition = image.CreateRecognitionInput();
            var requested = alignment.Matches.Select(m => CellRegion(m)).ToArray();
            var recognized = await Ocr.Pipeline.RecognizeOnlyAsync(recognition, requested, token);
            foreach (var line in recognized) cellLines[line.Region.SourceIndex] = new(line.Recognition.Text, line.Recognition.Confidence, line.Region.Polygon.Vertices.ToArray());
        }
        var lines = Enumerable.Range(0, alignment.StructureCellCount).Select(i => cellLines[i]).ToArray();
        string[] texts = lines.Select(l => l.Text).ToArray();
        string markup = PaddleDocumentTableCellAligner.FillMarkup(tableResult, alignment, texts);
        Save(Path.Combine(directory, "table.html"), Html("<h1>" + E(type) + " table</h1>" + markup));
        SaveJson(Path.Combine(directory, "cells.json"), new { type, cellTextMode, alignment.RowCount, alignment.StructureCellCount, lines });
        return new(index, "table", bounds, "completed", lines, $"table-{index:D2}/table.html", table.Warnings.ToArray());

        PaddleDocumentResultMetadata Metadata(PaddleDocumentModelDescriptor descriptor) =>
            new(descriptor, request.BackendId!.Value.Value, TimeSpan.Zero, image.InputId!, pageIndex);
    }

    private async Task<ArchiveRegion> RunFormulaAsync(int index, Mat original, RectangleF bounds, string directory, CancellationToken token)
    {
        string cropPath = Path.Combine(directory, "crop.png");
        using (var crop = original.SubMat(new Rect((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height))) WriteImage(cropPath, crop);
        var tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(Path.Combine(options.Models, "pp-formulanet-plus-s.inference.yml"));
        var profile = PaddleDocumentProfiles.CreateFormula(PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s"),
            new PaddleDocumentFormulaSchema(tokenizer, endTokenId: 2, startTokenId: 0, padTokenId: 1, unknownTokenId: 3));
        var formulaRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var pipeline = GetPipeline(profile, "pp-formulanet-plus-s.onnx", formulaRequest);
        using var input = new OpenCvVisualInputFactory().CreateFromFile(cropPath, profile.VisualProfile, cancellationToken: token);
        var formula = (await pipeline.RunAsync(input, cancellationToken: token)).GetValue<PaddleDocumentFormulaResult>();
        SaveJson(Path.Combine(directory, "formula.json"), new { formula.Latex, formula.FinishReason, formula.TokenIds, formula.Warnings, backend = "ort-cpu" });
        Save(Path.Combine(directory, "formula.tex"), formula.Latex);
        var diagnostics = formula.Warnings.ToList();
        if (!BalancedBraces(formula.Latex)) diagnostics.Add("latex-unbalanced-braces");
        bool complete = formula.FinishReason == GenerationFinishReason.EndOfSequence && !string.IsNullOrWhiteSpace(formula.Latex) && diagnostics.Count == 0;
        return new(index, "formula", bounds, complete ? "completed" : "needs-review", [], null, diagnostics.ToArray(),
            formula.Latex, $"formula-{index:D2}/formula.json", "ort-cpu");
    }

    private async Task<VisualInferenceResult> InferAsync(PaddleDocumentProfile profile, string file,
        OpenCvDecodedRoiImage image, RectangleF bounds, CancellationToken token)
    {
        var pipeline = GetPipeline(profile, file, request);
        using var input = image.PrepareRectangle(new RectangleRoiGeometry(bounds), profile.VisualProfile.Input.Name,
            profile.VisualProfile.Preprocessing!.ToOpenCvOptions(), token);
        using var geometry = profile.VisualProfile.AuxiliaryInputs.Count == 0 ? null : OpenCvPaddleDocumentPreprocessing.RebindWithGeometryInputs(input, profile);
        return await pipeline.RunAsync(geometry ?? input, cancellationToken: token);
    }

    private VisualPipeline GetPipeline(PaddleDocumentProfile profile, string file, BackendRequest backendRequest)
    {
        string key = backendRequest.BackendId + ":" + file;
        if (!_pipelines.TryGetValue(key, out var pipeline))
        {
            var registry = new VisualProfileRegistry(); registry.Register(profile.VisualProfile); registry.Freeze();
            pipeline = new VisualPipeline(backends, registry.Select(new ModelArtifact(profile.VisualProfile.ModelId, "onnx", Path.Combine(options.Models, file)),
                backends, backendRequest, profile.VisualProfile.Task), backendRequest);
            _pipelines.Add(key, pipeline);
        }
        return pipeline;
    }

    private static TextRegion CellRegion(PaddleDocumentTableCellMatch match)
    {
        var b = match.Bounds;
        var q = new TextQuadrilateral(new PointF(b.X, b.Y), new PointF(b.Right, b.Y), new PointF(b.Right, b.Bottom), new PointF(b.X, b.Bottom), TextCornerOrder.TopLeftClockwise);
        return new TextRegion(match.StructureCellIndex, match.Score, q.Polygon, q);
    }
    private static RectangleF Clip(RectangleF b, VisualSize s)
    {
        int x = Math.Clamp((int)Math.Floor(b.X), 0, s.Width - 1), y = Math.Clamp((int)Math.Floor(b.Y), 0, s.Height - 1);
        int r = Math.Clamp((int)Math.Ceiling(b.Right), x + 1, s.Width), d = Math.Clamp((int)Math.Ceiling(b.Bottom), y + 1, s.Height);
        return new RectangleF(x, y, r - x, d - y);
    }
    private static Point CvPoint(PointF p) => new((int)p.X, (int)p.Y);
    private static bool BalancedBraces(string text)
    {
        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == '{') depth++;
            if (text[i] == '}' && --depth < 0) return false;
        }
        return depth == 0;
    }
    private static bool SafeId(string? value) => Regex.IsMatch(value ?? "", "^[a-z0-9][a-z0-9-]{0,63}$");
    private static void DrawBox(Mat m, RectangleF b, string label, Scalar c)
    {
        Drawing.Rectangle(m, new Point((int)b.X, (int)b.Y), new Point((int)b.Right, (int)b.Bottom), c, 2);
        Drawing.PutText(m, label, new Point((int)b.X, Math.Max(15, (int)b.Y)), JYPPX.OpenCvSharp.ImgProc.HersheyFonts.HersheySimplex, .5, c, 1);
    }
    private static void WriteImage(string path, Mat image) { if (!ImageCodecs.ImWrite(path, image)) throw new IOException("Cannot save " + path); }
    private static void Save(string path, string text) => File.WriteAllText(path, text, new UTF8Encoding(false));
    private static void SaveJson(string path, object data) => Save(path, JsonSerializer.Serialize(data, Json));
    private static string E(string? value) => WebUtility.HtmlEncode(value) ?? "";
    private static string Html(string body) => "<!doctype html><html lang=zh-CN><meta charset=utf-8><title>DeploySharp 文档归档</title><style>body{font:17px system-ui;background:#eff4f7;color:#15354a;max-width:1180px;margin:32px auto;padding:0 20px}article{background:white;border-radius:16px;padding:24px;margin:20px 0}img{max-width:100%;max-height:540px;object-fit:contain}pre{white-space:pre-wrap}table{border-collapse:collapse}td,th{border:1px solid #8898a5;padding:8px}a{color:#187964}.status{background:#e3f1eb;padding:4px 10px;border-radius:8px}summary{cursor:pointer}input{padding:12px;width:60%;border:1px solid #b5c4cc;border-radius:8px}</style><body>" + body + "</body></html>";
    public void Dispose()
    {
        _ocr?.Dispose();
        foreach (var pipeline in _pipelines.Values.Reverse()) pipeline.Dispose();
    }
}
