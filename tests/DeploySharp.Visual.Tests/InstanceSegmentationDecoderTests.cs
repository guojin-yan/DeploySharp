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
    public sealed class InstanceSegmentationDecoderTests
    {
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void OwnedMaskRleResultAndMetadataAreDefensiveAndDeterministic()
        {
            byte[] pixels = { 1, 1, 0, 0, 1, 0 };
            var mask = new InstanceBinaryMask(3, 2, pixels);
            pixels[0] = 0;
            Assert.IsTrue(mask.IsForeground(0, 0));
            Assert.AreEqual(3, mask.ForegroundPixelCount);
            byte[] copy = new byte[8];
            mask.CopyTo(copy, 1);
            Assert.AreEqual(1, copy[1]);
            var booleanCopy = new bool[8];
            mask.CopyTo(booleanCopy, 1);
            Assert.IsTrue(booleanCopy[1]);
            Assert.IsFalse(booleanCopy[3]);
            RectangleF bounds = mask.GetForegroundBounds()!.Value;
            Assert.AreEqual(new RectangleF(0, 0, 2, 2), bounds);
            InstanceMaskRle rle = InstanceMaskRle.Encode(mask);
            Assert.AreEqual("deploysharp-row-major-foreground-runs-v1", rle.Format);
            Assert.AreEqual(2, rle.Runs.Count);
            CollectionAssert.AreEqual(mask.ToArray(), rle.Decode().ToArray());
            Assert.AreEqual(mask.ComputeSha256(), mask.ComputeSha256());

            var metadata = new Dictionary<string, string> { ["track"] = "17" };
            var instance = new InstanceSegmentationInstance(0, 1, "part", .9f, new RectangleF(0, 0, 2, 2), mask, rle, "external-1", metadata);
            metadata["track"] = "changed";
            Assert.AreEqual("17", instance.Metadata["track"]);
            var result = new InstanceSegmentationResult(new[] { instance }, new VisualSize(3, 2), "tests/instance.v1", new ModelId("tests/instance"));
            Assert.AreEqual(result.ComputeSha256(), result.ComputeSha256());
            Assert.ThrowsExactly<ArgumentException>(() => new InstanceSegmentationResult(new[]
            {
                Instance(1, .8f, new byte[6]), Instance(0, .9f, new byte[6])
            }, new VisualSize(3,2), "tests/instance.v1", new ModelId("tests/instance")));
        }

        [TestMethod]
        public void DirectNchwAppliesStableNmsCropRleAndScorePriorityOwnership()
        {
            var schema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Probabilities);
            var options = new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, iouThreshold: .5f, overlapMode: InstanceMaskOverlapMode.ScorePriorityOwnership, maximumCandidates: 3, maximumInstances: 3);
            var decoder = new DirectInstanceSegmentationDecoder(schema, options);
            VisualModelProfile profile = DirectProfile(decoder, new TensorShape(1, 3, 4, 4));
            float[] masks = FilledMasks(3, 4, 4, 1f);
            using PreparedVisualInput input = Input(new VisualSize(4, 4), new VisualSize(4, 4), ImageTransform.Resize(new VisualSize(4, 4), new VisualSize(4, 4)));
            InstanceSegmentationResult result = Decode(decoder, profile, input, DirectOutputs(masks, new TensorShape(1, 3, 4, 4)));

            Assert.AreEqual(VisualTaskId.InstanceSegmentation, decoder.Task);
            Assert.AreEqual(2, result.Instances.Count);
            Assert.AreEqual(0, result.Instances[0].SourceIndex);
            Assert.AreEqual(2, result.Instances[1].SourceIndex);
            Assert.AreEqual(9, result.Instances[0].Mask.ForegroundPixelCount);
            Assert.AreEqual(4, result.Instances[1].Mask.ForegroundPixelCount);
            Assert.IsNotNull(result.Instances[0].Rle);
            Assert.IsNotNull(result.OwnershipMap);
            Assert.AreEqual(0, result.OwnershipMap.GetOwnerIndex(2, 0));
            Assert.AreEqual(1, result.OwnershipMap.GetOwnerIndex(3, 0));
            Assert.AreEqual(-1, result.OwnershipMap.GetOwnerIndex(3, 3));
        }

        [TestMethod]
        public void DirectNhwcFloat64BinaryMaskUsesExactLayoutAndOwnedOutput()
        {
            var schema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nhwc, InstanceMaskValueKind.Binary, interpolation: InstanceMaskInterpolationMode.NearestNeighbor, thresholdOrder: InstanceMaskThresholdOrder.BeforeResize, cropSpace: InstanceMaskCropSpace.None);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, generateRle: false, maximumCandidates: 3, maximumInstances: 3));
            VisualModelProfile profile = DirectProfile(decoder, new TensorShape(1, 3, 2, 2, 1), TensorElementType.Float64);
            double[] masks =
            {
                1,0,0,1,
                1,1,1,1,
                0,1,1,0
            };
            var outputs = DirectOutputs(masks, new TensorShape(1, 3, 2, 2, 1));
            using PreparedVisualInput input = Input(new VisualSize(4, 4), new VisualSize(4, 4), ImageTransform.Resize(new VisualSize(4, 4), new VisualSize(4, 4)));
            InstanceSegmentationResult result = Decode(decoder, profile, input, outputs);
            masks[0] = 0;

            Assert.AreEqual(2, result.Instances.Count);
            Assert.IsTrue(result.Instances[0].Mask.IsForeground(0, 0));
            Assert.IsNull(result.Instances[0].Rle);
            Assert.AreEqual(InstanceMaskCoordinateSpace.SourceImage, result.Instances[0].Mask.CoordinateSpace);
        }

        [TestMethod]
        public void DirectInstanceDecoderSupportsDynamicBatchWithIndependentSourceGeometry()
        {
            var schema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Binary,
                interpolation: InstanceMaskInterpolationMode.NearestNeighbor, thresholdOrder: InstanceMaskThresholdOrder.BeforeResize, cropSpace: InstanceMaskCropSpace.None);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, generateRle: false, maximumCandidates: 2, maximumInstances: 2));
            var profile = new VisualModelProfile(
                "tests/direct-instance-batch.v1", new ModelId("tests/direct-instance-batch"), VisualTaskId.InstanceSegmentation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2), VisualTensorLayout.Nchw, 1, 2),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(-1, 2, 4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 2)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(-1, 2)),
                    new VisualOutputBinding("masks", TensorElementType.Float32, new TensorShape(-1, 2, 2, 2))
                }, new[] { new VisualLabel(0, "alpha") }, decoder);
            var first = new VisualSize(4, 4);
            var model = new VisualSize(2, 2);
            var firstTransform = ImageTransform.Resize(first, model);
            var secondTransform = ImageTransform.Resize(model, model);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), first, model, 2, VisualTensorLayout.Nchw, firstTransform,
                batchFrames: new[] { new VisualInputFrame(first, model, firstTransform, "first"), new VisualInputFrame(model, model, secondTransform, "second") });
            var boxes = new float[] { 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2 };
            var scores = new[] { .9f, 0f, .8f, 0f };
            var classes = new[] { 0f, 0f, 0f, 0f };
            var masks = new float[16];
            for (int index = 0; index < masks.Length; index++) masks[index] = 1f;
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("boxes", new Tensor<float>(new TensorShape(2, 2, 4), boxes)),
                new NamedTensor("scores", new Tensor<float>(new TensorShape(2, 2), scores)),
                new NamedTensor("classes", new Tensor<float>(new TensorShape(2, 2), classes)),
                new NamedTensor("masks", new Tensor<float>(new TensorShape(2, 2, 2, 2), masks))
            });
            object decoded = decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            var batch = (InstanceSegmentationBatchResult)decoded;
            Assert.AreEqual(2, batch.Count);
            Assert.AreEqual(4, batch[0].SourceSize.Width);
            Assert.AreEqual(2, batch[1].SourceSize.Width);
            Assert.AreEqual(1, batch[0].Instances.Count);
            Assert.AreEqual(1, batch[1].Instances.Count);
        }

        [TestMethod]
        public void PrototypeInstanceDecoderSupportsSharedPrototypeDynamicBatch()
        {
            var schema = new PrototypeInstanceSegmentationOutputSchema(Candidates(), "prototypes", "coefficients", InstanceMaskTensorLayout.Nchw, cropSpace: InstanceMaskCropSpace.None);
            var decoder = new PrototypeInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, generateRle: false, maximumCandidates: 2, maximumInstances: 2, maximumPrototypeChannels: 2));
            var profile = new VisualModelProfile(
                "tests/prototype-instance-batch.v1", new ModelId("tests/prototype-instance-batch"), VisualTaskId.InstanceSegmentation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2), VisualTensorLayout.Nchw, 1, 2),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(-1, 2, 4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 2)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(-1, 2)),
                    new VisualOutputBinding("prototypes", TensorElementType.Float32, new TensorShape(1, 2, 2, 2)),
                    new VisualOutputBinding("coefficients", TensorElementType.Float32, new TensorShape(-1, 2, 2))
                }, new[] { new VisualLabel(0, "alpha") }, decoder);
            var first = new VisualSize(4, 4);
            var model = new VisualSize(2, 2);
            var firstTransform = ImageTransform.Resize(first, model);
            var secondTransform = ImageTransform.Resize(model, model);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), first, model, 2, VisualTensorLayout.Nchw, firstTransform,
                batchFrames: new[] { new VisualInputFrame(first, model, firstTransform, "first"), new VisualInputFrame(model, model, secondTransform, "second") });
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("boxes", new Tensor<float>(new TensorShape(2, 2, 4), new float[] { 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2 })),
                new NamedTensor("scores", new Tensor<float>(new TensorShape(2, 2), new[] { .9f, 0f, .8f, 0f })),
                new NamedTensor("classes", new Tensor<float>(new TensorShape(2, 2), new float[] { 0, 0, 0, 0 })),
                new NamedTensor("prototypes", new Tensor<float>(new TensorShape(1, 2, 2, 2), new float[] { 10, 10, -10, -10, -10, -10, 10, 10 })),
                new NamedTensor("coefficients", new Tensor<float>(new TensorShape(2, 2, 2), new float[] { 1, 0, 0, 0, 1, 0, 0, 0 }))
            });
            object decoded = decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            var batch = (InstanceSegmentationBatchResult)decoded;
            Assert.AreEqual(2, batch.Count);
            Assert.AreEqual(4, batch[0].SourceSize.Width);
            Assert.AreEqual(2, batch[1].SourceSize.Width);
            Assert.AreEqual(1, batch[0].Instances.Count);
            Assert.AreEqual(1, batch[1].Instances.Count);
        }

        [TestMethod]
        public void PrototypeNchwAndNhwcUseExplicitLinearCombinationAndActivation()
        {
            RunPrototypeLayout(InstanceMaskTensorLayout.Nchw, false);
            RunPrototypeLayout(InstanceMaskTensorLayout.Nhwc, true);
        }

        [TestMethod]
        public void ResizeLetterboxAndCropRestoreMasksInSourceSpace()
        {
            var schema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Binary, interpolation: InstanceMaskInterpolationMode.NearestNeighbor, thresholdOrder: InstanceMaskThresholdOrder.BeforeResize, cropSpace: InstanceMaskCropSpace.None);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, generateRle: false, maximumCandidates: 3, maximumInstances: 3));
            VisualModelProfile profile = DirectProfile(decoder, new TensorShape(1, 3, 2, 2));
            float[] values = FilledMasks(3, 2, 2, 1);

            var source = new VisualSize(8, 4);
            var model = new VisualSize(4, 4);
            using (PreparedVisualInput input = Input(source, model, ImageTransform.Letterbox(source, model)))
            {
                InstanceSegmentationResult result = Decode(decoder, profile, input, DirectOutputs(values, new TensorShape(1, 3, 2, 2)));
                Assert.AreEqual(32, result.Instances[0].Mask.ForegroundPixelCount);
            }

            var cropSource = new VisualSize(8, 4);
            using (PreparedVisualInput input = Input(cropSource, model, ImageTransform.Crop(cropSource, model, new RectangleF(2, 0, 4, 4))))
            {
                InstanceSegmentationResult result = Decode(decoder, profile, input, DirectOutputs(values, new TensorShape(1, 3, 2, 2)));
                Assert.AreEqual(16, result.Instances[0].Mask.ForegroundPixelCount);
                Assert.IsFalse(result.Instances[0].Mask.IsForeground(0, 0));
                Assert.IsTrue(result.Instances[0].Mask.IsForeground(2, 0));
            }
        }

        [TestMethod]
        public void DirectRejectsExtraOutputsWrongShapeTypeAndDeclaredValues()
        {
            var schema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Probabilities);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(maximumCandidates: 3));
            VisualModelProfile profile = DirectProfile(decoder, new TensorShape(1, 3, 2, 2));
            using PreparedVisualInput input = Input(new VisualSize(4,4), new VisualSize(4,4), ImageTransform.Resize(new VisualSize(4,4), new VisualSize(4,4)));
            InferenceOutputs extra = Outputs(
                ("boxes", Float(new TensorShape(1,3,4), Boxes())), ("scores", Float(new TensorShape(1,3), Scores())), ("classes", Float(new TensorShape(1,3), Classes())),
                ("masks", Float(new TensorShape(1,3,2,2), FilledMasks(3,2,2,1))), ("extra", Float(new TensorShape(1), new[] { 1f })));
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(decoder, profile, input, extra)).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(decoder, profile, input, DirectOutputs(FilledMasks(3,2,2,1), new TensorShape(1,3,2,2,1)))).ErrorCode);

            float[] invalid = FilledMasks(3,2,2,1); invalid[3] = 1.1f;
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => Decode(decoder, profile, input, DirectOutputs(invalid, new TensorShape(1,3,2,2)))).ErrorCode);
            InferenceOutputs integerMasks = Outputs(
                ("boxes", Float(new TensorShape(1,3,4), Boxes())), ("scores", Float(new TensorShape(1,3), Scores())), ("classes", Float(new TensorShape(1,3), Classes())),
                ("masks", new Tensor<int>(new TensorShape(1,3,2,2), new int[12])));
            Assert.AreEqual(VisualErrorCodes.TensorInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(decoder, profile, input, integerMasks)).ErrorCode);
        }

        [TestMethod]
        public void DecoderRejectsCandidatePrototypePixelResultWorkspaceAndRleBounds()
        {
            var directSchema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Binary, interpolation: InstanceMaskInterpolationMode.NearestNeighbor, thresholdOrder: InstanceMaskThresholdOrder.BeforeResize, cropSpace: InstanceMaskCropSpace.None);
            using PreparedVisualInput input = Input(new VisualSize(4,4), new VisualSize(4,4), ImageTransform.Resize(new VisualSize(4,4), new VisualSize(4,4)));
            Assert.ThrowsExactly<VisualException>(() => Decode(
                new DirectInstanceSegmentationDecoder(directSchema, new InstanceSegmentationDecoderOptions(maximumCandidates: 2)),
                DirectProfile(new DirectInstanceSegmentationDecoder(directSchema), new TensorShape(1,3,2,2)), input,
                DirectOutputs(FilledMasks(3,2,2,1), new TensorShape(1,3,2,2))));
            var pixelDecoder = new DirectInstanceSegmentationDecoder(directSchema, new InstanceSegmentationDecoderOptions(maximumCandidates: 3, maximumMaskPixels: 11));
            Assert.ThrowsExactly<VisualException>(() => Decode(pixelDecoder, DirectProfile(pixelDecoder, new TensorShape(1,3,2,2)), input, DirectOutputs(FilledMasks(3,2,2,1), new TensorShape(1,3,2,2))));
            var resultDecoder = new DirectInstanceSegmentationDecoder(directSchema, new InstanceSegmentationDecoderOptions(maximumCandidates: 3, maximumResultBytes: 8, generateRle: false));
            Assert.ThrowsExactly<VisualException>(() => Decode(resultDecoder, DirectProfile(resultDecoder, new TensorShape(1,3,2,2)), input, DirectOutputs(FilledMasks(3,2,2,1), new TensorShape(1,3,2,2))));
            var rleDecoder = new DirectInstanceSegmentationDecoder(directSchema, new InstanceSegmentationDecoderOptions(maximumCandidates: 3, maximumRleRuns: 1));
            float[] checkerboard = FilledMasks(3,2,2,0); checkerboard[0] = 1; checkerboard[3] = 1;
            Assert.ThrowsExactly<VisualException>(() => Decode(rleDecoder, DirectProfile(rleDecoder, new TensorShape(1,3,2,2)), input, DirectOutputs(checkerboard, new TensorShape(1,3,2,2))));

            PrototypeInstanceSegmentationDecoder prototype = PrototypeDecoder(InstanceMaskTensorLayout.Nchw, new InstanceSegmentationDecoderOptions(maximumCandidates: 3, maximumPrototypeChannels: 1));
            Assert.ThrowsExactly<VisualException>(() => Decode(prototype, PrototypeProfile(prototype, TensorElementType.Float32), input, PrototypeOutputs(false)));
            PrototypeInstanceSegmentationDecoder workspace = PrototypeDecoder(InstanceMaskTensorLayout.Nchw, new InstanceSegmentationDecoderOptions(maximumCandidates: 3, maximumWorkspaceBytes: 8));
            Assert.ThrowsExactly<VisualException>(() => Decode(workspace, PrototypeProfile(workspace, TensorElementType.Float32), input, PrototypeOutputs(false)));
        }

        [TestMethod]
        public async Task ReusableDecoderIsConcurrentAndCancellationIsObserved()
        {
            var schema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Binary, interpolation: InstanceMaskInterpolationMode.NearestNeighbor, thresholdOrder: InstanceMaskThresholdOrder.BeforeResize, cropSpace: InstanceMaskCropSpace.None);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(generateRle: false, maximumCandidates: 3, maximumInstances: 3));
            VisualModelProfile profile = DirectProfile(decoder, new TensorShape(1,3,8,8));
            InferenceOutputs outputs = DirectOutputs(FilledMasks(3,8,8,1), new TensorShape(1,3,8,8));
            using PreparedVisualInput input = Input(new VisualSize(64,64), new VisualSize(8,8), ImageTransform.Resize(new VisualSize(64,64), new VisualSize(8,8)));
            string expected = Decode(decoder, profile, input, outputs).ComputeSha256();
            var tasks = new Task<string>[8];
            for (int index = 0; index < tasks.Length; index++) tasks[index] = Task.Run(() => Decode(decoder, profile, input, outputs).ComputeSha256());
            string[] hashes = await Task.WhenAll(tasks);
            foreach (string hash in hashes) Assert.AreEqual(expected, hash);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.ThrowsExactly<OperationCanceledException>(() => decoder.Decode(new VisualDecodeContext(input, profile, outputs, cancellation.Token)));
        }

        [TestMethod]
        public async Task FakeBackendRunsCompletePipelineAndResultOutlivesSession()
        {
            var schema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Binary, interpolation: InstanceMaskInterpolationMode.NearestNeighbor, thresholdOrder: InstanceMaskThresholdOrder.BeforeResize, cropSpace: InstanceMaskCropSpace.None);
            var decoder = new DirectInstanceSegmentationDecoder(schema, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, generateRle: true, maximumCandidates: 3, maximumInstances: 3));
            VisualModelProfile profile = DirectProfile(decoder, new TensorShape(1,3,2,2));
            float[] masks = FilledMasks(3, 2, 2, 1);
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, new TensorShape(1,3,4), _ => DirectOutputs(masks, new TensorShape(1,3,2,2)));
            using PreparedVisualInput input = Input(new VisualSize(4,4), new VisualSize(4,4), ImageTransform.Resize(new VisualSize(4,4), new VisualSize(4,4)));

            VisualInferenceResult inference = await fixture.Pipeline.RunAsync(input);
            InstanceSegmentationResult result = inference.GetValue<InstanceSegmentationResult>();
            fixture.Pipeline.Dispose();
            masks[0] = 0;

            Assert.AreEqual(2, result.Instances.Count);
            Assert.IsTrue(result.Instances[0].Mask.IsForeground(0, 0));
            Assert.IsNotNull(result.Instances[0].Rle);
            Assert.AreEqual(1, fixture.Provider.LastSession!.DisposeCount);
        }

        [TestMethod]
        public void DirectAndPrototypePerformanceEntryReportsThroughputWithoutAbsoluteThreshold()
        {
            var directSchema = new DirectInstanceSegmentationOutputSchema(Candidates(), "masks", InstanceMaskTensorLayout.Nchw, InstanceMaskValueKind.Binary, interpolation: InstanceMaskInterpolationMode.NearestNeighbor, thresholdOrder: InstanceMaskThresholdOrder.BeforeResize, cropSpace: InstanceMaskCropSpace.None);
            var direct = new DirectInstanceSegmentationDecoder(directSchema, new InstanceSegmentationDecoderOptions(generateRle: false, maximumCandidates: 3));
            var prototype = PrototypeDecoder(InstanceMaskTensorLayout.Nchw, new InstanceSegmentationDecoderOptions(generateRle: false, maximumCandidates: 3));
            using PreparedVisualInput input = Input(new VisualSize(64,64), new VisualSize(8,8), ImageTransform.Resize(new VisualSize(64,64), new VisualSize(8,8)));
            var stopwatch = Stopwatch.StartNew();
            InstanceSegmentationResult directResult = Decode(direct, DirectProfile(direct, new TensorShape(1,3,8,8)), input, DirectOutputs(FilledMasks(3,8,8,1), new TensorShape(1,3,8,8)));
            long directTicks = stopwatch.ElapsedTicks;
            stopwatch.Restart();
            InstanceSegmentationResult prototypeResult = Decode(prototype, PrototypeProfile(prototype, TensorElementType.Float32), input, PrototypeOutputs(false));
            long prototypeTicks = stopwatch.ElapsedTicks;
            TestContext?.WriteLine("directTicks={0}; prototypeTicks={1}; sourcePixels={2}; directInstances={3}; prototypeInstances={4}", directTicks, prototypeTicks, 4096, directResult.Instances.Count, prototypeResult.Instances.Count);
            Assert.AreEqual(2, directResult.Instances.Count);
            Assert.AreEqual(2, prototypeResult.Instances.Count);
        }

        private static void RunPrototypeLayout(InstanceMaskTensorLayout layout, bool useDouble)
        {
            PrototypeInstanceSegmentationDecoder decoder = PrototypeDecoder(layout, new InstanceSegmentationDecoderOptions(scoreThreshold: .1f, maximumCandidates: 3, maximumInstances: 3));
            VisualModelProfile profile = PrototypeProfile(decoder, useDouble ? TensorElementType.Float64 : TensorElementType.Float32);
            using PreparedVisualInput input = Input(new VisualSize(4,4), new VisualSize(4,4), ImageTransform.Resize(new VisualSize(4,4), new VisualSize(4,4)));
            InstanceSegmentationResult result = Decode(decoder, profile, input, PrototypeOutputs(useDouble, layout));
            Assert.AreEqual(2, result.Instances.Count);
            Assert.AreEqual(0, result.Instances[0].SourceIndex);
            Assert.AreEqual(2, result.Instances[1].SourceIndex);
            Assert.IsTrue(result.Instances[0].Mask.IsForeground(0, 0));
            Assert.IsFalse(result.Instances[0].Mask.IsForeground(3, 3));
            Assert.IsTrue(result.Instances[1].Mask.IsForeground(3, 0));
        }

        private static InstanceSegmentationInstance Instance(int sourceIndex, float score, byte[] pixels)
            => new InstanceSegmentationInstance(sourceIndex, 0, "part", score, new RectangleF(0,0,1,1), new InstanceBinaryMask(3,2,pixels));

        private static InstanceSegmentationCandidateSchema Candidates()
            => new InstanceSegmentationCandidateSchema("boxes", "scores", "classes");

        private static PrototypeInstanceSegmentationDecoder PrototypeDecoder(InstanceMaskTensorLayout layout, InstanceSegmentationDecoderOptions options)
            => new PrototypeInstanceSegmentationDecoder(new PrototypeInstanceSegmentationOutputSchema(Candidates(), "prototypes", "coefficients", layout, cropSpace: InstanceMaskCropSpace.None), options);

        private static VisualModelProfile DirectProfile(DirectInstanceSegmentationDecoder decoder, TensorShape maskShape, TensorElementType maskType = TensorElementType.Float32)
            => new VisualModelProfile(
                "tests/direct-instance.v1", new ModelId("tests/direct-instance"), VisualTaskId.InstanceSegmentation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,-1,-1), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("masks", maskType, maskShape)
                },
                new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);

        private static VisualModelProfile PrototypeProfile(PrototypeInstanceSegmentationDecoder decoder, TensorElementType type)
        {
            TensorShape prototypeShape = decoder.Schema.PrototypeLayout == InstanceMaskTensorLayout.Nchw ? new TensorShape(1,2,4,4) : new TensorShape(1,4,4,2);
            return new VisualModelProfile(
                "tests/prototype-instance.v1", new ModelId("tests/prototype-instance"), VisualTaskId.InstanceSegmentation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,-1,-1), VisualTensorLayout.Nchw),
                new[]
                {
                    new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(1,3,4)),
                    new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("classes", TensorElementType.Float32, new TensorShape(1,3)),
                    new VisualOutputBinding("prototypes", type, prototypeShape),
                    new VisualOutputBinding("coefficients", type, new TensorShape(1,3,2))
                },
                new[] { new VisualLabel(0,"alpha"), new VisualLabel(1,"beta") }, decoder);
        }

        private static InferenceOutputs DirectOutputs(float[] masks, TensorShape shape)
            => Outputs(("boxes", Float(new TensorShape(1,3,4), Boxes())), ("scores", Float(new TensorShape(1,3), Scores())), ("classes", Float(new TensorShape(1,3), Classes())), ("masks", Float(shape, masks)));

        private static InferenceOutputs DirectOutputs(double[] masks, TensorShape shape)
            => Outputs(("boxes", Float(new TensorShape(1,3,4), Boxes())), ("scores", Float(new TensorShape(1,3), Scores())), ("classes", Float(new TensorShape(1,3), Classes())), ("masks", new Tensor<double>(shape, masks, TensorBufferOwnership.Borrow)));

        private static InferenceOutputs PrototypeOutputs(bool useDouble, InstanceMaskTensorLayout layout = InstanceMaskTensorLayout.Nchw)
        {
            float[] nchw =
            {
                10,10,-10,-10, 10,10,-10,-10, 10,10,-10,-10, 10,10,-10,-10,
                -10,-10,10,10, -10,-10,10,10, -10,-10,10,10, -10,-10,10,10
            };
            float[] prototypes = nchw;
            if (layout == InstanceMaskTensorLayout.Nhwc)
            {
                prototypes = new float[32];
                for (int position = 0; position < 16; position++) { prototypes[position * 2] = nchw[position]; prototypes[(position * 2) + 1] = nchw[16 + position]; }
            }
            float[] coefficients = { 1,0, 1,0, 0,1 };
            TensorShape prototypeShape = layout == InstanceMaskTensorLayout.Nchw ? new TensorShape(1,2,4,4) : new TensorShape(1,4,4,2);
            ITensor prototypeTensor = useDouble ? new Tensor<double>(prototypeShape, Array.ConvertAll(prototypes, value => (double)value)) : Float(prototypeShape, prototypes);
            ITensor coefficientTensor = useDouble ? new Tensor<double>(new TensorShape(1,3,2), Array.ConvertAll(coefficients, value => (double)value)) : Float(new TensorShape(1,3,2), coefficients);
            return Outputs(("boxes", Float(new TensorShape(1,3,4), Boxes())), ("scores", Float(new TensorShape(1,3), Scores())), ("classes", Float(new TensorShape(1,3), Classes())), ("prototypes", prototypeTensor), ("coefficients", coefficientTensor));
        }

        private static float[] Boxes() => new float[] { 0,0,3,3, .1f,.1f,3.1f,3.1f, 2,0,4,2 };
        private static float[] Scores() => new[] { .9f, .8f, .9f };
        private static float[] Classes() => new[] { 0f, 0f, 1f };

        private static float[] FilledMasks(int candidates, int width, int height, float value)
        {
            var result = new float[checked(candidates * width * height)];
            for (int index = 0; index < result.Length; index++) result[index] = value;
            return result;
        }

        private static Tensor<float> Float(TensorShape shape, float[] values) => new Tensor<float>(shape, values, TensorBufferOwnership.Borrow);

        private static PreparedVisualInput Input(VisualSize source, VisualSize model, ImageTransform transform)
            => new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,model.Height,model.Width), new float[checked(3 * model.Height * model.Width)]), source, model, 1, VisualTensorLayout.Nchw, transform);

        private static InstanceSegmentationResult Decode(IVisualDecoder decoder, VisualModelProfile profile, PreparedVisualInput input, InferenceOutputs outputs, CancellationToken cancellationToken = default)
            => (InstanceSegmentationResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, cancellationToken));

        private static InferenceOutputs Outputs(params (string Name, ITensor Tensor)[] tensors)
        {
            var values = new List<NamedTensor>(tensors.Length);
            for (int index = 0; index < tensors.Length; index++) values.Add(new NamedTensor(tensors[index].Name, tensors[index].Tensor));
            return new InferenceOutputs(values);
        }
    }
}
