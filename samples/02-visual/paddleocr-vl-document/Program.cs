using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 4 || args.Length > 5)
        {
            Console.Error.WriteLine("Usage: dotnet run -- <tokenizer-root> <bundle-root> <pages.json> <output-directory> [page-parallelism]");
            return 2;
        }
        string manifestPath = Path.GetFullPath(args[2]);
        DocumentSpec spec = JsonSerializer.Deserialize<DocumentSpec>(File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty document manifest.");
        bool automaticTasks = string.Equals(spec.Task, "Auto", StringComparison.OrdinalIgnoreCase);
        PaddleOcrVlTask task = default;
        if (!automaticTasks && (!Enum.TryParse(spec.Task, true, out task) || !Enum.IsDefined(typeof(PaddleOcrVlTask), task)))
            throw new InvalidDataException("Unknown VL task.");
        var overrides = spec.TaskByCategory?.Select(mapping => new KeyValuePair<string, PaddleOcrVlTask?>(mapping.Key, ParseOptionalTask(mapping.Value)));
        PaddleOcrVlTaskRouter router = PaddleOcrVlTaskRouter.CreatePpStructure(overrides, ParseOptionalTask(spec.UnmatchedCategoryTask));
        if (!automaticTasks && (spec.TaskByCategory != null || spec.UnmatchedCategoryTask != null))
            throw new InvalidDataException("Category routing options require task=Auto.");
        if (spec.LayoutDetector != null && (!automaticTasks || spec.Pages.Any(page => page.Regions.Length != 0)))
            throw new InvalidDataException("layoutDetector requires task=Auto and no manual regions.");
        if (spec.LayoutDetector != null && (spec.LayoutDetector.ModelId != "paddle-doc/pp-doclayout-l" || string.IsNullOrWhiteSpace(spec.LayoutDetector.Path)))
            throw new InvalidDataException("This detector example requires paddle-doc/pp-doclayout-l and an explicit ONNX path.");
        if (spec.Pages.Length == 0 || spec.Pages.Select(page => page.PageIndex).Distinct().Count() != spec.Pages.Length)
            throw new InvalidDataException("Pages must be nonempty and have unique pageIndex values.");
        int parallelism = args.Length == 5 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1;
        if (parallelism < 1 || parallelism > 8) throw new ArgumentOutOfRangeException(nameof(parallelism), "Page parallelism must be between 1 and 8.");
        string output = Path.GetFullPath(args[3]);
        Directory.CreateDirectory(output);
        PaddleDocumentPage[] pages = spec.Pages.Select(page => new PaddleDocumentPage(
            spec.LayoutDetector != null
                ? OpenCvImageSource.FromFile(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, page.Image)))
                : (object)new PageInput(OpenCvImageSource.FromFile(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, page.Image))), page),
            new VisualSize(page.Width, page.Height), page.PageIndex)).ToArray();

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1,
            enableMemoryPattern: false, enableCpuMemoryArena: false));
        using var session = PaddleOcrVlOnnxSession.Create(registry, Path.GetFullPath(args[1]),
            new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
        var tokenizer = new PaddleOcrVlTokenizer(Path.GetFullPath(args[0]));
        var layoutModel = new PaddleDocumentModelDescriptor("application/manual-regions", "Caller-supplied regions",
            PaddleDocumentModule.LayoutDetection, "https://github.com/guojin-yan/DeploySharp", "page-rectangles");
        var vlModel = new PaddleDocumentModelDescriptor("paddleocr-vl/1.6", "PaddleOCR-VL-1.6",
            automaticTasks ? PaddleDocumentModule.VisionLanguageParsing : PaddleDocumentVisionLanguageResult.ModuleForTask(task),
            "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
        var layout = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, token) =>
        {
            token.ThrowIfCancellationRequested();
            var input = (PageInput)context.Page.Source;
            return Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                new PaddleDocumentResultMetadata(layoutModel, "manual-layout", TimeSpan.Zero, input.Source.Sha256, context.Page.PageIndex),
                input.Spec.Regions.Select(region => new PaddleDocumentRegion(region.Category, 1f,
                    new RectangleF(region.X, region.Y, region.Width, region.Height)))));
        });
        var generationOptions = new PaddleOcrVlGenerationOptions(maximumNewTokens: spec.MaximumNewTokens, timeout: TimeSpan.FromMinutes(15));
        IPaddleDocumentPipelineStage vl = automaticTasks
            ? OpenCvPaddleOcrVlLayoutStage.FromSession(vlModel, "onnxruntime-cpu", router, session, tokenizer,
                page => ((PageInput)page.Source).Source, generationOptions, continueOnRegionFailure: true)
            : OpenCvPaddleOcrVlRegionStage.FromSession(vlModel, "onnxruntime-cpu", task, session, tokenizer,
                page => ((PageInput)page.Source).Source, generationOptions, continueOnRegionFailure: true);
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { layout, vl });
        IReadOnlyList<PaddleDocumentPipelineResult> results;
        if (spec.LayoutDetector != null)
        {
            var model = PaddleDocumentModelCatalog.Get(spec.LayoutDetector.ModelId);
            var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(model, PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: spec.LayoutDetector.ScoreThreshold);
            var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            string modelPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, spec.LayoutDetector.Path));
            using var detector = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(modelPath), registry, request, VisualTaskId.LayoutDetection), request);
            var detectedPipeline = OpenCvPaddleOcrVlDocumentPipeline.FromSession(detector, model, vlModel, "onnxruntime-cpu", session, tokenizer,
                generationOptions, router, maximumRegions: spec.LayoutDetector.MaximumRegions,
                maximumPagePixels: spec.LayoutDetector.MaximumPagePixels, continueOnRegionFailure: true);
            results = await detectedPipeline.RunManyAsync(pages, parallelism, cancellation.Token);
        }
        else
        {
            results = parallelism == 1
                ? await pipeline.RunManyAsync(pages, cancellation.Token)
                : await pipeline.RunManyConcurrentAsync(pages, parallelism, cancellation.Token);
        }
        bool incomplete = false;
        foreach (PaddleDocumentPipelineResult result in results)
        {
            int index = result.Page.PageIndex;
            File.WriteAllText(Path.Combine(output, "page-" + index + ".json"), PaddleDocumentPipelineExport.ToJson(result));
            File.WriteAllText(Path.Combine(output, "page-" + index + ".md"), PaddleDocumentPipelineExport.ToMarkdown(result));
            int succeeded, failed, skipped;
            IReadOnlyList<PaddleDocumentVisionLanguageRegionExecution> executions;
            if (automaticTasks)
            {
                var generated = result.GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(vl.Module);
                succeeded = generated.SucceededCount; failed = generated.FailedCount; skipped = generated.SkippedCount;
                executions = generated.Executions;
            }
            else
            {
                var generated = result.GetRequired<PaddleDocumentVisionLanguagePageResult>(vl.Module);
                succeeded = generated.SucceededCount; failed = generated.FailedCount; skipped = 0;
                executions = generated.Executions;
            }
            incomplete |= failed > 0 || executions.Any(region => !region.Skipped && region.Result?.FinishReason != GenerationFinishReason.EndOfSequence);
            foreach (PaddleDocumentVisionLanguageRegionExecution execution in executions)
            {
                PaddleOcrVlSpottingParseResult? spotting = execution.Result?.Spotting;
                if (spotting != null)
                {
                    incomplete |= !spotting.Succeeded;
                    Console.WriteLine($"page={index} region={execution.RegionIndex} spotting={spotting.Status} polygons={spotting.Regions.Count} pageCoordinates={spotting.PageCoordinatesAvailable}");
                }
                PaddleOcrVlTableParseResult? table = execution.Result?.Table;
                if (table == null) continue;
                incomplete |= !table.Succeeded;
                if (table.Succeeded)
                    File.WriteAllText(Path.Combine(output, "page-" + index + "-region-" + execution.RegionIndex + ".table.html"), table.Html!);
                Console.WriteLine($"page={index} region={execution.RegionIndex} table={table.Status} rows={table.RowCount} columns={table.ColumnCount}");
            }
            Console.WriteLine($"page={index} succeeded={succeeded} failed={failed} skipped={skipped}");
        }
        return incomplete ? 1 : 0;
    }

    private static PaddleOcrVlTask? ParseOptionalTask(string? value)
    {
        if (value == null) return null;
        if (!Enum.TryParse(value, true, out PaddleOcrVlTask task) || !Enum.IsDefined(typeof(PaddleOcrVlTask), task))
            throw new InvalidDataException("Unknown routed VL task: " + value);
        return task;
    }

    private sealed record PageInput(OpenCvImageSource Source, PageSpec Spec);
    private sealed class DocumentSpec
    {
        public string Task { get; set; } = "TextRecognition";
        public int MaximumNewTokens { get; set; } = 512;
        public Dictionary<string, string?>? TaskByCategory { get; set; }
        public string? UnmatchedCategoryTask { get; set; }
        public LayoutDetectorSpec? LayoutDetector { get; set; }
        public PageSpec[] Pages { get; set; } = Array.Empty<PageSpec>();
    }
    private sealed class LayoutDetectorSpec
    {
        public string ModelId { get; set; } = "paddle-doc/pp-doclayout-l";
        public string Path { get; set; } = "";
        public float ScoreThreshold { get; set; } = .3f;
        public int MaximumRegions { get; set; } = 64;
        public int MaximumPagePixels { get; set; } = 128 * 1024 * 1024;
    }
    private sealed class PageSpec
    {
        public string Image { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int PageIndex { get; set; }
        public RegionSpec[] Regions { get; set; } = Array.Empty<RegionSpec>();
    }
    private sealed class RegionSpec
    {
        public string Category { get; set; } = "text";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}
