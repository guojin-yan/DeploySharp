using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using RectangleF = JYPPX.DeploySharp.Geometry.RectangleF;
using PointF = JYPPX.DeploySharp.Geometry.PointF;
using TextRegion = JYPPX.DeploySharp.Visual.TextRegion;
using OcrResult = JYPPX.DeploySharp.Visual.OcrResult;

internal sealed class StructureDemo : IDisposable
{
    private readonly string _imagePath, _modelRoot, _backend, _output;
    private readonly BackendRegistry _backends = new();
    private readonly List<IDisposable> _sessions = new();
    private readonly BackendRequest _request;
    private readonly OpenCvVisualInputFactory _factory = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public StructureDemo(string imagePath, string modelRoot, string backend, string mode)
    {
        _imagePath = Path.GetFullPath(imagePath);
        _modelRoot = Path.GetFullPath(modelRoot);
        _backend = backend;
        _output = Path.GetFullPath(Path.Combine("results", backend, mode));
        // Validate before creating any native sessions.
        if (!File.Exists(_imagePath)) throw new FileNotFoundException("Input image is missing.", _imagePath);
        BackendId id = backend switch
        {
            "ort-cpu" => OnnxRuntimeBackendProvider.BackendId,
            "openvino" => OpenVinoBackendProvider.BackendId,
            _ => throw new ArgumentException("Backend must be ort-cpu or openvino.")
        };
        if (backend == "openvino") _backends.UseOpenVino();
        else _backends.UseOnnxRuntime();
        _request = new BackendRequest(BackendCapabilities.TensorInference, id, "cpu");
        Directory.CreateDirectory(_output);
    }

