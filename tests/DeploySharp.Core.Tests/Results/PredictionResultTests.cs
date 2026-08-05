using System;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.DeploySharp.Core.Tests.Results
{
    [TestClass]
    public sealed class PredictionResultTests
    {
        [TestMethod]
        public void ResultCopiesWarningsAndPreservesMetadata()
        {
            var warnings = new[] { new PredictionWarning("fallback.cpu", "Used CPU fallback.") };
            var generation = new GenerationResult(
                "result",
                GenerationFinishReason.EndOfSequence,
                new TokenUsage(2, 1));
            var result = new PredictionResult<GenerationResult>(
                generation,
                new ModelId("language/test"),
                new BackendId("fake"),
                new InferenceTiming(
                    TimeSpan.FromMilliseconds(1),
                    TimeSpan.FromMilliseconds(2),
                    TimeSpan.FromMilliseconds(3)),
                warnings,
                "operation-1");

            warnings[0] = new PredictionWarning("changed", "Changed after construction.");

            Assert.AreEqual("fallback.cpu", result.Warnings[0].Code);
            Assert.AreEqual(TimeSpan.FromMilliseconds(6), result.Timing.Total);
            Assert.AreEqual("operation-1", result.CorrelationId);
        }

        [TestMethod]
        public void EmbeddingCopiesCallerValues()
        {
            float[] values = { 1.0f, 2.0f };
            var embedding = new EmbeddingResult(values, false);

            values[0] = 99.0f;

            CollectionAssert.AreEqual(new[] { 1.0f, 2.0f }, embedding.ToArray());
        }
    }
}
