using System;
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
        if (!Enum.TryParse(spec.Task, true, out PaddleOcrVlTask task) || !Enum.IsDefined(typeof(PaddleOcrVlTask), task))
            throw new InvalidDataException("Unknown VL task.");
        if (spec.Pages.Length == 0 || spec.Pages.Select(page => page.PageIndex).Distinct().Count() != spec.Pages.Length)
            throw new InvalidDataException("Pages must be nonempty and have unique pageIndex values.");
        int parallelism = args.Length == 5 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1;
        if (parallelism < 1 || parallelism > 8) throw new ArgumentOutOfRangeException(nameof(parallelism), "Page parallelism must be between 1 and 8.");
        string output = Path.GetFullPath(args[3]);
        Directory.CreateDirectory(output);
        PaddleDocumentPage[] pages = spec.Pages.Select(page => new PaddleDocumentPage(
            new PageInput(OpenCvImageSource.FromFile(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, page.Image))), page),
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
            PaddleDocumentVisionLanguageResult.ModuleForTask(task), "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
        var layout = new PaddleDocumentPipelineStage(PaddleDocumentModule.LayoutDetection, (context, token) =>
        {
            token.ThrowIfCancellationRequested();
            var input = (PageInput)context.Page.Source;
            return Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection,
                new PaddleDocumentResultMetadata(layoutModel, "manual-layout", TimeSpan.Zero, input.Source.Sha256, context.Page.PageIndex),
                input.Spec.Regions.Select(region => new PaddleDocumentRegion(region.Category, 1f,
                    new RectangleF(region.X, region.Y, region.Width, region.Height)))));
        });
        var vl = OpenCvPaddleOcrVlRegionStage.FromSession(vlModel, "onnxruntime-cpu", task, session, tokenizer,
            page => ((PageInput)page.Source).Source,
            new PaddleOcrVlGenerationOptions(maximumNewTokens: spec.MaximumNewTokens, timeout: TimeSpan.FromMinutes(15)),
            continueOnRegionFailure: true);
        var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { layout, vl });
        var results = parallelism == 1
            ? await pipeline.RunManyAsync(pages, cancellation.Token)
            : await pipeline.RunManyConcurrentAsync(pages, parallelism, cancellation.Token);
        bool incomplete = false;
        foreach (PaddleDocumentPipelineResult result in results)
        {
            int index = result.Page.PageIndex;
            File.WriteAllText(Path.Combine(output, "page-" + index + ".json"), PaddleDocumentPipelineExport.ToJson(result));
            File.WriteAllText(Path.Combine(output, "page-" + index + ".md"), PaddleDocumentPipelineExport.ToMarkdown(result));
            PaddleDocumentVisionLanguagePageResult generated = result.GetRequired<PaddleDocumentVisionLanguagePageResult>(vl.Module);
            incomplete |= generated.FailedCount > 0 || generated.Executions.Any(region => region.Result?.FinishReason != GenerationFinishReason.EndOfSequence);
            Console.WriteLine($"page={index} succeeded={generated.SucceededCount} failed={generated.FailedCount}");
        }
        return incomplete ? 1 : 0;
    }

    private sealed record PageInput(OpenCvImageSource Source, PageSpec Spec);
    private sealed class DocumentSpec
    {
        public string Task { get; set; } = "TextRecognition";
        public int MaximumNewTokens { get; set; } = 512;
        public PageSpec[] Pages { get; set; } = Array.Empty<PageSpec>();
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
