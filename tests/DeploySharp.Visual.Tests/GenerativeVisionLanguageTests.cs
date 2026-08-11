using System;
using System.Collections.Generic;
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
    public sealed class GenerativeVisionLanguageTests
    {
        private static readonly BackendId Backend = new BackendId("generative-vlm-fake");

        [TestMethod]
        public void ProfileRequestTokenAndBundleContractsAreImmutableAndArtifactBound()
        {
            GenerativeVisionLanguageProfile profile = Profile();
            Assert.AreEqual(GenerativeVisionLanguageFamily.Blip, profile.Family);
            Assert.AreEqual(GenerativeVisionLanguageTask.ImageCaptioning, profile.Task);
            Assert.AreEqual(GenerativeVisionLanguageCacheMode.NoneFullPrefix, profile.Generation.CacheMode);
            CollectionAssert.AreEqual(new[] { "input_ids", "attention_mask", "encoder_hidden_states", "encoder_attention_mask" }, profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder).Inputs.Select(value => value.Name).ToArray());
            Assert.ThrowsExactly<VisualException>(() => new GenerativeVisionLanguageRequest(GenerativeVisionLanguageTask.VisualQuestionAnswering, " "));
            Assert.ThrowsExactly<VisualException>(() => new GenerativeTokenSequence("x", new long[0], "fake", new string('d', 64)));
            var bindings = new[]
            {
                new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, "vision.onnx", Backend)),
                new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, "decoder.onnx", Backend))
            };
            Assert.IsNotNull(new GenerativeVisionLanguageArtifactBundle(profile, bindings));
            ModelArtifact mixed = new ModelArtifact(profile.GetArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder).ModelId, "onnx", "mixed.onnx", new string('f', 64), Backend);
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, Assert.ThrowsExactly<VisualException>(() => new GenerativeVisionLanguageArtifactBundle(profile, new[] { bindings[0], new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, mixed) })).ErrorCode);
        }

        [TestMethod]
        public void OfficialProfilesExposeOneExecutableCaptionContractAndThreeAuditedBlockers()
        {
            GenerativeVisionLanguageProfile caption = GenerativeVisionLanguageProfiles.CreateBlipCaptionBase();
            Assert.IsTrue(caption.Executable);
            Assert.AreEqual(2, caption.Artifacts.Count);
            Assert.AreEqual("pixel_values", caption.GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder).Inputs.Single().Name);
            Assert.AreEqual(30524, caption.Tokenizer.VocabularySize);
            Assert.AreEqual(GenerativeVisionLanguageLengthMode.TotalTokens, caption.Generation.LengthMode);

            GenerativeVisionLanguageProfile[] blockers =
            {
                GenerativeVisionLanguageProfiles.CreateBlipVqaBaseBlocker(),
                GenerativeVisionLanguageProfiles.CreateBlip2CaptionOpt27BBlocker(),
                GenerativeVisionLanguageProfiles.CreateInstructBlipFlanT5XlBlocker()
            };
            Assert.IsTrue(blockers.All(value => !value.Executable && !string.IsNullOrWhiteSpace(value.Blocker)));
            Assert.IsFalse(blockers[1].Tokenizer.IsComplete);
            Assert.AreEqual(GenerativeVisionLanguageLengthMode.NewTokens, blockers[2].Generation.LengthMode);
            Assert.AreEqual(VisualErrorCodes.CapabilityUnavailable, Assert.ThrowsExactly<VisualException>(() => blockers[0].GetArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder)).ErrorCode);
        }

        [TestMethod]
        public void SessionCachesOneImageGeneratesTwiceStreamsAndReturnsOwnedResults()
        {
            GenerativeVisionLanguageProfile profile = Profile();
            using var registry = new BackendRegistry();
            registry.Register(new Provider(profile, TimeSpan.Zero));
            using GenerativeVisionLanguageSession session = Session(registry, profile);
            using PreparedVisualInput input = ImageInput(profile);
            GenerativeVisionLanguageImageState image = session.SetImage(input);
            Assert.IsTrue(session.HasImage);
            Assert.AreEqual(new string('9', 64), image.Identity.SourceImageSha256);
            var chunks = new List<GenerationChunk>();
            var tokenizer = new FakeTokenizer(profile.Tokenizer);
            GenerativeVisionLanguageResult first = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer, chunks.Add);
            GenerativeVisionLanguageResult second = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
            Assert.AreEqual("caption", first.Generation.Text);
            Assert.AreEqual(GenerationFinishReason.EndOfSequence, first.Generation.FinishReason);
            CollectionAssert.AreEqual(new[] { 7, 2 }, first.Generation.TokenIds.ToArray());
            CollectionAssert.AreEqual(first.Generation.TokenIds.ToArray(), second.Generation.TokenIds.ToArray());
            Assert.AreEqual(2, first.TokenScores.Count);
            Assert.AreEqual(2, chunks.Count);
            Assert.IsTrue(chunks[1].IsTerminal);
            int[] copied = first.Generation.TokenIds.ToArray();
            copied[0] = 8;
            Assert.AreEqual(7, first.Generation.TokenIds[0]);
            session.ClearImage();
            Assert.IsFalse(session.HasImage);
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageStateInvalid, Assert.ThrowsExactly<VisualException>(() => session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer)).ErrorCode);
        }

        [TestMethod]
        public async Task SessionRejectsConcurrentCancellationTokenizerMismatchNaNAndDisposedUse()
        {
            GenerativeVisionLanguageProfile profile = Profile();
            var provider = new Provider(profile, TimeSpan.FromMilliseconds(120));
            using var registry = new BackendRegistry();
            registry.Register(provider);
            var session = Session(registry, profile);
            Task<GenerativeVisionLanguageImageState> active = session.SetImageAsync(ImageInput(profile));
            await Task.Delay(20);
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageConcurrentOperation, Assert.ThrowsExactly<VisualException>(() => session.ClearImage()).ErrorCode);
            await active;
            var mismatch = new FakeTokenizer(new GenerativeVisionLanguageTokenizerContract("other", new string('f', 64), "fake", 10, 1, 2, 0, 3, 4, "exact"));
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, Assert.ThrowsExactly<VisualException>(() => session.Generate(GenerativeVisionLanguageRequest.Caption(), mismatch)).ErrorCode);
            using (var cancelled = new CancellationTokenSource(20))
            {
                VisualException exception = await Assert.ThrowsExactlyAsync<VisualException>(() => session.GenerateAsync(GenerativeVisionLanguageRequest.Caption(), new FakeTokenizer(profile.Tokenizer), cancellationToken: cancelled.Token));
                Assert.AreEqual(VisualErrorCodes.Cancelled, exception.ErrorCode);
            }
            provider.NaNLogits = true;
            Assert.AreEqual(VisualErrorCodes.GenerativeVisionLanguageGenerationInvalid, Assert.ThrowsExactly<VisualException>(() => session.Generate(GenerativeVisionLanguageRequest.Caption(), new FakeTokenizer(profile.Tokenizer))).ErrorCode);
            session.Dispose();
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, Assert.ThrowsExactly<VisualException>(() => session.ClearImage()).ErrorCode);
        }

        private static GenerativeVisionLanguageProfile Profile()
        {
            var vision = new GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole.VisionEncoder, new ModelId("external/blip/test/vision"), "onnx", new string('a', 64), 1, 17,
                new[] { Tensor("pixel_values", TensorElementType.Float32, 1, 3, 2, 2) }, new[] { Tensor("encoder_hidden_states", TensorElementType.Float32, 1, 2, 4) }, "commit", "exporter", "BSD-3-Clause", "https://example.invalid/vision");
            var decoder = new GenerativeVisionLanguageArtifactContract(GenerativeVisionLanguageArtifactRole.LanguageDecoder, new ModelId("external/blip/test/decoder"), "onnx", new string('b', 64), 1, 17,
                new[] { Tensor("input_ids", TensorElementType.Int64, 1, -1), Tensor("attention_mask", TensorElementType.Int64, 1, -1), Tensor("encoder_hidden_states", TensorElementType.Float32, 1, 2, 4), Tensor("encoder_attention_mask", TensorElementType.Int64, 1, 2) },
                new[] { Tensor("logits", TensorElementType.Float32, 1, -1, 10) }, "commit", "exporter", "BSD-3-Clause", "https://example.invalid/decoder");
            return new GenerativeVisionLanguageProfile("generative-vlm.blip.test", GenerativeVisionLanguageFamily.Blip, "test", GenerativeVisionLanguageTask.ImageCaptioning,
                new GenerativeVisionLanguageProcessorContract("processor", new string('c', 64), new VisualSize(2, 2), new[] { 0f, 0f, 0f }, new[] { 1f, 1f, 1f }, "bicubic", "official"),
                new GenerativeVisionLanguageTokenizerContract("fake", new string('d', 64), "fake", 10, 1, 2, 0, 3, 4, "exact"),
                new GenerativeVisionLanguageGenerationContract("generation", new string('e', 64), GenerativeVisionLanguageGenerationMode.Greedy, GenerativeVisionLanguageCacheMode.NoneFullPrefix, 3, 4),
                "caption", new[] { vision, decoder }, "test", true);
        }

        private static GenerativeVisionLanguageTensorContract Tensor(string name, TensorElementType type, params long[] shape) => new GenerativeVisionLanguageTensorContract(name, type, new TensorShape(shape), 1_000_000);

        private static PreparedVisualInput ImageInput(GenerativeVisionLanguageProfile profile) => new PreparedVisualInput("pixel_values", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), new VisualSize(4, 3), profile.Processor.ImageSize, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(4, 3), profile.Processor.ImageSize), inputId: new string('9', 64));

        private static GenerativeVisionLanguageSession Session(BackendRegistry registry, GenerativeVisionLanguageProfile profile)
        {
            var bundle = new GenerativeVisionLanguageArtifactBundle(profile, new[]
            {
                new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, "vision.onnx", Backend)),
                new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, "decoder.onnx", Backend))
            });
            return new GenerativeVisionLanguageSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, Backend, "cpu"));
        }

        private sealed class FakeTokenizer : IGenerativeVisionLanguageTokenizer
        {
            private readonly GenerativeVisionLanguageTokenizerContract _contract;
            internal FakeTokenizer(GenerativeVisionLanguageTokenizerContract contract) { _contract = contract; }
            public string TokenizerId => _contract.TokenizerId;
            public string Sha256 => _contract.Sha256;
            public GenerativeTokenSequence EncodePrefix(GenerativeVisionLanguageProfile profile, GenerativeVisionLanguageRequest request) => new GenerativeTokenSequence(profile.PromptTemplate, new long[] { _contract.BosTokenId, 4 }, TokenizerId, Sha256);
            public string DecodeCompletion(IEnumerable<int> tokenIds) => tokenIds.Contains(7) ? "caption" : string.Empty;
        }

        private sealed class Provider : IBackendProvider
        {
            private readonly GenerativeVisionLanguageProfile _profile;
            private readonly TimeSpan _delay;
            internal Provider(GenerativeVisionLanguageProfile profile, TimeSpan delay) { _profile = profile; _delay = delay; Descriptor = new BackendDescriptor(Backend, "Generative VLM fake", "1", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { "onnx" }); }
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
                if (contract.Role == GenerativeVisionLanguageArtifactRole.VisionEncoder) return InferenceOutputs.Create("encoder_hidden_states", new Tensor<float>(new TensorShape(1, 2, 4), Enumerable.Repeat(.25f, 8).ToArray()));
                int sequence = checked((int)inputs.GetRequired("input_ids").Shape[1]);
                var logits = Enumerable.Repeat(-10f, sequence * 10).ToArray();
                int token = sequence == 2 ? 7 : 2;
                logits[((sequence - 1) * 10) + token] = NaNLogits ? float.NaN : 10f;
                return InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(1, sequence, 10), logits));
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
