using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class VisionLanguageTests
    {
        private static readonly BackendId Backend = new BackendId("vlm-fake");

        [TestMethod]
        public void ProfilesBindOfficialArtifactsTokenizerPortsAndBlocker()
        {
            VisionLanguageEmbeddingProfile clip = VisionLanguageProfiles.CreateClipVitB32();
            VisionLanguageEmbeddingProfile siglip = VisionLanguageProfiles.CreateSigLipBase();
            VisionLanguageEmbeddingProfile siglip2 = VisionLanguageProfiles.CreateSigLip2BaseBlocker();
            Assert.AreEqual(512, clip.EmbeddingDimension);
            Assert.AreEqual("pixel_values", clip.GetArtifact(VisionLanguageArtifactRole.ImageEncoder).Inputs.Single().Name);
            CollectionAssert.AreEqual(new[] { "input_ids", "attention_mask" }, clip.GetArtifact(VisionLanguageArtifactRole.TextEncoder).Inputs.Select(value => value.Name).ToArray());
            Assert.AreEqual(VisionLanguageScoreSemantics.ClipSoftmax, clip.ScoreSemantics);
            Assert.AreEqual(768, siglip.EmbeddingDimension);
            CollectionAssert.AreEqual(new[] { "input_ids" }, siglip.GetArtifact(VisionLanguageArtifactRole.TextEncoder).Inputs.Select(value => value.Name).ToArray());
            Assert.AreEqual(VisionLanguageScoreSemantics.SigLipIndependentSigmoid, siglip.ScoreSemantics);
            Assert.IsFalse(siglip2.Executable);
            Assert.IsTrue(siglip2.Blocker!.Contains("ONNX/OpenVINO", StringComparison.Ordinal));
            VisualException unavailable = Assert.ThrowsExactly<VisualException>(() => siglip2.GetArtifact(VisionLanguageArtifactRole.ImageEncoder));
            Assert.AreEqual(VisualErrorCodes.CapabilityUnavailable, unavailable.ErrorCode);
        }

        [TestMethod]
        public void SessionOwnsCachesAndProducesStableClassificationAndRetrieval()
        {
            VisionLanguageEmbeddingProfile profile = VisionLanguageProfiles.CreateClipVitB32();
            using var registry = new BackendRegistry();
            registry.Register(CreateProvider(profile, TimeSpan.Zero));
            using var session = CreateSession(registry, profile);
            using PreparedVisualInput imageInput = ImageInput(profile, 1);
            VisionLanguageImageEmbedding image = session.EncodeImage(imageInput);
            VisionLanguageTextEmbedding text = session.EncodeText(Tokens(profile, 3));
            Assert.IsTrue(session.HasImage && session.HasText);
            Assert.AreEqual(1, image.BatchSize);
            Assert.AreEqual(3, text.BatchSize);
            VisionLanguageScoreMatrix matrix = VisionLanguageScorer.Score(profile, image, text);
            Assert.AreEqual(1f, matrix.CopyProbabilities().Sum(), .00001f);
            Assert.AreEqual(0, VisionLanguageScorer.RetrieveTexts(profile, image, text, 3)[0].Index);
            using PreparedVisualInput imageBatchInput = ImageInput(profile, 2);
            VisionLanguageImageEmbedding imageBatch = session.EncodeImage(imageBatchInput);
            CollectionAssert.AreEqual(new[] { 0, 1 }, VisionLanguageScorer.RetrieveImages(profile, imageBatch, text, 0, 2).Select(value => value.Index).ToArray());
            VisionLanguageClassificationResult classification = VisionLanguageScorer.Classify(profile, image, text, new[] { new ZeroShotLabelPrompt("first", new[] { 0 }), new ZeroShotLabelPrompt("second", new[] { 1, 2 }) });
            Assert.AreEqual("first", classification.Classification.TopPrediction!.Label);
            float[] copy = image.CopyValues();
            copy[0] = 999;
            Assert.AreNotEqual(copy[0], image.CopyValues()[0]);
            session.ClearCache();
            Assert.IsFalse(session.HasImage);
            Assert.IsFalse(session.HasText);
        }

        [TestMethod]
        public async Task SessionRejectsTokenizerMismatchConcurrencyCancellationAndDisposedUse()
        {
            VisionLanguageEmbeddingProfile profile = VisionLanguageProfiles.CreateClipVitB32();
            var provider = CreateProvider(profile, TimeSpan.FromMilliseconds(150));
            using var registry = new BackendRegistry();
            registry.Register(provider);
            var session = CreateSession(registry, profile);
            TextTokenBatch wrong = new TextTokenBatch(new[] { "wrong" }, new long[77], 1, 77, profile.Tokenizer.TokenizerId, new string('f', 64), new long[77]);
            VisualException identity = Assert.ThrowsExactly<VisualException>(() => session.EncodeText(wrong));
            Assert.AreEqual(VisualErrorCodes.VisionLanguageContractInvalid, identity.ErrorCode);
            Task<VisionLanguageImageEmbedding> active = session.EncodeImageAsync(ImageInput(profile, 1));
            await Task.Delay(20);
            VisualException concurrent = Assert.ThrowsExactly<VisualException>(() => session.EncodeText(Tokens(profile, 1)));
            Assert.AreEqual(VisualErrorCodes.VisionLanguageConcurrentOperation, concurrent.ErrorCode);
            await active;
            using var cancelled = new CancellationTokenSource(20);
            VisualException cancellation = await Assert.ThrowsExactlyAsync<VisualException>(() => session.EncodeTextAsync(Tokens(profile, 1), cancellationToken: cancelled.Token));
            Assert.AreEqual(VisualErrorCodes.Cancelled, cancellation.ErrorCode);
            session.Dispose();
            VisualException disposed = Assert.ThrowsExactly<VisualException>(() => session.ClearCache());
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, disposed.ErrorCode);
        }

        [TestMethod]
        public void TokenBatchAndLabelAggregationRejectInvalidShapesMasksAndDuplicateIndexes()
        {
            VisionLanguageEmbeddingProfile siglip = VisionLanguageProfiles.CreateSigLipBase();
            using var registry = new BackendRegistry();
            registry.Register(CreateProvider(siglip, TimeSpan.Zero));
            using var session = CreateSession(registry, siglip);
            var forbiddenMask = new TextTokenBatch(new[] { "x" }, new long[64], 1, 64, siglip.Tokenizer.TokenizerId, siglip.Tokenizer.Sha256, new long[64]);
            Assert.ThrowsExactly<VisualException>(() => session.EncodeText(forbiddenMask));
            Assert.ThrowsExactly<VisualException>(() => new TextTokenBatch(new[] { "x" }, new long[63], 1, 64, siglip.Tokenizer.TokenizerId, siglip.Tokenizer.Sha256));
            Assert.ThrowsExactly<VisualException>(() => new ZeroShotLabelPrompt("x", new[] { 0, 0 }));
            TextTokenBatch valid = Tokens(siglip, 2);
            Assert.IsNull(valid.CopyAttentionMask());
            Assert.AreEqual(64, valid.SequenceLength);
        }

        private static VisionLanguageEmbeddingSession CreateSession(BackendRegistry registry, VisionLanguageEmbeddingProfile profile)
        {
            var bundle = new VisionLanguageArtifactBundle(profile, profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder, "image.onnx", Backend), profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder, "text.onnx", Backend));
            return new VisionLanguageEmbeddingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, Backend, "cpu"));
        }

        private static PreparedVisualInput ImageInput(VisionLanguageEmbeddingProfile profile, int batch)
        {
            VisionLanguageArtifactContract artifact = profile.GetArtifact(VisionLanguageArtifactRole.ImageEncoder);
            var tensor = new Tensor<float>(new TensorShape(batch, 3, 224, 224), new float[batch * 3 * 224 * 224]);
            return new PreparedVisualInput(artifact.Inputs[0].Name, tensor, profile.ImageSize, profile.ImageSize, batch, VisualTensorLayout.Nchw, ImageTransform.Resize(profile.ImageSize, profile.ImageSize), inputId: new string('a', 64));
        }

        private static TextTokenBatch Tokens(VisionLanguageEmbeddingProfile profile, int batch)
        {
            int sequence = profile.Tokenizer.MaximumTokens;
            var ids = new long[batch * sequence];
            var texts = Enumerable.Range(0, batch).Select(value => "prompt-" + value).ToArray();
            long[]? mask = null;
            if (profile.Tokenizer.AttentionMaskRequired) { mask = Enumerable.Repeat(1L, ids.Length).ToArray(); }
            return new TextTokenBatch(texts, ids, batch, sequence, profile.Tokenizer.TokenizerId, profile.Tokenizer.Sha256, mask);
        }

        private static MultiProvider CreateProvider(VisionLanguageEmbeddingProfile profile, TimeSpan delay)
        {
            var definitions = new Dictionary<ModelId, Func<InferenceInputs, InferenceOutputs>>();
            foreach (VisionLanguageArtifactRole role in new[] { VisionLanguageArtifactRole.ImageEncoder, VisionLanguageArtifactRole.TextEncoder })
            {
                VisionLanguageArtifactContract contract = profile.GetArtifact(role);
                int dimension = profile.EmbeddingDimension;
                definitions.Add(contract.ModelId, inputs =>
                {
                    int batch = checked((int)inputs[0].Tensor.Shape[0]);
                    var values = new float[batch * dimension];
                    for (int row = 0; row < batch; row++) values[(row * dimension) + (row % Math.Min(batch, dimension))] = 1f;
                    return InferenceOutputs.Create(contract.Outputs[0].Name, new Tensor<float>(new TensorShape(batch, dimension), values));
                });
            }
            return new MultiProvider(profile, definitions, delay);
        }

        private sealed class MultiProvider : IBackendProvider
        {
            private readonly VisionLanguageEmbeddingProfile _profile;
            private readonly Dictionary<ModelId, Func<InferenceInputs, InferenceOutputs>> _definitions;
            private readonly TimeSpan _delay;
            public MultiProvider(VisionLanguageEmbeddingProfile profile, Dictionary<ModelId, Func<InferenceInputs, InferenceOutputs>> definitions, TimeSpan delay) { _profile = profile; _definitions = definitions; _delay = delay; Descriptor = new BackendDescriptor(Backend, "VLM fake", "1", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { "onnx" }); }
            public BackendDescriptor Descriptor { get; }
            public bool CanCreate(ModelArtifact artifact, BackendRequest request) => _definitions.ContainsKey(artifact.ModelId) && Descriptor.Supports(request.RequiredCapabilities);
            public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
            {
                VisionLanguageArtifactContract contract = _profile.Artifacts.Single(value => value.ModelId == artifact.ModelId);
                var metadata = new ModelMetadata(contract.ModelId, contract.Format, contract.Inputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)), contract.Outputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)));
                return new Session(metadata, _definitions[artifact.ModelId], _delay);
            }
            public void Dispose() { }
        }

        private sealed class Session : IInferenceSession
        {
            private readonly Func<InferenceInputs, InferenceOutputs> _factory;
            private readonly TimeSpan _delay;
            private bool _disposed;
            public Session(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> factory, TimeSpan delay) { Metadata = metadata; _factory = factory; _delay = delay; }
            public ModelMetadata Metadata { get; }
            public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken) => RunAsync(inputs, cancellationToken).GetAwaiter().GetResult();
            public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken) { if (_disposed) throw new ObjectDisposedException(nameof(Session)); if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken); return _factory(inputs); }
            public void Dispose() { _disposed = true; }
        }
    }
}
