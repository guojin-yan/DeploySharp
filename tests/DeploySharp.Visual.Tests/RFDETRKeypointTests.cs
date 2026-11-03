using System;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class RFDETRKeypointTests
    {
        private static VisualModelProfile Profile(bool dynamicBatch = false) => RFDETRKeypointProfiles.CreatePreview(new ModelId("tests/rfdetr/keypoint"),
            new PortableDetectorProfileOptions(17, new VisualSize(576, 576), new[] { "background", "person" }, maximumResults: 100, topK: 100, hasDynamicBatchAxis: dynamicBatch));

        [TestMethod]
        public void PreviewBindsExactPortsAndRejectsDynamicBatchAndWrongSchema()
        {
            VisualModelProfile profile = Profile();
            Assert.AreEqual(VisualTaskId.PoseEstimation, profile.Task);
            Assert.AreEqual(new TensorShape(1, 100, 34, 8), profile.Outputs.Single(value => value.Name == "keypoints").ShapePattern);
            Assert.AreEqual(VisualResizeMode.Resize, profile.Preprocessing!.ResizeMode);
            Assert.ThrowsExactly<ArgumentException>(() => Profile(true));
            Assert.ThrowsExactly<ArgumentException>(() => RFDETRKeypointProfiles.CreatePreview(new ModelId("x"), new PortableDetectorProfileOptions(17, new VisualSize(576, 576), new[] { "person" })));
        }

        [TestMethod]
        public void PrecisionFusionMatchesAnalyticGoldenAndReturnsOwnedSourceGeometry()
        {
            using PreparedVisualInput input = Input();
            float[] points = Points(0);
            var result = Decode(input, points, new[] { -10f, 0f, -10f, -10f }, threshold: .3f);
            Assert.HasCount(1, result.Instances);
            PoseInstance pose = result.Instances[0];
            double fused = .5 * Math.Pow(2, -.2);
            Assert.AreEqual((float)(fused / (1 + fused)), pose.Score, 1e-6f);
            Assert.AreEqual(50f, pose.Keypoints[0].Point.X, 1e-4f);
            Assert.AreEqual(75f, pose.Keypoints[0].Point.Y, 1e-4f);
            Assert.AreEqual(.5f, pose.Keypoints[0].Score);
            Assert.AreEqual(PoseKeypointVisibility.Unknown, pose.Keypoints[0].Visibility);
            Assert.AreEqual(1, pose.ClassIndex);
            points[17 * 8] = 0;
            Assert.AreEqual(50f, pose.Keypoints[0].Point.X, 1e-4f);
        }

        [TestMethod]
        public void ThresholdAndResultLimitRunAfterFusionAndRanking()
        {
            using PreparedVisualInput input = Input();
            float[] points = Points(0);
            for (int point = 0; point < 17; point++)
            {
                int offset = (34 + 17 + point) * 8;
                points[offset + 4] = 8;
                points[offset + 6] = 8;
            }
            var result = Decode(input, points, new[] { -10f, 2f, -10f, -1.4f }, threshold: .4f, maximum: 1);
            Assert.HasCount(1, result.Instances);
            Assert.AreEqual(3, result.Instances[0].SourceIndex);
            Assert.IsGreaterThan(.4f, result.Instances[0].Score);
        }

        [TestMethod]
        public void TopKIncludesBackgroundBeforeDiscardingItAndThresholdIsStrict()
        {
            using PreparedVisualInput input = Input();
            Assert.HasCount(0, Decode(input, Points(0), new[] { 20f, 2f, -10f, -10f }, topK: 1).Instances);
            Assert.HasCount(0, Decode(input, Points(0), new[] { -10f, 0f, -10f, 0f }, threshold: .5f, traceAlpha: 0).Instances);
        }

        [TestMethod]
        public void LetterboxRestorationUsesModelCanvasAndPreservesOutsideKeypoints()
        {
            using PreparedVisualInput input = Input(letterbox: true);
            PoseInstance pose = Decode(input, Points(0), new[] { -10f, 2f, -10f, -10f }).Instances[0];
            Assert.AreEqual(50f, pose.Keypoints[0].Point.X, 1e-4f);
            Assert.AreEqual(100f, pose.Keypoints[0].Point.Y, 1e-4f);
        }

        [TestMethod]
        public void InvalidShapeNonFiniteAndCancellationFailWithStableDiagnostics()
        {
            using PreparedVisualInput input = Input();
            float[] points = Points(0);
            points[0] = float.NaN;
            VisualException invalid = Assert.ThrowsExactly<VisualException>(() => Decode(input, points, new[] { -10f, 2f, -10f, -10f }));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, invalid.ErrorCode);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("dets", new Tensor<float>(new TensorShape(1, 2, 4), new float[8])),
                new NamedTensor("labels", new Tensor<float>(new TensorShape(1, 2, 2), new float[4])),
                new NamedTensor("keypoints", new Tensor<float>(new TensorShape(1, 2, 34, 7), new float[2 * 34 * 7]))
            });
            Assert.ThrowsExactly<VisualException>(() => new RFDETRKeypointDecoder(queryCount: 2, topK: 2, maximumResults: 2).Decode(new VisualDecodeContext(input, Profile(), outputs, CancellationToken.None)));
            Assert.ThrowsExactly<OperationCanceledException>(() => Decode(input, Points(0), new[] { -10f, 2f, -10f, -10f }, cancellationToken: new CancellationToken(true)));
        }

        [TestMethod]
        public void RoiTransformRestoresFullSourceAndExistingPoseFilterAcceptsTheResult()
        {
            var source = new VisualSize(400, 300);
            var model = new VisualSize(576, 576);
            using var input = new PreparedVisualInput("input", new Tensor<float>(new TensorShape(1, 3, 576, 576), new float[3 * 576 * 576]), source, model, 1, VisualTensorLayout.Nchw,
                new ImageTransform(ImageTransformKind.Custom, source, model, 576f / 200, 576f / 100, -288, -288));
            PoseEstimationResult result = Decode(input, Points(0), new[] { -10f, 2f, -10f, -10f });
            Assert.AreEqual(150f, result.Instances[0].Keypoints[0].Point.X, 1e-4f);
            Assert.AreEqual(125f, result.Instances[0].Keypoints[0].Point.Y, 1e-4f);
            var include = new VisualRoi("person-area", new RectangleRoiGeometry(new RectangleF(140, 115, 20, 20)), hitTestMode: RoiHitTestMode.AllKeypoints, taskFilter: new[] { VisualTaskId.PoseEstimation });
            Assert.HasCount(1, VisualRoiPoseFilter.Filter(result, new VisualRoiSnapshot(source, new[] { include })).Instances);
            var exclude = new VisualRoi("excluded", include.Geometry, inclusionMode: RoiInclusionMode.Exclude, hitTestMode: RoiHitTestMode.AnyKeypoint, taskFilter: new[] { VisualTaskId.PoseEstimation });
            Assert.HasCount(0, VisualRoiPoseFilter.Filter(result, new VisualRoiSnapshot(source, new[] { include, exclude })).Instances);
        }

        private static PoseEstimationResult Decode(PreparedVisualInput input, float[] points, float[] logits, float threshold = .2f, int topK = 2, int maximum = 1, float traceAlpha = .2f, CancellationToken cancellationToken = default)
        {
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("dets", new Tensor<float>(new TensorShape(1, 2, 4), new[] { .5f, .5f, .5f, .5f, .5f, .5f, .5f, .5f })),
                new NamedTensor("labels", new Tensor<float>(new TensorShape(1, 2, 2), logits)),
                new NamedTensor("keypoints", new Tensor<float>(new TensorShape(1, 2, 34, 8), points))
            });
            return (PoseEstimationResult)new RFDETRKeypointDecoder(queryCount: 2, scoreThreshold: threshold, topK: topK, maximumResults: maximum, traceAlpha: traceAlpha).Decode(new VisualDecodeContext(input, Profile(), outputs, cancellationToken));
        }

        private static float[] Points(float precision)
        {
            var result = new float[2 * 34 * 8];
            for (int query = 0; query < 2; query++) for (int point = 0; point < 17; point++)
            {
                int offset = (query * 34 + 17 + point) * 8;
                result[offset] = .25f; result[offset + 1] = .75f;
                result[offset + 4] = precision; result[offset + 6] = precision;
            }
            return result;
        }

        private static PreparedVisualInput Input(bool letterbox = false)
        {
            var source = new VisualSize(200, 100);
            var model = new VisualSize(576, 576);
            return new PreparedVisualInput("input", new Tensor<float>(new TensorShape(1, 3, 576, 576), new float[3 * 576 * 576]), source, model, 1, VisualTensorLayout.Nchw,
                letterbox ? ImageTransform.Letterbox(source, model) : ImageTransform.Resize(source, model));
        }
    }
}
