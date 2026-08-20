using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Multimodal;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Results.Multimodal;

internal static class Program
{
    private static async Task<int> Main()
    {
        using var backend = new ConsumerBackend();
        using var session = new MultimodalSession(backend, ownsBackend: false);
        var media = new[]
        {
            new MultimodalMediaInput("front", MediaKind.Image, "image/png", new byte[] { 1, 2 }),
            new MultimodalMediaInput("back", MediaKind.Image, "image/png", new byte[] { 3, 4 })
        };
        var request = new MultimodalRequest("compare", media);
        var chunks = new List<GenerationChunk>();
        await foreach (GenerationChunk chunk in session.StreamAsync(request)) chunks.Add(chunk);
        if (chunks.Count != 2 || !chunks[1].IsTerminal) return 2;
        var result = await session.GenerateAsync(request);
        if (result.Media.Count != 2 || result.Media[0].Id != "front" || result.Media[1].Id != "back") return 3;
        Console.WriteLine("DEPLOYSHARP_MULTIMODAL_PACKAGE_CONSUMER_OK media=2 streaming=terminal single-writer=contract");
        return 0;
    }

    private sealed class ConsumerBackend : IMultimodalBackendSession
    {
        public MultimodalBackendDescriptor Descriptor { get; } = new MultimodalBackendDescriptor("consumer", "1", new ModelId("consumer/vlm"), MultimodalCapabilities.TextGeneration | MultimodalCapabilities.Streaming | MultimodalCapabilities.MultipleMedia | MultimodalCapabilities.Cancellation, 2, new MultimodalAvailability(MultimodalAvailabilityState.Available, "consumer", "managed"));
        public Task<GenerationResult> GenerateAsync(MultimodalRequest request, CancellationToken cancellationToken = default(CancellationToken)) => Task.FromResult(new GenerationResult("compared", GenerationFinishReason.EndOfSequence, new TokenUsage(1, 1)));
        public async IAsyncEnumerable<GenerationChunk> StreamAsync(MultimodalRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            yield return new GenerationChunk(0, "compared", 1);
            await Task.Yield();
            yield return new GenerationChunk(1, string.Empty, finishReason: GenerationFinishReason.EndOfSequence);
        }
        public void Dispose() { }
    }
}
