using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PoseDecoderTests
    {
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void TopologyAndResultsValidateSymmetryEdgesAndDefensiveCollections()
        {
            PoseTopology topology = Topology(withSigmas: true);
            Assert.AreEqual(3, topology.Keypoints.Count);
            Assert.AreEqual(2, topology.Edges.Count);
            Assert.ThrowsExactly<ArgumentException>(() => new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "left", mirroredIndex: 1),
                new PoseKeypointDefinition(1, "right")
            }));
            Assert.ThrowsExactly<ArgumentException>(() => new PoseTopology(topology.Keypoints, new[]
            {
                new PoseSkeletonEdge(0, 1), new PoseSkeletonEdge(1, 0)
            }));

            var sourcePoints = new List<PoseKeypoint>
            {
                new PoseKeypoint(0, new PointF(1, 2), 0.9f, PoseKeypointVisibility.Visible, true),
                new PoseKeypoint(1, new PointF(3, 4), 0.8f, PoseKeypointVisibility.Unknown, true),
                new PoseKeypoint(2, new PointF(5, 6), 0.7f, PoseKeypointVisibility.NotVisible, false)
            };
            var instance = new PoseInstance(0, 0.9f, sourcePoints, new RectangleF(0, 0, 10, 10));
            sourcePoints.Clear();
            Assert.AreEqual(3, instance.Keypoints.Count);
            var sourceInstances = new List<PoseInstance> { instance };
            var result = new PoseEstimationResult(topology, sourceInstances, new VisualSize(10, 10), "tests/pose.v1", new ModelId("tests/pose"));
            sourceInstances.Clear();
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(result.ComputeSha256(), result.ComputeSha256());
            Assert.ThrowsExactly<ArgumentException>(() => new PoseEstimationResult(topology, new[]
            {
                Instance(1, .8f, new[] { new PointF(0,0), new PointF(1,1), new PointF(2,2) }),
                Instance(0, .9f, new[] { new PointF(0,0), new PointF(1,1), new PointF(2,2) })
            }, new VisualSize(10, 10), "tests/pose.v1", new ModelId("tests/pose")));
        }

        [TestMethod]
        public void DirectDecoderUsesStableSortInverseResizeAndOksSuppression()
        {
            PoseTopology topology = Topology(withSigmas: true);
            var schema = new DirectPoseOutputSchema("keypoints", 3, 4, visibilityComponentIndex: 3, boxesOutputName: "boxes", instanceScoresOutputName: "scores");
            var options = new PoseDecoderOptions(instanceScoreThreshold: 0.1f, maximumCandidates: 3, maximumInstances: 3, oks: new PoseOksOptions(0.8f));
            var decoder = new DirectPoseDecoder(schema, topology, options);
            VisualModelProfile profile = DirectProfile(decoder, 3);
            float[] keypoints =
            {
                20,20,.9f,1, 30,30,.8f,1, 40,40,.7f,1,
                21,21,.9f,1, 31,31,.8f,1, 41,41,.7f,1,
                65,65,.8f,1, 75,75,.7f,1, 85,85,.6f,1
            };
            var outputs = Outputs(
                ("keypoints", new Tensor<float>(new TensorShape(1, 3, 3, 4), keypoints)),
                ("boxes", new Tensor<float>(new TensorShape(1, 3, 4), new float[] { 10,10,50,50, 11,11,51,51, 60,60,90,90 })),
                ("scores", new Tensor<float>(new TensorShape(1, 3), new[] { .9f, .8f, .9f })));
            using PreparedVisualInput input = Input(new VisualSize(200, 100), new VisualSize(100, 100), ImageTransform.Resize(new VisualSize(200, 100), new VisualSize(100, 100)));
            PoseEstimationResult result = Decode(decoder, profile, input, outputs);

            Assert.AreEqual(2, result.Instances.Count);
            Assert.AreEqual(0, result.Instances[0].SourceIndex);
            Assert.AreEqual(2, result.Instances[1].SourceIndex);
            Assert.AreEqual(40f, result.Instances[0].Keypoints[0].Point.X, 0.0001f);
            Assert.AreEqual(20f, result.Instances[0].Keypoints[0].Point.Y, 0.0001f);
            Assert.AreEqual(20f, result.Instances[0].BoundingBox!.Value.X, 0.0001f);
            Assert.AreEqual(80f, result.Instances[0].BoundingBox!.Value.Width, 0.0001f);
            Assert.AreEqual(PoseKeypointVisibility.Visible, result.Instances[0].Keypoints[0].Visibility);
        }

        [TestMethod]
        public void DirectCoordinateSpacesAndBoundaryModesAreExplicit()
        {
            PoseTopology topology = Topology();
            var normalizedSchema = new DirectPoseOutputSchema("keypoints", 3, 3, coordinateSpace: PoseCoordinateSpace.Normalized, gridMappingMode: PoseGridMappingMode.AlignCorners);
            var normalizedDecoder = new DirectPoseDecoder(normalizedSchema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            VisualModelProfile normalizedProfile = DirectProfile(normalizedDecoder, 1, includeBoxes: false, includeScores: false);
            using (PreparedVisualInput input = Input(new VisualSize(200, 100), new VisualSize(100, 100), ImageTransform.Resize(new VisualSize(200, 100), new VisualSize(100, 100))))
            {
                PoseEstimationResult result = Decode(normalizedDecoder, normalizedProfile, input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), new[] { .5f,.5f,1, 0,0,1, 1.1f,1.1f,1 }))));
                Assert.AreEqual(99f, result.Instances[0].Keypoints[0].Point.X, 0.001f);
                Assert.AreEqual(49.5f, result.Instances[0].Keypoints[0].Point.Y, 0.001f);
                Assert.IsFalse(result.Instances[0].Keypoints[2].IsValid);
            }

            var gridSchema = new DirectPoseOutputSchema("keypoints", 3, 3, coordinateSpace: PoseCoordinateSpace.TensorGrid, tensorGridSize: new VisualSize(10, 10));
            var gridDecoder = new DirectPoseDecoder(gridSchema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            VisualModelProfile gridProfile = DirectProfile(gridDecoder, 1, includeBoxes: false, includeScores: false);
            using (PreparedVisualInput input = Input(new VisualSize(100, 100), new VisualSize(100, 100), ImageTransform.Resize(new VisualSize(100, 100), new VisualSize(100, 100))))
            {
                PoseEstimationResult result = Decode(gridDecoder, gridProfile, input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), new[] { 0f,0f,1f, 1f,1f,1f, 9f,9f,1f }))));
                Assert.AreEqual(4.5f, result.Instances[0].Keypoints[0].Point.X, 0.001f);
                Assert.AreEqual(94.5f, result.Instances[0].Keypoints[2].Point.X, 0.001f);
            }

            var modelSchema = new DirectPoseOutputSchema("keypoints", 3, 3);
            VisualSize source = new VisualSize(200, 100);
            VisualSize model = new VisualSize(100, 100);
            ImageTransform letterbox = ImageTransform.Letterbox(source, model);
            float[] paddedPointValues = { 50,10,1, 50,50,1, 50,90,1 };
            var markDecoder = new DirectPoseDecoder(modelSchema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            using (PreparedVisualInput input = Input(source, model, letterbox))
            {
                PoseEstimationResult result = Decode(markDecoder, DirectProfile(markDecoder, 1, false, false), input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), paddedPointValues))));
                Assert.IsFalse(result.Instances[0].Keypoints[0].IsValid);
                Assert.IsTrue(result.Instances[0].Keypoints[1].IsValid);
            }
            var clipDecoder = new DirectPoseDecoder(modelSchema, topology, OneCandidateOptions(PoseBoundaryMode.Clip));
            using (PreparedVisualInput input = Input(source, model, letterbox))
            {
                PoseEstimationResult result = Decode(clipDecoder, DirectProfile(clipDecoder, 1, false, false), input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), paddedPointValues))));
                Assert.IsTrue(result.Instances[0].Keypoints[0].IsValid);
                Assert.AreEqual(0f, result.Instances[0].Keypoints[0].Point.Y, 0.001f);
            }

            ImageTransform crop = ImageTransform.Crop(new VisualSize(100, 100), new VisualSize(50, 50), new RectangleF(25, 25, 50, 50));
            using (PreparedVisualInput input = Input(new VisualSize(100, 100), new VisualSize(50, 50), crop))
            {
                PoseEstimationResult result = Decode(markDecoder, DirectProfile(markDecoder, 1, false, false, new VisualSize(50, 50)), input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), new[] { 0f,0f,1f, 25f,25f,1f, 49f,49f,1f }))));
                Assert.AreEqual(25f, result.Instances[0].Keypoints[0].Point.X, 0.001f);
                Assert.AreEqual(50f, result.Instances[0].Keypoints[1].Point.X, 0.001f);
            }
        }

        [TestMethod]
        public void HeatmapNchwAndNhwcUseStablePeaksAndNoImplicitActivation()
        {
            PoseTopology topology = Topology();
            var options = OneCandidateOptions(PoseBoundaryMode.MarkInvalid, keypointThreshold: -10f);
            var nchwSchema = new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw, PoseScoreKind.Raw, PoseGridMappingMode.AlignCorners);
            var nchwDecoder = new HeatmapPoseDecoder(nchwSchema, topology, options);
            VisualModelProfile nchwProfile = HeatmapProfile(nchwDecoder, new TensorShape(1, 3, 2, 2));
            float[] nchwValues = { .9f,0,0,0, .1f,.8f,.8f,0, -2,-2,-2,-1 };
            using PreparedVisualInput input = Input(new VisualSize(8, 8), new VisualSize(8, 8), ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)));
            PoseEstimationResult nchw = Decode(nchwDecoder, nchwProfile, input, Outputs(("heatmaps", new Tensor<float>(new TensorShape(1, 3, 2, 2), nchwValues))));
            Assert.AreEqual(0f, nchw.Instances[0].Keypoints[0].Point.X, 0.001f);
            Assert.AreEqual(7f, nchw.Instances[0].Keypoints[1].Point.X, 0.001f);
            Assert.AreEqual(0f, nchw.Instances[0].Keypoints[1].Point.Y, 0.001f);
            Assert.AreEqual(7f, nchw.Instances[0].Keypoints[2].Point.X, 0.001f);
            Assert.AreEqual(7f, nchw.Instances[0].Keypoints[2].Point.Y, 0.001f);
            Assert.AreEqual(-1f, nchw.Instances[0].Keypoints[2].Score, 0.001f);

            var nhwcSchema = new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nhwc, PoseScoreKind.Raw, PoseGridMappingMode.AlignCorners);
            var nhwcDecoder = new HeatmapPoseDecoder(nhwcSchema, topology, options);
            float[] nhwcValues = { .9f,.1f,-2, 0,.8f,-2, 0,.8f,-2, 0,0,-1 };
            PoseEstimationResult nhwc = Decode(nhwcDecoder, HeatmapProfile(nhwcDecoder, new TensorShape(1, 2, 2, 3)), input, Outputs(("heatmaps", new Tensor<float>(new TensorShape(1, 2, 2, 3), nhwcValues))));
            Assert.AreEqual(nchw.ComputeSha256(), nhwc.ComputeSha256());
        }

        [TestMethod]
        public void Float64DynamicHeatmapFlowsThroughFakeBackendAndVisibilityRemainsExplicit()
        {
            PoseTopology topology = Topology();
            var schema = new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw, PoseScoreKind.Probability, PoseGridMappingMode.AlignCorners, "pose_score");
            var decoder = new HeatmapPoseDecoder(schema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            var profile = new VisualModelProfile(
                "tests/dynamic-heatmap-pose.v1", new ModelId("tests/dynamic-heatmap-pose"), VisualTaskId.PoseEstimation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,8,8), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("heatmaps", TensorElementType.Float64, new TensorShape(1,3,-1,-1)),
                    new VisualOutputBinding("pose_score", TensorElementType.Float64, new TensorShape(1))
                },
                Array.Empty<VisualLabel>(), decoder);
            var metadata = new ModelMetadata(profile.ModelId, "fake", new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1,3,8,8)) }, new[]
            {
                new TensorDescriptor("heatmaps", TensorElementType.Float64, new TensorShape(1,3,-1,-1)),
                new TensorDescriptor("pose_score", TensorElementType.Float64, new TensorShape(1))
            });
            var outputs = Outputs(
                ("heatmaps", new Tensor<double>(new TensorShape(1,3,2,2), new[] { .9,0,0,0, .1,.8,.8,0, 0,0,0,.7 })),
                ("pose_score", new Tensor<double>(new TensorShape(1), new[] { .95 })));
            var provider = new FakeVisualBackendProvider(metadata, _ => outputs);
            using var registry = new BackendRegistry(); registry.Register(provider);
            var profiles = new VisualProfileRegistry(); profiles.Register(profile); profiles.Freeze();
            var artifact = new ModelArtifact(profile.ModelId, "fake", "fixture.fake", preferredBackend: VisualTestData.BackendId);
            var request = new BackendRequest(BackendCapabilities.TensorInference, VisualTestData.BackendId);
            using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.PoseEstimation), request);
            using PreparedVisualInput input = Input(new VisualSize(8,8), new VisualSize(8,8), ImageTransform.Resize(new VisualSize(8,8), new VisualSize(8,8)));
            PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(7f, result.Instances[0].Keypoints[2].Point.X, .0001f);
            Assert.AreEqual(PoseKeypointVisibility.Unknown, result.Instances[0].Keypoints[0].Visibility);
        }

        [TestMethod]
        public void DirectAndHeatmapDecodersReturnIndependentDynamicBatchRows()
        {
            PoseTopology topology = Topology();
            var directSchema = new DirectPoseOutputSchema("keypoints", 3, 3, boxesOutputName: "boxes", instanceScoresOutputName: "scores");
            var direct = new DirectPoseDecoder(directSchema, topology, new PoseDecoderOptions(instanceScoreThreshold: 0, maximumCandidates: 1, maximumInstances: 1));
            VisualModelProfile directProfile = new VisualModelProfile("tests/direct-pose-batch", new ModelId("tests/direct-pose-batch"), VisualTaskId.PoseEstimation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 8, 8), VisualTensorLayout.Nchw, 1, 2),
                new[] { new VisualOutputBinding("keypoints", TensorElementType.Float32, new TensorShape(-1, 1, 3, 3)), new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(-1, 1, 4)), new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 1)) }, Array.Empty<VisualLabel>(), direct);
            using var input = BatchInput(new[] { new VisualSize(8, 8), new VisualSize(16, 8) });
            var directValues = new float[18];
            for (int row = 0; row < 2; row++) for (int keypoint = 0; keypoint < 3; keypoint++) { int offset = row * 9 + keypoint * 3; directValues[offset] = 4; directValues[offset + 1] = 4; directValues[offset + 2] = .9f; }
            var directOutputs = Outputs(("keypoints", new Tensor<float>(new TensorShape(2, 1, 3, 3), directValues)), ("boxes", new Tensor<float>(new TensorShape(2, 1, 4), new[] { 1f, 1f, 7f, 7f, 1f, 1f, 7f, 7f })), ("scores", new Tensor<float>(new TensorShape(2, 1), new[] { .9f, .8f })));
            var directBatch = (PoseEstimationBatchResult)direct.Decode(new VisualDecodeContext(input, directProfile, directOutputs, CancellationToken.None));
            Assert.AreEqual(2, directBatch.Count);
            Assert.AreEqual(8, directBatch[0].SourceSize.Width);
            Assert.AreEqual(16, directBatch[1].SourceSize.Width);

            var heatmapSchema = new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw, PoseScoreKind.Raw, PoseGridMappingMode.AlignCorners);
            var heatmap = new HeatmapPoseDecoder(heatmapSchema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid, keypointThreshold: -10));
            VisualModelProfile heatmapProfile = HeatmapProfile(heatmap, new TensorShape(-1, 3, 2, 2));
            var heatmapValues = new float[24];
            for (int row = 0; row < 2; row++) for (int keypoint = 0; keypoint < 3; keypoint++) heatmapValues[row * 12 + keypoint * 4] = 1;
            var heatmapBatch = (PoseEstimationBatchResult)heatmap.Decode(new VisualDecodeContext(input, heatmapProfile, Outputs(("heatmaps", new Tensor<float>(new TensorShape(2, 3, 2, 2), heatmapValues))), CancellationToken.None));
            Assert.AreEqual(2, heatmapBatch.Count);
            Assert.AreEqual(8, heatmapBatch[0].SourceSize.Width);
            Assert.AreEqual(16, heatmapBatch[1].SourceSize.Width);
        }

        [TestMethod]
        public void OksUsesExplicitAreaSigmasVisibilityAndScore()
        {
            PoseTopology topology = Topology(withSigmas: true);
            PoseInstance first = Instance(0, 1, new[] { new PointF(0,0), new PointF(10,10), new PointF(20,20) });
            PoseInstance identical = Instance(1, .9f, new[] { new PointF(0,0), new PointF(10,10), new PointF(20,20) });
            PoseInstance distant = Instance(2, .8f, new[] { new PointF(50,50), new PointF(60,60), new PointF(70,70) });
            Assert.AreEqual(1f, PoseOks.CalculateSimilarity(first, identical, topology.Keypoints, 400), 0.0001f);
            Assert.IsTrue(PoseOks.CalculateSimilarity(first, distant, topology.Keypoints, 400) < 0.01f);
            Assert.ThrowsExactly<ArgumentException>(() => PoseOks.CalculateSimilarity(first, identical, Topology().Keypoints, 400));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PoseOks.CalculateSimilarity(first, identical, topology.Keypoints, 0));
        }

        [TestMethod]
        public void SchemasAndDecodersRejectConflictsInvalidScoresShapesAndMemory()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new DirectPoseOutputSchema("keypoints", 3, 3, xComponentIndex: 0, yComponentIndex: 0));
            Assert.ThrowsExactly<ArgumentException>(() => new DirectPoseOutputSchema("keypoints", 3, 3, coordinateSpace: PoseCoordinateSpace.TensorGrid));
            Assert.ThrowsExactly<ArgumentException>(() => new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw, instanceScoreOutputName: "heatmaps"));
            PoseTopology topology = Topology();
            var directSchema = new DirectPoseOutputSchema("keypoints", 3, 3);
            var noScoreSchema = new DirectPoseOutputSchema("keypoints", 3, 2, scoreComponentIndex: -1);
            Assert.ThrowsExactly<ArgumentException>(() => new DirectPoseDecoder(noScoreSchema, topology, new PoseDecoderOptions(instanceScoreMode: PoseInstanceScoreMode.InstanceScoreTimesMeanKeypointScore, maximumCandidates: 1, maximumInstances: 1)));
            Assert.ThrowsExactly<ArgumentException>(() => new HeatmapPoseDecoder(new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw), topology, new PoseDecoderOptions(maximumCandidates: 2, maximumInstances: 1)));

            var decoder = new DirectPoseDecoder(directSchema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            VisualModelProfile profile = DirectProfile(decoder, 1, false, false);
            using PreparedVisualInput input = Input(new VisualSize(100, 100), new VisualSize(100, 100), ImageTransform.Resize(new VisualSize(100, 100), new VisualSize(100, 100)));
            VisualException badScore = Assert.ThrowsExactly<VisualException>(() => Decode(decoder, profile, input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), new[] { 1f,1f,-.1f, 2f,2f,1f, 3f,3f,1f }))))) ;
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, badScore.ErrorCode);
            VisualException extra = Assert.ThrowsExactly<VisualException>(() => Decode(decoder, profile, input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), new float[9])), ("extra", new Tensor<float>(new TensorShape(1), new[] { 1f }))))) ;
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, extra.ErrorCode);
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => Decode(decoder, profile, input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), new[] { float.NaN,1f,1f, 2f,2f,1f, 3f,3f,1f }))))).ErrorCode);

            var bounded = new DirectPoseDecoder(directSchema, topology, new PoseDecoderOptions(maximumCandidates: 1, maximumInstances: 1, maximumResultBytes: 8));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => Decode(bounded, DirectProfile(bounded, 1, false, false), input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1, 1, 3, 3), new float[9]))))).ErrorCode);

            var truncated = new DirectPoseDecoder(directSchema, topology, new PoseDecoderOptions(instanceScoreThreshold: 0, maximumCandidates: 2, maximumInstances: 1));
            PoseEstimationResult truncatedResult = Decode(truncated, DirectProfile(truncated, 2, false, false), input, Outputs(("keypoints", new Tensor<float>(new TensorShape(1,2,3,3), new[]
            {
                1f,1f,.9f, 2f,2f,.9f, 3f,3f,.9f,
                4f,4f,.9f, 5f,5f,.9f, 6f,6f,.9f
            }))));
            Assert.AreEqual(1, truncatedResult.Instances.Count);
            Assert.AreEqual(0, truncatedResult.Instances[0].SourceIndex);

            var probabilityHeatmap = new HeatmapPoseDecoder(new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw), topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => Decode(probabilityHeatmap, HeatmapProfile(probabilityHeatmap, new TensorShape(1,3,1,1)), input, Outputs(("heatmaps", new Tensor<float>(new TensorShape(1,3,1,1), new[] { 0f, 1.1f, .5f }))))).ErrorCode);
        }

        [TestMethod]
        public void DecoderHonorsCancellationAndPipelineSupportsNamedMultiOutputs()
        {
            PoseTopology topology = Topology();
            var schema = new DirectPoseOutputSchema("keypoints", 3, 3, boxesOutputName: "boxes", instanceScoresOutputName: "scores");
            var decoder = new DirectPoseDecoder(schema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            VisualModelProfile profile = DirectProfile(decoder, 1);
            InferenceOutputs outputs = Outputs(
                ("keypoints", new Tensor<float>(new TensorShape(1,1,3,3), new[] { 10f,10f,1f, 20f,20f,1f, 30f,30f,1f })),
                ("boxes", new Tensor<float>(new TensorShape(1,1,4), new[] { 0f,0f,50f,50f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            using PreparedVisualInput cancelledInput = Input(new VisualSize(100,100), new VisualSize(100,100), ImageTransform.Resize(new VisualSize(100,100), new VisualSize(100,100)));
            Assert.ThrowsExactly<OperationCanceledException>(() => decoder.Decode(new VisualDecodeContext(cancelledInput, profile, outputs, cancelled.Token)));

            var metadata = new ModelMetadata(profile.ModelId, "fake", new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1,3,100,100)) }, new[]
            {
                new TensorDescriptor("keypoints", TensorElementType.Float32, new TensorShape(1,1,3,3)),
                new TensorDescriptor("boxes", TensorElementType.Float32, new TensorShape(1,1,4)),
                new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1,1))
            });
            var provider = new FakeVisualBackendProvider(metadata, _ => outputs);
            using var registry = new BackendRegistry();
            registry.Register(provider);
            var profiles = new VisualProfileRegistry(); profiles.Register(profile); profiles.Freeze();
            var artifact = new ModelArtifact(profile.ModelId, "fake", "fixture.fake", preferredBackend: VisualTestData.BackendId);
            var request = new BackendRequest(BackendCapabilities.TensorInference, VisualTestData.BackendId);
            using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, VisualTaskId.PoseEstimation), request);
            using PreparedVisualInput input = Input(new VisualSize(100,100), new VisualSize(100,100), ImageTransform.Resize(new VisualSize(100,100), new VisualSize(100,100)));
            PoseEstimationResult result = pipeline.Run(input).GetValue<PoseEstimationResult>();
            Assert.AreEqual(1, result.Instances.Count);
            Assert.AreEqual(3, result.Instances[0].Keypoints.Count);
        }

        [TestMethod]
        public void PoseDecoderBenchmarkReportsDirectAndHeatmapTimeAndAllocationWithoutBrittleLatencyGate()
        {
            PoseTopology topology = Topology();
            var directSchema = new DirectPoseOutputSchema("keypoints", 3, 3);
            var directDecoder = new DirectPoseDecoder(directSchema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            VisualModelProfile directProfile = DirectProfile(directDecoder, 1, false, false);
            InferenceOutputs directOutputs = Outputs(("keypoints", new Tensor<float>(new TensorShape(1,1,3,3), new[] { 10f,10f,.9f, 20f,20f,.8f, 30f,30f,.7f })));
            using PreparedVisualInput directInput = Input(new VisualSize(100,100), new VisualSize(100,100), ImageTransform.Resize(new VisualSize(100,100), new VisualSize(100,100)));
            MeasureDecoder("direct", directDecoder, directProfile, directInput, directOutputs);

            var schema = new HeatmapPoseOutputSchema("heatmaps", 3, PoseHeatmapLayout.Nchw);
            var decoder = new HeatmapPoseDecoder(schema, topology, OneCandidateOptions(PoseBoundaryMode.MarkInvalid));
            VisualModelProfile profile = HeatmapProfile(decoder, new TensorShape(1,3,32,32));
            float[] values = new float[3 * 32 * 32]; values[100] = .9f; values[1024 + 500] = .8f; values[2048 + 900] = .7f;
            InferenceOutputs outputs = Outputs(("heatmaps", new Tensor<float>(new TensorShape(1,3,32,32), values)));
            using PreparedVisualInput input = Input(new VisualSize(256,256), new VisualSize(256,256), ImageTransform.Resize(new VisualSize(256,256), new VisualSize(256,256)));
            MeasureDecoder("heatmap", decoder, profile, input, outputs);
        }

        private void MeasureDecoder(string name, IVisualDecoder decoder, VisualModelProfile profile, PreparedVisualInput input, InferenceOutputs outputs)
        {
            PoseEstimationResult warm = Decode(decoder, profile, input, outputs);
            string golden = warm.ComputeSha256();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int index = 0; index < 100; index++) Assert.AreEqual(golden, Decode(decoder, profile, input, outputs).ComputeSha256());
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext?.WriteLine("pose_{0}_decode_iterations=100;elapsed_ms={1:F3};allocated_bytes={2}", name, watch.Elapsed.TotalMilliseconds, allocated);
            Assert.IsTrue(allocated > 0);
        }

        private static PoseTopology Topology(bool withSigmas = false)
        {
            float? sigma = withSigmas ? .1f : (float?)null;
            return new PoseTopology(new[]
            {
                new PoseKeypointDefinition(0, "left", 1, new PoseColor(255,0,0), sigma),
                new PoseKeypointDefinition(1, "right", 0, new PoseColor(0,255,0), sigma),
                new PoseKeypointDefinition(2, "center", null, new PoseColor(0,0,255), sigma)
            }, new[] { new PoseSkeletonEdge(0,2), new PoseSkeletonEdge(1,2) });
        }

        private static PoseDecoderOptions OneCandidateOptions(PoseBoundaryMode mode, float keypointThreshold = 0f)
            => new PoseDecoderOptions(instanceScoreThreshold: 0, keypointScoreThreshold: keypointThreshold, boundaryMode: mode, maximumCandidates: 1, maximumInstances: 1);

        private static PoseInstance Instance(int index, float score, PointF[] points)
        {
            var keypoints = new List<PoseKeypoint>();
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++) keypoints.Add(new PoseKeypoint(pointIndex, points[pointIndex], 1, PoseKeypointVisibility.Visible, true));
            return new PoseInstance(index, score, keypoints, new RectangleF(0,0,20,20));
        }

        private static VisualModelProfile DirectProfile(DirectPoseDecoder decoder, int candidates, bool includeBoxes = true, bool includeScores = true, VisualSize? modelSize = null)
        {
            VisualSize size = modelSize ?? new VisualSize(100,100);
            var outputs = new List<VisualOutputBinding> { new VisualOutputBinding("keypoints", TensorElementType.Float32, new TensorShape(1,candidates,3,decoder.Schema.ComponentCount)) };
            if (includeBoxes) outputs.Add(new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,candidates,4)));
            if (includeScores) outputs.Add(new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,candidates)));
            return new VisualModelProfile("tests/direct-pose.v1", new ModelId("tests/direct-pose"), VisualTaskId.PoseEstimation, "1.0", "fake", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,size.Height,size.Width), VisualTensorLayout.Nchw), outputs, Array.Empty<VisualLabel>(), decoder);
        }

        private static VisualModelProfile HeatmapProfile(HeatmapPoseDecoder decoder, TensorShape shape)
            => new VisualModelProfile("tests/heatmap-pose.v1", new ModelId("tests/heatmap-pose"), VisualTaskId.PoseEstimation, "1.0", "fake", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,100,100), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("heatmaps", TensorElementType.Float32, shape) }, Array.Empty<VisualLabel>(), decoder);

        private static PreparedVisualInput Input(VisualSize source, VisualSize model, ImageTransform transform)
            => new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,model.Height,model.Width), new float[checked(3 * model.Height * model.Width)]), source, model, 1, VisualTensorLayout.Nchw, transform);

        private static PreparedVisualInput BatchInput(IReadOnlyList<VisualSize> sources)
        {
            var frames = new List<VisualInputFrame>();
            for (int index = 0; index < sources.Count; index++) frames.Add(new VisualInputFrame(sources[index], new VisualSize(8, 8), ImageTransform.Resize(sources[index], new VisualSize(8, 8))));
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(sources.Count, 3, 8, 8), new float[sources.Count * 3 * 8 * 8]), sources[0], new VisualSize(8, 8), sources.Count, VisualTensorLayout.Nchw, frames[0].Transform, batchFrames: frames);
        }

        private static PoseEstimationResult Decode(IVisualDecoder decoder, VisualModelProfile profile, PreparedVisualInput input, InferenceOutputs outputs)
            => (PoseEstimationResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));

        private static InferenceOutputs Outputs(params (string Name, ITensor Tensor)[] tensors)
        {
            var values = new List<NamedTensor>(tensors.Length);
            for (int index = 0; index < tensors.Length; index++) values.Add(new NamedTensor(tensors[index].Name, tensors[index].Tensor));
            return new InferenceOutputs(values);
        }
    }
}
