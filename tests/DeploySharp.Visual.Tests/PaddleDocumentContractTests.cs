using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PaddleDocumentContractTests
    {
        [TestMethod]
        public void OfficialCatalogCoversStructureModulesWithoutClaimingRuntimeSupport()
        {
            Assert.IsTrue(PaddleDocumentModelCatalog.Official.Count >= 28);
            Assert.IsTrue(Enum.GetValues<PaddleDocumentModule>().All(module => module == PaddleDocumentModule.StructurePipeline || PaddleDocumentModelCatalog.Official.Any(model => model.Module == module)));
            Assert.IsTrue(PaddleDocumentModelCatalog.Official.All(model => model.Status == PaddleDocumentArtifactStatus.Catalogued));
            CollectionAssert.AreEquivalent(new[] { "paddle-doc/pp-lcnet-x1-0-doc-ori", "paddle-doc/uvdoc", "paddle-table/slanext-wired", "paddle-formula/pp-formulanet-plus-s", "paddle-seal/ppocrv4-mobile", "paddle-chart/pp-chart2table" }, PaddleDocumentModelCatalog.Official.Select(model => model.ModelId).Intersect(new[] { "paddle-doc/pp-lcnet-x1-0-doc-ori", "paddle-doc/uvdoc", "paddle-table/slanext-wired", "paddle-formula/pp-formulanet-plus-s", "paddle-seal/ppocrv4-mobile", "paddle-chart/pp-chart2table" }).ToArray());
        }

        [TestMethod]
        public void CatalogLookupIsStableAndModuleFiltered()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-m");
            Assert.AreEqual(PaddleDocumentModule.FormulaRecognition, descriptor.Module);
            Assert.IsTrue(PaddleDocumentModelCatalog.ForModule(PaddleDocumentModule.LayoutDetection).Count >= 12);
            Assert.ThrowsExactly<KeyNotFoundException>(() => PaddleDocumentModelCatalog.Get("paddle-document/missing"));
        }

        [TestMethod]
        public void DescriptorValidatesConvertedArtifactEvidence()
        {
            PaddleDocumentModelDescriptor descriptor = new PaddleDocumentModelDescriptor("custom/layout", "Custom Layout", PaddleDocumentModule.LayoutDetection, "https://example.invalid/layout", "onnx", PaddleDocumentArtifactStatus.OnnxConverted, "layout.onnx", new string('A', 64));
            Assert.AreEqual("layout.onnx", descriptor.ArtifactPath);
            Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentModelDescriptor("custom/layout", "Custom Layout", PaddleDocumentModule.LayoutDetection, "https://example.invalid/layout", "onnx", PaddleDocumentArtifactStatus.OnnxConverted));
            Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentModelDescriptor("custom/layout", "Custom Layout", PaddleDocumentModule.LayoutDetection, "https://example.invalid/layout", "onnx", PaddleDocumentArtifactStatus.Catalogued, sha256: "bad"));
        }

        [TestMethod]
        public void SharedResultPreservesGeometryMetadataAndProvenance()
        {
            PaddleDocumentModelDescriptor model = PaddleDocumentModelCatalog.Official[2];
            var metadata = new PaddleDocumentResultMetadata(model, "onnxruntime-cpu", TimeSpan.FromMilliseconds(3), new string('b', 64), 2);
            var region = new PaddleDocumentRegion("table", .9f, new RectangleF(1, 2, 30, 40));
            var result = new TestResult(PaddleDocumentModule.LayoutDetection, metadata, new[] { region }, new[] { "conversion-not-verified" });
            Assert.AreEqual(2, result.Metadata.PageIndex);
            Assert.AreEqual("table", result.Regions[0].Category);
            Assert.AreEqual("conversion-not-verified", result.Warnings[0]);
        }

        [TestMethod]
        public void ReusableDocumentProfilesExposeSpecializedTaskIds()
        {
            PaddleDocumentModelDescriptor orientation = PaddleDocumentModelCatalog.Official[0];
            PaddleDocumentProfile orientationProfile = PaddleDocumentProfiles.CreateClassification(orientation, PaddleDocumentProfiles.DocumentOrientationLabels, VisualTaskId.DocumentOrientation);
            Assert.AreEqual(VisualTaskId.DocumentOrientation, orientationProfile.VisualProfile.Task);
            Assert.AreEqual(4, orientationProfile.VisualProfile.Labels.Count);

            PaddleDocumentModelDescriptor layout = PaddleDocumentModelCatalog.Get("paddle-doc/rt-detr-h-layout-17cls");
            PaddleDocumentProfile layoutProfile = PaddleDocumentProfiles.CreateRegionDetection(layout, PaddleDocumentProfiles.Layout17Labels, VisualTaskId.LayoutDetection);
            Assert.AreEqual(VisualTaskId.LayoutDetection, layoutProfile.VisualProfile.Task);
            Assert.AreEqual(17, layoutProfile.VisualProfile.Labels.Count);
            Assert.AreEqual("paddle-doc/rt-detr-h-layout-17cls", layoutProfile.VisualProfile.ModelId.Value);
        }

        [TestMethod]
        public void SpecializedResultsRejectWrongModuleAndKeepPayload()
        {
            PaddleDocumentModelDescriptor model = PaddleDocumentModelCatalog.Official[0];
            var metadata = new PaddleDocumentResultMetadata(model, "test", TimeSpan.Zero, new string('a', 64));
            var formula = new PaddleDocumentFormulaResult(metadata, "x^2");
            Assert.AreEqual("x^2", formula.Latex);
            Assert.ThrowsExactly<ArgumentException>(() => new PaddleDocumentTableResult(PaddleDocumentModule.ChartParsing, metadata, "<table/>"));
            var orientation = new PaddleDocumentOrientationResult(metadata, "90_degree", 90);
            Assert.AreEqual(90, orientation.RotationDegrees);
        }

        [TestMethod]
        public void SlanetDecoderRestoresStructureTokensAndSourceCellGeometry()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateTableStructure(descriptor);
            const int sequence = 6;
            const int classes = 50;
            var structure = new float[sequence * classes];
            SetWinner(structure, classes, 0, 0);
            SetWinner(structure, classes, 1, 1);
            SetWinner(structure, classes, 2, 5);
            SetWinner(structure, classes, 3, 7);
            SetWinner(structure, classes, 4, 10);
            SetWinner(structure, classes, 5, 49);
            var locations = new float[sequence * 8];
            locations[(3 * 8) + 0] = .1f;
            locations[(3 * 8) + 1] = .1f;
            locations[(3 * 8) + 2] = .2f;
            locations[(3 * 8) + 3] = .1f;
            locations[(3 * 8) + 4] = .2f;
            locations[(3 * 8) + 5] = .2f;
            locations[(3 * 8) + 6] = .1f;
            locations[(3 * 8) + 7] = .2f;
            var image = new Tensor<float>(new TensorShape(1, 3, 512, 512), new float[1 * 3 * 512 * 512], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("x", image, new VisualSize(100, 100), new VisualSize(512, 512), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(100, 100), new VisualSize(512, 512)), inputId: new string('a', 64));
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("fetch_name_0", new Tensor<float>(new TensorShape(1, sequence, 8), locations, TensorBufferOwnership.Transfer)),
                new NamedTensor("fetch_name_1", new Tensor<float>(new TensorShape(1, sequence, classes), structure, TensorBufferOwnership.Transfer))
            });
            using (input)
            {
                var result = (PaddleDocumentTableResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
                Assert.AreEqual("<thead><tr><td></td>", result.Markup);
                Assert.AreEqual(1, result.Regions.Count);
                Assert.AreEqual(10f, result.Regions[0].Bounds.X, .01f);
                Assert.AreEqual(10f, result.Regions[0].Bounds.Y, .01f);
                Assert.AreEqual(10f, result.Regions[0].Bounds.Width, .01f);
                Assert.AreEqual(10f, result.Regions[0].Bounds.Height, .01f);
                Assert.AreEqual(4, result.Tokens.Count);
                Assert.AreEqual(1f, result.AverageScore, .001f);
            }
        }

        [TestMethod]
        public void UvdocDecoderReturnsOwnedCorrectedPixels()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateUnwarping(descriptor, new VisualSize(2, 2));
            var source = new float[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };
            var image = new Tensor<float>(new TensorShape(1, 3, 2, 2), new float[12], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("image", image, new VisualSize(2, 2), new VisualSize(2, 2), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(2, 2), new VisualSize(2, 2)), inputId: new string('b', 64));
            var outputs = InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 3, 2, 2), source, TensorBufferOwnership.Transfer));
            using (input)
            {
                var result = (PaddleDocumentUnwarpingResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
                Assert.AreEqual(12, result.Pixels.Count);
                Assert.AreEqual(10, result.Pixels[0]);
                Assert.AreEqual(30, result.Pixels[2]);
            }
        }

        [TestMethod]
        public void FormulaDecoderStopsAtEosAndPreservesTokenIds()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s");
            var schema = new PaddleDocumentFormulaSchema(new[] { "<s>", "a", "^2", "</s>" }, 3, 0);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(descriptor, schema);
            var image = new Tensor<float>(new TensorShape(1, 1, 384, 384), new float[384 * 384], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("x", image, new VisualSize(384, 384), new VisualSize(384, 384), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(384, 384), new VisualSize(384, 384)), inputId: new string('c', 64));
            var tokens = new Tensor<long>(new TensorShape(1, 5), new long[] { 0, 1, 2, 3, 1 }, TensorBufferOwnership.Transfer);
            using (input)
            {
                var result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, InferenceOutputs.Create("fetch_name_0", tokens), CancellationToken.None));
                Assert.AreEqual("a^2", result.Latex);
                CollectionAssert.AreEqual(new[] { 1, 2 }, result.TokenIds.ToArray());
            }
        }

        [TestMethod]
        public void PaddleNmsDecoderMapsClassScoreAndModelCoordinatesToSource()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(descriptor, new[] { "text", "table" }, new VisualSize(640, 640), includeGeometryInputs: true);
            var image = new Tensor<float>(new TensorShape(1, 3, 640, 640), new float[3 * 640 * 640], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("image", image, new VisualSize(320, 320), new VisualSize(640, 640), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(320, 320), new VisualSize(640, 640)), inputId: new string('d', 64));
            var rows = new Tensor<float>(new TensorShape(1, 2, 6), new float[] { 1, .9f, 100, 120, 300, 320, 0, .2f, 0, 0, 10, 10 }, TensorBufferOwnership.Transfer);
            var count = new Tensor<long>(new TensorShape(1), new long[] { 1 }, TensorBufferOwnership.Transfer);
            var outputs = new InferenceOutputs(new[] { new NamedTensor("fetch_name_0", rows), new NamedTensor("fetch_name_1", count) });
            using (input)
            {
                var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
                Assert.AreEqual(1, result.Detections.Count);
                Assert.AreEqual("table", result.Detections[0].Label.Label);
                Assert.AreEqual(50f, result.Detections[0].Box.X, .01f);
                Assert.AreEqual(100f, result.Detections[0].Box.Width, .01f);
            }
        }

        [TestMethod]
        public void SealDecoderTurnsProbabilityComponentsIntoSourceRegions()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-mobile");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(descriptor, new VisualSize(8, 8), threshold: .5f, minimumArea: 2);
            var image = new Tensor<float>(new TensorShape(1, 3, 8, 8), new float[3 * 8 * 8], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("x", image, new VisualSize(4, 4), new VisualSize(8, 8), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(4, 4), new VisualSize(8, 8)), inputId: new string('e', 64));
            var mask = new float[64]; mask[(2 * 8) + 2] = .9f; mask[(2 * 8) + 3] = .8f; mask[(3 * 8) + 2] = .7f; mask[(3 * 8) + 3] = .6f;
            using (input)
            {
                var result = (PaddleDocumentSealResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(1, 1, 8, 8), mask, TensorBufferOwnership.Transfer)), CancellationToken.None));
                Assert.AreEqual(1, result.Regions.Count);
                Assert.AreEqual(1f, result.Regions[0].Bounds.X, .01f);
                Assert.AreEqual(1f, result.Regions[0].Bounds.Width, .01f);
                Assert.AreEqual("seal", result.Regions[0].Category);
            }
        }

        private static void SetWinner(float[] values, int classes, int step, int selected)
        {
            for (int index = 0; index < classes; index++) values[(step * classes) + index] = .01f;
            values[(step * classes) + selected] = 1f;
        }

        private sealed class TestResult : PaddleDocumentModuleResult
        {
            internal TestResult(PaddleDocumentModule module, PaddleDocumentResultMetadata metadata, PaddleDocumentRegion[] regions, string[] warnings)
                : base(module, metadata, regions, warnings) { }
        }
    }
}
