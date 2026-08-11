using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    public sealed class NativeMultimodalTests
    {
        private static readonly BackendId Backend = new BackendId("native-multimodal-fake");

        [TestMethod]
        public void OfficialProfileBindsVisionProjectorEmbeddingAndExactKvPorts()
        {
            NativeMultimodalProfile profile = NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
            Assert.IsTrue(profile.Executable);
            Assert.AreEqual(NativeMultimodalFamily.Llava, profile.Family);
            Assert.AreEqual(3, profile.Artifacts.Count);
            Assert.AreEqual(51, profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder).Inputs.Count);
            Assert.AreEqual(49, profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder).Outputs.Count);
            Assert.AreEqual("past_key_values.23.value", profile.KvCache.PastValue(23));
            Assert.AreEqual("present.23.key", profile.KvCache.PresentKey(23));
            Assert.AreEqual(1485, profile.Processor.GetPackedTokenCount(new VisualSize(350, 350), new NativeMultimodalImageGrid(1, 1)));
            Assert.AreEqual(new NativeMultimodalImageGrid(1, 1), profile.Processor.SelectGrid(new VisualSize(350, 350)));
            Assert.AreEqual(GenerativeVisionLanguageLengthMode.NewTokens, profile.Generation.LengthMode);
        }

        [TestMethod]
        public void SessionPacksImageRunsPrefillAndKvDecodeReturnsOwnedResultAndClears()
        {
            using Fixture fixture = Fixture.Create(TimeSpan.Zero);
            using NativeMultimodalPreparedImage image = fixture.Image();
            NativeMultimodalImageState state = fixture.Session.SetImage(image);
            Assert.AreEqual(10, state.ImageTokenCount);
            CollectionAssert.AreEqual(new long[] { 10, 4 }, state.FeatureState.Shape.ToArray());
            NativeMultimodalResult result = fixture.Session.Generate(GenerativeVisionLanguageRequest.Caption(), new FakeTokenizer(fixture.Profile));
            Assert.AreEqual("answer", result.Generation.Generation.Text);
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, result.Generation.Generation.FinishReason);
            CollectionAssert.AreEqual(new[] { 7, 2 }, result.Generation.Generation.TokenIds.ToArray());
            Assert.AreEqual(13, result.KvState.PastTokens);
            Assert.AreEqual(result.KvState.Identity, fixture.Session.CurrentKvState!.Identity);
            fixture.Session.Clear();
            Assert.IsFalse(fixture.Session.HasImage);
            Assert.IsNull(fixture.Session.CurrentKvState);
            Assert.AreEqual(VisualErrorCodes.NativeMultimodalStateInvalid, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Generate(GenerativeVisionLanguageRequest.Caption(), new FakeTokenizer(fixture.Profile))).ErrorCode);
        }

        [TestMethod]
        public async Task SessionRejectsConcurrencyCancellationIdentityNaNAndDisposedUse()
        {
            using Fixture fixture = Fixture.Create(TimeSpan.FromMilliseconds(100));
            Task<NativeMultimodalImageState> active = fixture.Session.SetImageAsync(fixture.Image());
            await Task.Delay(20);
            Assert.AreEqual(VisualErrorCodes.NativeMultimodalConcurrentOperation, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Clear()).ErrorCode);
            await active;
            using (var cancellation = new CancellationTokenSource(20))
            {
                VisualException cancelled = await Assert.ThrowsExactlyAsync<VisualException>(() => fixture.Session.GenerateAsync(GenerativeVisionLanguageRequest.Caption(), new FakeTokenizer(fixture.Profile), cancellationToken: cancellation.Token));
                Assert.AreEqual(VisualErrorCodes.Cancelled, cancelled.ErrorCode);
                Assert.IsNull(fixture.Session.CurrentKvState);
            }
            Assert.AreEqual(VisualErrorCodes.NativeMultimodalIdentityMismatch, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Generate(GenerativeVisionLanguageRequest.Caption(), new MismatchedTokenizer())).ErrorCode);
            fixture.Provider.NaNLogits = true;
            Assert.AreEqual(VisualErrorCodes.NativeMultimodalGenerationInvalid, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Generate(GenerativeVisionLanguageRequest.Caption(), new FakeTokenizer(fixture.Profile))).ErrorCode);
            fixture.Session.Dispose();
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Clear()).ErrorCode);
        }

        private static NativeMultimodalProfile Profile(string newlineSha)
        {
            var vision = new GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole.VisionEncoder, new ModelId("external/native/test/vision"), "onnx", new string('a', 64), 1, 14,
                new[] { Tensor("pixel_values", TensorElementType.Float32, -1, 3, 2, 2) }, new[] { Tensor("image_features", TensorElementType.Float32, -1, 4, 4) }, "revision", "exporter", "Apache-2.0", "https://example.invalid/vision");
            var embedding = new GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole.TokenEmbedding, new ModelId("external/native/test/embedding"), "onnx", new string('b', 64), 1, 13,
                new[] { Tensor("input_ids", TensorElementType.Int64, 1, -1) }, new[] { Tensor("inputs_embeds", TensorElementType.Float32, 1, -1, 4) }, "revision", "exporter", "Apache-2.0", "https://example.invalid/embedding");
            var kv = new NativeMultimodalKvCacheContract("fake-kv", 2, 1, 2, 32);
            var inputs = new List<GenerativeVisionLanguageTensorContract> { Tensor("attention_mask", TensorElementType.Int64, 1, -1), Tensor("position_ids", TensorElementType.Int64, 1, -1) };
            var outputs = new List<GenerativeVisionLanguageTensorContract> { Tensor("logits", TensorElementType.Float32, 1, -1, 10) };
            for (int layer = 0; layer < 2; layer++)
            {
                inputs.Add(Tensor(kv.PastKey(layer), TensorElementType.Float32, 1, 1, -1, 2));
                inputs.Add(Tensor(kv.PastValue(layer), TensorElementType.Float32, 1, 1, -1, 2));
                outputs.Add(Tensor(kv.PresentKey(layer), TensorElementType.Float32, 1, 1, -1, 2));
                outputs.Add(Tensor(kv.PresentValue(layer), TensorElementType.Float32, 1, 1, -1, 2));
            }
            inputs.Add(Tensor("inputs_embeds", TensorElementType.Float32, 1, -1, 4));
            var decoder = new GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole.LanguageDecoder, new ModelId("external/native/test/decoder"), "onnx", new string('c', 64), 1, 14, inputs, outputs, "revision", "exporter", "Apache-2.0", "https://example.invalid/decoder");
            var processor = new NativeMultimodalProcessorContract("fake", new string('d', 64), 2, 1, 4, 1, 1, newlineSha, new[] { new NativeMultimodalImageGrid(1, 1) }, "bicubic");
            var tokenizer = new NativeMultimodalTokenizerContract("fake", new string('e', 64), new string('f', 64), new string('1', 64), "regex", "{0}", 10, 6, 0, 1, 2, 32);
            var generation = new GenerativeVisionLanguageGenerationContract("fake", new string('2', 64), GenerativeVisionLanguageGenerationMode.Greedy, GenerativeVisionLanguageCacheMode.PastPresent, 1, 2, lengthMode: GenerativeVisionLanguageLengthMode.NewTokens);
            return new NativeMultimodalProfile("native-vlm.fake", NativeMultimodalFamily.Llava, "fake", "revision", processor, tokenizer, kv, generation, new[] { GenerativeVisionLanguageTask.ImageCaptioning }, new[] { vision, embedding, decoder }, true);
        }

        private static GenerativeVisionLanguageTensorContract Tensor(string name, TensorElementType type, params long[] shape) => new GenerativeVisionLanguageTensorContract(name, type, new TensorShape(shape), 1_000_000);

        private sealed class FakeTokenizer : INativeMultimodalTokenizer
        {
            private readonly NativeMultimodalProfile _profile;
            internal FakeTokenizer(NativeMultimodalProfile profile) { _profile = profile; }
            public string TokenizerId => _profile.Tokenizer.TokenizerId;
            public string Sha256 => _profile.Tokenizer.Identity;
            public NativeMultimodalTokenSequence Encode(NativeMultimodalProfile profile, GenerativeVisionLanguageRequest request, int imageTokenCount)
            {
                var ids = new List<long> { 1 };
                ids.AddRange(Enumerable.Repeat(6L, imageTokenCount));
                ids.Add(3);
                return new NativeMultimodalTokenSequence("fake", ids, imageTokenCount, TokenizerId, Sha256);
            }
            public string DecodeCompletion(IEnumerable<int> tokenIds) => tokenIds.Contains(7) ? "answer" : string.Empty;
        }

        private sealed class MismatchedTokenizer : INativeMultimodalTokenizer
        {
            public string TokenizerId => "other";
            public string Sha256 => new string('8', 64);
            public NativeMultimodalTokenSequence Encode(NativeMultimodalProfile profile, GenerativeVisionLanguageRequest request, int imageTokenCount) => new NativeMultimodalTokenSequence("fake", new long[] { 1, 6, 3 }, 1, TokenizerId, Sha256);
            public string DecodeCompletion(IEnumerable<int> tokenIds) => string.Empty;
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(string newlinePath, NativeMultimodalProfile profile, Provider provider, BackendRegistry registry, NativeMultimodalSession session) { NewlinePath = newlinePath; Profile = profile; Provider = provider; Registry = registry; Session = session; }
            internal string NewlinePath { get; }
            internal NativeMultimodalProfile Profile { get; }
            internal Provider Provider { get; }
            internal BackendRegistry Registry { get; }
            internal NativeMultimodalSession Session { get; }
            internal static Fixture Create(TimeSpan delay)
            {
                string path = Path.Combine(Path.GetTempPath(), "deploysharp-native-newline-" + Guid.NewGuid().ToString("N") + ".f32");
                var newline = new[] { .1f, .2f, .3f, .4f };
                var bytes = new byte[newline.Length * sizeof(float)];
                Buffer.BlockCopy(newline, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(path, bytes);
                string sha;
                using (SHA256 algorithm = SHA256.Create()) sha = string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
                NativeMultimodalProfile profile = Profile(sha);
                var provider = new Provider(profile, delay);
                var registry = new BackendRegistry();
                registry.Register(provider);
                var bindings = profile.Artifacts.Select(contract => new GenerativeVisionLanguageArtifactBinding(contract.Role, profile.CreateArtifact(contract.Role, contract.Role + ".onnx", Backend))).ToArray();
                var bundle = new NativeMultimodalArtifactBundle(profile, bindings);
                var session = new NativeMultimodalSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, Backend, "cpu"), path);
                return new Fixture(path, profile, provider, registry, session);
            }
            internal NativeMultimodalPreparedImage Image()
            {
                var source = new VisualSize(2, 2);
                var prepared = new PreparedVisualInput("pixel_values", new Tensor<float>(new TensorShape(2, 3, 2, 2), new float[24]), source, source, 2, VisualTensorLayout.Nchw, ImageTransform.Resize(source, source), inputId: new string('9', 64));
                return new NativeMultimodalPreparedImage(Profile.ProfileId, prepared, new NativeMultimodalImageGrid(1, 1), 10);
            }
            public void Dispose() { Session.Dispose(); Registry.Dispose(); try { File.Delete(NewlinePath); } catch { } }
        }

        private sealed class Provider : IBackendProvider
        {
            private readonly NativeMultimodalProfile _profile;
            private readonly TimeSpan _delay;
            internal Provider(NativeMultimodalProfile profile, TimeSpan delay) { _profile = profile; _delay = delay; Descriptor = new BackendDescriptor(Backend, "Native fake", "1", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { "onnx" }); }
            public BackendDescriptor Descriptor { get; }
            internal bool NaNLogits { get; set; }
            public bool CanCreate(ModelArtifact artifact, BackendRequest request) => _profile.Artifacts.Any(value => value.ModelId == artifact.ModelId) && Descriptor.Supports(request.RequiredCapabilities);
            public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
            {
                GenerativeVisionLanguageArtifactContract contract = _profile.Artifacts.Single(value => value.ModelId == artifact.ModelId);
                var metadata = new ModelMetadata(contract.ModelId, contract.Format, contract.Inputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)), contract.Outputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)));
                return new FakeSession(metadata, inputs => Run(contract, inputs), _delay);
            }
            public void Dispose() { }
            private InferenceOutputs Run(GenerativeVisionLanguageArtifactContract contract, InferenceInputs inputs)
            {
                if (contract.Role == GenerativeVisionLanguageArtifactRole.VisionEncoder) return InferenceOutputs.Create("image_features", new Tensor<float>(new TensorShape(2, 4, 4), Enumerable.Repeat(.25f, 32).ToArray()));
                if (contract.Role == GenerativeVisionLanguageArtifactRole.TokenEmbedding)
                {
                    int count = checked((int)inputs.GetRequired("input_ids").Shape[1]);
                    return InferenceOutputs.Create("inputs_embeds", new Tensor<float>(new TensorShape(1, count, 4), new float[count * 4]));
                }
                int sequence = checked((int)inputs.GetRequired("inputs_embeds").Shape[1]);
                int previous = checked((int)inputs.GetRequired("past_key_values.0.key").Shape[2]);
                int present = previous + sequence;
                var logits = Enumerable.Repeat(-10f, sequence * 10).ToArray();
                int selected = previous == 0 ? 7 : 2;
                logits[((sequence - 1) * 10) + selected] = NaNLogits ? float.NaN : 10f;
                var outputs = new List<NamedTensor> { new NamedTensor("logits", new Tensor<float>(new TensorShape(1, sequence, 10), logits)) };
                for (int layer = 0; layer < 2; layer++)
                {
                    outputs.Add(new NamedTensor("present." + layer + ".key", new Tensor<float>(new TensorShape(1, 1, present, 2), new float[present * 2])));
                    outputs.Add(new NamedTensor("present." + layer + ".value", new Tensor<float>(new TensorShape(1, 1, present, 2), new float[present * 2])));
                }
                return new InferenceOutputs(outputs);
            }
        }

        private sealed class FakeSession : IInferenceSession
        {
            private readonly Func<InferenceInputs, InferenceOutputs> _run;
            private readonly TimeSpan _delay;
            private bool _disposed;
            internal FakeSession(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> run, TimeSpan delay) { Metadata = metadata; _run = run; _delay = delay; }
            public ModelMetadata Metadata { get; }
            public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken) => RunAsync(inputs, cancellationToken).GetAwaiter().GetResult();
            public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken) { if (_disposed) throw new ObjectDisposedException(nameof(FakeSession)); if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken); return _run(inputs); }
            public void Dispose() { _disposed = true; }
        }
    }
}
