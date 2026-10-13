using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Registry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Extends the v6-small DET batch-to-decoder evidence to every registered
/// PP-OCR v4/v5/v6 detector artifact on the same exact TensorRT runtime.
/// </summary>
[TestClass]
public sealed class PaddleOcrDetectorTensorRtDynamicBatchAllModelsIntegrationTests
{
    private const string DefaultImage = @"E:\Data\ocr\demo_1.jpg";
    private const int ModelSide = 736;
    private const float MinimumMatchedDecoderIoU = 0.90f;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void AllRegisteredPpOcrDetectorsRunBatchTwoThroughTensorRtAndDbDecoder()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_RUN_EXTERNAL=1 to run the authorized local TensorRT DET dynamic-batch test.");

        string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_IMAGE") ?? DefaultImage);
        var results = new List<ModelResult>();
        foreach (DetectorCase model in Cases)
        {
            if (!File.Exists(model.Path))
            {
                results.Add(ModelResult.Blocked(model, "missing-model", "The configured ONNX model does not exist."));
                continue;
            }

            try
            {
                results.Add(RunCase(model, imagePath));
            }
            catch (Exception exception)
            {
                string details = exception.ToString();
                Console.WriteLine("PADDLEOCR_TRT_DET_DYNAMIC_BATCH_BLOCKED model=" + model.ModelId + ";sha256=" + model.Sha256 + ";error=" + details);
                results.Add(ModelResult.Blocked(model, "runtime-or-importer", details));
            }
        }

        string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_DET_ALL_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddleocr-core-detectors-tensorrt-dynamic-batch-decoder-20261013.json");
        string? directory = Path.GetDirectoryName(Path.GetFullPath(report));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            environment = new
            {
                operatingSystem = Environment.OSVersion.VersionString,
                processArchitecture = Environment.Is64BitProcess ? "x64" : "x86",
                tensorRtApi = "TensorRT 11",
                tensorRtRoot = Environment.GetEnvironmentVariable("JYPPX_TENSORRT_ROOT"),
                cudaRoot = Environment.GetEnvironmentVariable("JYPPX_CUDA_ROOT"),
                cudnnRoot = Environment.GetEnvironmentVariable("JYPPX_CUDNN_ROOT"),
                image = imagePath,
                imageSha256 = Sha256File(imagePath),
                batch = 2,
                inputShape = new[] { 2, 3, ModelSide, ModelSide },
                minimumMatchedDecoderIoU = MinimumMatchedDecoderIoU
            },
            scope = "PP-OCRv4 mobile/server, PP-OCRv5 mobile/server and PP-OCRv6 tiny/small/medium DET exact ONNX artifacts; ORT CPU and DeploySharp-built TensorRT CUDA are both decoded by PaddleDbTextDetectionDecoder. Raw probability drift is recorded diagnostically; pass/fail is based on finite outputs, distinct batch rows, output batch/shape, source geometry and one-to-one decoded polygon matching.",
            results,
            boundary = "A pass proves raw output batch preservation, bounded ORT/TensorRT probability-map drift and per-row DB decoder geometry for the exact artifact/runtime. It does not prove detection recall, CER/WER, complete det-to-rec quality, throughput, cross-device engine portability or every TensorRT release."
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        TestContext.AddResultFile(report);

