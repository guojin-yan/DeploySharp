using System;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage24VisionLanguageCatalogAdmissionTests
    {
        [TestMethod]
        public void OfflinePreviewQueriesCompleteDualEncoderByAllVisionLanguageCapabilities()
        {
            ValidatedModelCatalog catalog = Catalog("official-rgb-bicubic-224", "official-rgb-bicubic-224", "projected-512-v1", "projected-512-v1", "l2-v1", "l2-v1", "clip-candidate-softmax", "clip-candidate-softmax");
            var query = new ModelBundleQuery(task: "vision-language-embedding", family: "clip", modelVersion: "clip-vit-b-32-hf-3d74acf9-opset17", capability: "cross-modal-retrieval", format: "onnx", backend: "openvino", precision: "fp32", includePreview: true, requiredRoles: new[] { "image-encoder", "text-encoder" }, tokenizerId: "openai-clip-bpe-77", vocabularyMode: "dual-encoder", language: "en", resolution: "224x224", scoreSemantics: "clip-candidate-softmax");
            ModelBundleSelection selected = ModelCatalogQuery.SelectBundles(catalog, query).Single();
            CollectionAssert.AreEqual(new[] { "image-encoder", "text-encoder" }, selected.Artifacts.Select(value => value.BundleRole).ToArray());
            Assert.IsTrue(selected.Artifacts.All(value => value.EmbeddingDimension == 512 && value.NormalizationId == "l2-v1"));
            ValidatedModelCatalog roundTrip = ModelCatalogJsonSerializer.Deserialize(ModelCatalogJsonSerializer.Serialize(catalog));
            Assert.IsTrue(roundTrip.Document.Entries.Single().Artifacts.All(value => value.ImagePreprocessingId == "official-rgb-bicubic-224" && value.ScoreSemantics == "clip-candidate-softmax" && value.Language == "en" && value.Resolution == "224x224"));
            Assert.AreEqual(0, ModelCatalogQuery.SelectBundles(catalog, new ModelBundleQuery(family: "clip", includePreview: true, language: "multilingual")).Count);
            OfficialCatalogAssertions.Excludes(catalog);
        }

        [TestMethod]
        public void BundleRejectsMixedPreprocessProjectionNormalizationScoreLanguageAndResolution()
        {
            AssertBundleInvalid(Catalog("image-v1", "image-v2", "projection-v1", "projection-v1", "l2-v1", "l2-v1", "score-v1", "score-v1"));
            AssertBundleInvalid(Catalog("image-v1", "image-v1", "projection-v1", "projection-v2", "l2-v1", "l2-v1", "score-v1", "score-v1"));
            AssertBundleInvalid(Catalog("image-v1", "image-v1", "projection-v1", "projection-v1", "l2-v1", "l2-v2", "score-v1", "score-v1"));
            AssertBundleInvalid(Catalog("image-v1", "image-v1", "projection-v1", "projection-v1", "l2-v1", "l2-v1", "score-v1", "score-v2"));
            AssertBundleInvalid(Catalog("image-v1", "image-v1", "projection-v1", "projection-v1", "l2-v1", "l2-v1", "score-v1", "score-v1", "en", "multilingual"));
            AssertBundleInvalid(Catalog("image-v1", "image-v1", "projection-v1", "projection-v1", "l2-v1", "l2-v1", "score-v1", "score-v1", imageResolution: "224x224", textResolution: "dynamic"));
        }

        private static void AssertBundleInvalid(ValidatedModelCatalog catalog)
        {
            ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(catalog, Query()));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, exception.Diagnostics.Single().Code);
        }

        private static ModelBundleQuery Query() => new ModelBundleQuery(family: "clip", backend: "onnxruntime", includePreview: true, requiredRoles: new[] { "image-encoder", "text-encoder" });

        private static ValidatedModelCatalog Catalog(string imagePreprocess, string textPreprocess, string imageProjection, string textProjection, string imageNormalization, string textNormalization, string imageScore, string textScore, string imageLanguage = "en", string textLanguage = "en", string imageResolution = "224x224", string textResolution = "224x224")
        {
            const string version = "clip-vit-b-32-hf-3d74acf9-opset17";
            var conversion = new ModelCatalogConversion("transformers-official-features-plus-torch-onnx", "4.57.3-torch-2.9.1-opset17", "3d74acf9a28c67741b2f4f2ea7635f0aaf6f0268", "External reproducible conversion; redistribution disabled.");
            var image = new ModelCatalogArtifact("image.onnx.fp32", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), conversion, "image-encoder", version, new[] { "embedding", "zero-shot-classification", "cross-modal-retrieval" }, tokenizerId: "openai-clip-bpe-77", vocabularyMode: "dual-encoder", embeddingDimension: 512, imagePreprocessingId: imagePreprocess, projectionId: imageProjection, normalizationId: imageNormalization, scoreSemantics: imageScore, language: imageLanguage, resolution: imageResolution);
            var text = new ModelCatalogArtifact("text.onnx.fp32", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), conversion, "text-encoder", version, new[] { "embedding", "zero-shot-classification", "cross-modal-retrieval" }, tokenizerId: "openai-clip-bpe-77", vocabularyMode: "dual-encoder", embeddingDimension: 512, imagePreprocessingId: textPreprocess, projectionId: textProjection, normalizationId: textNormalization, scoreSemantics: textScore, language: textLanguage, resolution: textResolution);
            var source = new ModelSourceDocument("https://huggingface.co/openai/clip-vit-base-patch32", "https://github.com/openai/CLIP", "3d74acf9a28c67741b2f4f2ea7635f0aaf6f0268", "OpenAI", null, "MIT", null, false);
            var entry = new ModelCatalogEntry("external/vlm/clip-vit-b-32", "CLIP ViT-B/32 external dual encoder", "clip", "vision-language-embedding", version, ModelCatalogStatus.External, "External split dual encoder", source, null, new[] { image, text }, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-vision-language.md");
            return ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-09T00:00:00Z", "stage24.vision-language.external.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
        }
    }
}
