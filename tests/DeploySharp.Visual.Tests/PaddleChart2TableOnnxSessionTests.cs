using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Language;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PaddleChart2TableOnnxSessionTests
    {
        private static readonly BackendId Backend = new BackendId("chart2table-fake-onnx");
        private const string VisionId = "paddle-chart/pp-chart2table/vision-projector";
        private const string EmbeddingId = "paddle-chart/pp-chart2table/token-embedding";
        private const string PrefillId = "paddle-chart/pp-chart2table/text-prefill";
        private const string DecodeId = "paddle-chart/pp-chart2table/text-decode-with-past";

        [TestMethod]
        public async Task FourGraphBundleRunsPromptPrefillAndDynamicKvDecode()
        {
            string? root = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
            if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "qwen.tiktoken"))) Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_MODEL_ROOT to the official PP-Chart2Table directory.");
            var tokenizer = new PaddleChart2TableTokenizer(root!);
            string fakePath = typeof(PaddleChart2TableOnnxSessionTests).Assembly.Location;
            var bundle = new PaddleChart2TableOnnxBundle(fakePath, fakePath, fakePath, fakePath, Backend);
            var provider = new Chart2TableFakeProvider();
            using var registry = new BackendRegistry();
            registry.Register(provider);
            using var session = new PaddleChart2TableOnnxSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, Backend));
            using PreparedVisualInput input = CreatePreparedInput();

            PaddleChart2TableGenerationResult result = await session.GenerateAsync(input, tokenizer, maximumNewTokens: 4);

            CollectionAssert.AreEqual(new[] { 100, tokenizer.EndOfSequenceTokenId }, result.TokenIds.ToArray());
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.FinishReason);
            Assert.AreEqual(286, result.PromptTokenCount);
            Assert.AreEqual(4, provider.CreatedSessions.Count);
            Assert.AreEqual(51, provider.LastDecodeInputCount);
            Assert.AreEqual(286, provider.LastDecodePastLength);
            Assert.AreSame(provider.LastPrefillFirstKey, provider.FirstDecodePastKey, "Decode should retain the provider-owned KV tensor instead of cloning it.");
            Assert.IsTrue(result.VisionTime >= TimeSpan.Zero);
            Assert.IsTrue(result.PrefillTime >= TimeSpan.Zero);
            Assert.AreEqual(1, result.DecodeSteps.Count);
        }

        private static PreparedVisualInput CreatePreparedInput()
        {
            var size = new VisualSize(1024, 1024);
            var tensor = new Tensor<float>(new TensorShape(1, 3, 1024, 1024), new float[3 * 1024 * 1024], TensorBufferOwnership.Transfer);
            float[] means = { .48145466f, .4578275f, .40821073f };
            float[] scales = { 1f / .26862954f, 1f / .26130258f, 1f / .27577711f };
            return new PreparedVisualInput("pixel_values", tensor, size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), new VisualPreprocessingDescriptor(VisualColorOrder.Rgb, means, scales, "PP-Chart2Table official GOTImageProcessor contract"));
        }

        private sealed class Chart2TableFakeProvider : IBackendProvider
        {
            private bool _disposed;
            public Chart2TableFakeProvider() => Descriptor = new BackendDescriptor(Backend, "Chart2Table fake", "1", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { "onnx" });
            public BackendDescriptor Descriptor { get; }
            public List<FakeVisualSession> CreatedSessions { get; } = new List<FakeVisualSession>();
            public int LastDecodeInputCount { get; private set; }
            public int LastDecodePastLength { get; private set; }
            public Tensor<float>? LastPrefillFirstKey { get; private set; }
            public ITensor? FirstDecodePastKey { get; private set; }
            public bool CanCreate(ModelArtifact artifact, BackendRequest request) => !_disposed && artifact.Format == "onnx" && Descriptor.Supports(request.RequiredCapabilities);

            public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Chart2TableFakeProvider));
                ModelMetadata metadata = Metadata(artifact.ModelId.Value);
                var session = new FakeVisualSession(metadata, inputs => CreateOutputs(artifact.ModelId.Value, inputs), () => TimeSpan.Zero, () => null, () => null);
                CreatedSessions.Add(session);
                return session;
            }

            public void Dispose() => _disposed = true;

            private ModelMetadata Metadata(string modelId)
            {
                IReadOnlyList<TensorDescriptor> inputs;
                IReadOnlyList<TensorDescriptor> outputs;
                if (modelId == VisionId)
                {
                    inputs = new[] { Port("pixel_values", TensorElementType.Float32, 1, 3, 1024, 1024) };
                    outputs = new[] { Port("fetch_name_0", TensorElementType.Float32, 1, 256, 1024) };
                }
                else if (modelId == EmbeddingId)
                {
                    inputs = new[] { Port("input_ids", TensorElementType.Int64, 1, -1) };
                    outputs = new[] { Port("fetch_name_0", TensorElementType.Float32, 1, -1, 1024) };
                }
                else if (modelId == PrefillId)
                {
                    inputs = new[] { Port("inputs_embeds", TensorElementType.Float32, 1, 286, 1024), Port("attention_mask", TensorElementType.Boolean, 1, 286), Port("position_ids", TensorElementType.Int64, 1, 286) };
                    outputs = CacheOutputs(286);
                }
                else if (modelId == DecodeId)
                {
                    var ports = new List<TensorDescriptor> { Port("inputs_embeds", TensorElementType.Float32, 1, 1, 1024), Port("attention_mask", TensorElementType.Boolean, 1, -1), Port("position_ids", TensorElementType.Int64, 1, 1) };
                    for (int layer = 0; layer < 24; layer++) { ports.Add(Port("past_key_" + layer, TensorElementType.Float32, 1, -1, 16, 64)); ports.Add(Port("past_value_" + layer, TensorElementType.Float32, 1, -1, 16, 64)); }
                    inputs = ports;
                    outputs = CacheOutputs(-1);
                }
                else throw new InvalidOperationException("Unexpected Chart2Table artifact: " + modelId);
                return new ModelMetadata(new ModelId(modelId), "onnx", inputs, outputs);
            }

            private InferenceOutputs CreateOutputs(string modelId, InferenceInputs inputs)
            {
                if (modelId == VisionId) return InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 256, 1024), Filled(256 * 1024, .001f), TensorBufferOwnership.Transfer));
                if (modelId == EmbeddingId)
                {
                    int sequence = checked((int)inputs.GetRequired("input_ids").Shape[1]);
                    return InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, sequence, 1024), Filled(sequence * 1024, .002f), TensorBufferOwnership.Transfer));
                }
                int cacheLength;
                int nextToken;
                if (modelId == PrefillId) { cacheLength = 286; nextToken = 100; }
                else
                {
                    LastDecodeInputCount = inputs.Count;
                    LastDecodePastLength = checked((int)inputs.GetRequired("past_key_0").Shape[1]);
                    FirstDecodePastKey = inputs.GetRequired("past_key_0");
                    cacheLength = LastDecodePastLength + 1;
                    nextToken = 151645;
                }
                var outputs = new List<NamedTensor> { new NamedTensor("fetch_name_0", Logits(nextToken)) };
                for (int index = 1; index < 49; index++)
                {
                    var cacheTensor = new Tensor<float>(new TensorShape(1, cacheLength, 16, 64), new float[cacheLength * 16 * 64], TensorBufferOwnership.Transfer);
                    if (modelId == PrefillId && index == 1) LastPrefillFirstKey = cacheTensor;
                    outputs.Add(new NamedTensor("fetch_name_" + index, cacheTensor));
                }
                return new InferenceOutputs(outputs);
            }

            private static Tensor<float> Logits(int selected)
            {
                var values = new float[151860];
                values[selected] = 10;
                return new Tensor<float>(new TensorShape(1, 1, 151860), values, TensorBufferOwnership.Transfer);
            }

            private static float[] Filled(int count, float value)
            {
                var values = new float[count];
                for (int index = 0; index < values.Length; index++) values[index] = value;
                return values;
            }

            private static IReadOnlyList<TensorDescriptor> CacheOutputs(long sequence)
            {
                var outputs = new List<TensorDescriptor> { Port("fetch_name_0", TensorElementType.Float32, 1, 1, 151860) };
                for (int index = 1; index < 49; index++) outputs.Add(Port("fetch_name_" + index, TensorElementType.Float32, 1, sequence, 16, 64));
                return outputs;
            }

            private static TensorDescriptor Port(string name, TensorElementType type, params long[] shape) => new TensorDescriptor(name, type, new TensorShape(shape));
        }
    }
}
