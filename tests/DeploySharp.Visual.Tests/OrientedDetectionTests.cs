using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OrientedDetectionTests
    {
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void QuadrilateralCanonicalizationRejectsInvalidOrderAndOwnsVertices()
        {
            var valid = new[] { new PointF(0, 0), new PointF(4, 0), new PointF(4, 2), new PointF(0, 2) };
            OrientedQuadrilateral result = OrientedQuadrilateral.Canonicalize(valid, OrientedVertexOrder.CounterClockwise);
            valid[0] = new PointF(99, 99);
            Assert.AreEqual(8f, result.Area, .0001f);
            Assert.AreEqual(new PointF(0, 0), result.First);
            Assert.ThrowsExactly<ArgumentException>(() => OrientedQuadrilateral.Canonicalize(new[] { new PointF(0, 0), new PointF(4, 2), new PointF(4, 0), new PointF(0, 2) }, OrientedVertexOrder.CounterClockwise));
            Assert.ThrowsExactly<ArgumentException>(() => OrientedQuadrilateral.Canonicalize(new[] { new PointF(0, 0), new PointF(1, 0), new PointF(0, 0), new PointF(0, 1) }, OrientedVertexOrder.CounterClockwise));
        }

        [TestMethod]
        public void PolygonIoUHandlesExactPartialTouchingAndContainment()
        {
            OrientedQuadrilateral first = Quad(0, 0, 10, 10);
            OrientedQuadrilateral same = Quad(0, 0, 10, 10);
            OrientedQuadrilateral partial = Quad(5, 0, 10, 10);
            OrientedQuadrilateral disjoint = Quad(20, 0, 10, 10);
            OrientedQuadrilateral contained = Quad(2, 2, 2, 2);
            Assert.AreEqual(1f, OrientedGeometryTestHook.Iou(first, same), .0001f);
            Assert.AreEqual(1f / 3f, OrientedGeometryTestHook.Iou(first, partial), .0001f);
            Assert.AreEqual(0f, OrientedGeometryTestHook.Iou(first, disjoint), .0001f);
            Assert.AreEqual(.04f, OrientedGeometryTestHook.Iou(first, contained), .0001f);
            Assert.AreEqual(0f, OrientedGeometryTestHook.Iou(first, Quad(10, 10, 4, 4)), .0001f);
        }

        [TestMethod]
        public void DirectDecoderUsesExplicitAngleAndClassAwareOrAgnosticNms()
        {
            var schema = new CenterSizeAngleOutputSchema("boxes", "scores", "classes", angleUnit: OrientedAngleUnit.Degrees, angleDirection: OrientedAngleDirection.Clockwise, angleRange: OrientedAngleRange.MinusHalfPiToHalfPi);
            var decoder = new DirectOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, iouThreshold: .3f, maximumCandidates: 4, maximumDetections: 4));
            VisualModelProfile profile = Profile(decoder, new[] { new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1, 4, 5)), new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 4)), new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1, 4)) });
            using PreparedVisualInput input = Input(new VisualSize(100, 100), new VisualSize(100, 100), ImageTransform.Resize(new VisualSize(100, 100), new VisualSize(100, 100)));
            InferenceOutputs outputs = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1, 4, 5), new[] { 20f,20f,20f,10f,45f, 20f,20f,20f,10f,45f, 20f,20f,20f,10f,45f, 70f,70f,20f,10f,0f })),
                ("scores", new Tensor<float>(new TensorShape(1, 4), new[] { .9f,.8f,.9f,.9f })),
                ("classes", new Tensor<float>(new TensorShape(1, 4), new[] { 0f,0f,1f,0f })));
            OrientedDetectionResult result = (OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            Assert.AreEqual(3, result.Detections.Count);
            Assert.AreEqual(0, result.Detections[0].SourceIndex);
            Assert.IsTrue(result.Detections[0].HasExactRotatedRectangle);
            Assert.AreEqual(-Math.PI / 4d, result.Detections[0].AngleRadiansCounterClockwise!.Value, .0001);

            var agnostic = new DirectOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, iouThreshold: .3f, nmsMode: DetectionNmsMode.ClassAgnostic, maximumCandidates: 4, maximumDetections: 4));
            OrientedDetectionResult agnosticResult = (OrientedDetectionResult)agnostic.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            Assert.AreEqual(2, agnosticResult.Detections.Count);
        }

        [TestMethod]
        public void Float64CornerDecoderRestoresNonUniformResizeAsAuthoritativeParallelogram()
        {
            var schema = new FourCornerOutputSchema("corners", "scores", "classes", inputVertexOrder: OrientedVertexOrder.CounterClockwise);
            var decoder = new FourCornerOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 2, maximumDetections: 2));
            VisualModelProfile profile = Profile(decoder, new[] { new VisualOutputBinding("corners", TensorElementType.Float64, new TensorShape(1, 1, 8)), new VisualOutputBinding("scores", TensorElementType.Float64, new TensorShape(1, 1)), new VisualOutputBinding("classes", TensorElementType.Float64, new TensorShape(1, 1)) });
            using PreparedVisualInput input = Input(new VisualSize(8, 4), new VisualSize(4, 4), ImageTransform.Resize(new VisualSize(8, 4), new VisualSize(4, 4)));
            InferenceOutputs outputs = Outputs(
                ("corners", new Tensor<double>(new TensorShape(1, 1, 8), new[] { 1d,1d, 3.828427d,3.828427d, 2.414214d,5.242641d, -.414214d,2.414214d })),
                ("scores", new Tensor<double>(new TensorShape(1, 1), new[] { .95d })),
                ("classes", new Tensor<double>(new TensorShape(1, 1), new[] { 0d })));
            OrientedDetectionResult result = (OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.IsFalse(result.Detections[0].HasExactRotatedRectangle);
            Assert.IsNull(result.Detections[0].AngleRadiansCounterClockwise);
            Assert.IsTrue(Math.Abs(result.Detections[0].Quadrilateral.First.X - result.Detections[0].Quadrilateral.Second.X) > 0);
            Assert.AreEqual(result.Detections[0].Quadrilateral.Area, result.Detections[0].Quadrilateral.SignedArea, .0001f);
        }

        [TestMethod]
        public void AngleRangeDirectionAndLongSideConventionAreExplicit()
        {
            var schema = new CenterSizeAngleOutputSchema(
                "boxes", "scores", "classes", angleUnit: OrientedAngleUnit.Degrees,
                angleDirection: OrientedAngleDirection.CounterClockwise, angleRange: OrientedAngleRange.ZeroToPi,
                widthConvention: OrientedWidthConvention.LongSide);
            var decoder = new DirectOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 1, maximumDetections: 1));
            VisualModelProfile profile = Profile(decoder, DirectBindings(TensorElementType.Float32, 1));
            using PreparedVisualInput input = Input(new VisualSize(100,100), new VisualSize(100,100), ImageTransform.Resize(new VisualSize(100,100), new VisualSize(100,100)));
            InferenceOutputs valid = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1,1,5), new[] { 50f,50f,10f,20f,0f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })));
            OrientedDetectionResult result = (OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, valid, CancellationToken.None));
            Assert.AreEqual((float)(Math.PI / 2d), result.Detections[0].AngleRadiansCounterClockwise!.Value, .0001f);
            Assert.AreEqual(20f, Math.Max(result.Detections[0].EdgeLength01, result.Detections[0].EdgeLength12), .0001f);
            Assert.AreEqual(10f, Math.Min(result.Detections[0].EdgeLength01, result.Detections[0].EdgeLength12), .0001f);

            InferenceOutputs excludedUpper = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1,1,5), new[] { 50f,50f,20f,10f,180f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })));
            Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, excludedUpper, CancellationToken.None)));
        }

        [TestMethod]
        public void LetterboxAndCropRestoreEveryVertexWithoutAabbConversion()
        {
            var decoder = new FourCornerOrientedDetectionDecoder(
                new FourCornerOutputSchema("corners", "scores", "classes"),
                new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 1, maximumDetections: 1));
            VisualModelProfile profile = Profile(decoder, CornerBindings(TensorElementType.Float32, 1));
            InferenceOutputs outputs = Outputs(
                ("corners", new Tensor<float>(new TensorShape(1,1,8), new[] { 25f,25f, 75f,25f, 75f,75f, 25f,75f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })));
            var source = new VisualSize(200,100);
            var model = new VisualSize(100,100);
            using (PreparedVisualInput input = Input(source, model, ImageTransform.Letterbox(source, model)))
            {
                OrientedDetectionResult result = (OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
                Assert.AreEqual(new PointF(50,0), result.Detections[0].Quadrilateral.First);
                Assert.AreEqual(new PointF(150,100), result.Detections[0].Quadrilateral.Third);
            }

            using (PreparedVisualInput input = Input(new VisualSize(100,100), model, ImageTransform.Crop(new VisualSize(100,100), model, new RectangleF(25,25,50,50))))
            {
                OrientedDetectionResult result = (OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
                Assert.AreEqual(new PointF(37.5f,37.5f), result.Detections[0].Quadrilateral.First);
                Assert.AreEqual(new PointF(62.5f,62.5f), result.Detections[0].Quadrilateral.Third);
            }
        }

        [TestMethod]
        public void StrictOutputsBoundaryAndWorkspaceLimitsProduceVisualDiagnostics()
        {
            var rejectSchema = new CenterSizeAngleOutputSchema("boxes", "scores", "classes", boundaryMode: OrientedDetectionBoundaryMode.RejectOutsideSource);
            var decoder = new DirectOrientedDetectionDecoder(rejectSchema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 1, maximumDetections: 1));
            VisualModelProfile profile = Profile(decoder, DirectBindings(TensorElementType.Float32, 1));
            using PreparedVisualInput input = Input(new VisualSize(10,10), new VisualSize(10,10), ImageTransform.Resize(new VisualSize(10,10), new VisualSize(10,10)));
            InferenceOutputs outside = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1,1,5), new[] { 0f,0f,4f,4f,0f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })));
            VisualException boundary = Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, outside, CancellationToken.None)));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, boundary.ErrorCode);

            InferenceOutputs extra = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1,1,5), new[] { 5f,5f,4f,4f,0f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })),
                ("unexpected", new Tensor<float>(new TensorShape(1), new[] { 0f })));
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, extra, CancellationToken.None))).ErrorCode);

            InferenceOutputs wrongRank = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1,5), new[] { 5f,5f,4f,4f,0f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })));
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, wrongRank, CancellationToken.None))).ErrorCode);

            InferenceOutputs nonFinite = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1,1,5), new[] { 5f,5f,4f,4f,0f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { float.NaN })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })));
            Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(input, profile, nonFinite, CancellationToken.None)));

            using (var batchInput = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2,3,10,10), new float[600]), new VisualSize(10,10), new VisualSize(10,10), 2, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(10,10), new VisualSize(10,10))))
            {
                Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new VisualDecodeContext(batchInput, profile, outside, CancellationToken.None))).ErrorCode);
            }

            var bounded = new DirectOrientedDetectionDecoder(new CenterSizeAngleOutputSchema("boxes", "scores", "classes"), new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 1, maximumDetections: 1, maximumWorkspaceBytes: 1));
            VisualModelProfile doubleProfile = Profile(bounded, DirectBindings(TensorElementType.Float64, 1));
            InferenceOutputs doubles = Outputs(
                ("boxes", new Tensor<double>(new TensorShape(1,1,5), new[] { 5d,5d,4d,4d,0d })),
                ("scores", new Tensor<double>(new TensorShape(1,1), new[] { .9d })),
                ("classes", new Tensor<double>(new TensorShape(1,1), new[] { 0d })));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => bounded.Decode(new VisualDecodeContext(input, doubleProfile, doubles, CancellationToken.None))).ErrorCode);
        }

        [TestMethod]
        public void RotatedNmsPerformanceEntryRecordsTimeThroughputAndAllocation()
        {
            const int count = 128;
            var boxes = new float[count * 5];
            var scores = new float[count];
            var classes = new float[count];
            for (int index = 0; index < count; index++)
            {
                int offset = index * 5;
                boxes[offset] = 10 + (index % 16) * 12;
                boxes[offset + 1] = 10 + (index / 16) * 12;
                boxes[offset + 2] = 10;
                boxes[offset + 3] = 6;
                boxes[offset + 4] = (index % 9) * .03f;
                scores[index] = 1f - (index * .001f);
                classes[index] = index % 2;
            }
            var decoder = new DirectOrientedDetectionDecoder(new CenterSizeAngleOutputSchema("boxes", "scores", "classes"), new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: count, maximumDetections: count));
            VisualModelProfile profile = Profile(decoder, DirectBindings(TensorElementType.Float32, count));
            using PreparedVisualInput input = Input(new VisualSize(256,256), new VisualSize(256,256), ImageTransform.Resize(new VisualSize(256,256), new VisualSize(256,256)));
            InferenceOutputs outputs = Outputs(("boxes", new Tensor<float>(new TensorShape(1,count,5), boxes)), ("scores", new Tensor<float>(new TensorShape(1,count), scores)), ("classes", new Tensor<float>(new TensorShape(1,count), classes)));
            string golden = ((OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None))).ComputeSha256();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int iteration = 0; iteration < 20; iteration++) Assert.AreEqual(golden, ((OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None))).ComputeSha256());
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext?.WriteLine("obb_candidates={0};iterations=20;elapsed_ms={1:F3};throughput_per_second={2:F1};allocated_bytes={3}", count, watch.Elapsed.TotalMilliseconds, 20d / watch.Elapsed.TotalSeconds, allocated);
            Assert.IsTrue(allocated > 0);
        }

        [TestMethod]
        public void DecoderCanBeReusedConcurrentlyWithoutSharedMutableWorkspace()
        {
            const int calls = 16;
            var decoder = new DirectOrientedDetectionDecoder(new CenterSizeAngleOutputSchema("boxes", "scores", "classes"), new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 1, maximumDetections: 1));
            VisualModelProfile profile = Profile(decoder, DirectBindings(TensorElementType.Float32, 1));
            using PreparedVisualInput input = Input(new VisualSize(20,20), new VisualSize(20,20), ImageTransform.Resize(new VisualSize(20,20), new VisualSize(20,20)));
            InferenceOutputs outputs = Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1,1,5), new[] { 10f,10f,8f,4f,.2f })),
                ("scores", new Tensor<float>(new TensorShape(1,1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1,1), new[] { 0f })));
            var hashes = new string[calls];
            Parallel.For(0, calls, index => hashes[index] = ((OrientedDetectionResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None))).ComputeSha256());
            for (int index = 1; index < hashes.Length; index++) Assert.AreEqual(hashes[0], hashes[index]);
        }

        [TestMethod]
        public async Task FakePipelineCancellationAndResultOwnershipAreStable()
        {
            var schema = new CenterSizeAngleOutputSchema("boxes", "scores", "classes");
            var decoder = new DirectOrientedDetectionDecoder(schema, new OrientedDetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 1, maximumDetections: 1));
            VisualModelProfile profile = Profile(decoder, new[] { new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1, 1, 5)), new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 1)), new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1, 1)) });
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1, 1, 5), _ => Outputs(
                ("boxes", new Tensor<float>(new TensorShape(1, 1, 5), new[] { 2f,2f,2f,2f,0f })),
                ("scores", new Tensor<float>(new TensorShape(1, 1), new[] { .9f })),
                ("classes", new Tensor<float>(new TensorShape(1, 1), new[] { 0f }))));
            fixture.Provider.Delay = TimeSpan.FromMilliseconds(40);
            using PreparedVisualInput input = Input(new VisualSize(4, 4), new VisualSize(4, 4), ImageTransform.Resize(new VisualSize(4, 4), new VisualSize(4, 4)));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            VisualException cancelled = await Assert.ThrowsExactlyAsync<VisualException>(() => fixture.Pipeline.RunAsync(input, cancellationToken: cancellation.Token));
            Assert.AreEqual(VisualErrorCodes.Cancelled, cancelled.ErrorCode);
            VisualInferenceResult inference = await fixture.Pipeline.RunAsync(input);
            OrientedDetectionResult result = inference.GetValue<OrientedDetectionResult>();
            fixture.Pipeline.Dispose();
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual(1, fixture.Provider.LastSession!.DisposeCount);
        }

        private static OrientedQuadrilateral Quad(float x, float y, float width, float height) => OrientedQuadrilateral.Canonicalize(new[] { new PointF(x, y), new PointF(x + width, y), new PointF(x + width, y + height), new PointF(x, y + height) }, OrientedVertexOrder.CounterClockwise);

        private static IReadOnlyList<VisualOutputBinding> DirectBindings(TensorElementType type, int candidates) => new[] { new VisualOutputBinding("boxes", type, new TensorShape(1,candidates,5)), new VisualOutputBinding("scores", type, new TensorShape(1,candidates)), new VisualOutputBinding("classes", type, new TensorShape(1,candidates)) };

        private static IReadOnlyList<VisualOutputBinding> CornerBindings(TensorElementType type, int candidates) => new[] { new VisualOutputBinding("corners", type, new TensorShape(1,candidates,8)), new VisualOutputBinding("scores", type, new TensorShape(1,candidates)), new VisualOutputBinding("classes", type, new TensorShape(1,candidates)) };

        private static VisualModelProfile Profile(IVisualDecoder decoder, IReadOnlyList<VisualOutputBinding> outputs)
            => new VisualModelProfile("tests/oriented-detection.v1", new ModelId("tests/oriented-detection"), VisualTaskId.OrientedObjectDetection, "1.0", "fake", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,-1,-1), VisualTensorLayout.Nchw), outputs, new[] { new VisualLabel(0, "alpha"), new VisualLabel(1, "beta") }, decoder);

        private static PreparedVisualInput Input(VisualSize source, VisualSize model, ImageTransform transform)
            => new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,model.Height,model.Width), new float[checked(3 * model.Height * model.Width)]), source, model, 1, VisualTensorLayout.Nchw, transform);

        private static InferenceOutputs Outputs(params (string Name, ITensor Tensor)[] tensors)
        {
            var values = new List<NamedTensor>(tensors.Length);
            for (int index = 0; index < tensors.Length; index++) values.Add(new NamedTensor(tensors[index].Name, tensors[index].Tensor));
            return new InferenceOutputs(values);
        }
    }

    internal static class OrientedGeometryTestHook
    {
        public static float Iou(OrientedQuadrilateral first, OrientedQuadrilateral second) => Invoke(first, second);
        private static float Invoke(OrientedQuadrilateral first, OrientedQuadrilateral second)
        {
            return OrientedQuadrilateral.IntersectionOverUnion(first, second);
        }
    }
}
