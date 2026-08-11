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
        string? root = ExternalDirectory("DEPLOYSHARP_NATIVE_VLM_MODEL_ROOT");
        string? image = ExternalFile("DEPLOYSHARP_NATIVE_VLM_IMAGE");
        if (root == null || image == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_NATIVE_VLM_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        string vision = Path.Combine(root, "official-onnx-int8", "vision_encoder.onnx");
        string embedding = Path.Combine(root, "official-onnx-int8", "embed_tokens_int8.onnx");
        string decoder = Path.Combine(root, "official-onnx-int8", "decoder_model_merged_int8.onnx");
        string newline = Path.Combine(root, "evidence", "ocr-demo2", "image_newline.f32");
        foreach (string path in new[] { vision, embedding, decoder, newline, Path.Combine(root, "tokenizer.json"), Path.Combine(root, "vocab.json"), Path.Combine(root, "merges.txt") })
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("DEPLOYSHARP_VISUAL_NATIVE_VLM_CONSUMER_SKIP missing-external-file");
                return 0;
            }
        }

        NativeMultimodalProfile profile = NativeMultimodalProfiles.CreateLlavaOneVisionQwen2HalfB();
        var tokenizer = new Qwen2NativeMultimodalTokenizer(root, profile.Tokenizer);
        BackendId backend = OnnxRuntimeBackendProvider.BackendId;
        var bundle = new NativeMultimodalArtifactBundle(profile, new[]
        {
            Bind(profile, GenerativeVisionLanguageArtifactRole.VisionEncoder, vision, backend),
            Bind(profile, GenerativeVisionLanguageArtifactRole.TokenEmbedding, embedding, backend),
            Bind(profile, GenerativeVisionLanguageArtifactRole.LanguageDecoder, decoder, backend)
        });
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var session = new NativeMultimodalSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"), newline);
        using NativeMultimodalPreparedImage input = new OpenCvNativeMultimodalInputFactory().CreateFromFile(image, profile);
        NativeMultimodalImageState state = session.SetImage(input);
        NativeMultimodalResult vqa = session.Generate(GenerativeVisionLanguageRequest.Question("What languages are visible on this clothing label?"), tokenizer);
        NativeMultimodalResult caption = session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer);
        if (string.IsNullOrWhiteSpace(vqa.Generation.Generation.Text) || string.IsNullOrWhiteSpace(caption.Generation.Generation.Text)) throw new InvalidOperationException("The real VQA or Caption result was empty.");
        if (vqa.KvState.Layers != 24 || caption.KvState.Layers != 24) throw new InvalidOperationException("The exact 24-layer KV contract was not preserved.");
        session.Clear();
        VisualException cleared = ExpectVisual(() => session.Generate(GenerativeVisionLanguageRequest.Caption(), tokenizer));
        if (cleared.ErrorCode != VisualErrorCodes.NativeMultimodalStateInvalid) throw new InvalidOperationException("Clear did not produce the stable native multimodal state error.");

        Console.WriteLine("native-vlm:vqa=" + vqa.Generation.Generation.Text + ";caption=" + caption.Generation.Generation.Text + ";imageStateSha=" + state.FeatureState.ValueSha256 + ";vqaFinish=" + vqa.Generation.Generation.FinishReason + ";captionFinish=" + caption.Generation.Generation.FinishReason);
        Console.WriteLine("DEPLOYSHARP_VISUAL_NATIVE_VLM_CONSUMER_OK");
        return 0;
    }

    private static GenerativeVisionLanguageArtifactBinding Bind(NativeMultimodalProfile profile, GenerativeVisionLanguageArtifactRole role, string path, BackendId backend) => new GenerativeVisionLanguageArtifactBinding(role, profile.CreateArtifact(role, path, backend));

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
