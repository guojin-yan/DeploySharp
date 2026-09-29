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
    [TestClass]
    public sealed class Stage22PaddleOcrDetectionIntermediateParityTests
    {
        private const string DatasetRoot = @"F:\OCRBenchmarkTesting";
        private const string ModelRoot = @"E:\Model\paddleocr\PP-OCRv5";
        private const string DetectorSha = "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void SroieDetectionOutputsMatchAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string datasetRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE22_DATASET_ROOT") ?? DatasetRoot;
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE22_MODEL_ROOT") ?? ModelRoot;
            string manifestA = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE22_MANIFEST_A") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072455614249Z.jsonl");
            string manifestB = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE22_MANIFEST_B") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072550009854Z.jsonl");
            string detectorModel = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE22_DET_MODEL") ?? Path.Combine(modelRoot, "PP-OCRv5_mobile_det.onnx"));
            List<DetectionPage> pages = LoadPages(new[] { manifestA, manifestB }, datasetRoot);
            Assert.AreEqual(10, pages.Count, "The pinned SROIE smoke selection must contain ten pages.");

            PaddleOcrProfile profile = PaddleOcrProfiles.CreateDetection(
                new ModelId("external/stage22-sroie-detector"),
                Artifact(11, DetectorSha, "ppocr-det-resize-long960-stride128-f32-v2", "ppocr-db-contour-minarea-unclip-v2"));

            BackendRun ort = RunBackend(profile, detectorModel, pages, openVino: false);
            BackendRun openVino = RunBackend(profile, detectorModel, pages, openVino: true);
            Assert.AreEqual(ort.Pages.Count, openVino.Pages.Count);

            int inputMismatches = 0;
            int shapeMismatches = 0;
            double maximumOutputAbs = 0;
            double maximumOutputMean = 0;
            for (int index = 0; index < ort.Pages.Count; index++)
            {
                DetectionPageRun left = ort.Pages[index];
                DetectionPageRun right = openVino.Pages[index];
                Assert.AreEqual(left.Id, right.Id);
                if (!string.Equals(left.InputSha, right.InputSha, StringComparison.Ordinal)) inputMismatches++;
                if (left.OutputShape.Count != right.OutputShape.Count || left.OutputShape.Where((value, dimension) => value != right.OutputShape[dimension]).Any()) shapeMismatches++;
                Assert.AreEqual(left.OutputValues.Length, right.OutputValues.Length, "Detection output element count differs for " + left.Id + ".");
                double sum = 0;
                double max = 0;
                for (int valueIndex = 0; valueIndex < left.OutputValues.Length; valueIndex++)
                {
                    double delta = Math.Abs(left.OutputValues[valueIndex] - right.OutputValues[valueIndex]);
                    sum += delta;
                    if (delta > max) max = delta;
                }
                double mean = left.OutputValues.Length == 0 ? 0 : sum / left.OutputValues.Length;
                if (max > maximumOutputAbs) maximumOutputAbs = max;
                if (mean > maximumOutputMean) maximumOutputMean = mean;
            }

            Assert.AreEqual(0, inputMismatches, "The same OpenCV detector preprocessing must produce the same tensor on both backends.");
            Assert.AreEqual(0, shapeMismatches, "Detector output shapes must match across ORT and OpenVINO.");
            Assert.IsTrue(maximumOutputAbs <= .01, "The selected CPU backend detector drift exceeded 0.01: " + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture));

            string? evidencePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE22_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(evidencePath)) WriteEvidence(evidencePath, pages, ort, openVino, manifestA, manifestB, inputMismatches, shapeMismatches, maximumOutputAbs, maximumOutputMean);

            Console.WriteLine("STAGE22_PADDLEOCR_DET_PARITY pages=" + pages.Count.ToString(CultureInfo.InvariantCulture)
                + ";inputMismatches=" + inputMismatches.ToString(CultureInfo.InvariantCulture)
                + ";shapeMismatches=" + shapeMismatches.ToString(CultureInfo.InvariantCulture)
                + ";maximumOutputAbs=" + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture)
                + ";maximumOutputMean=" + maximumOutputMean.ToString("R", CultureInfo.InvariantCulture)
                + ";ortInputDigest=" + ort.InputDigest
                + ";openVinoInputDigest=" + openVino.InputDigest);
        }

        private static BackendRun RunBackend(PaddleOcrProfile profile, string modelPath, IReadOnlyList<DetectionPage> pages, bool openVino)
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
            var runs = new List<DetectionPageRun>();
            foreach (DetectionPage page in pages)
            {
                VisualSize sourceSize;
                using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(page.ImagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr))) sourceSize = probe.SourceSize;
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(page.ImagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
                string inputSha = TensorSha(input.Tensor);
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
                ITensor output = outputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
                float[] values = output.Buffer as float[] ?? throw new InvalidOperationException("The PaddleOCR detection output must be Float32.");
                runs.Add(new DetectionPageRun(page.ImageId, inputSha, output.Shape.ToArray(), values.ToArray(), TensorSha(output)));
            }
            return new BackendRun(CropDigest(runs), runs);
        }

        private static void WriteEvidence(string path, IReadOnlyList<DetectionPage> pages, BackendRun ort, BackendRun openVino, string manifestA, string manifestB, int inputMismatches, int shapeMismatches, double maximumOutputAbs, double maximumOutputMean)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var evidence = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                model = new { id = "paddleocr/ppocrv5/mobile-det", sha256 = DetectorSha },
                source = new
                {
                    dataset = "jsdnrs/ICDAR2019-SROIE",
                    revision = "bffe40c26759f3376ec2b3ae9031dbba54cd587c",
                    pageCount = pages.Count,
                    manifestA,
                    manifestB
                },
                comparison = new
                {
                    leftBackend = "onnxruntime-cpu",
                    rightBackend = "openvino-cpu",
                    inputMismatches,
                    shapeMismatches,
                    maximumOutputAbs,
                    maximumOutputMean,
                    ortInputDigest = ort.InputDigest,
                    openVinoInputDigest = openVino.InputDigest,
                    ortOutputDigest = OutputDigest(ort.Pages),
                    openVinoOutputDigest = OutputDigest(openVino.Pages)
                },
                pages = ort.Pages.Select((page, index) => new
                {
                    page.Id,
                    inputSha256 = page.InputSha,
                    outputShape = page.OutputShape,
                    ortOutputSha256 = page.OutputSha,
                    openVinoOutputSha256 = openVino.Pages[index].OutputSha
                }).ToArray()
            };
            File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static List<DetectionPage> LoadPages(IEnumerable<string> manifests, string datasetRoot)
        {
            var pages = new List<DetectionPage>();
            foreach (string manifest in manifests)
            {
                foreach (string line in File.ReadLines(RequireFile(manifest)))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement root = document.RootElement;
                    string imageId = root.GetProperty("image_id").GetString() ?? throw new InvalidDataException("Manifest image_id is missing.");
                    string relative = root.GetProperty("image_relpath").GetString() ?? throw new InvalidDataException("Manifest image_relpath is missing.");
                    pages.Add(new DetectionPage(imageId, RequireFile(Path.Combine(datasetRoot, relative.Replace('/', Path.DirectorySeparatorChar)))));
                }
            }
            return pages;
        }

        private static PaddleOcrArtifactContract Artifact(int opset, string sha, string preprocessing, string postprocessing)
            => new PaddleOcrArtifactContract(opset, sha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", preprocessing, postprocessing);

        private static string TensorSha(ITensor tensor)
        {
            if (!(tensor.Buffer is float[] values)) throw new InvalidOperationException("The PaddleOCR tensor must be Float32.");
            byte[] bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static string CropDigest(IEnumerable<DetectionPageRun> pages)
        {
            string text = string.Join("\n", pages.Select(page => page.Id + "\t" + page.InputSha));
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string OutputDigest(IEnumerable<DetectionPageRun> pages)
        {
            string text = string.Join("\n", pages.Select(page => page.Id + "\t" + page.OutputSha));
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured stage-22 validation file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE22_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE22_RUN_EXTERNAL=1 to run the authorized local SROIE detector parity test.");
        }

        private sealed record DetectionPage(string ImageId, string ImagePath);
        private sealed record DetectionPageRun(string Id, string InputSha, IReadOnlyList<long> OutputShape, float[] OutputValues, string OutputSha);
        private sealed record BackendRun(string InputDigest, IReadOnlyList<DetectionPageRun> Pages);
    }
}
