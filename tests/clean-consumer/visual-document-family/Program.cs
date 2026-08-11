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
    private static readonly int[] OfficialCompletion = { 57526, 57528, 20220, 38946, 4107, 27587, 40242, 57527, 57566, 33891, 56548, 32557, 57565, 57560, 11817, 53692, 57559, 57530, 3822, 57529, 57532, 42990, 21718, 57531, 57525, 57534, 57536, 42990, 46347, 53692, 57535, 57540, 20017, 35815, 41742, 50934, 57539, 57533, 57544, 57546, 11817, 53692, 57545, 57570, 11817, 53692, 57569, 57556, 51764, 49351, 57555, 57543, 2 };
    private const string ExpectedJson = "{\"menu\":{\"nm\":\"- TICKET CP\",\"num\":\"901016\",\"unitprice\":\"60.000\",\"cnt\":\"2\",\"price\":\"60,000\"},\"sub_total\":{\"subtotal_price\":\"-60.000\",\"tax_price\":\"5,455\"},\"total\":{\"total_price\":\"60.000\",\"emoneyprice\":\"60.000\",\"menuqty_cnt\":\"2.00\"}}";

    private static int Main()
    {
        string? root = ExternalDirectory("DEPLOYSHARP_DOCUMENT_MODEL_ROOT");
        if (root == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_DOCUMENT_FAMILY_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        string image = Path.Combine(root, "evidence", "cord-test-0", "document.png");
        string encoder = Path.Combine(root, "onnx", "encoder_model.onnx");
        string prefill = Path.Combine(root, "onnx", "decoder_model.onnx");
        string decode = Path.Combine(root, "onnx", "decoder_with_past_model.onnx");
        foreach (string path in new[] { image, encoder, prefill, decode, Path.Combine(root, "checkpoint", "sentencepiece.bpe.model"), Path.Combine(root, "checkpoint", "tokenizer.json"), Path.Combine(root, "checkpoint", "added_tokens.json") })
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("DEPLOYSHARP_VISUAL_DOCUMENT_FAMILY_CONSUMER_SKIP missing-external-file");
                return 0;
            }
        }

        DocumentUnderstandingProfile profile = DocumentUnderstandingProfiles.CreateDonutCordV2Onnx();
        BackendId backend = OnnxRuntimeBackendProvider.BackendId;
        var bundle = new DocumentUnderstandingBundle(profile, new[]
        {
            Bind(profile, DocumentArtifactRole.DocumentEncoder, encoder, backend),
            Bind(profile, DocumentArtifactRole.DecoderPrefill, prefill, backend),
            Bind(profile, DocumentArtifactRole.DecoderWithPast, decode, backend)
        });

        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        using var session = new DocumentUnderstandingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
        var tokenizer = new DonutDocumentTokenizer(Path.Combine(root, "checkpoint"), profile.Tokenizer);
        using PreparedDocumentPage page = new OpenCvDocumentUnderstandingInputFactory().CreatePageFromFile(image, profile);
        using var document = new PreparedDocument(profile, new[] { page });
        DocumentEncodedState state = session.SetDocument(document);
        DocumentUnderstandingResult result = session.Generate(DocumentTaskRequest.StructuredExtraction(profile.Schema.SchemaId), tokenizer);

        if (!OfficialCompletion.SequenceEqual(result.Generation.TokenIds)) throw new InvalidOperationException("The real completion token sequence did not match the audited official Predictor sequence.");
        if (result.StructuredOutput.Status != DocumentParseStatus.Success || result.StructuredOutput.Json != ExpectedJson) throw new InvalidOperationException("The real structured result did not match the audited CORD-v2 parse.");
        if (result.KvState.Layers != 4 || result.KvState.CrossTokens != 1200 || result.KvState.SelfTokens != 53) throw new InvalidOperationException("The exact Donut KV contract was not preserved.");

        session.Clear();
        VisualException cleared = ExpectVisual(() => session.Generate(DocumentTaskRequest.StructuredExtraction(profile.Schema.SchemaId), tokenizer));
        if (cleared.ErrorCode != VisualErrorCodes.DocumentUnderstandingStateInvalid) throw new InvalidOperationException("Clear did not produce the stable document-state error.");

        Console.WriteLine("document-family:featureSha=" + state.FeatureSha256 + ";kvSha=" + result.KvState.Sha256 + ";tokens=" + result.Generation.TokenIds.Count + ";parse=" + result.StructuredOutput.Status);
        Console.WriteLine("DEPLOYSHARP_VISUAL_DOCUMENT_FAMILY_CONSUMER_OK");
        return 0;
    }

    private static DocumentArtifactBinding Bind(DocumentUnderstandingProfile profile, DocumentArtifactRole role, string path, BackendId backend) => new DocumentArtifactBinding(role, profile.CreateArtifact(role, path, backend));

    private static VisualException ExpectVisual(Action action)
    {
        try { action(); }
        catch (VisualException exception) { return exception; }
        throw new InvalidOperationException("Expected a VisualException.");
    }

    private static string? ExternalDirectory(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || !Directory.Exists(value) ? null : Path.GetFullPath(value);
    }
}
