using System;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class SemanticSegmentationDecoderTests
    {
        [TestMethod]
        public void NchwLogitsUseStableArgmaxProduceStatisticsAndGoldenRle()
        {
            var schema = new SegmentationOutputSchema("logits", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, 3);
            VisualModelProfile profile = Profile(schema, TensorElementType.Float32, new TensorShape(1, 3, 2, 3));
            float[] logits =
            {
                9, 0, 0, 1, 5, 0,
                0, 9, 0, 1, 5, 9,
                0, 0, 9, 0, 5, 9
            };
            SemanticSegmentationResult result = Decode(profile, Input(new VisualSize(3, 2)), new Tensor<float>(new TensorShape(1, 3, 2, 3), logits));

            CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 0, 0, 1 }, result.Mask.ToArray());
            Assert.AreEqual("2ed4fa5094662ebe63d9265149adf86858fd7b03983a35118880f09517f824de", result.Mask.ComputeSha256());
            Assert.AreEqual(3, result.Statistics[0].PixelCount);
            Assert.AreEqual(2, result.Statistics[1].PixelCount);
            Assert.AreEqual(1, result.Statistics[2].PixelCount);
            Assert.IsNotNull(result.Rle);
            CollectionAssert.AreEqual(result.Mask.ToArray(), result.Rle!.Decode().ToArray());
            Assert.AreEqual(SegmentationPolygonStatus.Unsupported, result.PolygonStatus);
            Assert.IsTrue(result.Classes[0].IsBackground);
            Assert.AreEqual("road", result.Classes[1].Label);
        }

        [TestMethod]
        public void RowMajorRleCoversEmptyForegroundUniformSinglePixelBoundariesAndOverflow()
        {
            var emptyForeground = new SemanticSegmentationMask(4, 1, new ushort[] { 0, 0, 0, 0 });
            SegmentationRle emptyForegroundRle = SegmentationRle.Encode(emptyForeground);
            Assert.AreEqual(1, emptyForegroundRle.Runs.Count);
            Assert.AreEqual(0, emptyForegroundRle.Runs[0].Start);
            Assert.AreEqual(4, emptyForegroundRle.Runs[0].Length);
            Assert.AreEqual((ushort)0, emptyForegroundRle.Runs[0].ClassIndex);
            CollectionAssert.AreEqual(emptyForeground.ToArray(), emptyForegroundRle.Decode().ToArray());

            var fullForeground = new SemanticSegmentationMask(3, 1, new ushort[] { 1, 1, 1 });
            SegmentationRle fullForegroundRle = SegmentationRle.Encode(fullForeground);
            Assert.AreEqual(1, fullForegroundRle.Runs.Count);
            Assert.AreEqual((ushort)1, fullForegroundRle.Runs[0].ClassIndex);

            var singlePixel = new SemanticSegmentationMask(1, 1, new ushort[] { ushort.MaxValue });
            CollectionAssert.AreEqual(singlePixel.ToArray(), SegmentationRle.Encode(singlePixel).Decode().ToArray());

            var boundaries = new SemanticSegmentationMask(4, 1, new ushort[] { 1, 0, 0, 2 });
            SegmentationRle boundaryRle = SegmentationRle.Encode(boundaries);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, boundaryRle.Runs.Select(run => run.Start).ToArray());
            CollectionAssert.AreEqual(boundaries.ToArray(), boundaryRle.Decode().ToArray());

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SemanticSegmentationMask(0, 1, Array.Empty<ushort>()));
            Assert.ThrowsExactly<OverflowException>(() => new SegmentationRle(int.MaxValue, 2, Array.Empty<SegmentationRleRun>()));
        }

        [TestMethod]
        public void NhwcProbabilitiesPreserveCanonicalHwcValues()
        {
            var schema = new SegmentationOutputSchema("probabilities", SegmentationOutputKind.Probabilities, SegmentationTensorLayout.Nhwc, 3);
            var options = new SegmentationDecoderOptions(outputSizeMode: SegmentationOutputSizeMode.Tensor, preserveProbabilityMap: true, generateRle: false);
            VisualModelProfile profile = Profile(schema, TensorElementType.Float32, new TensorShape(1, 1, 2, 3), options);
            float[] probabilities = { 0.8f, 0.1f, 0.1f, 0.2f, 0.7f, 0.1f };
            SemanticSegmentationResult result = Decode(profile, Input(new VisualSize(2, 1)), new Tensor<float>(new TensorShape(1, 1, 2, 3), probabilities));

            CollectionAssert.AreEqual(new ushort[] { 0, 1 }, result.Mask.ToArray());
            Assert.IsNotNull(result.ProbabilityMap);
            CollectionAssert.AreEqual(probabilities, result.ProbabilityMap!.ToArray());
            Assert.AreEqual(3, result.ProbabilityMap.ClassCount);
            Assert.IsNull(result.Rle);
        }

        [TestMethod]
        public void ChwAndHwcScoreLayoutsProduceTheSameMask()
        {
            var chwSchema = new SegmentationOutputSchema("scores", SegmentationOutputKind.Logits, SegmentationTensorLayout.Chw, 3);
            VisualModelProfile chwProfile = Profile(chwSchema, TensorElementType.Float32, new TensorShape(3, 1, 2));
            SemanticSegmentationResult chw = Decode(chwProfile, Input(new VisualSize(2, 1)), new Tensor<float>(new TensorShape(3, 1, 2), new[] { 9f, 0f, 0f, 9f, 0f, 0f }));
            CollectionAssert.AreEqual(new ushort[] { 0, 1 }, chw.Mask.ToArray());

            var hwcSchema = new SegmentationOutputSchema("scores", SegmentationOutputKind.Logits, SegmentationTensorLayout.Hwc, 3);
            VisualModelProfile hwcProfile = Profile(hwcSchema, TensorElementType.Float32, new TensorShape(1, 2, 3));
            SemanticSegmentationResult hwc = Decode(hwcProfile, Input(new VisualSize(2, 1)), new Tensor<float>(new TensorShape(1, 2, 3), new[] { 9f, 0f, 0f, 0f, 9f, 0f }));
            CollectionAssert.AreEqual(chw.Mask.ToArray(), hwc.Mask.ToArray());
        }

        [TestMethod]
        public void SchemaAndOptionsRejectConflictingOrSilentlyIgnoredSemantics()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new SegmentationOutputSchema("labels", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Hw, 2, backgroundClassIndex: 0, ignoreClassIndex: 0));
            var probability = new SegmentationOutputSchema("scores", SegmentationOutputKind.Probabilities, SegmentationTensorLayout.Nchw, 2);
            Assert.ThrowsExactly<ArgumentException>(() => new SemanticSegmentationDecoder(probability, new SegmentationDecoderOptions(binaryThreshold: 1.1f)));
            var labels = new SegmentationOutputSchema("labels", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Hw, 2);
            Assert.ThrowsExactly<ArgumentException>(() => new SemanticSegmentationDecoder(labels, new SegmentationDecoderOptions(binaryThreshold: 0.5f)));
            Assert.ThrowsExactly<ArgumentException>(() => new SegmentationProbabilityMap(1, 1, 2, new[] { 0.5f, float.NaN }));
        }

        [TestMethod]
        public void SingleChannelThresholdNeverAppliesImplicitActivation()
        {
            var logitsSchema = new SegmentationOutputSchema("mask", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, 2);
            var logitsOptions = new SegmentationDecoderOptions(binaryThreshold: 0, outputSizeMode: SegmentationOutputSizeMode.Tensor);
            VisualModelProfile logitsProfile = Profile(logitsSchema, TensorElementType.Float32, new TensorShape(1, 1, 1, 4), logitsOptions);
            SemanticSegmentationResult logits = Decode(logitsProfile, Input(new VisualSize(4, 1)), new Tensor<float>(new TensorShape(1, 1, 1, 4), new[] { -0.01f, 0f, 0.49f, 2f }));
            CollectionAssert.AreEqual(new ushort[] { 0, 1, 1, 1 }, logits.Mask.ToArray());

            var probabilitySchema = new SegmentationOutputSchema("mask", SegmentationOutputKind.Probabilities, SegmentationTensorLayout.Hwc, 2);
            VisualModelProfile probabilityProfile = Profile(probabilitySchema, TensorElementType.Float32, new TensorShape(1, 4, 1));
            SemanticSegmentationResult probabilities = Decode(probabilityProfile, Input(new VisualSize(4, 1)), new Tensor<float>(new TensorShape(1, 4, 1), new[] { 0.49f, 0.5f, 0.8f, 0f }));
            CollectionAssert.AreEqual(new ushort[] { 0, 1, 1, 0 }, probabilities.Mask.ToArray());
        }

        [TestMethod]
        public void IntegerLabelMapsSupportBackendNeutralIntegralTypes()
        {
            var schema = new SegmentationOutputSchema("labels", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Nhw, 3, ignoreClassIndex: 2);
            VisualModelProfile uint8Profile = Profile(schema, TensorElementType.UInt8, new TensorShape(1, 2, 2));
            SemanticSegmentationResult uint8 = Decode(uint8Profile, Input(new VisualSize(2, 2)), new Tensor<byte>(new TensorShape(1, 2, 2), new byte[] { 0, 1, 2, 1 }));
            CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 1 }, uint8.Mask.ToArray());
            Assert.IsTrue(uint8.Classes[2].IsIgnored);

            VisualModelProfile int64Profile = Profile(schema, TensorElementType.Int64, new TensorShape(1, 2, 2));
            SemanticSegmentationResult int64 = Decode(int64Profile, Input(new VisualSize(2, 2)), new Tensor<long>(new TensorShape(1, 2, 2), new long[] { 2, 1, 0, 1 }));
            CollectionAssert.AreEqual(new ushort[] { 2, 1, 0, 1 }, int64.Mask.ToArray());
        }

        [TestMethod]
        public void NearestRestorationHandlesResizeLetterboxAndCrop()
        {
            var schema = new SegmentationOutputSchema("labels", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Hw, 3);
            var resizeOptions = new SegmentationDecoderOptions(outputSizeMode: SegmentationOutputSizeMode.Source, generateRle: false);
            VisualModelProfile resizeProfile = Profile(schema, TensorElementType.UInt16, new TensorShape(2, 2), resizeOptions, new VisualSize(2, 2));
            var source4 = new VisualSize(4, 4);
            var model2 = new VisualSize(2, 2);
            SemanticSegmentationResult resize = Decode(resizeProfile, Input(source4, model2, ImageTransform.Resize(source4, model2)), new Tensor<ushort>(new TensorShape(2, 2), new ushort[] { 1, 0, 0, 2 }));
            CollectionAssert.AreEqual(new ushort[] { 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 2, 2, 0, 0, 2, 2 }, resize.Mask.ToArray());

            var sourceLetterbox = new VisualSize(4, 2);
            var modelLetterbox = new VisualSize(4, 4);
            VisualModelProfile letterboxProfile = Profile(schema, TensorElementType.UInt16, new TensorShape(4, 4), resizeOptions, modelLetterbox);
            ushort[] letterboxValues = { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 0 };
            SemanticSegmentationResult letterbox = Decode(letterboxProfile, Input(sourceLetterbox, modelLetterbox, ImageTransform.Letterbox(sourceLetterbox, modelLetterbox)), new Tensor<ushort>(new TensorShape(4, 4), letterboxValues));
            CollectionAssert.AreEqual(new ushort[] { 1, 1, 1, 1, 2, 2, 2, 2 }, letterbox.Mask.ToArray());

            VisualModelProfile cropProfile = Profile(schema, TensorElementType.UInt16, new TensorShape(2, 2), resizeOptions, model2);
            ImageTransform cropTransform = ImageTransform.Crop(source4, model2, new RectangleF(1, 1, 2, 2));
            SemanticSegmentationResult crop = Decode(cropProfile, Input(source4, model2, cropTransform), new Tensor<ushort>(new TensorShape(2, 2), new ushort[] { 1, 2, 2, 1 }));
            CollectionAssert.AreEqual(new ushort[] { 0, 0, 0, 0, 0, 1, 2, 0, 0, 2, 1, 0, 0, 0, 0, 0 }, crop.Mask.ToArray());
        }

        [TestMethod]
        public void SmallFourConnectedRegionsAreReplacedWithBackground()
        {
            var schema = new SegmentationOutputSchema("labels", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Hw, 3);
            var options = new SegmentationDecoderOptions(outputSizeMode: SegmentationOutputSizeMode.Tensor, minimumRegionPixels: 2);
            VisualModelProfile profile = Profile(schema, TensorElementType.UInt16, new TensorShape(3, 3), options, new VisualSize(3, 3));
            ushort[] labels = { 1, 0, 2, 1, 0, 0, 0, 0, 2 };
            SemanticSegmentationResult result = Decode(profile, Input(new VisualSize(3, 3)), new Tensor<ushort>(new TensorShape(3, 3), labels));
            CollectionAssert.AreEqual(new ushort[] { 1, 0, 0, 1, 0, 0, 0, 0, 0 }, result.Mask.ToArray());
        }

        [TestMethod]
        public void DynamicOutputPatternFlowsThroughFakeBackendPipeline()
        {
            var schema = new SegmentationOutputSchema("logits", SegmentationOutputKind.Logits, SegmentationTensorLayout.Nchw, 3);
            var modelId = new ModelId("tests/dynamic-semantic-segmentation");
            var profile = new VisualModelProfile(
                "tests/dynamic-semantic-segmentation.v1", modelId, VisualTaskId.SemanticSegmentation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 3), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("logits", TensorElementType.Float32, new TensorShape(-1, 3, -1, -1)) },
                new[] { new VisualLabel(0, "background"), new VisualLabel(1, "road"), new VisualLabel(2, "person") },
                new SemanticSegmentationDecoder(schema));
            var actualShape = new TensorShape(1, 3, 2, 3);
            float[] logits = { 9, 0, 0, 1, 5, 0, 0, 9, 0, 1, 5, 9, 0, 0, 9, 0, 5, 9 };
            using PipelineFixture fixture = VisualTestData.Pipeline(profile, actualShape, _ => InferenceOutputs.Create("logits", new Tensor<float>(actualShape, logits)));
            using PreparedVisualInput input = Input(new VisualSize(3, 2));
            SemanticSegmentationResult result = fixture.Pipeline.Run(input).GetValue<SemanticSegmentationResult>();
            CollectionAssert.AreEqual(new ushort[] { 0, 1, 2, 0, 0, 1 }, result.Mask.ToArray());
        }

        [TestMethod]
        public void InvalidValuesShapesCapabilitiesAndMemoryHaveStableDiagnostics()
        {
            var probabilitySchema = new SegmentationOutputSchema("mask", SegmentationOutputKind.Probabilities, SegmentationTensorLayout.Nchw, 2);
            VisualModelProfile probabilityProfile = Profile(probabilitySchema, TensorElementType.Float32, new TensorShape(1, 1, 1, 2));
            VisualException probability = Assert.ThrowsExactly<VisualException>(() => Decode(probabilityProfile, Input(new VisualSize(2, 1)), new Tensor<float>(new TensorShape(1, 1, 1, 2), new[] { 0f, 1.1f })));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, probability.ErrorCode);

            var labelSchema = new SegmentationOutputSchema("mask", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Hw, 2);
            VisualModelProfile labelProfile = Profile(labelSchema, TensorElementType.Int32, new TensorShape(1, 2));
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => Decode(labelProfile, Input(new VisualSize(2, 1)), new Tensor<int>(new TensorShape(1, 2), new[] { -1, 0 }))).ErrorCode);

            var polygonOptions = new SegmentationDecoderOptions(generatePolygons: true);
            VisualModelProfile polygonProfile = Profile(labelSchema, TensorElementType.UInt8, new TensorShape(1, 2), polygonOptions);
            Assert.AreEqual(VisualErrorCodes.CapabilityUnavailable, Assert.ThrowsExactly<VisualException>(() => Decode(polygonProfile, Input(new VisualSize(2, 1)), new Tensor<byte>(new TensorShape(1, 2), new byte[] { 0, 1 }))).ErrorCode);

            var boundedOptions = new SegmentationDecoderOptions(maximumOutputBytes: 8);
            VisualModelProfile boundedProfile = Profile(labelSchema, TensorElementType.UInt8, new TensorShape(1, 2), boundedOptions);
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => Decode(boundedProfile, Input(new VisualSize(2, 1)), new Tensor<byte>(new TensorShape(1, 2), new byte[] { 0, 1 }))).ErrorCode);
        }

        [TestMethod]
        public void DecoderHonorsCancellationAndResultOwnsDefensiveCopies()
        {
            var schema = new SegmentationOutputSchema("labels", SegmentationOutputKind.LabelMap, SegmentationTensorLayout.Hw, 2);
            VisualModelProfile profile = Profile(schema, TensorElementType.UInt16, new TensorShape(1, 2));
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.ThrowsExactly<OperationCanceledException>(() => Decode(profile, Input(new VisualSize(2, 1)), new Tensor<ushort>(new TensorShape(1, 2), new ushort[] { 0, 1 }), source.Token));

            ushort[] supplied = { 0, 1 };
            var mask = new SemanticSegmentationMask(2, 1, supplied);
            supplied[0] = 1;
            ushort[] returned = mask.ToArray();
            returned[1] = 0;
            CollectionAssert.AreEqual(new ushort[] { 0, 1 }, mask.ToArray());
            Assert.ThrowsExactly<ArgumentException>(() => new SegmentationRle(2, 1, new[] { new SegmentationRleRun(1, 1, 0) }));
        }

        private static SemanticSegmentationResult Decode(VisualModelProfile profile, PreparedVisualInput input, ITensor tensor, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (input)
            {
                InferenceOutputs outputs = InferenceOutputs.Create(profile.Outputs[0].Name, tensor);
                return (SemanticSegmentationResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, outputs, cancellationToken));
            }
        }

        private static VisualModelProfile Profile(SegmentationOutputSchema schema, TensorElementType outputType, TensorShape outputShape, SegmentationDecoderOptions? options = null, VisualSize? modelSize = null)
        {
            VisualSize size = modelSize ?? new VisualSize((int)outputShape[outputShape.Rank - 1], outputShape.Rank > 1 ? (int)outputShape[outputShape.Rank - 2] : 1);
            VisualLabel[] labels = schema.ClassCount == 2
                ? new[] { new VisualLabel(0, "background"), new VisualLabel(1, "road") }
                : new[] { new VisualLabel(0, "background"), new VisualLabel(1, "road"), new VisualLabel(2, "person") };
            return new VisualModelProfile(
                "tests/semantic-segmentation.v1", new ModelId("tests/semantic-segmentation"), VisualTaskId.SemanticSegmentation, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, size.Height, size.Width), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(schema.OutputName, outputType, outputShape) },
                labels,
                new SemanticSegmentationDecoder(schema, options));
        }

        private static PreparedVisualInput Input(VisualSize size) => Input(size, size, ImageTransform.Resize(size, size));

        private static PreparedVisualInput Input(VisualSize source, VisualSize model, ImageTransform transform)
        {
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, model.Height, model.Width), new float[checked(3 * model.Height * model.Width)]), source, model, 1, VisualTensorLayout.Nchw, transform);
        }
    }
}
