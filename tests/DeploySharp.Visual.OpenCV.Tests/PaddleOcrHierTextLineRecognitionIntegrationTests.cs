using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Scores recognizer-only OCR on source-linked HierText line crops. / 在带来源映射的 HierText 文本行裁剪图上隔离评测识别器。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrHierTextLineRecognitionIntegrationTests
{
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting";
    private const string ModelRoot = @"E:\Model\paddleocr";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";
    private const string CropRevision = "70b6620b2b112597d8219e11eee9773a1403827c";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task HierTextLineCropsProduceTraceableRecognitionMetrics(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC=1 to run line-level HierText recognition evidence.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_ROOT") ?? DefaultDatasetRoot);
        int maximumRows = ReadMaximumRows();
        LineCropDataset dataset = LoadLineCrops(root, maximumRows);
        List<LineCrop> crops = dataset.Crops;
        if (crops.Count == 0) Assert.Inconclusive("No source-linked HierText text-line crops were found under " + root);

        string directory = Path.Combine(ModelRoot, "PP-OCRv6", "small");
        string detectorPath = Path.Combine(directory, "PP-OCRv6_small_det_inference.onnx");
        string recognizerPath = Path.Combine(directory, "PP-OCRv6_small_rec_inference.onnx");
        string dictionaryPath = Path.Combine(directory, "PP-OCRv6_small_rec_dict.txt");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath) || !File.Exists(dictionaryPath))
            Assert.Inconclusive("PP-OCRv6 Small assets are incomplete.");

        ModelId detectorId = new ModelId("paddleocr/ppocrv6/small-det");
        ModelId recognizerId = new ModelId("paddleocr/ppocrv6/small-rec");
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(detectorId);
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(recognizerId);
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "hiertext-line-rec-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease), maximumBatch: 1);
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, DictionarySha), characters, maximumBatch: 16);

        using var registry = new BackendRegistry();
        BackendId backendId;
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase)) { registry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; }
        else { registry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; }
        var profiles = new VisualProfileRegistry(); profiles.Register(detector.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, "CPU");
        using var pipeline = new OcrPipeline(registry,
            profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection), request,
            profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition), request,
            recognizer.CropProfile!, new OcrPipelineOptions(maximumRegions: 1, maximumRecognitionBatch: 16, maximumSourcePixels: 128L * 1024L * 1024L),
            new SessionOptions(1), new SessionOptions(1));

        var rows = new List<object>(crops.Count);
        var lengthBuckets = new Dictionary<string, MetricBucket>(StringComparer.Ordinal);
        int exact = 0, referenceCharacters = 0, characterEdits = 0, referenceWords = 0, wordEdits = 0;
        foreach (LineCrop crop in crops)
        {
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(crop.ImagePath, "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(crop.ImagePath,
                detector.VisualProfile.Input.Name,
                OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));
            TextQuadrilateral quad = new TextQuadrilateral(new PointF(0, 0), new PointF(probe.SourceSize.Width, 0),
                new PointF(probe.SourceSize.Width, probe.SourceSize.Height), new PointF(0, probe.SourceSize.Height), TextCornerOrder.TopLeftClockwise);
            IReadOnlyList<OcrRegionResult> result = await pipeline.RecognizeOnlyAsync(input,
                new[] { new TextRegion(0, 1f, quad.Polygon, quad) }).ConfigureAwait(false);
            Assert.AreEqual(1, result.Count);
            OcrTextAccuracyMetrics metrics = OcrTextAccuracy.Compare(crop.Text, result[0].Recognition.Text);
            if (metrics.CharacterEditDistance == 0 && metrics.HypothesisCharacters == metrics.ReferenceCharacters) exact++;
            referenceCharacters += metrics.ReferenceCharacters; characterEdits += metrics.CharacterEditDistance;
            referenceWords += metrics.ReferenceWords; wordEdits += metrics.WordEditDistance;
            string bucketName = LengthBucket(metrics.ReferenceCharacters);
            if (!lengthBuckets.TryGetValue(bucketName, out MetricBucket? bucket))
                lengthBuckets.Add(bucketName, bucket = new MetricBucket());
            bucket.Add(metrics);
            rows.Add(new
            {
                sample = crop.Sample, cropId = crop.CropId, parentImageId = crop.ParentImageId, parentInstanceId = crop.ParentInstanceId,
                language = crop.Language, handwritten = crop.Handwritten, vertical = crop.Vertical,
                sourceCropSha256 = crop.SourceCropSha256, imageSha256 = crop.ImageSha256, expectedText = crop.Text,
                recognizedText = result[0].Recognition.Text, confidence = result[0].Recognition.Confidence,
                characterEditDistance = metrics.CharacterEditDistance, characterErrorRate = metrics.CharacterErrorRate,
                referenceCharacterCount = metrics.ReferenceCharacters,
                wordEditDistance = metrics.WordEditDistance, wordErrorRate = metrics.WordErrorRate,
                expectedTextSha256 = Sha256(crop.Text), recognizedTextSha256 = Sha256(result[0].Recognition.Text)
            });
        }

        string outputDirectory = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_OUTPUT") ?? TestContext.TestResultsDirectory!;
        Directory.CreateDirectory(outputDirectory);
        string report = Path.Combine(Path.GetFullPath(outputDirectory), "paddleocr-hiertext-line-rec-" + backend + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 2, generatedUtc = DateTimeOffset.UtcNow, backend, model = "PP-OCRv6 Small REC",
            detectorSha256 = detectorRelease.Sha256, recognizerSha256 = recognizerRelease.Sha256, dictionarySha256 = DictionarySha,
            dataset = new
            {
                name = "google-research-datasets/hiertext", split = "validation",
                cropRevision = CropRevision,
                annotationRows = dataset.AnnotationRows,
                ignoredAnnotationRows = dataset.IgnoredAnnotationRows,
                emptyAnnotationRows = dataset.EmptyAnnotationRows,
                cropManifestRows = dataset.CropManifestRows,
                excludedOrUnmatchedCropRows = dataset.ExcludedOrUnmatchedCropRows,
                missingCropFiles = dataset.MissingCropFiles,
                eligibleSourceLinkedRows = dataset.EligibleSourceLinkedRows,
                selection = crops.GroupBy(crop => crop.Sample).OrderBy(group => group.Key).Select(group => new
                {
                    sample = group.Key,
                    selectedRows = group.Count(),
                    annotationManifestSha256 = Sha256File(Path.Combine(root, "data", "annotations", "manifests", "hiertext-validation-" + group.Key + ".jsonl")),
                    cropManifestSha256 = Sha256File(Path.Combine(root, "data", "crops", "hiertext", CropRevision, "validation", group.Key, "manifest.jsonl"))
                }).ToArray(),
                uniqueParentImages = crops.Select(crop => crop.ParentImageId).Distinct(StringComparer.Ordinal).Count(),
                minimumCharacters = crops.Min(crop => crop.Text.Length),
                medianCharacters = Median(crops.Select(crop => crop.Text.Length).OrderBy(length => length).ToArray()),
                maximumCharacters = crops.Max(crop => crop.Text.Length),
                selectionPolicy = "Evenly spaced across the ordered eligible crop manifests; no contiguous first-N prefix. Empty and source-ignored HierText annotations are excluded.",
                lineLengthBuckets = lengthBuckets.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new
                {
                    bucket = pair.Key,
                    rows = pair.Value.Rows,
                    exactRows = pair.Value.ExactRows,
                    referenceCharacters = pair.Value.ReferenceCharacters,
                    characterEdits = pair.Value.CharacterEdits,
                    characterErrorRate = pair.Value.ReferenceCharacters == 0 ? 0d : (double)pair.Value.CharacterEdits / pair.Value.ReferenceCharacters,
                    referenceWords = pair.Value.ReferenceWords,
                    wordEdits = pair.Value.WordEdits,
                    wordErrorRate = pair.Value.ReferenceWords == 0 ? 0d : (double)pair.Value.WordEdits / pair.Value.ReferenceWords,
                    emptyPredictions = pair.Value.EmptyPredictions
                }).ToArray()
            },
            rows = rows.Count, exactRows = exact, exactRate = (double)exact / rows.Count,
            referenceCharacters, characterEdits, characterErrorRate = referenceCharacters == 0 ? 0d : (double)characterEdits / referenceCharacters,
            referenceWords, wordEdits, wordErrorRate = referenceWords == 0 ? 0d : (double)wordEdits / referenceWords,
            crops = rows, boundary = "Recognizer-only score on source-linked HierText text-line crops; detection recall, page reading order and formal release gates are out of scope."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(crops.Count, rows.Count);
    }

    private static LineCropDataset LoadLineCrops(string root, int maximumRows)
    {
        var expected = new Dictionary<string, (string ImageId, string Text, string Language, bool Handwritten, bool Vertical)>(StringComparer.Ordinal);
        int annotationRows = 0, ignoredAnnotationRows = 0, emptyAnnotationRows = 0;
        foreach (string manifestName in new[] { "hiertext-validation-sample-002.jsonl", "hiertext-validation-sample-003.jsonl" })
        {
            string path = Path.Combine(root, "data", "annotations", "manifests", manifestName);
            if (!File.Exists(path)) continue;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement image = document.RootElement;
                foreach (JsonElement instance in image.GetProperty("instances").EnumerateArray())
                {
                    annotationRows++;
                    string text = instance.GetProperty("text").GetString() ?? string.Empty;
                    if (instance.TryGetProperty("ignore", out JsonElement ignore) && ignore.GetBoolean())
                    {
                        ignoredAnnotationRows++;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        emptyAnnotationRows++;
                        continue;
                    }
                    JsonElement attributes = instance.GetProperty("attributes");
                    expected[instance.GetProperty("instance_id").GetString()!] =
                    (
                        image.GetProperty("image_id").GetString()!,
                        text,
                        instance.TryGetProperty("language", out JsonElement language) ? language.GetString() ?? "und" : "und",
                        attributes.GetProperty("handwritten").GetProperty("value").GetBoolean(),
                        attributes.GetProperty("vertical").GetProperty("value").GetBoolean()
                    );
                }
            }
        }
        var output = new List<LineCrop>();
        int cropManifestRows = 0, excludedOrUnmatchedCropRows = 0, missingCropFiles = 0;
        foreach (string sample in new[] { "sample-002", "sample-003" })
        {
            string path = Path.Combine(root, "data", "crops", "hiertext", CropRevision, "validation", sample, "manifest.jsonl");
            if (!File.Exists(path)) continue;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement crop = document.RootElement;
                cropManifestRows++;
                string parent = crop.GetProperty("parent_instance_id").GetString()!;
                if (!expected.TryGetValue(parent, out var reference))
                {
                    excludedOrUnmatchedCropRows++;
                    continue;
                }
                string imagePath = Path.Combine(root, crop.GetProperty("crop_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(imagePath))
                {
                    missingCropFiles++;
                    continue;
                }
                string manifestCropSha = crop.GetProperty("crop_sha256").GetString()!;
                string actualCropSha = Sha256File(imagePath);
                if (!string.Equals(manifestCropSha, actualCropSha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("HierText crop SHA-256 mismatch for " + crop.GetProperty("crop_id").GetString());
                output.Add(new LineCrop(sample, crop.GetProperty("crop_id").GetString()!, reference.ImageId, parent, reference.Text, reference.Language, reference.Handwritten, reference.Vertical,
                    manifestCropSha, actualCropSha, imagePath));
            }
        }
        int eligibleSourceLinkedRows = output.Count;
        List<LineCrop> selected;
        if (output.Count <= maximumRows)
        {
            selected = output;
        }
        else
        {
            selected = new List<LineCrop>(maximumRows);
            for (int index = 0; index < maximumRows; index++)
            {
                int sourceIndex = (int)((long)index * output.Count / maximumRows);
                selected.Add(output[sourceIndex]);
            }
        }

        return new LineCropDataset(selected, annotationRows, ignoredAnnotationRows, emptyAnnotationRows,
            cropManifestRows, excludedOrUnmatchedCropRows, missingCropFiles, eligibleSourceLinkedRows);
    }

    private static string LengthBucket(int referenceCharacters) => referenceCharacters switch
    {
        <= 4 => "01-04",
        <= 10 => "05-10",
        <= 20 => "11-20",
        <= 40 => "21-40",
        _ => "41+"
    };

    private static int ReadMaximumRows()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_LINE_REC_MAX");
        return int.TryParse(value, out int parsed) && parsed > 0 && parsed <= 4096 ? parsed : 1024;
    }

    private static double Median(int[] sortedValues)
    {
        if (sortedValues.Length == 0) return 0;
        int middle = sortedValues.Length / 2;
        return sortedValues.Length % 2 == 0 ? (sortedValues[middle - 1] + sortedValues[middle]) / 2d : sortedValues[middle];
    }

    private sealed class MetricBucket
    {
        public int Rows { get; private set; }
        public int ExactRows { get; private set; }
        public int ReferenceCharacters { get; private set; }
        public int CharacterEdits { get; private set; }
        public int ReferenceWords { get; private set; }
        public int WordEdits { get; private set; }
        public int EmptyPredictions { get; private set; }

        public void Add(OcrTextAccuracyMetrics metrics)
        {
            Rows++;
            if (metrics.CharacterEditDistance == 0 && metrics.HypothesisCharacters == metrics.ReferenceCharacters) ExactRows++;
            ReferenceCharacters += metrics.ReferenceCharacters;
            CharacterEdits += metrics.CharacterEditDistance;
            ReferenceWords += metrics.ReferenceWords;
            WordEdits += metrics.WordEditDistance;
            if (metrics.HypothesisCharacters == 0) EmptyPredictions++;
        }
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private sealed record LineCropDataset(List<LineCrop> Crops, int AnnotationRows, int IgnoredAnnotationRows, int EmptyAnnotationRows,
        int CropManifestRows, int ExcludedOrUnmatchedCropRows, int MissingCropFiles, int EligibleSourceLinkedRows);
    private sealed record LineCrop(string Sample, string CropId, string ParentImageId, string ParentInstanceId, string Text, string Language,
        bool Handwritten, bool Vertical, string SourceCropSha256, string ImageSha256, string ImagePath);
}
