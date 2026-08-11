using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    [TestClass]
    public sealed class Stage27DocumentUnderstandingExternalIntegrationTests
    {
        private static readonly int[] OfficialCompletion = { 57526, 57528, 20220, 38946, 4107, 27587, 40242, 57527, 57566, 33891, 56548, 32557, 57565, 57560, 11817, 53692, 57559, 57530, 3822, 57529, 57532, 42990, 21718, 57531, 57525, 57534, 57536, 42990, 46347, 53692, 57535, 57540, 20017, 35815, 41742, 50934, 57539, 57533, 57544, 57546, 11817, 53692, 57545, 57570, 11817, 53692, 57569, 57556, 51764, 49351, 57555, 57543, 2 };
        private const string ExpectedJson = "{\"menu\":{\"nm\":\"- TICKET CP\",\"num\":\"901016\",\"unitprice\":\"60.000\",\"cnt\":\"2\",\"price\":\"60,000\"},\"sub_total\":{\"subtotal_price\":\"-60.000\",\"tax_price\":\"5,455\"},\"total\":{\"total_price\":\"60.000\",\"emoneyprice\":\"60.000\",\"menuqty_cnt\":\"2.00\"}}";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDonutCordV2MatchesOpenCvOrtOpenVinoAndOfficialPredictor()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_DOCUMENT_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_DOCUMENT_RUN_EXTERNAL=1 to run the Stage 27 document gate.");
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_DOCUMENT_MODEL_ROOT") ?? @"E:\DeploySharp-Models\donut-base-finetuned-cord-v2"; string image = Path.Combine(root, "evidence", "cord-test-0", "document.png");
            Require(image); Require(Path.Combine(root, "checkpoint", "sentencepiece.bpe.model")); foreach (string name in new[] { "encoder_model.onnx", "decoder_model.onnx", "decoder_with_past_model.onnx" }) Require(Path.Combine(root, "onnx", name)); foreach (string name in new[] { "encoder_model.xml", "encoder_model.bin", "decoder_model.xml", "decoder_model.bin", "decoder_with_past_model.xml", "decoder_with_past_model.bin" }) Require(Path.Combine(root, "openvino", name));
            Evidence ort = Run(root, image, false); Evidence openVino = Run(root, image, true);
            CollectionAssert.AreEqual(OfficialCompletion, ort.Result.Generation.TokenIds.ToArray()); CollectionAssert.AreEqual(OfficialCompletion, openVino.Result.Generation.TokenIds.ToArray()); Assert.AreEqual(ExpectedJson, ort.Result.StructuredOutput.Json); Assert.AreEqual(ExpectedJson, openVino.Result.StructuredOutput.Json); Assert.AreEqual(DocumentParseStatus.Success, ort.Result.StructuredOutput.Status); Assert.AreEqual(DocumentParseStatus.Success, openVino.Result.StructuredOutput.Status); Assert.AreEqual(1200, ort.Result.KvState.CrossTokens); Assert.AreEqual(53, ort.Result.KvState.SelfTokens); Assert.AreEqual(4, ort.Result.KvState.Layers);
            string evidencePath = Path.Combine(root, "evidence", "cord-test-0", "deploysharp-dotnet.json");
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
            {
                schemaVersion = "1.0",
                sourceImageSha256 = "8612d04b70f430f3aef07fbbd5200e382dcc4152b344cc2eff9f735f05a257c8",
                officialCompletion = OfficialCompletion,
                structuredJson = ExpectedJson,
                ort = EvidenceJson(ort),
                openvino = EvidenceJson(openVino)
            }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            Console.WriteLine("STAGE27_DOCUMENT_EVIDENCE imageSha=8612d04b70f430f3aef07fbbd5200e382dcc4152b344cc2eff9f735f05a257c8;ortFeatureSha=" + ort.State.FeatureSha256 + ";openVinoFeatureSha=" + openVino.State.FeatureSha256 + ";ortPreprocessMs=" + ort.Result.Timing.Preprocess.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";ortEncoderMs=" + ort.Result.Timing.Encode.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";ortPrefillMs=" + ort.Result.Timing.Prefill.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";ortDecodeMs=" + ort.Result.Timing.DecodeTotal.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";openVinoPreprocessMs=" + openVino.Result.Timing.Preprocess.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";openVinoEncoderMs=" + openVino.Result.Timing.Encode.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";openVinoPrefillMs=" + openVino.Result.Timing.Prefill.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";openVinoDecodeMs=" + openVino.Result.Timing.DecodeTotal.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";tokens=" + string.Join(",", OfficialCompletion));
        }

        private static Evidence Run(string root, string image, bool openVino)
        {
            DocumentUnderstandingProfile profile = openVino ? DocumentUnderstandingProfiles.CreateDonutCordV2OpenVino() : DocumentUnderstandingProfiles.CreateDonutCordV2Onnx(); string directory = Path.Combine(root, openVino ? "openvino" : "onnx"); string extension = openVino ? ".xml" : ".onnx"; BackendId backend = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            using var registry = new BackendRegistry(); if (openVino) registry.UseOpenVino(); else registry.UseOnnxRuntime();
            var bundle = new DocumentUnderstandingBundle(profile, new[] { Bind(profile, DocumentArtifactRole.DocumentEncoder, Path.Combine(directory, "encoder_model" + extension), backend), Bind(profile, DocumentArtifactRole.DecoderPrefill, Path.Combine(directory, "decoder_model" + extension), backend), Bind(profile, DocumentArtifactRole.DecoderWithPast, Path.Combine(directory, "decoder_with_past_model" + extension), backend) });
            using var session = new DocumentUnderstandingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, backend, openVino ? "CPU" : "cpu"));
            var tokenizer = new DonutDocumentTokenizer(Path.Combine(root, "checkpoint"), profile.Tokenizer); using PreparedDocumentPage page = new OpenCvDocumentUnderstandingInputFactory().CreatePageFromFile(image, profile); using var document = new PreparedDocument(profile, new[] { page }); DocumentEncodedState state = session.SetDocument(document); DocumentUnderstandingResult result = session.Generate(DocumentTaskRequest.StructuredExtraction(profile.Schema.SchemaId), tokenizer); session.Clear(); Assert.AreEqual(VisualErrorCodes.DocumentUnderstandingStateInvalid, Assert.ThrowsExactly<VisualException>(() => session.Generate(DocumentTaskRequest.StructuredExtraction(profile.Schema.SchemaId), tokenizer)).ErrorCode); return new Evidence(state, result);
        }
        private static DocumentArtifactBinding Bind(DocumentUnderstandingProfile profile, DocumentArtifactRole role, string path, BackendId backend) => new DocumentArtifactBinding(role, profile.CreateArtifact(role, path, backend));
        private static object EvidenceJson(Evidence evidence) => new { featureSha256 = evidence.State.FeatureSha256, kvSha256 = evidence.Result.KvState.Sha256, tokens = evidence.Result.Generation.TokenIds, rawText = evidence.Result.Generation.Text, parseStatus = evidence.Result.StructuredOutput.Status.ToString(), timingMilliseconds = new { preprocess = evidence.Result.Timing.Preprocess.TotalMilliseconds, encoder = evidence.Result.Timing.Encode.TotalMilliseconds, tokenize = evidence.Result.Timing.Tokenize.TotalMilliseconds, prefill = evidence.Result.Timing.Prefill.TotalMilliseconds, decodeSteps = evidence.Result.Timing.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray(), decodeTotal = evidence.Result.Timing.DecodeTotal.TotalMilliseconds, finalDecode = evidence.Result.Timing.FinalDecode.TotalMilliseconds, parse = evidence.Result.Timing.Parse.TotalMilliseconds } };
        private static void Require(string path) { if (!File.Exists(path) && !Directory.Exists(path)) Assert.Inconclusive("External Stage 27 asset is missing: " + path); }
        private sealed class Evidence { internal Evidence(DocumentEncodedState state, DocumentUnderstandingResult result) { State = state; Result = result; } internal DocumentEncodedState State { get; } internal DocumentUnderstandingResult Result { get; } }
    }
}
