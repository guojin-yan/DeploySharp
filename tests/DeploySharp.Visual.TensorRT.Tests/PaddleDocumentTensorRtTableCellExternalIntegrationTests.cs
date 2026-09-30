using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.TensorRT;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests
{
    /// <summary>Runs both RT-DETR table-cell artifacts through the TensorRT builder, NMS decoder, and ORT parity contract.</summary>
    [TestClass]
    public sealed class PaddleDocumentTensorRtTableCellExternalIntegrationTests
    {
        private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
        private const string ImagePath = @"E:\Data\image\bus.jpg";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AllLocalTableCellNmsRunThroughTensorRtAndPreserveOrtDecoding()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1 to run the opt-in PP-Structure TensorRT test.");
            if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing local PP-Structure test image: " + ImagePath);

            var cases = new[]
            {
                (Model: "paddle-table/rt-detr-l-wired-cell-det", File: "rt-detr-l-wired-cell-det.onnx"),
                (Model: "paddle-table/rt-detr-l-wireless-cell-det", File: "rt-detr-l-wireless-cell-det.onnx")
            };
            var evidence = new List<object>(cases.Length);
            int unexpectedFailures = 0;
            int passed = 0;
            int unsupported = 0;
            foreach (var item in cases)
            {
                string onnxPath = Path.Combine(ModelRoot, item.File);
                if (!File.Exists(onnxPath))
                {
                    evidence.Add(new { model = item.Model, status = "missing-model", file = item.File });
                    unexpectedFailures++;
                    continue;
                }

                string root = Path.Combine(Path.GetTempPath(), "deploysharp-paddle-table-cell-trt-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    PaddleDocumentProfile document = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                        PaddleDocumentModelCatalog.Get(item.Model),
                        new[] { "table-cell" },
                        new VisualSize(640, 640),
                        includeGeometryInputs: true,
                        scoreThreshold: 0);
                    string onnxSha256 = ComputeSha256(onnxPath);
                    string enginePath = Path.Combine(root, Path.GetFileNameWithoutExtension(item.File) + ".engine");
                    TensorRtApiVersion apiVersion = ResolveApiVersion();
                    var shape = new TensorShape(1, 2);
                    var buildOptions = new TensorRtOnnxEngineBuildOptions(
                        apiVersion: apiVersion,
                        workspaceBytes: 536870912UL,
                        optimizationLevel: 3,
                        inputProfiles: new[]
                        {
                            new TensorRtOnnxInputProfile("im_shape", shape, shape, shape),
                            new TensorRtOnnxInputProfile("image", new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640), new TensorShape(1, 3, 640, 640)),
                            new TensorRtOnnxInputProfile("scale_factor", shape, shape, shape)
                        },
                        overwrite: true);

                    Stopwatch buildWatch = Stopwatch.StartNew();
                    TensorRtOnnxEngineBuildResult build;
                    try
                    {
                        build = new TensorRtOnnxEngineBuilder().Build(
                            new ModelArtifact(new ModelId(item.Model), "onnx", onnxPath, onnxSha256, TensorRtBackendProvider.BackendId),
                            enginePath,
                            buildOptions);
                    }
                    catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                    {
                        evidence.Add(new { model = item.Model, status = "blocked", errorCode = exception.ErrorCode, technicalDetails = exception.TechnicalDetails, message = exception.Message });
                        WriteReport(evidence, passed, ++unsupported, unexpectedFailures, "TensorRT native runtime was unavailable; no model/backend status was promoted.");
                        Assert.Inconclusive("TensorRT native runtime is unavailable or mismatched. " + (exception.TechnicalDetails ?? exception.Message));
                        return;
                    }
                    catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.OnnxParseFailed || exception.ErrorCode == TensorRtErrorCodes.EngineBuildFailed)
                    {
                        unsupported++;
                        evidence.Add(new { model = item.Model, status = "unsupported", modelSha256 = onnxSha256, errorCode = exception.ErrorCode, technicalDetails = exception.TechnicalDetails, message = exception.Message });
                        Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_TABLE_CELL model=" + item.Model + ";status=unsupported;errorCode=" + exception.ErrorCode + ";details=" + (exception.TechnicalDetails ?? exception.Message));
                        continue;
                    }
                    buildWatch.Stop();

                    using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(
                        new OpenCvVisualInputFactory(), ImagePath, document, inputId: "paddle-document-tensorrt-table-cell");
                    var inputList = new List<NamedTensor>(1 + input.AuxiliaryInputs.Count) { new NamedTensor(input.InputName, input.Tensor) };
                    inputList.AddRange(input.AuxiliaryInputs);

                    DetectionResult ortReference;
                    using (var ortRegistry = new BackendRegistry().UseOnnxRuntime())
                    using (IInferenceSession ortSession = ortRegistry.CreateSession(
                        document.CreateArtifact(onnxPath, OnnxRuntimeBackendProvider.BackendId),
                        new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu")))
                    {
                        InferenceOutputs outputs = ortSession.Run(new InferenceInputs(inputList), CancellationToken.None);
                        ortReference = (DetectionResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                    }

                    TensorRtBackendOptions backendOptions = new TensorRtBackendOptions(
                        apiVersion,
                        cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_86");
                    var samples = new List<double>();
                    DetectionResult trtResult = null!;
                    int warmup = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_CELL_WARMUP", 5);
                    int iterations = ResolvePositiveInt("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_CELL_ITERATIONS", 50);
                    using (var provider = new TensorRtBackendProvider(backendOptions))
                    using (IInferenceSession session = provider.CreateSession(
                        new ModelArtifact(new ModelId(item.Model), "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId),
                        new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
                        SessionOptions.Default))
                    {
                        for (int index = -warmup; index < iterations; index++)
                        {
                            Stopwatch watch = Stopwatch.StartNew();
                            InferenceOutputs outputs = session.Run(new InferenceInputs(inputList), CancellationToken.None);
                            trtResult = (DetectionResult)document.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, document.VisualProfile, outputs, CancellationToken.None));
                            watch.Stop();
                            if (index >= 0) samples.Add(watch.Elapsed.TotalMilliseconds);
                        }
                    }

                    Assert.IsTrue(ortReference.Detections.Count > 0, item.Model + " ORT returned no table-cell regions.");
                    Assert.AreEqual(ortReference.Detections.Count, trtResult.Detections.Count, item.Model + " changed the exported NMS result count.");
                    const float confidenceFloor = .05f;
                    Detection[] expected = SortConfident(ortReference, confidenceFloor);
                    Detection[] actual = SortConfident(trtResult, confidenceFloor);
                    Assert.IsTrue(Math.Abs(expected.Length - actual.Length) <= 1, item.Model + " changed the confident region count by more than one candidate.");
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_TABLE_CELL_COMPARE model=" + item.Model + ";ort=" + JsonSerializer.Serialize(expected.Take(5).Select(value => new { label = value.Label.Index, score = value.Label.Score, x = value.Box.X, y = value.Box.Y, w = value.Box.Width, h = value.Box.Height })) + ";trt=" + JsonSerializer.Serialize(actual.Take(5).Select(value => new { label = value.Label.Index, score = value.Label.Score, x = value.Box.X, y = value.Box.Y, w = value.Box.Width, h = value.Box.Height })));
                    int compareCount = Math.Min(expected.Length, actual.Length);
                    Detection[] expectedComparable = expected.Take(compareCount).ToArray();
                    int[] matches = MatchByIoU(expectedComparable, actual, out float minimumMatchedIou);
                    float maximumScoreDelta = 0;
                    for (int index = 0; index < expectedComparable.Length; index++)
                    {
                        int match = matches[index];
                        Assert.IsTrue(match >= 0, item.Model + " did not return a matching cell geometry.");
                        Detection candidate = actual[match];
                        Assert.AreEqual(expectedComparable[index].Label.Index, candidate.Label.Index, item.Model + " changed a cell label.");
                        float scoreDelta = Math.Abs(expectedComparable[index].Label.Score - candidate.Label.Score);
                        Assert.IsTrue(scoreDelta <= .03f, item.Model + " confidence drift exceeded .03; observed " + scoreDelta.ToString("R"));
                        maximumScoreDelta = Math.Max(maximumScoreDelta, scoreDelta);
                    }
                    Assert.IsTrue(minimumMatchedIou >= .85f, item.Model + " did not preserve cell geometry at IoU >= .85; minimum IoU=" + minimumMatchedIou.ToString("R"));

                    double[] sorted = samples.OrderBy(value => value).ToArray();
                    passed++;
                    evidence.Add(new
                    {
                        model = item.Model,
                        status = "passed",
                        modelSha256 = onnxSha256,
                        engineSha256 = build.EngineSha256,
                        engineBytes = build.EngineBytes,
                        api = (int)apiVersion,
                        engineBuildMs = buildWatch.Elapsed.TotalMilliseconds,
                        warmup,
                        iterations,
                        ortDetections = ortReference.Detections.Count,
                        trtDetections = trtResult.Detections.Count,
                        ortConfidentDetections = expected.Length,
                        trtConfidentDetections = actual.Length,
                        comparedConfidentDetections = compareCount,
                        confidenceFloor,
                        maximumScoreDelta,
                        scoreTolerance = .03,
                        minimumMatchedIou,
                        iouThreshold = .85,
                        p50Ms = Percentile(sorted, .50),
                        p95Ms = Percentile(sorted, .95),
                        cudaArchitecture = backendOptions.CudaTargetArchitecture
                    });
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_TABLE_CELL model=" + item.Model + ";status=passed;detections=" + trtResult.Detections.Count + ";p50Ms=" + Percentile(sorted, .50).ToString("F3") + ";p95Ms=" + Percentile(sorted, .95).ToString("F3"));
                }
                catch (AssertFailedException exception)
                {
                    unexpectedFailures++;
                    evidence.Add(new { model = item.Model, status = "failed", exception = exception.ToString() });
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_TABLE_CELL model=" + item.Model + ";status=failed;exception=" + exception.Message);
                }
                catch (Exception exception)
                {
                    unexpectedFailures++;
                    evidence.Add(new { model = item.Model, status = "failed", exception = exception.ToString() });
                    Console.WriteLine("PADDLE_DOCUMENT_TENSORRT_TABLE_CELL model=" + item.Model + ";status=failed;exception=" + exception);
                }
                finally
                {
                    try { Directory.Delete(root, recursive: true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }

            WriteReport(evidence, passed, unsupported, unexpectedFailures, "Execution and ORT parity evidence only; no cell recall, table structure accuracy, or cross-device performance claim is made.");
            Assert.AreEqual(0, unexpectedFailures, "Unexpected TensorRT table-cell failures must be investigated.");
            Assert.AreEqual(cases.Length, evidence.Count, "Every table-cell artifact must have an explicit evidence row.");
            if (passed == 0 && unsupported > 0) Assert.Inconclusive("TensorRT importer/runtime did not accept any table-cell artifact; see the evidence report.");
        }

        private void WriteReport(IReadOnlyList<object> evidence, int passed, int unsupported, int failed, string boundary)
        {
            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_TABLE_CELL_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddle-document-tensorrt-table-cell-matrix.json");
            string? directory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                backend = "tensorrt-cuda",
                runtime = DescribeNativeRuntime(),
                input = new { file = "bus.jpg", path = ImagePath },
                scope = "The wired and wireless PP-Structure RT-DETR table-cell ONNX artifacts with their registered Paddle NMS decoder/input contracts.",
                counts = new { total = evidence.Count, passed, unsupported, failed },
                results = evidence,
                boundary
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
        }

        private static Detection[] SortConfident(DetectionResult result, float floor) => result.Detections
            .Where(value => value.Label.Score >= floor)
            .OrderBy(value => value.Label.Index)
            .ThenByDescending(value => value.Label.Score)
            .ThenBy(value => value.Box.X)
            .ThenBy(value => value.Box.Y)
            .ToArray();

        private static int[] MatchByIoU(IReadOnlyList<Detection> expected, IReadOnlyList<Detection> actual, out float minimumIou)
        {
            var pairs = new List<(float Iou, int Expected, int Actual)>(expected.Count * actual.Count);
            for (int expectedIndex = 0; expectedIndex < expected.Count; expectedIndex++)
            {
                for (int actualIndex = 0; actualIndex < actual.Count; actualIndex++)
                {
                    if (expected[expectedIndex].Label.Index != actual[actualIndex].Label.Index) continue;
                    pairs.Add((IntersectionOverUnion(expected[expectedIndex].Box, actual[actualIndex].Box), expectedIndex, actualIndex));
                }
            }
            pairs.Sort((left, right) => right.Iou.CompareTo(left.Iou));
            int[] matches = Enumerable.Repeat(-1, expected.Count).ToArray();
            bool[] usedActual = new bool[actual.Count];
            minimumIou = 1;
            int assigned = 0;
            foreach (var pair in pairs)
            {
                if (matches[pair.Expected] >= 0 || usedActual[pair.Actual]) continue;
                matches[pair.Expected] = pair.Actual;
                usedActual[pair.Actual] = true;
                minimumIou = Math.Min(minimumIou, pair.Iou);
                assigned++;
            }
            if (assigned != expected.Count) minimumIou = 0;
            return matches;
        }

        private static float IntersectionOverUnion(RectangleF left, RectangleF right)
        {
            float x1 = Math.Max(left.X, right.X);
            float y1 = Math.Max(left.Y, right.Y);
            float x2 = Math.Min(left.X + left.Width, right.X + right.Width);
            float y2 = Math.Min(left.Y + left.Height, right.Y + right.Height);
            float intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float union = Math.Max(0, left.Width) * Math.Max(0, left.Height) + Math.Max(0, right.Width) * Math.Max(0, right.Height) - intersection;
            return union <= 0 ? 0 : intersection / union;
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 0) return 0;
            int index = Math.Max(0, Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * percentile) - 1));
            return sorted[index];
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        private static TensorRtApiVersion ResolveApiVersion()
        {
            string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API") ?? Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API");
            if (int.TryParse(value, out int number) && Enum.IsDefined(typeof(TensorRtApiVersion), number)) return (TensorRtApiVersion)number;
            if (Enum.TryParse(value, true, out TensorRtApiVersion parsed) && Enum.IsDefined(typeof(TensorRtApiVersion), parsed)) return parsed;
            string root = Environment.GetEnvironmentVariable("JYPPX_TENSORRT_ROOT") ?? string.Empty;
            var match = System.Text.RegularExpressions.Regex.Match(root, @"TensorRT-(8|10|11)(?:\.|-|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int inferred) && Enum.IsDefined(typeof(TensorRtApiVersion), inferred)) return (TensorRtApiVersion)inferred;
            return TensorRtApiVersion.TensorRt11;
        }

        private static int ResolvePositiveInt(string name, int fallback)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
        }

        private static string DescribeNativeRuntime()
        {
            string[] variables = { "JYPPX_NATIVE_BRIDGE_PATH", "JYPPX_TENSORRT_ROOT", "JYPPX_CUDA_ROOT", "JYPPX_CUDNN_ROOT" };
            return string.Join("|", variables.Select(variable => variable + "=" + (Environment.GetEnvironmentVariable(variable) ?? "<unset>")));
        }
    }
}
