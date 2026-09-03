using System;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class DetectionDecoderTests
    {
        [TestMethod]
        public void DecodesAllBoxFormatsAndNormalizedLetterboxCoordinates()
        {
            DetectionResult xyxy = Decode(new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4), new[] { 10f, 20f, 50f, 60f, 0.8f, 0.2f }, new VisualSize(100, 100), null);
            AssertBox(xyxy.Detections[0].Box, 10, 20, 40, 40);

            DetectionResult xywh = Decode(new DetectionOutputSchema("boxes", DetectionBoxFormat.Xywh, false, DetectionScoreMode.ClassScore, 2, 4), new[] { 10f, 20f, 40f, 40f, 0.8f, 0.2f }, new VisualSize(100, 100), null);
            AssertBox(xywh.Detections[0].Box, 10, 20, 40, 40);

            VisualSize source = new VisualSize(200, 100);
            ImageTransform letterbox = ImageTransform.Letterbox(source, new VisualSize(100, 100));
            DetectionResult center = Decode(new DetectionOutputSchema("boxes", DetectionBoxFormat.Cxcywh, true, DetectionScoreMode.ClassScore, 2, 4), new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.8f, 0.2f }, source, letterbox);
            AssertBox(center.Detections[0].Box, 50, 0, 100, 100);
        }

        [TestMethod]
        public void ObjectnessIsMultipliedAndScoreBoundsAreValidated()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xywh, false, DetectionScoreMode.ObjectnessTimesClassScore, 2, 5, 4);
            DetectionResult result = Decode(schema, new[] { 0f, 0f, 10f, 10f, 0.5f, 0.2f, 0.8f }, new VisualSize(100, 100), null, new DetectionDecoderOptions(0.3f));
            Assert.AreEqual(1, result.Detections.Count);
            Assert.AreEqual(1, result.Detections[0].Label.Index);
            Assert.AreEqual(0.4f, result.Detections[0].Label.Score, 0.0001f);
            Assert.ThrowsExactly<VisualException>(() => Decode(schema, new[] { 0f, 0f, 10f, 10f, 1.2f, 0.2f, 0.8f }, new VisualSize(100, 100), null));
        }

        [TestMethod]
        public void ClassAwareAndAgnosticNmsHaveDeterministicBehavior()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            float[] values =
            {
                10, 10, 50, 50, 0.9f, 0.1f,
                12, 12, 48, 48, 0.8f, 0.2f,
                10, 10, 50, 50, 0.1f, 0.9f
            };
            DetectionResult aware = Decode(schema, values, new VisualSize(100, 100), null, new DetectionDecoderOptions(nmsMode: DetectionNmsMode.ClassAware));
            Assert.AreEqual(2, aware.Detections.Count);
            Assert.AreEqual(0, aware.Detections[0].Label.Index);
            Assert.AreEqual(1, aware.Detections[1].Label.Index);
            DetectionResult agnostic = Decode(schema, values, new VisualSize(100, 100), null, new DetectionDecoderOptions(nmsMode: DetectionNmsMode.ClassAgnostic));
            Assert.AreEqual(1, agnostic.Detections.Count);
        }

        [TestMethod]
        public void ZeroAreaBoxesAreSkippedAndNegativeBoxesFail()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xywh, false, DetectionScoreMode.ClassScore, 2, 4);
            DetectionResult result = Decode(schema, new[] { 10f, 10f, 0f, 10f, 0.9f, 0.1f }, new VisualSize(100, 100), null);
            Assert.AreEqual(0, result.Detections.Count);
            Assert.ThrowsExactly<VisualException>(() => Decode(schema, new[] { 10f, 10f, -1f, 10f, 0.9f, 0.1f }, new VisualSize(100, 100), null));
        }

        [TestMethod]
        public void IoUAndMalformedTensorContractsAreStable()
        {
            Assert.AreEqual(1f, DetectionDecoder.IntersectionOverUnion(new RectangleF(0, 0, 10, 10), new RectangleF(0, 0, 10, 10)), 0.0001f);
            Assert.AreEqual(0f, DetectionDecoder.IntersectionOverUnion(new RectangleF(0, 0, 10, 10), new RectangleF(20, 20, 5, 5)), 0.0001f);
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, outputShape: new TensorShape(-1, 6));
            var output = InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 1, 6), new[] { 0f, 0f, 1f, 1f, 0.9f, 0.1f }));
            Assert.IsInstanceOfType(profile.Decoder.Decode(new VisualDecodeContext(VisualTestData.DetectionInput(), profile, output, CancellationToken.None)), typeof(DetectionResult));
            var bad = InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(1, 5), new float[5]));
            Assert.ThrowsExactly<VisualException>(() => profile.Decoder.Decode(new VisualDecodeContext(VisualTestData.DetectionInput(), profile, bad, CancellationToken.None)));
        }

        [TestMethod]
        public void DynamicBatchRestoresEachFrameGeometryAndSuppressesPerFrame()
        {
            var schema = new DetectionOutputSchema("boxes", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
            var profile = new VisualModelProfile(
                "tests/detection.dynamic.v1", VisualTestData.DetectionModelId, VisualTaskId.ObjectDetection, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 100, 100), VisualTensorLayout.Nchw, 1, 4),
                new[] { new VisualOutputBinding("boxes", TensorElementType.Float32, new TensorShape(-1, -1, 6)) },
                new[] { new VisualLabel(0, "cat"), new VisualLabel(1, "dog") }, new DetectionDecoder(schema));
            var firstSource = new VisualSize(100, 100);
            var secondSource = new VisualSize(200, 100);
            var modelSize = new VisualSize(100, 100);
            var frames = new[]
            {
                new VisualInputFrame(firstSource, modelSize, ImageTransform.Resize(firstSource, modelSize), "first"),
                new VisualInputFrame(secondSource, modelSize, ImageTransform.Letterbox(secondSource, modelSize), "second")
            };
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 100, 100), new float[60000]), firstSource, modelSize, 2, VisualTensorLayout.Nchw, ImageTransform.Resize(firstSource, modelSize), batchFrames: frames);
            var values = new[]
            {
                10f, 20f, 50f, 60f, .8f, .2f,
                0f, 0f, 100f, 100f, .9f, .1f
            };
            var output = InferenceOutputs.Create("boxes", new Tensor<float>(new TensorShape(2, 1, 6), values));

            var result = (DetectionBatchResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, output, CancellationToken.None));

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].Detections.Count);
            Assert.AreEqual(40f, result[0].Detections[0].Box.Width, .001f);
            Assert.AreEqual(1, result[1].Detections.Count);
            Assert.AreEqual(200f, result[1].Detections[0].Box.Width, .001f);
        }

        private static DetectionResult Decode(DetectionOutputSchema schema, float[] values, VisualSize sourceSize, ImageTransform? transform, DetectionDecoderOptions? options = null)
        {
            int fields = schema.ClassScoreOffset + schema.ClassCount;
            int candidates = values.Length / fields;
            var shape = new TensorShape(candidates, fields);
            VisualModelProfile profile = VisualTestData.DetectionProfile(schema, options, new TensorShape(-1, fields));
            var output = InferenceOutputs.Create(schema.OutputName, new Tensor<float>(shape, values));
            PreparedVisualInput input = VisualTestData.DetectionInput(sourceSize, transform ?? ImageTransform.Resize(sourceSize, new VisualSize(100, 100)));
            return (DetectionResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, output, CancellationToken.None));
        }

        private static void AssertBox(RectangleF box, float x, float y, float width, float height)
        {
            Assert.AreEqual(x, box.X, 0.001f);
            Assert.AreEqual(y, box.Y, 0.001f);
            Assert.AreEqual(width, box.Width, 0.001f);
            Assert.AreEqual(height, box.Height, 0.001f);
        }
    }
}