    public async Task RunPageAsync(CancellationToken token)
    {
        OpenCvImageSource source = OpenCvImageSource.FromFile(_imagePath);
        using OpenCvDecodedRoiImage image = _factory.DecodeForRois(source);
        var descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
        var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(descriptor,
            PaddleDocumentProfiles.Layout23Labels, new VisualSize(640, 640),
            includeGeometryInputs: true, scoreThreshold: .5f);
        VisualPipeline layout = DocumentSession(profile, "pp-doclayout-l.onnx");
        var det = OcrProfile("ppocrv5/mobile-det", 1);
        var rec = OcrProfile("ppocrv5/mobile-rec", 16);
        OcrPipeline ocr = Own(new OcrPipeline(_backends, Select(det.VisualProfile, "ppocrv5-mobile-det.onnx"),
            _request, Select(rec.VisualProfile, "ppocrv5-mobile-rec.onnx"), _request, rec.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 512, maximumRecognitionBatch: 16)));
        var textLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            "text", "paragraph_title", "doc_title", "figure_title", "table_title",
            "abstract", "reference", "header", "footer", "footnote", "content",
            "aside_text", "number", "chart_title", "formula_number"
        };
        var lines = new List<PageLine>();
        var blocks = new List<object>();
        var layoutStage = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection,
            async (context, ct) =>
            {
                using var input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, profile, source.Sha256, ct);
                VisualInferenceResult inference = await layout.RunAsync(input, cancellationToken: ct);
                var result = inference.GetValue<DetectionResult>();
                return new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                    Metadata(descriptor, inference, source, context.Page.PageIndex),
                    result.Detections.Select(d => new PaddleDocumentRegion(d.Label.Label, d.Label.Score, d.Box)));
            });
        var ocrStage = new PaddleDocumentDependentStage(PaddleDocumentModule.TextRecognition,
            new[] { PaddleDocumentModule.LayoutDetection }, async (context, ct) =>
            {
                var watch = Stopwatch.StartNew();
                var regions = context.TryGet<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection)!.Regions;
                var items = new List<PaddleDocumentTextItem>();
                // This is a geometric display order, not a learned newspaper reading order.
                var ordered = regions.Select((region, index) => (region, index))
                    .Where(x => textLabels.Contains(x.region.Category))
                    .OrderBy(x => x.region.Bounds.Y).ThenBy(x => x.region.Bounds.X);
                foreach (var (region, index) in ordered)
                {
                    ct.ThrowIfCancellationRequested();
                    RectangleF box = Clip(region.Bounds, image.SourceSize);
                    var options = OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(
                        new VisualSize((int)box.Width, (int)box.Height), resizeLong: 960);
                    var detectorInput = image.PrepareRectangle(new RectangleRoiGeometry(box),
                        det.VisualProfile.Input.Name, options, ct);
                    using var input = new PageRoiOcrInput(image, detectorInput);
                    OcrResult result = await ocr.RunAsync(input, cancellationToken: ct);
                    var blockLines = result.Regions.OrderBy(x => x.Region.Polygon.Vertices.Min(p => p.Y))
                        .ThenBy(x => x.Region.Polygon.Vertices.Min(p => p.X)).ToArray();
                    foreach (var line in blockLines)
                        lines.Add(new PageLine(index, region.Category, line.Recognition.Text,
                            line.Recognition.Confidence, line.Region.Polygon.Vertices.ToArray()));
                    string text = string.Join(Environment.NewLine, blockLines.Select(x => x.Recognition.Text));
                    items.Add(new PaddleDocumentTextItem(index, text,
                        blockLines.Length == 0 ? 0 : blockLines.Average(x => x.Recognition.Confidence), box));
                    blocks.Add(new { layoutIndex = index, category = region.Category, lineCount = blockLines.Length, text });
                }
                return new PaddleDocumentTextResult(new PaddleDocumentResultMetadata(
                    new PaddleDocumentModelDescriptor("paddleocr/ppocrv5/mobile-rec", "PP-OCRv5 mobile REC",
                        PaddleDocumentModule.TextRecognition,
                        "https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr", "onnx"),
                    _request.BackendId!.Value.Value, watch.Elapsed, source.Sha256, context.Page.PageIndex), items);
            });
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { layoutStage, ocrStage });
        var pageResult = await pipeline.RunAsync(new PaddleDocumentPage(source, image.SourceSize), token);
        var regionsResult = pageResult.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection);
        Save("page.json", PaddleDocumentPipelineExport.ToJson(pageResult));
        SaveJson("page-lines.json", new { backend = _backend, sourceSize = image.SourceSize, blocks, lines });
        Save("page.txt", string.Join(Environment.NewLine + Environment.NewLine,
            pageResult.GetRequired<PaddleDocumentTextResult>(PaddleDocumentModule.TextRecognition).Items.Select(x => x.Text)));
        DrawPage(regionsResult.Regions, lines);
        Console.WriteLine($"page: backend={_backend}; layout_regions={regionsResult.Regions.Count}; ocr_blocks={blocks.Count}; text_lines={lines.Count}");
        foreach (var line in lines.Take(8)) Console.WriteLine($"[{line.LayoutIndex}/{line.Category}] {line.Text}");
        Console.WriteLine($"Saved complete results: {_output}");
    }

    public async Task RunTableAsync(CancellationToken token)
    {
        OpenCvImageSource source = OpenCvImageSource.FromFile(_imagePath);
        using OpenCvDecodedRoiImage image = _factory.DecodeForRois(source);
        var clsDescriptor = PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls");
        var clsProfile = PaddleDocumentProfiles.CreateClassification(clsDescriptor,
            new[] { "wired_table", "wireless_table" }, VisualTaskId.TableClassification);
        var classifier = DocumentSession(clsProfile, "pp-lcnet-x1-0-table-cls.onnx");
        string tableType = "";
        var classificationStage = new PaddleDocumentPipelineStage(PaddleDocumentModule.TableClassification,
            async (context, ct) =>
            {
                using var input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, clsProfile, source.Sha256, ct);
                var inference = await classifier.RunAsync(input, cancellationToken: ct);
                var best = inference.GetValue<ClassificationResult>().Predictions[0];
                tableType = best.Label switch
                {
                    "wired_table" => "wired", "wireless_table" => "wireless",
                    _ => throw new InvalidOperationException("Unknown table type: " + best.Label)
                };
                return new PaddleDocumentClassificationResult(PaddleDocumentModule.TableClassification,
                    Metadata(clsDescriptor, inference, source, context.Page.PageIndex), best.Label, best.Index, best.Score);
            });
        var cellStage = new PaddleDocumentDependentStage(PaddleDocumentModule.TableCellDetection,
            new[] { PaddleDocumentModule.TableClassification }, async (context, ct) =>
            {
                var descriptor = PaddleDocumentModelCatalog.Get($"paddle-table/rt-detr-l-{tableType}-cell-det");
                var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(descriptor,
                    new[] { "table-cell" }, new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: .3f);
                var detector = DocumentSession(profile, $"rt-detr-l-{tableType}-cell-det.onnx");
                using var input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, profile, source.Sha256, ct);
                var inference = await detector.RunAsync(input, cancellationToken: ct);
                return new PaddleDocumentRegionResult(PaddleDocumentModule.TableCellDetection,
                    Metadata(descriptor, inference, source, context.Page.PageIndex),
                    inference.GetValue<DetectionResult>().Detections.Select(d => new PaddleDocumentRegion(d.Label.Label, d.Label.Score, d.Box)));
            });
        var structureStage = new PaddleDocumentDependentStage(PaddleDocumentModule.TableStructureRecognition,
            new[] { PaddleDocumentModule.TableCellDetection }, async (context, ct) =>
            {
                var descriptor = PaddleDocumentModelCatalog.Get($"paddle-table/slanext-{tableType}");
                var profile = PaddleDocumentProfiles.CreateTableStructure(descriptor, modelSize: new VisualSize(512, 512));
                string suffix = _backend == "openvino" ? "-openvino-compat" : "";
                var structure = DocumentSession(profile, $"slanext-{tableType}{suffix}.onnx");
                using var input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, profile, source.Sha256, ct);
                var inference = await structure.RunAsync(input, cancellationToken: ct);
                var value = inference.GetValue<PaddleDocumentTableResult>();
                return new PaddleDocumentTableResult(PaddleDocumentModule.TableStructureRecognition,
                    Metadata(descriptor, inference, source, context.Page.PageIndex), value.Markup,
                    value.Regions, tableType, value.Warnings, value.Tokens, value.AverageScore);
            });
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[]
            { classificationStage, cellStage, structureStage });
        var result = await pipeline.RunAsync(new PaddleDocumentPage(source, image.SourceSize), token);
        var table = result.GetRequired<PaddleDocumentTableResult>(PaddleDocumentModule.TableStructureRecognition);
        var cells = result.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.TableCellDetection);
        var alignment = PaddleDocumentTableCellAligner.Align(table, cells, image.SourceSize.Width, image.SourceSize.Height);
        Save("table.json", PaddleDocumentPipelineExport.ToJson(result));
        SaveJson("alignment.json", alignment);
        DrawCells(cells.Regions, alignment);
        if (!alignment.Succeeded)
        {
            Save("table.html", HtmlDocument("<p>Cell alignment failed; OCR was not filled.</p><pre>"
                + WebUtility.HtmlEncode(table.Markup) + "</pre>"));
            throw new InvalidOperationException("Cell alignment failed: " + string.Join(", ", alignment.Diagnostics));
        }
        var rec = OcrProfile("ppocrv5/mobile-rec", 16);
        var recognizer = Own(OcrPipeline.CreateRecognitionOnly(_backends,
            Select(rec.VisualProfile, "ppocrv5-mobile-rec.onnx"), _request, rec.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 512, maximumRecognitionBatch: 16)));
        using var recognitionInput = image.CreateRecognitionInput();
        var regions = alignment.Matches.Select(match => CellRegion(match, image.SourceSize)).ToArray();
        var recognized = await recognizer.RecognizeOnlyAsync(recognitionInput, regions, token);
        if (recognized.Count != alignment.StructureCellCount) throw new InvalidOperationException("Incomplete OCR output.");
        var byIndex = recognized.ToDictionary(x => x.Region.SourceIndex);
        string[] texts = Enumerable.Range(0, alignment.StructureCellCount).Select(i => byIndex[i].Recognition.Text).ToArray();
        string html = PaddleDocumentTableCellAligner.FillMarkup(table, alignment, texts);
        Save("table.html", HtmlDocument(html));
        SaveJson("cell-texts.json", new
        {
            backend = _backend, tableType, sourceSize = image.SourceSize,
            cells = alignment.Matches.Select(m => new
            {
                m.StructureCellIndex, m.RowIndex, m.CellIndexInRow, m.DetectionIndex, m.Bounds,
                text = texts[m.StructureCellIndex], confidence = byIndex[m.StructureCellIndex].Recognition.Confidence
            })
        });
        Console.WriteLine($"table: backend={_backend}; type={tableType}; rows={alignment.RowCount}; cells={cells.Regions.Count}; alignment={alignment.Succeeded}");
        Console.WriteLine("Structure warnings: " + string.Join(", ", table.Warnings));
        foreach (string text in texts.Take(8)) Console.WriteLine(text);
        Console.WriteLine($"Saved complete results: {_output}");
    }

    private VisualPipeline DocumentSession(PaddleDocumentProfile profile, string file) =>
        Own(new VisualPipeline(_backends, Select(profile.VisualProfile, file), _request));

    private VisualProfileSelection Select(VisualModelProfile profile, string file)
    {
        var registry = new VisualProfileRegistry();
        registry.Register(profile);
        registry.Freeze();
        string path = Path.Combine(_modelRoot, file);
        if (!File.Exists(path)) throw new FileNotFoundException("Required model is missing.", path);
        return registry.Select(new ModelArtifact(profile.ModelId, "onnx", path), _backends, _request, profile.Task);
    }

    private PaddleOcrProfile OcrProfile(string id, int maximumBatch)
    {
        var row = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/" + id));
        var contract = new PaddleOcrArtifactContract(row.ReleaseArtifact!.Opset,
            "", "", "", "Apache-2.0", "local-input-v1", "local-output-v1", "onnx");
        if (row.Family == "det") return PaddleOcrProfiles.CreateDetection(row.ModelId, contract,
            row.InputName, row.OutputName, postprocess: new PaddleDbPostprocessOptions(maximumRegions: 512));
        var characters = PaddleOcrProfiles.LoadCharacterSet(Path.Combine(_modelRoot, "ppocrv5_dict.txt"),
            "local.v5", "1", useSpaceCharacter: true);
        return PaddleOcrModelCatalog.CreateProfile(row, contract, characters, maximumBatch);
    }

    private static PaddleDocumentResultMetadata Metadata(PaddleDocumentModelDescriptor descriptor,
        VisualInferenceResult inference, OpenCvImageSource source, int pageIndex) =>
        new(descriptor, inference.BackendId.Value, inference.Timing.Total, source.Sha256, pageIndex);

    private T Own<T>(T session) where T : IDisposable { _sessions.Add(session); return session; }
    private void Save(string name, string value) => File.WriteAllText(Path.Combine(_output, name), value, new UTF8Encoding(false));
    private void SaveJson(string name, object value) => Save(name, JsonSerializer.Serialize(value, JsonOptions));

    private static RectangleF Clip(RectangleF bounds, VisualSize size)
    {
        int left = Math.Clamp((int)Math.Floor(bounds.X), 0, size.Width - 1);
        int top = Math.Clamp((int)Math.Floor(bounds.Y), 0, size.Height - 1);
        int right = Math.Clamp((int)Math.Ceiling(bounds.Right), left + 1, size.Width);
        int bottom = Math.Clamp((int)Math.Ceiling(bounds.Bottom), top + 1, size.Height);
        return new RectangleF(left, top, right - left, bottom - top);
    }

    private static TextRegion CellRegion(PaddleDocumentTableCellMatch match, VisualSize size)
    {
        // Keep detector sub-pixel coordinates; do not round outward into table borders.
        var b = match.Bounds;
        float left = Math.Max(0, b.X), top = Math.Max(0, b.Y);
        float right = Math.Min(size.Width, b.Right), bottom = Math.Min(size.Height, b.Bottom);
        if (right <= left || bottom <= top) throw new InvalidOperationException("Empty cell crop.");
        var quad = new TextQuadrilateral(new PointF(left, top), new PointF(right, top),
            new PointF(right, bottom), new PointF(left, bottom), TextCornerOrder.TopLeftClockwise);
        return new TextRegion(match.StructureCellIndex, match.Score, quad.Polygon, quad);
    }

    private void DrawPage(IReadOnlyList<PaddleDocumentRegion> regions, IReadOnlyList<PageLine> lines)
    {
        using var layout = ImageCodecs.ImRead(_imagePath);
        using var text = layout.Clone();
        for (int i = 0; i < regions.Count; i++) DrawBox(layout, regions[i].Bounds, $"{i}: {regions[i].Category}", new Scalar(70, 155, 20));
        foreach (var line in lines)
        {
            var points = line.Polygon.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();
            for (int i = 0; i < points.Length; i++) Drawing.Line(text, points[i], points[(i + 1) % points.Length], new Scalar(10, 100, 240), 2);
        }
        WriteImage("layout.jpg", layout);
        WriteImage("page-ocr.jpg", text);
    }

    private void DrawCells(IReadOnlyList<PaddleDocumentRegion> cells, PaddleDocumentTableCellAlignmentResult alignment)
    {
        using var canvas = ImageCodecs.ImRead(_imagePath);
        foreach (var match in alignment.Matches) DrawBox(canvas, cells[match.DetectionIndex].Bounds,
            match.StructureCellIndex.ToString(), new Scalar(0, 130, 240));
        if (!alignment.Succeeded)
            for (int i = 0; i < cells.Count; i++) DrawBox(canvas, cells[i].Bounds, "det " + i, new Scalar(0, 130, 240));
        WriteImage("cells.jpg", canvas);
    }

    private static void DrawBox(Mat canvas, RectangleF b, string label, Scalar color)
    {
        Drawing.Rectangle(canvas, new Point((int)b.X, (int)b.Y), new Point((int)b.Right, (int)b.Bottom), color, 2);
        Drawing.PutText(canvas, label, new Point(Math.Max(0, (int)b.X), Math.Max(15, (int)b.Y + 14)),
            HersheyFonts.HersheySimplex, .45, color, 1);
    }

    private void WriteImage(string name, Mat image)
    {
        if (!ImageCodecs.ImWrite(Path.Combine(_output, name), image)) throw new IOException("Cannot save result image.");
    }

    private static string HtmlDocument(string body) => "<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">"
        + "<title>DeploySharp PP-Structure table</title><style>body{font:18px system-ui;margin:32px}"
        + "table{border-collapse:collapse}td,th{border:1px solid #728294;padding:12px 18px}pre{white-space:pre-wrap}</style></head><body>"
        + body + "</body></html>";

    public void Dispose()
    {
        for (int i = _sessions.Count - 1; i >= 0; i--) _sessions[i].Dispose();
        _backends.Dispose();
    }

    private sealed record PageLine(int LayoutIndex, string Category, string Text, float Confidence, IReadOnlyList<PointF> Polygon);
}
