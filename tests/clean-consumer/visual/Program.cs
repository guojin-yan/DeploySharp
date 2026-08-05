using System;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;

namespace DeploySharp.Visual.CleanConsumer;

internal static class Program
{
    private static readonly BackendId Backend = new("clean-visual");
    private static readonly ModelId ClassificationModel = new("clean/classification");
    private static readonly ModelId DetectionModel = new("clean/detection");

    private static async Task Main()
    {
        using var backends = new BackendRegistry();
        backends.Register(new CleanVisualProvider());

        var profiles = new VisualProfileRegistry();
        profiles.Register(ClassificationProfile());
        profiles.Register(DetectionProfile());
        profiles.Freeze();

        var request = new BackendRequest(BackendCapabilities.TensorInference, Backend);
        var classificationArtifact = new ModelArtifact(ClassificationModel, "fake", "classification.fake", preferredBackend: Backend);
        VisualProfileSelection classificationSelection = profiles.Select(classificationArtifact, backends, request, VisualTaskId.ImageClassification);
        using (var pipeline = new VisualPipeline(backends, classificationSelection, request))
        {
            var size = new VisualSize(2, 2);
            using var input = new PreparedVisualInput(
                "images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]),
                size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            ClassificationResult result = (await pipeline.RunAsync(input).ConfigureAwait(false)).GetValue<ClassificationResult>();
            if (result.TopPrediction?.Index != 1) throw new InvalidOperationException("Classification decoding failed.");
        }

        var detectionArtifact = new ModelArtifact(DetectionModel, "fake", "detection.fake", preferredBackend: Backend);
        VisualProfileSelection detectionSelection = profiles.Select(detectionArtifact, backends, request, VisualTaskId.ObjectDetection);
        using (var pipeline = new VisualPipeline(backends, detectionSelection, request))
        {
            var size = new VisualSize(100, 100);
            using var input = new PreparedVisualInput(
                "images", new Tensor<float>(new TensorShape(1, 3, 100, 100), new float[30000]),
                size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size));
            DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();
            if (result.Detections.Count != 1 || result.Detections[0].Label.Index != 0) throw new InvalidOperationException("Detection decoding failed.");
        }

        Console.WriteLine("Visual package-only classification and detection consumer passed.");
    }

    private static VisualModelProfile ClassificationProfile()
    {
        return new VisualModelProfile(
            "clean/classification.v1", ClassificationModel, VisualTaskId.ImageClassification, "1.0", "fake",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
            new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
            new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
            new ClassificationDecoder("scores"));
    }

    private static VisualModelProfile DetectionProfile()
    {
        var schema = new DetectionOutputSchema("detections", DetectionBoxFormat.Xyxy, false, DetectionScoreMode.ClassScore, 2, 4);
        return new VisualModelProfile(
            "clean/detection.v1", DetectionModel, VisualTaskId.ObjectDetection, "1.0", "fake",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100), VisualTensorLayout.Nchw),
            new[] { new VisualOutputBinding("detections", TensorElementType.Float32, new TensorShape(1, 1, 6)) },
            new[] { new VisualLabel(0, "cat"), new VisualLabel(1, "dog") },
            new DetectionDecoder(schema));
    }

    private sealed class CleanVisualProvider : IBackendProvider
    {
        public BackendDescriptor Descriptor { get; } = new(
            Backend, "Clean Visual Fake", "1.0",
            BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution,
            new[] { "fake" });

        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            return string.Equals(artifact.Format, "fake", StringComparison.OrdinalIgnoreCase)
                && (artifact.ModelId == ClassificationModel || artifact.ModelId == DetectionModel)
                && Descriptor.Supports(request.RequiredCapabilities);
        }

        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
        {
            return new CleanVisualSession(artifact.ModelId);
        }

        public void Dispose() { }
    }

    private sealed class CleanVisualSession : IInferenceSession
    {
        private readonly ModelId _modelId;

        public CleanVisualSession(ModelId modelId)
        {
            _modelId = modelId;
            Metadata = modelId == ClassificationModel
                ? new ModelMetadata(modelId, "fake", new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) }, new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) })
                : new ModelMetadata(modelId, "fake", new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 100, 100)) }, new[] { new TensorDescriptor("detections", TensorElementType.Float32, new TensorShape(1, 1, 6)) });
        }

        public ModelMetadata Metadata { get; }

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _modelId == ClassificationModel
                ? InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 0.1f, 0.8f, 0.1f }))
                : InferenceOutputs.Create("detections", new Tensor<float>(new TensorShape(1, 1, 6), new[] { 10f, 20f, 50f, 60f, 0.9f, 0.1f }));
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            return Task.FromResult(Run(inputs, cancellationToken));
        }

        public void Dispose() { }
    }
}
