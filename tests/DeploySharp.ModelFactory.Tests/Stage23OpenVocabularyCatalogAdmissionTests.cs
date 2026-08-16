using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage23OpenVocabularyCatalogAdmissionTests
    {
        [TestMethod]
        public void OfflineExternalBundleQueriesFamilyPromptBackendTokenizerAndVocabularyMode()
        {
            ValidatedModelCatalog catalog = Catalog("clip-bpe-77", "clip-bpe-77", "clip-bpe-77", "fixed", "fixed", "fixed");
            var query = new ModelBundleQuery(
                task: "grounded-promptable-segmentation",
                family: "yolo-world-v2-sam-v1",
                modelVersion: "person-bus-vit-b-stage23",
                capability: "grounded-box",
                format: "onnx",
                backend: "openvino",
                precision: "fp32",
                includePreview: true,
                requiredRoles: new[] { "detector", "image-encoder", "prompt-mask-decoder" },
                tokenizerId: "clip-bpe-77",
                vocabularyMode: "fixed");
            ModelBundleSelection selected = ModelCatalogQuery.SelectBundles(catalog, query).Single();
            CollectionAssert.AreEqual(new[] { "detector", "image-encoder", "prompt-mask-decoder" }, selected.Artifacts.Select(value => value.BundleRole).ToArray());
            Assert.IsTrue(selected.Artifacts.All(value => value.TokenizerId == "clip-bpe-77" && value.VocabularyMode == "fixed"));
            string json = ModelCatalogJsonSerializer.Serialize(catalog);
            ValidatedModelCatalog roundTrip = ModelCatalogJsonSerializer.Deserialize(json);
            Assert.AreEqual("clip-bpe-77", roundTrip.Document.Entries.Single().Artifacts.Single(value => value.BundleRole == "detector").TokenizerId);
            OfficialCatalogAssertions.Excludes(catalog);
        }

        [TestMethod]
        public void BundleRejectsMixedTokenizerAndVocabularyMode()
        {
            ModelFactoryException tokenizer = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(Catalog("clip-bpe-77", "bert-base-uncased", "clip-bpe-77", "fixed", "fixed", "fixed"), Query()));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, tokenizer.Diagnostics.Single().Code);
            ModelFactoryException vocabulary = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(Catalog("clip-bpe-77", "clip-bpe-77", "clip-bpe-77", "fixed", "runtime-text", "fixed"), Query()));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, vocabulary.Diagnostics.Single().Code);
        }

        private static ModelBundleQuery Query() => new ModelBundleQuery(family: "yolo-world-v2-sam-v1", capability: "grounded-box", backend: "onnxruntime", includePreview: true, requiredRoles: new[] { "detector", "image-encoder", "prompt-mask-decoder" });

        private static ValidatedModelCatalog Catalog(string detectorTokenizer, string encoderTokenizer, string decoderTokenizer, string detectorVocabulary, string encoderVocabulary, string decoderVocabulary)
        {
            var artifacts = new[]
            {
                Artifact("detector", detectorTokenizer, detectorVocabulary, "Ultralytics 8.2.2", "1110258d379bed8d623068ff7ceda8c9290f0774"),
                Artifact("image-encoder", encoderTokenizer, encoderVocabulary, "torch.onnx.export 2.9.1", "dca509fe793f601edb92606367a655c15ac00fdf"),
                Artifact("prompt-mask-decoder", decoderTokenizer, decoderVocabulary, "torch.onnx.export 2.9.1", "dca509fe793f601edb92606367a655c15ac00fdf")
            };
            var source = new ModelSourceDocument("https://github.com/ultralytics/ultralytics", "https://github.com/facebookresearch/segment-anything", "stage23-external-composition", "Ultralytics and Meta AI", null, "External", null, false);
            var entry = new ModelCatalogEntry("external/grounded-sam/yoloworldv2-sam-v1", "YOLO-Worldv2 plus SAM v1 external bundle", "yolo-world-v2-sam-v1", "grounded-promptable-segmentation", "person-bus-vit-b-stage23", ModelCatalogStatus.External, "External local bundle", source, null, artifacts, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-open-vocabulary.md");
            return ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-08T00:00:00Z", "stage23.open-vocabulary.external.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
        }

        private static ModelCatalogArtifact Artifact(string role, string tokenizer, string vocabularyMode, string exporterVersion, string revision)
        {
            var conversion = new ModelCatalogConversion("official-audited-export", exporterVersion, revision, "External local artifact; redistribution is disabled.");
            return new ModelCatalogArtifact(role + ".onnx.fp32", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), conversion, role, "person-bus-vit-b-stage23", new[] { "grounded-box", "fixed-vocabulary" }, tokenizerId: tokenizer, vocabularyMode: vocabularyMode);
        }
    }
}
