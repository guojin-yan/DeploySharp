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
    /// Compares the raw DB detector output for every local PP-OCR v4/v5/v6
    /// detector on the same three images in ORT CPU and OpenVINO CPU.
    /// This is an intermediate tensor contract, not a detection quality score.
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrCoreDetectionIntermediateParityTests
    {
        private const string ImageRootDefault = @"E:\Data\ocr";
        private const string ModelRootDefault = @"E:\Model\paddleocr";
        private const double MaximumAbsDifferenceDefault = 0.01;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AllCoreDetectorsMatchAcrossOrtAndOpenVinoOnThreeImages()
        {
            RequireExternal();
            string imageRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_IMAGE_ROOT") ?? ImageRootDefault;
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault;
            double maximumAbsDifference = ParseDouble(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_MAX_ABS"), MaximumAbsDifferenceDefault);
            string[] images =
            {
                RequireFile(Path.Combine(imageRoot, "demo_1.jpg")),
                RequireFile(Path.Combine(imageRoot, "demo_2.jpg")),
                RequireFile(Path.Combine(imageRoot, "demo_3.jpg"))
            };

            var records = new List<DetectorRecord>();
            foreach (DetectorCase testCase in Cases)
            {
                string modelPath = RequireFile(Path.Combine(modelRoot, testCase.Folder, testCase.FileName));
                PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.ModelId));
                PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, Artifact(descriptor), maximumBatch: 1);
                BackendRun ort = RunBackend(profile, modelPath, images, openVino: false);
                BackendRun openVino = RunBackend(profile, modelPath, images, openVino: true);
                Assert.AreEqual(ort.Images.Count, openVino.Images.Count, testCase.Name + " image count differs.");

                var imageRecords = new List<ImageRecord>();
                double maximumObservedAbs = 0;
                double maximumObservedMean = 0;
                int inputMismatches = 0;
                int shapeMismatches = 0;
                for (int index = 0; index < ort.Images.Count; index++)
                {
                    DetectorImageRun left = ort.Images[index];
                    DetectorImageRun right = openVino.Images[index];
                    Assert.AreEqual(left.ImagePath, right.ImagePath, testCase.Name + " image ordering differs.");
                    if (!string.Equals(left.InputSha256, right.InputSha256, StringComparison.Ordinal)) inputMismatches++;
                    if (!left.OutputShape.SequenceEqual(right.OutputShape)) shapeMismatches++;
                    Assert.AreEqual(left.OutputValues.Length, right.OutputValues.Length, testCase.Name + " output length differs for " + left.ImagePath + ".");

                    double sum = 0;
                    double maximum = 0;
                    for (int valueIndex = 0; valueIndex < left.OutputValues.Length; valueIndex++)
                    {
                        double delta = Math.Abs(left.OutputValues[valueIndex] - right.OutputValues[valueIndex]);
                        sum += delta;
                        if (delta > maximum) maximum = delta;
                    }
                    double mean = left.OutputValues.Length == 0 ? 0 : sum / left.OutputValues.Length;
                    maximumObservedAbs = Math.Max(maximumObservedAbs, maximum);
                    maximumObservedMean = Math.Max(maximumObservedMean, mean);
                    imageRecords.Add(new ImageRecord(
                        Path.GetFileName(left.ImagePath),
                        left.InputSha256,
                        left.OutputShape,
                        left.OutputSha256,
                        right.OutputSha256,
                        maximum,
                        mean));
                }

                Assert.AreEqual(0, inputMismatches, testCase.Name + " preprocessing input differs across backends.");
                Assert.AreEqual(0, shapeMismatches, testCase.Name + " output shape differs across backends.");
                Assert.IsTrue(maximumObservedAbs <= maximumAbsDifference,
                    testCase.Name + " raw detector drift exceeded " + maximumAbsDifference.ToString("R", CultureInfo.InvariantCulture) + ": " + maximumObservedAbs.ToString("R", CultureInfo.InvariantCulture));
                records.Add(new DetectorRecord(
                    testCase.Name,
                    testCase.ModelId,
                    modelPath,
                    Sha256File(modelPath),
                    maximumAbsDifference,
                    inputMismatches,
                    shapeMismatches,
                    maximumObservedAbs,
                    maximumObservedMean,
                    imageRecords));

                Console.WriteLine("PADDLEOCR_DET_PARITY variant=" + testCase.Name
                    + ";images=" + imageRecords.Count.ToString(CultureInfo.InvariantCulture)
                    + ";inputMismatches=" + inputMismatches.ToString(CultureInfo.InvariantCulture)
                    + ";shapeMismatches=" + shapeMismatches.ToString(CultureInfo.InvariantCulture)
                    + ";maximumAbs=" + maximumObservedAbs.ToString("R", CultureInfo.InvariantCulture)
                    + ";maximumMean=" + maximumObservedMean.ToString("R", CultureInfo.InvariantCulture));
            }

            string? evidencePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(evidencePath)) WriteEvidence(evidencePath!, images, modelRoot, maximumAbsDifference, records);
        }

        private static readonly DetectorCase[] Cases =
        {
            new DetectorCase("v4-mobile", "paddleocr/ppocrv4/mobile-det", "PP-OCRv4", "PP-OCRv4_mobile_det.onnx"),
            new DetectorCase("v4-server", "paddleocr/ppocrv4/server-det", "PP-OCRv4", "PP-OCRv4_server_det.onnx"),
            new DetectorCase("v5-mobile", "paddleocr/ppocrv5/mobile-det", "PP-OCRv5", "PP-OCRv5_mobile_det.onnx"),
            new DetectorCase("v5-server", "paddleocr/ppocrv5/server-det", "PP-OCRv5", "PP-OCRv5_server_det.onnx"),
            new DetectorCase("v6-tiny", "paddleocr/ppocrv6/tiny-det", Path.Combine("PP-OCRv6", "tiny"), "PP-OCRv6_tiny_det_inference.onnx"),
            new DetectorCase("v6-small", "paddleocr/ppocrv6/small-det", Path.Combine("PP-OCRv6", "small"), "PP-OCRv6_small_det_inference.onnx"),
            new DetectorCase("v6-medium", "paddleocr/ppocrv6/medium-det", Path.Combine("PP-OCRv6", "medium"), "PP-OCRv6_medium_det_inference.onnx")
        };

        private static BackendRun RunBackend(PaddleOcrProfile profile, string modelPath, IReadOnlyList<string> images, bool openVino)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            var runs = new List<DetectorImageRun>();
            foreach (string imagePath in images)
            {
                VisualSize sourceSize;
                using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
                    sourceSize = probe.SourceSize;
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                    imagePath,
                    profile.VisualProfile.Input.Name,
                    OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
                ITensor output = outputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
                float[] values = output.Buffer as float[] ?? throw new InvalidOperationException("The PaddleOCR detector output must be Float32.");
                runs.Add(new DetectorImageRun(imagePath, TensorSha(input.Tensor), output.Shape.ToArray(), values.ToArray(), TensorSha(output)));
            }
            return new BackendRun(runs);
        }

        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor)
        {
            PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
            return new PaddleOcrArtifactContract(
                release.Opset,
                release.Sha256,
                "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
                "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
                "Apache-2.0;external-artifact-redistribution-unverified",
                "ppocr-official-inference-v1",
                "deploysharp-paddleocr-db-ctc-v1");
        }

        private static void WriteEvidence(string path, IReadOnlyList<string> images, string modelRoot, double maximumAbsDifference, IReadOnlyList<DetectorRecord> records)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var evidence = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceRevision = ResolveSourceRevision(),
                runtime = new { left = "onnxruntime-cpu", right = "openvino-cpu" },
                modelRoot,
                protocol = new
                {
                    images = images.Select(Path.GetFileName).ToArray(),
                    combinations = records.Count * images.Count,
                    maximumAbsDifference,
                    input = "OpenCV input factory + PP-OCR official detection preprocessing",
                    comparison = "raw detector output element-wise maximum/mean absolute difference"
                },
                summary = new
                {
                    detectorCount = records.Count,
                    imageCount = images.Count,
                    combinations = records.Count * images.Count,
                    inputMismatches = records.Sum(record => record.InputMismatches),
                    shapeMismatches = records.Sum(record => record.ShapeMismatches),
                    maximumObservedAbs = records.Count == 0 ? 0 : records.Max(record => record.MaximumObservedAbs),
                    maximumObservedMean = records.Count == 0 ? 0 : records.Max(record => record.MaximumObservedMean),
                    passed = records.All(record => record.InputMismatches == 0 && record.ShapeMismatches == 0 && record.MaximumObservedAbs <= maximumAbsDifference)
                },
                inputs = images.Select(image => new { path = image, sha256 = Sha256File(image) }).ToArray(),
                detectors = records
            };
            File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
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
                return value;
            }
            catch { return "unknown"; }
        }

        private static string TensorSha(ITensor tensor)
        {
            if (!(tensor.Buffer is float[] values)) throw new InvalidOperationException("The PaddleOCR tensor must be Float32.");
            byte[] bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static string Sha256File(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static double ParseDouble(string? value, double fallback)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed > 0 ? parsed : fallback;

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR detector parity file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_DET_PARITY_RUN_EXTERNAL=1 to run the local detector parity matrix.");
        }

        private sealed record DetectorCase(string Name, string ModelId, string Folder, string FileName);
        private sealed record DetectorImageRun(string ImagePath, string InputSha256, IReadOnlyList<long> OutputShape, float[] OutputValues, string OutputSha256);
        private sealed record BackendRun(IReadOnlyList<DetectorImageRun> Images);
        private sealed record ImageRecord(string Image, string InputSha256, IReadOnlyList<long> OutputShape, string OrtOutputSha256, string OpenVinoOutputSha256, double MaximumAbsDifference, double MeanAbsDifference);
        private sealed record DetectorRecord(string Variant, string ModelId, string ModelPath, string ModelSha256, double MaximumAbsDifferenceThreshold, int InputMismatches, int ShapeMismatches, double MaximumObservedAbs, double MaximumObservedMean, IReadOnlyList<ImageRecord> Images);
    }
}
