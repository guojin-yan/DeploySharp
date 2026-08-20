using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Multimodal;
using JYPPX.DeploySharp.Multimodal.Adapters;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Results.Multimodal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Multimodal.Tests
{
    [TestClass]
    public sealed class MultimodalSessionTests
    {
        [TestMethod]
        public void MediaCopiesContentAndVerifiesIdentity()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("image-fixture");
            var media = new MultimodalMediaInput("image-1", MediaKind.Image, "image/png", bytes);
            bytes[0] = 0;
            Assert.AreEqual("image-fixture", Encoding.UTF8.GetString(media.ToArray()));
            Assert.AreEqual(64, media.Sha256.Length);
            Assert.ThrowsExactly<MultimodalException>(() => new MultimodalMediaInput("image-1", MediaKind.Image, "image/png", bytes, new string('a', 64)));
        }

        [TestMethod]
        public async Task CompletedGenerationPreservesMediaOrder()
        {
            using var backend = new FakeBackend(MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Streaming | MultimodalCapabilities.MultipleMedia | MultimodalCapabilities.Cancellation, 2);
            using var session = new MultimodalSession(backend, ownsBackend: false);
            MultimodalRequest request = Request(2);
            MultimodalTextResult result = await session.GenerateAsync(request);
            Assert.AreEqual("complete", result.Generation.Text);
            Assert.AreEqual("image-0", result.Media[0].Id);
            Assert.AreEqual("image-1", result.Media[1].Id);
        }

        [TestMethod]
        public async Task StreamRequiresContiguousSequenceAndTerminalChunk()
        {
            using var backend = new FakeBackend(MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Streaming | MultimodalCapabilities.Cancellation, 1);
            using var session = new MultimodalSession(backend, ownsBackend: false);
            var chunks = new List<GenerationChunk>();
            await foreach (GenerationChunk chunk in session.StreamAsync(Request(1))) chunks.Add(chunk);
            Assert.AreEqual(3, chunks.Count);
            Assert.IsTrue(chunks[2].IsTerminal);

            backend.InvalidStream = true;
            MultimodalException exception = await Assert.ThrowsExactlyAsync<MultimodalException>(async () =>
            {
                await foreach (GenerationChunk _ in session.StreamAsync(Request(1))) { }
            });
            Assert.AreEqual(MultimodalErrorCodes.BackendContractInvalid, exception.ErrorCode);
        }

        [TestMethod]
        public async Task ConcurrentWriteIsRejectedAndTimeoutIsStable()
        {
            using var backend = new FakeBackend(MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Cancellation, 1) { BlockGeneration = true };
            using var session = new MultimodalSession(backend, ownsBackend: false);
            Task first = session.GenerateAsync(Request(1));
            await backend.Started.Task;
            MultimodalException busy = await Assert.ThrowsExactlyAsync<MultimodalException>(() => session.GenerateAsync(Request(1)));
            Assert.AreEqual(MultimodalErrorCodes.SessionBusy, busy.ErrorCode);
            backend.Release.TrySetResult(true);
            await first;

            backend.BlockGeneration = true;
            backend.Started = NewSignal();
            backend.Release = NewSignal();
            MultimodalException timeout = await Assert.ThrowsExactlyAsync<MultimodalException>(() => session.GenerateAsync(Request(1, TimeSpan.FromMilliseconds(20))));
            Assert.AreEqual(MultimodalErrorCodes.Timeout, timeout.ErrorCode);
        }

        [TestMethod]
        public async Task StreamCancellationUsesStableMultimodalError()
        {
            using var backend = new FakeBackend(MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Streaming | MultimodalCapabilities.Cancellation, 1) { BlockStream = true };
            using var session = new MultimodalSession(backend, ownsBackend: false);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            MultimodalException exception = await Assert.ThrowsExactlyAsync<MultimodalException>(async () =>
            {
                await foreach (GenerationChunk _ in session.StreamAsync(Request(1), cancellation.Token)) { }
            });
            Assert.AreEqual(MultimodalErrorCodes.Cancelled, exception.ErrorCode);
        }

        [TestMethod]
        public void CapabilityAndExternalRuntimeBoundariesAreExplicit()
        {
            using var backend = new FakeBackend(MultimodalCapabilities.TextGeneration, 1);
            using var session = new MultimodalSession(backend, ownsBackend: false);
            MultimodalException multiple = Assert.ThrowsExactly<MultimodalException>(() => session.Generate(Request(2)));
            Assert.AreEqual(MultimodalErrorCodes.CapabilityUnavailable, multiple.ErrorCode);
            MultimodalBackendDescriptor mtmd = ExternalMultimodalProbes.LlamaSharpMtmd(new ModelId("external/mtmd"));
            MultimodalBackendDescriptor genAi = ExternalMultimodalProbes.OpenVinoGenAi(new ModelId("external/openvino-genai"), "NPU");
            Assert.AreEqual(MultimodalAvailabilityState.Unavailable, mtmd.Availability.State);
            Assert.AreEqual(MultimodalAvailabilityState.Unavailable, genAi.Availability.State);
            StringAssert.Contains(genAi.Availability.RuntimeIdentity!, "NPU");
        }

        [TestMethod]
        public void RequestRejectsDuplicateMediaAndUnsupportedRegions()
        {
            MultimodalMediaInput media = Media(0);
            Assert.ThrowsExactly<MultimodalException>(() => new MultimodalRequest("describe", new[] { media, media }));
            var region = new MultimodalRegion("label", 0, 0, 10, 10);
            var regionMedia = new MultimodalMediaInput("region-image", MediaKind.Image, "image/png", new byte[] { 1 }, region: region);
            using var backend = new FakeBackend(MultimodalCapabilities.TextGeneration, 1);
            using var session = new MultimodalSession(backend, ownsBackend: false);
            Assert.AreEqual(MultimodalErrorCodes.CapabilityUnavailable, Assert.ThrowsExactly<MultimodalException>(() => session.Generate(new MultimodalRequest("read", new[] { regionMedia }))).ErrorCode);
        }

        private static MultimodalRequest Request(int count, TimeSpan? timeout = null)
        {
            var media = new List<MultimodalMediaInput>();
            for (int index = 0; index < count; index++) media.Add(Media(index));
            return new MultimodalRequest("describe", media, options: new GenerationOptions(timeout: timeout));
        }

        private static MultimodalMediaInput Media(int index) => new MultimodalMediaInput("image-" + index, MediaKind.Image, "image/png", new[] { checked((byte)(index + 1)) });
        private static TaskCompletionSource<bool> NewSignal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class FakeBackend : IMultimodalBackendSession
        {
            internal FakeBackend(MultimodalCapabilities capabilities, int maximumMedia)
            {
                Descriptor = new MultimodalBackendDescriptor("fake", "1", new ModelId("fixture/vlm"), capabilities, maximumMedia, new MultimodalAvailability(MultimodalAvailabilityState.Available, "fixture", "managed"));
            }

            public MultimodalBackendDescriptor Descriptor { get; }
            internal bool InvalidStream { get; set; }
            internal bool BlockGeneration { get; set; }
            internal bool BlockStream { get; set; }
            internal TaskCompletionSource<bool> Started { get; set; } = NewSignal();
            internal TaskCompletionSource<bool> Release { get; set; } = NewSignal();

            public async Task<GenerationResult> GenerateAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (BlockGeneration)
                {
                    Started.TrySetResult(true);
                    await Release.Task.WaitAsync(cancellationToken);
                }
                return new GenerationResult("complete", GenerationFinishReason.EndOfSequence, new TokenUsage(1, 1));
            }

            public async IAsyncEnumerable<GenerationChunk> StreamAsync(MultimodalRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
            {
                if (BlockStream) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                yield return new GenerationChunk(0, "one", 1);
                await Task.Yield();
                yield return new GenerationChunk(InvalidStream ? 2 : 1, "two", 2);
                yield return new GenerationChunk(2, string.Empty, finishReason: GenerationFinishReason.EndOfSequence);
            }

            public void Dispose() { }
        }
    }
}
