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

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>
    /// Verifies PP-OCRv6 Small DET raw dynamic-batch row isolation on ORT CPU
    /// and a freshly built TensorRT engine. The production DB decoder remains
    /// intentionally batch=1 until source geometry per row is specified.
    /// / 验证 PP-OCRv6 Small DET 原始动态 Batch 行隔离；生产 DB Decoder 在明确逐行几何前仍保持 batch=1。
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrDetectionTensorRtDynamicBatchIntegrationTests
    {
        private const string ImageDefault = @"E:\Data\ocr\demo_1.jpg";
        private const string ModelDefault = @"E:\Model\paddleocr\PP-OCRv6\small\PP-OCRv6_small_det_inference.onnx";
        private const string ModelSha = "d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e";
        private const int ModelSide = 736;

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RealRoiRowsRunOnOrtAndTensorRtDynamicBatchTwo()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_RUN_EXTERNAL=1 to run the authorized local TensorRT DET dynamic-batch test.");
            string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_IMAGE") ?? ImageDefault);
            string modelPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_DET_MODEL") ?? ModelDefault);
            PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/small-det"));
            PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, Artifact(descriptor), maximumBatch: 1);
            using PreparedVisualInput input = CreateDistinctRoiBatch(imagePath, profile.VisualProfile.Input.Name);
            Assert.AreEqual(2, input.BatchSize);
            string[] inputRows = Enumerable.Range(0, 2).Select(row => RowSha256(input.Tensor, row)).ToArray();
            CollectionAssert.AllItemsAreNotNull(inputRows);
            Assert.AreNotEqual(inputRows[0], inputRows[1], "The two DET ROI rows must be different.");

            RawRun ort = RunOrt(profile, modelPath, input);
            string engineRoot = Path.Combine(Path.GetTempPath(), "deploysharp-ppocr-trt-det-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(engineRoot);
            try
            {
                TensorRtOnnxEngineBuildResult build = BuildEngine(profile, modelPath, Path.Combine(engineRoot, "ppocrv6-small-det.engine"));
                RawRun trt = RunTensorRt(profile, build, input);
                Assert.AreEqual(2L, ort.Shape[0], "ORT DET output must preserve batch row count.");
                Assert.AreEqual(2L, trt.Shape[0], "TensorRT DET output must preserve batch row count.");
                Assert.AreEqual(ort.Shape.Length, trt.Shape.Length);
                for (int dimension = 1; dimension < ort.Shape.Length; dimension++) Assert.AreEqual(ort.Shape[dimension], trt.Shape[dimension], "DET output dimension differs at " + dimension + ".");
                Assert.AreNotEqual(ort.RowSha256[0], ort.RowSha256[1], "ORT returned identical DET probability rows for distinct inputs.");
                Assert.AreNotEqual(trt.RowSha256[0], trt.RowSha256[1], "TensorRT returned identical DET probability rows for distinct inputs.");
                Assert.IsTrue(ort.AllFinite && trt.AllFinite, "DET raw outputs must contain only finite values.");
                double maximumDifference = 0;
                double sumDifference = 0;
                long count = Math.Min(ort.Values.Length, trt.Values.Length);
                for (int index = 0; index < count; index++)
                {
                    double difference = Math.Abs((double)ort.Values[index] - trt.Values[index]);
                    maximumDifference = Math.Max(maximumDifference, difference);
                    sumDifference += difference;
                }
                double meanDifference = count == 0 ? 0 : sumDifference / count;
                Assert.IsTrue(maximumDifference <= 0.05, "TensorRT DET raw probability drift exceeded 0.05: " + maximumDifference.ToString("R", CultureInfo.InvariantCulture));
                Console.WriteLine("PADDLEOCR_TRT_DET_DYNAMIC_BATCH model=paddleocr/ppocrv6/small-det;batch=2;shape=" + string.Join("x", trt.Shape) + ";maximumAbsDiff=" + maximumDifference.ToString("R", CultureInfo.InvariantCulture) + ";meanAbsDiff=" + meanDifference.ToString("R", CultureInfo.InvariantCulture) + ";engineSha=" + build.EngineSha256);
                WriteReport(imagePath, input, build, ort, trt, maximumDifference, meanDifference);
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
            return factory.CreateRoiBatch(OpenCvImageSource.FromFile(imagePath), geometries, inputName, options, "trt-det-dynamic-batch");
        }

        private static RawRun RunOrt(PaddleOcrProfile profile, string path, PreparedVisualInput input)
        {
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), request, SessionOptions.Default);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
            return Summarize(outputs.GetRequired(profile.VisualProfile.Outputs[0].Name));
        }

        private static TensorRtOnnxEngineBuildResult BuildEngine(PaddleOcrProfile profile, string path, string enginePath)
        {
            var minimum = new TensorShape(1, 3, ModelSide, ModelSide);
            var optimum = new TensorShape(2, 3, ModelSide, ModelSide);
            var options = new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt11, precision: TensorRtOnnxEnginePrecision.RuntimeDefault, workspaceBytes: 268435456UL, optimizationLevel: 0, overwrite: true,
                inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, minimum, optimum, optimum) });
            return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, options);
        }

        private static RawRun RunTensorRt(PaddleOcrProfile profile, TensorRtOnnxEngineBuildResult build, PreparedVisualInput input)
        {
            var request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11, cudaTargetArchitecture: "sm_86"));
            using IInferenceSession session = provider.CreateSession(new ModelArtifact(profile.VisualProfile.ModelId, "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), request, SessionOptions.Default);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
            return Summarize(outputs.GetRequired(profile.VisualProfile.Outputs[0].Name));
        }

        private void WriteReport(string imagePath, PreparedVisualInput input, TensorRtOnnxEngineBuildResult build, RawRun ort, RawRun trt, double maximumDifference, double meanDifference)
        {
            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_REPORT_PATH") ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddleocr-v6-small-det-tensorrt-dynamic-batch.json");
            string? directory = Path.GetDirectoryName(Path.GetFullPath(report)); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                model = "paddleocr/ppocrv6/small-det",
                input = new { image = imagePath, imageSha256 = Sha256File(imagePath), tensorSha256 = Sha256Tensor(input.Tensor), shape = input.Tensor.Shape.ToArray(), batch = input.BatchSize, rowSha256 = Enumerable.Range(0, input.BatchSize).Select(row => RowSha256(input.Tensor, row)).ToArray() },
                detector = new { onnxSha256 = ModelSha, engineSha256 = build.EngineSha256, engineBytes = build.EngineBytes },
                backends = new { ortCpu = new { outputShape = ort.Shape, outputSha256 = ort.OutputSha256, rowSha256 = ort.RowSha256, maximumAbs = ort.MaximumAbs, meanAbs = ort.MeanAbs }, tensorRtCuda = new { outputShape = trt.Shape, outputSha256 = trt.OutputSha256, rowSha256 = trt.RowSha256, maximumAbs = trt.MaximumAbs, meanAbs = trt.MeanAbs } },
                comparison = new { maximumAbsDifference = maximumDifference, meanAbsDifference = meanDifference },
                contract = "Both backends must preserve batch=2, return finite raw probability maps with matching non-batch dimensions, and keep distinct input rows distinct; numeric drift is bounded by 0.05.",
                boundary = "Raw graph/provider dynamic-batch evidence only. PaddleDbTextDetectionDecoder currently requires batch=1 because per-row source geometry and postprocess mapping are not yet defined. This is not full DET quality, OCR pipeline parity, throughput or all-model TensorRT support."
            }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            TestContext.AddResultFile(report);
        }

        private static RawRun Summarize(ITensor tensor)
        {
            if (!(tensor is Tensor<float> valuesTensor)) throw new AssertFailedException("The DET output must be Float32.");
            float[] values = valuesTensor.ToArray();
            bool finite = true; double maximum = 0; double sum = 0;
            foreach (float value in values) { finite &= float.IsFinite(value); maximum = Math.Max(maximum, Math.Abs(value)); sum += Math.Abs(value); }
            long[] shape = tensor.Shape.ToArray(); int rowLength = checked((int)(values.Length / shape[0])); var rows = new string[shape[0]];
            using SHA256 sha = SHA256.Create();
            for (int row = 0; row < shape[0]; row++) { byte[] bytes = new byte[rowLength * sizeof(float)]; Buffer.BlockCopy(values, row * rowLength, bytes, 0, bytes.Length); rows[row] = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant(); }
            return new RawRun(shape, values, Convert.ToHexString(Sha256(values)).ToLowerInvariant(), rows, finite, maximum, values.Length == 0 ? 0 : sum / values.Length);
        }

        private static byte[] Sha256(float[] values) { byte[] bytes = new byte[checked(values.Length * sizeof(float))]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length); using SHA256 sha = SHA256.Create(); return sha.ComputeHash(bytes); }
        private static string RowSha256(ITensor tensor, int row)
        {
            var values = (Tensor<float>)tensor; float[] buffer = values.ToArray(); int rowLength = checked((int)(buffer.Length / tensor.Shape[0])); byte[] bytes = new byte[rowLength * sizeof(float)]; Buffer.BlockCopy(buffer, row * rowLength, bytes, 0, bytes.Length); using SHA256 sha = SHA256.Create(); return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }
        private static string Sha256Tensor(ITensor tensor) => Convert.ToHexString(Sha256(((Tensor<float>)tensor).ToArray())).ToLowerInvariant();
        private static string Sha256File(string path) { using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path); return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant(); }
        private static string RequireFile(string path) { if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR file does not exist: " + path); return path; }
        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor) { PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId); return new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "paddleocr-db-raw-dynamic-batch-v1"); }
        private sealed record RawRun(long[] Shape, float[] Values, string OutputSha256, string[] RowSha256, bool AllFinite, double MaximumAbs, double MeanAbs);
    }
}
