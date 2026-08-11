using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>Runs the user-authorized portable detector artifacts through real OpenCV and CPU backends. / 通过真实 OpenCV 与 CPU 后端运行用户授权的便携检测工件。</summary>
    [TestClass]
    public sealed class OpenCvPortableDetectorIntegrationTests
    {
        [TestMethod]
        [TestCategory("ExternalModels")]
        public void FourRunnableOnnxArtifactsUseOfficialOpenCvContractsOnOnnxRuntimeCpu()
        {
            Require("DEPLOYSHARP_DETR_RUN_EXTERNAL");
            RunMatrix(OnnxRuntimeBackendProvider.BackendId, "cpu", includeRtDetr: false);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RfDetrDetectionAndSegmentationUseOfficialOpenCvContractsOnOpenVinoCpu()
        {
            Require("DEPLOYSHARP_DETR_RUN_EXTERNAL_OPENVINO");
            RunMatrix(OpenVinoBackendProvider.BackendId, "CPU", includeRtDetr: true);
        }

        private static void RunMatrix(BackendId backendId, string device, bool includeRtDetr)
        {
            string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_DETR_IMAGE") ?? @"E:\Data\image\bus.jpg";
            if (!File.Exists(imagePath)) Assert.Inconclusive("The configured local validation image does not exist: " + imagePath);
            // OpenVINO 3.3.0 loses several external port aliases and PP-YOLOE is rejected by the current CPU plug-in.
            // OpenVINO 3.3.0 会丢失若干外部端口别名，且当前 CPU 插件会拒绝 PP-YOLOE。
            IReadOnlyList<PortableCase> cases = Cases().Where(item => backendId == OpenVinoBackendProvider.BackendId ? item.Family == PortableDetectorFamily.RFDETRDet || item.Family == PortableDetectorFamily.RFDETRSeg : includeRtDetr || item.Family != PortableDetectorFamily.RTDETRDet).ToArray();
            foreach (PortableCase item in cases) if (!File.Exists(item.Path)) Assert.Inconclusive("The configured local model does not exist: " + item.Path);

            var summaries = new List<string>(cases.Count);
            foreach (PortableCase item in cases)
            {
                Console.WriteLine("Starting " + item.Family.ToString());
                PortableDetectorProfile profile = item.CreateProfile(backendId);
                using var registry = new BackendRegistry();
                if (backendId == OnnxRuntimeBackendProvider.BackendId) registry.UseOnnxRuntime();
                else registry.UseOpenVino();
                var profiles = new VisualProfileRegistry();
                profiles.Register(profile.VisualProfile);
                profiles.Freeze();
                var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
                using var pipeline = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(item.Path, backendId), registry, request, profile.VisualProfile.Task), request);
                using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), imagePath, profile);
                VisualInferenceResult result = pipeline.Run(input);
                Assert.AreEqual(profile.VisualProfile.Task, result.Task);
                Assert.IsNotNull(result.Value);
                summaries.Add(string.Format(CultureInfo.InvariantCulture, "{0}:backend={1};pre={2:F2};infer={3:F2};decode={4:F2};result={5}", item.Family, result.BackendId, result.Timing.Preprocessing.TotalMilliseconds, result.Timing.Inference.TotalMilliseconds, result.Timing.Postprocessing.TotalMilliseconds, Describe(result.Value)));
            }

            Assert.AreEqual(backendId == OpenVinoBackendProvider.BackendId ? 2 : 4, summaries.Count);
            Console.WriteLine(string.Join(Environment.NewLine, summaries));
        }

        private static string Describe(object value)
        {
            if (value is DetectionResult detection) return "detections=" + detection.Detections.Count.ToString(CultureInfo.InvariantCulture);
            if (value is InstanceSegmentationResult segmentation) return "instances=" + segmentation.Instances.Count.ToString(CultureInfo.InvariantCulture);
            return value.GetType().Name;
        }

        private static IReadOnlyList<PortableCase> Cases()
        {
            return new[]
            {
                new PortableCase(PortableDetectorFamily.DEIMv2Det, @"E:\Model\DEIMv2\DEIMv2.onnx", "08a6a9052c83ccd356e91f8839dfe7b2e686639b577feb7f0b7b204f7f2969cc", 16, new VisualSize(640, 640), YoloLabelSets.Coco80, "images", null, -1, false),
                new PortableCase(PortableDetectorFamily.RFDETRDet, @"E:\Model\rf-detr\rf-detr.onnx", "b464822e768f5795f249a6bd08cf1c5299787806c740204ed8e46d3a369ab769", 17, new VisualSize(512, 512), Labels(5), "input", null, 300, true),
                new PortableCase(PortableDetectorFamily.RFDETRSeg, @"E:\Model\rf-detr\rf-detr-seg.onnx", "6156aaff01ea0da0a007b29157fa34bf512d99d9e6a872cad70ae28cd08d6a35", 17, new VisualSize(432, 432), Labels(90), "input", "4245", 200, true),
                new PortableCase(PortableDetectorFamily.RTDETRDet, @"E:\Model\RT-DETR\RTDETR\rtdetr_r50vd_6x_coco.onnx", "6769a122fd045ab68e427f6651326dac8cac8d2983d43cd512a5e243fb13e94b", 16, new VisualSize(640, 640), YoloLabelSets.Coco80, "image", null, -1, false),
                new PortableCase(PortableDetectorFamily.PPYOLOEDet, @"E:\Model\ppyoloe\ppyoloe_plus_crn_l_80e_coco.onnx", "68866d9841e41f6637d4a1c13db6c70a42c9f0367c79870b0a8a9e9df32b8504", 11, new VisualSize(640, 640), YoloLabelSets.Coco80, "image", null, -1, false)
            };
        }

        private static IEnumerable<string> Labels(int count) => Enumerable.Range(0, count).Select(index => "class" + index.ToString(CultureInfo.InvariantCulture));

        private static void Require(string variable)
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(variable), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set " + variable + "=1 to run the authorized local portable-detector matrix.");
        }

        private sealed class PortableCase
        {
            internal PortableCase(PortableDetectorFamily family, string path, string sha256, int opset, VisualSize size, IEnumerable<string> labels, string inputName, string? masksName, int queryCount, bool includesNoObjectClass)
            {
                Family = family; Path = path; Sha256 = sha256; Opset = opset; Size = size; Labels = labels.ToArray(); InputName = inputName; MasksName = masksName; QueryCount = queryCount; IncludesNoObjectClass = includesNoObjectClass;
            }

            internal PortableDetectorFamily Family { get; }
            internal string Path { get; }
            internal string Sha256 { get; }
            internal int Opset { get; }
            internal VisualSize Size { get; }
            internal IReadOnlyList<string> Labels { get; }
            internal string InputName { get; }
            internal string? MasksName { get; }
            internal int QueryCount { get; }
            internal bool IncludesNoObjectClass { get; }

            internal PortableDetectorProfile CreateProfile(BackendId backendId)
            {
                bool openVino = backendId == OpenVinoBackendProvider.BackendId;
                string inputName = openVino && (Family == PortableDetectorFamily.RFDETRDet || Family == PortableDetectorFamily.RFDETRSeg) ? "/backbone/backbone.0/encoder/encoder/embeddings/Cast_output_0" : InputName;
                string? masksName = openVino && Family == PortableDetectorFamily.RFDETRSeg ? "/segmentation_head/Einsum_output_0" : MasksName;
                var options = new PortableDetectorProfileOptions(Opset, Size, Labels, inputName: inputName, artifactSha256: Sha256, upstreamRepository: "local-authorized-read-only", upstreamCommit: "external-review-required", exporterVersion: "inspected-onnx", license: "External", scoreThreshold: .4f, maximumCandidates: 3000, maximumResults: 100, topK: 300, masksOutputName: masksName, rfDetrQueryCount: QueryCount, rfDetrIncludesNoObjectClass: IncludesNoObjectClass);
                var id = new ModelId("external/" + Family.ToString().ToLowerInvariant());
                if (Family == PortableDetectorFamily.DEIMv2Det) return PortableDetectorProfiles.CreateDEIMv2(id, options);
                if (Family == PortableDetectorFamily.RFDETRDet) return PortableDetectorProfiles.CreateRFDETR(id, options);
                if (Family == PortableDetectorFamily.RFDETRSeg) return PortableDetectorProfiles.CreateRFDETRSeg(id, options);
                if (Family == PortableDetectorFamily.RTDETRDet) return PortableDetectorProfiles.CreateRTDETR(id, options);
                return PortableDetectorProfiles.CreatePPYOLOE(id, options);
            }
        }
    }
}
