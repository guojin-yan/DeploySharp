using System;
using System.IO;
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
        string? root = ExternalDirectory("DEPLOYSHARP_BLIP_MODEL_ROOT");
        string? image = ExternalFile("DEPLOYSHARP_BLIP_IMAGE");
        if (root == null || image == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_BLIP_FAMILY_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        string vision = Path.Combine(root, "converted-opset17", "vision_encoder.onnx");
        string decoder = Path.Combine(root, "converted-opset17", "text_decoder_full_prefix.onnx");
        string vocabulary = Path.Combine(root, "bert-base-uncased-vocab.txt");
        if (!File.Exists(vision) || !File.Exists(decoder) || !File.Exists(vocabulary))
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_BLIP_FAMILY_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        GenerativeVisionLanguageProfile profile = GenerativeVisionLanguageProfiles.CreateBlipCaptionBase();
        var tokenizer = new BlipBertTokenizer(vocabulary, profile.Tokenizer);
        var backend = OnnxRuntimeBackendProvider.BackendId;
        var bundle = new GenerativeVisionLanguageArtifactBundle(profile, new[]
        {
            new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.VisionEncoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.VisionEncoder, vision, backend)),
            new GenerativeVisionLanguageArtifactBinding(GenerativeVisionLanguageArtifactRole.LanguageDecoder, profile.CreateArtifact(GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoder, backend))
        });
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var session = new GenerativeVisionLanguageSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
        using PreparedVisualInput input = new OpenCvGenerativeVisionLanguageInputFactory().CreateFromFile(image, profile);
        GenerativeVisionLanguageImageState firstState = session.SetImage(input);
        GenerativeVisionLanguageResult first = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
        GenerativeVisionLanguageResult second = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
        if (!string.Equals(first.Generation.Text, second.Generation.Text, StringComparison.Ordinal)) throw new InvalidOperationException("Repeated caption generation was not deterministic.");
        session.ClearImage();
        VisualException cleared = ExpectVisual(() => session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer));
        if (cleared.ErrorCode != VisualErrorCodes.GenerativeVisionLanguageStateInvalid) throw new InvalidOperationException("Clear-image did not produce the stable state error.");
        GenerativeVisionLanguageImageState rebuiltState = session.SetImage(input);
        GenerativeVisionLanguageResult rebuilt = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
        if (!string.Equals(firstState.ValueSha256, rebuiltState.ValueSha256, StringComparison.Ordinal) || !string.Equals(first.Generation.Text, rebuilt.Generation.Text, StringComparison.Ordinal)) throw new InvalidOperationException("Rebuilt image state changed the deterministic caption.");

        Console.WriteLine("blip:caption=" + first.Generation.Text + ";imageStateSha=" + firstState.ValueSha256 + ";finish=" + first.Generation.FinishReason);
        Console.WriteLine("DEPLOYSHARP_VISUAL_BLIP_FAMILY_CONSUMER_OK");
        return 0;
    }

    private static VisualException ExpectVisual(Action action)
    {
        try { action(); }
        catch (VisualException exception) { return exception; }
        throw new InvalidOperationException("Expected a VisualException.");
    }

    private static string? ExternalFile(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : Path.GetFullPath(value);
    }

    private static string? ExternalDirectory(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || !Directory.Exists(value) ? null : Path.GetFullPath(value);
    }
}
