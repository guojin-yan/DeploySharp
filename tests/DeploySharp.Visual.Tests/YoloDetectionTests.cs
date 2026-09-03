using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class YoloDetectionTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [TestMethod]
        public void V1DetectionFamiliesExposeFourExplicitExportContracts()
        {
            var expected = new Dictionary<YoloDetectionFamily, (YoloDetectionOutputKind Kind, string Name, string Shape)>
            {
                [YoloDetectionFamily.YoloV5] = (YoloDetectionOutputKind.RawCandidateMajor, "output0", "[1,-1,85]"),
                [YoloDetectionFamily.YoloV6] = (YoloDetectionOutputKind.RawCandidateMajor, "outputs", "[1,-1,85]"),
                [YoloDetectionFamily.YoloV7] = (YoloDetectionOutputKind.BatchedEndToEnd, "output", "[-1,7]"),
                [YoloDetectionFamily.YoloV8] = (YoloDetectionOutputKind.RawAttributeMajor, "output0", "[1,84,-1]"),
                [YoloDetectionFamily.YoloV9] = (YoloDetectionOutputKind.RawAttributeMajor, "output0", "[1,84,-1]"),
                [YoloDetectionFamily.YoloV10] = (YoloDetectionOutputKind.EndToEnd, "output0", "[1,-1,6]"),
                [YoloDetectionFamily.YoloV11] = (YoloDetectionOutputKind.RawAttributeMajor, "output0", "[1,84,-1]"),
                [YoloDetectionFamily.YoloV12] = (YoloDetectionOutputKind.RawAttributeMajor, "output0", "[1,84,-1]"),
                [YoloDetectionFamily.YoloV13] = (YoloDetectionOutputKind.RawAttributeMajor, "output0", "[1,84,-1]"),
                [YoloDetectionFamily.YoloV26] = (YoloDetectionOutputKind.EndToEnd, "output0", "[1,-1,6]")
            };

            foreach (KeyValuePair<YoloDetectionFamily, (YoloDetectionOutputKind Kind, string Name, string Shape)> item in expected)
            {
                YoloDetectionProfile profile = Profile(item.Key);
                Assert.AreEqual(item.Value.Kind, profile.Output.Kind, item.Key.ToString());
                Assert.AreEqual(item.Value.Name, profile.Output.OutputName, item.Key.ToString());
                Assert.AreEqual(item.Value.Shape, profile.VisualProfile.Outputs[0].ShapePattern.ToString(), item.Key.ToString());
                Assert.AreEqual(80, profile.VisualProfile.Labels.Count);
                Assert.AreEqual("person", profile.VisualProfile.GetLabel(0));
                Assert.AreEqual("toothbrush", profile.VisualProfile.GetLabel(79));
                Assert.AreEqual("onnx", profile.VisualProfile.ModelFormat);
                Assert.AreEqual(12, profile.Opset);
                Assert.IsFalse(profile.DynamicShapes);
                Assert.AreEqual("ultralytics-letterbox-rgb-nchw-v1", profile.PreprocessingVersion);
                Assert.AreEqual("deploysharp-yolo-detection-v1", profile.PostprocessingVersion);
            }
        }

        [TestMethod]
        public void ArtifactCanExplicitlyBindAnAlternatePhysicalYoloOutputContract()
        {
            YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV7,
                new ModelId("models/yolov7-opencv-raw"),
                Sha,
                YoloLabelSets.Coco80,
                "0123456789abcdef",
                "test-exporter",
                new YoloDetectionProfileOptions(
                    12,
                    outputName: "onnx_node!/model/model.105/Concat_3",
                    outputKind: YoloDetectionOutputKind.RawCandidateMajor));

            Assert.AreEqual(YoloDetectionOutputKind.RawCandidateMajor, profile.Output.Kind);
            Assert.AreEqual("onnx_node!/model/model.105/Concat_3", profile.Output.OutputName);
            Assert.AreEqual("[1,-1,85]", profile.VisualProfile.Outputs[0].ShapePattern.ToString());
        }

        [TestMethod]
        public void RawYoloProfileCanDecodeTrueDynamicBatchWithPerFrameTransforms()
        {
            YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV8,
                new ModelId("tests/yolo-dynamic"),
                Sha,
                new[] { "cat", "dog" },
                "commit",
                "test-exporter",
                new YoloDetectionProfileOptions(12, new VisualSize(100, 100), outputKind: YoloDetectionOutputKind.RawCandidateMajor, maximumBatch: 2));
            Assert.AreEqual("[-1,-1,7]", profile.VisualProfile.Outputs[0].ShapePattern.ToString());

            var sourceOne = new VisualSize(100, 100);
            var sourceTwo = new VisualSize(200, 100);
            var model = new VisualSize(100, 100);
            var frames = new[]
            {
                new VisualInputFrame(sourceOne, model, ImageTransform.Resize(sourceOne, model)),
                new VisualInputFrame(sourceTwo, model, ImageTransform.Letterbox(sourceTwo, model))
            };
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 100, 100), new float[60000]), sourceOne, model, 2, VisualTensorLayout.Nchw, ImageTransform.Resize(sourceOne, model), batchFrames: frames);
            var output = InferenceOutputs.Create("output0", new Tensor<float>(new TensorShape(2, 1, 7), new[]
            {
                50f, 50f, 40f, 20f, 1f, .9f, .1f,
                50f, 50f, 100f, 100f, 1f, .1f, .9f
            }));

            var result = (DetectionBatchResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, output, CancellationToken.None));

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(0, result[0].Detections[0].Label.Index);
            Assert.AreEqual(1, result[1].Detections[0].Label.Index);
            Assert.AreEqual(40f, result[0].Detections[0].Box.Width, .001f);
            Assert.AreEqual(200f, result[1].Detections[0].Box.Width, .001f);
        }

        [TestMethod]
        public void CandidateMajorRawHeadMultipliesObjectnessAndUsesBestClass()
        {
            var contract = new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.RawCandidateMajor, 2);
            var decoder = new YoloDetectionDecoder(contract, new YoloDetectionDecoderOptions(scoreThreshold: .25f));
            DetectionResult result = Decode(decoder, contract, new Tensor<float>(new TensorShape(1, 1, 7), new[] { 50f, 50f, 40f, 20f, .5f, .2f, .8f }));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual(1, result.Detections[0].Label.Index);
            Assert.AreEqual(.4f, result.Detections[0].Label.Score, .00001f);
            AssertBox(result.Detections[0].Box, 30, 40, 40, 20);
        }

        [TestMethod]
        public void AttributeMajorRawHeadUsesDeterministicModelSpaceNms()
        {
            var contract = new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.RawAttributeMajor, 2);
            var decoder = new YoloDetectionDecoder(contract, new YoloDetectionDecoderOptions(scoreThreshold: .25f, iouThreshold: .5f));
            float[] values =
            {
                50f, 52f,
                50f, 52f,
                40f, 40f,
                20f, 20f,
                .9f, .8f,
                .1f, .2f
            };
            DetectionResult result = Decode(decoder, contract, new Tensor<float>(new TensorShape(1, 6, 2), values));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual(.9f, result.Detections[0].Label.Score, .00001f);
            AssertBox(result.Detections[0].Box, 30, 40, 40, 20);
        }

        [TestMethod]
        public void MultiLabelModeKeepsDistinctClassesBeforeClassAwareNms()
        {
            var contract = new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.RawAttributeMajor, 2);
            var options = new YoloDetectionDecoderOptions(scoreThreshold: .25f, classSelection: YoloClassSelectionMode.MultiLabel);
            var decoder = new YoloDetectionDecoder(contract, options);
            DetectionResult result = Decode(decoder, contract, new Tensor<float>(new TensorShape(1, 6, 1), new[] { 50f, 50f, 40f, 20f, .9f, .8f }));
            Assert.AreEqual(2, result.Detections.Count);
            Assert.AreEqual(0, result.Detections[0].Label.Index);
            Assert.AreEqual(1, result.Detections[1].Label.Index);
        }

        [TestMethod]
        public void BatchedEndToEndFiltersOtherBatchesWithoutRunningNms()
        {
            var contract = new YoloDetectionOutputContract("output", YoloDetectionOutputKind.BatchedEndToEnd, 2);
            var decoder = new YoloDetectionDecoder(contract, new YoloDetectionDecoderOptions(scoreThreshold: .25f));
            float[] values =
            {
                0, 10, 20, 50, 60, 1, .9f,
                1, 10, 20, 50, 60, 1, .95f,
                0, 10, 20, 50, 60, 0, .1f
            };
            DetectionResult result = Decode(decoder, contract, new Tensor<float>(new TensorShape(3, 7), values));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual(1, result.Detections[0].Label.Index);
            AssertBox(result.Detections[0].Box, 10, 20, 40, 40);
        }

        [TestMethod]
        public void EndToEndPreservesRowsAndDoesNotRepeatNms()
        {
            var contract = new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.EndToEnd, 2);
            var decoder = new YoloDetectionDecoder(contract, new YoloDetectionDecoderOptions(scoreThreshold: .25f, iouThreshold: 0f));
            float[] values =
            {
                10, 20, 50, 60, .9f, 1,
                10, 20, 50, 60, .8f, 1,
                0, 0, 0, 0, .25f, 0
            };
            DetectionResult result = Decode(decoder, contract, new Tensor<float>(new TensorShape(1, 3, 6), values));
            Assert.AreEqual(2, result.Detections.Count);
            Assert.AreEqual(.9f, result.Detections[0].Label.Score, .00001f);
            Assert.AreEqual(.8f, result.Detections[1].Label.Score, .00001f);
        }

        [TestMethod]
        public void SigmoidAndMalformedContractsHaveStableDiagnostics()
        {
            var sigmoidContract = new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.RawAttributeMajor, 1, YoloScoreActivation.Sigmoid);
            DetectionResult sigmoid = Decode(new YoloDetectionDecoder(sigmoidContract), sigmoidContract, new Tensor<float>(new TensorShape(1, 5, 1), new[] { 50f, 50f, 20f, 20f, 0f }));
            Assert.AreEqual(.5f, sigmoid.Detections[0].Label.Score, .00001f);

            var probabilityContract = new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.RawAttributeMajor, 1);
            VisualException range = Assert.ThrowsExactly<VisualException>(() => Decode(new YoloDetectionDecoder(probabilityContract), probabilityContract, new Tensor<float>(new TensorShape(1, 5, 1), new[] { 50f, 50f, 20f, 20f, 1.1f })));
            Assert.AreEqual(VisualErrorCodes.YoloContractInvalid, range.ErrorCode);
            VisualException shape = Assert.ThrowsExactly<VisualException>(() => Decode(new YoloDetectionDecoder(probabilityContract), probabilityContract, new Tensor<float>(new TensorShape(1, 1, 5), new[] { 50f, 50f, 20f, 20f, .9f })));
            Assert.AreEqual(VisualErrorCodes.YoloContractInvalid, shape.ErrorCode);
        }

        [TestMethod]
        public void ArtifactProfileRequiresProvenanceAndBindsSha()
        {
            var options = new YoloDetectionProfileOptions(19);
            Assert.ThrowsExactly<VisualException>(() => YoloDetectionProfiles.Create(YoloDetectionFamily.YoloV8, new ModelId("models/yolov8n"), "bad", YoloLabelSets.Coco80, "commit", "8.3.78", options));
            Assert.ThrowsExactly<VisualException>(() => YoloDetectionProfiles.Create(YoloDetectionFamily.YoloV8, new ModelId("models/yolov8n"), Sha, YoloLabelSets.Coco80, string.Empty, "8.3.78", options));
            Assert.ThrowsExactly<VisualException>(() => YoloDetectionProfiles.Create(YoloDetectionFamily.YoloV8, new ModelId("models/yolov8n"), Sha, YoloLabelSets.Coco80, "commit", "8.3.78", null!));
            YoloDetectionProfile profile = Profile(YoloDetectionFamily.YoloV8);
            ModelArtifact artifact = profile.CreateArtifact(@"C:\models\yolov8n.onnx", new BackendId("onnxruntime"));
            Assert.AreEqual(Sha, artifact.Sha256);
            Assert.AreEqual(profile.VisualProfile.ModelId, artifact.ModelId);
            Assert.AreEqual("onnx", profile.VisualProfile.ModelFormat);
            StringAssert.Contains(profile.VisualProfile.ProfileId, profile.VisualProfile.ModelId.Value);
            Assert.AreEqual("https://github.com/ultralytics/ultralytics", profile.UpstreamRepository);
        }

        [TestMethod]
        public void ProfileCanBindAConvertedOpenVinoIrArtifact()
        {
            const string sha = "065b06a5d8c60ab18bf0ccd0baa285e21f31c9e517042b79cd5d78971b1551a1";
            YoloDetectionProfile profile = YoloDetectionProfiles.Create(
                YoloDetectionFamily.YoloV8,
                new ModelId("tests/yolov8n-ir"),
                sha,
                YoloLabelSets.Coco80,
                "commit",
                "OpenVINO OVC 2025.4.0",
                new YoloDetectionProfileOptions(19, modelFormat: "openvino-ir"));

            ModelArtifact artifact = profile.CreateArtifact(@"C:\models\yolov8n.xml");
            Assert.AreEqual("openvino-ir", profile.VisualProfile.ModelFormat);
            Assert.AreEqual("openvino-ir", artifact.Format);
            Assert.AreEqual(sha, artifact.Sha256);
        }

        private static YoloDetectionProfile Profile(YoloDetectionFamily family)
        {
            return YoloDetectionProfiles.Create(
                family,
                new ModelId("models/" + family.ToString().ToLowerInvariant()),
                Sha,
                YoloLabelSets.Coco80,
                "0123456789abcdef",
                "test-exporter",
                new YoloDetectionProfileOptions(12, new VisualSize(100, 100)));
        }

        private static DetectionResult Decode(YoloDetectionDecoder decoder, YoloDetectionOutputContract contract, ITensor tensor)
        {
            var modelSize = new VisualSize(100, 100);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), modelSize, modelSize, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(modelSize, modelSize));
            var labels = new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one") };
            var profile = new VisualModelProfile(
                "tests.yolo",
                new ModelId("tests/yolo"),
                VisualTaskId.ObjectDetection,
                "2.0.0",
                "onnx",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(contract.OutputName, TensorElementType.Float32, new TensorShape(tensor.Shape.ToArray())) },
                labels,
                decoder);
            return (DetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, InferenceOutputs.Create(contract.OutputName, tensor), CancellationToken.None));
        }

        private static void AssertBox(RectangleF box, float x, float y, float width, float height)
        {
            Assert.AreEqual(x, box.X, .001f);
            Assert.AreEqual(y, box.Y, .001f);
            Assert.AreEqual(width, box.Width, .001f);
            Assert.AreEqual(height, box.Height, .001f);
        }
    }
}
