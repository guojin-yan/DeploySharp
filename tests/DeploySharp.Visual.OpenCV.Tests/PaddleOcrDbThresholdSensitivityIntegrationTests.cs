using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
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

/// <summary>Measures PP-OCR DB decoder threshold sensitivity with one model inference per image and disjoint tune/holdout manifests. / 每图仅推理一次并使用互斥调参/验证清单评估 PP-OCR DB 解码阈值敏感性。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrDbThresholdSensitivityIntegrationTests
{
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting";
    private const string DefaultModelPath = @"E:\Model\paddleocr\PP-OCRv6\small\PP-OCRv6_small_det_inference.onnx";
    private const string TuningManifestName = "hiertext-validation-sample-002.jsonl";
    private const string HoldoutManifestName = "hiertext-validation-sample-003.jsonl";
    private const string ModelIdValue = "paddleocr/ppocrv6/small-det";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void PpOcrV6SmallDbThresholdSweepUsesDisjointTuneAndHoldoutSetsOnOrtCpu()
        => RunThresholdSweep("onnxruntime");

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void PpOcrV6SmallDbThresholdSweepUsesDisjointTuneAndHoldoutSetsOnOpenVinoCpu()
        => RunThresholdSweep("openvino");

    private void RunThresholdSweep(string backendName)
    {
        if (backendName != "onnxruntime" && backendName != "openvino") throw new ArgumentOutOfRangeException(nameof(backendName));
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DB_SWEEP"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_DB_SWEEP=1 to run the opt-in PP-OCRv6 Small DB threshold sensitivity evaluation.");

        string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DB_SWEEP_DATASET_ROOT") ?? DefaultDatasetRoot);
        string modelPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DB_SWEEP_MODEL") ?? DefaultModelPath);
        string tuningManifestPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DB_SWEEP_TUNING_MANIFEST") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", TuningManifestName));
        string holdoutManifestPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DB_SWEEP_HOLDOUT_MANIFEST") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", HoldoutManifestName));
        RequireFile(modelPath);
        RequireFile(tuningManifestPath);
        RequireFile(holdoutManifestPath);

        List<ImageCase> tuningImages = LoadManifest(tuningManifestPath, datasetRoot);
        List<ImageCase> holdoutImages = LoadManifest(holdoutManifestPath, datasetRoot);
        Assert.IsTrue(tuningImages.Count > 0 && holdoutImages.Count > 0, "Both fixed HierText selections must contain images.");
        HashSet<string> tuningIds = new HashSet<string>(tuningImages.Select(image => image.ImageId), StringComparer.Ordinal);
        Assert.AreEqual(0, holdoutImages.Count(image => tuningIds.Contains(image.ImageId)), "Tune and holdout manifests must be disjoint.");

        ModelId modelId = new ModelId(ModelIdValue);
        PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(modelId);
        PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(modelId);
        PaddleOcrArtifactContract artifact = new PaddleOcrArtifactContract(
            release.Opset,
            release.Sha256,
            "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified",
            "ppocr-official-inference-v1",
            "deploysharp-paddleocr-db-ctc-v1");
        PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, artifact, maximumBatch: 1);
        Assert.AreEqual(release.Sha256, Sha256File(modelPath), "The configured detector differs from the catalog-pinned artifact.");
        DecoderCase[] tuningCandidates = CreateTuningCandidates();

        string backend = backendName + "-cpu";
        using var backends = new BackendRegistry();
        BackendId backendId;
        string device;
        if (backendName == "openvino")
        {
            backends.UseOpenVino();
            backendId = OpenVinoBackendProvider.BackendId;
            device = "CPU";
        }
        else
        {
            backends.UseOnnxRuntime();
            backendId = OnnxRuntimeBackendProvider.BackendId;
            device = "cpu";
        }
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
        using IInferenceSession session = backends.CreateSession(
            profile.CreateArtifact(modelPath, backendId),
            request,
            new SessionOptions(1, false));

        List<ImageEvidence> tuningEvidence = RunSet(tuningImages, profile.VisualProfile, session, tuningCandidates);
        CandidateMetrics[] tuningMetrics = Aggregate(tuningEvidence, tuningCandidates);
        CandidateMetrics selected = tuningMetrics
            .OrderByDescending(item => item.Iou05.F1)
            .ThenBy(item => item.Candidate.IsBaseline ? 0 : 1)
            .ThenBy(item => item.Candidate.Name, StringComparer.Ordinal)
            .First();

        DecoderCase tuningBaseline = tuningCandidates.Single(item => item.IsBaseline);
        DecoderCase[] holdoutCandidates = selected.Candidate.IsBaseline
            ? new[] { new DecoderCase(tuningBaseline.Name, tuningBaseline.Options, isBaseline: true) }
            : new[]
            {
                new DecoderCase(tuningBaseline.Name, tuningBaseline.Options, isBaseline: true),
                new DecoderCase(selected.Candidate.Name, selected.Candidate.Options)
            };
        List<ImageEvidence> holdoutEvidence = RunSet(holdoutImages, profile.VisualProfile, session, holdoutCandidates);
        CandidateMetrics[] holdoutMetrics = Aggregate(holdoutEvidence, holdoutCandidates);

        string outputDirectory = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DB_SWEEP_OUTPUT") ?? TestContext.TestResultsDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(Path.GetFullPath(outputDirectory));
        string reportPath = Path.Combine(Path.GetFullPath(outputDirectory), "paddleocr-v6-small-db-threshold-sensitivity-" + backendName + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + ".json");
        var report = new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            sourceRevision = ResolveSourceRevision(),
            sourceWorkingTreeDirty = true,
            model = new { id = ModelIdValue, variant = "PP-OCRv6 Small DET", path = modelPath, sha256 = Sha256File(modelPath), catalogSha256 = release.Sha256 },
            backend,
            protocol = new
            {
                inferenceCount = tuningEvidence.Count + holdoutEvidence.Count,
                candidatePolicy = "One raw detector probability map per image is reused across every decoder candidate; only CPU DB postprocessing thresholds vary.",
                tuningSelection = "sample-002; maximize aggregate IoU@0.5 F1; ties prefer the unchanged default.",
                holdoutPolicy = "sample-003 is evaluated only after selecting on sample-002; no parameter is selected from holdout metrics.",
                thresholds = new[] { 0.5, 0.75 },
                limitations = new[]
                {
                    "This is a bounded smoke sensitivity study on one PP-OCRv6 Small DET artifact and two fixed HierText selections, not a release accuracy claim.",
                    "It evaluates detection geometry only; recognition, CER/WER, other model variants, GPU execution, and other backends are outside each run.",
                    "HierText image and annotation license terms remain source-specific; this report does not grant redistribution rights.",
                    "No library default or model profile is changed by this experiment."
                }
            },
            manifests = new
            {
                tuning = new { path = tuningManifestPath, sha256 = Sha256File(tuningManifestPath), images = tuningImages.Count, imageIds = tuningImages.Select(image => image.ImageId).ToArray() },
                holdout = new { path = holdoutManifestPath, sha256 = Sha256File(holdoutManifestPath), images = holdoutImages.Count, imageIds = holdoutImages.Select(image => image.ImageId).ToArray() },
                disjointImageIds = !holdoutImages.Any(image => tuningIds.Contains(image.ImageId))
            },
            tuning = new { selectedCandidate = selected.Candidate.ToRecord(), candidates = tuningMetrics.Select(ToRecord).ToArray(), images = tuningEvidence },
            holdout = new { candidates = holdoutMetrics.Select(ToRecord).ToArray(), images = holdoutEvidence }
        };

        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + Environment.NewLine, new UTF8Encoding(false));
        TestContext.AddResultFile(reportPath);
        Console.WriteLine("PADDLEOCR_DB_THRESHOLD_SWEEP model=" + ModelIdValue
            + ";backend=" + backend
            + ";tuningImages=" + tuningImages.Count.ToString(CultureInfo.InvariantCulture)
            + ";holdoutImages=" + holdoutImages.Count.ToString(CultureInfo.InvariantCulture)
            + ";selected=" + selected.Candidate.Name
            + ";tuningF1=" + selected.Iou05.F1.ToString("R", CultureInfo.InvariantCulture)
            + ";report=" + reportPath);
    }

    private static List<ImageEvidence> RunSet(IReadOnlyList<ImageCase> images, VisualModelProfile profile, IInferenceSession session, IReadOnlyList<DecoderCase> candidates)
    {
        var evidence = new List<ImageEvidence>(images.Count);
        var inputFactory = new OpenCvVisualInputFactory();
        foreach (ImageCase image in images)
        {
            string actualImageSha = Sha256File(image.ImagePath);
            if (!string.IsNullOrWhiteSpace(image.ExpectedImageSha256))
                Assert.AreEqual(image.ExpectedImageSha256, actualImageSha, "Dataset source bytes differ from the manifest for " + image.ImageId + ".");

            VisualSize sourceSize;
            using (PreparedVisualInput probe = inputFactory.CreateFromFile(
                image.ImagePath,
                "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
                sourceSize = probe.SourceSize;

            using PreparedVisualInput input = inputFactory.CreateFromFile(
                image.ImagePath,
                profile.Input.Name,
                OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.Input.Name, input.Tensor), CancellationToken.None);
            ITensor output = outputs.GetRequired(profile.Outputs[0].Name);
            string inputSha = Sha256Tensor(input.Tensor);
            string rawOutputSha = Sha256Tensor(output);
            var candidateEvidence = new List<CandidateImageEvidence>(candidates.Count);
            foreach (DecoderCase candidate in candidates)
            {
                var decoder = new PaddleDbTextDetectionDecoder(profile.Outputs[0].Name, candidate.Options);
                var context = new VisualDecodeContext(input, profile, outputs, CancellationToken.None);
                var decoded = (TextDetectionResult)decoder.Decode(context);
                candidate.Metrics.Add(image.GroundTruth, decoded.Regions);
                candidateEvidence.Add(new CandidateImageEvidence(
                    candidate.Name,
                    decoded.Regions.Count,
                    Sha256Decoded(decoded.Regions),
                    Sha256Geometry(decoded.Regions)));
            }

            evidence.Add(new ImageEvidence(
                image.ImageId,
                Path.GetFileName(image.ImagePath),
                actualImageSha,
                inputSha,
                output.Shape.ToArray(),
                rawOutputSha,
                candidateEvidence));
        }
        return evidence;
    }

    private static CandidateMetrics[] Aggregate(IReadOnlyList<ImageEvidence> images, IReadOnlyList<DecoderCase> candidates)
    {
        // RunSet accumulators are intentionally candidate-owned; this method snapshots
        // their counts only after a complete, one-inference-per-image split has finished.
        return candidates.Select(candidate => candidate.Metrics.Snapshot(candidate, images.Count)).ToArray();
    }

    private static DecoderCase[] CreateTuningCandidates()
    {
        return new[]
        {
            new DecoderCase("default", new PaddleDbPostprocessOptions(), true),
            new DecoderCase("probability-0.25", new PaddleDbPostprocessOptions(probabilityThreshold: 0.25f)),
            new DecoderCase("probability-0.35", new PaddleDbPostprocessOptions(probabilityThreshold: 0.35f)),
            new DecoderCase("box-0.55", new PaddleDbPostprocessOptions(boxThreshold: 0.55f)),
            new DecoderCase("box-0.65", new PaddleDbPostprocessOptions(boxThreshold: 0.65f)),
            new DecoderCase("unclip-1.3", new PaddleDbPostprocessOptions(unclipRatio: 1.3f)),
            new DecoderCase("unclip-1.7", new PaddleDbPostprocessOptions(unclipRatio: 1.7f))
        };
    }

    private static object ToRecord(CandidateMetrics metrics) => new
    {
        candidate = metrics.Candidate.ToRecord(),
        images = metrics.ImageCount,
        iou05 = metrics.Iou05.ToRecord(),
        iou075 = metrics.Iou075.ToRecord()
    };

    private static List<ImageCase> LoadManifest(string manifestPath, string datasetRoot)
    {
        var rows = new List<ImageCase>();
        foreach (string line in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            string imageId = root.GetProperty("image_id").GetString() ?? throw new InvalidDataException("Manifest image_id cannot be empty.");
            string relativePath = root.GetProperty("image_relpath").GetString() ?? throw new InvalidDataException("Manifest image_relpath cannot be empty.");
            string imagePath = Path.GetFullPath(Path.Combine(datasetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            RequireFile(imagePath);
            string? expectedSha = null;
            if (root.TryGetProperty("source_provenance", out JsonElement provenance)
                && provenance.TryGetProperty("image_sha256", out JsonElement shaElement)
                && shaElement.ValueKind == JsonValueKind.String)
                expectedSha = shaElement.GetString();

            var groundTruth = new List<TextPolygon>();
            foreach (JsonElement instance in root.GetProperty("instances").EnumerateArray())
            {
                bool ignored = instance.TryGetProperty("ignore", out JsonElement ignoreElement) && ignoreElement.GetBoolean();
                string text = instance.TryGetProperty("text", out JsonElement textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
                if (ignored || string.IsNullOrWhiteSpace(text)) continue;

                JsonElement polygonElement = instance.GetProperty("polygon");
                var points = new List<PointF>();
                foreach (JsonElement point in polygonElement.EnumerateArray())
                {
                    JsonElement.ArrayEnumerator coordinates = point.EnumerateArray();
                    Assert.IsTrue(coordinates.MoveNext(), "Ground-truth polygon point is missing X.");
                    float x = checked((float)coordinates.Current.GetDouble());
                    Assert.IsTrue(coordinates.MoveNext(), "Ground-truth polygon point is missing Y.");
                    float y = checked((float)coordinates.Current.GetDouble());
                    points.Add(new PointF(x, y));
                }
                Assert.AreEqual(4, points.Count, "The fixed HierText sensitivity selection is expected to use quadrilateral annotations.");
                double signedArea = SignedArea(points);
                Assert.IsTrue(Math.Abs(signedArea) > 0.000001, "Ground-truth polygon area must be positive.");
                groundTruth.Add(TextPolygon.Canonicalize(points, signedArea > 0 ? OrientedVertexOrder.CounterClockwise : OrientedVertexOrder.Clockwise));
            }
            rows.Add(new ImageCase(imageId, imagePath, expectedSha, groundTruth));
        }
        return rows;
    }

    private static MetricsSnapshot Measure(IReadOnlyList<TextPolygon> groundTruth, IReadOnlyList<TextRegion> predictions, float threshold)
    {
        var matches = new List<(float IoU, int Prediction, int GroundTruth)>();
        for (int groundTruthIndex = 0; groundTruthIndex < groundTruth.Count; groundTruthIndex++)
        {
            for (int predictionIndex = 0; predictionIndex < predictions.Count; predictionIndex++)
            {
                float iou = TextPolygon.IntersectionOverUnion(groundTruth[groundTruthIndex], predictions[predictionIndex].Polygon);
                if (iou >= threshold) matches.Add((iou, predictionIndex, groundTruthIndex));
            }
        }
        matches.Sort((left, right) =>
        {
            int comparison = right.IoU.CompareTo(left.IoU);
            if (comparison != 0) return comparison;
            comparison = right.Prediction.CompareTo(left.Prediction);
            return comparison != 0 ? comparison : right.GroundTruth.CompareTo(left.GroundTruth);
        });

        var usedGroundTruth = new HashSet<int>();
        var usedPredictions = new HashSet<int>();
        foreach ((float _, int prediction, int groundTruthIndex) in matches)
        {
            if (usedPredictions.Add(prediction))
            {
                if (!usedGroundTruth.Add(groundTruthIndex)) usedPredictions.Remove(prediction);
            }
        }
        return new MetricsSnapshot(usedGroundTruth.Count, predictions.Count - usedPredictions.Count, groundTruth.Count - usedGroundTruth.Count);
    }

    private static double SignedArea(IReadOnlyList<PointF> points)
    {
        double sum = 0;
        for (int index = 0; index < points.Count; index++)
        {
            PointF current = points[index];
            PointF next = points[(index + 1) % points.Count];
            sum += current.X * next.Y - next.X * current.Y;
        }
        return sum / 2d;
    }

    private static string Sha256Tensor(ITensor tensor)
    {
        if (tensor.ElementType != TensorElementType.Float32 || tensor.Buffer is not float[] values)
            throw new InvalidDataException("PP-OCR DB sensitivity evidence requires Float32 image and output tensors.");
        byte[] bytes = new byte[checked(values.Length * sizeof(float))];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static string Sha256Decoded(IReadOnlyList<TextRegion> regions)
    {
        var text = new StringBuilder();
        foreach (TextRegion region in regions)
        {
            text.Append(region.SourceIndex.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(region.Score.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            foreach (PointF point in region.Polygon.Vertices)
                text.Append(point.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(point.Y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            text.Append('\n');
        }
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static string Sha256Geometry(IReadOnlyList<TextRegion> regions)
    {
        var text = new StringBuilder();
        foreach (TextRegion region in regions)
        {
            foreach (PointF point in region.Polygon.Vertices)
                text.Append(point.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(point.Y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            text.Append('\n');
        }
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static string Sha256File(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string ResolveSourceRevision()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppContext.BaseDirectory
                }
            };
            process.Start();
            string value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? value : "unknown";
        }
        catch { return "unknown"; }
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR sensitivity input does not exist: " + path);
    }

    private sealed class ImageCase
    {
        public ImageCase(string imageId, string imagePath, string? expectedImageSha256, IReadOnlyList<TextPolygon> groundTruth)
        {
            ImageId = imageId;
            ImagePath = imagePath;
            ExpectedImageSha256 = expectedImageSha256;
            GroundTruth = groundTruth;
        }
        public string ImageId { get; }
        public string ImagePath { get; }
        public string? ExpectedImageSha256 { get; }
        public IReadOnlyList<TextPolygon> GroundTruth { get; }
    }

    private sealed class DecoderCase
    {
        public DecoderCase(string name, PaddleDbPostprocessOptions options, bool isBaseline = false)
        {
            Name = name;
            Options = options;
            IsBaseline = isBaseline;
            Metrics = new MetricsAccumulator();
        }
        public string Name { get; }
        public PaddleDbPostprocessOptions Options { get; }
        public bool IsBaseline { get; }
        public MetricsAccumulator Metrics { get; }
        public object ToRecord() => new
        {
            name = Name,
            probabilityThreshold = Options.ProbabilityThreshold,
            boxThreshold = Options.BoxThreshold,
            unclipRatio = Options.UnclipRatio,
            scoreMode = Options.ScoreMode.ToString(),
            boxType = Options.BoxType.ToString(),
            isBaseline = IsBaseline
        };
    }

    private sealed class CandidateImageEvidence
    {
        public CandidateImageEvidence(string candidate, int regionCount, string decodedOutputSha256, string geometrySha256)
        {
            Candidate = candidate;
            RegionCount = regionCount;
            DecodedOutputSha256 = decodedOutputSha256;
            GeometrySha256 = geometrySha256;
        }
        public string Candidate { get; }
        public int RegionCount { get; }
        public string DecodedOutputSha256 { get; }
        public string GeometrySha256 { get; }
    }

    private sealed class ImageEvidence
    {
        public ImageEvidence(string imageId, string imageFileName, string imageSha256, string inputTensorSha256, long[] outputShape, string rawOutputSha256, IReadOnlyList<CandidateImageEvidence> candidates)
        {
            ImageId = imageId;
            ImageFileName = imageFileName;
            ImageSha256 = imageSha256;
            InputTensorSha256 = inputTensorSha256;
            OutputShape = outputShape;
            RawOutputSha256 = rawOutputSha256;
            Candidates = candidates;
        }
        public string ImageId { get; }
        public string ImageFileName { get; }
        public string ImageSha256 { get; }
        public string InputTensorSha256 { get; }
        public long[] OutputShape { get; }
        public string RawOutputSha256 { get; }
        public IReadOnlyList<CandidateImageEvidence> Candidates { get; }
    }

    private sealed class MetricsAccumulator
    {
        private readonly Counter _iou05 = new Counter();
        private readonly Counter _iou075 = new Counter();
        public void Add(IReadOnlyList<TextPolygon> groundTruth, IReadOnlyList<TextRegion> predictions)
        {
            _iou05.Add(Measure(groundTruth, predictions, 0.5f));
            _iou075.Add(Measure(groundTruth, predictions, 0.75f));
        }
        public CandidateMetrics Snapshot(DecoderCase candidate, int imageCount)
            => new CandidateMetrics(candidate, imageCount, _iou05.Snapshot(), _iou075.Snapshot());
    }

    private sealed class Counter
    {
        private int _truePositive;
        private int _falsePositive;
        private int _falseNegative;
        public void Add(MetricsSnapshot snapshot)
        {
            _truePositive += snapshot.TruePositive;
            _falsePositive += snapshot.FalsePositive;
            _falseNegative += snapshot.FalseNegative;
        }
        public MetricsSnapshot Snapshot() => new MetricsSnapshot(_truePositive, _falsePositive, _falseNegative);
    }

    private sealed class CandidateMetrics
    {
        public CandidateMetrics(DecoderCase candidate, int imageCount, MetricsSnapshot iou05, MetricsSnapshot iou075)
        {
            Candidate = candidate;
            ImageCount = imageCount;
            Iou05 = iou05;
            Iou075 = iou075;
        }
        public DecoderCase Candidate { get; }
        public int ImageCount { get; }
        public MetricsSnapshot Iou05 { get; }
        public MetricsSnapshot Iou075 { get; }
    }

    private sealed class MetricsSnapshot
    {
        public MetricsSnapshot(int truePositive, int falsePositive, int falseNegative)
        {
            TruePositive = truePositive;
            FalsePositive = falsePositive;
            FalseNegative = falseNegative;
            double precision = truePositive + falsePositive == 0 ? 0d : (double)truePositive / (truePositive + falsePositive);
            double recall = truePositive + falseNegative == 0 ? 0d : (double)truePositive / (truePositive + falseNegative);
            Precision = precision;
            Recall = recall;
            F1 = precision + recall == 0d ? 0d : 2d * precision * recall / (precision + recall);
        }
        public int TruePositive { get; }
        public int FalsePositive { get; }
        public int FalseNegative { get; }
        public double Precision { get; }
        public double Recall { get; }
        public double F1 { get; }
        public object ToRecord() => new { tp = TruePositive, fp = FalsePositive, fn = FalseNegative, precision = Precision, recall = Recall, f1 = F1 };
    }
}
