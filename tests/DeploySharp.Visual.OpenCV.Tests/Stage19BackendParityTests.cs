using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage19BackendParityTests
    {
        private const string ImagePath = @"E:\Data\image\bus.jpg";
        private const string DictionaryPath = @"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt";
        private const string DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OrtAndOpenVinoMatchOcrAnomalyAndAlphaFields()
        {
            RequireExternal();
            string image = RequireFile(ImagePath);
            string inputSha = FileSha256(image);

            OcrResult ortOcr = Timed("ort-ocr", () => RunOcr(false, image));
            OcrResult openVinoOcr = Timed("openvino-ocr", () => RunOcr(true, image));
            AssertOcrParity(ortOcr, openVinoOcr, .001f, .25f);

            AnomalyDetectionResult ortAnomaly = Timed("ort-anomaly", () => RunAnomaly(false, image));
            AnomalyDetectionResult openVinoAnomaly = Timed("openvino-anomaly", () => RunAnomaly(true, image));
            float anomalyMaxError = MaxError(ortAnomaly.NormalizedMap.ToArray(), openVinoAnomaly.NormalizedMap.ToArray());
            Assert.AreEqual(ortAnomaly.ImageScore, openVinoAnomaly.ImageScore, .0001f);
            Assert.IsTrue(anomalyMaxError <= .001f, "Anomaly map max error was " + anomalyMaxError.ToString("R", CultureInfo.InvariantCulture));
            CollectionAssert.AreEqual(ortAnomaly.Mask.ToArray(), openVinoAnomaly.Mask.ToArray());

            BackgroundRemovalResult ortAlpha = Timed("ort-alpha", () => RunAlpha(false, image));
            BackgroundRemovalResult openVinoAlpha = Timed("openvino-alpha", () => RunAlpha(true, image));
            float alphaMaxError = MaxError(ortAlpha.Alpha.ToArray(), openVinoAlpha.Alpha.ToArray());
            Assert.IsTrue(alphaMaxError <= .001f, "Alpha max error was " + alphaMaxError.ToString("R", CultureInfo.InvariantCulture));

            Console.WriteLine(
                "STAGE19_PARITY inputSha=" + inputSha +
                ";ortOcrSha=" + ortOcr.ComputeSha256() +
                ";openvinoOcrSha=" + openVinoOcr.ComputeSha256() +
                ";ortAnomalySha=" + ortAnomaly.ComputeSha256() +
                ";openvinoAnomalySha=" + openVinoAnomaly.ComputeSha256() +
                ";anomalyMaxError=" + anomalyMaxError.ToString("R", CultureInfo.InvariantCulture) +
                ";ortAlphaSha=" + ortAlpha.Alpha.ComputeSha256() +
                ";openvinoAlphaSha=" + openVinoAlpha.Alpha.ComputeSha256() +
                ";alphaMaxError=" + alphaMaxError.ToString("R", CultureInfo.InvariantCulture));
        }

        private static OcrResult RunOcr(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(
                new ModelId("external/parity-ppocrv5-det"),
                PaddleArtifact(11, "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039"));
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(RequireFile(DictionaryPath), "external.ppocrv5", "v5", true, DictionarySha);
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(
                new ModelId("external/parity-ppocrv5-rec"),
                PaddleArtifact(7, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", DictionarySha),
                characters);

            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(
                imagePath,
                "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
            {
                sourceSize = probe.SourceSize;
            }

            using var backends = CreateBackends(openVino);
            var profiles = new VisualProfileRegistry();
            profiles.Register(detector.VisualProfile);
            profiles.Register(recognizer.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using var pipeline = new OcrPipeline(
                backends,
                profiles.Select(detector.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx"), backendId), backends, request, VisualTaskId.TextDetection),
                request,
                profiles.Select(recognizer.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx"), backendId), backends, request, VisualTaskId.TextRecognition),
                request,
                recognizer.CropProfile ?? throw new InvalidOperationException("The recognition crop profile is missing."));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(
                imagePath,
                detector.VisualProfile.Input.Name,
                OpenCvStage19Preprocessing.CreatePaddleOcrDetectionOptions(sourceSize));
            OcrResult result = pipeline.Run(input);
            Assert.IsTrue(result.Regions.Count > 0);
            return result;
        }

        private static AnomalyDetectionResult RunAnomaly(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            AnomalibProfile profile = AnomalibProfiles.CreatePadim(
                new ModelId("external/parity-anomalib-padim"),
                new AnomalibArtifactContract(14, "bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff", "ffde4cce3db38964f9cf627b524dd325401c6107", "pytorch-2.7.1-opset14"));
            using var backends = CreateBackends(openVino);
            var profiles = Registry(profile.VisualProfile);
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using var pipeline = new AnomalyPipeline(backends, profiles.Select(profile.CreateArtifact(RequireFile(@"E:\Model\anomalib\Padim\model\padim.onnx"), backendId), backends, request, VisualTaskId.AnomalyDetection), request);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreateAnomalibOptions(profile));
            return pipeline.Run(input);
        }

        private static BackgroundRemovalResult RunAlpha(bool openVino, string imagePath)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg14(
                new ModelId("external/parity-rmbg-1.4"),
                new BriaRmbgProfileOptions(11, new VisualSize(1024, 1024), "input", "output", "8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f", "2ceba5a5efaec153162aedea169f76caf9b46cf8", "pytorch-2.1.0-opset11", "LicenseRef-BRIA-RMBG-1.4"));
            using var backends = CreateBackends(openVino);
            var profiles = Registry(profile.VisualProfile);
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using var pipeline = new VisualPipeline(backends, profiles.Select(profile.CreateArtifact(RequireFile(@"E:\Model\RMBG\bria-rmbg-1.4.onnx"), backendId), backends, request, VisualTaskId.ForegroundMatting), request);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvStage19Preprocessing.CreateBriaRmbgOptions(profile));
            return pipeline.Run(input).GetValue<BackgroundRemovalResult>();
        }

        private static BackendRegistry CreateBackends(bool openVino)
        {
            var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino();
            else backends.UseOnnxRuntime();
            return backends;
        }

        private static VisualProfileRegistry Registry(VisualModelProfile profile)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            return profiles;
        }

        private static void AssertOcrParity(OcrResult expected, OcrResult actual, float scoreTolerance, float coordinateTolerance)
        {
            Assert.AreEqual(expected.Regions.Count, actual.Regions.Count);
            for (int regionIndex = 0; regionIndex < expected.Regions.Count; regionIndex++)
            {
                OcrRegionResult left = expected.Regions[regionIndex];
                OcrRegionResult right = actual.Regions[regionIndex];
                Assert.AreEqual(left.Region.SourceIndex, right.Region.SourceIndex);
                Assert.AreEqual(left.Region.Score, right.Region.Score, scoreTolerance);
                Assert.AreEqual(left.Recognition.Text, right.Recognition.Text);
                Assert.AreEqual(left.Recognition.Confidence, right.Recognition.Confidence, scoreTolerance);
                Assert.AreEqual(left.Region.Polygon.Vertices.Count, right.Region.Polygon.Vertices.Count);
                for (int vertexIndex = 0; vertexIndex < left.Region.Polygon.Vertices.Count; vertexIndex++)
                {
                    PointF leftPoint = left.Region.Polygon.Vertices[vertexIndex];
                    PointF rightPoint = right.Region.Polygon.Vertices[vertexIndex];
                    Assert.AreEqual(leftPoint.X, rightPoint.X, coordinateTolerance);
                    Assert.AreEqual(leftPoint.Y, rightPoint.Y, coordinateTolerance);
                }
                Assert.AreEqual(left.Recognition.Tokens.Count, right.Recognition.Tokens.Count);
                for (int tokenIndex = 0; tokenIndex < left.Recognition.Tokens.Count; tokenIndex++)
                {
                    Assert.AreEqual(left.Recognition.Tokens[tokenIndex].ClassIndex, right.Recognition.Tokens[tokenIndex].ClassIndex);
                    Assert.AreEqual(left.Recognition.Tokens[tokenIndex].Text, right.Recognition.Tokens[tokenIndex].Text);
                    Assert.AreEqual(left.Recognition.Tokens[tokenIndex].Confidence, right.Recognition.Tokens[tokenIndex].Confidence, scoreTolerance);
                }
            }
        }

        private static float MaxError(float[] left, float[] right)
        {
            Assert.AreEqual(left.Length, right.Length);
            float maximum = 0f;
            for (int index = 0; index < left.Length; index++) maximum = Math.Max(maximum, Math.Abs(left[index] - right[index]));
            return maximum;
        }

        private static T Timed<T>(string name, Func<T> action)
        {
            var watch = Stopwatch.StartNew();
            T value = action();
            watch.Stop();
            Console.WriteLine("STAGE19_PARITY_TIMING path=" + name + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            return value;
        }

        private static PaddleOcrArtifactContract PaddleArtifact(int opset, string modelSha, string? dictionarySha = null)
        {
            return new PaddleOcrArtifactContract(opset, modelSha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0", "stage19-preprocess-v1", "stage19-postprocess-v1", dictionarySha256: dictionarySha, dictionaryLicense: "official-repository-file-separate-review-required");
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE19_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE19_RUN_EXTERNAL=1 to run the authorized local stage-19 parity matrix.");
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured local validation file does not exist: " + path);
            return path;
        }

        private static string FileSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(stream);
            var chars = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                chars[index * 2] = alphabet[bytes[index] >> 4];
                chars[(index * 2) + 1] = alphabet[bytes[index] & 15];
            }
            return new string(chars);
        }
    }
}
