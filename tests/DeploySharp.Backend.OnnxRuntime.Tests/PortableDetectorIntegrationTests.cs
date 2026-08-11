using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OnnxRuntime.Tests
{
    /// <summary>Runs the user-authorized local five-family ONNX matrix when explicitly enabled. / 在显式启用时运行用户授权的五模型族本地 ONNX 矩阵。</summary>
    [TestClass]
    public sealed class PortableDetectorIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void DeimV2RunsThroughRealCpuOnnxRuntime()
        {
            RequireExternal();
            RunDeim();
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RfDetrRunsThroughRealCpuOnnxRuntime()
        {
            RequireExternal();
            RunRf(false);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RfDetrSegRunsThroughRealCpuOnnxRuntime()
        {
            RequireExternal();
            RunRf(true);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RtDetrLocalExportReportsStableTileContractFailure()
        {
            RequireExternal();
            VisualException exception = Assert.ThrowsExactly<VisualException>(RunRt);
            Assert.AreEqual(VisualErrorCodes.InferenceFailed, exception.ErrorCode);
            StringAssert.Contains(exception.ToString(), "Tile");
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RtDetrVectorCountExportRunsThroughRealCpuOnnxRuntime()
        {
            RequireExternal();
            RunRtDecodedVector();
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RtDetrRawQueryExportRunsThroughRealCpuOnnxRuntime()
        {
            RequireExternal();
            RunRtRaw();
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialRtDetrV2ExportRunsWhenExplicitlyConfigured()
        {
            RequireExternal();
            string? path = Environment.GetEnvironmentVariable("DEPLOYSHARP_RTDETRV2_ONNX");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) Assert.Inconclusive("Set DEPLOYSHARP_RTDETRV2_ONNX to an official tools/export_onnx.py output.");
            var options = new PortableDetectorProfileOptions(16, new VisualSize(640, 640), YoloLabelSets.Coco80, inputName: "images", upstreamRepository: "https://github.com/lyuwenyu/RT-DETR", upstreamCommit: "1c8ac3f7ba84f14bd5651ab7b1b70d69a5f55f47", exporterVersion: "official-tools/export_onnx.py", license: "Apache-2.0", scoreThreshold: .01f, maximumCandidates: 300, maximumResults: 300, topK: 300, rfDetrQueryCount: 300, hasDynamicBatchAxis: true);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETRv2(new ModelId("external/rtdetrv2"), options);
            Run(profile, path!, new[] { new NamedTensor("orig_target_sizes", new Tensor<long>(new TensorShape(1, 2), new long[] { 640, 640 })) });
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void PpYoloeRunsThroughRealCpuOnnxRuntime()
        {
            RequireExternal();
            RunPp();
        }

        private static void RunDeim()
        {
            const string path = @"E:\Model\DEIMv2\DEIMv2.onnx";
            RequireFile(path);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateDEIMv2(new ModelId("external/deimv2"), Options(16, new VisualSize(640, 640), Enumerable.Range(0, 4).Select(index => "class" + index), "images"));
            Run(profile, path, new[] { new NamedTensor("orig_target_sizes", new Tensor<long>(new TensorShape(1, 2), new long[] { 640, 640 })) });
        }

        private static void RunRf(bool segmentation)
        {
            string path = segmentation ? @"E:\Model\rf-detr\rf-detr-seg.onnx" : @"E:\Model\rf-detr\rf-detr.onnx";
            RequireFile(path);
            int classes = segmentation ? 90 : 5;
            IEnumerable<string> labels = Enumerable.Range(0, classes).Select(index => "class" + index);
            PortableDetectorProfile profile = segmentation
                ? PortableDetectorProfiles.CreateRFDETRSeg(new ModelId("external/rfdetr-seg"), Options(17, new VisualSize(segmentation ? 432 : 512, segmentation ? 432 : 512), labels, "input", segmentation ? "4245" : null, segmentation ? 200 : 300, true))
                : PortableDetectorProfiles.CreateRFDETR(new ModelId("external/rfdetr"), Options(17, new VisualSize(512, 512), labels, "input", null, 300, true));
            Run(profile, path, Array.Empty<NamedTensor>());
        }

        private static void RunRt()
        {
            const string path = @"E:\Model\RT-DETR\RTDETR\rtdetr_r50vd_6x_coco.onnx";
            RequireFile(path);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETR(new ModelId("external/rtdetr"), Options(16, new VisualSize(640, 640), YoloLabelSets.Coco80, "image"));
            Run(profile, path, new[]
            {
                new NamedTensor("im_shape", new Tensor<float>(new TensorShape(1, 2), new[] { 640f, 640f })),
                new NamedTensor("scale_factor", new Tensor<float>(new TensorShape(1, 2), new[] { 1f, 1f }))
            });
        }

        private static void RunRtDecodedVector()
        {
            const string path = @"E:\Model\RT-DETR\RTDETR\rtdetr_r50vd_6x_coco_quant.onnx";
            RequireFile(path);
            var options = new PortableDetectorProfileOptions(16, new VisualSize(640, 640), YoloLabelSets.Coco80, inputName: "image", upstreamRepository: "local-authorized-read-only", upstreamCommit: "external-review-required", exporterVersion: "inspected-paddle2onnx", license: "External", scoreThreshold: .01f, maximumCandidates: 3000, maximumResults: 300, topK: 300, boxesOutputName: "save_infer_model/scale_0.tmp_0", countOutputName: "save_infer_model/scale_1.tmp_0", hasDynamicBatchAxis: true, paddleCountShape: PortableDetectorCountShape.BatchVector);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETR(new ModelId("external/rtdetr-vector-count"), options);
            Run(profile, path, new[]
            {
                new NamedTensor("im_shape", new Tensor<float>(new TensorShape(1, 2), new[] { 640f, 640f })),
                new NamedTensor("scale_factor", new Tensor<float>(new TensorShape(1, 2), new[] { 1f, 1f }))
            });
        }

        private static void RunRtRaw()
        {
            const string path = @"E:\Model\RT-DETR\RTDETR_cropping\rtdetr_r50vd_6x_coco.onnx";
            RequireFile(path);
            var options = new PortableDetectorProfileOptions(16, new VisualSize(640, 640), YoloLabelSets.Coco80, inputName: "image", upstreamRepository: "local-authorized-read-only", upstreamCommit: "external-review-required", exporterVersion: "inspected-paddle2onnx-raw", license: "External", scoreThreshold: .01f, maximumCandidates: 300, maximumResults: 300, topK: 300, boxesOutputName: "stack_7.tmp_0_slice_0", labelsOutputName: "stack_8.tmp_0_slice_0", rfDetrQueryCount: 300, hasDynamicBatchAxis: true);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreateRTDETRRaw(new ModelId("external/rtdetr-raw"), options);
            Run(profile, path, Array.Empty<NamedTensor>());
        }

        private static void RunPp()
        {
            const string path = @"E:\Model\ppyoloe\ppyoloe_plus_crn_l_80e_coco.onnx";
            RequireFile(path);
            PortableDetectorProfile profile = PortableDetectorProfiles.CreatePPYOLOE(new ModelId("external/ppyoloe"), Options(11, new VisualSize(640, 640), YoloLabelSets.Coco80, "image"));
            Run(profile, path, new[] { new NamedTensor("scale_factor", new Tensor<float>(new TensorShape(1, 2), new[] { 1f, 1f })) });
        }

        private static void Run(PortableDetectorProfile profile, string path, IEnumerable<NamedTensor> auxiliary)
        {
            var artifact = new ModelArtifact(profile.VisualProfile.ModelId, profile.VisualProfile.ModelFormat, path, preferredBackend: OnnxRuntimeBackendProvider.BackendId);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.VisualProfile.Task), request);
            int size = (int)profile.VisualProfile.Input.ShapePattern[2];
            var visualSize = new VisualSize(size, size);
            using var input = new PreparedVisualInput(profile.VisualProfile.Input.Name, new Tensor<float>(new TensorShape(1, 3, size, size), new float[checked(size * size * 3)]), visualSize, visualSize, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(visualSize, visualSize), auxiliaryInputs: auxiliary);
            VisualInferenceResult result = pipeline.Run(input);
            Assert.AreEqual(profile.VisualProfile.Task, result.Task);
            Assert.IsNotNull(result.Value);
        }

        private static PortableDetectorProfileOptions Options(int opset, VisualSize size, IEnumerable<string> labels, string inputName, string? masksName = null, int queryCount = -1, bool includesNoObjectClass = false)
        {
            return new PortableDetectorProfileOptions(opset, size, labels, inputName: inputName, upstreamRepository: "local-authorized-read-only", upstreamCommit: "external-review-required", exporterVersion: "external-review-required", license: "External", scoreThreshold: .01f, maximumCandidates: 3000, maximumResults: 300, topK: 300, masksOutputName: masksName, rfDetrQueryCount: queryCount, rfDetrIncludesNoObjectClass: includesNoObjectClass);
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_DETR_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_DETR_RUN_EXTERNAL=1 to run the authorized local model matrix.");
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured local model does not exist: " + path);
        }
    }
}
