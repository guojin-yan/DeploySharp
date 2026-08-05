using System;
using System.Collections.Generic;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;

namespace DeploySharp.Visual.Tests
{
    internal static class VisualTestData
    {
        public static readonly BackendId BackendId = new BackendId("fake-visual");
        public static readonly ModelId ClassificationModelId = new ModelId("tests/classification");
        public static readonly ModelId DetectionModelId = new ModelId("tests/detection");

        public static VisualModelProfile ClassificationProfile(ClassificationScoreMode mode = ClassificationScoreMode.Logits, int topK = 3, float threshold = 0)
        {
            return new VisualModelProfile(
                "tests/classification.v1", ClassificationModelId, VisualTaskId.ImageClassification, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
                new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
                new ClassificationDecoder("scores", mode, topK, threshold));
        }

        public static VisualModelProfile DetectionProfile(DetectionOutputSchema schema, DetectionDecoderOptions? options = null, TensorShape? outputShape = null)
        {
            return new VisualModelProfile(
                "tests/detection.v1", DetectionModelId, VisualTaskId.ObjectDetection, "1.0", "fake",
                new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
                new[] { new VisualOutputBinding(schema.OutputName, TensorElementType.Float32, outputShape ?? new TensorShape(-1, schema.ClassScoreOffset + schema.ClassCount)) },
                new[] { new VisualLabel(0, "cat"), new VisualLabel(1, "dog") },
                new DetectionDecoder(schema, options));
        }

        public static PreparedVisualInput ClassificationInput(PreparedInputOwnership ownership = PreparedInputOwnership.Borrowed, IDisposable? resource = null)
        {
            var size = new VisualSize(2, 2);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), inputId: "classification", ownership: ownership, ownedResource: resource);
        }

        public static PreparedVisualInput DetectionInput(VisualSize? sourceSize = null, ImageTransform? transform = null)
        {
            VisualSize source = sourceSize ?? new VisualSize(100, 100);
            VisualSize model = new VisualSize(100, 100);
            return new PreparedVisualInput("images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]), source, model, 1, VisualTensorLayout.Nchw, transform ?? ImageTransform.Resize(source, model), inputId: "detection");
        }

        public static ModelMetadata Metadata(VisualModelProfile profile, TensorShape outputShape)
        {
            return new ModelMetadata(profile.ModelId, "fake", new[] { new TensorDescriptor(profile.Input.Name, profile.Input.ElementType, profile.Input.ShapePattern) }, new[] { new TensorDescriptor(profile.Outputs[0].Name, profile.Outputs[0].ElementType, outputShape) });
        }

        public static PipelineFixture Pipeline(VisualModelProfile profile, TensorShape outputShape, Func<InferenceInputs, InferenceOutputs> outputFactory, int maximumConcurrency = 1)
        {
            var provider = new FakeVisualBackendProvider(Metadata(profile, outputShape), outputFactory);
            var registry = new BackendRegistry();
            registry.Register(provider);
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile);
            profiles.Freeze();
            var artifact = new ModelArtifact(profile.ModelId, "fake", "fixture.fake", preferredBackend: BackendId);
            var request = new BackendRequest(BackendCapabilities.TensorInference, BackendId);
            VisualProfileSelection selection = profiles.Select(artifact, registry, request, profile.Task);
            var pipeline = new VisualPipeline(registry, selection, request, new SessionOptions(maximumConcurrency));
            return new PipelineFixture(registry, provider, pipeline);
        }
    }

    internal sealed class PipelineFixture : IDisposable
    {
        public PipelineFixture(BackendRegistry registry, FakeVisualBackendProvider provider, VisualPipeline pipeline) { Registry = registry; Provider = provider; Pipeline = pipeline; }
        public BackendRegistry Registry { get; }
        public FakeVisualBackendProvider Provider { get; }
        public VisualPipeline Pipeline { get; }
        public void Dispose() { Pipeline.Dispose(); Registry.Dispose(); }
    }
}
