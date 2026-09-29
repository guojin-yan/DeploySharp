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
    public sealed class Stage21PaddleOcrRecognitionCropParityTests
    {
        private const string DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";
        private const string DetectorSha = "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039";
        private const string RecognitionSha = "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9";
        private const string DatasetRoot = @"F:\OCRBenchmarkTesting";
        private const string ModelRoot = @"E:\Model\paddleocr\PP-OCRv5";
        private const int MaximumCropsPerPage = 4;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void SroieRecognitionCropsMatchAcrossOrtAndOpenVino()
        {
            RequireExternal();
            string datasetRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_DATASET_ROOT") ?? DatasetRoot;
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_MODEL_ROOT") ?? ModelRoot;
            string manifestA = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_MANIFEST_A") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072455614249Z.jsonl");
            string manifestB = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_MANIFEST_B") ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072550009854Z.jsonl");
            string recognitionModel = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_REC_MODEL") ?? Path.Combine(modelRoot, "PP-OCRv5_mobile_rec.onnx"));
            string dictionary = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_DICT") ?? Path.Combine(modelRoot, "ppocrv5_dict.txt"));

            List<CropPage> pages = LoadPages(new[] { manifestA, manifestB }, datasetRoot);
            Assert.AreEqual(10, pages.Count, "The pinned SROIE smoke selection must contain ten pages.");
            Assert.IsTrue(pages.All(page => page.Crops.Count == MaximumCropsPerPage));

            PaddleOcrProfile profile = PaddleOcrProfiles.CreateRecognition(
                new ModelId("external/stage21-sroie-recognizer"),
                Artifact(7, RecognitionSha, "ppocr-rec-bgr-half-range-h48-v1", "ppocr-ctc-probability-greedy-v1", DictionarySha),
                PaddleOcrProfiles.LoadCharacterSet(dictionary, "external.ppocrv5", "v5", true, DictionarySha));

            BackendRun ort = RunBackend(profile, recognitionModel, pages, openVino: false);
            BackendRun openVino = RunBackend(profile, recognitionModel, pages, openVino: true);
            Assert.AreEqual(ort.Crops.Count, openVino.Crops.Count);
            Assert.AreEqual(40, ort.Crops.Count);

            int inputMismatches = 0;
            int textMismatches = 0;
            int shapeMismatches = 0;
            double maximumOutputAbs = 0;
            double maximumOutputMean = 0;
            float maximumConfidenceDelta = 0;
            for (int index = 0; index < ort.Crops.Count; index++)
            {
                CropRun left = ort.Crops[index];
                CropRun right = openVino.Crops[index];
                Assert.AreEqual(left.Id, right.Id);
                if (!string.Equals(left.InputSha, right.InputSha, StringComparison.Ordinal)) inputMismatches++;
                if (left.OutputShape.Count != right.OutputShape.Count || left.OutputShape.Where((value, dimension) => value != right.OutputShape[dimension]).Any()) shapeMismatches++;
                if (!string.Equals(left.Text, right.Text, StringComparison.Ordinal)) textMismatches++;
                float confidenceDelta = Math.Abs(left.Confidence - right.Confidence);
                if (confidenceDelta > maximumConfidenceDelta) maximumConfidenceDelta = confidenceDelta;
                Assert.IsTrue(left.OutputValues.Length == right.OutputValues.Length, "Output element count differs for " + left.Id + ".");
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

            Assert.AreEqual(0, inputMismatches, "The same OpenCV crop must produce the same REC tensor on both backends.");
            Assert.AreEqual(0, shapeMismatches, "REC output shapes must match across ORT and OpenVINO.");
            Assert.AreEqual(0, textMismatches, "CTC decoded text must match across ORT and OpenVINO.");
            Assert.IsTrue(maximumOutputAbs <= .01, "The selected CPU backend output drift exceeded 0.01: " + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture));
            Assert.IsTrue(maximumConfidenceDelta <= .001f, "The selected CPU backend confidence drift exceeded 0.001: " + maximumConfidenceDelta.ToString("R", CultureInfo.InvariantCulture));

            string? evidencePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(evidencePath))
            {
                WriteEvidence(evidencePath, pages, ort, openVino, manifestA, manifestB, inputMismatches, shapeMismatches, textMismatches, maximumOutputAbs, maximumOutputMean, maximumConfidenceDelta);
            }

            Console.WriteLine("STAGE21_PADDLEOCR_CROP_PARITY pages=" + pages.Count.ToString(CultureInfo.InvariantCulture)
                + ";crops=" + ort.Crops.Count.ToString(CultureInfo.InvariantCulture)
                + ";inputMismatches=" + inputMismatches.ToString(CultureInfo.InvariantCulture)
                + ";shapeMismatches=" + shapeMismatches.ToString(CultureInfo.InvariantCulture)
                + ";textMismatches=" + textMismatches.ToString(CultureInfo.InvariantCulture)
                + ";maximumOutputAbs=" + maximumOutputAbs.ToString("R", CultureInfo.InvariantCulture)
                + ";maximumOutputMean=" + maximumOutputMean.ToString("R", CultureInfo.InvariantCulture)
                + ";maximumConfidenceDelta=" + maximumConfidenceDelta.ToString("R", CultureInfo.InvariantCulture)
                + ";ortInputSha=" + ort.InputSha
                + ";openVinoInputSha=" + openVino.InputSha);
        }

        private static BackendRun RunBackend(PaddleOcrProfile profile, string modelPath, IReadOnlyList<CropPage> pages, bool openVino)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            var crops = new List<CropRun>();
            string inputSha = string.Empty;
            foreach (CropPage page in pages)
            {
                using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(page.ImagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
                foreach (TextRegion region in page.Crops)
                {
                    var cropRequest = new TextCropRequest(region, profile.CropProfile ?? throw new InvalidOperationException("The recognition crop profile is missing."));
                    using PreparedVisualInput prepared = input.PrepareRecognitionBatch(profile.VisualProfile.Input.Name, new[] { cropRequest }, CancellationToken.None);
                    string cropInputSha = TensorSha(prepared.Tensor);
                    if (inputSha.Length == 0) inputSha = cropInputSha;
                    InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, prepared.Tensor), CancellationToken.None);
                    ITensor output = outputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
                    float[] values = output.Buffer as float[] ?? throw new InvalidOperationException("The PaddleOCR recognition output must be Float32.");
                    var decoded = (TextRecognitionBatchResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(prepared, profile.VisualProfile, outputs, CancellationToken.None));
                    RecognizedText recognized = decoded.Items[0];
                    crops.Add(new CropRun(page.ImageId + "/" + region.SourceIndex, cropInputSha, output.Shape.ToArray(), values.ToArray(), TensorSha(output), recognized.Text, recognized.Confidence));
                }
            }
            return new BackendRun(inputSha, CropDigest(crops), crops);
        }

        private static void WriteEvidence(string path, IReadOnlyList<CropPage> pages, BackendRun ort, BackendRun openVino, string manifestA, string manifestB, int inputMismatches, int shapeMismatches, int textMismatches, double maximumOutputAbs, double maximumOutputMean, float maximumConfidenceDelta)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var evidence = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                model = new { id = "paddleocr/ppocrv5/mobile-rec", sha256 = RecognitionSha },
                dictionarySha256 = DictionarySha,
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
                    shapeMismatches,
                    textMismatches,
                    maximumOutputAbs,
                    maximumOutputMean,
                    maximumConfidenceDelta,
                    ortInputDigest = ort.InputDigest,
                    openVinoInputDigest = openVino.InputDigest,
                    ortOutputDigest = CropOutputDigest(ort.Crops),
                    openVinoOutputDigest = CropOutputDigest(openVino.Crops)
                },
                crops = ort.Crops.Select((crop, index) => new
                {
                    crop.Id,
                    inputSha256 = crop.InputSha,
                    outputShape = crop.OutputShape,
                    ortOutputSha256 = crop.OutputSha,
                    openVinoOutputSha256 = openVino.Crops[index].OutputSha,
                    text = crop.Text,
                    ortConfidence = crop.Confidence,
                    openVinoConfidence = openVino.Crops[index].Confidence
                }).ToArray()
            };
            File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static string CropDigest(IEnumerable<CropRun> crops)
        {
            string text = string.Join("\n", crops.Select(crop => crop.Id + "\t" + crop.InputSha));
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string CropOutputDigest(IEnumerable<CropRun> crops)
        {
            string text = string.Join("\n", crops.Select(crop => crop.Id + "\t" + crop.OutputSha));
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
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
                    string imagePath = Path.Combine(datasetRoot, relative.Replace('/', Path.DirectorySeparatorChar));
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
                    if (regions.Count == MaximumCropsPerPage) pages.Add(new CropPage(imageId, RequireFile(imagePath), regions));
                }
            }
            return pages;
        }

        private static PaddleOcrArtifactContract Artifact(int opset, string sha, string preprocessing, string postprocessing, string? dictionarySha = null)
            => new PaddleOcrArtifactContract(opset, sha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", preprocessing, postprocessing, dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? "" : "official-repository-file-separate-review-required");

        private static string TensorSha(ITensor tensor)
        {
            if (!(tensor.Buffer is float[] values)) throw new InvalidOperationException("The OCR crop input must be Float32.");
            byte[] bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured stage-21 validation file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE21_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_STAGE21_RUN_EXTERNAL=1 to run the authorized local SROIE crop parity test.");
        }

        private sealed record CropPage(string ImageId, string ImagePath, IReadOnlyList<TextRegion> Crops);
        private sealed record CropRun(string Id, string InputSha, IReadOnlyList<long> OutputShape, float[] OutputValues, string OutputSha, string Text, float Confidence);
        private sealed record BackendRun(string InputSha, string InputDigest, IReadOnlyList<CropRun> Crops);
    }
}
