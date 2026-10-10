using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PaddleDocumentPipelineTests
    {
        [TestMethod]
        public async Task VisualPipelineStageBridgesPreparedInputAndCanonicalDocumentResult()
        {
            using PipelineFixture fixture = VisualTestData.Pipeline(
                VisualTestData.ClassificationProfile(),
                new TensorShape(1, 3),
                inputs => InferenceOutputs.Create(
                    "scores",
                    new Tensor<float>(
                        new TensorShape(1, 3),
                        new[] { 0.05f, 0.9f, 0.05f })));

            PaddleDocumentPage page = new PaddleDocumentPage(new object(), new VisualSize(320, 240), 4);
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Official.First(model => model.Module == PaddleDocumentModule.DocumentOrientation);
            var stage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
                PaddleDocumentModule.DocumentOrientation,
                fixture.Pipeline,
                (context, token) => VisualTestData.ClassificationInput(),
                (context, inference) =>
                {
                    ClassificationResult classification = inference.GetValue<ClassificationResult>();
                    LabelScore top = classification.TopPrediction!;
                    return new PaddleDocumentOrientationResult(
                        new PaddleDocumentResultMetadata(descriptor, inference.BackendId.Value, inference.Timing.Total, new string('b', 64), context.Page.PageIndex),
                        top.Label,
                        top.Index * 90);
                });

            PaddleDocumentPipelineResult result = await new PaddleDocumentPipeline(new[] { stage }).RunAsync(page);

            PaddleDocumentOrientationResult orientation = result.GetRequired<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation);
            Assert.AreEqual(90, orientation.RotationDegrees);
            Assert.AreEqual("one", orientation.Label);
            Assert.AreEqual(4, orientation.Metadata.PageIndex);
        }

        [TestMethod]
        public async Task VisualPipelineStageReleasesOwnedPreparedInput()
        {
            using PipelineFixture fixture = VisualTestData.Pipeline(
                VisualTestData.ClassificationProfile(),
                new TensorShape(1, 3),
                inputs => InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 1f, 0f, 0f })));
            var owned = new TrackingDisposable();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Official.First(model => model.Module == PaddleDocumentModule.DocumentOrientation);
            var stage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
                PaddleDocumentModule.DocumentOrientation,
                fixture.Pipeline,
                (context, token) => VisualTestData.ClassificationInput(PreparedInputOwnership.Owned, owned),
                (context, inference) => new PaddleDocumentOrientationResult(
                    new PaddleDocumentResultMetadata(descriptor, inference.BackendId.Value, inference.Timing.Total, new string('c', 64), context.Page.PageIndex),
                    "0_degree",
                    0));

            await new PaddleDocumentPipeline(new[] { stage }).RunAsync(new PaddleDocumentPage(new object(), new VisualSize(32, 32)));

            Assert.AreEqual(1, owned.DisposeCount);
        }

        [TestMethod]
        public async Task StagesAreOrderedAndCanReadPriorResults()
        {
            var order = new List<PaddleDocumentModule>();
            PaddleDocumentPage page = new PaddleDocumentPage(new object(), new VisualSize(640, 480), 2);
            var formula = new PaddleDocumentPipelineStage(PaddleDocumentModule.FormulaRecognition, (context, token) =>
            {
                order.Add(PaddleDocumentModule.FormulaRecognition);
                Assert.IsNotNull(context.TryGet<PaddleDocumentOrientationResult>(PaddleDocumentModule.DocumentOrientation));
                return Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentFormulaResult(Metadata(PaddleDocumentModule.FormulaRecognition, page.PageIndex), "x+y"));
            });
            var orientation = new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, (context, token) =>
            {
                order.Add(PaddleDocumentModule.DocumentOrientation);
                return Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentOrientationResult(Metadata(PaddleDocumentModule.DocumentOrientation, page.PageIndex), "0_degree", 0));
            });

            PaddleDocumentPipelineResult result = await new PaddleDocumentPipeline(new[] { formula, orientation }).RunAsync(page);

            CollectionAssert.AreEqual(new[] { PaddleDocumentModule.DocumentOrientation, PaddleDocumentModule.FormulaRecognition }, order);
            Assert.AreEqual(2, result.Results.Count);
            Assert.AreEqual("x+y", result.GetRequired<PaddleDocumentFormulaResult>(PaddleDocumentModule.FormulaRecognition).Latex);
            Assert.AreEqual(2, result.Timings.Count);
            Assert.IsTrue(result.Elapsed >= TimeSpan.Zero);
        }

        [TestMethod]
        public void DuplicateModulesAndAggregateModuleAreRejected()
        {
            Func<PaddleDocumentPipelineContext, CancellationToken, Task<PaddleDocumentModuleResult>> handler = (context, token) => Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentFormulaResult(Metadata(PaddleDocumentModule.FormulaRecognition, 0), "x"));
            var first = new PaddleDocumentPipelineStage(PaddleDocumentModule.FormulaRecognition, handler);
            var second = new PaddleDocumentPipelineStage(PaddleDocumentModule.FormulaRecognition, handler);
            Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentPipeline(new[] { first, second }));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PaddleDocumentPipelineStage(PaddleDocumentModule.StructurePipeline, handler));
        }

        [TestMethod]
        public async Task MismatchedPageProvenanceFailsBeforeAggregateResultIsReturned()
        {
            var stage = new PaddleDocumentPipelineStage(PaddleDocumentModule.FormulaRecognition, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentFormulaResult(Metadata(PaddleDocumentModule.FormulaRecognition, context.Page.PageIndex + 1), "x")));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PaddleDocumentPipeline(new[] { stage }).RunAsync(new PaddleDocumentPage(new object(), new VisualSize(1, 1))));
        }

        [TestMethod]
        public async Task CancellationIsObservedBetweenStages()
        {
            using var cancellation = new CancellationTokenSource();
            var stage = new PaddleDocumentPipelineStage(PaddleDocumentModule.FormulaRecognition, (context, token) =>
            {
                cancellation.Cancel();
                return Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentFormulaResult(Metadata(PaddleDocumentModule.FormulaRecognition, context.Page.PageIndex), "x"));
            });
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new PaddleDocumentPipeline(new[] { stage, new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, (context, token) => Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentOrientationResult(Metadata(PaddleDocumentModule.DocumentOrientation, context.Page.PageIndex), "0_degree", 0))) }).RunAsync(new PaddleDocumentPage(new object(), new VisualSize(1, 1)), cancellation.Token));
        }

        [TestMethod]
        public async Task OrderedMultiPagePipelineExportsEachPageWithModuleProvenance()
        {
            var orientation = new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, (context, token) =>
                Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentOrientationResult(
                    Metadata(PaddleDocumentModule.DocumentOrientation, context.Page.PageIndex), "0_degree", 0)));
            var formula = new PaddleDocumentDependentStage(PaddleDocumentModule.FormulaRecognition,
                new[] { PaddleDocumentModule.DocumentOrientation },
                (context, token) => Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentFormulaResult(
                    Metadata(PaddleDocumentModule.FormulaRecognition, context.Page.PageIndex), "x+y", tokenIds: new[] { 1, 2 })));
            var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { formula, orientation });
            IReadOnlyList<PaddleDocumentPipelineResult> pages = await pipeline.RunManyAsync(new[]
            {
                new PaddleDocumentPage("page-0", new VisualSize(320, 240), 0),
                new PaddleDocumentPage("page-1", new VisualSize(640, 480), 1)
            });

            Assert.AreEqual(2, pages.Count);
            Assert.AreEqual(0, pages[0].Page.PageIndex);
            Assert.AreEqual(1, pages[1].Page.PageIndex);
            Assert.AreEqual("x+y", pages[1].GetRequired<PaddleDocumentFormulaResult>(PaddleDocumentModule.FormulaRecognition).Latex);
            string json = PaddleDocumentPipelineExport.ToJson(pages);
            string markdown = PaddleDocumentPipelineExport.ToMarkdown(pages);
            StringAssert.Contains(json, "pageIndex");
            StringAssert.Contains(markdown, "PP-Structure page 1");
        }

        [TestMethod]
        public async Task ConcurrentMultiPagePipelinePreservesOrderAndBoundsStageConcurrency()
        {
            int active = 0;
            int maximumActive = 0;
            var stage = new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, async (context, token) =>
            {
                int current = Interlocked.Increment(ref active);
                int observed;
                do
                {
                    observed = maximumActive;
                    if (current <= observed) break;
                }
                while (Interlocked.CompareExchange(ref maximumActive, current, observed) != observed);

                try
                {
                    await Task.Delay(30, token).ConfigureAwait(false);
                    return new PaddleDocumentOrientationResult(Metadata(PaddleDocumentModule.DocumentOrientation, context.Page.PageIndex), "0_degree", 0);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            });
            var pipeline = new PaddleDocumentPipeline(new[] { stage });
            PaddleDocumentPage[] input = Enumerable.Range(0, 6)
                .Select(index => new PaddleDocumentPage("page-" + index, new VisualSize(320, 240), index))
                .ToArray();

            IReadOnlyList<PaddleDocumentPipelineResult> results = await pipeline.RunManyConcurrentAsync(input, maxDegreeOfParallelism: 2);

            Assert.AreEqual(input.Length, results.Count);
            CollectionAssert.AreEqual(input.Select(page => page.PageIndex).ToArray(), results.Select(result => result.Page.PageIndex).ToArray());
            Assert.IsTrue(maximumActive >= 2, "The bounded API should overlap independent pages.");
            Assert.IsTrue(maximumActive <= 2, "The bounded API exceeded the requested concurrency.");
        }

        [TestMethod]
        public async Task ConcurrentMultiPagePipelineObservesCancellationAndRejectsInvalidConcurrency()
        {
            var stage = new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, async (context, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                return new PaddleDocumentOrientationResult(Metadata(PaddleDocumentModule.DocumentOrientation, context.Page.PageIndex), "0_degree", 0);
            });
            var pipeline = new PaddleDocumentPipeline(new[] { stage });
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => pipeline.RunManyConcurrentAsync(Array.Empty<PaddleDocumentPage>(), 0));

            using var cancellation = new CancellationTokenSource();
            Task<IReadOnlyList<PaddleDocumentPipelineResult>> run = pipeline.RunManyConcurrentAsync(
                new[]
                {
                    new PaddleDocumentPage("page-0", new VisualSize(320, 240), 0),
                    new PaddleDocumentPage("page-1", new VisualSize(320, 240), 1)
                },
                maxDegreeOfParallelism: 1,
                cancellation.Token);
            await Task.Delay(30).ConfigureAwait(false);
            cancellation.Cancel();
            try
            {
                await run.ConfigureAwait(false);
                Assert.Fail("The cancelled multi-page run unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                // Native/asynchronous stage cancellation is surfaced as either
                // OperationCanceledException or its TaskCanceledException subtype.
            }
        }

        [TestMethod]
        public async Task ConcurrentMultiPagePipelineStopsAssigningPagesAfterFirstStageFailure()
        {
            int startedAfterFailingPage = 0;
            var failure = new InvalidOperationException("synthetic page failure");
            var stage = new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, async (context, token) =>
            {
                if (context.Page.PageIndex == 0)
                {
                    await Task.Delay(30, token).ConfigureAwait(false);
                    throw failure;
                }

                Interlocked.Increment(ref startedAfterFailingPage);
                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                return new PaddleDocumentOrientationResult(Metadata(PaddleDocumentModule.DocumentOrientation, context.Page.PageIndex), "0_degree", 0);
            });
            var pipeline = new PaddleDocumentPipeline(new[] { stage });
            PaddleDocumentPage[] input = Enumerable.Range(0, 100)
                .Select(index => new PaddleDocumentPage("page-" + index, new VisualSize(320, 240), index))
                .ToArray();

            InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => pipeline.RunManyConcurrentAsync(input, maxDegreeOfParallelism: 2));

            Assert.AreSame(failure, thrown);
            Assert.IsTrue(Volatile.Read(ref startedAfterFailingPage) <= 1,
                "A failed worker must cancel queued pages instead of releasing a gate that admits the entire backlog.");
        }

        [TestMethod]
        public async Task ConcurrentMultiPagePipelineCanBeReusedAfterAWorkerFailure()
        {
            int failuresRemaining = 1;
            var stage = new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, async (context, token) =>
            {
                await Task.Yield();
                if (context.Page.PageIndex == 0 && Interlocked.Exchange(ref failuresRemaining, 0) == 1)
                    throw new InvalidOperationException("one-shot failure");
                return new PaddleDocumentOrientationResult(Metadata(PaddleDocumentModule.DocumentOrientation, context.Page.PageIndex), "0_degree", 0);
            });
            var pipeline = new PaddleDocumentPipeline(new[] { stage });
            PaddleDocumentPage[] input = Enumerable.Range(0, 4)
                .Select(index => new PaddleDocumentPage("page-" + index, new VisualSize(320, 240), index))
                .ToArray();

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => pipeline.RunManyConcurrentAsync(input, maxDegreeOfParallelism: 2));
            IReadOnlyList<PaddleDocumentPipelineResult> results = await pipeline.RunManyConcurrentAsync(input, maxDegreeOfParallelism: 2);

            Assert.AreEqual(input.Length, results.Count);
            CollectionAssert.AreEqual(input.Select(page => page.PageIndex).ToArray(), results.Select(result => result.Page.PageIndex).ToArray());
        }

        [TestMethod]
        public async Task ConcurrentMultiPagePipelineValidatesAllPagesBeforeStartingStages()
        {
            int stageCalls = 0;
            var stage = new PaddleDocumentPipelineStage(PaddleDocumentModule.DocumentOrientation, (context, token) =>
            {
                Interlocked.Increment(ref stageCalls);
                return Task.FromResult<PaddleDocumentModuleResult>(new PaddleDocumentOrientationResult(Metadata(PaddleDocumentModule.DocumentOrientation, context.Page.PageIndex), "0_degree", 0));
            });
            var pipeline = new PaddleDocumentPipeline(new[] { stage });
            PaddleDocumentPage[] pages =
            {
                new PaddleDocumentPage("page-0", new VisualSize(320, 240), 0),
                null!
            };

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => pipeline.RunManyConcurrentAsync(pages, maxDegreeOfParallelism: 1));

            Assert.AreEqual(0, Volatile.Read(ref stageCalls), "Invalid page collections must fail before any native/model stage starts.");
        }

        private static PaddleDocumentResultMetadata Metadata(PaddleDocumentModule module, int pageIndex)
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Official.First(model => model.Module == module);
            return new PaddleDocumentResultMetadata(descriptor, "test", TimeSpan.Zero, new string('a', 64), pageIndex);
        }
    }
}
