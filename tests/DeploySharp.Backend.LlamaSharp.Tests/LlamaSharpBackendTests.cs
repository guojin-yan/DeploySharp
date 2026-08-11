using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
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
            Assert.AreEqual("0.27.0", provider.Descriptor.Version);
            Assert.IsTrue(provider.Descriptor.SupportedFormats.Contains("gguf"));
            Assert.IsTrue(provider.Descriptor.Supports(BackendCapabilities.TextGeneration | BackendCapabilities.Embeddings));
        }

        [TestMethod]
        public void ManagedAdapterAssemblyDoesNotReferenceANativeBackend()
        {
            string[] references = typeof(LlamaSharpBackendProvider).Assembly.GetReferencedAssemblies().Select(value => value.Name!).ToArray();
            CollectionAssert.Contains(references, "LLamaSharp");
            Assert.IsFalse(references.Any(value => value.StartsWith("LLamaSharp.Backend.", StringComparison.Ordinal)));
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
        public void ValidatorRequiresTheExactConfiguredSha256()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-exact-" + Guid.NewGuid().ToString("N") + ".gguf");
            try
            {
                File.WriteAllBytes(path, new byte[] { (byte)'G', (byte)'G', (byte)'U', (byte)'F', 1, 0, 0, 0, 9, 8, 7, 6 });
                string hash;
                using (SHA256 algorithm = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    hash = string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
                }

                GgufModelArtifactValidator.Validate(new ModelArtifact(new ModelId("exact"), "gguf", path, hash));
                DeploySharpException mismatch = Assert.ThrowsExactly<DeploySharpException>(() =>
                    GgufModelArtifactValidator.Validate(new ModelArtifact(new ModelId("exact"), "gguf", path, new string('0', 64))));
                Assert.AreEqual(DeploySharpErrorCodes.ModelArtifactInvalid, mismatch.ErrorCode);
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
        public void RuntimeEvidenceWriterRefusesToOverwriteAnExistingRecord()
        {
            string path = Path.Combine(Path.GetTempPath(), "deploysharp-evidence-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "original", new UTF8Encoding(false));
                Assert.ThrowsExactly<InvalidOperationException>(() => WriteNewEvidence(path, "replacement"));
                Assert.AreEqual("original", File.ReadAllText(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
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
                string? expectedSha256 = Environment.GetEnvironmentVariable("DEPLOYSHARP_LLAMA_SHA256");
                string? evidencePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_LLAMA_EVIDENCE_PATH");
                using var provider = new LlamaSharpBackendProvider(new LlamaSharpOptions(contextSize: 512, gpuLayerCount: 0));
                var artifact = new ModelArtifact(new ModelId("integration-model"), "gguf", modelPath!, expectedSha256);
                var session = (LlamaSharpSession)provider.CreateSession(artifact, new LanguageModelRequest());
                var elapsed = Stopwatch.StartNew();
                string prompt = "<|im_start|>system\nYou are a concise assistant.<|im_end|>\n<|im_start|>user\nReply with one English word meaning hello.<|im_end|>\n<|im_start|>assistant\n";
                var deterministic = new TextGenerationRequest(prompt, new GenerationOptions(maxTokens: 8, temperature: 0, seed: 31, stopSequences: new[] { "<|im_end|>" }));

                GenerationResult first = session.Generate(deterministic);
                GenerationResult repeat = session.Generate(deterministic);
                Assert.IsFalse(string.IsNullOrWhiteSpace(first.Text));
                Assert.AreEqual(first.Text, repeat.Text, "The stateless CPU repeat must be deterministic for the same prompt, seed, and sampling options.");
                Assert.AreEqual(first.FinishReason, repeat.FinishReason);
                Assert.AreEqual(first.Usage.PromptTokens, repeat.Usage.PromptTokens);
                Assert.AreEqual(first.Usage.GeneratedTokens, repeat.Usage.GeneratedTokens);

                var streamChunks = new List<GenerationChunk>();
                await foreach (GenerationChunk chunk in session.StreamAsync(new TextGenerationRequest(prompt, new GenerationOptions(maxTokens: 4, temperature: 0, seed: 31))))
                {
                    streamChunks.Add(chunk);
                }

                Assert.IsTrue(streamChunks.Count > 1);
                Assert.IsTrue(streamChunks.Last().IsTerminal);

                var cancelledChunks = new List<GenerationChunk>();
                using (var cancellation = new CancellationTokenSource())
                {
                    await foreach (GenerationChunk chunk in session.StreamAsync(new TextGenerationRequest(prompt, new GenerationOptions(maxTokens: 64, temperature: 0, seed: 31)), cancellation.Token))
                    {
                        cancelledChunks.Add(chunk);
                        if (!chunk.IsTerminal) cancellation.Cancel();
                    }
                }

                Assert.IsTrue(cancelledChunks.Count > 1);
                Assert.AreEqual(GenerationFinishReason.Cancelled, cancelledChunks.Last().FinishReason);

                string contentionCode;
                using (var contentionCancellation = new CancellationTokenSource())
                {
                    IAsyncEnumerator<GenerationChunk> active = session.StreamAsync(new TextGenerationRequest(prompt, new GenerationOptions(maxTokens: 64, temperature: 0, seed: 31)), contentionCancellation.Token).GetAsyncEnumerator();
                    try
                    {
                        Assert.IsTrue(await active.MoveNextAsync());
                        DeploySharpException contention = Assert.ThrowsExactly<DeploySharpException>(() => session.Generate(deterministic));
                        Assert.AreEqual(DeploySharpErrorCodes.LanguageModelSessionBusy, contention.ErrorCode);
                        contentionCode = contention.ErrorCode;
                    }
                    finally
                    {
                        contentionCancellation.Cancel();
                        await active.DisposeAsync();
                    }
                }

                string embeddingOperation;
                int? embeddingDimensions = null;
                string? embeddingSha256 = null;
                bool? embeddingNormalized = null;
                if ((session.Metadata.Capabilities & LanguageModelCapabilities.Embeddings) != 0)
                {
                    EmbeddingResult embedding = session.Embed(new TextEmbeddingRequest("DeploySharp local CPU embedding evidence", true));
                    Assert.IsTrue(embedding.Dimensions > 0);
                    Assert.IsTrue(embedding.IsNormalized);
                    embeddingOperation = "embedding";
                    embeddingDimensions = embedding.Dimensions;
                    embeddingNormalized = embedding.IsNormalized;
                    embeddingSha256 = HashFloats(embedding.ToArray());
                }
                else
                {
                    embeddingOperation = "embedding-unsupported";
                    Assert.AreEqual(DeploySharpErrorCodes.LanguageModelCapabilityUnavailable, Assert.ThrowsExactly<DeploySharpException>(() => session.Embed(new TextEmbeddingRequest("unsupported"))).ErrorCode);
                }

                string[] metadataKeys =
                {
                    "general.architecture",
                    "general.file_type",
                    "general.quantization_version",
                    "qwen2.context_length",
                    "qwen2.embedding_length",
                    "tokenizer.ggml.model",
                    "tokenizer.ggml.bos_token_id",
                    "tokenizer.ggml.eos_token_id",
                    "tokenizer.ggml.padding_token_id",
                    "tokenizer.chat_template"
                };
                Dictionary<string, string> modelMetadata = session.ModelMetadata
                    .Where(pair => metadataKeys.Contains(pair.Key, StringComparer.Ordinal))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                object[] nativeModules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                    .Where(module => module.FileName != null && File.Exists(module.FileName) && (module.ModuleName.Equals("llama.dll", StringComparison.OrdinalIgnoreCase) || module.ModuleName.StartsWith("ggml", StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(module => module.ModuleName, StringComparer.OrdinalIgnoreCase)
                    .Select(module => (object)new { module = module.ModuleName, path = module.FileName, size = new FileInfo(module.FileName).Length, sha256 = HashFile(module.FileName) })
                    .ToArray();
                var modelFacts = new
                {
                    description = session.ModelDescription,
                    contextLength = session.ModelContextSize,
                    loadedSizeInBytes = session.ModelSizeInBytes,
                    parameterCount = session.ModelParameterCount,
                    embeddingSize = session.ModelEmbeddingSize,
                    vocabularySize = session.ModelVocabularySize,
                    bosTokenId = session.ModelBosTokenId,
                    eosTokenId = session.ModelEosTokenId,
                    padTokenId = session.ModelPadTokenId,
                    metadata = modelMetadata
                };

                Task[] concurrentDisposals = Enumerable.Range(0, 8).Select(_ => Task.Run(session.Dispose)).ToArray();
                await Task.WhenAll(concurrentDisposals);
                session.Dispose();
                Assert.ThrowsExactly<ObjectDisposedException>(() => session.Generate(deterministic));
                elapsed.Stop();

                if (!string.IsNullOrWhiteSpace(evidencePath))
                {
                    string fullEvidencePath = Path.GetFullPath(evidencePath!);
                    string evidenceRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(modelPath!))!, "evidence") + Path.DirectorySeparatorChar;
                    if (!fullEvidencePath.StartsWith(evidenceRoot, StringComparison.OrdinalIgnoreCase)) Assert.Fail("Runtime evidence must remain below the selected model's evidence directory.");
                    Directory.CreateDirectory(Path.GetDirectoryName(fullEvidencePath)!);
                    string evidenceJson = JsonSerializer.Serialize(new
                    {
                        schemaVersion = "1.0",
                        generatedAtUtc = DateTimeOffset.UtcNow,
                        model = new { path = Path.GetFullPath(modelPath!), size = new FileInfo(modelPath!).Length, sha256 = HashFile(modelPath!), magic = "GGUF", facts = modelFacts },
                        managedRuntime = new { backend = provider.Descriptor.Id.Value, version = provider.Descriptor.Version, framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription },
                        nativeRuntime = new { package = "LLamaSharp.Backend.Cpu", version = "0.27.0", llamaCppRevision = "3f7c29d318e317b63f54c558bc69803963d7d88c", gpuLayerCount = 0, modules = nativeModules },
                        operations = new
                        {
                            cpuGenerate = new { first.Text, finishReason = first.FinishReason.ToString(), first.Usage.PromptTokens, first.Usage.GeneratedTokens, textSha256 = HashText(first.Text) },
                            stream = new { chunks = streamChunks.Count, terminal = streamChunks.Last().FinishReason.ToString(), textSha256 = HashText(string.Concat(streamChunks.Select(chunk => chunk.Text))) },
                            cancel = new { chunks = cancelledChunks.Count, terminal = cancelledChunks.Last().FinishReason.ToString() },
                            repeat = new { identical = true, textSha256 = HashText(repeat.Text) },
                            contention = new { errorCode = contentionCode },
                            dispose = new { idempotent = true, useAfterDispose = nameof(ObjectDisposedException) },
                            embedding = new { operation = embeddingOperation, dimensions = embeddingDimensions, normalized = embeddingNormalized, sha256 = embeddingSha256 }
                        },
                        runtimeEvidenceOperations = new[] { "cpu-generate", "stream", "cancel", "repeat", "contention", "dispose", embeddingOperation },
                        elapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds
                    }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
                    WriteNewEvidence(fullEvidencePath, evidenceJson);
                }
            }
            catch (DeploySharpException exception) when (exception.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable)
            {
                Assert.Inconclusive("The GGUF model is available but no matching LLamaSharp native backend is installed: " + exception.Message);
            }
        }

        private static string HashFile(string path)
        {
            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string HashText(string value)
        {
            using SHA256 algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item => item.ToString("x2")));
        }

        private static string HashFloats(float[] values)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(bytes).Select(item => item.ToString("x2")));
        }

        private static void WriteNewEvidence(string path, string contents)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(contents);
            }
            catch (IOException exception) when (File.Exists(path))
            {
                throw new InvalidOperationException("Runtime evidence is immutable. Select a new evidence file instead of overwriting an existing record.", exception);
            }
        }
    }
}
