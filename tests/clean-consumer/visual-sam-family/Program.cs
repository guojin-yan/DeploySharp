using System;
using System.Globalization;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Geometry;
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
        string? encoderPath = ExternalFile("DEPLOYSHARP_SAM_ENCODER_ONNX");
        string? decoderPath = ExternalFile("DEPLOYSHARP_SAM_DECODER_ONNX");
        string? imagePath = ExternalFile("DEPLOYSHARP_SAM_IMAGE");
        if (encoderPath == null || decoderPath == null || imagePath == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_SAM_FAMILY_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        PromptableSegmentationProfile profile = PromptableSegmentationProfiles.CreateSamV1(
            "external/sam-v1-vit-b-consumer",
            new ModelId("external/sam-v1-vit-b-encoder"),
            new ModelId("external/sam-v1-vit-b-prompt-mask-decoder"),
            EncoderSha,
            DecoderSha,
            "dca509fe793f601edb92606367a655c15ac00fdf",
            "traceable official-image-encoder wrapper; torch-2.9.1+cpu; opset17",
            "official scripts/export_onnx_model.py plus dynamo=false; torch-2.9.1+cpu; opset17");
        var bundle = new PromptableSegmentationArtifactBundle(profile, new[]
        {
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder, profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder).CreateArtifact(encoderPath, OnnxRuntimeBackendProvider.BackendId)),
            new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder, profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).CreateArtifact(decoderPath, OnnxRuntimeBackendProvider.BackendId))
        });
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var session = new PromptableSegmentationImageSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
        using PreparedVisualInput input = new OpenCvPromptableSegmentationInputFactory().CreateSamV1FromFile(imagePath);
        int width = input.SourceSize.Width;
        int height = input.SourceSize.Height;
        PromptableImageEmbedding embedding = session.SetImage(input);
        var points = new[]
        {
            new PromptPoint(width * .5f, height * .5f, PromptPointLabel.Foreground),
            new PromptPoint(width * .25f, height * .25f, PromptPointLabel.Background)
        };
        var box = new RectangleF(width * .2f, height * .1f, width * .6f, height * .8f);
        PromptableSegmentationResult multi = session.Predict(new PromptableSegmentationPrompt(points, box, returnMultipleMasks: true, promptId: "consumer-point-box"));
        if (multi.Candidates.Count == 0) throw new InvalidOperationException("SAM returned no multimask candidate.");
        PromptableMaskFeedback feedback = multi.Candidates[0].LowResolutionLogits.CreateFeedback();
        PromptableSegmentationResult refined = session.Predict(new PromptableSegmentationPrompt(points, box, feedback, returnMultipleMasks: false, promptId: "consumer-feedback"));
        if (refined.Candidates.Count != 1 || refined.Segmentation.Instances.Count != 1) throw new InvalidOperationException("SAM feedback did not return one owned source mask.");

        Console.WriteLine("sam:embedding=" + embedding.Summaries[0].Shape + ";multimask=" + multi.Candidates.Count.ToString(CultureInfo.InvariantCulture) + ";refinedForeground=" + refined.Segmentation.Instances[0].Mask.ForegroundPixelCount.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("DEPLOYSHARP_VISUAL_SAM_FAMILY_CONSUMER_OK");
        return 0;
    }

    private static string? ExternalFile(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : Path.GetFullPath(value);
    }
}
