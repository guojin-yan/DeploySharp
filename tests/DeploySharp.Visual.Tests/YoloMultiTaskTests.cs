using System;
using System.Threading;
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
    public sealed class YoloMultiTaskTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [TestMethod]
        public void ClassificationProfileBindsV1YoloClsToProbabilitiesAndCenterCrop()
        {
            var profile = YoloMultiTaskProfiles.CreateClassification(
                new ModelId("tests/yolo-cls"), Sha, new[] { "zero", "one", "two" }, "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6",
                new YoloClassificationProfileOptions(17, new VisualSize(2, 2), topK: 2));
            Assert.AreEqual("https://github.com/ultralytics/ultralytics", profile.UpstreamRepository);
            Assert.AreEqual(YoloImageResizeMode.CenterCrop, profile.Preprocessing.ResizeMode);
            Assert.AreEqual("ultralytics-exported-probabilities-v1", profile.PostprocessingVersion);
            var outputs = InferenceOutputs.Create("output0", new Tensor<float>(new TensorShape(1, 3), new[] { .1f, .8f, .2f }));
            using var input = VisualTestData.ClassificationInput();
            var context = new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None);
            var result = profile.VisualProfile.Decoder.Decode(context) as JYPPX.DeploySharp.Results.Vision.ClassificationResult;
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result!.Predictions.Count);
            Assert.AreEqual(1, result.Predictions[0].Index);
            Assert.AreEqual(.8f, result.Predictions[0].Score, .00001f);
        }

        [TestMethod]
        public void PackedV8SegmentationReconstructsOwnedMask()
        {
            var options = new YoloPackedProfileOptions(12, 2, new VisualSize(16, 16), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 2, maximumDetections: 2));
            var profile = YoloMultiTaskProfiles.CreateInstanceSegmentation(YoloDetectionFamily.YoloV8, new ModelId("tests/yolo-v8-seg"), Sha, new[] { "zero", "one" }, "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6", options);
            int fields = 4 + 2 + 32;
            var packed = new float[fields * 2];
            packed[0] = 8; packed[2] = 8; packed[4] = 8; packed[6] = 8; packed[4 * 2] = .9f;
            packed[1] = 8; packed[3] = 8; packed[5] = 8; packed[7] = 8; packed[4 * 2 + 1] = .1f;
            int coefficientBase = (4 + 2) * 2;
            packed[coefficientBase] = 1f;
            var prototypes = new float[32 * 4 * 4];
            for (int index = 0; index < 16; index++) prototypes[index] = 10f;
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("output0", new Tensor<float>(new TensorShape(1, fields, 2), packed)),
                new NamedTensor("output1", new Tensor<float>(new TensorShape(1, 32, 4, 4), prototypes))
            });
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 16, 16), new float[768]), new VisualSize(16, 16), new VisualSize(16, 16), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(16, 16), new VisualSize(16, 16)));
            var result = (JYPPX.DeploySharp.Visual.InstanceSegmentationResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual("zero", result.Instances[0].Label);
            Assert.IsTrue(result.Instances[0].Mask.IsForeground(8, 8));
            Assert.AreEqual(256, result.Instances[0].Mask.ToArray().Length);
        }

        [TestMethod]
        public void EndToEndPoseAndObbExposeCanonicalOwnedResults()
        {
            var packedOptions = new YoloPackedProfileOptions(19, 1, new VisualSize(16, 16), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 1, maximumDetections: 1));
            var pose = YoloMultiTaskProfiles.CreatePose(YoloDetectionFamily.YoloV26, new ModelId("tests/yolo-v26-pose"), Sha, "6f6158be448c73471c000cf41db5cd9169300ed9", "8.4.0", packedOptions);
            var poseValues = new float[57];
            poseValues[0] = 2; poseValues[1] = 2; poseValues[2] = 14; poseValues[3] = 14; poseValues[4] = .9f; poseValues[5] = 0;
            for (int keypoint = 0; keypoint < 17; keypoint++) { poseValues[6 + (keypoint * 3)] = 8; poseValues[7 + (keypoint * 3)] = 8; poseValues[8 + (keypoint * 3)] = .9f; }
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 16, 16), new float[768]), new VisualSize(16, 16), new VisualSize(16, 16), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(16, 16), new VisualSize(16, 16)));
            var poseOutputs = InferenceOutputs.Create("output0", new Tensor<float>(new TensorShape(1, 1, 57), poseValues));
            var poseResult = (PoseEstimationResult)pose.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, pose.VisualProfile, poseOutputs, CancellationToken.None));
            Assert.AreEqual(1, poseResult.Instances.Count);
            Assert.AreEqual(17, poseResult.Instances[0].Keypoints.Count);
            Assert.AreEqual(0, poseResult.Instances[0].ClassIndex);

            var obb = YoloMultiTaskProfiles.CreateObb(YoloDetectionFamily.YoloV26, new ModelId("tests/yolo-v26-obb"), Sha, YoloLabelSets.Dota15, "6f6158be448c73471c000cf41db5cd9169300ed9", "8.4.0", packedOptions);
            var obbOutputs = InferenceOutputs.Create("output0", new Tensor<float>(new TensorShape(1, 1, 7), new[] { 8f, 8f, 6f, 4f, .9f, 0f, .2f }));
            var obbResult = (JYPPX.DeploySharp.Visual.OrientedDetectionResult)obb.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, obb.VisualProfile, obbOutputs, CancellationToken.None));
            Assert.AreEqual(1, obbResult.Detections.Count);
            Assert.AreEqual("plane", obbResult.Detections[0].Label);
            Assert.IsTrue(obbResult.Detections[0].HasExactRotatedRectangle);
            Assert.AreEqual(4, obbResult.Detections[0].Quadrilateral.Vertices.Count);
        }
    }
}
