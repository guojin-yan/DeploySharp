using System;
using System.Threading;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class ClassificationDecoderTests
    {
        [TestMethod]
        public void StableSoftmaxHandlesExtremeLogitsAndTopK()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile(ClassificationScoreMode.Logits, 2);
            ClassificationResult result = Decode(profile, new[] { 10000f, 9999f, -10000f });
            Assert.AreEqual(2, result.Predictions.Count);
            Assert.AreEqual(0, result.Predictions[0].Index);
            Assert.AreEqual(1, result.Predictions[1].Index);
            Assert.IsTrue(result.Predictions[0].Score > result.Predictions[1].Score);
            Assert.IsTrue(result.Predictions[0].Score <= 1);
        }

        [TestMethod]
        public void EqualScoresUseClassIndexTieBreakAndThreshold()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile(ClassificationScoreMode.Probabilities, 3, 0.25f);
            ClassificationResult result = Decode(profile, new[] { 0.4f, 0.4f, 0.2f });
            Assert.AreEqual(2, result.Predictions.Count);
            Assert.AreEqual(0, result.Predictions[0].Index);
            Assert.AreEqual(1, result.Predictions[1].Index);
        }

        [TestMethod]
        public void InvalidProbabilityShapeAndNonFiniteValuesAreDiagnosed()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile(ClassificationScoreMode.Probabilities);
            Assert.ThrowsExactly<VisualException>(() => Decode(profile, new[] { 1.2f, 0f, 0f }));
            Assert.ThrowsExactly<VisualException>(() => Decode(profile, new[] { float.NaN, 0f, 1f }));
            var output = InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(3, 1), new[] { 0f, 0f, 1f }));
            Assert.ThrowsExactly<VisualException>(() => profile.Decoder.Decode(new VisualDecodeContext(VisualTestData.ClassificationInput(), profile, output, CancellationToken.None)));
        }

        [TestMethod]
        public void MissingLabelFallsBackToInvariantClassIndex()
        {
            var decoder = new ClassificationDecoder("scores", ClassificationScoreMode.Probabilities, 1);
            var profile = new VisualModelProfile("tests/sparse-label", VisualTestData.ClassificationModelId, VisualTaskId.ImageClassification, "1", "fake", new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw), new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) }, new[] { new VisualLabel(0, "zero") }, decoder);
            ClassificationResult result = Decode(profile, new[] { 0f, 0.9f, 0.1f });
            Assert.AreEqual("1", result.TopPrediction!.Label);
        }

        [TestMethod]
        public void DecoderHonorsCancellationBeforeReadingOutput()
        {
            VisualModelProfile profile = VisualTestData.ClassificationProfile();
            using var source = new CancellationTokenSource();
            source.Cancel();
            var output = InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 0f, 0f, 1f }));
            Assert.ThrowsExactly<OperationCanceledException>(() => profile.Decoder.Decode(new VisualDecodeContext(VisualTestData.ClassificationInput(), profile, output, source.Token)));
        }

        [TestMethod]
        public void DynamicBatchDecodesEveryClassificationRowInInputOrder()
        {
            var decoder = new ClassificationDecoder("scores", ClassificationScoreMode.Probabilities, topK: 2);
            var profile = new VisualModelProfile(
                "tests/classification.dynamic.v1", VisualTestData.ClassificationModelId, VisualTaskId.ImageClassification, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(-1, 3, 2, 2), VisualTensorLayout.Nchw, 1, 4),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(-1, 3)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") }, decoder);
            using var input = new PreparedVisualInput("images", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), new VisualSize(2, 2), new VisualSize(2, 2), 2, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(2, 2), new VisualSize(2, 2)), inputId: "classification-batch");
            var output = InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(2, 3), new[] { .1f, .8f, .1f, .7f, .2f, .1f }));
            var result = (ClassificationBatchResult)decoder.Decode(new VisualDecodeContext(input, profile, output, CancellationToken.None));
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("one", result[0].TopPrediction!.Label);
            Assert.AreEqual("zero", result[1].TopPrediction!.Label);
            Assert.AreEqual("zero", result[0].Predictions[1].Label);
        }

        private static ClassificationResult Decode(VisualModelProfile profile, float[] scores)
        {
            var output = InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, scores.Length), scores));
            return (ClassificationResult)profile.Decoder.Decode(new VisualDecodeContext(VisualTestData.ClassificationInput(), profile, output, CancellationToken.None));
        }
    }
}
