using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static readonly BackendId Backend = new BackendId("clean-opencv");
    private static readonly ModelId Model = new ModelId("clean/opencv-classification");

    private static int Main()
    {
        OpenCvRuntimeInfo runtime = OpenCvRuntimePreflight.Check();
        var options = new OpenCvPreprocessOptions(new VisualSize(2, 2), colorOrder: VisualColorOrder.Rgb, outputType: OpenCvOutputType.Float32);
        string imagePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_IMAGE") ?? Path.Combine(AppContext.BaseDirectory, "rgb.png");
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(Path.GetFullPath(imagePath), "images", options);

        using var backends = new BackendRegistry();
        backends.Register(new ClassificationProvider());
        var profiles = new VisualProfileRegistry();
        profiles.Register(new VisualModelProfile(
            "clean/opencv-classification.v1",
            Model,
            VisualTaskId.ImageClassification,
            "1.0",
            "fake",
            new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2), VisualTensorLayout.Nchw),
            new[] { new VisualOutputBinding("scores", TensorElementType.Float32, new TensorShape(1, 3)) },
            new[] { new VisualLabel(0, "zero"), new VisualLabel(1, "one"), new VisualLabel(2, "two") },
            new ClassificationDecoder("scores")));
        profiles.Freeze();

        var artifact = new ModelArtifact(Model, "fake", "classification.fake", preferredBackend: Backend);
        var request = new BackendRequest(BackendCapabilities.TensorInference, Backend);
        using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, VisualTaskId.ImageClassification), request);
        ClassificationResult result = pipeline.Run(input).GetValue<ClassificationResult>();
        if (result.TopPrediction?.Index != 1) return 2;

        Console.WriteLine("DEPLOYSHARP_VISUAL_OPENCV_CONSUMER_OK native=" + runtime.NativeVersion);
        return 0;
    }

    private sealed class ClassificationProvider : IBackendProvider
    {
        public BackendDescriptor Descriptor { get; } = new BackendDescriptor(Backend, "Clean OpenCV Fake", "1.0", BackendCapabilities.TensorInference, new[] { "fake" });
        public bool CanCreate(ModelArtifact artifact, BackendRequest request) => artifact.ModelId == Model && string.Equals(artifact.Format, "fake", StringComparison.OrdinalIgnoreCase) && Descriptor.Supports(request.RequiredCapabilities);
        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options) => new ClassificationSession();
        public void Dispose() { }
    }

    private sealed class ClassificationSession : IInferenceSession
    {
        public ModelMetadata Metadata { get; } = new ModelMetadata(
            Model,
            "fake",
            new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) },
            new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) });

        public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = inputs.GetRequired("images");
            return InferenceOutputs.Create("scores", new Tensor<float>(new TensorShape(1, 3), new[] { 0.1f, 0.8f, 0.1f }));
        }

        public Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken) => Task.FromResult(Run(inputs, cancellationToken));
        public void Dispose() { }
    }
}
