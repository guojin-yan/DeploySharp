using System;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage27DocumentUnderstandingCatalogAdmissionTests
    {
        [TestMethod]
        public void OfflineExternalQuerySelectsExactDocumentBundleAndRoundTripsIdentity()
        {
            Identity identity = Exact();
            ValidatedModelCatalog catalog = Catalog(identity, identity, identity);
            var query = new ModelBundleQuery(
                task: "structured-document-extraction",
                family: "donut",
                modelVersion: Version,
                capability: "structured-extraction",
                format: "onnx",
                backend: "onnxruntime",
                precision: "fp32",
                includePreview: true,
                requiredRoles: new[] { "document-encoder", "decoder-prefill", "decoder-with-past" },
                tokenizerId: "naver.donut.cord-v2.xlm-roberta",
                generationMode: "greedy-prefill-past-present",
                kvCacheSchemaId: "donut.mbart.4x16x64.cross1200.v1",
                contextLength: 768,
                pageCount: 1,
                schemaId: "cord-v2.donut-tags.v1",
                ocrOwnership: "ocr-free",
                processorId: "naver.donut.cord-v2.processor");

            ModelBundleSelection selected = ModelCatalogQuery.SelectBundles(catalog, query).Single();
            CollectionAssert.AreEqual(new[] { "document-encoder", "decoder-prefill", "decoder-with-past" }, selected.Artifacts.Select(value => value.BundleRole).ToArray());

            ValidatedModelCatalog roundTrip = ModelCatalogJsonSerializer.Deserialize(ModelCatalogJsonSerializer.Serialize(catalog));
            Assert.IsTrue(roundTrip.Document.Entries.Single().Artifacts.All(value => value.PageCount == 1 && value.ContextLength == 768));
            Assert.IsTrue(roundTrip.Document.Entries.Single().Artifacts.All(value => value.SchemaId == "cord-v2.donut-tags.v1" && value.OcrOwnership == "ocr-free" && value.ProcessorId == "naver.donut.cord-v2.processor"));
            OfficialCatalogAssertions.Excludes(catalog);
        }

        [TestMethod]
        public void BundleRejectsMixedDocumentIdentityAndInvalidOcrOwnership()
        {
            AssertMixed(value => value with { PageCount = 2 });
            AssertMixed(value => value with { ContextLength = 1024 });
            AssertMixed(value => value with { Schema = "other-schema" });
            AssertMixed(value => value with { OcrOwnership = "caller" });
            AssertMixed(value => value with { Processor = "other-processor" });
            AssertMixed(value => value with { KvSchema = "other-kv" });
            AssertMixed(value => value with { Tokenizer = "other-tokenizer" });

            ModelFactoryException invalid = Assert.ThrowsExactly<ModelFactoryException>(() => Catalog(Exact() with { OcrOwnership = "backend" }, Exact(), Exact()));
            Assert.IsTrue(invalid.Diagnostics.Any(value => value.Code == ModelFactoryDiagnosticCodes.CatalogInvalid && value.JsonPath!.EndsWith(".ocrOwnership", StringComparison.Ordinal)));
        }

        private static void AssertMixed(Func<Identity, Identity> mutate)
        {
            ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(Catalog(Exact(), Exact(), mutate(Exact())), Query()));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, exception.Diagnostics.Single().Code);
        }

        private static ModelBundleQuery Query() => new ModelBundleQuery(family: "donut", backend: "onnxruntime", includePreview: true, requiredRoles: new[] { "document-encoder", "decoder-prefill", "decoder-with-past" });

        private static ValidatedModelCatalog Catalog(Identity encoder, Identity prefill, Identity decode)
        {
            var source = new ModelSourceDocument("https://huggingface.co/naver-clova-ix/donut-base-finetuned-cord-v2", "https://github.com/clovaai/donut", "8003d433113256b4ce3a0f5bf604b29ff78a7451", "NAVER CLOVA", null, "MIT", null, false);
            var entry = new ModelCatalogEntry("external/document/donut-cord-v2", "Donut CORD-v2 external document bundle", "donut", "structured-document-extraction", Version, ModelCatalogStatus.External, "External OCR-free Encoder, Prefill, and KV Decode bundle", source, null, new[]
            {
                Artifact("document-encoder", encoder),
                Artifact("decoder-prefill", prefill),
                Artifact("decoder-with-past", decode)
            }, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-document-understanding.md");
            return ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-09T00:00:00Z", "stage27.document.external.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
        }

        private static ModelCatalogArtifact Artifact(string role, Identity identity)
        {
            var conversion = new ModelCatalogConversion("optimum-onnx", "0.1.0-opset17", "8003d433113256b4ce3a0f5bf604b29ff78a7451", "External local evidence; redistribution and download disabled.");
            return new ModelCatalogArtifact(role + ".onnx.fp32", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), conversion, role, Version, new[] { "structured-extraction" }, tokenizerId: identity.Tokenizer, vocabularyMode: "autoregressive", imagePreprocessingId: identity.Processor, language: "multilingual", resolution: "960x1280-thumbnail-pad", visionBackbone: "donut-swin-base", languageModelId: "mbart-4-layer-cord-v2", promptTemplateId: "donut.cord-v2.task-prompt.v1", generationConfigId: "donut.cord-v2.greedy-max768.v1", generationMode: "greedy-prefill-past-present", kvCacheSchemaId: identity.KvSchema, contextLength: identity.ContextLength, pageCount: identity.PageCount, schemaId: identity.Schema, ocrOwnership: identity.OcrOwnership, processorId: identity.Processor);
        }

        private static Identity Exact() => new Identity("naver.donut.cord-v2.xlm-roberta", "naver.donut.cord-v2.processor", "donut.mbart.4x16x64.cross1200.v1", "cord-v2.donut-tags.v1", "ocr-free", 1, 768);
        private const string Version = "donut-cord-v2-8003d433-opset17";
        private sealed record Identity(string Tokenizer, string Processor, string KvSchema, string Schema, string OcrOwnership, int PageCount, int ContextLength);
    }
}
