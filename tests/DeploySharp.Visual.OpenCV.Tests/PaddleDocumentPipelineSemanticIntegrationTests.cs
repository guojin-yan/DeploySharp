using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>
    /// Runs a real orientation -> layout composition through the backend-neutral
    /// PP-Structure orchestrator and independent ONNX Runtime sessions.
    /// This is intentionally separate from per-model decoder smoke: it proves
    /// stage ordering, context propagation and result aggregation on a real page.
    /// </summary>
    [TestClass]
    public sealed class PaddleDocumentPipelineSemanticIntegrationTests
    {
        private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
        private const string ImagePath = @"E:\Data\image\bus.jpg";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public async Task OrientationAndLayoutComposeThroughUnifiedPipelineOnOrtCpu()
        {
            RequireExternal();
            if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing semantic smoke input image: " + ImagePath);
            string orientationPath = Path.Combine(ModelRoot, "pp-lcnet-x1-0-doc-ori.onnx");
            string layoutPath = Path.Combine(ModelRoot, "pp-doclayout-l.onnx");
            if (!File.Exists(orientationPath)) Assert.Inconclusive("Missing local document orientation model: " + orientationPath);
            if (!File.Exists(layoutPath)) Assert.Inconclusive("Missing local document layout model: " + layoutPath);

            PaddleDocumentModelDescriptor orientationDescriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-lcnet-x1-0-doc-ori");
            PaddleDocumentProfile orientationProfile = PaddleDocumentProfiles.CreateClassification(
                orientationDescriptor,
                PaddleDocumentProfiles.DocumentOrientationLabels,
                VisualTaskId.DocumentOrientation,
                modelSize: new VisualSize(224, 224));
            PaddleDocumentModelDescriptor layoutDescriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile layoutProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                layoutDescriptor,
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                scoreThreshold: 0);

            using var orientationRegistry = new BackendRegistry();
            using var layoutRegistry = new BackendRegistry();
            orientationRegistry.UseOnnxRuntime();
            layoutRegistry.UseOnnxRuntime();
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using VisualPipeline orientationVisual = CreateVisualPipeline(orientationRegistry, orientationProfile, orientationPath, request);
            using (IInferenceSession probe = layoutRegistry.CreateSession(layoutProfile.CreateArtifact(layoutPath, OnnxRuntimeBackendProvider.BackendId), request))
            {
                Console.WriteLine("PADDLE_DOCUMENT_PIPELINE_METADATA inputs=" + string.Join(",", probe.Metadata.Inputs.Select(item => item.Name + ":" + item.ElementType + ":" + item.Shape)) + ";outputs=" + string.Join(",", probe.Metadata.Outputs.Select(item => item.Name + ":" + item.ElementType + ":" + item.Shape)));
            }
            using VisualPipeline layoutVisual = CreateVisualPipeline(layoutRegistry, layoutProfile, layoutPath, request);

            using PreparedVisualInput sourceProbe = new OpenCvVisualInputFactory().CreateFromFile(ImagePath, orientationProfile.VisualProfile);
            VisualSize sourceSize = sourceProbe.SourceSize;
            string sourceSha = ComputeSha256(ImagePath);
            var orientationStage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
                PaddleDocumentModule.DocumentOrientation,
                orientationVisual,
                (context, token) => new OpenCvVisualInputFactory().CreateFromFile(
                    ImagePath,
                    orientationProfile.VisualProfile,
                    inputId: sourceSha),
                (context, inference) =>
                {
                    ClassificationResult value = inference.GetValue<ClassificationResult>();
                    LabelScore top = value.TopPrediction ?? throw new AssertFailedException("Orientation returned no prediction.");
                    return new PaddleDocumentOrientationResult(
                        new PaddleDocumentResultMetadata(orientationDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex),
                        top.Label,
                        top.Index * 90);
                });

            var layoutStage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
                PaddleDocumentModule.LayoutDetection,
                layoutVisual,
                (context, token) =>
                {
                    Assert.IsNotNull(context.TryGet<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation), "Layout must receive orientation output from the preceding stage.");
                    return OpenCvPaddleDocumentPreprocessing.CreateFromFile(
                        new OpenCvVisualInputFactory(),
                        ImagePath,
                        layoutProfile,
                        inputId: sourceSha);
                },
                (context, inference) =>
                {
                    DetectionResult value = inference.GetValue<DetectionResult>();
                    var regions = value.Detections.Select(detection => new PaddleDocumentRegion(
                        detection.Label.Label,
                        detection.Label.Score,
                        detection.Box)).ToArray();
                    return new PaddleDocumentRegionResult(
                        PaddleDocumentModule.LayoutDetection,
                        new PaddleDocumentResultMetadata(layoutDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex),
                        regions);
                });

            var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { layoutStage, orientationStage });
            PaddleDocumentPipelineResult page = await pipeline.RunAsync(
                new PaddleDocumentPage(ImagePath, sourceSize, pageIndex: 0),
                CancellationToken.None).ConfigureAwait(false);

            PaddleDocumentOrientationResult orientation = page.GetRequired<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation);
            PaddleDocumentRegionResult layout = page.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.LayoutDetection);
            Assert.IsTrue(orientation.RotationDegrees == 0 || orientation.RotationDegrees == 90 || orientation.RotationDegrees == 180 || orientation.RotationDegrees == 270);
            Assert.IsNotNull(layout.Regions);
            Assert.AreEqual(2, page.Results.Count);
            Assert.AreEqual(2, page.Timings.Count);
            Console.WriteLine(
                "PADDLE_DOCUMENT_PIPELINE_ORT_CPU page=" + page.Page.PageIndex +
                ";orientation=" + orientation.Label +
                ";rotation=" + orientation.RotationDegrees +
                ";layoutRegions=" + layout.Regions.Count +
                ";orientationMs=" + page.Timings[0].Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ";layoutMs=" + page.Timings[1].Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ";totalMs=" + page.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ";inputSha256=" + sourceSha);
        }

        private static VisualPipeline CreateVisualPipeline(BackendRegistry registry, PaddleDocumentProfile profile, string modelPath, BackendRequest request)
        {
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            VisualProfileSelection selection = profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), registry, request, profile.VisualProfile.Task);
            return new VisualPipeline(registry, selection, request);
        }

        private static NamedTensor FloatInput(string name, params float[] values)
            => new NamedTensor(name, new JYPPX.DeploySharp.Tensors.Tensor<float>(new JYPPX.DeploySharp.Tensors.TensorShape(1, 2), values, JYPPX.DeploySharp.Tensors.TensorBufferOwnership.Transfer));

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1 to run the authorized local PP-Structure semantic smoke.");
        }
    }
}
