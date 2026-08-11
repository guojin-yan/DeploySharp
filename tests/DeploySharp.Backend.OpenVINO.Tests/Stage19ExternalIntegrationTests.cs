using System;
using System.Diagnostics;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    /// <summary>Runs representative stage-19 named-output contracts through real OpenVINO CPU. / 通过真实 OpenVINO CPU 运行阶段 19 的代表性命名输出合同。</summary>
    [TestClass]
    public sealed class Stage19ExternalIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void PaddleOcrDetectionAndRecognitionContractsRunOnRealOpenVinoCpu()
        {
            RequireExternal();
            PaddleOcrProfile detector = PaddleOcrProfiles.CreateDetection(new ModelId("external/openvino-ppocrv5-det"), PaddleArtifact(11, "1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039"));
            Assert.IsInstanceOfType<TextDetectionResult>(Run(detector.VisualProfile, detector.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_det_onnx.onnx"), OpenVinoBackendProvider.BackendId), Input("x", 32, 32)).Value);

            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(RequireFile(@"E:\Model\ocr\ppocrv5\ppocrv5_dict.txt"), "external.ppocrv5", "v5", true, "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b");
            PaddleOcrProfile recognizer = PaddleOcrProfiles.CreateRecognition(new ModelId("external/openvino-ppocrv5-rec"), PaddleArtifact(7, "f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9", "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b"), characters);
            Assert.IsInstanceOfType<TextRecognitionBatchResult>(Run(recognizer.VisualProfile, recognizer.CreateArtifact(RequireFile(@"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_rec_onnx.onnx"), OpenVinoBackendProvider.BackendId), Input("x", 320, 48)).Value);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AnomalibFourOutputContractRunsOnRealOpenVinoCpu()
        {
            RequireExternal();
            AnomalibProfile profile = AnomalibProfiles.CreatePadim(new ModelId("external/openvino-anomalib-padim"), new AnomalibArtifactContract(14, "bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff", "ffde4cce", "torch-2.7.1"));
            VisualInferenceResult inference = Run(profile.VisualProfile, profile.CreateArtifact(RequireFile(@"E:\Model\anomalib\Padim\model\padim.onnx"), OpenVinoBackendProvider.BackendId), Input("input", 256, 256));
            Assert.IsInstanceOfType<AnomalyDetectionResult>(inference.Value);
            Assert.AreEqual(256, ((AnomalyDetectionResult)inference.Value).NormalizedMap.Width);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void BriaAlphaContractRunsOnRealOpenVinoCpu()
        {
            RequireExternal();
            BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg14(new ModelId("external/openvino-rmbg-1.4"), new BriaRmbgProfileOptions(11, new VisualSize(1024, 1024), "input", "output", "8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f", "2ceba5a5", "torch-2.1.0", "LicenseRef-BRIA-RMBG-1.4"));
            VisualInferenceResult inference = Run(profile.VisualProfile, profile.CreateArtifact(RequireFile(@"E:\Model\RMBG\bria-rmbg-1.4.onnx"), OpenVinoBackendProvider.BackendId), Input("input", 1024, 1024));
            Assert.IsInstanceOfType<BackgroundRemovalResult>(inference.Value);
            Assert.AreEqual(1024, ((BackgroundRemovalResult)inference.Value).Alpha.Width);
        }

        private static VisualInferenceResult Run(VisualModelProfile profile, ModelArtifact artifact, PreparedVisualInput input)
        {
            using (input)
            using (var registry = new BackendRegistry())
            {
                registry.UseOpenVino();
                var profiles = new VisualProfileRegistry();
                profiles.Register(profile);
                profiles.Freeze();
                var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
                using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.Task), request);
                var watch = Stopwatch.StartNew();
                VisualInferenceResult result = pipeline.Run(input);
                watch.Stop();
                Console.WriteLine("STAGE19_OPENVINO model=" + profile.ModelId.Value + " elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
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

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE19_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE19_RUN_EXTERNAL=1 to run the authorized local stage-19 model matrix.");
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured local model does not exist: " + path);
            return path;
        }
    }
}
