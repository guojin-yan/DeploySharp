using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
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
        string tableType = options.TableType + "_table";
        string cellId = "paddle-table/rt-detr-l-" + options.TableType + "-cell-det";
        string structureId = "paddle-table/slanext-" + options.TableType;
        string releaseStructureId = options.Backend == "openvino" ? structureId + "-openvino-compat" : structureId;
        ModelFile classificationModel;
        ModelFile cellModel;
        ModelFile structureModel;
        if (options.ModelDirectory != null)
        {
            classificationModel = ModelFile.FromLocal(Path.Combine(options.ModelDirectory, "pp-lcnet-x1-0-table-cls.onnx"));
            cellModel = ModelFile.FromLocal(Path.Combine(options.ModelDirectory, "rt-detr-l-" + options.TableType + "-cell-det.onnx"));
            structureModel = ModelFile.FromLocal(Path.Combine(options.ModelDirectory,
                options.Backend == "openvino" ? "slanext-" + options.TableType + "-openvino-compat.onnx" : "slanext-" + options.TableType + ".onnx"));
        }
        else
        {
            using var release = new PaddleOcrReleaseClient(new PaddleOcrReleaseClientOptions(options.Cache, offline: options.Offline));
            classificationModel = ModelFile.FromRelease(await release.GetModelAsync("paddle-table/pp-lcnet-x1-0-table-cls", cancellationToken: cancellationToken).ConfigureAwait(false));
            cellModel = ModelFile.FromRelease(await release.GetModelAsync(cellId, cancellationToken: cancellationToken).ConfigureAwait(false));
            structureModel = ModelFile.FromRelease(await release.GetModelAsync(releaseStructureId, cancellationToken: cancellationToken).ConfigureAwait(false));
        }

        PaddleDocumentModelDescriptor classificationDescriptor = PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls");
        PaddleDocumentModelDescriptor cellDescriptor = PaddleDocumentModelCatalog.Get(cellId);
        PaddleDocumentModelDescriptor structureDescriptor = PaddleDocumentModelCatalog.Get(structureId);
        PaddleDocumentProfile classificationProfile = PaddleDocumentProfiles.CreateClassification(classificationDescriptor,
            new[] { "wired_table", "wireless_table" }, VisualTaskId.TableClassification, modelSize: new VisualSize(224, 224));
        PaddleDocumentProfile cellProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(cellDescriptor,
            new[] { "table-cell" }, new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: 0);
        PaddleDocumentProfile structureProfile = PaddleDocumentProfiles.CreateTableStructure(structureDescriptor, modelSize: new VisualSize(512, 512));

        using var classificationRegistry = CreateRegistry(options.Backend);
        using var cellRegistry = CreateRegistry(options.Backend);
        using var structureRegistry = CreateRegistry(options.Backend);
        BackendId backendId = options.Backend == "openvino" ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
        var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, options.Backend == "openvino" ? "CPU" : "cpu");
        using VisualPipeline classification = CreatePipeline(classificationRegistry, classificationProfile, classificationModel, request, backendId);
        using VisualPipeline cells = CreatePipeline(cellRegistry, cellProfile, cellModel, request, backendId);
        using VisualPipeline structure = CreatePipeline(structureRegistry, structureProfile, structureModel, request, backendId);

        var imageSource = OpenCvImageSource.FromFile(options.Image);
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
                if (selected.Label != tableType)
                    throw new InvalidOperationException("Classifier selected '" + selected.Label + "', but --table-type is '" + options.TableType + "'. Rerun with the matching table type.");
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
                using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromPage(image, structureProfile, sourceSha, token);
                VisualInferenceResult inference = await structure.RunAsync(input, cancellationToken: token).ConfigureAwait(false);
                PaddleDocumentTableResult value = inference.GetValue<PaddleDocumentTableResult>();
                return new PaddleDocumentTableResult(PaddleDocumentModule.TableStructureRecognition,
                    new PaddleDocumentResultMetadata(structureDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex),
                    value.Markup, value.Regions, value.TableType, value.Warnings, value.Tokens, value.AverageScore);
            });

        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { structureStage, cellStage, classificationStage });
        PaddleDocumentPipelineResult result = await pipeline.RunAsync(page, cancellationToken).ConfigureAwait(false);
        PaddleDocumentTableResult table = result.GetRequired<PaddleDocumentTableResult>(PaddleDocumentModule.TableStructureRecognition);
        Directory.CreateDirectory(options.Output);
        string jsonPath = Path.Combine(options.Output, "table-result.json");
        string htmlPath = Path.Combine(options.Output, "table-result.html");
        await File.WriteAllTextAsync(jsonPath, PaddleDocumentPipelineExport.ToJson(result), cancellationToken).ConfigureAwait(false);
        string tableElement = table.Markup.IndexOf("<table", StringComparison.OrdinalIgnoreCase) >= 0
            ? table.Markup : "<table>" + table.Markup + "</table>";
        string htmlDocument = "<!doctype html><html><head><meta charset=\"utf-8\"><title>PP-Structure table</title></head><body>"
            + tableElement + "</body></html>";
        await File.WriteAllTextAsync(htmlPath, htmlDocument, cancellationToken).ConfigureAwait(false);
        Console.WriteLine("Backend: " + options.Backend + "; table: " + tableType + "; detected cells: " + result.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.TableCellDetection).Regions.Count);
        Console.WriteLine("Structure tokens: " + table.Tokens.Count + "; regions: " + table.Regions.Count + "; warnings: " + string.Join(", ", table.Warnings));
        Console.WriteLine("JSON: " + jsonPath);
        Console.WriteLine("HTML: " + htmlPath);
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

    private static void PrintUsage() => Console.WriteLine(
        "Usage: dotnet run --project samples/02-visual/paddle-structure-table -- --image <path> --output <directory> [--table-type wired|wireless] [--backend ort|openvino] [--cache <directory> | --model-dir <directory>] [--offline]");

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
        private Options(string image, string output, string tableType, string backend, string cache, string? modelDirectory, bool offline)
        { Image = image; Output = output; TableType = tableType; Backend = backend; Cache = cache; ModelDirectory = modelDirectory; Offline = offline; }

        public string Image { get; }
        public string Output { get; }
        public string TableType { get; }
        public string Backend { get; }
        public string Cache { get; }
        public string? ModelDirectory { get; }
        public bool Offline { get; }

        public static Options Parse(string[] args)
        {
            string? image = null, output = null, cache = null, modelDirectory = null;
            string tableType = "wired", backend = "ort";
            bool offline = false;
            for (int index = 0; index < args.Length; index++)
            {
                string key = args[index];
                if (key == "--offline") { offline = true; continue; }
                if (index + 1 >= args.Length) throw new ArgumentException("Missing value for " + key);
                string value = args[++index];
                switch (key)
                {
                    case "--image": image = value; break;
                    case "--output": output = value; break;
                    case "--table-type": tableType = value.ToLowerInvariant(); break;
                    case "--backend": backend = value.ToLowerInvariant(); break;
                    case "--cache": cache = value; break;
                    case "--model-dir": modelDirectory = value; break;
                    default: throw new ArgumentException("Unknown option: " + key);
                }
            }
            if (string.IsNullOrWhiteSpace(image) || string.IsNullOrWhiteSpace(output)) throw new ArgumentException("--image and --output are required.");
            if (tableType != "wired" && tableType != "wireless") throw new ArgumentException("--table-type must be wired or wireless.");
            if (backend != "ort" && backend != "openvino") throw new ArgumentException("--backend must be ort or openvino.");
            if (modelDirectory != null && (offline || cache != null)) throw new ArgumentException("--model-dir cannot be combined with --offline or --cache.");
            return new Options(Path.GetFullPath(image), Path.GetFullPath(output), tableType, backend,
                Path.GetFullPath(cache ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeploySharp", "models")),
                modelDirectory == null ? null : Path.GetFullPath(modelDirectory), offline);
        }
    }
}
