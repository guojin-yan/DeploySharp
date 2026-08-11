using System;
using System.Diagnostics;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenVINO.Tests
{
    /// <summary>Runs the distinct stage-21 RT-DETR contracts on real OpenVINO CPU when explicitly enabled. / 显式启用时在真实 OpenVINO CPU 上运行阶段 21 的不同 RT-DETR 合同。</summary>
    [TestClass]
    public sealed class Stage21RtDetrIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void PaddleDecodedVectorCountIrRunsOnOpenVinoCpu()
        {
            RequireExternal();
            const string path = @"E:\Model\RT-DETR\RTDETR\rtdetr_r50vd_6x_coco_quant.xml";
            RequireFile(path);
            var options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(640, 640),
                YoloLabelSets.Coco80,
                modelFormat: "openvino-ir",
                inputName: "image",
                artifactSha256: "9d49703964c07567de7f00bda85bae1760da322e2b0655bfae110f2c222c778d",
                upstreamRepository: "local-authorized-read-only",
                upstreamCommit: "external-review-required",
                exporterVersion: "external-openvino-ir",
                license: "External",
                scoreThreshold: .01f,
                boxesOutputName: "save_infer_model/scale_0.tmp_0",
                countOutputName: "cast_5.tmp_0",
                hasDynamicBatchAxis: true,
                paddleCountShape: PortableDetectorCountShape.BatchVector);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETR(new ModelId("external/openvino-rtdetr-decoded"), options);
            using PreparedVisualInput input = Input(profile, new[]
            {
                new NamedTensor("im_shape", new Tensor<float>(new TensorShape(1, 2), new[] { 640f, 640f })),
                new NamedTensor("scale_factor", new Tensor<float>(new TensorShape(1, 2), new[] { 1f, 1f }))
            });
            Assert.IsInstanceOfType<DetectionResult>(Run(profile, path, input).Value);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RawQueryOnnxRunsOnOpenVinoCpu()
        {
            RequireExternal();
            const string path = @"E:\Model\RT-DETR\RTDETR_cropping\rtdetr_r50vd_6x_coco.onnx";
            RequireFile(path);
            var options = new PortableDetectorProfileOptions(
                16,
                new VisualSize(640, 640),
                YoloLabelSets.Coco80,
                inputName: "image",
                artifactSha256: "544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad",
                upstreamRepository: "local-authorized-read-only",
                upstreamCommit: "external-review-required",
                exporterVersion: "inspected-paddle2onnx-raw",
                license: "External",
                scoreThreshold: .01f,
                maximumCandidates: 300,
                maximumResults: 300,
                topK: 300,
                boxesOutputName: "stack_7.tmp_0_slice_0",
                labelsOutputName: "stack_8.tmp_0_slice_0",
                rfDetrQueryCount: 300,
                hasDynamicBatchAxis: true);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETRRaw(new ModelId("external/openvino-rtdetr-raw"), options);
            using PreparedVisualInput input = Input(profile, Array.Empty<NamedTensor>());
            Assert.IsInstanceOfType<DetectionResult>(Run(profile, path, input).Value);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialRtDetrV2OnnxRunsWhenExplicitlyConfigured()
        {
            RequireExternal();
            string? path = Environment.GetEnvironmentVariable("DEPLOYSHARP_RTDETRV2_ONNX");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) Assert.Inconclusive("Set DEPLOYSHARP_RTDETRV2_ONNX to an official tools/export_onnx.py output.");
            var options = new PortableDetectorProfileOptions(16, new VisualSize(640, 640), YoloLabelSets.Coco80, inputName: "images", upstreamRepository: "https://github.com/lyuwenyu/RT-DETR", upstreamCommit: "1c8ac3f7ba84f14bd5651ab7b1b70d69a5f55f47", exporterVersion: "official-tools/export_onnx.py", license: "Apache-2.0", scoreThreshold: .01f, maximumCandidates: 300, maximumResults: 300, topK: 300, rfDetrQueryCount: 300, hasDynamicBatchAxis: true);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETRv2(new ModelId("external/openvino-rtdetrv2"), options);
            using PreparedVisualInput input = Input(profile, new[] { new NamedTensor("orig_target_sizes", new Tensor<long>(new TensorShape(1, 2), new long[] { 640, 640 })) });
            Assert.IsInstanceOfType<DetectionResult>(Run(profile, path!, input).Value);
        }

        private static VisualInferenceResult Run(PortableDetectorProfile profile, string path, PreparedVisualInput input)
        {
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            ModelArtifact artifact = profile.CreateArtifact(path, OpenVinoBackendProvider.BackendId);
            using (IInferenceSession metadataSession = registry.CreateSession(artifact, request))
            {
                foreach (TensorDescriptor descriptor in metadataSession.Metadata.Inputs) Console.WriteLine("STAGE21_RTDETR_OPENVINO_INPUT name=" + descriptor.Name + ";type=" + descriptor.ElementType + ";shape=" + descriptor.Shape);
                foreach (TensorDescriptor output in metadataSession.Metadata.Outputs) Console.WriteLine("STAGE21_RTDETR_OPENVINO_OUTPUT name=" + output.Name + ";type=" + output.ElementType + ";shape=" + output.Shape);
            }
            using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.VisualProfile.Task), request);
            var watch = Stopwatch.StartNew();
            VisualInferenceResult result = pipeline.Run(input);
            watch.Stop();
            Console.WriteLine("STAGE21_RTDETR_OPENVINO model=" + profile.VisualProfile.ModelId.Value + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            return result;
        }

        private static PreparedVisualInput Input(PortableDetectorProfile profile, NamedTensor[] auxiliary)
        {
            var size = new VisualSize(640, 640);
            return new PreparedVisualInput(profile.VisualProfile.Input.Name, new Tensor<float>(new TensorShape(1, 3, 640, 640), new float[3 * 640 * 640]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), auxiliaryInputs: auxiliary);
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_DETR_RUN_EXTERNAL_OPENVINO"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_DETR_RUN_EXTERNAL_OPENVINO=1 to run the authorized local RT-DETR OpenVINO matrix.");
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured local model does not exist: " + path);
        }
    }
}
