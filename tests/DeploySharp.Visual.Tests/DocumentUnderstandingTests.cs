using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class DocumentUnderstandingTests
    {
        private static readonly BackendId Backend = new BackendId("document-fake");

        [TestMethod]
        public void OfficialProfilesBindOcrPatchSchemaAndExactDonutPorts()
        {
            DocumentUnderstandingProfile donut = DocumentUnderstandingProfiles.CreateDonutCordV2Onnx();
            Assert.IsTrue(donut.Executable); Assert.AreEqual(DocumentOcrOwnership.NoneOcrFree, donut.OcrOwnership); Assert.AreEqual(3, donut.Artifacts.Count); Assert.AreEqual(17, donut.GetArtifact(DocumentArtifactRole.DecoderPrefill).Outputs.Count); Assert.AreEqual(17, donut.GetArtifact(DocumentArtifactRole.DecoderWithPast).Inputs.Count); Assert.AreEqual("past_key_values.3.encoder.value", donut.KvCache!.Past(3, false, false));
            DocumentUnderstandingProfile layout = DocumentUnderstandingProfiles.CreateLayoutLmV3BaseContract();
            Assert.IsFalse(layout.Executable); Assert.AreEqual(DocumentOcrOwnership.Caller, layout.OcrOwnership); Assert.AreEqual(512, layout.Processor.MaximumWords);
            DocumentUnderstandingProfile pix = DocumentUnderstandingProfiles.CreatePix2StructDocVqaContract();
            Assert.IsFalse(pix.Executable); Assert.AreEqual(2048, pix.Processor.MaximumPatches); Assert.AreEqual(16, pix.Processor.PatchSize);
        }

        [TestMethod]
        public void LayoutAndParserRejectInvalidGeometryAlignmentSyntaxAndLimitsWithoutRepair()
        {
            Assert.AreEqual(VisualErrorCodes.DocumentUnderstandingContractInvalid, Assert.ThrowsExactly<VisualException>(() => new DocumentNormalizedBox(0, 0, 0, 5)).ErrorCode);
            var words = new[] { new DocumentWord("total", new DocumentNormalizedBox(1, 2, 10, 20)) };
            Assert.AreEqual(VisualErrorCodes.DocumentUnderstandingContractInvalid, Assert.ThrowsExactly<VisualException>(() => new DocumentLayoutInput(new VisualSize(100, 100), words, new[] { 3 })).ErrorCode);
            DocumentUnderstandingProfile profile = Profile();
            string page = new string('a', 64); string prompt = new string('b', 64);
            DocumentStructuredOutput success = DocumentStructuredOutputParser.Parse(new[] { 4, 2 }, "<s_total><s_price>10.00</s_price></s_total>", profile.Schema, profile.Schema.SchemaId, page, prompt);
            Assert.AreEqual(DocumentParseStatus.Success, success.Status); Assert.AreEqual("{\"total\":{\"price\":\"10.00\"}}", success.Json); Assert.AreEqual(page, success.Nodes[0].Children[0].Provenance.PageIdentity);
            DocumentStructuredOutput invalid = DocumentStructuredOutputParser.Parse(new[] { 4 }, "<s_total><s_price>10", profile.Schema, profile.Schema.SchemaId, page, prompt);
            Assert.AreEqual(DocumentParseStatus.InvalidSyntax, invalid.Status); Assert.IsNull(invalid.Json); Assert.AreEqual("DS-DOCUMENT-TAG-UNCLOSED", invalid.Diagnostic);
            DocumentStructuredOutput mismatch = DocumentStructuredOutputParser.Parse(new[] { 4 }, "<s_x>y</s_x>", profile.Schema, "other", page, prompt);
            Assert.AreEqual(DocumentParseStatus.SchemaMismatch, mismatch.Status);
        }

        [TestMethod]
        public async Task SessionEncodesGeneratesMultiplePromptsOwnsResultsAndRejectsConcurrencyCancellationNaNAndDisposedUse()
        {
            using Fixture fixture = Fixture.Create(TimeSpan.Zero);
            using PreparedDocument document = fixture.Document();
            DocumentEncodedState state = fixture.Session.SetDocument(document);
            CollectionAssert.AreEqual(new long[] { 1, 3, 4 }, state.Shape);
            DocumentUnderstandingResult first = fixture.Session.Generate(DocumentTaskRequest.StructuredExtraction(fixture.Profile.Schema.SchemaId), new FakeTokenizer(fixture.Profile));
            Assert.AreEqual("{\"total\":{\"price\":\"10.00\"}}", first.StructuredOutput.Json); Assert.AreEqual(2, first.Generation.TokenIds.Count); Assert.AreEqual(2, first.KvState.SelfTokens); Assert.IsNotNull(fixture.Session.CurrentKvState);
            DocumentUnderstandingResult second = fixture.Session.Generate(DocumentTaskRequest.StructuredExtraction(fixture.Profile.Schema.SchemaId), new FakeTokenizer(fixture.Profile));
            Assert.AreEqual(first.Generation.Text, second.Generation.Text);
            fixture.Provider.NaNLogits = true;
            Assert.AreEqual(VisualErrorCodes.DocumentUnderstandingGenerationInvalid, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Generate(DocumentTaskRequest.StructuredExtraction(fixture.Profile.Schema.SchemaId), new FakeTokenizer(fixture.Profile))).ErrorCode);
            fixture.Provider.NaNLogits = false;

            using Fixture delayed = Fixture.Create(TimeSpan.FromMilliseconds(100));
            Task<DocumentEncodedState> active = delayed.Session.SetDocumentAsync(delayed.Document());
            await Task.Delay(20);
            Assert.AreEqual(VisualErrorCodes.DocumentUnderstandingConcurrentOperation, Assert.ThrowsExactly<VisualException>(() => delayed.Session.Clear()).ErrorCode);
            await active;
            using (var cancellation = new CancellationTokenSource(20))
            {
                VisualException cancelled = await Assert.ThrowsExactlyAsync<VisualException>(() => delayed.Session.GenerateAsync(DocumentTaskRequest.StructuredExtraction(delayed.Profile.Schema.SchemaId), new FakeTokenizer(delayed.Profile), cancellationToken: cancellation.Token));
                Assert.AreEqual(VisualErrorCodes.Cancelled, cancelled.ErrorCode); Assert.IsNull(delayed.Session.CurrentKvState);
            }
            delayed.Session.Dispose();
            Assert.AreEqual(VisualErrorCodes.ObjectDisposed, Assert.ThrowsExactly<VisualException>(() => delayed.Session.Clear()).ErrorCode);
        }

        private static DocumentUnderstandingProfile Profile()
        {
            var kv = new DocumentKvCacheContract("fake-kv", 2, 1, 2, 3, 16);
            var encoder = Artifact(DocumentArtifactRole.DocumentEncoder, "encoder", new[] { Tensor("pixel_values", TensorElementType.Float32, 1, 3, 2, 2) }, new[] { Tensor("last_hidden_state", TensorElementType.Float32, 1, 3, 4) });
            var prefillOutputs = new List<GenerativeVisionLanguageTensorContract> { Tensor("logits", TensorElementType.Float32, 1, -1, 10) };
            var decodeInputs = new List<GenerativeVisionLanguageTensorContract> { Tensor("input_ids", TensorElementType.Int64, 1, -1) };
            var decodeOutputs = new List<GenerativeVisionLanguageTensorContract> { Tensor("logits", TensorElementType.Float32, 1, 1, 10) };
            for (int layer = 0; layer < 2; layer++)
            {
                prefillOutputs.Add(Tensor(kv.Present(layer, true, true), TensorElementType.Float32, 1, 1, -1, 2)); prefillOutputs.Add(Tensor(kv.Present(layer, true, false), TensorElementType.Float32, 1, 1, -1, 2)); prefillOutputs.Add(Tensor(kv.Present(layer, false, true), TensorElementType.Float32, 1, 1, 3, 2)); prefillOutputs.Add(Tensor(kv.Present(layer, false, false), TensorElementType.Float32, 1, 1, 3, 2));
                decodeInputs.Add(Tensor(kv.Past(layer, true, true), TensorElementType.Float32, 1, 1, -1, 2)); decodeInputs.Add(Tensor(kv.Past(layer, true, false), TensorElementType.Float32, 1, 1, -1, 2)); decodeInputs.Add(Tensor(kv.Past(layer, false, true), TensorElementType.Float32, 1, 1, 3, 2)); decodeInputs.Add(Tensor(kv.Past(layer, false, false), TensorElementType.Float32, 1, 1, 3, 2));
                decodeOutputs.Add(Tensor(kv.Present(layer, true, true), TensorElementType.Float32, 1, 1, -1, 2)); decodeOutputs.Add(Tensor(kv.Present(layer, true, false), TensorElementType.Float32, 1, 1, -1, 2));
            }
            var prefill = Artifact(DocumentArtifactRole.DecoderPrefill, "prefill", new[] { Tensor("input_ids", TensorElementType.Int64, 1, -1), Tensor("encoder_hidden_states", TensorElementType.Float32, 1, 3, 4) }, prefillOutputs);
            var decode = Artifact(DocumentArtifactRole.DecoderWithPast, "decode", decodeInputs, decodeOutputs);
            var processor = new DocumentProcessorContract("fake", new string('1', 64), DocumentProcessorMode.DonutThumbnailPad, new VisualSize(2, 2), new[] { .5f, .5f, .5f }, new[] { .5f, .5f, .5f }, "fake", 1, 1024, 0, 3, 1);
            var tokenizer = new DocumentTokenizerContract("fake", new string('2', 64), new string('3', 64), new string('4', 64), "fake", "fake", "<s_fake>", 10, 0, 1, 2, 3, 16);
            var schema = new DocumentSchemaContract("fake-schema", new string('5', 64), "donut-tags-v1", 8, 32, 1024);
            return new DocumentUnderstandingProfile("document.fake", DocumentUnderstandingFamily.Donut, "fake", "revision", DocumentOcrOwnership.NoneOcrFree, processor, tokenizer, schema, kv, new[] { DocumentUnderstandingTask.StructuredExtraction }, new[] { encoder, prefill, decode }, true);
        }
        private static DocumentArtifactContract Artifact(DocumentArtifactRole role, string id, IEnumerable<GenerativeVisionLanguageTensorContract> inputs, IEnumerable<GenerativeVisionLanguageTensorContract> outputs) => new DocumentArtifactContract(role, new ModelId("external/document/fake/" + id), "onnx", new string((char)('a' + (int)role), 64), 1, 17, inputs, outputs, "revision", "fake", "MIT", "https://example.invalid");
        private static GenerativeVisionLanguageTensorContract Tensor(string name, TensorElementType type, params long[] shape) => new GenerativeVisionLanguageTensorContract(name, type, new TensorShape(shape), 10_000);

        private sealed class FakeTokenizer : IDocumentUnderstandingTokenizer
        {
            private readonly DocumentUnderstandingProfile _profile; internal FakeTokenizer(DocumentUnderstandingProfile profile) { _profile = profile; }
            public string TokenizerId => _profile.Tokenizer.TokenizerId; public string Identity => _profile.Tokenizer.Identity;
            public DocumentTokenSequence Encode(DocumentUnderstandingProfile profile, DocumentTaskRequest request) => new DocumentTokenSequence("<s_fake>", new long[] { 1 }, TokenizerId, Identity);
            public string Decode(IEnumerable<int> tokenIds) => tokenIds.Contains(4) ? "<s_total><s_price>10.00</s_price></s_total>" : string.Empty;
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(DocumentUnderstandingProfile profile, Provider provider, BackendRegistry registry, DocumentUnderstandingSession session) { Profile = profile; Provider = provider; Registry = registry; Session = session; }
            internal DocumentUnderstandingProfile Profile { get; } internal Provider Provider { get; } internal BackendRegistry Registry { get; } internal DocumentUnderstandingSession Session { get; }
            internal static Fixture Create(TimeSpan delay)
            {
                DocumentUnderstandingProfile profile = Profile(); var provider = new Provider(profile, delay); var registry = new BackendRegistry(); registry.Register(provider);
                var bundle = new DocumentUnderstandingBundle(profile, profile.Artifacts.Select(value => new DocumentArtifactBinding(value.Role, profile.CreateArtifact(value.Role, value.Role + ".onnx", Backend))));
                return new Fixture(profile, provider, registry, new DocumentUnderstandingSession(registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, Backend, "cpu")));
            }
            internal PreparedDocument Document()
            {
                var size = new VisualSize(2, 2); var input = new PreparedVisualInput("pixel_values", new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12]), size, size, 1, VisualTensorLayout.Nchw, ImageTransform.Resize(size, size), inputId: new string('9', 64));
                return new PreparedDocument(Profile, new[] { new PreparedDocumentPage(Profile.ProfileId, 0, input) });
            }
            public void Dispose() { Session.Dispose(); Registry.Dispose(); }
        }

        private sealed class Provider : IBackendProvider
        {
            private readonly DocumentUnderstandingProfile _profile; private readonly TimeSpan _delay; internal Provider(DocumentUnderstandingProfile profile, TimeSpan delay) { _profile = profile; _delay = delay; Descriptor = new BackendDescriptor(Backend, "Document fake", "1", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { "onnx" }); }
            public BackendDescriptor Descriptor { get; } internal bool NaNLogits { get; set; }
            public bool CanCreate(ModelArtifact artifact, BackendRequest request) => _profile.Artifacts.Any(value => value.ModelId == artifact.ModelId) && Descriptor.Supports(request.RequiredCapabilities);
            public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
            {
                DocumentArtifactContract contract = _profile.Artifacts.Single(value => value.ModelId == artifact.ModelId); var metadata = new ModelMetadata(contract.ModelId, contract.Format, contract.Inputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)), contract.Outputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)));
                return new FakeSession(metadata, inputs => Run(contract, inputs), _delay);
            }
            public void Dispose() { }
            private InferenceOutputs Run(DocumentArtifactContract contract, InferenceInputs inputs)
            {
                if (contract.Role == DocumentArtifactRole.DocumentEncoder) return InferenceOutputs.Create("last_hidden_state", new Tensor<float>(new TensorShape(1, 3, 4), Enumerable.Repeat(.25f, 12).ToArray()));
                int previous = contract.Role == DocumentArtifactRole.DecoderPrefill ? 0 : checked((int)inputs.GetRequired("past_key_values.0.decoder.key").Shape[2]); int present = previous + 1;
                var logits = Enumerable.Repeat(-10f, 10).ToArray(); logits[previous == 0 ? 4 : 2] = NaNLogits ? float.NaN : 10f;
                var outputs = new List<NamedTensor> { new NamedTensor("logits", new Tensor<float>(new TensorShape(1, 1, 10), logits)) };
                for (int layer = 0; layer < 2; layer++)
                {
                    outputs.Add(new NamedTensor("present." + layer + ".decoder.key", new Tensor<float>(new TensorShape(1, 1, present, 2), new float[present * 2]))); outputs.Add(new NamedTensor("present." + layer + ".decoder.value", new Tensor<float>(new TensorShape(1, 1, present, 2), new float[present * 2])));
                    if (contract.Role == DocumentArtifactRole.DecoderPrefill) { outputs.Add(new NamedTensor("present." + layer + ".encoder.key", new Tensor<float>(new TensorShape(1, 1, 3, 2), new float[6]))); outputs.Add(new NamedTensor("present." + layer + ".encoder.value", new Tensor<float>(new TensorShape(1, 1, 3, 2), new float[6]))); }
                }
                return new InferenceOutputs(outputs);
            }
        }
        private sealed class FakeSession : IInferenceSession
        {
            private readonly Func<InferenceInputs, InferenceOutputs> _run; private readonly TimeSpan _delay; private bool _disposed; internal FakeSession(ModelMetadata metadata, Func<InferenceInputs, InferenceOutputs> run, TimeSpan delay) { Metadata = metadata; _run = run; _delay = delay; }
            public ModelMetadata Metadata { get; } public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken) => RunAsync(inputs, cancellationToken).GetAwaiter().GetResult();
            public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken) { if (_disposed) throw new ObjectDisposedException(nameof(FakeSession)); if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken); return _run(inputs); }
            public void Dispose() { _disposed = true; }
        }
    }
}
