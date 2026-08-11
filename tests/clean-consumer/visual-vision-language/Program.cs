using System;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static int Main()
    {
        string? imageEncoder = ExternalFile("DEPLOYSHARP_VLM_CLIP_IMAGE_ONNX");
        string? textEncoder = ExternalFile("DEPLOYSHARP_VLM_CLIP_TEXT_ONNX");
        string? imagePath = ExternalFile("DEPLOYSHARP_VLM_IMAGE");
        if (imageEncoder == null || textEncoder == null || imagePath == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_VLM_EMBEDDING_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        VisionLanguageEmbeddingProfile profile = VisionLanguageProfiles.CreateClipVitB32();
        var backend = OnnxRuntimeBackendProvider.BackendId;
        var bundle = new VisionLanguageArtifactBundle(profile, profile.CreateArtifact(VisionLanguageArtifactRole.ImageEncoder, imageEncoder, backend), profile.CreateArtifact(VisionLanguageArtifactRole.TextEncoder, textEncoder, backend));
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var session = new VisionLanguageEmbeddingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
        using PreparedVisualInput input = new OpenCvVisionLanguageInputFactory().CreateFromFile(imagePath, profile);
        VisionLanguageImageEmbedding image = session.EncodeImage(input);
        VisionLanguageTextEmbedding text = session.EncodeText(ClipPrompts(profile));
        VisionLanguageClassificationResult classification = VisionLanguageScorer.Classify(profile, image, text, new[] { new ZeroShotLabelPrompt("bus", new[] { 0 }), new ZeroShotLabelPrompt("person", new[] { 1 }), new ZeroShotLabelPrompt("dog", new[] { 2 }) });
        var textRetrieval = VisionLanguageScorer.RetrieveTexts(profile, image, text, 3);
        var imageRetrieval = VisionLanguageScorer.RetrieveImages(profile, image, text, 0, 1);
        if (classification.Classification.TopPrediction?.Label != "bus" || textRetrieval[0].Index != 0 || textRetrieval.Select(value => value.Index).Distinct().Count() != 3 || imageRetrieval[0].Index != 0) throw new InvalidOperationException("The zero-shot classification and bidirectional retrieval results did not preserve the audited bus prompt/image order.");
        Console.WriteLine("vlm:top=" + classification.Classification.TopPrediction.Label + ";score=" + classification.Classification.TopPrediction.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";imageEmbeddingSha=" + image.Sha256 + ";textEmbeddingSha=" + text.Sha256);
        Console.WriteLine("DEPLOYSHARP_VISUAL_VLM_EMBEDDING_CONSUMER_OK");
        return 0;
    }

    private static TextTokenBatch ClipPrompts(VisionLanguageEmbeddingProfile profile)
    {
        int[][] tokenRows =
        {
            new[] { 49406, 320, 1125, 539, 320, 2840, 49407 },
            new[] { 49406, 320, 1125, 539, 320, 2533, 49407 },
            new[] { 49406, 320, 1125, 539, 320, 1929, 49407 }
        };
        var ids = Enumerable.Repeat(49407L, 3 * 77).ToArray();
        var mask = new long[ids.Length];
        for (int row = 0; row < tokenRows.Length; row++) for (int column = 0; column < tokenRows[row].Length; column++) { ids[(row * 77) + column] = tokenRows[row][column]; mask[(row * 77) + column] = 1; }
        return new TextTokenBatch(new[] { "a photo of a bus", "a photo of a person", "a photo of a dog" }, ids, 3, 77, profile.Tokenizer.TokenizerId, profile.Tokenizer.Sha256, mask);
    }

    private static string? ExternalFile(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : Path.GetFullPath(value);
    }
}
