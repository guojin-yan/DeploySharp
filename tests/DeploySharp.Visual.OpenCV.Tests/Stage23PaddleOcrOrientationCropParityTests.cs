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

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage23PaddleOcrOrientationCropParityTests
    {
        private const string DatasetRoot = @"F:\OCRBenchmarkTesting";
        private const string ModelRoot = @"E:\Model\paddleocr\PP-OCRv5";
        private const string ClassifierSha = "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2";
        private const int MaximumCropsPerPage = 4;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void SroieOrientationCropsMatchAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string datasetRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_DATASET_ROOT") ?? DatasetRoot;
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_MODEL_ROOT") ?? ModelRoot;
            string manifestA = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_MANIFEST_A") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072455614249Z.jsonl");
            string manifestB = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_MANIFEST_B") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072550009854Z.jsonl");
            string classifierModel = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_CLS_MODEL") ?? Path.Combine(modelRoot, "PP-OCRv5_mobile_cls.onnx"));
            List<CropPage> pages = LoadPages(new[] { manifestA, manifestB }, datasetRoot);
            Assert.AreEqual(10, pages.Count, "The pinned SROIE smoke selection must contain ten pages.");
            Assert.IsTrue(pages.All(page => page.Crops.Count == MaximumCropsPerPage));

            PaddleOcrProfile profile = PaddleOcrProfiles.CreateTextLineOrientationClassification(
                new ModelId("external/stage23-sroie-orientation"),
                Artifact(7, ClassifierSha, "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1"));

            BackendRun ort = RunBackend(profile, classifierModel, pages, openVino: false);
            BackendRun openVino = RunBackend(profile, classifierModel, pages, openVino: true);
            Assert.AreEqual(ort.Crops.Count, openVino.Crops.Count);
            Assert.AreEqual(40, ort.Crops.Count);

            int inputMismatches = 0;
            int classMismatches = 0;
            int rejectionMismatches = 0;
            int orientationMismatches = 0;
            double maximumOutputAbs = 0;
            double maximumOutputMean = 0;
            float maximumConfidenceDelta = 0;
            for (int index = 0; index < ort.Crops.Count; index++)
            {
                CropRun left = ort.Crops[index];
                CropRun right = openVino.Crops[index];
                Assert.AreEqual(left.Id, right.Id);
                if (!string.Equals(left.InputSha, right.InputSha, StringComparison.Ordinal)) inputMismatches++;
                if (left.ClassIndex != right.ClassIndex) classMismatches++;
                if (left.Rejected != right.Rejected) rejectionMismatches++;
                if (!string.Equals(left.Orientation, right.Orientation, StringComparison.Ordinal)) orientationMismatches++;
                float confidenceDelta = Math.Abs(left.Confidence - right.Confidence);
                if (confidenceDelta > maximumConfidenceDelta) maximumConfidenceDelta = confidenceDelta;
                Assert.AreEqual(left.OutputValues.Length, right.OutputValues.Length, "Orientation output element count differs for " + left.Id + ".");
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

            Assert.AreEqual(0, inputMismatches, "The same OpenCV crop must produce the same CLS tensor on both backends.");
            Assert.AreEqual(0, classMismatches, "Orientation classes must match across ORT and OpenVINO.");
            Assert.AreEqual(0, rejectionMismatches, "Orientation rejection decisions must match across ORT and OpenVINO.");
            Assert.AreEqual(0, orientationMismatches, "Accepted orientations must match across ORT and OpenVINO.");
            Assert.IsTrue(maximumOutputAbs <= .01, "The selected CPU backend CLS drift exceeded 0.01: " + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture));
            Assert.IsTrue(maximumConfidenceDelta <= .001f, "The selected CPU backend CLS confidence drift exceeded 0.001: " + maximumConfidenceDelta.ToString("R", CultureInfo.InvariantCulture));

            string? evidencePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(evidencePath)) WriteEvidence(evidencePath, pages, ort, openVino, manifestA, manifestB, inputMismatches, classMismatches, rejectionMismatches, orientationMismatches, maximumOutputAbs, maximumOutputMean, maximumConfidenceDelta);

            Console.WriteLine("STAGE23_PADDLEOCR_CLS_PARITY pages=" + pages.Count.ToString(CultureInfo.InvariantCulture)
                + ";crops=" + ort.Crops.Count.ToString(CultureInfo.InvariantCulture)
                + ";inputMismatches=" + inputMismatches.ToString(CultureInfo.InvariantCulture)
                + ";classMismatches=" + classMismatches.ToString(CultureInfo.InvariantCulture)
                + ";rejectionMismatches=" + rejectionMismatches.ToString(CultureInfo.InvariantCulture)
                + ";orientationMismatches=" + orientationMismatches.ToString(CultureInfo.InvariantCulture)
                + ";maximumOutputAbs=" + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture)
                + ";maximumConfidenceDelta=" + maximumConfidenceDelta.ToString("R", CultureInfo.InvariantCulture)
                + ";ortInputDigest=" + ort.InputDigest
                + ";openVinoInputDigest=" + openVino.InputDigest);
        }

        private static BackendRun RunBackend(PaddleOcrProfile profile, string modelPath, IReadOnlyList<CropPage> pages, bool openVino)
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
            var crops = new List<CropRun>();
            string inputSha = string.Empty;
            foreach (CropPage page in pages)
            {
                using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(page.ImagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
                foreach (TextRegion region in page.Crops)
                {
                    var cropRequest = new TextCropRequest(region, profile.CropProfile ?? throw new InvalidOperationException("The orientation crop profile is missing."));
                    using PreparedVisualInput prepared = input.PrepareRecognitionBatch(profile.VisualProfile.Input.Name, new[] { cropRequest }, CancellationToken.None);
                    string cropInputSha = TensorSha(prepared.Tensor);
                    if (inputSha.Length == 0) inputSha = cropInputSha;
                    InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, prepared.Tensor), CancellationToken.None);
                    ITensor output = outputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
                    float[] values = output.Buffer as float[] ?? throw new InvalidOperationException("The PaddleOCR orientation output must be Float32.");
                    OcrOrientationResult orientation = (OcrOrientationResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(prepared, profile.VisualProfile, outputs, CancellationToken.None));
                    crops.Add(new CropRun(page.ImageId + "/" + region.SourceIndex, cropInputSha, values.ToArray(), TensorSha(output), orientation.ClassIndex, orientation.Rejected, orientation.Orientation.ToString(), orientation.Confidence));
                }
            }
            return new BackendRun(CropDigest(crops), crops);
        }

        private static void WriteEvidence(string path, IReadOnlyList<CropPage> pages, BackendRun ort, BackendRun openVino, string manifestA, string manifestB, int inputMismatches, int classMismatches, int rejectionMismatches, int orientationMismatches, double maximumOutputAbs, double maximumOutputMean, float maximumConfidenceDelta)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var evidence = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                model = new { id = "paddleocr/ppocrv5/mobile-cls", sha256 = ClassifierSha },
                source = new
                {
                    dataset = "jsdnrs/ICDAR2019-SROIE",
                    revision = "bffe40c26759f3376ec2b3ae9031dbba54cd587c",
                    pageCount = pages.Count,
                    cropCount = ort.Crops.Count,
                    cropsPerPage = MaximumCropsPerPage,
                    manifestA,
                    manifestB
                },
                comparison = new
                {
                    leftBackend = "onnxruntime-cpu",
                    rightBackend = "openvino-cpu",
                    inputMismatches,
                    classMismatches,
                    rejectionMismatches,
                    orientationMismatches,
                    maximumOutputAbs,
                    maximumOutputMean,
                    maximumConfidenceDelta,
                    ortInputDigest = ort.InputDigest,
                    openVinoInputDigest = openVino.InputDigest,
                    ortOutputDigest = OutputDigest(ort.Crops),
                    openVinoOutputDigest = OutputDigest(openVino.Crops)
                },
                crops = ort.Crops.Select((crop, index) => new
                {
                    crop.Id,
                    inputSha256 = crop.InputSha,
                    ortOutputSha256 = crop.OutputSha,
                    openVinoOutputSha256 = openVino.Crops[index].OutputSha,
                    classIndex = crop.ClassIndex,
                    orientation = crop.Orientation,
                    rejected = crop.Rejected,
                    ortConfidence = crop.Confidence,
                    openVinoConfidence = openVino.Crops[index].Confidence
                }).ToArray()
            };
            File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static List<CropPage> LoadPages(IEnumerable<string> manifests, string datasetRoot)
        {
            var pages = new List<CropPage>();
            foreach (string manifest in manifests)
            {
                foreach (string line in File.ReadLines(RequireFile(manifest)))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement root = document.RootElement;
                    string imageId = root.GetProperty("image_id").GetString() ?? throw new InvalidDataException("Manifest image_id is missing.");
                    string relative = root.GetProperty("image_relpath").GetString() ?? throw new InvalidDataException("Manifest image_relpath is missing.");
                    var regions = new List<TextRegion>();
                    foreach (JsonElement instance in root.GetProperty("instances").EnumerateArray().Take(MaximumCropsPerPage))
                    {
                        JsonElement polygon = instance.GetProperty("polygon");
                        if (polygon.GetArrayLength() != 4) throw new InvalidDataException("SROIE crop parity requires four-sided regions.");
                        var points = new PointF[4];
                        for (int index = 0; index < points.Length; index++)
                        {
                            JsonElement point = polygon[index];
                            points[index] = new PointF(point[0].GetSingle(), point[1].GetSingle());
                        }
                        var quad = new TextQuadrilateral(points[0], points[1], points[2], points[3], TextCornerOrder.TopLeftClockwise);
                        regions.Add(new TextRegion(regions.Count, 1f, quad.Polygon, quad));
                    }
                    if (regions.Count == MaximumCropsPerPage) pages.Add(new CropPage(imageId, RequireFile(Path.Combine(datasetRoot, relative.Replace('/', Path.DirectorySeparatorChar))), regions));
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

        private static string CropDigest(IEnumerable<CropRun> crops)
        {
            string text = string.Join("\n", crops.Select(crop => crop.Id + "\t" + crop.InputSha));
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string OutputDigest(IEnumerable<CropRun> crops)
        {
            string text = string.Join("\n", crops.Select(crop => crop.Id + "\t" + crop.OutputSha));
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured stage-23 validation file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE23_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE23_RUN_EXTERNAL=1 to run the authorized local SROIE orientation parity test.");
        }

        private sealed record CropPage(string ImageId, string ImagePath, IReadOnlyList<TextRegion> Crops);
        private sealed record CropRun(string Id, string InputSha, float[] OutputValues, string OutputSha, int ClassIndex, bool Rejected, string Orientation, float Confidence);
        private sealed record BackendRun(string InputDigest, IReadOnlyList<CropRun> Crops);
    }
}
