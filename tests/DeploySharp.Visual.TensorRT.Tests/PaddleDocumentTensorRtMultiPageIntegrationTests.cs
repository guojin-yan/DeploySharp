using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentTensorRtMultiPageIntegrationTests
{
    private const string OrientationModelId = "paddle-doc/pp-lcnet-x1-0-doc-ori";
    private const string LayoutModelId = "paddle-doc/pp-doclayout-l";
    private const string DefaultOrientationPath = @"E:\Model\PaddleDocument\onnx\pp-lcnet-x1-0-doc-ori.onnx";
    private const string DefaultLayoutPath = @"E:\Model\PaddleDocument\onnx\pp-doclayout-l.onnx";
    private const string DefaultImagePath = @"E:\Data\image\bus.jpg";
    private const string ExpectedOrientationSha256 = "96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0";
    private const string ExpectedLayoutSha256 = "d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9";

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task TwoPageOrientationLayoutRunsThroughTensorRtAndPreservesPageOrder()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE=1 to run the opt-in TensorRT PP-Structure multi-page case.");
        }

        string orientationPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_ORIENTATION_ONNX") ?? DefaultOrientationPath;
        string layoutPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_LAYOUT_ONNX") ?? DefaultLayoutPath;
        string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_IMAGE") ?? DefaultImagePath;
        if (!File.Exists(orientationPath)) Assert.Inconclusive("Missing local PP-Structure orientation ONNX model: " + orientationPath);
        if (!File.Exists(layoutPath)) Assert.Inconclusive("Missing local PP-Structure layout ONNX model: " + layoutPath);
        if (!File.Exists(imagePath)) Assert.Inconclusive("Missing multi-page input: " + imagePath);

        int sessionConcurrency = ReadPositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_SESSION_CONCURRENCY", 2);
        int pageConcurrency = ReadPositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_PAGE_CONCURRENCY", 2);
        string orientationSha256 = ComputeSha256(orientationPath);
        string layoutSha256 = ComputeSha256(layoutPath);
        Assert.AreEqual(ExpectedOrientationSha256, orientationSha256, "The local orientation ONNX differs from the registered Release artifact.");
        Assert.AreEqual(ExpectedLayoutSha256, layoutSha256, "The local layout ONNX differs from the registered Release artifact.");

        PaddleDocumentModelDescriptor orientationDescriptor = PaddleDocumentModelCatalog.Get(OrientationModelId);
        PaddleDocumentProfile orientationProfile = PaddleDocumentProfiles.CreateClassification(
            orientationDescriptor,
            PaddleDocumentProfiles.DocumentOrientationLabels,
            VisualTaskId.DocumentOrientation,
            modelSize: new VisualSize(224, 224));
        PaddleDocumentModelDescriptor layoutDescriptor = PaddleDocumentModelCatalog.Get(LayoutModelId);
        PaddleDocumentProfile layoutProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
            layoutDescriptor,
            PaddleDocumentProfiles.Layout23Labels,
            new VisualSize(640, 640),
            includeGeometryInputs: true,
            scoreThreshold: 0);
        PaddleDocumentProfile orientationTensorRtProfile = AsTensorRtProfile(orientationProfile);
        PaddleDocumentProfile layoutTensorRtProfile = AsTensorRtProfile(layoutProfile);

        string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-document-trt-multipage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string orientationEnginePath = Path.Combine(root, "pp-lcnet-x1-0-doc-ori.engine");
        string layoutEnginePath = Path.Combine(root, "pp-doclayout-l.engine");
        TensorRtApiVersion apiVersion = ResolveApiVersion();
        string architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86";
        TensorRtOnnxEngineBuildResult orientationBuild;
        TensorRtOnnxEngineBuildResult layoutBuild;
        try
        {
            orientationBuild = new TensorRtOnnxEngineBuilder().Build(
                new ModelArtifact(new ModelId(OrientationModelId), "onnx", orientationPath, orientationSha256, TensorRtBackendProvider.BackendId),
                orientationEnginePath,
                new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    workspaceBytes: 268435456UL,
                    optimizationLevel: 3,
                    inputProfiles: new[]
                    {
                        new TensorRtOnnxInputProfile("x", new TensorShape(1, 3, 224, 224), new TensorShape(1, 3, 224, 224), new TensorShape(1, 3, 224, 224))
                    },
                    overwrite: true));
            layoutBuild = new TensorRtOnnxEngineBuilder().Build(
                new ModelArtifact(new ModelId(LayoutModelId), "onnx", layoutPath, layoutSha256, TensorRtBackendProvider.BackendId),
                layoutEnginePath,
                new TensorRtOnnxEngineBuildOptions(
                    apiVersion: apiVersion,
                    workspaceBytes: 536870912UL,
                    optimizationLevel: 3,
                    inputProfiles: new[]
                    {
                        new TensorRtOnnxInputProfile("im_shape", new TensorShape(1, 2), new TensorShape(1, 2), new TensorShape(1, 2)),
                        new TensorRtOnnxInputProfile("image", new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640)),
                        new TensorRtOnnxInputProfile("scale_factor", new TensorShape(1, 2), new TensorShape(1, 2), new TensorShape(1, 2))
                    },
                    overwrite: true));

            using var orientationRegistry = new BackendRegistry().UseTensorRT(new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: architecture));
            using var layoutRegistry = new BackendRegistry().UseTensorRT(new TensorRtBackendOptions(apiVersion, cudaTargetArchitecture: architecture));
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
            using VisualPipeline orientation = CreatePipeline(orientationRegistry, orientationTensorRtProfile, orientationEnginePath, request, sessionConcurrency);
            using VisualPipeline layout = CreatePipeline(layoutRegistry, layoutTensorRtProfile, layoutEnginePath, request, sessionConcurrency);
            string sourceSha = ComputeSha256(imagePath);
            var stages = new IPaddleDocumentPipelineStage[]
            {
                PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
                    PaddleDocumentModule.DocumentOrientation,
                    orientation,
                    (context, token) => new OpenCvVisualInputFactory().CreateFromFile(imagePath, orientationTensorRtProfile.VisualProfile, sourceSha, token),
                    (context, inference) =>
                    {
                        LabelScore top = inference.GetValue<ClassificationResult>().TopPrediction ?? throw new AssertFailedException("Orientation prediction missing.");
                        return new PaddleDocumentOrientationResult(new PaddleDocumentResultMetadata(orientationDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex), top.Label, top.Index * 90);
                    }),
                PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
                    PaddleDocumentModule.LayoutDetection,
                    layout,
                    (context, token) => OpenCvPaddleDocumentPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), imagePath, layoutTensorRtProfile, sourceSha, token),
                    (context, inference) =>
                    {
                        DetectionResult value = inference.GetValue<DetectionResult>();
                        var regions = value.Detections.Select(item => new PaddleDocumentRegion(item.Label.Label, item.Label.Score, item.Box)).ToArray();
                        return new PaddleDocumentRegionResult(PaddleDocumentModule.LayoutDetection, new PaddleDocumentResultMetadata(layoutDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex), regions);
                    })
            };
            var pipeline = new PaddleDocumentPipeline(stages);
            PaddleDocumentPage[] inputPages =
            {
                new PaddleDocumentPage(imagePath, new VisualSize(810, 1080), 0),
                new PaddleDocumentPage(imagePath, new VisualSize(810, 1080), 1)
            };
            Stopwatch sequentialWatch = Stopwatch.StartNew();
            IReadOnlyList<PaddleDocumentPipelineResult> sequential = await pipeline.RunManyAsync(inputPages, CancellationToken.None).ConfigureAwait(false);
            sequentialWatch.Stop();
            AssertPageContract(sequential, sourceSha);
            Stopwatch concurrentWatch = Stopwatch.StartNew();
            IReadOnlyList<PaddleDocumentPipelineResult> concurrent = await pipeline.RunManyConcurrentAsync(inputPages, pageConcurrency, CancellationToken.None).ConfigureAwait(false);
            concurrentWatch.Stop();
            AssertPageContract(concurrent, sourceSha);

            MultiPageBenchmarkEvidence? benchmark = null;
            if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_BENCHMARK") == "1")
            {
                int warmups = ReadPositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_WARMUPS", 5);
                int measurements = ReadPositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_MEASUREMENTS", 50);
                benchmark = new MultiPageBenchmarkEvidence
                {
                    warmupCount = warmups,
                    measurementCount = measurements,
                    sequential = await MeasureRepeatedAsync(() => pipeline.RunManyAsync(inputPages, CancellationToken.None), sourceSha, warmups, measurements).ConfigureAwait(false),
                    concurrent = await MeasureRepeatedAsync(() => pipeline.RunManyConcurrentAsync(inputPages, pageConcurrency, CancellationToken.None), sourceSha, warmups, measurements).ConfigureAwait(false)
                };
            }

            var evidence = new
            {
                schemaVersion = "deploysharp-paddle-document-tensorrt-multipage-v1",
                generatedUtc = DateTimeOffset.UtcNow.ToString("O"),
                sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_SOURCE_REVISION"),
                backend = "tensorrt-cuda",
                apiVersion = (int)apiVersion,
                cudaArchitecture = architecture,
                device = "cuda",
                environment = new
                {
                    machine = Environment.MachineName,
                    operatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    processorCount = Environment.ProcessorCount
                },
                input = new { path = imagePath, sha256 = sourceSha, pageCount = concurrent.Count, sourceSize = new { width = 810, height = 1080 } },
                execution = new
                {
                    sequentialMethod = "PaddleDocumentPipeline.RunManyAsync",
                    concurrentMethod = "PaddleDocumentPipeline.RunManyConcurrentAsync",
                    maxDegreeOfParallelism = pageConcurrency,
                    sessionMaxConcurrency = sessionConcurrency,
                    sequentialWallElapsedMs = sequentialWatch.Elapsed.TotalMilliseconds,
                    sequentialPageElapsedSumMs = sequential.Sum(page => page.Elapsed.TotalMilliseconds),
                    concurrentElapsedMs = concurrentWatch.Elapsed.TotalMilliseconds,
                    benchmark
                },
                engines = new
                {
                    orientation = new { modelId = OrientationModelId, onnxSha256 = orientationSha256, engineSha256 = orientationBuild.EngineSha256, engineBytes = orientationBuild.EngineBytes },
                    layout = new { modelId = LayoutModelId, onnxSha256 = layoutSha256, engineSha256 = layoutBuild.EngineSha256, engineBytes = layoutBuild.EngineBytes }
                },
                pages = concurrent.Select((page, index) => new
                {
                    inputIndex = index,
                    pageIndex = page.Page.PageIndex,
                    inputSha256 = page.GetRequired<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation).Metadata.InputSha256,
                    orientation = page.GetRequired<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation).Label,
                    layoutRegionCount = page.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection).Regions.Count,
                    elapsedMs = page.Elapsed.TotalMilliseconds,
                    timings = page.Timings.ToDictionary(item => item.Module.ToString(), item => item.Elapsed.TotalMilliseconds)
                }).ToArray(),
                boundary = "This is a real two-page TensorRT CUDA page-concurrency and provenance observation on one host; it is not a quality score, a tensor batch benchmark, or a cross-device performance claim."
            };
            WriteEvidence(evidence);
            Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_MULTIPAGE " + JsonSerializer.Serialize(new { sessionConcurrency, pageConcurrency, sequentialMs = sequentialWatch.Elapsed.TotalMilliseconds, concurrentMs = concurrentWatch.Elapsed.TotalMilliseconds, orientationEngineSha256 = orientationBuild.EngineSha256, layoutEngineSha256 = layoutBuild.EngineSha256 }));
        }
        catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable || exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
        {
            Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_BLOCKED errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
            Assert.Inconclusive("TensorRT native runtime or PP-Structure importer is unavailable: " + (exception.TechnicalDetails ?? exception.Message));
        }
        catch (VisualException exception) when (exception.InnerException is TensorRtBackendException backendException)
        {
            Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_BLOCKED errorCode=" + backendException.ErrorCode + ";details=" + (backendException.TechnicalDetails ?? backendException.Message));
            Assert.Inconclusive("TensorRT session could not be created: " + (backendException.TechnicalDetails ?? backendException.Message));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static VisualPipeline CreatePipeline(BackendRegistry registry, PaddleDocumentProfile profile, string enginePath, BackendRequest request, int sessionConcurrency)
    {
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        var artifact = new ModelArtifact(profile.VisualProfile.ModelId, "tensorrt-engine", enginePath, preferredBackend: TensorRtBackendProvider.BackendId);
        return new VisualPipeline(
            registry,
            profiles.Select(artifact, registry, request, profile.VisualProfile.Task),
            request,
            new SessionOptions(sessionConcurrency, false));
    }

    private static PaddleDocumentProfile AsTensorRtProfile(PaddleDocumentProfile source)
    {
        VisualModelProfile original = source.VisualProfile;
        var visual = new VisualModelProfile(
            original.ProfileId + ".tensorrt",
            original.ModelId,
            original.Task,
            original.Version,
            "tensorrt-engine",
            original.Input,
            original.Outputs,
            original.Labels,
            original.Decoder,
            original.RequiredCapabilities,
            original.MinimumBackendVersion,
            original.AuxiliaryInputs,
            original.Preprocessing);
        return new PaddleDocumentProfile(source.Descriptor, visual);
    }

    private static void AssertPageContract(IReadOnlyList<PaddleDocumentPipelineResult> pages, string sourceSha)
    {
        Assert.AreEqual(2, pages.Count);
        CollectionAssert.AreEqual(new[] { 0, 1 }, pages.Select(page => page.Page.PageIndex).ToArray());
        Assert.IsTrue(pages.All(page => page.GetRequired<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation).Metadata.InputSha256 == sourceSha));
        Assert.IsTrue(pages.All(page => page.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection).Regions.Count > 0));
    }

    private static async Task<MultiPageTimingSummary> MeasureRepeatedAsync(
        Func<Task<IReadOnlyList<PaddleDocumentPipelineResult>>> execute,
        string sourceSha,
        int warmupCount,
        int measurementCount)
    {
        for (int index = 0; index < warmupCount; index++) AssertPageContract(await execute().ConfigureAwait(false), sourceSha);
        var samples = new List<double>(measurementCount);
        for (int index = 0; index < measurementCount; index++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            IReadOnlyList<PaddleDocumentPipelineResult> pages = await execute().ConfigureAwait(false);
            watch.Stop();
            AssertPageContract(pages, sourceSha);
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        return new MultiPageTimingSummary
        {
            samplesMs = samples.ToArray(),
            minMs = samples.Min(),
            maxMs = samples.Max(),
            p50Ms = Percentile(samples, .5),
            p95Ms = Percentile(samples, .95)
        };
    }

    private static void WriteEvidence(object evidence)
    {
        string? requestedPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_MULTIPAGE_EVIDENCE_PATH");
        if (string.IsNullOrWhiteSpace(requestedPath)) return;
        string path = Path.GetFullPath(requestedPath.Replace("{backend}", "tensorrt", StringComparison.OrdinalIgnoreCase));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        double rank = (sorted.Length - 1) * percentile;
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        return lower == upper ? sorted[lower] : sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }

    private static int ReadPositiveInt(string name, int fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out int parsed) && parsed > 0 && parsed <= 64 ? parsed : fallback;
    }

    private static TensorRtApiVersion ResolveApiVersion()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API") ?? Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API");
        if (int.TryParse(value, out int number) && Enum.IsDefined(typeof(TensorRtApiVersion), number)) return (TensorRtApiVersion)number;
        if (Enum.TryParse(value, true, out TensorRtApiVersion parsed) && Enum.IsDefined(typeof(TensorRtApiVersion), parsed)) return parsed;
        return TensorRtApiVersion.TensorRt11;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed class MultiPageBenchmarkEvidence
    {
        public int warmupCount { get; init; }
        public int measurementCount { get; init; }
        public MultiPageTimingSummary sequential { get; init; } = null!;
        public MultiPageTimingSummary concurrent { get; init; } = null!;
    }

    private sealed class MultiPageTimingSummary
    {
        public double[] samplesMs { get; init; } = Array.Empty<double>();
        public double minMs { get; init; }
        public double maxMs { get; init; }
        public double p50Ms { get; init; }
        public double p95Ms { get; init; }
    }
}
