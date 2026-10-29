using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;

internal sealed class OcrService : IDisposable
{
    private readonly PaddleOcrProfile _detection;
    public OcrPipeline Pipeline { get; }

    public OcrService(BackendRegistry backends, BackendRequest request, string models)
    {
        var det = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv5/mobile-det"));
        var rec = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv5/mobile-rec"));
        _detection = PaddleOcrProfiles.CreateDetection(det.ModelId, Contract(det.ReleaseArtifact!.Opset), det.InputName, det.OutputName,
            postprocess: new PaddleDbPostprocessOptions(maximumRegions: 512));
        var characters = PaddleOcrProfiles.LoadCharacterSet(Path.Combine(models, "ppocrv5_dict.txt"), "local.v5", "1", useSpaceCharacter: true);
        var recognition = PaddleOcrModelCatalog.CreateProfile(rec, Contract(rec.ReleaseArtifact!.Opset), characters, 16);
        Pipeline = new OcrPipeline(backends, Select(_detection.VisualProfile, "ppocrv5-mobile-det.onnx"), request,
            Select(recognition.VisualProfile, "ppocrv5-mobile-rec.onnx"), request, recognition.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 512, maximumRecognitionBatch: 16));

        VisualProfileSelection Select(VisualModelProfile profile, string file)
        {
            var registry = new VisualProfileRegistry();
            registry.Register(profile); registry.Freeze();
            return registry.Select(new ModelArtifact(profile.ModelId, "onnx", Path.Combine(models, file)), backends, request, profile.Task);
        }
        static PaddleOcrArtifactContract Contract(int opset) => new(opset, "", "", "", "Apache-2.0", "local-input-v1", "local-output-v1", "onnx");
    }

    public async Task<OcrResult> RecognizeAsync(OpenCvDecodedRoiImage image, RectangleF bounds, CancellationToken token)
    {
        var settings = OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(
            new VisualSize(Math.Max(1, (int)bounds.Width), Math.Max(1, (int)bounds.Height)), resizeLong: 960);
        var detectionInput = image.PrepareRectangle(new RectangleRoiGeometry(bounds),
            _detection.VisualProfile.Input.Name, settings, token);
        using var input = new PageRoiOcrInput(image, detectionInput);
        return await Pipeline.RunAsync(input, cancellationToken: token);
    }

    public void Dispose() => Pipeline.Dispose();
}
