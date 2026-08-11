using System;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage25GenerativeVisionLanguageCatalogAdmissionTests
    {
        [TestMethod]
        public void OfflineExternalQueriesCompleteBlipBundleByGenerationIdentity()
        {
            ValidatedModelCatalog catalog = Catalog(Identity(), Identity());
            var query = new ModelBundleQuery(
                task: "image-captioning",
                family: "blip",
                modelVersion: Version,
                capability: "image-captioning",
                format: "onnx",
                backend: "openvino",
                precision: "fp32",
                includePreview: true,
                requiredRoles: new[] { "vision-encoder", "language-decoder" },
                tokenizerId: "bert-base-uncased-blip-dec",
                visionBackbone: "vit-base-patch16-384",
                languageModelId: "bert-lm-head-30524",
                promptTemplateId: "blip-caption-a-picture-of-v1",
                generationConfigId: "blip-caption-greedy-5-20-v1",
                generationMode: "greedy-full-prefix",
                kvCacheSchemaId: "none-full-prefix-v1");

            ModelBundleSelection selected = ModelCatalogQuery.SelectBundles(catalog, query).Single();
            CollectionAssert.AreEqual(new[] { "vision-encoder", "language-decoder" }, selected.Artifacts.Select(value => value.BundleRole).ToArray());

            ValidatedModelCatalog roundTrip = ModelCatalogJsonSerializer.Deserialize(ModelCatalogJsonSerializer.Serialize(catalog));
            Assert.IsTrue(roundTrip.Document.Entries.Single().Artifacts.All(value => value.VisionBackbone == "vit-base-patch16-384" && value.KvCacheSchemaId == "none-full-prefix-v1"));
            Assert.AreEqual(0, OfficialModelCatalog.Load().Document.Entries.Count);
        }

        [TestMethod]
        public void BundleRejectsEveryMixedGenerativeIdentity()
        {
            AssertMixed(value => value with { VisionBackbone = "other-vision" });
            AssertMixed(value => value with { QFormerId = "unexpected-q-former" });
            AssertMixed(value => value with { LanguageModelId = "other-language-model" });
            AssertMixed(value => value with { PromptTemplateId = "other-prompt" });
            AssertMixed(value => value with { GenerationConfigId = "other-generation" });
            AssertMixed(value => value with { GenerationMode = "beam-search" });
            AssertMixed(value => value with { KvCacheSchemaId = "past-present-v1" });
        }

        private static void AssertMixed(Func<GenerationIdentity, GenerationIdentity> mutate)
        {
            ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(Catalog(Identity(), mutate(Identity())), Query()));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, exception.Diagnostics.Single().Code);
        }

        private static ModelBundleQuery Query() => new ModelBundleQuery(family: "blip", backend: "onnxruntime", includePreview: true, requiredRoles: new[] { "vision-encoder", "language-decoder" });

        private static ValidatedModelCatalog Catalog(GenerationIdentity visionIdentity, GenerationIdentity decoderIdentity)
        {
            var artifacts = new[]
            {
                Artifact("vision-encoder", visionIdentity),
                Artifact("language-decoder", decoderIdentity)
            };
            var source = new ModelSourceDocument("https://storage.googleapis.com/sfr-vision-language-research/BLIP/models/model_base_caption_capfilt_large.pth", "https://github.com/salesforce/BLIP", "056a169437371659074aa2732649d5de3bffb4a8", "Salesforce Research", null, "BSD-3-Clause", null, false);
            var entry = new ModelCatalogEntry("external/generative-vlm/blip-caption-base", "BLIP caption base external bundle", "blip", "image-captioning", Version, ModelCatalogStatus.External, "External reproducible split graph bundle", source, null, artifacts, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-generative-vision-language.md");
            return ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-09T00:00:00Z", "stage25.generative-vlm.external.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
        }

        private static ModelCatalogArtifact Artifact(string role, GenerationIdentity identity)
        {
            var conversion = new ModelCatalogConversion("official-blip-plus-torch-onnx", "torch-2.9.1-opset17", "056a169437371659074aa2732649d5de3bffb4a8", "External conversion; redistribution disabled.");
            return new ModelCatalogArtifact(role + ".onnx.fp32", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), conversion, role, Version, new[] { "image-captioning" }, tokenizerId: "bert-base-uncased-blip-dec", vocabularyMode: "autoregressive", visionBackbone: identity.VisionBackbone, qFormerId: identity.QFormerId, languageModelId: identity.LanguageModelId, promptTemplateId: identity.PromptTemplateId, generationConfigId: identity.GenerationConfigId, generationMode: identity.GenerationMode, kvCacheSchemaId: identity.KvCacheSchemaId);
        }

        private static GenerationIdentity Identity() => new GenerationIdentity("vit-base-patch16-384", null, "bert-lm-head-30524", "blip-caption-a-picture-of-v1", "blip-caption-greedy-5-20-v1", "greedy-full-prefix", "none-full-prefix-v1");

        private const string Version = "blip-base-caption-capfilt-large-opset17";

        private sealed record GenerationIdentity(string VisionBackbone, string? QFormerId, string LanguageModelId, string PromptTemplateId, string GenerationConfigId, string GenerationMode, string KvCacheSchemaId);
    }
}
