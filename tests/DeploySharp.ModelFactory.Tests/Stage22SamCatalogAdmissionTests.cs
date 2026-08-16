using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.ModelPack.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage22SamCatalogAdmissionTests
    {
        [TestMethod]
        public void OfflinePreviewQueriesExactPromptableBundleAndOfficialCatalogStaysEmpty()
        {
            using JsonDocument support = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "sam-family-support.json")));
            Assert.AreEqual(5, support.RootElement.GetProperty("bundles").GetArrayLength());
            JsonElement first = support.RootElement.GetProperty("bundles")[0];
            Assert.IsFalse(first.GetProperty("redistributionAllowed").GetBoolean());
            ValidatedModelCatalog catalog = Catalog("vit-b-dca509f", "vit-b-dca509f");

            var query = new ModelBundleQuery(task: "promptable-image-segmentation", family: "sam", modelVersion: "vit-b-dca509f", capability: "mask-feedback", format: "onnx", backend: "onnxruntime", precision: "fp32", includePreview: true, requiredRoles: new[] { "image-encoder", "prompt-mask-decoder" });
            ModelBundleSelection bundle = ModelCatalogQuery.SelectBundles(catalog, query).Single();
            Assert.AreEqual(2, bundle.Artifacts.Count);
            CollectionAssert.AreEquivalent(new[] { "image-encoder", "prompt-mask-decoder" }, bundle.Artifacts.Select(artifact => artifact.BundleRole).ToArray());
            Assert.AreEqual(2, ModelCatalogQuery.Select(catalog, new ModelQuery(family: "sam", includePreview: true, modelVersion: "vit-b-dca509f", capability: "point")).Count);
            OfficialCatalogAssertions.Excludes(catalog);
        }

        [TestMethod]
        public void BundleQueryRejectsMixedVersionsAndValidatorRejectsMissingSidecar()
        {
            ModelFactoryException mixed = Assert.ThrowsExactly<ModelFactoryException>(() => ModelCatalogQuery.SelectBundles(Catalog("vit-b-dca509f", "wrong-version"), Query()));
            Assert.AreEqual(ModelFactoryDiagnosticCodes.BundleInvalid, mixed.Diagnostics.Single().Code);

            ModelCatalogArtifact missing = Artifact("decoder", "vit-b-dca509f", requiredAssetIds: new[] { "decoder-data" });
            ModelFactoryException sidecar = Assert.ThrowsExactly<ModelFactoryException>(() => Validate(new[] { Artifact("image-encoder", "vit-b-dca509f"), missing }));
            Assert.IsTrue(sidecar.Diagnostics.Any(value => value.Code == ModelFactoryDiagnosticCodes.AssetInvalid && value.Message.Contains("sidecar", StringComparison.OrdinalIgnoreCase)));
        }

        private static ModelBundleQuery Query() => new ModelBundleQuery(family: "sam", capability: "point", backend: "onnxruntime", includePreview: true, requiredRoles: new[] { "image-encoder", "prompt-mask-decoder" });
        private static ValidatedModelCatalog Catalog(string encoderVersion, string decoderVersion) => Validate(new[] { Artifact("image-encoder", encoderVersion), Artifact("prompt-mask-decoder", decoderVersion) });

        private static ValidatedModelCatalog Validate(IEnumerable<ModelCatalogArtifact> artifacts)
        {
            var source = new ModelSourceDocument("https://github.com/facebookresearch/segment-anything", null, "dca509fe793f601edb92606367a655c15ac00fdf", "Meta AI", null, "Apache-2.0", null, false);
            var entry = new ModelCatalogEntry("sam/v1/vit-b-image-prompt/external", "SAM ViT-B external bundle", "sam", "promptable-image-segmentation", "vit-b-dca509f", ModelCatalogStatus.External, "External only", source, null, artifacts, Array.Empty<ModelCatalogAsset>(), documentationPath: "articles/visual-sam-family.md");
            return ModelCatalogValidator.Validate(new ModelCatalogDocument("1.0", "2026-08-08T00:00:00Z", "stage22.sam.external.1", new Uri("https://github.com/guojin-yan/DeploySharp"), new[] { entry }));
        }

        private static ModelCatalogArtifact Artifact(string role, string version, IEnumerable<string>? requiredAssetIds = null)
        {
            var conversion = new ModelCatalogConversion("torch.onnx.export", "2.9.1", "dca509fe793f601edb92606367a655c15ac00fdf", "External local export");
            return new ModelCatalogArtifact(role + ".onnx.fp32", "onnx", new[] { "onnxruntime", "openvino" }, "fp32", "none", true, null, Array.Empty<ModelCatalogAsset>(), conversion, role, version, new[] { "point", "box", "mask-feedback", "multimask" }, requiredAssetIds);
        }
    }
}