        string[] blocked = results.Where(result => result.Status != "pass").Select(result => result.ModelId).ToArray();
        if (blocked.Length > 0)
            Assert.Fail("TensorRT DET dynamic-batch/decoder evidence is blocked for: " + string.Join(", ", blocked) + ". See " + report);
    }

    private ModelResult RunCase(DetectorCase model, string imagePath)
    {
        PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(new ModelId(model.ModelId));
        PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, Artifact(descriptor), maximumBatch: 2);
        using PreparedVisualInput input = CreateDistinctRoiBatch(imagePath, profile.VisualProfile.Input.Name);
        RawRun ort = RunOrt(profile, model.Path, input);

        string engineRoot = Path.Combine(Path.GetTempPath(), "deploysharp-ppocr-trt-det-all-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(engineRoot);
        try
        {
            TensorRtOnnxEngineBuildResult build = BuildEngine(profile, model, Path.Combine(engineRoot, "det.engine"));
            RawRun trt = RunTensorRt(profile, build, input);
            Assert.AreEqual(2L, ort.Shape[0], model.ModelId + " ORT output lost batch row count.");
            Assert.AreEqual(2L, trt.Shape[0], model.ModelId + " TensorRT output lost batch row count.");
            CollectionAssert.AreEqual(ort.Shape.Skip(1).ToArray(), trt.Shape.Skip(1).ToArray(), model.ModelId + " output spatial shape differs between ORT and TensorRT.");
            Assert.IsTrue(ort.AllFinite && trt.AllFinite, model.ModelId + " returned a non-finite probability map.");
            Assert.AreNotEqual(ort.RowSha256[0], ort.RowSha256[1], model.ModelId + " ORT returned identical probability rows for distinct ROIs.");
            Assert.AreNotEqual(trt.RowSha256[0], trt.RowSha256[1], model.ModelId + " TensorRT returned identical probability rows for distinct ROIs.");

            double maximumDifference = 0;
            double sumDifference = 0;
            int count = Math.Min(ort.Values.Length, trt.Values.Length);
            for (int index = 0; index < count; index++)
            {
                double difference = Math.Abs((double)ort.Values[index] - trt.Values[index]);
                maximumDifference = Math.Max(maximumDifference, difference);
                sumDifference += difference;
            }
            double meanDifference = count == 0 ? 0 : sumDifference / count;
            TextDetectionBatchResult ortDecoded = DecodeRows(profile, input, ort);
            TextDetectionBatchResult trtDecoded = DecodeRows(profile, input, trt);
            Assert.AreEqual(2, ortDecoded.Count, model.ModelId + " ORT decoder did not return two rows.");
            Assert.AreEqual(2, trtDecoded.Count, model.ModelId + " TensorRT decoder did not return two rows.");
            var decoderComparisons = new DecoderComparison[input.BatchSize];
            for (int row = 0; row < input.BatchSize; row++)
            {
                Assert.AreEqual(input.BatchFrames[row].SourceSize, ortDecoded[row].SourceSize, model.ModelId + " ORT decoder lost source geometry for row " + row + ".");
                Assert.AreEqual(input.BatchFrames[row].SourceSize, trtDecoded[row].SourceSize, model.ModelId + " TensorRT decoder lost source geometry for row " + row + ".");
                decoderComparisons[row] = CompareDecodedRegions(ortDecoded[row], trtDecoded[row]);
                Assert.AreEqual(decoderComparisons[row].OrtRegionCount, decoderComparisons[row].TensorRtRegionCount, model.ModelId + " decoder region counts differ for row " + row + ".");
                Assert.AreEqual(decoderComparisons[row].OrtRegionCount, decoderComparisons[row].MatchedRegionCount, model.ModelId + " decoder could not match every region for row " + row + ".");
                Assert.IsTrue(decoderComparisons[row].MinimumMatchedIoU >= MinimumMatchedDecoderIoU, model.ModelId + " decoder polygon parity fell below IoU " + MinimumMatchedDecoderIoU.ToString("R", CultureInfo.InvariantCulture) + " for row " + row + ": " + decoderComparisons[row].MinimumMatchedIoU.ToString("R", CultureInfo.InvariantCulture));
            }

            Console.WriteLine("PADDLEOCR_TRT_DET_DYNAMIC_BATCH_DECODER model=" + model.ModelId + ";batch=2;shape=" + string.Join("x", trt.Shape) + ";maximumAbsDiff=" + maximumDifference.ToString("R", CultureInfo.InvariantCulture) + ";meanAbsDiff=" + meanDifference.ToString("R", CultureInfo.InvariantCulture) + ";ortRegionCounts=" + string.Join(",", ortDecoded.Items.Select(item => item.Regions.Count)) + ";trtRegionCounts=" + string.Join(",", trtDecoded.Items.Select(item => item.Regions.Count)) + ";minimumDecoderIoU=" + decoderComparisons.Min(item => item.MinimumMatchedIoU).ToString("R", CultureInfo.InvariantCulture) + ";engineSha=" + build.EngineSha256);
            return ModelResult.Pass(model, build, input, ort, trt, maximumDifference, meanDifference, decoderComparisons);
        }
        finally
        {
            try { Directory.Delete(engineRoot, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static PreparedVisualInput CreateDistinctRoiBatch(string imagePath, string inputName)
    {
        var factory = new OpenCvVisualInputFactory();
        VisualSize sourceSize;
        using (PreparedVisualInput probe = factory.CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr))) sourceSize = probe.SourceSize;
        float split = sourceSize.Width / 2f;
        var geometries = new IVisualRoiGeometry[]
        {
            new RectangleRoiGeometry(new RectangleF(0, 0, split, sourceSize.Height)),
            new RectangleRoiGeometry(new RectangleF(split, 0, sourceSize.Width - split, sourceSize.Height))
        };
        var options = new OpenCvPreprocessOptions(new VisualSize(ModelSide, ModelSide), OpenCvResizeMode.Resize, VisualColorOrder.Bgr, OpenCvAlphaMode.Drop,
            means: new[] { .485f, .456f, .406f }, standardDeviations: new[] { .229f, .224f, .225f }, inputDivisors: new[] { 255f, 255f, 255f });
        return factory.CreateRoiBatch(OpenCvImageSource.FromFile(imagePath), geometries, inputName, options, "trt-det-all-dynamic-batch");
    }

    private static RawRun RunOrt(PaddleOcrProfile profile, string path, PreparedVisualInput input)
    {
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"), SessionOptions.Default);
        InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
        return Summarize(outputs.GetRequired(profile.VisualProfile.Outputs[0].Name));
    }

    private static TensorRtOnnxEngineBuildResult BuildEngine(PaddleOcrProfile profile, DetectorCase model, string enginePath)
    {
        var minimum = new TensorShape(1, 3, ModelSide, ModelSide);
        var optimum = new TensorShape(2, 3, ModelSide, ModelSide);
        var options = new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt11, precision: TensorRtOnnxEnginePrecision.RuntimeDefault, workspaceBytes: 268435456UL, optimizationLevel: 0, overwrite: true,
            inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, minimum, optimum, optimum) });
        var artifact = new ModelArtifact(new ModelId(model.ModelId), "onnx", model.Path, model.Sha256);
        return new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, options);
    }

    private static RawRun RunTensorRt(PaddleOcrProfile profile, TensorRtOnnxEngineBuildResult build, PreparedVisualInput input)
    {
        using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11, cudaTargetArchitecture: "sm_86"));
        using IInferenceSession session = provider.CreateSession(new ModelArtifact(profile.VisualProfile.ModelId, "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"), SessionOptions.Default);
        InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
        return Summarize(outputs.GetRequired(profile.VisualProfile.Outputs[0].Name));
    }

    private static TextDetectionBatchResult DecodeRows(PaddleOcrProfile profile, PreparedVisualInput input, RawRun raw)
    {
        var tensor = new Tensor<float>(new TensorShape(raw.Shape), raw.Values, TensorBufferOwnership.Borrow);
        object decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, InferenceOutputs.Create(profile.VisualProfile.Outputs[0].Name, tensor), default));
        return decoded as TextDetectionBatchResult ?? throw new AssertFailedException("The dynamic DB decoder did not return a batch result.");
    }

    private static DecoderComparison CompareDecodedRegions(TextDetectionResult ort, TextDetectionResult tensorRt)
    {
        int leftCount = ort.Regions.Count;
        int rightCount = tensorRt.Regions.Count;
        if (leftCount == 0 && rightCount == 0) return new DecoderComparison(0, 0, 0, 1f);
        var edges = new List<(int Left, int Right, float Iou)>();
        for (int left = 0; left < leftCount; left++)
            for (int right = 0; right < rightCount; right++)
            {
                float iou = TextPolygon.IntersectionOverUnion(ort.Regions[left].Polygon, tensorRt.Regions[right].Polygon);
                if (iou > 0) edges.Add((left, right, iou));
            }
        edges.Sort((first, second) => second.Iou.CompareTo(first.Iou));
        var matchedLeft = new bool[leftCount];
        var matchedRight = new bool[rightCount];
        var matched = new List<float>(Math.Min(leftCount, rightCount));
        foreach ((int left, int right, float iou) in edges)
        {
            if (matchedLeft[left] || matchedRight[right]) continue;
            matchedLeft[left] = true;
            matchedRight[right] = true;
            matched.Add(iou);
        }
        return new DecoderComparison(leftCount, rightCount, matched.Count, matched.Count == 0 ? 0f : matched.Min());
    }

    private static RawRun Summarize(ITensor tensor)
    {
        if (tensor is not Tensor<float> valuesTensor) throw new AssertFailedException("The DET output must be Float32.");
        float[] values = valuesTensor.ToArray();
        bool finite = true;
        double maximum = 0;
        double sum = 0;
        foreach (float value in values) { finite &= float.IsFinite(value); maximum = Math.Max(maximum, Math.Abs(value)); sum += Math.Abs(value); }
        long[] shape = tensor.Shape.ToArray();
        int rowLength = checked((int)(values.Length / shape[0]));
        var rows = new string[shape[0]];
        using SHA256 sha = SHA256.Create();
        for (int row = 0; row < shape[0]; row++)
        {
            byte[] bytes = new byte[rowLength * sizeof(float)];
            Buffer.BlockCopy(values, row * rowLength, bytes, 0, bytes.Length);
            rows[row] = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }
        return new RawRun(shape, values, Convert.ToHexString(Sha256(values)).ToLowerInvariant(), rows, finite, maximum, values.Length == 0 ? 0 : sum / values.Length);
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor)
    {
        PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
        return new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "paddleocr-db-raw-dynamic-batch-v1");
    }

    private static byte[] Sha256(float[] values) { byte[] bytes = new byte[checked(values.Length * sizeof(float))]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length); using SHA256 sha = SHA256.Create(); return sha.ComputeHash(bytes); }
    private static string RowSha256(ITensor tensor, int row)
    {
        var values = (Tensor<float>)tensor;
        float[] buffer = values.ToArray();
        int rowLength = checked((int)(buffer.Length / tensor.Shape[0]));
        byte[] bytes = new byte[rowLength * sizeof(float)];
        Buffer.BlockCopy(buffer, row * rowLength, bytes, 0, bytes.Length);
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
    private static string Sha256File(string path) { using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path); return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant(); }
    private static string RequireFile(string path) { if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR file does not exist: " + path); return path; }

    private static readonly DetectorCase[] Cases =
    {
        new("paddleocr/ppocrv4/mobile-det", @"E:\Model\paddleocr\PP-OCRv4\PP-OCRv4_mobile_det.onnx", "63e5e65c450d1fc85bf3670916cfc0041ad5acc3b8e4773de59337d413ae27eb"),
        new("paddleocr/ppocrv4/server-det", @"E:\Model\paddleocr\PP-OCRv4\PP-OCRv4_server_det.onnx", "1d6b24b3038d814ba5243691d4d5c2af2d3451f909f12617748aba508345f6b9"),
        new("paddleocr/ppocrv5/mobile-det", @"E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_mobile_det.onnx", "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039"),
        new("paddleocr/ppocrv5/server-det", @"E:\Model\paddleocr\PP-OCRv5\PP-OCRv5_server_det.onnx", "0c4ff76f78feb3e4b4e9b3030df350234f570027e1ec9cfe5bbb7a596cb57f47"),
        new("paddleocr/ppocrv6/tiny-det", @"E:\Model\paddleocr\PP-OCRv6\tiny\PP-OCRv6_tiny_det_inference.onnx", "193bab7a04fca699a6c82e6abb5b81bdb28177f0abd4062552b04908dafb19f8"),
        new("paddleocr/ppocrv6/small-det", @"E:\Model\paddleocr\PP-OCRv6\small\PP-OCRv6_small_det_inference.onnx", "d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e"),
        new("paddleocr/ppocrv6/medium-det", @"E:\Model\paddleocr\PP-OCRv6\medium\PP-OCRv6_medium_det_inference.onnx", "eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1")
    };

    private sealed record DetectorCase(string ModelId, string Path, string Sha256);
    private sealed record DecoderComparison(int OrtRegionCount, int TensorRtRegionCount, int MatchedRegionCount, float MinimumMatchedIoU);
    private sealed record RawRun(long[] Shape, float[] Values, string OutputSha256, string[] RowSha256, bool AllFinite, double MaximumAbs, double MeanAbs);
    private sealed record ModelResult(string ModelId, string OnnxSha256, string Status, string? Blocker, object? Evidence)
    {
        public static ModelResult Blocked(DetectorCase model, string blocker, string details) => new(model.ModelId, model.Sha256, "blocked", blocker, new { details });
        public static ModelResult Pass(DetectorCase model, TensorRtOnnxEngineBuildResult build, PreparedVisualInput input, RawRun ort, RawRun trt, double maximumDifference, double meanDifference, IReadOnlyList<DecoderComparison> decoderComparisons) => new(model.ModelId, model.Sha256, "pass", null, new
        {
            engineSha256 = build.EngineSha256,
            engineBytes = build.EngineBytes,
            outputShape = trt.Shape,
            ortOutputSha256 = ort.OutputSha256,
            tensorRtOutputSha256 = trt.OutputSha256,
            ortOutputRowSha256 = ort.RowSha256,
            tensorRtOutputRowSha256 = trt.RowSha256,
            inputRowSha256 = Enumerable.Range(0, input.BatchSize).Select(row => RowSha256(input.Tensor, row)).ToArray(),
            inputSourceSizes = input.BatchFrames.Select(frame => new { width = frame.SourceSize.Width, height = frame.SourceSize.Height }).ToArray(),
            maximumAbsDifference = maximumDifference,
            meanAbsDifference = meanDifference,
            decoderComparisons
        });
    }
}
