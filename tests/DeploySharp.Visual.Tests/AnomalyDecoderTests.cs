using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed class AnomalyDecoderTests
    {
        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void ProbabilityMapOwnsRawNormalizedMaskScoreAndCanonicalHash()
        {
            var schema = new AnomalyMapSchema("image_score", "anomaly_map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 2);
            var options = new AnomalyDecoderOptions(outputSizeMode: AnomalyOutputSizeMode.Tensor, channelAggregation: AnomalyChannelAggregation.SingleChannel, channelIndex: 0, interpolation: AnomalyMapInterpolation.Nearest);
            VisualModelProfile profile = Profile(schema, new TensorShape(1,2,2,3), options);
            float[] supplied = { 0f, .25f, .5f, .75f, 1f, .4f, 1f, .8f, .6f, .4f, .2f, 0f };
            AnomalyDetectionResult result = Decode(profile, Input(new VisualSize(3,2)), new Tensor<float>(new TensorShape(1,2,2,3), supplied), .875f);

            Assert.AreEqual(.875f, result.ImageScore, .000001f);
            Assert.IsNotNull(result.RawMap);
            CollectionAssert.AreEqual(new[] { 0f, .25f, .5f, .75f, 1f, .4f }, result.RawMap!.ToArray());
            CollectionAssert.AreEqual(result.RawMap.ToArray(), result.NormalizedMap.ToArray());
            CollectionAssert.AreEqual(new byte[] { 0,0,1,1,1,0 }, result.Mask.ToArray());
            Assert.AreEqual(.5d, result.AnomalousPixelRatio, .000001d);
            string digest = result.ComputeSha256();
            Assert.AreEqual(64, digest.Length);
            supplied[0] = 1f;
            float[] returned = result.RawMap.ToArray();
            returned[1] = 1f;
            CollectionAssert.AreEqual(new[] { 0f, .25f, .5f, .75f, 1f, .4f }, result.RawMap.ToArray());
            Assert.AreEqual(digest, result.ComputeSha256());
        }

        [TestMethod]
        public void ChannelAggregationAndLayoutsAreDeterministicForFloat32AndFloat64()
        {
            var maximumOptions = new AnomalyDecoderOptions(outputSizeMode: AnomalyOutputSizeMode.Tensor, channelAggregation: AnomalyChannelAggregation.Maximum, preserveRawMap: false);
            var nchw = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Logits, AnomalyTensorLayout.Nchw, 2);
            VisualModelProfile nchwProfile = Profile(nchw, new TensorShape(1,2,1,3), maximumOptions, TensorElementType.Float64);
            AnomalyDetectionResult maximum = Decode(nchwProfile, Input(new VisualSize(3,1)), new Tensor<double>(new TensorShape(1,2,1,3), new[] { -1d, 4d, 2d, 3d, 1d, 2d }), 4d);
            CollectionAssert.AreEqual(new[] { 3f,4f,2f }, maximum.NormalizedMap.ToArray());
            Assert.IsNull(maximum.RawMap);

            var meanOptions = new AnomalyDecoderOptions(outputSizeMode: AnomalyOutputSizeMode.Tensor, channelAggregation: AnomalyChannelAggregation.Mean);
            var nhwc = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Distances, AnomalyTensorLayout.Nhwc, 2);
            VisualModelProfile nhwcProfile = Profile(nhwc, new TensorShape(1,1,3,2), meanOptions);
            AnomalyDetectionResult mean = Decode(nhwcProfile, Input(new VisualSize(3,1)), new Tensor<float>(new TensorShape(1,1,3,2), new[] { 1f,3f,4f,2f,2f,2f }), 4f);
            CollectionAssert.AreEqual(new[] { 2f,3f,2f }, mean.RawMap!.ToArray());

            foreach (AnomalyTensorLayout layout in new[] { AnomalyTensorLayout.Chw, AnomalyTensorLayout.Hwc })
            {
                TensorShape shape = layout == AnomalyTensorLayout.Chw ? new TensorShape(2,1,2) : new TensorShape(1,2,2);
                float[] values = layout == AnomalyTensorLayout.Chw ? new[] { .1f,.9f,.8f,.2f } : new[] { .1f,.8f,.9f,.2f };
                var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Probabilities, layout, 2);
                VisualModelProfile profile = Profile(schema, shape, maximumOptions);
                CollectionAssert.AreEqual(new[] { .8f,.9f }, Decode(profile, Input(new VisualSize(2,1)), new Tensor<float>(shape, values), .9f).NormalizedMap.ToArray());
            }
        }

        [TestMethod]
        public void MinMaxFixedRangeAndConstantMapsHaveExplicitSemantics()
        {
            var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Logits, AnomalyTensorLayout.Nchw, 1);
            var minMax = new AnomalyDecoderOptions(normalization: AnomalyNormalizationMode.MinMax, threshold: .5f, outputSizeMode: AnomalyOutputSizeMode.Tensor);
            AnomalyDetectionResult normalized = Decode(Profile(schema, new TensorShape(1,1,1,4), minMax), Input(new VisualSize(4,1)), new Tensor<float>(new TensorShape(1,1,1,4), new[] { -2f,0f,2f,6f }), 6f);
            CollectionAssert.AreEqual(new[] { 0f,.25f,.5f,1f }, normalized.NormalizedMap.ToArray());
            CollectionAssert.AreEqual(new byte[] { 0,0,1,1 }, normalized.Mask.ToArray());
            Assert.AreEqual(0, normalized.Warnings.Count);

            var fixedRange = new AnomalyDecoderOptions(normalization: AnomalyNormalizationMode.FixedRange, fixedRangeMinimum: -1f, fixedRangeMaximum: 3f, threshold: .5f, outputSizeMode: AnomalyOutputSizeMode.Tensor);
            AnomalyDetectionResult fixedResult = Decode(Profile(schema, new TensorShape(1,1,1,4), fixedRange), Input(new VisualSize(4,1)), new Tensor<float>(new TensorShape(1,1,1,4), new[] { -2f,-1f,1f,5f }), 1f);
            CollectionAssert.AreEqual(new[] { 0f,0f,.5f,1f }, fixedResult.NormalizedMap.ToArray());

            AnomalyDetectionResult constant = Decode(Profile(schema, new TensorShape(1,1,1,3), minMax), Input(new VisualSize(3,1)), new Tensor<float>(new TensorShape(1,1,1,3), new[] { 2f,2f,2f }), 2f);
            CollectionAssert.AreEqual(new[] { 0f,0f,0f }, constant.NormalizedMap.ToArray());
            Assert.AreEqual("anomaly.constant-map", constant.Warnings.Single().Code);
        }

        [TestMethod]
        public void SourceRestorationHandlesResizeLetterboxCropAndHalfPixelInterpolation()
        {
            var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 1);
            var nearest = new AnomalyDecoderOptions(outputSizeMode: AnomalyOutputSizeMode.Source, interpolation: AnomalyMapInterpolation.Nearest);
            var source4 = new VisualSize(4,4);
            var model2 = new VisualSize(2,2);
            AnomalyDetectionResult resize = Decode(Profile(schema, new TensorShape(1,1,2,2), nearest, modelSize: model2), Input(source4, model2, ImageTransform.Resize(source4, model2)), new Tensor<float>(new TensorShape(1,1,2,2), new[] { 0f,1f,.5f,.75f }), .75f);
            CollectionAssert.AreEqual(new[] { 0f,0f,1f,1f,0f,0f,1f,1f,.5f,.5f,.75f,.75f,.5f,.5f,.75f,.75f }, resize.NormalizedMap.ToArray());

            var letterboxSource = new VisualSize(4,2);
            var letterboxModel = new VisualSize(4,4);
            float[] letterboxMap = { 0,0,0,0, .2f,.4f,.6f,.8f, .1f,.3f,.5f,.7f, 0,0,0,0 };
            AnomalyDetectionResult letterbox = Decode(Profile(schema, new TensorShape(1,1,4,4), nearest, modelSize: letterboxModel), Input(letterboxSource, letterboxModel, ImageTransform.Letterbox(letterboxSource, letterboxModel)), new Tensor<float>(new TensorShape(1,1,4,4), letterboxMap), .8f);
            CollectionAssert.AreEqual(new[] { .2f,.4f,.6f,.8f,.1f,.3f,.5f,.7f }, letterbox.NormalizedMap.ToArray());

            ImageTransform cropTransform = ImageTransform.Crop(source4, model2, new RectangleF(1,1,2,2));
            AnomalyDetectionResult crop = Decode(Profile(schema, new TensorShape(1,1,2,2), nearest, modelSize: model2), Input(source4, model2, cropTransform), new Tensor<float>(new TensorShape(1,1,2,2), new[] { .2f,.4f,.6f,.8f }), .8f);
            CollectionAssert.AreEqual(new[] { 0f,0f,0f,0f,0f,.2f,.4f,0f,0f,.6f,.8f,0f,0f,0f,0f,0f }, crop.NormalizedMap.ToArray());

            var bilinear = new AnomalyDecoderOptions(outputSizeMode: AnomalyOutputSizeMode.Model, interpolation: AnomalyMapInterpolation.BilinearHalfPixel);
            var model3 = new VisualSize(3,1);
            AnomalyDetectionResult interpolated = Decode(Profile(schema, new TensorShape(1,1,1,2), bilinear, modelSize: model3), Input(model3), new Tensor<float>(new TensorShape(1,1,1,2), new[] { 0f,1f }), 1f);
            float[] actual = interpolated.NormalizedMap.ToArray();
            Assert.AreEqual(0f, actual[0], .00001f);
            Assert.AreEqual(.5f, actual[1], .00001f);
            Assert.AreEqual(1f, actual[2], .00001f);
        }

        [TestMethod]
        public void SourceCoordinateMapRequiresExactSourceDimensions()
        {
            var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Chw, 1, AnomalyMapCoordinateSpace.SourceImage);
            var options = new AnomalyDecoderOptions(outputSizeMode: AnomalyOutputSizeMode.Source);
            VisualModelProfile profile = Profile(schema, new TensorShape(1,2,2), options, modelSize: new VisualSize(3,3));
            PreparedVisualInput input = Input(new VisualSize(2,2), new VisualSize(3,3), ImageTransform.Resize(new VisualSize(2,2), new VisualSize(3,3)));
            AnomalyDetectionResult result = Decode(profile, input, new Tensor<float>(new TensorShape(1,2,2), new[] { 0f,.5f,.75f,1f }), 1f);
            CollectionAssert.AreEqual(new[] { 0f,.5f,.75f,1f }, result.NormalizedMap.ToArray());

            PreparedVisualInput mismatchInput = Input(new VisualSize(3,2), new VisualSize(3,3), ImageTransform.Resize(new VisualSize(3,2), new VisualSize(3,3)));
            VisualException mismatch = Assert.ThrowsExactly<VisualException>(() => Decode(profile, mismatchInput, new Tensor<float>(new TensorShape(1,2,2), new[] { 0f,.5f,.75f,1f }), 1f));
            Assert.AreEqual(VisualErrorCodes.AnomalyContractInvalid, mismatch.ErrorCode);
        }

        [TestMethod]
        public void InvalidNamesShapesTypesRangesAndLimitsHaveStableDiagnostics()
        {
            var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 1);
            VisualModelProfile profile = Profile(schema, new TensorShape(1,1,1,2));
            using PreparedVisualInput input = Input(new VisualSize(2,1));
            var decoder = (AnomalyDecoder)profile.Decoder;
            var extra = new InferenceOutputs(new[] { new NamedTensor("score", new Tensor<float>(new TensorShape(1), new[] { .8f })), new NamedTensor("map", new Tensor<float>(new TensorShape(1,1,1,2), new[] { 0f,1f })), new NamedTensor("extra", new Tensor<float>(new TensorShape(1), new[] { 0f })) });
            Assert.AreEqual(VisualErrorCodes.AnomalyContractInvalid, Assert.ThrowsExactly<VisualException>(() => decoder.DecodeAnomaly(new VisualDecodeContext(input, profile, extra, default(CancellationToken)))).ErrorCode);

            Assert.AreEqual(VisualErrorCodes.AnomalyContractInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(profile, Input(new VisualSize(2,1)), new Tensor<float>(new TensorShape(1,1,1,2), new[] { 0f,1.1f }), .8f)).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AnomalyContractInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(profile, Input(new VisualSize(2,1)), new Tensor<float>(new TensorShape(1,1,1,2), new[] { 0f,float.NaN }), .8f)).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AnomalyContractInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(profile, Input(new VisualSize(2,1)), new Tensor<float>(new TensorShape(1,1,1,2), new[] { 0f,1f }), 1.1f)).ErrorCode);

            var bounded = new AnomalyDecoderOptions(maximumMapPixels: 1);
            VisualModelProfile boundedProfile = Profile(schema, new TensorShape(1,1,1,2), bounded);
            Assert.AreEqual(VisualErrorCodes.AnomalyLimitExceeded, Assert.ThrowsExactly<VisualException>(() => Decode(boundedProfile, Input(new VisualSize(2,1)), new Tensor<float>(new TensorShape(1,1,1,2), new[] { 0f,1f }), .8f)).ErrorCode);

            var distance = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Distances, AnomalyTensorLayout.Nchw, 1);
            Assert.AreEqual(VisualErrorCodes.AnomalyContractInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(Profile(distance, new TensorShape(1,1,1,1)), Input(new VisualSize(1,1)), new Tensor<float>(new TensorShape(1,1,1,1), new[] { -1f }), 0f)).ErrorCode);
            var binary = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Binary, AnomalyTensorLayout.Nchw, 1);
            Assert.AreEqual(VisualErrorCodes.AnomalyContractInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(Profile(binary, new TensorShape(1,1,1,1)), Input(new VisualSize(1,1)), new Tensor<float>(new TensorShape(1,1,1,1), new[] { .5f }), 1f)).ErrorCode);
        }

        [TestMethod]
        public void ReservedThresholdPoliciesAreExplicitlyUnsupportedAndCancellationPropagates()
        {
            var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 1);
            var percentile = new AnomalyDecoderOptions(thresholdPolicy: AnomalyThresholdPolicy.Percentile);
            VisualModelProfile percentileProfile = Profile(schema, new TensorShape(1,1,1,1), percentile);
            Assert.AreEqual(VisualErrorCodes.AnomalyCapabilityUnavailable, Assert.ThrowsExactly<VisualException>(() => Decode(percentileProfile, Input(new VisualSize(1,1)), new Tensor<float>(new TensorShape(1,1,1,1), new[] { .5f }), .5f)).ErrorCode);

            using var source = new CancellationTokenSource();
            source.Cancel();
            VisualModelProfile profile = Profile(schema, new TensorShape(1,1,1,1));
            Assert.ThrowsExactly<OperationCanceledException>(() => Decode(profile, Input(new VisualSize(1,1)), new Tensor<float>(new TensorShape(1,1,1,1), new[] { .5f }), .5f, source.Token));
        }

        [TestMethod]
        public async Task TypedPipelineRunsAsyncTimesOutRecoversAndDisposesExactlyOnce()
        {
            var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 1);
            VisualModelProfile profile = Profile(schema, new TensorShape(1,1,2,3));
            using var fixture = new AnomalyPipelineFixture(profile, _ => Outputs(new Tensor<float>(new TensorShape(1,1,2,3), new[] { 0f,.25f,.5f,.75f,1f,.4f }), .875f));
            using (PreparedVisualInput input = Input(new VisualSize(3,2)))
            {
                AnomalyDetectionResult result = await fixture.Pipeline.RunAsync(input);
                Assert.AreEqual(.875f, result.ImageScore, .000001f);
                Assert.IsTrue(result.Timing.Inference >= TimeSpan.Zero);
            }

            fixture.Provider.Delay = TimeSpan.FromMilliseconds(200);
            using (PreparedVisualInput input = Input(new VisualSize(3,2)))
            {
                VisualException timeout = await Assert.ThrowsExactlyAsync<VisualException>(() => fixture.Pipeline.RunAsync(input, new VisualExecutionOptions(TimeSpan.FromMilliseconds(20))));
                Assert.AreEqual(VisualErrorCodes.Timeout, timeout.ErrorCode);
            }
            fixture.Provider.Delay = TimeSpan.Zero;
            using (PreparedVisualInput input = Input(new VisualSize(3,2))) Assert.AreEqual(.875f, fixture.Pipeline.Run(input).ImageScore, .000001f);
            fixture.Pipeline.Dispose();
            fixture.Pipeline.Dispose();
            Assert.AreEqual(1, fixture.Provider.LastSession!.DisposeCount);
            using PreparedVisualInput rejected = Input(new VisualSize(3,2));
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, Assert.ThrowsExactly<VisualException>(() => fixture.Pipeline.Run(rejected)).ErrorCode);
        }

        [TestMethod]
        public void PerformanceEntryRecordsMapThroughputAllocationAndStableResult()
        {
            const int width = 128;
            const int height = 96;
            var schema = new AnomalyMapSchema("score", "map", AnomalyMapValueMode.Probabilities, AnomalyTensorLayout.Nchw, 2);
            var options = new AnomalyDecoderOptions(normalization: AnomalyNormalizationMode.FixedRange, channelAggregation: AnomalyChannelAggregation.Maximum, outputSizeMode: AnomalyOutputSizeMode.Tensor);
            VisualModelProfile profile = Profile(schema, new TensorShape(1,2,height,width), options, modelSize: new VisualSize(width,height));
            var values = new float[checked(2 * width * height)];
            for (int index = 0; index < values.Length; index++) values[index] = (index % 101) / 100f;
            string? golden = null;
            long before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int iteration = 0; iteration < 20; iteration++)
            {
                AnomalyDetectionResult result = Decode(profile, Input(new VisualSize(width,height)), new Tensor<float>(new TensorShape(1,2,height,width), values), .99f);
                golden ??= result.ComputeSha256();
                Assert.AreEqual(golden, result.ComputeSha256());
            }
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double mapsPerSecond = 20d / watch.Elapsed.TotalSeconds;
            TestContext?.WriteLine($"ANOMALY_PERF pixels={width * height} channels=2 iterations=20 elapsedMs={watch.Elapsed.TotalMilliseconds:F3} mapsPerSecond={mapsPerSecond:F1} allocatedBytes={allocated} sha256={golden}");
            Assert.IsTrue(mapsPerSecond > 0d);
        }

        private static AnomalyDetectionResult Decode(VisualModelProfile profile, PreparedVisualInput input, ITensor map, double score, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (input)
            {
                return ((IAnomalyPostprocessor)profile.Decoder).DecodeAnomaly(new VisualDecodeContext(input, profile, Outputs(map, score, profile.Outputs[0].Name, profile.Outputs[1].Name), cancellationToken));
            }
        }

        private static InferenceOutputs Outputs(ITensor map, double score, string scoreName = "score", string mapName = "map")
        {
            ITensor scoreTensor = map.ElementType == TensorElementType.Float64
                ? new Tensor<double>(new TensorShape(1), new[] { score })
                : new Tensor<float>(new TensorShape(1), new[] { (float)score });
            return new InferenceOutputs(new[] { new NamedTensor(scoreName, scoreTensor), new NamedTensor(mapName, map) });
        }

        private static VisualModelProfile Profile(AnomalyMapSchema schema, TensorShape mapShape, AnomalyDecoderOptions? options = null, TensorElementType outputType = TensorElementType.Float32, VisualSize? modelSize = null)
        {
            VisualSize size = modelSize ?? new VisualSize((int)mapShape[mapShape.Rank - 1], (int)mapShape[mapShape.Rank - 2]);
            return new VisualModelProfile(
                "tests/anomaly.v1", new ModelId("tests/anomaly"), VisualTaskId.AnomalyDetection, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1,3,size.Height,size.Width), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(schema.ScoreOutputName, outputType, new TensorShape(1)), new VisualOutputBinding(schema.MapOutputName, outputType, mapShape) },
                Array.Empty<VisualLabel>(), new AnomalyDecoder(schema, options));
        }

        private static PreparedVisualInput Input(VisualSize size) => Input(size, size, ImageTransform.Resize(size, size));
        private static PreparedVisualInput Input(VisualSize source, VisualSize model, ImageTransform transform) => new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1,3,model.Height,model.Width), new float[checked(3 * model.Height * model.Width)]), source, model, 1, VisualTensorLayout.Nchw, transform);

        private sealed class AnomalyPipelineFixture : IDisposable
        {
            private readonly BackendRegistry _registry;
            public AnomalyPipelineFixture(VisualModelProfile profile, Func<InferenceInputs, InferenceOutputs> outputs)
            {
                Provider = new FakeVisualBackendProvider(VisualTestData.Metadata(profile, new TensorShape(1)), outputs);
                _registry = new BackendRegistry();
                _registry.Register(Provider);
                var profiles = new VisualProfileRegistry(); profiles.Register(profile); profiles.Freeze();
                var request = new BackendRequest(BackendCapabilities.TensorInference, VisualTestData.BackendId);
                VisualProfileSelection selection = profiles.Select(new ModelArtifact(profile.ModelId, "fake", "fixture.fake", preferredBackend: VisualTestData.BackendId), _registry, request, VisualTaskId.AnomalyDetection);
                Pipeline = new AnomalyPipeline(_registry, selection, request);
            }
            public FakeVisualBackendProvider Provider { get; }
            public AnomalyPipeline Pipeline { get; }
            public void Dispose() { Pipeline.Dispose(); _registry.Dispose(); }
        }
    }
}
