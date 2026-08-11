using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Prompt;
using JYPPX.DeploySharp.LLM.Registry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.LLM.Tests
{
    [TestClass]
    public sealed class ContractsTests
    {
        [TestMethod]
        public void ChatHistoryPreservesRolesAndSystemPrompt()
        {
            var history = new ChatHistory()
                .Add(new ChatMessage(ChatRole.System, "Be concise."))
                .Add(new ChatMessage(ChatRole.User, "Hello"));

            Assert.AreEqual(2, history.Messages.Count);
            Assert.AreEqual("Be concise.", history.GetSystemPrompt());
            Assert.AreEqual(ChatRole.User, history.Messages[1].Role);
        }

        [TestMethod]
        public void GenerationOptionsValidateAndCopyStopSequences()
        {
            var source = new List<string> { "<stop>" };
            var options = new GenerationOptions(32, 0.5f, 0.8f, 12, 42, source, TimeSpan.FromSeconds(2));
            source.Add("mutated");

            Assert.AreEqual(1, options.StopSequences.Count);
            Assert.AreEqual(42, options.Seed);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GenerationOptions(topP: 1.1f));
            Assert.ThrowsExactly<ArgumentException>(() => new GenerationOptions(stopSequences: new[] { string.Empty }));
        }

        [TestMethod]
        public void PlainTextFormatterKeepsMessageOrder()
        {
            var history = new ChatHistory(new[]
            {
                new ChatMessage(ChatRole.System, "Rules"),
                new ChatMessage(ChatRole.User, "Question")
            });

            string prompt = new PlainTextPromptFormatter().Format(history);
            StringAssert.Contains(prompt, "System: Rules");
            StringAssert.Contains(prompt, "User: Question");
            StringAssert.EndsWith(prompt, "Assistant:");
        }

        [TestMethod]
        public void RegistrySelectsFakeProviderAndDisposesIt()
        {
            var provider = new FakeProvider();
            var registry = new LanguageModelRegistry();
            registry.Register(provider);
            using (ILanguageModelSession session = registry.CreateSession(
                new ModelArtifact(new ModelId("fake-model"), "fake", "memory"),
                new LanguageModelRequest()))
            {
                Assert.AreEqual("fake", session.Metadata.Backend.Id.Value);
            }

            registry.Dispose();
            Assert.IsTrue(provider.Disposed);
        }

        [TestMethod]
        public async Task StreamAggregationPreservesOrderAndUsage()
        {
            using var session = new FakeSession();
            GenerationResult result = await session.GenerateAsync(new TextGenerationRequest("prompt"));

            Assert.AreEqual("Hello world", result.Text);
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.FinishReason);
            Assert.AreEqual(2, result.Usage.GeneratedTokens);
        }

        [TestMethod]
        public async Task CancellationIsObservableInStream()
        {
            using var session = new FakeSession();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool observed = false;
            await foreach (GenerationChunk chunk in session.StreamAsync(new TextGenerationRequest("prompt"), cancellation.Token))
            {
                observed |= chunk.FinishReason == GenerationFinishReason.Cancelled;
            }

            Assert.IsTrue(observed);
        }

        [TestMethod]
        public void RegistryReportsUnknownExplicitBackend()
        {
            using var registry = new LanguageModelRegistry();
            var artifact = new ModelArtifact(new ModelId("fake-model"), "fake", "memory");
            var request = new LanguageModelRequest(backendId: new BackendId("missing"));
            BackendNotFoundException exception = Assert.ThrowsExactly<BackendNotFoundException>(() => registry.CreateSession(artifact, request));
            Assert.AreEqual(DeploySharpErrorCodes.BackendNotFound, exception.ErrorCode);
        }

        [TestMethod]
        public void DisposedSessionRejectsFurtherOperations()
        {
            var session = new FakeSession();
            session.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => session.Embed(new TextEmbeddingRequest("value")));
        }

        [TestMethod]
        public void LanguageModelProfileAndBundleRejectMixedIdentity()
        {
            var artifact = new ModelArtifact(new ModelId("fixture/gguf"), "gguf", "fixture.gguf", new string('a', 64));
            var backend = new BackendId("fake");
            var first = new LanguageModelProfile(artifact, "revision-1", "q4_k_m", "tokenizer-1", "chat-1", "generation-1", 4096, true, backend, "audited-external-blocker");
            var secondArtifact = new ModelArtifact(new ModelId("fixture/gguf"), "gguf", "fixture-2.gguf", new string('b', 64));
            var second = new LanguageModelProfile(secondArtifact, "revision-1", "q4_k_m", "tokenizer-1", "chat-1", "generation-1", 4096, true, backend, "audited-external-blocker");

            DeploySharpException exception = Assert.ThrowsExactly<DeploySharpException>(() => new LanguageModelBundle(new[] { first, second }));
            Assert.AreEqual(DeploySharpErrorCodes.LanguageModelBundleMismatch, exception.ErrorCode);
            LanguageModelBundle bundle = new LanguageModelBundle(new[] { first });
            Assert.AreSame(first, bundle.Identity);
            Assert.ThrowsExactly<NotSupportedException>(() => ((IList<LanguageModelProfile>)bundle.Profiles).Add(first));
        }

        [TestMethod]
        public void UnverifiedProfileIsExplicitAndImmutable()
        {
            var artifact = new ModelArtifact(new ModelId("fixture/gguf"), "gguf", "fixture.gguf");
            LanguageModelProfile profile = LanguageModelProfile.CreateUnverified(artifact, new BackendId("fake"), 2048, false);
            Assert.AreEqual("caller-supplied-unverified", profile.ModelVersion);
            Assert.AreEqual("caller-owned-unverified", profile.LicenseStatus);
            Assert.IsFalse(profile.EmbeddingsSupported);
        }

        private sealed class FakeProvider : ILanguageModelProvider
        {
            public FakeProvider()
            {
                Descriptor = new BackendDescriptor(new BackendId("fake"), "Fake", "test", BackendCapabilities.TextGeneration | BackendCapabilities.Embeddings | BackendCapabilities.AsynchronousExecution, new[] { "fake" });
            }

            public BackendDescriptor Descriptor { get; }
            public bool Disposed { get; private set; }

            public bool CanCreate(ModelArtifact artifact, LanguageModelRequest request)
            {
                return artifact.Format == "fake";
            }

            public ILanguageModelSession CreateSession(ModelArtifact artifact, LanguageModelRequest request, LanguageModelSessionOptions? options = null)
            {
                return new FakeSession();
            }

            public void Dispose() { Disposed = true; }
        }

        private sealed class FakeSession : ILanguageModelSession
        {
            private bool _disposed;

            public FakeSession()
            {
                var artifact = new ModelArtifact(new ModelId("fake-model"), "fake", "memory");
                var descriptor = new BackendDescriptor(new BackendId("fake"), "Fake", "test", BackendCapabilities.TextGeneration | BackendCapabilities.Embeddings | BackendCapabilities.AsynchronousExecution, new[] { "fake" });
                Metadata = new LanguageModelMetadata(artifact, descriptor, LanguageModelCapabilities.TextGeneration | LanguageModelCapabilities.Streaming | LanguageModelCapabilities.Embeddings, 1024, 2, "cpu");
                PromptFormatter = new PlainTextPromptFormatter();
            }

            public LanguageModelMetadata Metadata { get; }
            public IPromptFormatter PromptFormatter { get; }

            public GenerationResult Generate(TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                return GenerateAsync(request, cancellationToken).GetAwaiter().GetResult();
            }

            public async Task<GenerationResult> GenerateAsync(TextGenerationRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                var text = string.Empty;
                GenerationFinishReason reason = GenerationFinishReason.None;
                int count = 0;
                await foreach (GenerationChunk chunk in StreamAsync(request, cancellationToken))
                {
                    text += chunk.Text;
                    if (chunk.IsTerminal) reason = chunk.FinishReason;
                    if (chunk.TokenId.HasValue) count++;
                }

                return new GenerationResult(text, reason, new TokenUsage(0, count));
            }

            public async IAsyncEnumerable<GenerationChunk> StreamAsync(TextGenerationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
            {
                ThrowIfDisposed();
                if (cancellationToken.IsCancellationRequested)
                {
                    yield return new GenerationChunk(0, string.Empty, finishReason: GenerationFinishReason.Cancelled);
                    yield break;
                }

                yield return new GenerationChunk(0, "Hello", 1);
                await Task.Yield();
                yield return new GenerationChunk(1, " world", 2);
                yield return new GenerationChunk(2, string.Empty, finishReason: GenerationFinishReason.EndOfSequence);
            }

            public Task<EmbeddingResult> EmbedAsync(TextEmbeddingRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                ThrowIfDisposed();
                return Task.FromResult(new EmbeddingResult(new[] { 1f, 0f }, true));
            }

            public void Dispose() { _disposed = true; }

            private void ThrowIfDisposed()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(FakeSession));
            }
        }
    }
}
