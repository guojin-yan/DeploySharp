using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using OcrTextRegion = JYPPX.DeploySharp.Visual.TextRegion;

internal static class Program
{
    private const string V5DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";
    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            PrintUsage();
            return 0;
        }

        Options options;
        try { options = Options.Parse(args); }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            PrintUsage();
            return 2;
        }

        try
        {
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            await RunAsync(options, cancellation.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) { return 130; }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task RunAsync(Options options, CancellationToken cancellationToken)
    {
        using PaddleOcrReleaseClient? release = options.ModelDirectory == null
            ? new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(options.Cache, offline: options.Offline))
            : null;
        var imageSource = OpenCvImageSource.FromFile(options.Image);
        ModelFile classificationModel = await ResolveModelAsync("paddle-table/pp-lcnet-x1-0-table-cls",
            "pp-lcnet-x1-0-table-cls.onnx", options, release, cancellationToken).ConfigureAwait(false);

        PaddleDocumentModelDescriptor classificationDescriptor = PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls");
        PaddleDocumentProfile classificationProfile = PaddleDocumentProfiles.CreateClassification(classificationDescriptor,
            new[] { "wired_table", "wireless_table" }, VisualTaskId.TableClassification, modelSize: new VisualSize(224, 224));

        using var classificationRegistry = CreateRegistry(options.Backend);
        using var cellRegistry = CreateRegistry(options.Backend);
        using var structureRegistry = CreateRegistry(options.Backend);
        BackendId backendId = options.Backend == "openvino" ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
        var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, options.Backend == "openvino" ? "CPU" : "cpu");
        using VisualPipeline classification = CreatePipeline(classificationRegistry, classificationProfile, classificationModel, request, backendId);
        VisualPipeline? cells = null;
        VisualPipeline? structure = null;

        var inputFactory = new OpenCvVisualInputFactory();
        using OpenCvDecodedRoiImage image = inputFactory.DecodeForRois(imageSource, cancellationToken);
        var page = new PaddleDocumentPage(image, image.SourceSize);
        string sourceSha = imageSource.Sha256;

        var classificationStage = new PaddleDocumentPipelineStage(PaddleDocumentModule.TableClassification, async (context, token) =>
        {
            using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, classificationProfile, sourceSha, token);
            VisualInferenceResult inference = await classification.RunAsync(input, cancellationToken: token).ConfigureAwait(false);
            LabelScore top = inference.GetValue<ClassificationResult>().TopPrediction
                ?? throw new InvalidOperationException("The table classifier returned no prediction.");
            return new PaddleDocumentClassificationResult(PaddleDocumentModule.TableClassification,
                new PaddleDocumentResultMetadata(classificationDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex),
                top.Label, top.Index, top.Score);
        });
        var cellStage = new PaddleDocumentDependentStage(PaddleDocumentModule.TableCellDetection,
            new[] { PaddleDocumentModule.TableClassification }, async (context, token) =>
            {
                PaddleDocumentClassificationResult selected = context.TryGet<PaddleDocumentClassificationResult>(PaddleDocumentModule.TableClassification)!;
                string tableType = SelectTableType(selected.Label, options.TableType);
                string cellId = "paddle-table/rt-detr-l-" + tableType + "-cell-det";
                PaddleDocumentModelDescriptor cellDescriptor = PaddleDocumentModelCatalog.Get(cellId);
                PaddleDocumentProfile cellProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(cellDescriptor,
                    new[] { "table-cell" }, new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: options.CellScoreThreshold);
                string filename = "rt-detr-l-" + tableType + "-cell-det.onnx";
                ModelFile cellModel = await ResolveModelAsync(cellId, filename, options, release, token).ConfigureAwait(false);
                cells = CreatePipeline(cellRegistry, cellProfile, cellModel, request, backendId);
                using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, cellProfile, sourceSha, token);
                VisualInferenceResult inference = await cells.RunAsync(input, cancellationToken: token).ConfigureAwait(false);
                DetectionResult value = inference.GetValue<DetectionResult>();
                var regions = value.Detections.Select(item => new PaddleDocumentRegion(item.Label.Label, item.Label.Score, item.Box)).ToArray();
                return new PaddleDocumentRegionResult(PaddleDocumentModule.TableCellDetection,
                    new PaddleDocumentResultMetadata(cellDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex), regions);
            });
        var structureStage = new PaddleDocumentDependentStage(PaddleDocumentModule.TableStructureRecognition,
            new[] { PaddleDocumentModule.TableCellDetection }, async (context, token) =>
            {
                PaddleDocumentClassificationResult selected = context.TryGet<PaddleDocumentClassificationResult>(PaddleDocumentModule.TableClassification)!;
                string tableType = SelectTableType(selected.Label, options.TableType);
                string structureId = "paddle-table/slanext-" + tableType;
                string releaseStructureId = options.Backend == "openvino" ? structureId + "-openvino-compat" : structureId;
                string filename = options.Backend == "openvino" ? "slanext-" + tableType + "-openvino-compat.onnx" : "slanext-" + tableType + ".onnx";
                PaddleDocumentModelDescriptor structureDescriptor = PaddleDocumentModelCatalog.Get(structureId);
                PaddleDocumentProfile structureProfile = PaddleDocumentProfiles.CreateTableStructure(structureDescriptor, modelSize: new VisualSize(512, 512));
                ModelFile structureModel = await ResolveModelAsync(releaseStructureId, filename, options, release, token).ConfigureAwait(false);
                structure = CreatePipeline(structureRegistry, structureProfile, structureModel, request, backendId);
                using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, structureProfile, sourceSha, token);
                VisualInferenceResult inference = await structure.RunAsync(input, cancellationToken: token).ConfigureAwait(false);
                PaddleDocumentTableResult value = inference.GetValue<PaddleDocumentTableResult>();
                return new PaddleDocumentTableResult(PaddleDocumentModule.TableStructureRecognition,
                    new PaddleDocumentResultMetadata(structureDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex),
                    value.Markup, value.Regions, value.TableType, value.Warnings, value.Tokens, value.AverageScore);
            });

        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { structureStage, cellStage, classificationStage });
        PaddleDocumentPipelineResult result;
        try { result = await pipeline.RunAsync(page, cancellationToken).ConfigureAwait(false); }
        finally { structure?.Dispose(); cells?.Dispose(); }
        PaddleDocumentTableResult table = result.GetRequired<PaddleDocumentTableResult>(PaddleDocumentModule.TableStructureRecognition);
        PaddleDocumentRegionResult detectedCells = result.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.TableCellDetection);
        PaddleDocumentTableCellAlignmentResult alignment = PaddleDocumentTableCellAligner.Align(table, detectedCells,
            image.SourceSize.Width, image.SourceSize.Height);
        string tableType = SelectTableType(result.GetRequired<PaddleDocumentClassificationResult>(PaddleDocumentModule.TableClassification).Label, options.TableType);
        Directory.CreateDirectory(options.Output);
        string jsonPath = Path.Combine(options.Output, "table-result.json");
        string htmlPath = Path.Combine(options.Output, "table-result.html");
        string alignmentPath = Path.Combine(options.Output, "table-cell-alignment.json");
        string ocrPath = Path.Combine(options.Output, "table-cell-ocr.json");
        await File.WriteAllTextAsync(jsonPath, PaddleDocumentPipelineExport.ToJson(result), cancellationToken).ConfigureAwait(false);
        var alignmentOutput = new
        {
            alignment.Succeeded,
            alignment.RowCount,
            alignment.StructureCellCount,
            alignment.DetectionCount,
            alignment.Diagnostics,
            Matches = alignment.Matches.Select(match => new
            {
                match.RowIndex, match.CellIndexInRow, match.StructureCellIndex, match.DetectionIndex,
                match.Score,
                Bounds = new { match.Bounds.X, match.Bounds.Y, match.Bounds.Width, match.Bounds.Height }
            })
        };
        await File.WriteAllTextAsync(alignmentPath, JsonSerializer.Serialize(alignmentOutput, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
        bool invalidMarkup = alignment.Diagnostics.Any(value => value == "invalid-structure-markup" ||
            value == "structure-too-long" || value == "incomplete-structure");
        string tableElement = invalidMarkup
            ? "<pre>" + WebUtility.HtmlEncode(table.Markup) + "</pre>"
            : "<table>" + table.Markup + "</table>";
        object ocrOutput = new { status = "skipped", reason = options.StructureOnly ? "structure-only" : string.Join(",", alignment.Diagnostics) };
        Exception? ocrFailure = null;
        if (alignment.Succeeded && !options.StructureOnly)
        {
            try
            {
                (string filled, object output) = await RecognizeCellsAsync(image, alignment, table, options, release, request, backendId, cancellationToken).ConfigureAwait(false);
                tableElement = filled;
                ocrOutput = output;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                ocrFailure = exception;
                ocrOutput = new { status = "failed", reason = exception.Message };
            }
        }
        await File.WriteAllTextAsync(ocrPath, JsonSerializer.Serialize(ocrOutput, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
        string htmlDocument = "<!doctype html><html><head><meta charset=\"utf-8\"><title>PP-Structure table</title></head><body>"
            + tableElement + "</body></html>";
        await File.WriteAllTextAsync(htmlPath, htmlDocument, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Backend: " + options.Backend + "; table: " + tableType + "; detected cells: " + detectedCells.Regions.Count);
        Console.WriteLine("Structure tokens: " + table.Tokens.Count + "; regions: " + table.Regions.Count + "; warnings: " + string.Join(", ", table.Warnings));
        Console.WriteLine("Cell alignment: " + (alignment.Succeeded ? "validated" : string.Join(", ", alignment.Diagnostics)));
        Console.WriteLine("JSON: " + jsonPath);
        Console.WriteLine("HTML: " + htmlPath);
        Console.WriteLine("Alignment: " + alignmentPath);
        Console.WriteLine("Cell OCR: " + ocrPath);
        if (ocrFailure != null) throw new InvalidOperationException("Table cell OCR failed; the structure-only HTML and diagnostic JSON remain available.", ocrFailure);
    }

    private static async Task<(string Html, object Output)> RecognizeCellsAsync(OpenCvDecodedRoiImage image,
        PaddleDocumentTableCellAlignmentResult alignment, PaddleDocumentTableResult table, Options options,
        PaddleOcrReleaseClient? release, BackendRequest request, BackendId backendId, CancellationToken cancellationToken)
    {
        const string modelId = "paddleocr/ppocrv5/mobile-rec";
        string dictionaryPath;
        ModelFile model;
        if (options.ModelDirectory == null)
        {
            PaddleOcrReleaseModelMaterialization downloaded = await (release ?? throw new InvalidOperationException("Release client is unavailable."))
                .GetModelAsync("ppocrv5/mobile-rec", cancellationToken: cancellationToken).ConfigureAwait(false);
            model = ModelFile.FromRelease(downloaded);
            dictionaryPath = downloaded.DictionaryPath ?? throw new InvalidOperationException("The PP-OCRv5 recognition dictionary is missing from the Release asset.");
        }
        else
        {
            model = ModelFile.FromLocal(options.RecognizerModel ?? Path.Combine(options.ModelDirectory, "PP-OCRv5_mobile_rec.onnx"));
            dictionaryPath = options.RecognizerDictionary ?? Path.Combine(options.ModelDirectory, "ppocrv5_dict.txt");
        }
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "paddle-structure-table.v5", "v5", true, V5DictionarySha);
        PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(new ModelId(modelId));
        PaddleOcrReleaseArtifact releaseArtifact = descriptor.ReleaseArtifact ?? throw new InvalidOperationException("The PP-OCRv5 recognizer is not registered.");
        var artifact = new PaddleOcrArtifactContract(releaseArtifact.Opset, model.Sha256, "", "", "",
            "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: V5DictionarySha);
        PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, artifact, characters, maximumBatch: 16);
        using var registry = CreateRegistry(options.Backend);
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        VisualProfileSelection selection = profiles.Select(profile.CreateArtifact(model.Path, backendId), registry, request, VisualTaskId.TextRecognition);
        using OcrPipeline recognizer = OcrPipeline.CreateRecognitionOnly(registry, selection, request, profile.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 512, maximumRecognitionBatch: 16));
        using IOcrRecognitionImageInput input = image.CreateRecognitionInput();
        var regions = new List<OcrTextRegion>(alignment.Matches.Count);
        foreach (PaddleDocumentTableCellMatch match in alignment.Matches)
        {
            RectangleF box = match.Bounds;
            float left = Math.Max(0, box.X);
            float top = Math.Max(0, box.Y);
            float right = Math.Min(image.SourceSize.Width, box.Right);
            float bottom = Math.Min(image.SourceSize.Height, box.Bottom);
            if (right - left < 1 || bottom - top < 1) throw new InvalidOperationException("A detected cell has no usable crop area.");
            var quad = new TextQuadrilateral(new PointF(left, top), new PointF(right, top),
                new PointF(right, bottom), new PointF(left, bottom), TextCornerOrder.TopLeftClockwise);
            regions.Add(new OcrTextRegion(match.StructureCellIndex, match.Score, quad.Polygon, quad));
        }
        IReadOnlyList<OcrRegionResult> recognized = await recognizer.RecognizeOnlyAsync(input, regions, cancellationToken).ConfigureAwait(false);
        if (recognized.Count != alignment.Matches.Count) throw new InvalidOperationException("The recognizer returned an incomplete cell batch.");
        string[] texts = new string[recognized.Count];
        for (int index = 0; index < recognized.Count; index++)
        {
            if (recognized[index].Region.SourceIndex != index) throw new InvalidOperationException("The recognizer changed cell order.");
            texts[index] = recognized[index].Recognition.Text;
        }
        string html = PaddleDocumentTableCellAligner.FillMarkup(table, alignment, texts);
        object output = new
        {
            status = "completed", modelId, modelSha256 = model.Sha256, dictionarySha256 = V5DictionarySha,
            backend = options.Backend, cells = alignment.Matches.Select((match, index) => new
            {
                match.StructureCellIndex, match.DetectionIndex, text = texts[index],
                confidence = recognized[index].Recognition.Confidence
            })
        };
        return (html, output);
    }

    private static BackendRegistry CreateRegistry(string backend)
    {
        var registry = new BackendRegistry();
        if (backend == "openvino") registry.UseOpenVino();
        else registry.UseOnnxRuntime();
        return registry;
    }

    private static VisualPipeline CreatePipeline(BackendRegistry registry, PaddleDocumentProfile profile,
        ModelFile model, BackendRequest request, BackendId backendId)
    {
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        ModelArtifact artifact = profile.CreateArtifactWithSha256(model.Path, model.Sha256, backendId);
        return new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.VisualProfile.Task), request);
    }

    private static async Task<ModelFile> ResolveModelAsync(string releaseId, string localName, Options options,
        PaddleOcrReleaseClient? release, CancellationToken cancellationToken)
    {
        if (options.ModelDirectory != null) return ModelFile.FromLocal(Path.Combine(options.ModelDirectory, localName));
        PaddleOcrReleaseModelMaterialization materialized = await (release ?? throw new InvalidOperationException("Release client is unavailable."))
            .GetModelAsync(releaseId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ModelFile.FromRelease(materialized);
    }

    private static string SelectTableType(string label, string requested)
    {
        string selected = label switch
        {
            "wired_table" => "wired",
            "wireless_table" => "wireless",
            _ => throw new InvalidOperationException("The table classifier returned an unsupported label: " + label)
        };
        if (requested != "auto" && requested != selected)
            throw new InvalidOperationException("Classifier selected '" + label + "', but --table-type is '" + requested + "'. Rerun with the matching table type or use auto.");
        return selected;
    }

    private static void PrintUsage() => Console.WriteLine(
        "Usage: dotnet run --project samples/02-visual/paddle-structure-table -- --image <path> --output <directory> [--table-type auto|wired|wireless] [--backend ort|openvino] [--cell-score-threshold 0..1] [--structure-only] [--cache <directory> | --model-dir <directory> [--recognizer-model <onnx> --recognizer-dictionary <txt>]] [--offline]");

    private sealed class ModelFile
    {
        private ModelFile(string path, string sha256) { Path = path; Sha256 = sha256; }
        public string Path { get; }
        public string Sha256 { get; }
        public static ModelFile FromRelease(PaddleOcrReleaseModelMaterialization model) => new ModelFile(model.ModelPath, model.Model.Sha256);
        public static ModelFile FromLocal(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Missing local table model", path);
            using FileStream stream = File.OpenRead(path);
            return new ModelFile(path, Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
        }
    }

    private sealed class Options
    {
        private Options(string image, string output, string tableType, string backend, float cellScoreThreshold, string cache, string? modelDirectory, string? recognizerModel, string? recognizerDictionary, bool offline, bool structureOnly)
        { Image = image; Output = output; TableType = tableType; Backend = backend; CellScoreThreshold = cellScoreThreshold; Cache = cache; ModelDirectory = modelDirectory; RecognizerModel = recognizerModel; RecognizerDictionary = recognizerDictionary; Offline = offline; StructureOnly = structureOnly; }

        public string Image { get; }
        public string Output { get; }
        public string TableType { get; }
        public string Backend { get; }
        public float CellScoreThreshold { get; }
        public string Cache { get; }
        public string? ModelDirectory { get; }
        public string? RecognizerModel { get; }
        public string? RecognizerDictionary { get; }
        public bool Offline { get; }
        public bool StructureOnly { get; }

        public static Options Parse(string[] args)
        {
            string? image = null, output = null, cache = null, modelDirectory = null, recognizerModel = null, recognizerDictionary = null;
            string tableType = "auto", backend = "ort";
            float cellScoreThreshold = 0.5f;
            bool offline = false, structureOnly = false;
            for (int index = 0; index < args.Length; index++)
            {
                string key = args[index];
                if (key == "--offline") { offline = true; continue; }
                if (key == "--structure-only") { structureOnly = true; continue; }
                if (index + 1 >= args.Length) throw new ArgumentException("Missing value for " + key);
                string value = args[++index];
                switch (key)
                {
                    case "--image": image = value; break;
                    case "--output": output = value; break;
                    case "--table-type": tableType = value.ToLowerInvariant(); break;
                    case "--backend": backend = value.ToLowerInvariant(); break;
                    case "--cell-score-threshold":
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out cellScoreThreshold) ||
                            float.IsNaN(cellScoreThreshold) || float.IsInfinity(cellScoreThreshold) || cellScoreThreshold < 0 || cellScoreThreshold > 1)
                            throw new ArgumentException("--cell-score-threshold must be a finite number from 0 to 1 using a decimal point.");
                        break;
                    case "--cache": cache = value; break;
                    case "--model-dir": modelDirectory = value; break;
                    case "--recognizer-model": recognizerModel = value; break;
                    case "--recognizer-dictionary": recognizerDictionary = value; break;
                    default: throw new ArgumentException("Unknown option: " + key);
                }
            }
            if (string.IsNullOrWhiteSpace(image) || string.IsNullOrWhiteSpace(output)) throw new ArgumentException("--image and --output are required.");
            if (tableType != "auto" && tableType != "wired" && tableType != "wireless") throw new ArgumentException("--table-type must be auto, wired or wireless.");
            if (backend != "ort" && backend != "openvino") throw new ArgumentException("--backend must be ort or openvino.");
            if (modelDirectory != null && (offline || cache != null)) throw new ArgumentException("--model-dir cannot be combined with --offline or --cache.");
            if ((recognizerModel == null) != (recognizerDictionary == null) || (recognizerModel != null && modelDirectory == null))
                throw new ArgumentException("--recognizer-model and --recognizer-dictionary must be used together with --model-dir.");
            return new Options(Path.GetFullPath(image), Path.GetFullPath(output), tableType, backend, cellScoreThreshold,
                Path.GetFullPath(cache ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharp", "models")),
                modelDirectory == null ? null : Path.GetFullPath(modelDirectory),
                recognizerModel == null ? null : Path.GetFullPath(recognizerModel),
                recognizerDictionary == null ? null : Path.GetFullPath(recognizerDictionary), offline, structureOnly);
        }
    }
}
