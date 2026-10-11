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
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>
    /// Compares PP-OCRv5 text-line orientation decoding for two real detector
    /// crops on ORT CPU and a freshly built TensorRT engine. This is opt-in
    /// because it requires local image/model/native CUDA assets.
    /// / 使用真实检测裁剪在 ORT CPU 与新建 TensorRT Engine 上比较 PP-OCRv5 文本行方向解码。
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrOrientationTensorRtParityIntegrationTests
    {
        private const string ImageDefault = @"E:\Data\ocr\demo_1.jpg";
        private const string ModelRootDefault = @"E:\Model\paddleocr";
        private const string ClassifierSha = "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2";
        private const string DetectorSha = "d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RealDetectorCropsDecodeTheSameOnOrtAndTensorRtDynamicBatchTwo()
        {
            RequireExternal();
            string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_CLS_PARITY_IMAGE") ?? ImageDefault);
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault;
            string detectorPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_det_inference.onnx"));
            string classifierPath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_CLS_MODEL") ?? Path.Combine(root, "PP-OCRv5", "PP-OCRv5_mobile_cls.onnx"));

            var detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/small-det"));
            var classifierDescriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv5/mobile-cls"));
            PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorDescriptor), maximumBatch: 1);
            PaddleOcrProfile classifier = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Artifact(classifierDescriptor), maximumBatch: 2);
            VisualSize sourceSize = ProbeSourceSize(imagePath);

            using var input = new OpenCvOcrImageInputFactory().CreateFromFile(
                imagePath,
                detector.VisualProfile.Input.Name,
                OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize),
                "trt-cls-parity");
            TextDetectionResult detection = RunDetector(detector, detectorPath, input.DetectionInput);
            Assert.IsTrue(detection.Regions.Count >= 2, "The real detector must provide at least two regions for batch row isolation.");
            TextRegion[] regions = detection.Regions.Take(2).ToArray();
            TextCropRequest[] requests = regions.Select(region => new TextCropRequest(region, classifier.CropProfile!)).ToArray();
            using PreparedVisualInput cropBatch = input.PrepareRecognitionBatch(classifier.VisualProfile.Input.Name, requests, default);
            Assert.AreEqual(2, cropBatch.BatchSize);
            Assert.AreNotEqual(RowSha256(cropBatch, 0), RowSha256(cropBatch, 1), "The two detector-derived crop rows must not be identical.");

            OrientationRun ort = RunOnOrt(classifier, classifierPath, cropBatch);
            string engineRoot = Path.Combine(Path.GetTempPath(), "deploysharp-ppocr-trt-cls-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(engineRoot);
            try
            {
                string enginePath = Path.Combine(engineRoot, "ppocrv5-mobile-cls.engine");
                TensorRtOnnxEngineBuildResult build = BuildEngine(classifier, classifierPath, enginePath);
                OrientationRun trt = RunOnTensorRt(classifier, build, cropBatch);
                Assert.AreEqual(2, ort.Result.Items.Count);
                Assert.AreEqual(2, trt.Result.Items.Count);

                var rows = new List<object>(2);
                double maximumOutputAbs = 0;
                float maximumConfidenceDifference = 0;
                for (int index = 0; index < 2; index++)
                {
                    OcrOrientationResult ortRow = ort.Result.Items[index];
                    OcrOrientationResult trtRow = trt.Result.Items[index];
                    Assert.AreEqual(ortRow.ClassIndex, trtRow.ClassIndex, "Orientation class differs at batch row " + index + ".");
                    Assert.AreEqual(ortRow.Orientation, trtRow.Orientation, "Orientation angle differs at batch row " + index + ".");
                    Assert.AreEqual(ortRow.Rejected, trtRow.Rejected, "Orientation rejection differs at batch row " + index + ".");
                    Assert.AreEqual(ortRow.Scores.Count, trtRow.Scores.Count);
                    double rowMaximum = 0;
                    for (int score = 0; score < ortRow.Scores.Count; score++) rowMaximum = Math.Max(rowMaximum, Math.Abs(ortRow.Scores[score] - trtRow.Scores[score]));
                    maximumOutputAbs = Math.Max(maximumOutputAbs, rowMaximum);
                    maximumConfidenceDifference = Math.Max(maximumConfidenceDifference, Math.Abs(ortRow.Confidence - trtRow.Confidence));
                    Console.WriteLine("PADDLEOCR_TRT_CLS_PARITY_ROW row=" + index + ";ortClass=" + ortRow.ClassIndex + ";trtClass=" + trtRow.ClassIndex + ";ortOrientation=" + ortRow.Orientation + ";trtOrientation=" + trtRow.Orientation + ";rejected=" + ortRow.Rejected + ";maxScoreDiff=" + rowMaximum.ToString("R", CultureInfo.InvariantCulture));
                    rows.Add(new
                    {
                        row = index,
                        ortClassIndex = ortRow.ClassIndex,
                        trtClassIndex = trtRow.ClassIndex,
                        ortOrientation = ortRow.Orientation.ToString(),
                        trtOrientation = trtRow.Orientation.ToString(),
                        ortRejected = ortRow.Rejected,
                        trtRejected = trtRow.Rejected,
                        ortConfidence = ortRow.Confidence,
                        trtConfidence = trtRow.Confidence,
                        maximumScoreDifference = rowMaximum,
                        inputSha256 = RowSha256(cropBatch, index)
                    });
                }
                Assert.IsTrue(maximumOutputAbs <= .01, "TensorRT CLS score drift exceeded 0.01: " + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture));
                Assert.IsTrue(maximumConfidenceDifference <= .01f, "TensorRT CLS confidence drift exceeded 0.01: " + maximumConfidenceDifference.ToString("R", CultureInfo.InvariantCulture));
                Console.WriteLine("PADDLEOCR_TRT_CLS_PARITY model=paddleocr/ppocrv5/mobile-cls;batch=2;rows=2;inputSha=" + Sha256Tensor(cropBatch) + ";onnxSha=" + ClassifierSha + ";engineSha=" + build.EngineSha256 + ";engineBytes=" + build.EngineBytes.ToString(CultureInfo.InvariantCulture) + ";maximumOutputAbs=" + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture));
                WriteReport(imagePath, cropBatch, build, ort, trt, rows, maximumOutputAbs, maximumConfidenceDifference);
            }
            finally
            {
                try { Directory.Delete(engineRoot, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static TextDetectionResult RunDetector(PaddleOcrProfile profile, string path, PreparedVisualInput input)
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), registry, request, VisualTaskId.TextDetection), request);
            return (TextDetectionResult)pipeline.Run(input).Value;
        }

        private static OrientationRun RunOnOrt(PaddleOcrProfile profile, string path, PreparedVisualInput input)
        {
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), request, SessionOptions.Default);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
            ITensor raw = outputs.GetRequired("fetch_name_0");
            var context = new VisualDecodeContext(input, profile.VisualProfile, outputs, default);
            return new OrientationRun((OcrOrientationBatchResult)profile.VisualProfile.Decoder.Decode(context), raw.Shape.ToArray(), Sha256Tensor(raw), MaximumAbs(raw), MeanAbs(raw));
        }

        private static TensorRtOnnxEngineBuildResult BuildEngine(PaddleOcrProfile profile, string path, string enginePath)
        {
            var minimum = new TensorShape(1, 3, 80, 160);
            var optimum = new TensorShape(2, 3, 80, 160);
            var options = new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt11, precision: TensorRtOnnxEnginePrecision.RuntimeDefault, workspaceBytes: 268435456UL, optimizationLevel: 0, overwrite: true,
                inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, minimum, optimum, optimum) });
            return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, options);
        }

        private static OrientationRun RunOnTensorRt(PaddleOcrProfile profile, TensorRtOnnxEngineBuildResult build, PreparedVisualInput input)
        {
            var request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11, cudaTargetArchitecture: "sm_86"));
            using IInferenceSession session = provider.CreateSession(new ModelArtifact(profile.VisualProfile.ModelId, "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId), request, SessionOptions.Default);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
            ITensor raw = outputs.GetRequired("fetch_name_0");
            var context = new VisualDecodeContext(input, profile.VisualProfile, outputs, default);
            return new OrientationRun((OcrOrientationBatchResult)profile.VisualProfile.Decoder.Decode(context), raw.Shape.ToArray(), Sha256Tensor(raw), MaximumAbs(raw), MeanAbs(raw));
        }

        private void WriteReport(string imagePath, PreparedVisualInput input, TensorRtOnnxEngineBuildResult build, OrientationRun ort, OrientationRun trt, IReadOnlyList<object> rows, double maximumOutputAbs, float maximumConfidenceDifference)
        {
            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_CLS_PARITY_REPORT_PATH") ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddleocr-v5-mobile-cls-tensorrt-dynamic-batch-parity.json");
            string? directory = Path.GetDirectoryName(Path.GetFullPath(report)); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                model = "paddleocr/ppocrv5/mobile-cls",
                input = new { image = imagePath, imageSha256 = Sha256File(imagePath), tensorSha256 = Sha256Tensor(input), shape = input.Tensor.Shape.ToArray(), batch = input.BatchSize },
                detector = new { model = "paddleocr/ppocrv6/small-det", modelSha256 = DetectorSha },
                classifier = new { onnxSha256 = ClassifierSha, engineSha256 = build.EngineSha256, engineBytes = build.EngineBytes },
                backends = new { ortCpu = new { outputShape = ort.OutputShape, outputSha256 = ort.OutputSha256, maximumAbs = ort.MaximumAbs, meanAbs = ort.MeanAbs }, tensorRtCuda = new { outputShape = trt.OutputShape, outputSha256 = trt.OutputSha256, maximumAbs = trt.MaximumAbs, meanAbs = trt.MeanAbs } },
                rows,
                maximumOutputAbs,
                maximumConfidenceDifference,
                contract = "Batch order, class index, orientation angle, rejection decision, and normalized score drift must match within the stated tolerance.",
                boundary = "Two detector-derived crops on one Windows host; not an accuracy dataset, throughput benchmark, or all-model TensorRT claim."
            }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            TestContext.AddResultFile(report);
        }

        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor)
        {
            PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
            return new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "paddleocr-orientation-argmax-v1");
        }

        private static VisualSize ProbeSourceSize(string path)
        {
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(path, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            return probe.SourceSize;
        }

        private static string RowSha256(PreparedVisualInput input, int row)
        {
            var tensor = (Tensor<float>)input.Tensor; int rowLength = checked((int)(tensor.Length / input.BatchSize)); float[] values = tensor.ToArray(); byte[] bytes = new byte[rowLength * sizeof(float)]; Buffer.BlockCopy(values, row * rowLength, bytes, 0, bytes.Length); using SHA256 sha = SHA256.Create(); return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static string Sha256Tensor(PreparedVisualInput input) => Sha256Tensor(input.Tensor);
        private static string Sha256Tensor(ITensor tensor)
        {
            if (!(tensor is Tensor<float> floatTensor)) throw new AssertFailedException("The parity test only supports Float32 tensors.");
            float[] values = floatTensor.ToArray(); byte[] bytes = new byte[checked(values.Length * sizeof(float))]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length); using SHA256 sha = SHA256.Create(); return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static double MaximumAbs(ITensor tensor) { double value = 0; ForEachValue(tensor, current => value = Math.Max(value, Math.Abs(current))); return value; }
        private static double MeanAbs(ITensor tensor) { double sum = 0; long count = 0; ForEachValue(tensor, current => { sum += Math.Abs(current); count++; }); return count == 0 ? 0 : sum / count; }
        private static void ForEachValue(ITensor tensor, Action<double> visitor)
        {
            if (tensor is Tensor<float> floats) { foreach (float value in floats.ToArray()) visitor(value); return; }
            if (tensor is Tensor<double> doubles) { foreach (double value in doubles.ToArray()) visitor(value); return; }
            throw new AssertFailedException("The parity test only supports Float32/Float64 raw outputs; actual=" + tensor.ElementType);
        }
        private static string Sha256File(string path) { using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path); return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant(); }
        private static string RequireFile(string path) { if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR file does not exist: " + path); return path; }
        private static void RequireExternal() { if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_PARITY_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_TRT_PARITY_RUN_EXTERNAL=1 to run the authorized local TensorRT OCR parity test."); }

        private sealed record OrientationRun(OcrOrientationBatchResult Result, long[] OutputShape, string OutputSha256, double MaximumAbs, double MeanAbs);
    }
}
