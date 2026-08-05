using System;
using JYPPX.DeploySharp.Backends.LlamaSharp;
using JYPPX.DeploySharp.LLM.Registry;

namespace DeploySharp.LlamaSharp.CleanConsumer
{
    internal static class Program
    {
        private static void Main()
        {
            using var registry = new LanguageModelRegistry();
            registry.UseLlamaSharp();
            Console.WriteLine($"{registry.GetDescriptors()[0].DisplayName} managed adapter startup diagnostic passed.");
            Console.WriteLine("Set DEPLOYSHARP_LLAMA_MODEL and use the integration test to load a real GGUF model.");
        }
    }
}
