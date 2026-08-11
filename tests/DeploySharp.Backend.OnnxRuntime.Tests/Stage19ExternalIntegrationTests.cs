using System;
using System.Diagnostics;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    /// <summary>Runs the user-authorized stage-19 local artifacts only when explicitly enabled. / 仅在显式启用时运行用户授权的阶段 19 本地工件。</summary>
    [TestClass]
    public sealed class Stage19ExternalIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void PaddleOcrMobileDetectionAndRecognitionRunOnRealCpuOrt()
        {
            RequireExternal();
            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(new ModelId("external/ppocrv5-mobile-det"), PaddleArtifact(11, "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039"));
            VisualInferenceResult detection = Run(detector.VisualProfile, detector.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("x", 32, 32));
            Assert.IsInstanceOfType<TextDetectionResult>(detection.Value);

            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(RequireFile(@"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt"), "external.ppocrv5", "v5", true, "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b");
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(new ModelId("external/ppocrv5-mobile-rec"), PaddleArtifact(7, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b"), characters);
            VisualInferenceResult recognition = Run(recognizer.VisualProfile, recognizer.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("x", 320, 48));
            Assert.IsInstanceOfType<TextRecognitionBatchResult>(recognition.Value);
            Assert.AreEqual(1, ((TextRecognitionBatchResult)recognition.Value).Items.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void PaddleOcrServerDetectionAndRecognitionRunOnRealCpuOrt()
        {
            RequireExternal();
            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(new ModelId("external/ppocrv5-server-det"), PaddleArtifact(11, "9a910baffbefb807ff2f7bfaa72910e3e470bd17014d798386d87bb46f442839"));
            Assert.IsInstanceOfType<TextDetectionResult>(Run(detector.VisualProfile, detector.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_server_det_onnx.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("x", 32, 32)).Value);

            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(RequireFile(@"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt"), "external.ppocrv5", "v5", true, "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b");
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(new ModelId("external/ppocrv5-server-rec"), PaddleArtifact(10, "5c4927aa0736ab598025a37b71daae061363642b1848a90a0cb1e02e2ce823d7", "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b"), characters);
            Assert.IsInstanceOfType<TextRecognitionBatchResult>(Run(recognizer.VisualProfile, recognizer.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_server_rec_onnx.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("x", 320, 48)).Value);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AnomalibPadimAndPatchCoreFourOutputContractsRunOnRealCpuOrt()
        {
            RequireExternal();
            AnomalibProfile padim = AnomalibProfiles.CreatePadim(new ModelId("external/anomalib-padim"), new AnomalibArtifactContract(14, "bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff", "ffde4cce", "torch-2.7.1"));
            AssertAnomaly(Run(padim.VisualProfile, padim.CreateArtifact(RequireFile(@"E:\Model\anomalib\Padim\model\padim.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("input", 256, 256)));

            AnomalibProfile patchCore = AnomalibProfiles.CreatePatchCore(new ModelId("external/anomalib-patchcore"), new AnomalibArtifactContract(14, "5e5a34babd1f984962c17c7fba0060d0d22a34085ad6cae392ae3db30bd4244a", "ffde4cce", "torch-2.7.1"));
            AssertAnomaly(Run(patchCore.VisualProfile, patchCore.CreateArtifact(RequireFile(@"E:\Model\anomalib\Patchcore\model\model.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("input", 256, 256)));
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void BriaRmbg14AlphaContractRunsOnRealCpuOrt()
        {
            RequireExternal();
            BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg14(new ModelId("external/bria-rmbg-1.4"), new BriaRmbgProfileOptions(11, new VisualSize(1024, 1024), "input", "output", "8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f", "2ceba5a5", "torch-2.1.0", "LicenseRef-BRIA-RMBG-1.4"));
            AssertAlpha(Run(profile.VisualProfile, profile.CreateArtifact(RequireFile(@"E:\Model\RMBG\bria-rmbg-1.4.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("input", 1024, 1024)), 1024, 1024);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void BriaRmbg20QuantizedDynamicAlphaContractRunsOnRealCpuOrt()
        {
            RequireExternal();
            BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg20(new ModelId("external/bria-rmbg-2.0-quantized"), new BriaRmbgProfileOptions(14, new VisualSize(1024, 1024), "pixel_values", "alphas", "fcea23951a378f92634834888896cc1eec54655366ae6e949282646ce17c5420", "5df4c9c7", "onnx.quantize", "LicenseRef-BRIA-RMBG-2.0"));
            AssertAlpha(Run(profile.VisualProfile, profile.CreateArtifact(RequireFile(@"E:\Model\RMBG\RMBG-2.0_quantized.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("pixel_values", 1024, 1024)), 1024, 1024);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void BriaRmbg20FullPrecisionAlphaContractRunsOnRealCpuOrt()
        {
            RequireExternal();
            BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg20(new ModelId("external/bria-rmbg-2.0-fp32"), new BriaRmbgProfileOptions(14, new VisualSize(1024, 1024), "pixel_values", "alphas", "5b486f08200f513f460da46dd701db5fbb47d79b4be4b708a19444bcd4e79958", "5df4c9c7", "local-exporter-unverified", "LicenseRef-BRIA-RMBG-2.0"));
            AssertAlpha(Run(profile.VisualProfile, profile.CreateArtifact(RequireFile(@"E:\Model\RMBG\RMBG-2.0.onnx"), OnnxRuntimeBackendProvider.BackendId), Input("pixel_values", 1024, 1024)), 1024, 1024);
        }

        private static VisualInferenceResult Run(VisualModelProfile profile, ModelArtifact artifact, PreparedVisualInput input)
        {
            using (input)
            using (var registry = new BackendRegistry())
            {
                registry.UseOnnxRuntime();
                var profiles = new VisualProfileRegistry();
                profiles.Register(profile);
                profiles.Freeze();
                var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
                using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.Task), request);
                var watch = Stopwatch.StartNew();
                VisualInferenceResult result = pipeline.Run(input);
                watch.Stop();
                TestContextOut.WriteLine("STAGE19_ORT model=" + profile.ModelId.Value + " elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                return result;
            }
        }

        private static PreparedVisualInput Input(string name, int width, int height)
        {
            var size = new VisualSize(width, height);
            return new PreparedVisualInput(name, new Tensor<float>(new TensorShape(1, 3, height, width), new float[checked(3 * width * height)]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static PaddleOcrArtifactContract PaddleArtifact(int opset, string modelSha, string? dictionarySha = null)
            => new PaddleOcrArtifactContract(opset, modelSha, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "local-exporter-unverified", "Apache-2.0", "stage19-preprocess-v1", "stage19-postprocess-v1", dictionarySha256: dictionarySha, dictionaryLicense: "Apache-2.0-review-required");

        private static void AssertAnomaly(VisualInferenceResult inference)
        {
            Assert.IsInstanceOfType<AnomalyDetectionResult>(inference.Value);
            var result = (AnomalyDetectionResult)inference.Value;
            Assert.AreEqual(256, result.NormalizedMap.Width);
            Assert.AreEqual(256, result.NormalizedMap.Height);
            Assert.IsTrue(result.ImageScore >= 0f && result.ImageScore <= 1f);
        }

        private static void AssertAlpha(VisualInferenceResult inference, int width, int height)
        {
            Assert.IsInstanceOfType<BackgroundRemovalResult>(inference.Value);
            var result = (BackgroundRemovalResult)inference.Value;
            Assert.AreEqual(width, result.Alpha.Width);
            Assert.AreEqual(height, result.Alpha.Height);
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE19_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE19_RUN_EXTERNAL=1 to run the authorized local stage-19 model matrix.");
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured local model does not exist: " + path);
            return path;
        }

        private static class TestContextOut
        {
            public static void WriteLine(string value) => Console.WriteLine(value);
        }
    }
}
