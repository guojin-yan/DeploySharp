using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests;

[TestClass]
public sealed class PaddleOcrVlOnnxSessionTests
{
    private static readonly Lazy<PaddleOcrVlTokenizer> Tokenizer = new(() =>
    {
        string? root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "tokenizer.model")))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT to the pinned official assets.");
        return new PaddleOcrVlTokenizer(root!);
    });
    private static PaddleOcrVlPreparedInput Input()
    {
        PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(new VisualSize(336, 336));
        return new PaddleOcrVlPreparedInput(plan, new Tensor<float>(new TensorShape(plan.GridHeight, plan.GridWidth, 3, 14, 14),
            new float[plan.PatchCount * 3 * 14 * 14], TensorBufferOwnership.Transfer), new string('a', 64));
    }

    [TestMethod]
    public async Task UsesOneVisionPrefillAndRequestLocalKvWithoutCloningCacheBuffers()
    {
        using var graphs = new Graphs();
        using var session = graphs.Create();
        PaddleOcrVlGenerationResult result = await session.GenerateAsync(Input(), Tokenizer.Value, new PaddleOcrVlGenerationOptions(8));
        CollectionAssert.AreEqual(new[] { 65, 66, 2 }, result.TokenIds.ToArray());
        Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.FinishReason);
        Assert.AreEqual(Tokenizer.Value.DecodeCompletion(new[] { 65, 66, 2 }), result.Text);
        Assert.AreEqual(1, graphs.Vision.Calls);
        Assert.AreEqual(3, graphs.Embedding.Calls);
        CollectionAssert.AreEqual(new[] { 0, 157, 158 }, graphs.PastLengths.ToArray());
        CollectionAssert.AreEqual(new[] { 157, 1, 1 }, graphs.QueryLengths.ToArray());
        Assert.AreEqual(157, result.PromptTokenCount);
        Assert.AreEqual(-132, result.RopeDelta);
        Assert.AreEqual(new string('a', 64), result.SourceSha256);
    }

    [TestMethod]
    public void OutputLimitAndGreedyTiesHaveExplicitTerminationWithoutAnExtraDecode()
    {
        using var graphs = new Graphs { Tie = true };
        using var session = graphs.Create();
        PaddleOcrVlGenerationResult result = session.Generate(Input(), Tokenizer.Value, new PaddleOcrVlGenerationOptions(1));
        CollectionAssert.AreEqual(new[] { 65 }, result.TokenIds.ToArray());
        Assert.AreEqual(GenerationFinishReason.MaxTokens, result.FinishReason);
        Assert.AreEqual(1, graphs.Decoder.Calls);
        Assert.AreEqual(1, graphs.Embedding.Calls);
    }

    [TestMethod]
    public void CacheAndInputBudgetsFailBeforeAnyGraphAndRecoverForNextRequest()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        using var graphs = new Graphs();
        using var session = graphs.Create();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Generate(Input(), tokenizer,
            new PaddleOcrVlGenerationOptions(maximumCacheBytes: 1)));
        Assert.AreEqual(0, graphs.Vision.Calls);
        PaddleOcrVlPreparedInput input = Input();
        ((float[])input.PixelPatches.Buffer)[0] = float.NaN;
        Assert.ThrowsExactly<VisualException>(() => session.Generate(input, tokenizer));
        Assert.AreEqual(0, graphs.Vision.Calls);
        Assert.AreEqual(GenerationFinishReason.EndOfSequence, session.Generate(Input(), tokenizer).FinishReason);
    }

    [TestMethod]
    public void InvalidCacheAndNonFiniteLogitsFailWithoutContaminatingFollowingRequests()
    {
        PaddleOcrVlTokenizer tokenizer = Tokenizer.Value;
        using var graphs = new Graphs { InvalidCache = true };
        using var session = graphs.Create();
        Assert.ThrowsExactly<VisualException>(() => session.Generate(Input(), tokenizer));
        graphs.InvalidCache = false;
        graphs.InvalidLogits = true;
        Assert.ThrowsExactly<VisualException>(() => session.Generate(Input(), tokenizer));
        graphs.InvalidLogits = false;
        CollectionAssert.AreEqual(new[] { 65, 66, 2 }, session.Generate(Input(), tokenizer).TokenIds.ToArray());
    }

    [TestMethod]
    public async Task QueuedCancellationDoesNotRunGraphsAndRemainingRequestsRetainFreshCache()
    {
        using var graphs = new Graphs { BlockVision = true };
        using var session = graphs.Create();
        Task<PaddleOcrVlGenerationResult> first = session.GenerateAsync(Input(), Tokenizer.Value);
        await graphs.Entered.Task;
        using var cancellation = new CancellationTokenSource();
        Task<PaddleOcrVlGenerationResult> queued = session.GenerateAsync(Input(), Tokenizer.Value, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await queued);
        Assert.AreEqual(1, graphs.Vision.Calls);
        graphs.Release.TrySetResult(true);
        await first;
        graphs.BlockVision = false;
        Task<PaddleOcrVlGenerationResult>[] later = Enumerable.Range(0, 3).Select(_ => session.GenerateAsync(Input(), Tokenizer.Value)).ToArray();
        PaddleOcrVlGenerationResult[] values = await Task.WhenAll(later);
        Assert.IsTrue(values.All(value => value.TokenIds.SequenceEqual(new[] { 65, 66, 2 })));
        Assert.AreEqual(4, graphs.Vision.Calls);
        Assert.AreEqual(4, graphs.PastLengths.Count(value => value == 0));
    }

    [TestMethod]
    public async Task DisposeCancelsActiveAndQueuedCallsAndDisposesOwnedGraphsOnce()
    {
        using var graphs = new Graphs { BlockVision = true };
        var session = graphs.Create(owns: true);
        Task<PaddleOcrVlGenerationResult> first = session.GenerateAsync(Input(), Tokenizer.Value);
        await graphs.Entered.Task;
        Task<PaddleOcrVlGenerationResult> second = session.GenerateAsync(Input(), Tokenizer.Value);
        await Task.Run(session.Dispose);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await first);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await second);
        session.Dispose();
        Assert.IsTrue(graphs.All.All(value => value.Disposals == 1));
        Assert.ThrowsExactly<ObjectDisposedException>(() => session.Generate(Input(), Tokenizer.Value));
    }

    [TestMethod]
    public void RejectsFixedPromptOrPastAxesWithoutTakingOwnership()
    {
        using var graphs = new Graphs();
        graphs.Decoder.Metadata = new ModelMetadata(new ModelId("test/decoder"), "onnx",
            graphs.Decoder.Metadata.Inputs.Select(port => port.Name == "past_key_0" ? new TensorDescriptor(port.Name, port.ElementType, new TensorShape(1, 2, 5, 128)) : port),
            graphs.Decoder.Metadata.Outputs);
        Assert.ThrowsExactly<VisualException>(() => graphs.Create(owns: true));
        Assert.IsTrue(graphs.All.All(value => value.Disposals == 0));
    }

    private sealed class Graphs : IDisposable
    {
        internal readonly Fake Vision, Embedding, Decoder, Head;
        internal IEnumerable<Fake> All => new[] { Vision, Embedding, Decoder, Head };
        internal readonly List<int> PastLengths = new(), QueryLengths = new();
        internal readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BlockVision, Tie, InvalidCache, InvalidLogits;
        private int _step;
        private int _imageOffset;
        private ITensor[]? _previous;
        internal Graphs()
        {
            Vision = new Fake("vision", new[] { P("pixel_patches", TensorElementType.Float32, -1, -1, 3, 14, 14) },
                new[] { P("image_features", TensorElementType.Float32, -1, 1024) }, _ =>
                {
                    _step = 0; _previous = null;
                    return InferenceOutputs.Create("image_features", F(new TensorShape(144, 1024), .25f));
                });
            Vision.BeforeAsync = async token => { if (BlockVision) { Entered.TrySetResult(true); await Release.Task.WaitAsync(token); } };
            Embedding = new Fake("embedding", new[] { P("input_ids", TensorElementType.Int64, 1, -1) },
                new[] { P("inputs_embeds", TensorElementType.Float32, 1, -1, 1024) }, input =>
                {
                    if (input.GetRequired("input_ids").Shape[1] > 1)
                        _imageOffset = Array.IndexOf((long[])input.GetRequired("input_ids").Buffer, 100295L);
                    return InferenceOutputs.Create("inputs_embeds", F(new TensorShape(1, input.GetRequired("input_ids").Shape[1], 1024), .1f));
                });
            var inputs = new List<TensorDescriptor> { P("inputs_embeds", TensorElementType.Float32, 1, -1, 1024), P("position_ids", TensorElementType.Int64, 3, 1, -1) };
            var outputs = new List<TensorDescriptor> { P("last_hidden_state", TensorElementType.Float32, 1, 1, 1024) };
            for (int layer = 0; layer < 18; layer++)
                foreach (string kind in new[] { "key", "value" })
                {
                    inputs.Add(P("past_" + kind + "_" + layer, TensorElementType.Float32, 1, 2, -1, 128));
                    outputs.Add(P("present_" + kind + "_" + layer, TensorElementType.Float32, 1, 2, -1, 128));
                }
            Decoder = new Fake("decoder", inputs, outputs, input =>
            {
                int past = (int)input.GetRequired("past_key_0").Shape[2], query = (int)input.GetRequired("inputs_embeds").Shape[1];
                PastLengths.Add(past); QueryLengths.Add(query);
                if (_step == 0)
                {
                    Assert.AreEqual(0, past);
                    float[] embedding = (float[])input.GetRequired("inputs_embeds").Buffer;
                    Assert.AreEqual(.1f, embedding[0]);
                    Assert.IsTrue(_imageOffset > 0);
                    Assert.AreEqual(.1f, embedding[(_imageOffset - 1) * 1024]);
                    Assert.AreEqual(.25f, embedding[_imageOffset * 1024]);
                    Assert.AreEqual(.25f, embedding[(_imageOffset + 143) * 1024 + 1023]);
                    Assert.AreEqual(.1f, embedding[(_imageOffset + 144) * 1024]);
                }
                else
                {
                    Assert.AreEqual(1, query);
                    Assert.AreEqual(157 + _step - 1, past);
                    Assert.AreEqual((long)past - 132, ((long[])input.GetRequired("position_ids").Buffer)[0]);
                }
                var result = new List<NamedTensor> { new("last_hidden_state", F(new TensorShape(1, 1, 1024), .5f)) };
                var cache = new ITensor[36];
                for (int i = 0; i < 36; i++)
                {
                    string kind = i % 2 == 0 ? "key_" : "value_";
                    ITensor old = input.GetRequired("past_" + kind + i / 2);
                    if (_previous != null) Assert.AreSame(_previous[i].Buffer, old.Buffer, "KV must be retained without a redundant managed clone.");
                    cache[i] = F(new TensorShape(1, 2, past + query + (InvalidCache ? 1 : 0), 128), .125f);
                    result.Add(new NamedTensor("present_" + kind + i / 2, cache[i]));
                }
                _previous = cache;
                return new InferenceOutputs(result);
            });
            Head = new Fake("head", new[] { P("last_hidden_state", TensorElementType.Float32, 1, 1, 1024) },
                new[] { P("logits", TensorElementType.Float32, 1, 1, 103424) }, _ =>
                {
                    var logits = new float[103424];
                    int token = _step == 0 ? 65 : _step == 1 ? 66 : 2;
                    logits[token] = InvalidLogits ? float.NaN : 10;
                    if (Tie) logits[66] = 10;
                    _step++;
                    return InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(1, 1, 103424), logits, TensorBufferOwnership.Transfer));
                });
        }
        internal PaddleOcrVlOnnxSession Create(bool owns = false) => new(Vision, Embedding, Decoder, Head, owns);
        public void Dispose() { }
        private static TensorDescriptor P(string name, TensorElementType type, params long[] dimensions) => new(name, type, new TensorShape(dimensions));
        private static Tensor<float> F(TensorShape shape, float value) => new(shape, Enumerable.Repeat(value, checked((int)shape.GetElementCount())).ToArray(), TensorBufferOwnership.Transfer);
    }

    private sealed class Fake : IInferenceSession
    {
        private readonly Func<InferenceInputs, InferenceOutputs> _run;
        internal Func<CancellationToken, Task>? BeforeAsync;
        internal int Calls, Disposals;
        public ModelMetadata Metadata { get; set; }
        internal Fake(string name, IEnumerable<TensorDescriptor> inputs, IEnumerable<TensorDescriptor> outputs, Func<InferenceInputs, InferenceOutputs> run)
        {
            Metadata = new ModelMetadata(new ModelId("test/" + name), "onnx", inputs, outputs); _run = run;
        }
        public InferenceOutputs Run(InferenceInputs input, CancellationToken token) { token.ThrowIfCancellationRequested(); Calls++; return _run(input); }
        public async Task<InferenceOutputs> RunAsync(InferenceInputs input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Calls++;
            if (BeforeAsync != null) await BeforeAsync(token);
            return _run(input);
        }
        public void Dispose() => Disposals++;
    }
}
