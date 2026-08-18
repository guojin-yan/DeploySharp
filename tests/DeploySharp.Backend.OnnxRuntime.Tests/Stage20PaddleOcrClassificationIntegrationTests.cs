using System;
using System.Diagnostics;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    [TestClass]
    public sealed class Stage20PaddleOcrClassificationIntegrationTests
    {
        [TestMethod]
        [DataRow(false, "mobile", "DEPLOYSHARP_STAGE20_PADDLE_OCR_CLS_MODEL", @"E:\Model\ocr\ppocrv5\PP-OCRv5_mobile_cls_onnx.onnx", "dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2")]
        [DataRow(false, "server", "DEPLOYSHARP_STAGE20_PADDLE_OCR_SERVER_CLS_MODEL", @"E:\Model\ocr\ppocrv5-1\PP-OCRv5_server_cls_onnx.onnx", "d874cd926a8f9f66e886bbd8ad7747635802b6cc52d3b81b5892845fc84c616f")]
        [DataRow(true, "legacy", "DEPLOYSHARP_STAGE20_PADDLE_OCR_LEGACY_CLS_MODEL", @"E:\Model\ocr\ppocrv4\PP-OCRv4_mobile_cls_onnx.onnx", "f4bb53707100c5f3d59ba834eb05bb400369f20aed35d4b26807b1bfadd2a70e")]
        [TestCategory("ExternalModels")]
        public void EveryLocalClassificationContractRunsOnRealCpuOrt(bool legacy, string variant, string environmentVariable, string fallbackPath, string sha256)
        {
            RequireExternal();
            PaddleOcrProfile profile = CreateProfile("external/stage20-" + variant + "-cls-ort", legacy, sha256);
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            ModelArtifact modelArtifact = profile.CreateArtifact(ModelPath(environmentVariable, fallbackPath), OnnxRuntimeBackendProvider.BackendId);
            using (IInferenceSession metadataSession = registry.CreateSession(modelArtifact, request))
            {
                Console.WriteLine("STAGE20_ORT_CLS_METADATA variant=" + variant + ";input=" + metadataSession.Metadata.Inputs[0].Name + metadataSession.Metadata.Inputs[0].Shape + ";output=" + metadataSession.Metadata.Outputs[0].Name + metadataSession.Metadata.Outputs[0].Shape);
            }
            using var pipeline = new OcrOrientationPipeline(registry, profiles.Select(modelArtifact, registry, request, VisualTaskId.TextOrientationClassification), request);
            using PreparedVisualInput input = Input(legacy);
            var watch = Stopwatch.StartNew();
            OcrOrientationResult result = pipeline.Run(input);
            watch.Stop();

            Assert.AreEqual(2, result.Scores.Count);
            Assert.IsTrue(result.Confidence >= 0f && result.Confidence <= 1f);
            Assert.IsTrue(result.Orientation == TextOrientation.Degrees0 || result.Orientation == TextOrientation.Degrees180);
            Console.WriteLine("STAGE20_ORT_CLS variant=" + variant + ";label=" + profile.VisualProfile.GetLabel(result.ClassIndex) + ";confidence=" + result.Confidence.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static PaddleOcrProfile CreateProfile(string modelId, bool legacy, string sha256)
        {
            var artifact = new PaddleOcrArtifactContract(legacy ? 11 : 7, sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", legacy ? "local-exporter-unverified" : "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", legacy ? "paddle-ocr-cls-legacy-bgr-h48-w192" : "pp-lcnet-textline-rgb-imagenet-v1", "argmax-0-180-threshold-v1");
            return legacy ? PaddleOcrProfiles.CreateLegacyClassification(new ModelId(modelId), artifact, outputName: "softmax_0.tmp_0", rejectionThreshold: 0f, allowDynamicBatch: true) : PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId(modelId), artifact, rejectionThreshold: 0f);
        }

        private static PreparedVisualInput Input(bool legacy)
        {
            var size = legacy ? new VisualSize(192, 48) : new VisualSize(160, 80);
            return new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, size.Height, size.Width), new float[3 * size.Height * size.Width]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
        }

        private static string ModelPath(string environmentVariable, string fallbackPath)
        {
            string path = Environment.GetEnvironmentVariable(environmentVariable) ?? fallbackPath;
            if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCRCls model does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_STAGE20_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_STAGE20_RUN_EXTERNAL=1 to run the authorized local PaddleOCRCls contract.");
        }
    }
}
