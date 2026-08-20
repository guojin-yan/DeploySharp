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
        using var session = new MultimodalSession(new SampleBackend(), ownsBackend: true);
        var request = new MultimodalRequest(
            "Compare the ordered views.",
            new[]
            {
                new MultimodalMediaInput("front", MediaKind.Image, "image/png", new byte[] { 1, 2, 3 }),
                new MultimodalMediaInput("back", MediaKind.Image, "image/png", new byte[] { 4, 5, 6 })
            });
        MultimodalTextResult result = await session.GenerateAsync(request);
        if (result.Media.Count != 2 || result.Media[1].Id != "back") return 2;
        Console.WriteLine("DEPLOYSHARP_MULTIMODAL_SAMPLE_OK ordered-media=2 backend-neutral=true");
        return 0;
    }

    private sealed class SampleBackend : IMultimodalBackendSession
    {
        public MultimodalBackendDescriptor Descriptor { get; } = new MultimodalBackendDescriptor("sample", "1", new ModelId("sample/vlm"), MultimodalCapabilities.TextGeneration | MultimodalCapabilities.MultipleMedia, 2, new MultimodalAvailability(MultimodalAvailabilityState.Available, "sample"));
        public Task<GenerationResult> GenerateAsync(MultimodalRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new GenerationResult("ordered", GenerationFinishReason.EndOfSequence, new TokenUsage(1, 1)));
        public async IAsyncEnumerable<GenerationChunk> StreamAsync(MultimodalRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new GenerationChunk(0, "ordered", 1);
            await Task.Yield();
            yield return new GenerationChunk(1, string.Empty, finishReason: GenerationFinishReason.EndOfSequence);
        }
        public void Dispose() { }
    }
}
