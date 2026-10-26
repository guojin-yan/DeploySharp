using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrVlFunsdSpottingQualityIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task EvaluateSelectedFunsdTestPageAgainstIndependentWordAnnotations()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_RUN") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_RUN=1 for the labelled Spotting evaluation.");

        string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FUNSD_ROOT")
            ?? @"F:\OCRBenchmarkTesting");
        string manifestPath = Path.Combine(datasetRoot, "data", "annotations", "manifests", "funsd-test-full-001.jsonl");
        string bundleRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT");
        string modelRoot = Required("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        if (!File.Exists(manifestPath)) Assert.Inconclusive("The pinned FUNSD manifest is required: " + manifestPath);
        int pageIndex = 0;
        string? requestedPage = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_PAGE_INDEX");
        if (requestedPage is not null && (!int.TryParse(requestedPage, NumberStyles.None, CultureInfo.InvariantCulture, out pageIndex) || pageIndex < 0))
            throw new ArgumentOutOfRangeException(nameof(requestedPage), "The FUNSD page index must be a nonnegative integer.");
        string reportPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, $"paddleocr-vl-funsd-spotting-{pageIndex}.json"));
        if (File.Exists(reportPath)) throw new IOException("The FUNSD Spotting report already exists: " + reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

        string selectedLine = SelectManifestLine(File.ReadLines(manifestPath), pageIndex);
        using JsonDocument document = JsonDocument.Parse(selectedLine);
        JsonElement page = document.RootElement;
        Assert.AreEqual("FUNSD", page.GetProperty("source_dataset").GetString());
        Assert.AreEqual("test", page.GetProperty("source_split").GetString());
        string imageId = page.GetProperty("image_id").GetString()!;
        string imageRelativePath = page.GetProperty("image_relpath").GetString()!;
        string imagePath = Path.GetFullPath(Path.Combine(datasetRoot, imageRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!imagePath.StartsWith(datasetRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The FUNSD image escapes the dataset root: " + imageRelativePath);
        int width = page.GetProperty("width").GetInt32(), height = page.GetProperty("height").GetInt32();
        string imageSha256 = page.GetProperty("source_provenance").GetProperty("image_sha256").GetString()!;
        Assert.AreEqual(imageSha256, Hash(imagePath), "The FUNSD image changed.");
        List<WordLabel> labels = page.GetProperty("instances").EnumerateArray()
            .Where(instance => !instance.GetProperty("ignore").GetBoolean() && !string.IsNullOrWhiteSpace(instance.GetProperty("text").GetString()))
            .Select(instance => new WordLabel(instance.GetProperty("instance_id").GetString()!,
                instance.GetProperty("text").GetString()!, instance.GetProperty("reading_order").GetInt32(),
                ReadPolygon(instance.GetProperty("polygon")))).ToList();
        Assert.AreEqual(page.GetProperty("statistics").GetProperty("valid_region_count").GetInt32(), labels.Count);
        Assert.IsTrue(labels.All(label => label.Polygon.All(point => point.X >= 0 && point.X <= width && point.Y >= 0 && point.Y <= height)));

        var report = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "deploysharp-paddleocr-vl-funsd-spotting-quality-v1",
            ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["status"] = "running",
            ["dataset"] = new { name = "FUNSD", split = "test", selection = "zero-based nonempty row of funsd-test-full-001.jsonl, chosen before inference",
                pageIndex, imageId, imageRelativePath, imageSha256, width, height, labelledWords = labels.Count,
                sourceRevision = page.GetProperty("source_revision").GetString(), manifestSha256 = Hash(manifestPath),
                benchmarkEligibility = page.GetProperty("benchmark_eligibility").GetString() },
            ["model"] = new { id = "paddleocr-vl/1.6", checkpointRevision = PaddleOcrVlInputPlanning.OfficialV16Revision,
                tokenizerSha256 = Hash(Path.Combine(modelRoot, "tokenizer.model")),
                graphs = new[] { "vision", "embedding", "decoder", "head" }.ToDictionary(role => role,
                    role => Hash(Path.Combine(bundleRoot, role + ".onnx"))) },
            ["environment"] = new { backend = "onnxruntime-cpu", os = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                sourceCommit = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT") ?? "not-recorded",
                visualAssemblySha256 = Hash(typeof(PaddleOcrVlOnnxSession).Assembly.Location),
                openCvAssemblySha256 = Hash(typeof(OpenCvPaddleOcrVlInputFactory).Assembly.Location),
                testAssemblySha256 = Hash(GetType().Assembly.Location) },
            ["protocol"] = new { task = "TextSpotting", maximumNewTokens = 2048, threads = 4,
                imageExifOrientation = "raw encoded pixels; no EXIF transposition",
                association = "Each labelled word centroid goes to the smallest-area containing predicted quadrilateral; ties use output index. A predicted region may own multiple words. Overlapping candidates and unmatched words/regions are reported.",
                transcript = "Associated reference words use FUNSD reading_order. NFKC, Unicode whitespace collapse and invariant uppercase precede Unicode-scalar CER and whitespace-token WER. Unmatched truth counts as deletion; unmatched prediction counts as insertion.",
                boundary = "FUNSD has word-level boxes; model Spotting can emit line-level regions. Centroid coverage and associated CER/WER are a predeclared diagnostic, not official FUNSD detection F1 or general model accuracy. No image filtering based on prediction; one page is not a representative dataset estimate." }
        };
        void Save(string status)
        {
            report["status"] = status;
            report["generatedUtc"] = DateTimeOffset.UtcNow;
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions) + "\n");
        }
        Save("running");
        try
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: 4, interOpThreads: 1,
                enableMemoryPattern: false, enableCpuMemoryArena: false));
            using var session = PaddleOcrVlOnnxSession.Create(registry, bundleRoot,
                new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
            var tokenizer = new PaddleOcrVlTokenizer(modelRoot);
            var factory = new OpenCvPaddleOcrVlInputFactory(128 * 1024 * 1024, normalizeExifOrientation: false);
            PaddleOcrVlPreparedInput input = factory.CreateFromFile(imagePath, PaddleOcrVlTask.TextSpotting);
            Assert.AreEqual(width, input.ImagePlan.SourceSize.Width);
            Assert.AreEqual(height, input.ImagePlan.SourceSize.Height);
            Assert.AreEqual(imageSha256, input.SourceSha256);
            float[] patches = (float[])input.PixelPatches.Buffer;
            string patchSha256 = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(patches.AsSpan()))).ToLowerInvariant();
            PaddleOcrVlGenerationResult generated = await session.GenerateAsync(input, tokenizer,
                new PaddleOcrVlGenerationOptions(maximumNewTokens: 2048, timeout: TimeSpan.FromMinutes(15)));
            PaddleOcrVlSpottingParseResult parsed = PaddleOcrVlSpottingParser.Parse(generated.Text,
                generated.FinishReason, input.ImagePlan.SourceSize, input.SourceFrame);
            report["generation"] = new { generated.Text, generatedTokenIds = generated.TokenIds.ToArray(),
                tokenCount = generated.TokenIds.Count, patchFloatCount = patches.Length, patchSha256,
                finishReason = generated.FinishReason.ToString(), generationMilliseconds = generated.Elapsed.TotalMilliseconds,
                parseStatus = parsed.Status.ToString(), parsed.Diagnostics, parsedRegionCount = parsed.Regions.Count,
                sourceSha256 = input.SourceSha256 };
            if (!parsed.Succeeded)
            {
                Save("not-scorable");
                Assert.Fail("Spotting did not yield a complete EOS result: " + parsed.Status + " " + string.Join("; ", parsed.Diagnostics));
            }
            List<PredictedRegion> predictions = parsed.Regions.Select((region, index) => new PredictedRegion(index, region.Text,
                region.SourcePolygon.Select(point => new Point(point.X, point.Y)).ToArray())).ToList();
            report["quality"] = Evaluate(labels, predictions);
            Save("completed");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.ToString();
            Save("failed");
            throw;
        }
        finally
        {
            TestContext.AddResultFile(reportPath);
        }
    }

    [TestMethod]
    public void LineRegionMatchesSeveralWordsWithoutCountingThemAsMissed()
    {
        var labels = new[] { Word("a", "Hello", 0, 0, 0, 40, 20), Word("b", "world", 1, 42, 0, 85, 20) };
        var predictions = new[] { Region(0, "Hello world", 0, 0, 85, 20) };
        QualityResult quality = Evaluate(labels, predictions);
        Assert.AreEqual(2, quality.CoveredWords);
        Assert.AreEqual(0, quality.UncoveredWords);
        Assert.AreEqual(0, quality.CharacterEdits);
        Assert.AreEqual(0, quality.WordEdits);
    }

    [TestMethod]
    public void DuplicateAndMissedRegionsContributeInsertionsAndDeletions()
    {
        var labels = new[] { Word("a", "Hello", 0, 0, 0, 40, 20), Word("b", "world", 1, 80, 0, 120, 20) };
        var predictions = new[] { Region(0, "Hello", 0, 0, 40, 20), Region(1, "Hello", 0, 0, 40, 20) };
        QualityResult quality = Evaluate(labels, predictions);
        Assert.AreEqual(1, quality.AmbiguousWords);
        Assert.AreEqual(1, quality.UncoveredWords);
        Assert.AreEqual(1, quality.UnmatchedPredictions);
        Assert.AreEqual(10, quality.CharacterEdits);
        Assert.AreEqual(2, quality.WordEdits);
    }

    [TestMethod]
    public void ManifestSelectionIgnoresBlankRowsAndRejectsOutOfRangeIndices()
    {
        string[] lines = { "", "first", "   ", "second" };
        Assert.AreEqual("first", SelectManifestLine(lines, 0));
        Assert.AreEqual("second", SelectManifestLine(lines, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SelectManifestLine(lines, -1));
        Assert.ThrowsExactly<InvalidDataException>(() => SelectManifestLine(lines, 2));
    }

    private static string SelectManifestLine(IEnumerable<string> lines, int pageIndex)
    {
        if (pageIndex < 0) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        return lines.Where(line => !string.IsNullOrWhiteSpace(line)).Skip(pageIndex).FirstOrDefault()
            ?? throw new InvalidDataException("The FUNSD manifest has no nonempty row at index " + pageIndex + ".");
    }

    private static QualityResult Evaluate(IReadOnlyList<WordLabel> labels, IReadOnlyList<PredictedRegion> predictions)
    {
        var assignments = new List<WordLabel>[predictions.Count];
        for (int index = 0; index < assignments.Length; index++) assignments[index] = new List<WordLabel>();
        var missed = new List<WordLabel>();
        int ambiguous = 0;
        foreach (WordLabel label in labels)
        {
            Point center = new Point(label.Polygon.Average(point => point.X), label.Polygon.Average(point => point.Y));
            List<PredictedRegion> candidates = predictions.Where(prediction => Contains(prediction.Polygon, center)).ToList();
            if (candidates.Count == 0) { missed.Add(label); continue; }
            if (candidates.Count > 1) ambiguous++;
            PredictedRegion selected = candidates.OrderBy(prediction => Area(prediction.Polygon)).ThenBy(prediction => prediction.Index).First();
            assignments[selected.Index].Add(label);
        }
        var rows = new List<MatchedRegion>(predictions.Count);
        int referenceCharacters = 0, referenceWords = 0, edits = 0, wordEdits = 0;
        for (int index = 0; index < predictions.Count; index++)
        {
            PredictedRegion prediction = predictions[index];
            WordLabel[] matched = assignments[index].OrderBy(label => label.ReadingOrder).ToArray();
            string reference = Normalize(string.Join(" ", matched.Select(label => label.Text)));
            string hypothesis = Normalize(prediction.Text);
            OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(reference, hypothesis);
            referenceCharacters += metrics.ReferenceCharacters; referenceWords += metrics.ReferenceWords;
            edits += metrics.CharacterEditDistance; wordEdits += metrics.WordEditDistance;
            rows.Add(new MatchedRegion(prediction.Index, prediction.Text, prediction.Polygon,
                matched.Select(label => label.Id).ToArray(), reference, metrics.CharacterEditDistance, metrics.WordEditDistance));
        }
        foreach (WordLabel label in missed)
        {
            OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(Normalize(label.Text), "");
            referenceCharacters += metrics.ReferenceCharacters; referenceWords += metrics.ReferenceWords;
            edits += metrics.CharacterEditDistance; wordEdits += metrics.WordEditDistance;
        }
        int covered = labels.Count - missed.Count;
        return new QualityResult(labels.Count, predictions.Count, covered, missed.Count,
            rows.Count(row => row.GroundTruthWordIds.Length == 0), ambiguous, referenceCharacters, referenceWords,
            edits, wordEdits, referenceCharacters == 0 ? 0 : (double)edits / referenceCharacters,
            referenceWords == 0 ? 0 : (double)wordEdits / referenceWords,
            missed.Select(label => label.Id).ToArray(), rows);
    }

    private static bool Contains(IReadOnlyList<Point> polygon, Point point)
    {
        bool positive = false, negative = false;
        for (int index = 0; index < polygon.Count; index++)
        {
            Point a = polygon[index], b = polygon[(index + 1) % polygon.Count];
            double cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
            positive |= cross > 1e-8; negative |= cross < -1e-8;
            if (positive && negative) return false;
        }
        return true;
    }

    private static double Area(IReadOnlyList<Point> polygon)
    {
        double doubled = 0;
        for (int index = 0; index < polygon.Count; index++)
        {
            Point a = polygon[index], b = polygon[(index + 1) % polygon.Count];
            doubled += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(doubled) / 2;
    }

    private static string Normalize(string value)
        => string.Join(" ", value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static Point[] ReadPolygon(JsonElement polygon)
    {
        Point[] points = polygon.EnumerateArray().Select(point => new Point(point[0].GetDouble(), point[1].GetDouble())).ToArray();
        if (points.Length != 4 || Area(points) <= 0) throw new InvalidDataException("FUNSD word polygons must have four non-degenerate points.");
        return points;
    }

    private static WordLabel Word(string id, string text, int order, double left, double top, double right, double bottom)
        => new WordLabel(id, text, order, Box(left, top, right, bottom));

    private static PredictedRegion Region(int index, string text, double left, double top, double right, double bottom)
        => new PredictedRegion(index, text, Box(left, top, right, bottom));

    private static Point[] Box(double left, double top, double right, double bottom)
        => new[] { new Point(left, top), new Point(right, top), new Point(right, bottom), new Point(left, bottom) };

    private static string Required(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Set " + variable + " for the external PaddleOCR-VL quality test.");
        return Path.GetFullPath(value!);
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record WordLabel(string Id, string Text, int ReadingOrder, Point[] Polygon);
    private sealed record PredictedRegion(int Index, string Text, Point[] Polygon);
    private readonly record struct Point(double X, double Y);
    private sealed record MatchedRegion(int Index, string Text, Point[] Polygon, string[] GroundTruthWordIds,
        string ReferenceText, int CharacterEdits, int WordEdits);
    private sealed record QualityResult(int LabelledWords, int PredictedRegions, int CoveredWords, int UncoveredWords,
        int UnmatchedPredictions, int AmbiguousWords, int ReferenceCharacters, int ReferenceWords,
        int CharacterEdits, int WordEdits, double CharacterErrorRate, double WordErrorRate,
        string[] UncoveredWordIds, IReadOnlyList<MatchedRegion> Regions);
}
