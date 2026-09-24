using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>Runs a representative PP-Structure layout export through OpenCV DNN when explicitly enabled. / 在显式启用时通过 OpenCV DNN 运行代表性 PP-Structure 版面导出。</summary>
    [TestClass]
    public sealed class PaddleDocumentOpenCvSemanticIntegrationTests
    {
        private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
        private const string ImagePath = @"E:\Data\image\bus.jpg";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LayoutRunsThroughOpenCvDnnWhenImporterSupportsPaddleNmsExport()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL=1 to run the authorized local PP-Structure OpenCV DNN smoke.");
            if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing semantic smoke input image: " + ImagePath);
            string modelPath = Path.Combine(ModelRoot, "pp-doclayout-l.onnx");
            if (!File.Exists(modelPath)) Assert.Inconclusive("Missing local layout model: " + modelPath);

            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                countOutputName: null,
                scoreThreshold: 0);
            var contract = new OpenCvDnnModelContract(
                profile.VisualProfile.ModelId,
                new[]
                {
                    new TensorDescriptor("image", TensorElementType.Float32, new TensorShape(-1, 3, 640, 640)),
                    new TensorDescriptor("im_shape", TensorElementType.Float32, new TensorShape(-1, 2)),
                    new TensorDescriptor("scale_factor", TensorElementType.Float32, new TensorShape(-1, 2))
                },
                new[]
                {
                    new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(-1, 6))
                },
                imageInputNames: new[] { "image" });

            using var registry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
            VisualProfileSelection selection = profiles.Select(profile.CreateArtifact(modelPath, OpenCvDnnBackendProvider.BackendId), registry, request, profile.VisualProfile.Task);
            try
            {
                using var pipeline = new VisualPipeline(registry, selection, request);
                using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(
                    new OpenCvVisualInputFactory(),
                    ImagePath,
                    profile,
                    inputId: "paddle-document-opencv-layout");
                VisualInferenceResult inference = pipeline.Run(input);
                DetectionResult result = inference.GetValue<DetectionResult>();
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Detections.Count > 0, "OpenCV DNN returned no valid layout regions from the semantic smoke image.");
                Assert.IsTrue(result.Detections.All(detection =>
                    !float.IsNaN(detection.Box.X) && !float.IsInfinity(detection.Box.X) &&
                    !float.IsNaN(detection.Box.Y) && !float.IsInfinity(detection.Box.Y) &&
                    !float.IsNaN(detection.Box.Width) && !float.IsInfinity(detection.Box.Width) &&
                    !float.IsNaN(detection.Box.Height) && !float.IsInfinity(detection.Box.Height)),
                    "OpenCV DNN returned a non-finite layout box.");
                Console.WriteLine("PADDLE_DOCUMENT_OPENCV_SEMANTIC module=layout;detections=" + result.Detections.Count + ";count-output=omitted-single-batch");

                // Compare both backends on the same prepared tensor. A successful
                // import must not silently change scores, labels or coordinates.
                using var ort = new BackendRegistry().UseOnnxRuntime();
                var ortRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
                using var reference = new VisualPipeline(ort, profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), ort, ortRequest, profile.VisualProfile.Task), ortRequest);
                DetectionResult expected = reference.Run(input).GetValue<DetectionResult>();
                Detection[] actualBoxes = result.Detections.Where(value => value.Label.Score >= .5f).OrderBy(value => value.Label.Index).ThenByDescending(value => value.Label.Score).ToArray();
                Detection[] referenceBoxes = expected.Detections.Where(value => value.Label.Score >= .5f).OrderBy(value => value.Label.Index).ThenByDescending(value => value.Label.Score).ToArray();
                Assert.IsTrue(referenceBoxes.Length > 0, "The ORT reference must contain at least one confident region.");
                Assert.AreEqual(referenceBoxes.Length, actualBoxes.Length, "Backend threshold decisions differ.");
                for (int index = 0; index < referenceBoxes.Length; index++)
                {
                    Assert.AreEqual(referenceBoxes[index].Label.Index, actualBoxes[index].Label.Index);
                    Assert.AreEqual(referenceBoxes[index].Label.Score, actualBoxes[index].Label.Score, .001f);
                    Assert.AreEqual(referenceBoxes[index].Box.X, actualBoxes[index].Box.X, .5f);
                    Assert.AreEqual(referenceBoxes[index].Box.Y, actualBoxes[index].Box.Y, .5f);
                    Assert.AreEqual(referenceBoxes[index].Box.Width, actualBoxes[index].Box.Width, .5f);
                    Assert.AreEqual(referenceBoxes[index].Box.Height, actualBoxes[index].Box.Height, .5f);
                }
                Console.WriteLine("PADDLE_DOCUMENT_OPENCV_PARITY regions=" + referenceBoxes.Length + ";scoreTolerance=0.001;boxTolerance=0.5px");
            }
            catch (VisualException exception) when (exception.ToString().IndexOf("training_mode", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Assert.Fail("The inference BatchNormalization compatibility pass regressed: " + exception);
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void UvDocRunsThroughOpenCvDnnAndProducesFiniteCorrectedTensor()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL=1 to run the authorized local UVDoc OpenCV smoke.");
            string modelPath = Path.Combine(ModelRoot, "uvdoc.onnx");
            if (!File.Exists(modelPath) || !File.Exists(ImagePath)) Assert.Inconclusive("Missing UVDoc model or semantic image.");
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateUnwarping(descriptor, new VisualSize(640, 640));
            var contract = new OpenCvDnnModelContract(profile.VisualProfile.ModelId,
                new[] { new TensorDescriptor("image", TensorElementType.Float32, new TensorShape(1, 3, -1, -1)) },
                new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, 3, 640, 640)) },
                imageInputNames: new[] { "image" });
            using var registry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
            var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
            VisualProfileSelection selection = profiles.Select(profile.CreateArtifact(modelPath, OpenCvDnnBackendProvider.BackendId), registry, request, profile.VisualProfile.Task);
            using var pipeline = new VisualPipeline(registry, selection, request);
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(ImagePath, profile.VisualProfile, "paddle-document-opencv-uvdoc");
            PaddleDocumentUnwarpingResult result = pipeline.Run(input).GetValue<PaddleDocumentUnwarpingResult>();
            Assert.AreEqual(640, result.Width); Assert.AreEqual(640, result.Height); Assert.AreEqual(3, result.Channels);
            Assert.IsTrue(result.Pixels.All(float.IsFinite));
            Console.WriteLine("PADDLE_DOCUMENT_OPENCV_SEMANTIC module=uvdoc;shape=" + result.Width + "x" + result.Height + "x" + result.Channels + ";min=" + result.Pixels.Min().ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";max=" + result.Pixels.Max().ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static NamedTensor FloatInput(string name, params float[] values)
            => new NamedTensor(name, new Tensor<float>(new TensorShape(1, 2), values, TensorBufferOwnership.Transfer));
    }
}
