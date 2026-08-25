using System;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.LLM.Prompt;

internal static class Program
{
    private static int Main()
    {
        var history = new ChatHistory()
            .Add(new ChatMessage(ChatRole.System, "Answer briefly."))
            .Add(new ChatMessage(ChatRole.User, "What is DeploySharp?"));
        string prompt = new PlainTextPromptFormatter().Format(history);
        if (!prompt.EndsWith("Assistant:", StringComparison.Ordinal)) return 2;
        Console.WriteLine($"DEPLOYSHARP_LLM_SAMPLE_OK messages={history.Messages.Count} prompt-chars={prompt.Length}");
        return 0;
    }
}
