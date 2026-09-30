using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
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
        public TestContext TestContext { get; set; } = null!;

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

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AllLocalPaddleNmsDecodersRunOnOpenCvDnnWhenImporterSupportsThem()
        {
            RequireExternalMatrix();
            var cases = new[]
            {
                (Model: "paddle-doc/pp-doclayout-plus-l", File: "pp-doclayout-plus-l.onnx", Size: new VisualSize(800, 800), Labels: PaddleDocumentProfiles.LayoutPlusLabels, ImageShape: true),
                (Model: "paddle-doc/pp-doclayout-m", File: "pp-doclayout-m.onnx", Size: new VisualSize(640, 640), Labels: PaddleDocumentProfiles.Layout23Labels, ImageShape: false),
                (Model: "paddle-doc/pp-doclayout-s", File: "pp-doclayout-s.onnx", Size: new VisualSize(480, 480), Labels: PaddleDocumentProfiles.Layout23Labels, ImageShape: false),
                (Model: "paddle-doc/pp-docblocklayout", File: "pp-docblocklayout.onnx", Size: new VisualSize(640, 640), Labels: PaddleDocumentProfiles.RegionLabels, ImageShape: true),
                (Model: "paddle-doc/picodet-layout-1x", File: "picodet-layout-1x.onnx", Size: new VisualSize(608, 800), Labels: PaddleDocumentProfiles.Layout5Labels, ImageShape: false),
                (Model: "paddle-doc/picodet-layout-1x-table", File: "picodet-layout-1x-table.onnx", Size: new VisualSize(608, 800), Labels: PaddleDocumentProfiles.TableOnlyLabels, ImageShape: false),
                (Model: "paddle-doc/picodet-s-layout-3cls", File: "picodet-s-layout-3cls.onnx", Size: new VisualSize(480, 480), Labels: PaddleDocumentProfiles.Layout3Labels, ImageShape: false),
                (Model: "paddle-doc/picodet-l-layout-3cls", File: "picodet-l-layout-3cls.onnx", Size: new VisualSize(640, 640), Labels: PaddleDocumentProfiles.Layout3Labels, ImageShape: false),
                (Model: "paddle-doc/rt-detr-h-layout-3cls", File: "rt-detr-h-layout-3cls.onnx", Size: new VisualSize(640, 640), Labels: PaddleDocumentProfiles.Layout3Labels, ImageShape: true),
                (Model: "paddle-doc/picodet-s-layout-17cls", File: "picodet-s-layout-17cls.onnx", Size: new VisualSize(480, 480), Labels: PaddleDocumentProfiles.Layout17Labels, ImageShape: false),
                (Model: "paddle-doc/picodet-l-layout-17cls", File: "picodet-l-layout-17cls.onnx", Size: new VisualSize(640, 640), Labels: PaddleDocumentProfiles.Layout17Labels, ImageShape: false),
                (Model: "paddle-doc/rt-detr-h-layout-17cls", File: "rt-detr-h-layout-17cls.onnx", Size: new VisualSize(640, 640), Labels: PaddleDocumentProfiles.Layout17Labels, ImageShape: true),
                (Model: "paddle-table/rt-detr-l-wired-cell-det", File: "rt-detr-l-wired-cell-det.onnx", Size: new VisualSize(640, 640), Labels: new[] { "table-cell" }, ImageShape: true),
                (Model: "paddle-table/rt-detr-l-wireless-cell-det", File: "rt-detr-l-wireless-cell-det.onnx", Size: new VisualSize(640, 640), Labels: new[] { "table-cell" }, ImageShape: true)
            };
            var evidence = new List<object>(cases.Length);
            int unexpectedFailures = 0;
            foreach (var item in cases)
            {
                string path = Path.Combine(ModelRoot, item.File);
                if (!File.Exists(path))
                {
                    evidence.Add(new { model = item.Model, status = "missing-model", file = item.File });
                    continue;
                }

                PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                    PaddleDocumentModelCatalog.Get(item.Model),
                    item.Labels,
                    item.Size,
                    countOutputName: null,
                    scoreThreshold: 0,
                    includeImageShapeInput: item.ImageShape,
                    includeScaleFactorInput: true);
                try
                {
                    var contract = CreateOpenCvPaddleNmsContract(profile);
                    using var registry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
                    var profiles = new VisualProfileRegistry();
                    profiles.Register(profile.VisualProfile);
                    profiles.Freeze();
                    var request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
                    using var pipeline = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(path, OpenCvDnnBackendProvider.BackendId), registry, request, profile.VisualProfile.Task), request);
                    using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), ImagePath, profile, inputId: "paddle-document-opencv-nms-matrix");
                    DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
                    Assert.IsTrue(result.Detections.All(detection => detection.Label.Score >= 0 && detection.Label.Score <= 1), item.Model + " returned an invalid score.");
                    Assert.IsTrue(result.Detections.All(detection => float.IsFinite(detection.Box.X) && float.IsFinite(detection.Box.Y) && float.IsFinite(detection.Box.Width) && float.IsFinite(detection.Box.Height)), item.Model + " returned non-finite geometry.");
                    evidence.Add(new
                    {
                        model = item.Model,
                        status = "passed",
                        modelSha256 = FileSha256(path),
                        modelSize = new { width = item.Size.Width, height = item.Size.Height },
                        labels = item.Labels,
                        includeImageShape = item.ImageShape,
                        detectionCount = result.Detections.Count
                    });
                    Console.WriteLine("PADDLE_DOCUMENT_OPENCV_NMS_MATRIX model=" + item.Model + ";status=passed;detections=" + result.Detections.Count);
                }
                catch (Exception exception)
                {
                    OpenCvDnnBackendException? backendException = FindOpenCvBackendException(exception);
                    if (backendException != null)
                    {
                        evidence.Add(new
                        {
                            model = item.Model,
                            status = "unsupported",
                            modelSha256 = FileSha256(path),
                            errorCode = backendException.ErrorCode,
                            technicalDetails = backendException.TechnicalDetails,
                            message = backendException.Message
                        });
                        Console.WriteLine("PADDLE_DOCUMENT_OPENCV_NMS_MATRIX model=" + item.Model + ";status=unsupported;errorCode=" + backendException.ErrorCode + ";details=" + backendException.TechnicalDetails);
                    }
                    else
                    {
                        unexpectedFailures++;
                        evidence.Add(new { model = item.Model, status = "failed", modelSha256 = FileSha256(path), exception = exception.ToString() });
                        Console.WriteLine("PADDLE_DOCUMENT_OPENCV_NMS_MATRIX model=" + item.Model + ";status=failed;exception=" + exception);
                    }
                }
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_NMS_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-opencv-nms-matrix.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                backend = "opencv-dnn-cpu",
                input = new { file = "bus.jpg", path = ImagePath },
                scope = "Fourteen PP-Structure Paddle NMS ONNX artifacts with their registered input and decoder contracts.",
                results = evidence,
                boundary = "Importer/execution and decoder-contract evidence only; no layout or cell recall, table structure accuracy or performance claim is made."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(cases.Length, evidence.Count);
            Assert.AreEqual(0, unexpectedFailures, "Unexpected non-OpenCV failures must be investigated instead of being treated as unsupported.");
        }

        private static OpenCvDnnModelContract CreateOpenCvPaddleNmsContract(PaddleDocumentProfile profile)
        {
            var inputs = new List<TensorDescriptor>
            {
                new TensorDescriptor(profile.VisualProfile.Input.Name, profile.VisualProfile.Input.ElementType, profile.VisualProfile.Input.ShapePattern)
            };
            inputs.AddRange(profile.VisualProfile.AuxiliaryInputs.Select(binding => new TensorDescriptor(binding.Name, binding.ElementType, binding.ShapePattern)));
            var outputs = profile.VisualProfile.Outputs.Select(binding => new TensorDescriptor(binding.Name, binding.ElementType, binding.ShapePattern));
            return new OpenCvDnnModelContract(profile.VisualProfile.ModelId, inputs, outputs, new[] { profile.VisualProfile.Input.Name });
        }

        private static string FileSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static OpenCvDnnBackendException? FindOpenCvBackendException(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is OpenCvDnnBackendException backendException) return backendException;
            }
            return null;
        }

        private static void RequireExternalMatrix()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL=1 to run the authorized local PP-Structure OpenCV DNN matrix.");
            if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing semantic smoke input image: " + ImagePath);
        }

        private static NamedTensor FloatInput(string name, params float[] values)
            => new NamedTensor(name, new Tensor<float>(new TensorShape(1, 2), values, TensorBufferOwnership.Transfer));
    }
}
