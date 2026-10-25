using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlRealFormulaQualityIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task EvaluateAnnotatedRealFormulaCorpus()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RUN") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RUN=1 for the labelled corpus evaluation.");

        string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT")
            ?? @"F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public");
        string manifestPath = Path.Combine(datasetRoot, "realFormula-manifest.jsonl");
        string csvPath = Path.Combine(datasetRoot, "test-corrected_normalized.csv");
        if (!File.Exists(manifestPath) || !File.Exists(csvPath))
            Assert.Inconclusive("The pinned realFormula manifest and annotation CSV are required: " + datasetRoot);

        List<RealFormulaRow> allRows = File.ReadLines(manifestPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<RealFormulaRow>(line, JsonOptions)!)
            .ToList();
        Assert.AreEqual(121, allRows.Count, "Expected the pinned realFormula v1 annotated selection.");
        Assert.AreEqual(allRows.Count, allRows.Select(row => row.Image).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        HashSet<string> selectedImages = (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_IMAGES") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] unknownImages = selectedImages.Where(image => allRows.All(row => !string.Equals(row.Image, image, StringComparison.OrdinalIgnoreCase))).ToArray();
        Assert.AreEqual(0, unknownImages.Length, "Unknown realFormula images: " + string.Join(", ", unknownImages));
        int limit = ReadLimit();
        List<RealFormulaRow> filtered = selectedImages.Count == 0 ? allRows
            : allRows.Where(row => selectedImages.Contains(row.Image)).ToList();
        List<RealFormulaRow> selected = limit == 0 ? filtered : filtered.Take(limit).ToList();

        string bundleRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string modelRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        string backend = (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_BACKEND") ?? "onnxruntime-cuda")
            .Trim().ToLowerInvariant();
        if (backend is not ("onnxruntime-cpu" or "onnxruntime-cuda" or "openvino-cpu"))
            throw new ArgumentException("Choose onnxruntime-cpu, onnxruntime-cuda or openvino-cpu for the quality backend.");
        bool cudaArenaShrink = backend == "onnxruntime-cuda"
            && Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_CUDA_ARENA_SHRINK") != "0";
        string reportPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "paddleocr-vl-realformula-" + backend + ".json"));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

        using var registry = new BackendRegistry();
        BackendId backendId;
        string device;
        if (backend == "openvino-cpu")
        {
            registry.UseOpenVino(new OpenVinoOptions(performanceHint: OpenVinoPerformanceHint.Latency,
                streams: 1, inferenceThreads: 4, requestCount: 1));
            backendId = OpenVinoBackendProvider.BackendId;
            device = "CPU";
        }
        else
        {
            bool cuda = backend == "onnxruntime-cuda";
            var runtimeOptions = new OnnxRuntimeOptions(executionProvider: cuda
                    ? OnnxRuntimeExecutionProvider.Cuda : OnnxRuntimeExecutionProvider.Cpu,
                intraOpThreads: 4, interOpThreads: 1, enableMemoryPattern: false, enableCpuMemoryArena: false);
            if (cudaArenaShrink)
                runtimeOptions = runtimeOptions.WithRunConfigEntries(new Dictionary<string, string>
                {
                    ["memory.enable_memory_arena_shrinkage"] = "gpu:0"
                });
            registry.UseOnnxRuntime(runtimeOptions);
            backendId = OnnxRuntimeBackendProvider.BackendId;
            device = cuda ? "cuda" : "cpu";
        }

        using PaddleOcrVlOnnxSession session = PaddleOcrVlOnnxSession.Create(registry, bundleRoot,
            new BackendRequest(BackendCapabilities.TensorInference, backendId, device));
        var tokenizer = new PaddleOcrVlTokenizer(modelRoot);
        var inputFactory = new OpenCvPaddleOcrVlInputFactory(128 * 1024 * 1024, normalizeExifOrientation: false);
        var options = new PaddleOcrVlGenerationOptions(maximumNewTokens: 512, timeout: TimeSpan.FromMinutes(15));
        string csvSha256 = Hash(csvPath), manifestSha256 = Hash(manifestPath);
        var graphHashes = new[] { "vision", "embedding", "decoder", "head" }
            .ToDictionary(role => role, role => Hash(Path.Combine(bundleRoot, role + ".onnx")));
        string visualAssemblySha256 = Hash(typeof(PaddleOcrVlOnnxSession).Assembly.Location);
        string openCvAssemblySha256 = Hash(typeof(OpenCvPaddleOcrVlInputFactory).Assembly.Location);
        string testAssemblySha256 = Hash(GetType().Assembly.Location);
        var results = new List<QualityRow>(selected.Count);
        string? failure = null;
        string? currentImage = null;
        void Save(string status)
        {
            long referenceCharacters = results.Sum(row => (long)row.ReferenceLength);
            long edits = results.Sum(row => (long)row.CharacterEdits);
            var report = new
            {
                schemaVersion = "deploysharp-paddleocr-vl-realformula-quality-v1",
                generatedUtc = DateTimeOffset.UtcNow,
                status,
                failure,
                currentImage,
                dataset = new
                {
                    name = "MathNet realFormula", version = "Zenodo 11296815 v1",
                    doi = "10.5281/zenodo.11296815", license = "CC-BY-4.0",
                    csvSha256, manifestSha256,
                    annotatedCount = allRows.Count, selectedCount = selected.Count,
                    selectedImages = selectedImages.OrderBy(value => value, StringComparer.Ordinal).ToArray(), limit
                },
                model = new
                {
                    id = "paddleocr-vl/1.6", checkpointRevision = PaddleOcrVlInputPlanning.OfficialV16Revision,
                    tokenizerSha256 = tokenizer.Sha256, graphs = graphHashes
                },
                environment = new
                {
                    backend, device, os = RuntimeInformation.OSDescription,
                    framework = RuntimeInformation.FrameworkDescription,
                    sourceCommit = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT") ?? "not-recorded",
                    visualAssemblySha256, openCvAssemblySha256, testAssemblySha256
                },
                protocol = new
                {
                    task = "FormulaRecognition", maximumNewTokens = options.MaximumNewTokens, cudaArenaShrink,
                    imageExifOrientation = "raw encoded pixels; no EXIF transposition",
                    normalization = "Remove Unicode whitespace in reference and prediction; compare remaining UTF-16 code units with Levenshtein distance. This is not mathematical or TeX semantic equivalence.",
                    timing = "One pass per image; wall time covers production preprocessing plus generation, excluding session creation. Preprocessing is measured before GenerateAsync; generationMilliseconds comes from the production session and excludes its queue. No warm-up or controlled clock."
                },
                summary = new
                {
                    completedCount = results.Count,
                    exactMatches = results.Count(row => row.ExactMatch),
                    exactMatchRate = results.Count == 0 ? 0 : (double)results.Count(row => row.ExactMatch) / results.Count,
                    corpusCharacterErrorRate = (double)edits / Math.Max(1, referenceCharacters),
                    meanCharacterErrorRate = results.Count == 0 ? 0 : results.Average(row => row.CharacterErrorRate),
                    endOfSequenceCount = results.Count(row => row.FinishReason == GenerationFinishReason.EndOfSequence.ToString()),
                    maxTokensCount = results.Count(row => row.FinishReason == GenerationFinishReason.MaxTokens.ToString()),
                    emptyPredictionCount = results.Count(row => row.PredictionLength == 0)
                },
                results,
                boundary = "Labelled realFormula v1 local evaluation for one fixed PaddleOCR-VL-1.6 export and backend. Training overlap is unknown. Whitespace-normalized CER does not measure formula equivalence; this is not a cross-backend result, controlled latency benchmark or full document-quality claim."
            };
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions) + "\n");
        }

        Save("running");
        try
        {
            foreach (RealFormulaRow sample in selected)
            {
                currentImage = sample.Image;
                string imagePath = Path.GetFullPath(Path.Combine(datasetRoot, sample.ImageRelPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!imagePath.StartsWith(datasetRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("An annotation image escapes the pinned dataset root: " + sample.ImageRelPath);
                Assert.AreEqual(sample.Image, Path.GetFileName(imagePath));
                Assert.AreEqual(sample.ImageSha256, Hash(imagePath), "Image SHA changed: " + sample.Image);
                Stopwatch timer = Stopwatch.StartNew();
                PaddleOcrVlPreparedInput prepared = inputFactory.CreateFromFile(imagePath, PaddleOcrVlTask.FormulaRecognition);
                double preprocessingMilliseconds = timer.Elapsed.TotalMilliseconds;
                Assert.AreEqual(sample.ImageSha256, prepared.SourceSha256);
                PaddleOcrVlGenerationResult generated = await session.GenerateAsync(prepared, tokenizer, options).ConfigureAwait(false);
                timer.Stop();
                string reference = WithoutWhitespace(sample.ReferenceLatex);
                string prediction = WithoutWhitespace(generated.Text);
                int edits = EditDistance(reference, prediction);
                results.Add(new QualityRow(sample.Image, sample.ImageSha256, sample.ReferenceLatex, generated.Text,
                    reference.Length, prediction.Length, edits, (double)edits / Math.Max(1, reference.Length),
                    string.Equals(reference, prediction, StringComparison.Ordinal), generated.FinishReason.ToString(),
                    generated.TokenIds.Count, preprocessingMilliseconds, generated.Elapsed.TotalMilliseconds,
                    timer.Elapsed.TotalMilliseconds));
                Save("running");
                TestContext.WriteLine("PaddleOCR-VL realFormula " + results.Count + "/" + selected.Count + " " + sample.Image
                    + " CER=" + ((double)edits / Math.Max(1, reference.Length)).ToString("F4"));
            }
            currentImage = null;
            Save("completed");
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            Save("failed");
            throw;
        }
        finally
        {
            TestContext.AddResultFile(reportPath);
        }
        Assert.AreEqual(selected.Count, results.Count);
    }

    [TestMethod]
    public void WhitespaceNormalizedFormulaMetricsCountEditsAndPreserveStructure()
    {
        Assert.AreEqual(@"\frac{a}{b}", WithoutWhitespace(" \\frac { a } { b }\n"));
        Assert.AreEqual(0, EditDistance("abc", "abc"));
        Assert.AreEqual(1, EditDistance("abc", "axc"));
        Assert.AreEqual(2, EditDistance("abc", "a"));
        Assert.AreEqual(1, EditDistance("", "x"));
    }

    private static string WithoutWhitespace(string value) => string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

    private static int EditDistance(string expected, string actual)
    {
        int[] previous = Enumerable.Range(0, actual.Length + 1).ToArray();
        int[] current = new int[actual.Length + 1];
        for (int row = 1; row <= expected.Length; row++)
        {
            current[0] = row;
            for (int column = 1; column <= actual.Length; column++)
                current[column] = Math.Min(Math.Min(previous[column] + 1, current[column - 1] + 1),
                    previous[column - 1] + (expected[row - 1] == actual[column - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[actual.Length];
    }

    private static int ReadLimit()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_LIMIT");
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (!int.TryParse(value, out int limit) || limit < 1 || limit > 121)
            throw new ArgumentException("DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_LIMIT must be between 1 and 121.");
        return limit;
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + name + " for the external PaddleOCR-VL quality test.");
        return Path.GetFullPath(value!);
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record RealFormulaRow(string Image, string ImageRelPath, string ImageSha256, string ReferenceLatex);
    private sealed record QualityRow(string Image, string ImageSha256, string ReferenceLatex, string Prediction,
        int ReferenceLength, int PredictionLength, int CharacterEdits, double CharacterErrorRate,
        bool ExactMatch, string FinishReason, int GeneratedTokens, double PreprocessingMilliseconds,
        double GenerationMilliseconds, double WallMilliseconds);
}
