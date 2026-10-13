using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgCodecs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoreOperations = JYPPX.OpenCvSharp.Core.Cv2;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Runs the real PP-LCNet 0/180 classifier on linked HierText horizontal crops and their
/// deterministic 180-degree counterparts. This is a classifier/dynamic-batch contract, not a
/// natural orientation accuracy claim because HierText does not provide upright-vs-inverted labels.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrHierTextOrientationClassifierIntegrationTests
{
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting";
    private const string ModelRoot = @"E:\Model\paddleocr\PP-OCRv5";
    private const string ClassifierSha = "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("onnxruntime")]
    [DataRow("openvino")]
    [TestCategory("ExternalModels")]
    public async Task RealTextLineClassifierRunsKnownZeroAndOneEightyBatch(string backend)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_ORIENTATION"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_HIERTEXT_ORIENTATION=1 to run the local HierText orientation evidence.");

        string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_ROOT") ?? DefaultDatasetRoot);
        int maximumRows = ReadMaximumRows();
        List<HorizontalCrop> crops = LoadHorizontalCrops(datasetRoot, maximumRows);
        if (crops.Count == 0) Assert.Inconclusive("No eligible horizontal HierText crops were found under " + datasetRoot);

        string modelPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ORIENTATION_MODEL")
            ?? Path.Combine(ModelRoot, "PP-OCRv5_mobile_cls.onnx");
        if (!File.Exists(modelPath)) Assert.Inconclusive("The PP-LCNet orientation model does not exist: " + modelPath);

        PaddleOcrArtifactContract artifact = new PaddleOcrArtifactContract(
            7, ClassifierSha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified",
            "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1");
        PaddleOcrProfile profile = PaddleOcrProfiles.CreateTextLineOrientationClassification(
            new ModelId("external/hiertext-ppocrv5-mobile-cls"), artifact, maximumBatch: 2, allowDynamicBatch: true);

        BackendId backendId;
        using var registry = new BackendRegistry();
        if (string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase))
        {
            registry.UseOpenVino();
            backendId = OpenVinoBackendProvider.BackendId;
        }
        else
        {
            registry.UseOnnxRuntime();
            backendId = OnnxRuntimeBackendProvider.BackendId;
        }

        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId,
            string.Equals(backend, "openvino", StringComparison.OrdinalIgnoreCase) ? "CPU" : "cpu");
        ModelArtifact model = profile.CreateArtifact(modelPath, backendId);
        using var orientation = new OcrOrientationPipeline(
            registry, profiles.Select(model, registry, request, VisualTaskId.TextOrientationClassification),
            request, new SessionOptions(1));

        var rows = new List<Observation>(crops.Count * 2);
        var inputFactory = new OpenCvVisualInputFactory();
        OpenCvPreprocessOptions classifierOptions = OpenCvStage19Preprocessing.CreatePaddleOcrTextLineOrientationOptions();
        foreach (HorizontalCrop crop in crops)
        {
            byte[] originalBytes;
            byte[] rotatedBytes;
            using (Mat source = ImageCodecs.ImRead(crop.ImagePath, ImreadModes.Unchanged))
            {
                Assert.IsFalse(source.Empty, "Could not decode HierText crop " + crop.CropId);
                originalBytes = ImageCodecs.ImEncode(".png", source);
                using Mat rotated = CoreOperations.Rotate(source, RotateFlags.Rotate180);
                rotatedBytes = ImageCodecs.ImEncode(".png", rotated);
            }
            using PreparedVisualInput upright = inputFactory.Create(OpenCvImageSource.FromBytes(originalBytes), profile.VisualProfile.Input.Name, classifierOptions, crop.CropId + ":0");
            using PreparedVisualInput upsideDown = inputFactory.Create(OpenCvImageSource.FromBytes(rotatedBytes), profile.VisualProfile.Input.Name, classifierOptions, crop.CropId + ":180");
            Assert.AreEqual(upright.Tensor.Shape, upsideDown.Tensor.Shape);
            float[] uprightValues = upright.Tensor.Buffer as float[] ?? throw new AssertFailedException("The orientation tensor must be Float32.");
            float[] upsideDownValues = upsideDown.Tensor.Buffer as float[] ?? throw new AssertFailedException("The rotated orientation tensor must be Float32.");
            var batchValues = new float[checked(uprightValues.Length + upsideDownValues.Length)];
            Array.Copy(uprightValues, 0, batchValues, 0, uprightValues.Length);
            Array.Copy(upsideDownValues, 0, batchValues, uprightValues.Length, upsideDownValues.Length);
            using var prepared = new PreparedVisualInput(
                profile.VisualProfile.Input.Name,
                new Tensor<float>(new TensorShape(2, 3, 80, 160), batchValues, TensorBufferOwnership.Transfer),
                upright.SourceSize,
                upright.ModelSize,
                2,
                VisualTensorLayout.Nchw,
                upright.Transform,
                upright.Preprocessing,
                crop.CropId,
                batchFrames: new[] { upright.BatchFrames[0], upsideDown.BatchFrames[0] });
            string inputSha = TensorSha(prepared.Tensor);
            IReadOnlyList<OcrOrientationResult> results = await orientation.RunBatchAsync(prepared).ConfigureAwait(false);
            Assert.AreEqual(2, results.Count, "The orientation model must return both dynamic-batch rows.");
            for (int index = 0; index < results.Count; index++)
            {
                TextOrientation expected = index == 0 ? TextOrientation.Degrees0 : TextOrientation.Degrees180;
                OcrOrientationResult result = results[index];
                rows.Add(new Observation(crop.CropId, expected.ToString(), result.Orientation.ToString(), result.ClassIndex,
                    result.Confidence, result.Rejected, inputSha, result.Scores.ToArray()));
            }
        }

        int correct = rows.Count(row => string.Equals(row.Expected, row.Predicted, StringComparison.Ordinal));
        int zeroExpected = crops.Count;
        int oneEightyExpected = crops.Count;
        int zeroPredicted = rows.Count(row => row.Predicted == TextOrientation.Degrees0.ToString());
        int oneEightyPredicted = rows.Count(row => row.Predicted == TextOrientation.Degrees180.ToString());
        string outputDirectory = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_ORIENTATION_OUTPUT") ?? TestContext.TestResultsDirectory!);
        Directory.CreateDirectory(outputDirectory);
        string jsonPath = Path.Combine(outputDirectory, "paddleocr-hiertext-orientation-classifier-" + backend + ".json");
        string markdownPath = Path.Combine(outputDirectory, "paddleocr-hiertext-orientation-classifier-" + backend + ".md");
        WriteEvidence(jsonPath, backend, datasetRoot, modelPath, profile, crops, rows, correct, zeroExpected, oneEightyExpected, zeroPredicted, oneEightyPredicted);
        WriteMarkdown(markdownPath, backend, modelPath, crops, rows, correct, zeroExpected, oneEightyExpected, zeroPredicted, oneEightyPredicted);
        TestContext.AddResultFile(jsonPath);
        TestContext.AddResultFile(markdownPath);

        // The source crops are naturally photographed text, not a curated upright/inverted
        // classification set. Keep the observed label accuracy as diagnostic evidence instead
        // of turning it into a false quality gate. The executable contract is that both dynamic
        // rows complete with finite two-class scores and stable input order.
        Assert.AreEqual(crops.Count * 2, rows.Count, "The orientation model must complete both rows for every crop.");
        Assert.IsTrue(rows.All(row => row.Scores.Length == 2 && row.Scores.All(score => !float.IsNaN(score) && !float.IsInfinity(score) && score >= 0f && score <= 1f)),
            "The orientation decoder must return two finite normalized scores per row.");
    }

    private static List<HorizontalCrop> LoadHorizontalCrops(string root, int maximumRows)
    {
        var eligible = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string manifestName in new[] { "hiertext-validation-sample-002.jsonl", "hiertext-validation-sample-003.jsonl" })
        {
            string path = Path.Combine(root, "data", "annotations", "manifests", manifestName);
            if (!File.Exists(path)) continue;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                foreach (JsonElement instance in document.RootElement.GetProperty("instances").EnumerateArray())
                {
                    string id = instance.GetProperty("instance_id").GetString()!;
                    string text = instance.GetProperty("text").GetString() ?? string.Empty;
                    bool ignored = instance.TryGetProperty("ignore", out JsonElement ignore) && ignore.GetBoolean();
                    string direction = instance.GetProperty("attributes").GetProperty("text_direction").GetString() ?? string.Empty;
                    if (!ignored && !string.IsNullOrWhiteSpace(text) && direction == "horizontal") eligible[id] = text;
                }
            }
        }

        var result = new List<HorizontalCrop>();
        foreach (string sample in new[] { "sample-002", "sample-003" })
        {
            string path = Path.Combine(root, "data", "crops", "hiertext", "70b6620b2b112597d8219e11eee9773a1403827c", "validation", sample, "manifest.jsonl");
            if (!File.Exists(path)) continue;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement crop = document.RootElement;
                string parentId = crop.GetProperty("parent_instance_id").GetString()!;
                if (!eligible.ContainsKey(parentId)) continue;
                string relative = crop.GetProperty("crop_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar);
                string imagePath = Path.Combine(root, relative);
                if (!File.Exists(imagePath)) continue;
                result.Add(new HorizontalCrop(crop.GetProperty("crop_id").GetString()!, parentId, eligible[parentId], imagePath));
                if (result.Count >= maximumRows) return result;
            }
        }
        return result;
    }

    private static int ReadMaximumRows()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_HIERTEXT_ORIENTATION_MAX");
        return int.TryParse(value, out int parsed) && parsed > 0 && parsed <= 128 ? parsed : 24;
    }

    private static void WriteEvidence(string path, string backend, string datasetRoot, string modelPath, PaddleOcrProfile profile,
        IReadOnlyList<HorizontalCrop> crops, IReadOnlyList<Observation> rows, int correct, int zeroExpected, int oneEightyExpected, int zeroPredicted, int oneEightyPredicted)
    {
        var evidence = new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            backend = backend + "-cpu",
            model = new { id = profile.VisualProfile.ModelId.Value, path = modelPath, sha256 = Sha256File(modelPath), registeredSha256 = ClassifierSha, input = "[2,3,80,160]", output = "[2,2]", labels = new[] { "0_degree", "180_degree" } },
            source = new { dataset = "google-research-datasets/hiertext", revision = "70b6620b2b112597d8219e11eee9773a1403827c", root = datasetRoot, horizontalCropCount = crops.Count, generatedPairs = rows.Count, truth = "The second row is a deterministic 180-degree transform of the first row; this is a model contract, not a natural inverted-text accuracy label." },
            summary = new { correct, total = rows.Count, accuracy = rows.Count == 0 ? 0d : (double)correct / rows.Count, zeroExpected, oneEightyExpected, zeroPredicted, oneEightyPredicted, rejected = rows.Count(row => row.Rejected), dynamicBatchRows = rows.Count },
            rows
        };
        File.WriteAllText(path, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static void WriteMarkdown(string path, string backend, string modelPath, IReadOnlyList<HorizontalCrop> crops,
        IReadOnlyList<Observation> rows, int correct, int zeroExpected, int oneEightyExpected, int zeroPredicted, int oneEightyPredicted)
    {
        double accuracy = rows.Count == 0 ? 0 : (double)correct / rows.Count;
        var text = new StringBuilder();
        text.AppendLine("# PP-LCNet HierText 0/180 orientation classifier contract (2026-10-14)");
        text.AppendLine();
        text.AppendLine("This record runs the real PP-OCRv5 Mobile PP-LCNet text-line orientation model on linked HierText horizontal crops. Each crop is evaluated as a dynamic batch of two rows: the original crop (known 0°) and the same crop rotated 180° by DeploySharp's native crop path (known 180°).");
        text.AppendLine();
        text.AppendLine($"- Backend: `{backend}-cpu`");
        text.AppendLine($"- Model: `{Path.GetFileName(modelPath)}`; registered SHA-256 `{ClassifierSha}`");
        text.AppendLine($"- Horizontal source crops: `{crops.Count}`; dynamic-batch rows: `{rows.Count}`; input `[2,3,80,160]`; output `[2,2]`");
        text.AppendLine($"- Contract accuracy: `{correct}/{rows.Count}` (`{accuracy.ToString("P2", CultureInfo.InvariantCulture)}`); rejected rows: `{rows.Count(row => row.Rejected)}`");
        text.AppendLine($"- Expected 0°/180°: `{zeroExpected}/{oneEightyExpected}`; predicted 0°/180°: `{zeroPredicted}/{oneEightyPredicted}`");
        text.AppendLine();
        text.AppendLine("## Boundary");
        text.AppendLine();
        text.AppendLine("This closes only the deterministic PP-LCNet 0°/180° decoder, preprocessing and dynamic-batch execution contract. HierText does not label upright versus upside-down text, so this is not a natural orientation accuracy score. The model contract intentionally has no 90°/270° classes; vertical-text handling and a natural angle quality gate remain open and must not be inferred from this report.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string TensorSha(ITensor tensor)
    {
        if (tensor.Buffer is not float[] values) throw new InvalidOperationException("The orientation input must be Float32.");
        byte[] bytes = new byte[checked(values.Length * sizeof(float))];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private sealed record HorizontalCrop(string CropId, string ParentInstanceId, string Text, string ImagePath);
    private sealed record Observation(string CropId, string Expected, string Predicted, int ClassIndex, float Confidence, bool Rejected, string InputSha256, float[] Scores);
}
