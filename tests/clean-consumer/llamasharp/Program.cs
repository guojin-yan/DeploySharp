using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.LlamaSharp;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.LLM.Registry;
using JYPPX.DeploySharp.Results.Language;

namespace DeploySharp.LlamaSharp.CleanConsumer
{
    internal static class Program
    {
        private static void Main()
        {
            string? modelPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_LLAMA_MODEL");
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                Console.WriteLine("DEPLOYSHARP_LLAMA_CONSUMER_SKIP reason=missing-exact-gguf");
                return;
            }

            string? modelSha256 = Environment.GetEnvironmentVariable("DEPLOYSHARP_LLAMA_SHA256");
            if (string.IsNullOrWhiteSpace(modelSha256))
            {
                Console.WriteLine("DEPLOYSHARP_LLAMA_CONSUMER_SKIP reason=missing-exact-sha256");
                return;
            }

            bool expectNoNative = string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_LLAMA_EXPECT_NO_NATIVE"), "1", StringComparison.Ordinal);
            using var registry = new LanguageModelRegistry();
            registry.UseLlamaSharp();
            var artifact = new ModelArtifact(new ModelId("llm/gguf/caller-owned"), "gguf", modelPath, modelSha256);
            ILanguageModelSession session;
            try
            {
                session = registry.CreateSession(artifact, new LanguageModelRequest());
            }
            catch (DeploySharpException exception) when (expectNoNative && exception.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable)
            {
                Console.WriteLine($"DEPLOYSHARP_LLAMA_NO_NATIVE_OK error={exception.ErrorCode}");
                return;
            }

            using (session)
            {
                if (expectNoNative) throw new InvalidOperationException("The no-native consumer unexpectedly loaded a native LLamaSharp backend.");
                GenerationResult result = session.Generate(new TextGenerationRequest("Reply with one word: hello.", new GenerationOptions(maxTokens: 8, temperature: 0)));
                if (string.IsNullOrWhiteSpace(result.Text)) throw new InvalidOperationException("The configured GGUF returned empty text.");
                if ((session.Metadata.Capabilities & LanguageModelCapabilities.Embeddings) != 0)
                {
                    EmbeddingResult embedding = session.Embed(new TextEmbeddingRequest("hello"));
                    if (embedding.Dimensions <= 0) throw new InvalidOperationException("The configured GGUF returned an empty embedding.");
                }

                Console.WriteLine($"DEPLOYSHARP_LLAMA_CONSUMER_OK finish={result.FinishReason} generatedTokens={result.Usage.GeneratedTokens}");
            }
        }
    }
}
