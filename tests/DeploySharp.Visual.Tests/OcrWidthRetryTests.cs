using System;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    public sealed partial class OcrTests
    {
        [TestMethod]
        public async Task WiderDynamicWidthRetrySelectsCandidateAndKeepsAdmissionEvidence()
        {
            var retry = new OcrWidthRetryOptions(32, confidenceThreshold: .95f,
                selectionPolicy: OcrWidthRetrySelectionPolicy.ConfidenceGain, minimumConfidenceGain: .01f,
                maximumRegionsPerImage: 1, maximumCropsPerImage: 8);
            using OcrFixture fixture = CreateOcrFixture(recognitionWidth: 8, dynamicWidth: true, widthRetry: retry,
                recognitionFactory: inputs => WidthSensitiveRecognitionOutputs(inputs.GetRequired("crops").Shape[3] > 8 ? .99f : .9f));
            using var input = new FakeOcrImageInput();

            OcrResult result = await fixture.Pipeline.RunAsync(input);

            Assert.AreEqual(2, result.Regions.Count);
            OcrWidthRetryResult firstRetry = result.Regions[0].WidthRetry ?? throw new AssertFailedException("The first region did not retain width retry evidence.");
            Assert.AreEqual(OcrWidthRetryDecision.CandidateSelected, firstRetry.Decision);
            OcrWidthRetryAttempt candidate = firstRetry.Candidate ?? throw new AssertFailedException("The selected width candidate is missing.");
            Assert.IsTrue(candidate.RecognitionWidth.HasValue && candidate.RecognitionWidth.Value.TargetWidth > 8);
            Assert.AreEqual(candidate.Recognition.Text, result.Regions[0].Recognition.Text);
            OcrWidthRetryResult secondRetry = result.Regions[1].WidthRetry ?? throw new AssertFailedException("The second region did not retain admission evidence.");
            Assert.AreEqual(OcrWidthRetryDecision.RegionLimit, secondRetry.Decision);
            Assert.IsNull(secondRetry.Candidate);
        }

        [TestMethod]
        public void WiderRetryRequiresDynamicProfileAndLargerCandidate()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new TextCropProfile("tests.fixed", 8, OcrRecognitionWidthMode.Fixed, 8, 8)
                .WithWidthRetry(new OcrWidthRetryOptions(16)));
            Assert.ThrowsExactly<ArgumentException>(() => new TextCropProfile("tests.dynamic", 8, OcrRecognitionWidthMode.Dynamic, 8, 16)
                .WithWidthRetry(new OcrWidthRetryOptions(16)));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OcrWidthRetryOptions(0));
        }

        private static InferenceOutputs WidthSensitiveRecognitionOutputs(float confidence)
        {
            float[] values = Probabilities(2, 6, 4, 0, 1, 1, 0, 2, 2, 3, 3, 0, 1, 1, 0);
            for (int index = 0; index < values.Length; index++) if (values[index] != 0) values[index] = confidence;
            return InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(2, 6, 4), values));
        }
    }
}
