using System;
using System.Globalization;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private const string EncoderSha = "95ea8873d6dbbf1226bf124f56930c1652c09c19f84c032b3721979699a21c3a";
    private const string DecoderSha = "b520bc95e049862bde768b959c124d6c2a53436df81bf9c5e8689f6e406ba21a";

    private static int Main()
    {
        string? detectorPath = ExternalFile("DEPLOYSHARP_OPEN_VOCAB_YOLOWORLD_ONNX");
        string? imagePath = ExternalFile("DEPLOYSHARP_OPEN_VOCAB_IMAGE");
        if (detectorPath == null || imagePath == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_OPEN_VOCAB_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        OpenVocabularyDetectionProfile detector = OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var profiles = new VisualProfileRegistry();
        profiles.Register(detector.VisualProfile);
        profiles.Freeze();
        using (var pipeline = new VisualPipeline(registry, profiles.Select(detector.CreateArtifact(detectorPath, OnnxRuntimeBackendProvider.BackendId), registry, request, VisualTaskId.ObjectDetection), request))
        using (PreparedVisualInput input = new OpenCvOpenVocabularyInputFactory().CreateFromFile(imagePath, detector))
        {
            OpenVocabularyDetectionResult result = pipeline.Run(input).GetValue<OpenVocabularyDetectionResult>();
            if (result.Detections.Detections.Count == 0 || result.Matches.Count != result.Detections.Detections.Count) throw new InvalidOperationException("The fixed-vocabulary detector returned no canonical phrase-bound result.");
            Console.WriteLine("open-vocabulary:count=" + result.Detections.Detections.Count.ToString(CultureInfo.InvariantCulture) + ";first=" + result.Matches[0].Phrase + ";vocabularySha=" + result.VocabularySha256);
        }
        Console.WriteLine("DEPLOYSHARP_VISUAL_OPEN_VOCAB_CONSUMER_OK");

        string? encoderPath = ExternalFile("DEPLOYSHARP_SAM_ENCODER_ONNX");
        string? decoderPath = ExternalFile("DEPLOYSHARP_SAM_DECODER_ONNX");
        if (encoderPath == null || decoderPath == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_GROUNDED_SAM_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        PromptableSegmentationProfile sam = PromptableSegmentationProfiles.CreateSamV1(
            "external/sam-v1-vit-b-open-vocabulary-consumer",
            new ModelId("external/sam-v1-vit-b-encoder"),
            new ModelId("external/sam-v1-vit-b-prompt-mask-decoder"),
            EncoderSha,
            DecoderSha,
            "dca509fe793f601edb92606367a655c15ac00fdf",
            "traceable official-image-encoder wrapper; torch-2.9.1+cpu; opset17",
            "official scripts/export_onnx_model.py plus dynamo=false; torch-2.9.1+cpu; opset17");
        var bundle = new PromptableSegmentationArtifactBundle(sam, new[]
        {
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder, sam.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder).CreateArtifact(encoderPath, OnnxRuntimeBackendProvider.BackendId)),
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder, sam.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).CreateArtifact(decoderPath, OnnxRuntimeBackendProvider.BackendId))
        });
        using (var session = new GroundedSamImageSession(registry, detector, detector.CreateArtifact(detectorPath, OnnxRuntimeBackendProvider.BackendId), request, bundle, request))
        using (GroundedSamPreparedInput input = new OpenCvOpenVocabularyInputFactory().CreateGroundedSamFromFile(imagePath, detector, sam))
        {
            GroundedSamImageState state = session.SetImage(input);
            GroundedSamResult result = session.SegmentDetections(1, .25f);
            if (result.Instances.Count != 1 || result.Instances[0].Segmentation.Segmentation.Instances.Count != 1) throw new InvalidOperationException("Grounded-SAM did not return one owned source mask.");
            Console.WriteLine("grounded-sam:embedding=" + state.Embedding.Summaries[0].Shape + ";phrase=" + result.Instances[0].Match.Phrase + ";foreground=" + result.Instances[0].Segmentation.Segmentation.Instances[0].Mask.ForegroundPixelCount.ToString(CultureInfo.InvariantCulture));
        }
        Console.WriteLine("DEPLOYSHARP_VISUAL_GROUNDED_SAM_CONSUMER_OK");
        return 0;
    }

    private static string? ExternalFile(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : Path.GetFullPath(value);
    }
}
