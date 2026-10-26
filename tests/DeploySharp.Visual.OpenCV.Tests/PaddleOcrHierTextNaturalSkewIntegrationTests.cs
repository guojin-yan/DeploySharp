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
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrHierTextNaturalSkewIntegrationTests
{
    private const string Revision = "70b6620b2b112597d8219e11eee9773a1403827c";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";
    private static readonly JsonSerializerOptions ReportOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void NaturalSkewStratumExcludesHorizontalAndRightAngleLines()
    {
        Assert.IsFalse(InSkewStratum(0));
        Assert.IsFalse(InSkewStratum(4.99));
        Assert.IsTrue(InSkewStratum(-5));
        Assert.IsTrue(InSkewStratum(20));
        Assert.IsFalse(InSkewStratum(20.01));
        Assert.IsFalse(InSkewStratum(90));
        Assert.IsFalse(InSkewStratum(double.NaN));
    }

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [DataRow("opencv-dnn")]
    [TestCategory("ExternalModels")]
    public async Task NaturalSkewLinesComparePolygonWithAxisAlignedBounds(string backend)
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_RUN") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_RUN=1 for the natural-skew quality check.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_ROOT")
            ?? @"F:\OCRBenchmarkTesting");
        string outputRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_OUTPUT")
            ?? TestContext.TestResultsDirectory!);
        string outputPath = Path.Combine(outputRoot, "paddleocr-hiertext-natural-skew-" + backend + ".json");
        if (File.Exists(outputPath)) throw new IOException("The natural-skew evidence already exists: " + outputPath);

        Selection selection = Select(root);
        Assert.IsTrue(selection.Lines.Count > 0, "The pinned HierText manifests must contain natural-skew lines.");
        string modelRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_MODEL_ROOT")
            ?? @"E:\Model\paddleocr");
        string folder = Path.Combine(modelRoot, "PP-OCRv6", "small");
        string detectorPath = Path.Combine(folder, "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(folder, "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(folder, "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath))
            Assert.Inconclusive("PP-OCRv6 Small model and dictionary files are required.");

        ModelId detectorId = new("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        Assert.AreEqual(detectorRelease.Sha256, ShaFile(detectorPath));
        Assert.AreEqual(recognizerRelease.Sha256, ShaFile(recognizerPath));
        Assert.AreEqual(DictionarySha, ShaFile(dictionaryPath));
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "hiertext-skew-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(PaddleOcrModelCatalog.Find(detectorId), Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(PaddleOcrModelCatalog.Find(recognizerId), Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 1);

        using var detectorRegistry = new BackendRegistry().UseOnnxRuntime();
        using var recognizerRegistry = new BackendRegistry();
        BackendId backendId;
        if (backend == "openvino") { recognizerRegistry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; }
        else if (backend == "onnxruntime") { recognizerRegistry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; }
        else if (backend == "opencv-dnn")
        {
            int classes = ((GreedyCtcDecoder)recognizer.VisualProfile.Decoder).ExpectedClassCount;
            var contract = new OpenCvDnnModelContract(recognizerId,
                new[] { new TensorDescriptor(recognizer.VisualProfile.Input.Name, TensorElementType.Float32, new TensorShape(1, 3, 48, -1)) },
                new[] { new TensorDescriptor(recognizer.VisualProfile.Outputs[0].Name, TensorElementType.Float32, new TensorShape(1, -1, classes)) });
            recognizerRegistry.UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            backendId = OpenCvDnnBackendProvider.BackendId;
        }
        else throw new ArgumentOutOfRangeException(nameof(backend));
        var profiles = new VisualProfileRegistry();
        profiles.Register(detector.VisualProfile);
        profiles.Register(recognizer.VisualProfile);
        profiles.Freeze();
        var detectorRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var recognizerRequest = new BackendRequest(BackendCapabilities.TensorInference, backendId, backend == "openvino" ? "CPU" : "cpu");
        using var pipeline = new OcrPipeline(detectorRegistry,
            profiles.Select(detector.CreateArtifact(detectorPath, OnnxRuntimeBackendProvider.BackendId), detectorRegistry, detectorRequest, VisualTaskId.TextDetection), detectorRequest,
            recognizerRegistry,
            profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), recognizerRegistry, recognizerRequest, VisualTaskId.TextRecognition), recognizerRequest,
            recognizer.CropProfile!, new OcrPipelineOptions(maximumRegions: 16, maximumRecognitionBatch: 1,
                maximumSourcePixels: 128L * 1024L * 1024L), new SessionOptions(1), new SessionOptions(1));

        var observations = new List<Observation>(selection.Lines.Count);
        var inputFactory = new OpenCvOcrImageInputFactory();
        foreach (IGrouping<string, SkewLine> image in selection.Lines.GroupBy(line => line.ImageId))
        {
            SkewLine first = image.First();
            using OpenCvOcrImageInput input = inputFactory.CreateFromFile(first.ImagePath, detector.VisualProfile.Input.Name,
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr), first.ImageId);
            foreach (SkewLine[] lines in image.Chunk(16))
            {
                TextRegion[] polygonRegions = lines.Select((line, index) => new TextRegion(index, 1f,
                    line.Quadrilateral.Polygon, line.Quadrilateral)).ToArray();
                TextRegion[] boxRegions = lines.Select((line, index) => new TextRegion(index, 1f,
                    line.AxisAlignedBox.Polygon, line.AxisAlignedBox)).ToArray();
                var watch = Stopwatch.StartNew();
                IReadOnlyList<OcrRegionResult> polygon = await pipeline.RecognizeOnlyAsync(input, polygonRegions).ConfigureAwait(false);
                TimeSpan polygonElapsed = watch.Elapsed;
                watch.Restart();
                IReadOnlyList<OcrRegionResult> box = await pipeline.RecognizeOnlyAsync(input, boxRegions).ConfigureAwait(false);
                TimeSpan boxElapsed = watch.Elapsed;
                Assert.AreEqual(lines.Length, polygon.Count);
                Assert.AreEqual(lines.Length, box.Count);
                for (int index = 0; index < lines.Length; index++)
                {
                    Assert.AreEqual(index, polygon[index].Region.SourceIndex);
                    Assert.AreEqual(index, box[index].Region.SourceIndex);
                    OcrTextAccuracyMetrics polygonMetrics = OcrTextAccuracy.Compare(lines[index].Text, polygon[index].Recognition.Text);
                    OcrTextAccuracyMetrics boxMetrics = OcrTextAccuracy.Compare(lines[index].Text, box[index].Recognition.Text);
                    Assert.AreEqual(polygonMetrics.ReferenceCharacters, boxMetrics.ReferenceCharacters);
                    Assert.AreEqual(polygonMetrics.ReferenceWords, boxMetrics.ReferenceWords);
                    observations.Add(new Observation(lines[index].Sample, lines[index].ImageId, lines[index].ImageSha256,
                        lines[index].InstanceId, lines[index].AngleDegrees, lines[index].Text,
                        polygon[index].Recognition.Text, box[index].Recognition.Text,
                        polygonMetrics.ReferenceCharacters, polygonMetrics.ReferenceWords,
                        polygonMetrics.CharacterEditDistance, boxMetrics.CharacterEditDistance,
                        polygonMetrics.WordEditDistance, boxMetrics.WordEditDistance,
                        polygon[index].Recognition.Confidence, box[index].Recognition.Confidence,
                        polygonElapsed.TotalMilliseconds, boxElapsed.TotalMilliseconds));
                }
            }
        }
        Assert.AreEqual(selection.Lines.Count, observations.Count);

        var report = new
        {
            schemaVersion = "deploysharp-paddleocr-hiertext-natural-skew-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            backend = backend + "-cpu",
            host = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                sourceCommit = Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_SOURCE_REVISION") ?? "not-recorded",
                testAssemblySha256 = ShaFile(GetType().Assembly.Location) },
            dataset = new { name = "google-research-datasets/hiertext", split = "validation", revision = Revision,
                manifests = selection.ManifestHashes, selection.CandidateCount, selection.OutOfBoundsCount,
                selectedLines = selection.Lines.Count, images = selection.Lines.Select(line => line.ImageId).Distinct().Count(),
                selectionRule = "All nonignored, nonempty, text_direction=horizontal quadrilaterals with absolute annotated top-edge angle in [5,20] degrees and all four vertices inside the source image; no prediction-based filtering." },
            model = new { id = recognizerId.Value, detectorSha256 = detectorRelease.Sha256,
                recognizerSha256 = recognizerRelease.Sha256, dictionarySha256 = DictionarySha },
            protocol = new { stage = "RecognizeOnlyAsync; detection and automatic orientation are not executed",
                comparison = "Same decoded source and recognizer: annotated perspective quadrilateral versus its axis-aligned bounding rectangle; case-sensitive OCR text metrics.",
                batchMaximum = 1, sessionCount = 1, targetHeight = recognizer.CropProfile!.TargetHeight,
                maximumWidth = recognizer.CropProfile.MaximumWidth },
            totals = new { rows = observations.Count, referenceCharacters = observations.Sum(row => row.ReferenceCharacters),
                referenceWords = observations.Sum(row => row.ReferenceWords),
                polygonCharacterEdits = observations.Sum(row => row.PolygonCharacterEdits),
                boxCharacterEdits = observations.Sum(row => row.BoxCharacterEdits),
                polygonWordEdits = observations.Sum(row => row.PolygonWordEdits),
                boxWordEdits = observations.Sum(row => row.BoxWordEdits),
                polygonBetter = observations.Count(row => row.PolygonCharacterEdits < row.BoxCharacterEdits),
                boxBetter = observations.Count(row => row.BoxCharacterEdits < row.PolygonCharacterEdits),
                equal = observations.Count(row => row.BoxCharacterEdits == row.PolygonCharacterEdits) },
            observations,
            boundary = "Real annotated skew geometry and transcription, but angle is derived from vertices, not an independent upright/inverted label. Recognizer-only paired crops do not measure detection, automatic orientation, whole-page quality or formal backend speed."
        };
        Directory.CreateDirectory(outputRoot);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, ReportOptions) + "\n");
        TestContext.AddResultFile(outputPath);
    }

    private static Selection Select(string root)
    {
        var selected = new List<SkewLine>();
        var manifestHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var checkedImages = new HashSet<string>(StringComparer.Ordinal);
        int candidates = 0, outOfBounds = 0;
        foreach (string sample in new[] { "sample-002", "sample-003" })
        {
            string name = "hiertext-validation-" + sample + ".jsonl";
            string manifest = Path.Combine(root, "data", "annotations", "manifests", name);
            if (!File.Exists(manifest)) Assert.Inconclusive("The pinned HierText manifest is required: " + manifest);
            manifestHashes.Add(name, ShaFile(manifest));
            foreach (string line in File.ReadLines(manifest))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement page = document.RootElement;
                Assert.AreEqual("google-research-datasets/hiertext", page.GetProperty("source_dataset").GetString());
                Assert.AreEqual("validation", page.GetProperty("source_split").GetString());
                Assert.AreEqual(Revision, page.GetProperty("source_revision").GetString());
                int width = page.GetProperty("width").GetInt32(), height = page.GetProperty("height").GetInt32();
                string imageId = page.GetProperty("image_id").GetString()!;
                string relativePath = page.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar);
                string imagePath = Path.GetFullPath(Path.Combine(root, relativePath));
                if (!imagePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The HierText image escapes the dataset root: " + relativePath);
                string imageSha = page.GetProperty("source_provenance").GetProperty("image_sha256").GetString()!;
                foreach (JsonElement instance in page.GetProperty("instances").EnumerateArray())
                {
                    if (instance.GetProperty("ignore").GetBoolean()) continue;
                    string text = instance.GetProperty("text").GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text)
                        || instance.GetProperty("attributes").GetProperty("text_direction").GetString() != "horizontal") continue;
                    JsonElement polygon = instance.GetProperty("polygon");
                    if (polygon.GetArrayLength() != 4) continue;
                    PointF[] points = polygon.EnumerateArray().Select(vertex => new PointF(vertex[0].GetSingle(), vertex[1].GetSingle())).ToArray();
                    double angle = Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X) * 180 / Math.PI;
                    if (!InSkewStratum(angle)) continue;
                    candidates++;
                    if (points.Any(point => point.X < 0 || point.Y < 0 || point.X > width || point.Y > height))
                    { outOfBounds++; continue; }
                    string instanceId = instance.GetProperty("instance_id").GetString()!;
                    if (!seen.Add(instanceId)) throw new InvalidDataException("Duplicate HierText line: " + instanceId);
                    if (checkedImages.Add(imageId)) Assert.AreEqual(imageSha, ShaFile(imagePath), "The HierText image changed: " + imageId);
                    var quadrilateral = new TextQuadrilateral(points[0], points[1], points[2], points[3], TextCornerOrder.TopLeftClockwise);
                    float left = points.Min(point => point.X), top = points.Min(point => point.Y);
                    float right = points.Max(point => point.X), bottom = points.Max(point => point.Y);
                    var box = new TextQuadrilateral(new PointF(left, top), new PointF(right, top),
                        new PointF(right, bottom), new PointF(left, bottom), TextCornerOrder.TopLeftClockwise);
                    selected.Add(new SkewLine(sample, imageId, imagePath, imageSha, instanceId, text, angle, quadrilateral, box));
                }
            }
        }
        selected.Sort((a, b) => StringComparer.Ordinal.Compare(a.InstanceId, b.InstanceId));
        return new Selection(selected, manifestHashes, candidates, outOfBounds);
    }

    private static bool InSkewStratum(double angle)
        => double.IsFinite(angle) && Math.Abs(angle) >= 5 && Math.Abs(angle) <= 20;

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string ShaFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record Selection(List<SkewLine> Lines, IReadOnlyDictionary<string, string> ManifestHashes,
        int CandidateCount, int OutOfBoundsCount);
    private sealed record SkewLine(string Sample, string ImageId, string ImagePath, string ImageSha256,
        string InstanceId, string Text, double AngleDegrees, TextQuadrilateral Quadrilateral, TextQuadrilateral AxisAlignedBox);
    private sealed record Observation(string Sample, string ImageId, string ImageSha256, string InstanceId,
        double AngleDegrees, string ReferenceText, string PolygonText, string BoxText,
        int ReferenceCharacters, int ReferenceWords, int PolygonCharacterEdits, int BoxCharacterEdits,
        int PolygonWordEdits, int BoxWordEdits, float PolygonConfidence, float BoxConfidence,
        double PolygonBatchMilliseconds, double BoxBatchMilliseconds);
}
