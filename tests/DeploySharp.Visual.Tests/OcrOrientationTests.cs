using System;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class OcrOrientationTests
    {
        private static readonly TextOrientation[] Mapping =
        {
            TextOrientation.Degrees0,
            TextOrientation.CounterClockwise90,
            TextOrientation.Clockwise90,
            TextOrientation.Degrees180
        };

        [TestMethod]
        public void SchemaRequiresExplicitFourClassMappingAndShape()
        {
            var schema = new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), TensorElementType.Float32, Mapping);
            Assert.AreEqual(4, schema.ClassToOrientation.Count);
            Assert.AreEqual(TextOrientation.CounterClockwise90, schema.ClassToOrientation[1]);
            Assert.ThrowsExactly<VisualException>(() => new OcrOrientationSchema("scores", new TensorShape(1, 3), TensorElementType.Float32, Mapping));
            Assert.ThrowsExactly<VisualException>(() => new OcrOrientationSchema("scores", new TensorShape(1, 4), TensorElementType.Float32, new[] { TextOrientation.Degrees0, TextOrientation.Degrees0, TextOrientation.Clockwise90, TextOrientation.Degrees180 }));
            Assert.ThrowsExactly<VisualException>(() => new OcrOrientationSchema("scores", new TensorShape(1, 4), TensorElementType.Float32, Mapping, OcrOrientationValueSemantics.Probability, applySoftmax: true));
        }

        [TestMethod]
        public void LogitsDecodeDeterministicallyAndOwnScores()
        {
            var decoder = Decoder(OcrOrientationValueSemantics.Logits, rejectionThreshold: .5f);
            OcrOrientationResult result = Decode(decoder, new Tensor<float>(new TensorShape(1, 4), new[] { 1f, 8f, 2f, 3f }));
            Assert.AreEqual(TextOrientation.CounterClockwise90, result.AcceptedOrientation);
            Assert.AreEqual(1, result.ClassIndex);
            Assert.IsFalse(result.Rejected);
            Assert.IsTrue(result.Confidence > .98f);
            Assert.AreEqual(64, result.CanonicalSha256.Length);
            float[] copy = result.Scores.ToArray();
            copy[1] = 0;
            Assert.IsTrue(result.Scores[1] > .98f);
        }

        [TestMethod]
        public void Float64ProbabilityTieUsesLowestClassIndexAndLowConfidenceRejects()
        {
            var tie = Decoder(OcrOrientationValueSemantics.Probability, rejectionThreshold: .2f);
            OcrOrientationResult tied = Decode(tie, new Tensor<double>(new TensorShape(1, 4), new[] { .4, .4, .1, .1 }));
            Assert.AreEqual(0, tied.ClassIndex);
            Assert.AreEqual(TextOrientation.Degrees0, tied.AcceptedOrientation);

            var reject = Decoder(OcrOrientationValueSemantics.Probability, rejectionThreshold: .5f);
            OcrOrientationResult rejected = Decode(reject, new Tensor<double>(new TensorShape(1, 4), new[] { .3, .3, .2, .2 }));
            Assert.IsTrue(rejected.Rejected);
            Assert.IsNull(rejected.AcceptedOrientation);
            CollectionAssert.Contains(rejected.Warnings.ToArray(), "ocr.orientation.rejected");
        }

        [TestMethod]
        public void DynamicBatchDecodesEveryRowInInputOrder()
        {
            var decoder = new OcrOrientationDecoder(
                new OcrOrientationSchema("orientation_scores", new TensorShape(-1, 4), TensorElementType.Float32, Mapping, OcrOrientationValueSemantics.Probability, applySoftmax: false, allowDynamicBatch: true),
                new OcrOrientationDecoderOptions(.5f));
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 1, 2, 2), new float[8]), size, size, 2, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            var profile = new VisualModelProfile(
                "tests/text-orientation.dynamic.v1", new ModelId("tests/text-orientation-dynamic"), VisualTaskId.TextOrientationClassification, "1", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 1, 2, 2), VisualTensorLayout.Nchw, 1, 2),
                new[] { new VisualOutputBinding("orientation_scores", TensorElementType.Float32, new TensorShape(-1, 4)) },
                Array.Empty<VisualLabel>(), decoder);
            object decoded = decoder.Decode(new VisualDecodeContext(input, profile,
                InferenceOutputs.Create("orientation_scores", new Tensor<float>(new TensorShape(2, 4), new[] { .8f, .1f, .05f, .05f, .05f, .05f, .1f, .8f })), CancellationToken.None));

            var batch = (OcrOrientationBatchResult)decoded;
            Assert.AreEqual(2, batch.Items.Count);
            Assert.AreEqual(TextOrientation.Degrees0, batch.Items[0].AcceptedOrientation);
            Assert.AreEqual(TextOrientation.Degrees180, batch.Items[1].AcceptedOrientation);

            var bounded = new OcrOrientationDecoder(
                new OcrOrientationSchema("orientation_scores", new TensorShape(-1, 4), TensorElementType.Float32, Mapping, OcrOrientationValueSemantics.Probability, applySoftmax: false, allowDynamicBatch: true),
                new OcrOrientationDecoderOptions(.5f, maximumResultBytes: 31));
            Assert.AreEqual(VisualErrorCodes.OcrOrientationLimitExceeded,
                Assert.ThrowsExactly<VisualException>(() => bounded.Decode(new VisualDecodeContext(input, profile,
                    InferenceOutputs.Create("orientation_scores", new Tensor<float>(new TensorShape(2, 4), new[] { .8f, .1f, .05f, .05f, .05f, .05f, .1f, .8f })), CancellationToken.None))).ErrorCode);
        }

        [TestMethod]
        public void DecoderRejectsNonFiniteInvalidProbabilityAndCancellation()
        {
            var decoder = Decoder(OcrOrientationValueSemantics.Probability);
            Assert.AreEqual(VisualErrorCodes.OcrOrientationContractInvalid, Assert.ThrowsExactly<VisualException>(() => Decode(decoder, new Tensor<float>(new TensorShape(1, 4), new[] { .5f, .5f, .5f, -.5f }))).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.DecodeFailed, Assert.ThrowsExactly<VisualException>(() => Decode(decoder, new Tensor<double>(new TensorShape(1, 4), new[] { double.NaN, 0.0, 0.0, 1.0 }))).ErrorCode);
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.ThrowsExactly<OperationCanceledException>(() => Decode(decoder, new Tensor<double>(new TensorShape(1, 4), new[] { .1, .2, .3, .4 }), source.Token));
            var bounded = new OcrOrientationDecoder(new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), TensorElementType.Float32, Mapping), new OcrOrientationDecoderOptions(maximumResultBytes: 15));
            Assert.AreEqual(VisualErrorCodes.OcrOrientationLimitExceeded, Assert.ThrowsExactly<VisualException>(() => Decode(bounded, new Tensor<float>(new TensorShape(1, 4), new[] { 1f, 2f, 3f, 4f }))).ErrorCode);
        }

        [TestMethod]
        public void OcrResultBindsOrientationProvenanceAndChangesCanonicalDigest()
        {
            OcrOrientationResult orientation = Decode(Decoder(OcrOrientationValueSemantics.Logits), new Tensor<float>(new TensorShape(1, 4), new[] { 9f, 1f, 2f, 3f }));
            var timing = new OcrStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            var plain = new OcrResult(Array.Empty<OcrRegionResult>(), new VisualSize(2, 2), "tests/detector", new ModelId("tests/detector"), "tests/recognizer", new ModelId("tests/recognizer"), timing);
            var oriented = new OcrResult(Array.Empty<OcrRegionResult>(), new VisualSize(2, 2), "tests/detector", new ModelId("tests/detector"), "tests/recognizer", new ModelId("tests/recognizer"), timing, orientation);
            Assert.AreEqual(orientation, oriented.Orientation);
            Assert.AreNotEqual(plain.ComputeSha256(), oriented.ComputeSha256());
            Assert.AreEqual(oriented.SourceSize, oriented.OriginalSourceSize);
        }

        [TestMethod]
        public void AcceptedOrientationMapsCorrectedCoordinatesBackToOriginalImage()
        {
            var clockwise = new OcrOrientationResult(TextOrientation.Clockwise90, 2, .9f, new[] { .01f, .04f, .9f, .05f }, false, "tests/orientation", new ModelId("tests/orientation"), new BackendId("fake"), new VisualSize(6, 4), new VisualSize(2, 2), TimeSpan.Zero);
            Assert.AreEqual(new VisualSize(4, 6), clockwise.CorrectedImageSize);
            Assert.AreEqual(new PointF(2, 3), clockwise.ToOriginalPoint(new PointF(1, 2)));

            var counterClockwise = new OcrOrientationResult(TextOrientation.CounterClockwise90, 1, .9f, new[] { .01f, .9f, .04f, .05f }, false, "tests/orientation", new ModelId("tests/orientation"), new BackendId("fake"), new VisualSize(6, 4), new VisualSize(2, 2), TimeSpan.Zero);
            Assert.AreEqual(new PointF(4, 1), counterClockwise.ToOriginalPoint(new PointF(1, 2)));

            var degrees180 = new OcrOrientationResult(TextOrientation.Degrees180, 3, .9f, new[] { .01f, .04f, .05f, .9f }, false, "tests/orientation", new ModelId("tests/orientation"), new BackendId("fake"), new VisualSize(6, 4), new VisualSize(2, 2), TimeSpan.Zero);
            Assert.AreEqual(new PointF(5, 2), degrees180.ToOriginalPoint(new PointF(1, 2)));
        }

        private static OcrOrientationDecoder Decoder(OcrOrientationValueSemantics semantics, float rejectionThreshold = 0)
        {
            return new OcrOrientationDecoder(
                new OcrOrientationSchema("orientation_scores", new TensorShape(1, 4), semantics == OcrOrientationValueSemantics.Probability ? TensorElementType.Float64 : TensorElementType.Float32, Mapping, semantics, semantics == OcrOrientationValueSemantics.Logits),
                new OcrOrientationDecoderOptions(rejectionThreshold));
        }

        private static OcrOrientationResult Decode(OcrOrientationDecoder decoder, ITensor tensor, CancellationToken cancellationToken = default(CancellationToken))
        {
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 1, 2, 2), new float[4]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            var profile = new VisualModelProfile(
                "tests/text-orientation.v1", new ModelId("tests/text-orientation"), VisualTaskId.TextOrientationClassification, "1", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 1, 2, 2), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("orientation_scores", tensor.ElementType, new TensorShape(1, 4)) },
                Array.Empty<VisualLabel>(), decoder);
            var outputs = InferenceOutputs.Create("orientation_scores", tensor);
            return (OcrOrientationResult)decoder.Decode(new VisualDecodeContext(input, profile, outputs, cancellationToken));
        }
    }
}
