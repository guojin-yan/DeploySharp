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
    public sealed class PromptableSegmentationTests
    {
        private static readonly BackendId Backend = new BackendId("tests/sam-fake");
        private static readonly ModelId EncoderId = new ModelId("tests/sam-encoder");
        private static readonly ModelId DecoderId = new ModelId("tests/sam-decoder");
        private const string ImageSha = "1111111111111111111111111111111111111111111111111111111111111111";

        [TestMethod]
        public void ProfileAndBundleAreArtifactBoundAndRejectMixedSubgraphs()
        {
            PromptableSegmentationProfile profile = Profile();
            Assert.AreEqual(PromptableSegmentationFamily.Sam, profile.Family);
            Assert.AreEqual(PromptableSegmentationExecutionKind.SamV1ImageOnnx, profile.ExecutionKind);
            Assert.AreEqual(2, profile.Artifacts.Count);
            Assert.IsTrue(profile.ArtifactIdentity.Contains("ImageEncoder=" + new string('a', 64), StringComparison.Ordinal));

            PromptableSegmentationArtifactContract encoder = profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder);
            var good = new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder, encoder.CreateArtifact("encoder.onnx", Backend));
            var wrong = new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder, new ModelArtifact(DecoderId, "onnx", "decoder.onnx", new string('f', 64), Backend));
            VisualException mismatch = Assert.ThrowsExactly<VisualException>(() => new PromptableSegmentationArtifactBundle(profile, new[] { good, wrong }));
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationIdentityMismatch, mismatch.ErrorCode);

            PromptableSegmentationProfile sam2Video = PromptableSegmentationProfiles.CreateSam2VideoBlocker("tests/sam2-video", "2b90b9f5ceec907a1c18123530e92e794ad901a4", "No complete official ONNX/OpenVINO memory export.");
            Assert.AreEqual(PromptableSegmentationExecutionKind.ExternalContractOnly, sam2Video.ExecutionKind);
            Assert.AreEqual("external-contract-only", sam2Video.ArtifactIdentity);
            Assert.IsFalse(sam2Video.Video!.Executable);
            Assert.IsTrue((sam2Video.Capabilities & PromptableSegmentationCapabilities.VideoPropagation) != 0);
        }

        [TestMethod]
        public void PromptSchemaRejectsEmptyNonFiniteAndInvalidFeedback()
        {
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationContractInvalid, Assert.ThrowsExactly<VisualException>(() => new PromptableSegmentationPrompt()).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationContractInvalid, Assert.ThrowsExactly<VisualException>(() => new PromptPoint(float.NaN, 0, PromptPointLabel.Foreground)).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationContractInvalid, Assert.ThrowsExactly<VisualException>(() => new PromptableSegmentationPrompt(box: new RectangleF(0, 0, 0, 1))).ErrorCode);
            var identity = new PromptableImageIdentity("tests/sam", "ImageEncoder=" + new string('a', 64), ImageSha, new VisualSize(2, 2), new VisualSize(8, 8));
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationContractInvalid, Assert.ThrowsExactly<VisualException>(() => new PromptableMaskLogits(2, 2, new[] { 0f, float.PositiveInfinity, 0f, 0f }, identity)).ErrorCode);
        }

        [TestMethod]
        public void SetImageCachesOnceAndPointBoxFeedbackUseExactNamedInputs()
        {
            PromptableSegmentationProfile profile = Profile();
            using var registry = new BackendRegistry();
            var provider = Provider(profile);
            registry.Register(provider);
            using var session = new PromptableSegmentationImageSession(registry, Bundle(profile), new BackendRequest(BackendCapabilities.TensorInference, Backend));
            using PreparedVisualInput input = Input();

            PromptableImageEmbedding embedding = session.SetImage(input);
            Assert.AreEqual(ImageSha, embedding.Identity.ContentSha256);
            Assert.AreEqual(1, embedding.Summaries.Count);
            Assert.AreEqual(1, provider.GetRunCount(EncoderId));

            var prompt = new PromptableSegmentationPrompt(
                new[] { new PromptPoint(3, 2, PromptPointLabel.Foreground), new PromptPoint(1, 1, PromptPointLabel.Background) },
                new RectangleF(1, 1, 4, 2),
                returnMultipleMasks: true,
                promptId: "first");
            PromptableSegmentationResult first = session.Predict(prompt);
            Assert.AreEqual(3, first.Segmentation.Instances.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, first.Segmentation.Instances.Select(value => value.SourceIndex).ToArray());
            Assert.AreEqual(.8f, first.Candidates[0].Quality, .000001f);
            Assert.AreEqual("first", first.Prompt.PromptId);
            Assert.AreEqual(2, first.Prompt.PointCount);
            Assert.IsTrue(first.Prompt.HasBox);
            Assert.AreEqual(1, provider.GetRunCount(EncoderId));
            Assert.AreEqual(1, provider.GetRunCount(DecoderId));

            InferenceInputs decoderInputs = provider.LastInputs[DecoderId];
            CollectionAssert.AreEquivalent(new[] { "image_embeddings", "point_coords", "point_labels", "mask_input", "has_mask_input", "orig_im_size" }, decoderInputs.Select(value => value.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 4f, 2.5f, 1.3333334f, 1.25f, 1.3333334f, 1.25f, 6.666667f, 3.75f }, ((Tensor<float>)decoderInputs.GetRequired("point_coords")).ToArray(), new FloatComparer(.00001f));
            CollectionAssert.AreEqual(new[] { 1f, 0f, 2f, 3f }, ((Tensor<float>)decoderInputs.GetRequired("point_labels")).ToArray());
            CollectionAssert.AreEqual(new[] { 4f, 6f }, ((Tensor<float>)decoderInputs.GetRequired("orig_im_size")).ToArray());

            PromptableMaskFeedback feedback = first.Candidates[0].LowResolutionLogits.CreateFeedback();
            PromptableSegmentationResult refined = session.Predict(new PromptableSegmentationPrompt(maskFeedback: feedback, returnMultipleMasks: false));
            Assert.AreEqual(1, refined.Candidates.Count);
            Assert.AreEqual(1f, ((Tensor<float>)provider.LastInputs[DecoderId].GetRequired("has_mask_input")).ToArray()[0]);
            Assert.AreEqual(1, provider.GetRunCount(EncoderId));
            Assert.AreEqual(2, provider.GetRunCount(DecoderId));
        }

        [TestMethod]
        public void IdentityCapacityStateCancellationConcurrencyAndDisposeAreStable()
        {
            PromptableSegmentationProfile profile = Profile(maximumPromptPoints: 2);
            using var registry = new BackendRegistry();
            var provider = Provider(profile);
            registry.Register(provider);
            var session = new PromptableSegmentationImageSession(registry, Bundle(profile), new BackendRequest(BackendCapabilities.TensorInference, Backend));
            using PreparedVisualInput input = Input();
            session.SetImage(input);

            VisualException capacity = Assert.ThrowsExactly<VisualException>(() => session.Predict(new PromptableSegmentationPrompt(new[] { new PromptPoint(1, 1, PromptPointLabel.Foreground) }, new RectangleF(0, 0, 2, 2))));
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationLimitExceeded, capacity.ErrorCode);
            VisualException boundary = Assert.ThrowsExactly<VisualException>(() => session.Predict(new PromptableSegmentationPrompt(new[] { new PromptPoint(6, 1, PromptPointLabel.Foreground) })));
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationContractInvalid, boundary.ErrorCode);

            var otherIdentity = new PromptableImageIdentity(profile.ProfileId, profile.ArtifactIdentity, new string('9', 64), new VisualSize(6, 4), new VisualSize(8, 8));
            var wrongFeedback = new PromptableMaskLogits(2, 2, new float[4], otherIdentity).CreateFeedback();
            VisualException mismatch = Assert.ThrowsExactly<VisualException>(() => session.Predict(new PromptableSegmentationPrompt(maskFeedback: wrongFeedback)));
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationIdentityMismatch, mismatch.ErrorCode);

            provider.Delay = TimeSpan.FromSeconds(2);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
            VisualException cancelled = Assert.ThrowsExactly<VisualException>(() => session.Predict(new PromptableSegmentationPrompt(new[] { new PromptPoint(1, 1, PromptPointLabel.Foreground) }), cancellationToken: cancellation.Token));
            Assert.AreEqual(VisualErrorCodes.Cancelled, cancelled.ErrorCode);

            provider.Delay = TimeSpan.FromMilliseconds(200);
            Task<PromptableSegmentationResult> active = session.PredictAsync(new PromptableSegmentationPrompt(new[] { new PromptPoint(1, 1, PromptPointLabel.Foreground) }));
            SpinWait.SpinUntil(() => provider.GetActiveCount(DecoderId) == 1, 1000);
            VisualException concurrent = Assert.ThrowsExactly<VisualException>(() => session.ClearImage());
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationConcurrentOperation, concurrent.ErrorCode);
            active.GetAwaiter().GetResult();

            session.ClearImage();
            Assert.AreEqual(VisualErrorCodes.PromptableSegmentationStateInvalid, Assert.ThrowsExactly<VisualException>(() => session.Predict(new PromptableSegmentationPrompt(new[] { new PromptPoint(1, 1, PromptPointLabel.Foreground) }))).ErrorCode);
            session.Dispose();
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, Assert.ThrowsExactly<VisualException>(() => session.ClearImage()).ErrorCode);
            Assert.AreEqual(1, provider.GetDisposeCount(EncoderId));
            Assert.AreEqual(1, provider.GetDisposeCount(DecoderId));
        }

        private static PromptableSegmentationProfile Profile(int maximumPromptPoints = 8)
        {
            return PromptableSegmentationProfiles.CreateSamV1("tests/sam-v1", EncoderId, DecoderId, new string('a', 64), new string('b', 64), "dca509fe793f601edb92606367a655c15ac00fdf", "test-traceable-encoder", "official-export_onnx_model.py", imageSize: 8, embeddingChannels: 2, embeddingSize: 2, lowResolutionMaskSize: 2, maximumPromptPoints: maximumPromptPoints, maximumSourceMaskPixels: 1024);
        }

        private static PromptableSegmentationArtifactBundle Bundle(PromptableSegmentationProfile profile)
        {
            return new PromptableSegmentationArtifactBundle(profile, profile.Artifacts.Select(contract => new PromptableSegmentationArtifact(contract.Role, contract.CreateArtifact(contract.Role + ".onnx", Backend))));
        }

        private static PreparedVisualInput Input()
        {
            var source = new VisualSize(6, 4);
            var model = new VisualSize(8, 8);
            var transform = new ImageTransform(ImageTransformKind.Letterbox, source, model, 8f / 6f, 5f / 4f, 0, 0);
            var preprocessing = new VisualPreprocessingDescriptor(VisualColorOrder.Rgb, new[] { 123.675f, 116.28f, 103.53f }, new[] { 1f / 58.395f, 1f / 57.12f, 1f / 57.375f });
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 8, 8), new float[192]), source, model, 1, VisualTensorLayout.Nchw, transform, preprocessing, ImageSha);
        }

        private static MultiModelProvider Provider(PromptableSegmentationProfile profile)
        {
            PromptableSegmentationArtifactContract encoder = profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder);
            PromptableSegmentationArtifactContract decoder = profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder);
            var provider = new MultiModelProvider(Backend);
            provider.Add(Metadata(encoder), inputs =>
            {
                Assert.AreEqual(1, inputs.Count);
                Assert.AreEqual("images", inputs[0].Name);
                return InferenceOutputs.Create("image_embeddings", new Tensor<float>(new TensorShape(1, 2, 2, 2), Enumerable.Range(0, 8).Select(value => value / 10f).ToArray()));
            });
            provider.Add(Metadata(decoder), inputs =>
            {
                var masks = new float[96];
                for (int mask = 0; mask < 4; mask++) for (int index = 0; index < 24; index++) masks[(mask * 24) + index] = index >= mask && index < 21 - mask ? 1f : -1f;
                var low = Enumerable.Range(0, 16).Select(value => value / 10f).ToArray();
                return new InferenceOutputs(new[]
                {
                    new NamedTensor("masks", new Tensor<float>(new TensorShape(1, 4, 4, 6), masks)),
                    new NamedTensor("iou_predictions", new Tensor<float>(new TensorShape(1, 4), new[] { .1f, .8f, .8f, .2f })),
                    new NamedTensor("low_res_masks", new Tensor<float>(new TensorShape(1, 4, 2, 2), low))
                });
            });
            return provider;
        }

        private static ModelMetadata Metadata(PromptableSegmentationArtifactContract contract)
        {
            return new ModelMetadata(contract.ModelId, contract.Format, contract.Inputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)), contract.Outputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)));
        }

        private sealed class FloatComparer : System.Collections.IComparer
        {
            private readonly float _tolerance;
            public FloatComparer(float tolerance) { _tolerance = tolerance; }
            public int Compare(object? x, object? y) => Math.Abs((float)x! - (float)y!) <= _tolerance ? 0 : 1;
        }

        private sealed class MultiModelProvider : IBackendProvider
        {
            private readonly Dictionary<ModelId, ModelDefinition> _models = new Dictionary<ModelId, ModelDefinition>();
            private bool _disposed;

            public MultiModelProvider(BackendId id) { Descriptor = new BackendDescriptor(id, "SAM fake", "1", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution, new[] { "onnx" }); }
            public BackendDescriptor Descriptor { get; }
            public TimeSpan Delay { get; set; }
            public Dictionary<ModelId, InferenceInputs> LastInputs { get; } = new Dictionary<ModelId, InferenceInputs>();
            public void Add(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> factory) => _models.Add(metadata.ModelId, new ModelDefinition(metadata, factory));
            public bool CanCreate(ModelArtifact artifact, BackendRequest request) => !_disposed && _models.ContainsKey(artifact.ModelId) && Descriptor.Supports(request.RequiredCapabilities);
            public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
            {
                ModelDefinition definition = _models[artifact.ModelId];
                var session = new MultiModelSession(definition.Metadata, inputs => { LastInputs[artifact.ModelId] = inputs; return definition.Factory(inputs); }, () => Delay);
                definition.Sessions.Add(session);
                return session;
            }
            public int GetRunCount(ModelId id) => _models[id].Sessions.Sum(value => value.RunCount);
            public int GetDisposeCount(ModelId id) => _models[id].Sessions.Sum(value => value.DisposeCount);
            public int GetActiveCount(ModelId id) => _models[id].Sessions.Sum(value => value.ActiveCount);
            public void Dispose() { _disposed = true; }

            private sealed class ModelDefinition
            {
                public ModelDefinition(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> factory) { Metadata = metadata; Factory = factory; }
                public ModelMetadata Metadata { get; }
                public Func<InferenceInputs, InferenceOutputs> Factory { get; }
                public List<MultiModelSession> Sessions { get; } = new List<MultiModelSession>();
            }
        }

        private sealed class MultiModelSession : IInferenceSession
        {
            private readonly Func<InferenceInputs, InferenceOutputs> _factory;
            private readonly Func<TimeSpan> _delay;
            private bool _disposed;
            private int _active;
            public MultiModelSession(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> factory, Func<TimeSpan> delay) { Metadata = metadata; _factory = factory; _delay = delay; }
            public ModelMetadata Metadata { get; }
            public int RunCount { get; private set; }
            public int DisposeCount { get; private set; }
            public int ActiveCount => _active;
            public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken) => RunAsync(inputs, cancellationToken).GetAwaiter().GetResult();
            public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MultiModelSession));
                Interlocked.Increment(ref _active);
                RunCount++;
                try
                {
                    TimeSpan delay = _delay();
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return _factory(inputs);
                }
                finally { Interlocked.Decrement(ref _active); }
            }
            public void Dispose() { if (_disposed) return; _disposed = true; DisposeCount++; }
        }
    }
}
