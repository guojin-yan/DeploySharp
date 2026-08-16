using System;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage26NativeMultimodalCatalogAdmissionTests
    {
        [TestMethod]
        public void OfflineExternalQuerySelectsExactSingleImageContextAndKvBundle()
        {
            Identity identity = Exact();
            ValidatedModelCatalog catalog = Catalog(identity, identity, identity);
            var query = new ModelBundleQuery(
                task: "multimodal-dialogue",
                family: "llava-onevision",
                modelVersion: Version,
                capability: "visual-question-answering",
                format: "onnx",
                backend: "openvino",
                precision: "mixed-fp32-int8",
                includePreview: true,
                requiredRoles: new[] { "vision-projector", "token-embedding", "prefill-kv-decoder" },
                tokenizerId: "qwen2-bytelevel-bpe-llava-onevision",
                resolution: "384-anyres-grid-1to6-max9-pack",
                visionBackbone: "siglip-so400m-patch14-384-projector-896",
                languageModelId: "qwen2-0.5b-instruct",
                promptTemplateId: "llava-onevision-single-image-chat-v1",
                generationConfigId: "llava-onevision-greedy-kv-max16-v1",
                generationMode: "greedy-prefill-past-present",
                kvCacheSchemaId: "qwen2-24l-2kvh-d64-past-present-v1",
                imageCount: 1,
                contextLength: 6144);

            ModelBundleSelection selected = ModelCatalogQuery.SelectBundles(catalog, query).Single();
            CollectionAssert.AreEqual(new[] { "vision-projector", "token-embedding", "prefill-kv-decoder" }, selected.Artifacts.Select(value => value.BundleRole).ToArray());
            ValidatedModelCatalog roundTrip = ModelCatalogJsonSerializer.Deserialize(ModelCatalogJsonSerializer.Serialize(catalog));
            Assert.IsTrue(roundTrip.Document.Entries.Single().Artifacts.All(value => value.ImageCount == 1 && value.ContextLength == 6144));
            OfficialCatalogAssertions.Excludes(catalog);
        }

        [TestMethod]
        public void BundleRejectsMixedProcessorImageContextKvAndMissingSidecar()
        {
            AssertMixed(value => value with { Processor = "other-processor" });
            AssertMixed(value => value with { ImageCount = 2 });
            AssertMixed(value => value with { ContextLength = 8192 });
            AssertMixed(value => value with { KvSchema = "other-kv" });
            AssertMixed(value => value with { Tokenizer = "other-tokenizer" });
            ModelFactoryException missing = Assert.ThrowsExactly<ModelFactoryException>(() => Catalog(Exact(), Exact(), Exact(), true));
            Assert.IsTrue(missing.Diagnostics.Any(value => value.Code == ModelFactoryDiagnosticCodes.AssetInvalid));
        }

        private static void AssertMixed(Func<Identity, Identity> mutate)
        {
            ModelFactoryException exception = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(Catalog(Exact(), Exact(), mutate(Exact())), Query()));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, exception.Diagnostics.Single().Code);
        }

        private static ModelBundleQuery Query() => new ModelBundleQuery(family: "llava-onevision", backend: "onnxruntime", includePreview: true, requiredRoles: new[] { "vision-projector", "token-embedding", "prefill-kv-decoder" });

        private static ValidatedModelCatalog Catalog(Identity vision, Identity embedding, Identity decoder, bool missingSidecar = false)
        {
            var source = new ModelSourceDocument("https://huggingface.co/llava-hf/llava-onevision-qwen2-0.5b-ov-hf", "https://github.com/LLaVA-VL/LLaVA-NeXT", "74dd0bf867a4cda7950c17663794267c60cf4b40", "LLaVA-HF and LLaVA-VL", null, "Apache-2.0", null, false);
            var entry = new ModelCatalogEntry("external/native-vlm/llava-onevision-qwen2-0.5b", "LLaVA OneVision Qwen2 0.5B external bundle", "llava-onevision", "multimodal-dialogue", Version, ModelCatalogStatus.External, "External mixed-precision three-graph bundle", source, null, new[]
            {
                Artifact("vision-projector", vision),
                Artifact("token-embedding", embedding),
                Artifact("prefill-kv-decoder", decoder, missingSidecar)
            }, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-native-multimodal.md");
            return ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-09T00:00:00Z", "stage26.native-vlm.external.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
        }

        private static ModelCatalogArtifact Artifact(string role, Identity identity, bool missingSidecar = false)
        {
            var conversion = new ModelCatalogConversion("official-llava-hf-transformers-js-onnx", "opset13-14", "74dd0bf867a4cda7950c17663794267c60cf4b40", "External official repository graph; redistribution disabled.");
            return new ModelCatalogArtifact(role + ".onnx.mixed", "onnx", new[] { "onnxruntime", "openvino" }, "mixed-fp32-int8", "mixed", true, null, Array.Empty<ModelCatalogAsset>(), conversion, role, Version, new[] { "image-captioning", "visual-question-answering" }, missingSidecar ? new[] { "tokenizer-json" } : Array.Empty<string>(), tokenizerId: identity.Tokenizer, vocabularyMode: "autoregressive", embeddingDimension: 896, imagePreprocessingId: identity.Processor, projectionId: "llava-onevision-projector-896", normalizationId: "rgb-minus1-plus1", language: "multilingual", resolution: "384-anyres-grid-1to6-max9-pack", visionBackbone: "siglip-so400m-patch14-384-projector-896", qFormerId: "none", languageModelId: "qwen2-0.5b-instruct", promptTemplateId: "llava-onevision-single-image-chat-v1", generationConfigId: "llava-onevision-greedy-kv-max16-v1", generationMode: "greedy-prefill-past-present", kvCacheSchemaId: identity.KvSchema, imageCount: identity.ImageCount, contextLength: identity.ContextLength);
        }

        private static Identity Exact() => new Identity("qwen2-bytelevel-bpe-llava-onevision", "llava-onevision-slow-pillow-bicubic-anyres-max9-v1", "qwen2-24l-2kvh-d64-past-present-v1", 1, 6144);
        private const string Version = "llava-onevision-qwen2-0.5b-74dd0bf8-onnx-js";
        private sealed record Identity(string Tokenizer, string Processor, string KvSchema, int ImageCount, int ContextLength);
    }
}
