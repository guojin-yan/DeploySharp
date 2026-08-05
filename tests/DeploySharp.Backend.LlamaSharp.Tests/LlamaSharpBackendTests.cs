using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.LlamaSharp;
using JYPPX.DeploySharp.Backends.LlamaSharp.Internal;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.LlamaSharp.Tests
{
    [TestClass]
    public sealed class LlamaSharpBackendTests
    {
        [TestMethod]
        public void DescriptorDeclaresGgufAndCoreCapabilities()
        {
            using var provider = new LlamaSharpBackendProvider();
            Assert.AreEqual("llamasharp", provider.Descriptor.Id.Value);
            Assert.IsTrue(provider.Descriptor.SupportedFormats.Contains("gguf"));
            Assert.IsTrue(provider.Descriptor.Supports(BackendCapabilities.TextGeneration | BackendCapabilities.Embeddings));
        }

        [TestMethod]
        public void ValidatorRejectsMissingArtifactWithStableCode()
        {
            var artifact = new ModelArtifact(new ModelId("missing"), "gguf", Path.Combine(Path.GetTempPath(), "deploysharp-missing-model.gguf"));
            DeploySharpException exception = Assert.ThrowsExactly<DeploySharpException>(() => GgufModelArtifactValidator.Validate(artifact));
            Assert.AreEqual(DeploySharpErrorCodes.ModelArtifactInvalid, exception.ErrorCode);
        }

        [TestMethod]
        public void ValidatorChecksGgufMagic()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-invalid-" + Guid.NewGuid().ToString("N") + ".gguf");
            try
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
                var artifact = new ModelArtifact(new ModelId("invalid"), "gguf", path);
                DeploySharpException exception = Assert.ThrowsExactly<DeploySharpException>(() => GgufModelArtifactValidator.Validate(artifact));
                Assert.AreEqual(DeploySharpErrorCodes.ModelArtifactInvalid, exception.ErrorCode);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void NativeFailureMapsToDiagnosticError()
        {
            var artifact = new ModelArtifact(new ModelId("native"), "gguf", "native.gguf");
            Exception mapped = LlamaSharpExceptionMapper.Map(new DllNotFoundException("llama native missing"), artifact, "load", loading: true);
            var deploySharp = mapped as DeploySharpException;
            Assert.IsNotNull(deploySharp);
            Assert.AreEqual(DeploySharpErrorCodes.NativeRuntimeUnavailable, deploySharp!.ErrorCode);
            StringAssert.Contains(deploySharp.TechnicalDetails, "llama native missing");
        }

        [TestMethod]
        public void ProviderRejectsUnsupportedFormatWithoutLoadingNativeRuntime()
        {
            using var provider = new LlamaSharpBackendProvider();
            var artifact = new ModelArtifact(new ModelId("model"), "onnx", "model.onnx");
            Assert.IsFalse(provider.CanCreate(artifact, new LanguageModelRequest()));
            Assert.ThrowsExactly<BackendNotCompatibleException>(() => provider.CreateSession(artifact, new LanguageModelRequest()));
        }

        [TestMethod]
        public void ProviderDisposeIsIdempotentAndGuardsUse()
        {
            var provider = new LlamaSharpBackendProvider();
            provider.Dispose();
            provider.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => provider.CanCreate(new ModelArtifact(new ModelId("model"), "gguf", "model.gguf"), new LanguageModelRequest()));
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task RealLlamaIntegrationIsExplicitlyGated()
        {
            string? modelPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_LLAMA_MODEL");
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_LLAMA_MODEL to an external GGUF file to run LLamaSharp integration tests.");
            }

            try
            {
                using var provider = new LlamaSharpBackendProvider(new LlamaSharpOptions(contextSize: 512));
                var artifact = new ModelArtifact(new ModelId("integration-model"), "gguf", modelPath!);
                using var session = provider.CreateSession(artifact, new LanguageModelRequest());
                GenerationResult result = session.Generate(new TextGenerationRequest("Say hello in one word.", new GenerationOptions(maxTokens: 8, temperature: 0)));
                Assert.IsNotNull(result.Text);
                int chunks = 0;
                await foreach (GenerationChunk chunk in session.StreamAsync(new TextGenerationRequest("Say hello in one word.", new GenerationOptions(maxTokens: 4))))
                {
                    chunks++;
                }

                Assert.IsTrue(chunks > 0);
                using var cancellation = new System.Threading.CancellationTokenSource();
                cancellation.Cancel();
                bool cancellationObserved;
                try
                {
                    GenerationResult cancelled = session.Generate(new TextGenerationRequest("Long answer", new GenerationOptions(maxTokens: 16)), cancellation.Token);
                    cancellationObserved = cancelled.FinishReason == GenerationFinishReason.Cancelled;
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved = true;
                }

                Assert.IsTrue(cancellationObserved);
                if ((session.Metadata.Capabilities & LanguageModelCapabilities.Embeddings) != 0)
                {
                    EmbeddingResult embedding = session.Embed(new TextEmbeddingRequest("embedding", true));
                    Assert.IsTrue(embedding.Dimensions > 0);
                }
            }
            catch (DeploySharpException exception) when (exception.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable)
            {
                Assert.Inconclusive("The GGUF model is available but no matching LLamaSharp native backend is installed: " + exception.Message);
            }
        }
    }
}
