using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
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
            Assert.IsTrue(PaddleDocumentModelCatalog.Official
                .Where(model => model.ModelId != "paddle-chart/pp-chart2table")
                .All(model => model.Status == PaddleDocumentArtifactStatus.Catalogued));
            Assert.AreEqual(PaddleDocumentArtifactStatus.ConversionBlocked,
                PaddleDocumentModelCatalog.Get("paddle-chart/pp-chart2table").Status);
            CollectionAssert.AreEquivalent(new[] { "paddle-doc/pp-lcnet-x1-0-doc-ori", "paddle-doc/uvdoc", "paddle-table/slanext-wired", "paddle-formula/pp-formulanet-plus-s", "paddle-seal/ppocrv4-mobile", "paddle-chart/pp-chart2table" }, PaddleDocumentModelCatalog.Official.Select(model => model.ModelId).Intersect(new[] { "paddle-doc/pp-lcnet-x1-0-doc-ori", "paddle-doc/uvdoc", "paddle-table/slanext-wired", "paddle-formula/pp-formulanet-plus-s", "paddle-seal/ppocrv4-mobile", "paddle-chart/pp-chart2table" }).ToArray());
        }

        [TestMethod]
        public void PaddleClassificationProfilesUseOfficialResizeShortThenCenterCropContract()
        {
            var profile = PaddleDocumentProfiles.CreateClassification(
                PaddleDocumentModelCatalog.Get("paddle-doc/pp-lcnet-x1-0-doc-ori"),
                PaddleDocumentProfiles.DocumentOrientationLabels,
                VisualTaskId.DocumentOrientation);
            Assert.AreEqual(VisualResizeMode.ShortestEdgeCenterCrop, profile.VisualProfile.Preprocessing!.ResizeMode);
            Assert.AreEqual(new VisualSize(256, 256), profile.VisualProfile.Preprocessing.ShortestEdgeResize);
            Assert.AreEqual(new VisualSize(224, 224), profile.VisualProfile.Preprocessing.ModelSize);
            Assert.AreEqual(VisualNormalizationMode.MeanStandardDeviation, profile.VisualProfile.Preprocessing.Normalization.Mode);
            Assert.AreEqual(VisualResizeMode.Resize, profile.VisualProfile.Preprocessing.WithResizeMode(VisualResizeMode.Resize).ResizeMode);
            Assert.IsNull(profile.VisualProfile.Preprocessing.WithResizeMode(VisualResizeMode.Resize).ShortestEdgeResize);
        }

        [TestMethod]
        public void OfficialLcNetProbabilitiesAreNotSoftmaxedTwice()
        {
            var profile = PaddleDocumentProfiles.CreateClassification(PaddleDocumentModelCatalog.Get("paddle-doc/pp-lcnet-x1-0-doc-ori"), PaddleDocumentProfiles.DocumentOrientationLabels, VisualTaskId.DocumentOrientation);
            using var input = new PreparedVisualInput("x", new Tensor<float>(new TensorShape(1, 3, 224, 224), new float[3 * 224 * 224], TensorBufferOwnership.Transfer), new VisualSize(224, 224), new VisualSize(224, 224), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(224, 224), new VisualSize(224, 224)));
            var outputs = new InferenceOutputs(new[] { new NamedTensor("fetch_name_0", new Tensor<float>(new TensorShape(1, 4), new[] { .02f, .04f, .88f, .06f }, TensorBufferOwnership.Transfer)) });
            var result = (ClassificationResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(2, result.TopPrediction!.Index);
            Assert.AreEqual(.88f, result.TopPrediction.Score, .000001f);
        }

        [TestMethod]
        public void SlanextUsesBgrAspectRatioAndTensorSpacePadding()
        {
            var options = PaddleDocumentProfiles.CreateTableStructure(PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired")).VisualProfile.Preprocessing!;
            Assert.AreEqual(VisualColorOrder.Bgr, options.ColorOrder);
            Assert.AreEqual(VisualResizeMode.LongestSidePadBottomRight, options.ResizeMode);
            Assert.AreEqual(0f, options.NormalizedPaddingValue);
            Assert.AreEqual(0f, options.WithBatchSize(2).WithModelSize(new VisualSize(512, 512)).NormalizedPaddingValue);
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
        public void RegionDetectionProfileSupportsIndependentDynamicBatchRows()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/rt-detr-h-layout-17cls");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateRegionDetection(
                descriptor,
                new[] { "table" },
                VisualTaskId.LayoutDetection,
                modelSize: new VisualSize(8, 8),
                maximumBatch: 2,
                decoderOptions: new DetectionDecoderOptions(scoreThreshold: .1f, maximumCandidates: 2, maximumDetections: 2));

            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0]);
            Assert.AreEqual(2, profile.VisualProfile.Input.MaximumBatch);
            var image = new Tensor<float>(new TensorShape(2, 3, 8, 8), new float[2 * 3 * 8 * 8], TensorBufferOwnership.Transfer);
            var frames = new[]
            {
                new VisualInputFrame(new VisualSize(8, 8), new VisualSize(8, 8), ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), new string('a', 64)),
                new VisualInputFrame(new VisualSize(16, 8), new VisualSize(8, 8), ImageTransform.Letterbox(new VisualSize(16, 8), new VisualSize(8, 8)), new string('b', 64))
            };
            using var input = new PreparedVisualInput("x", image, new VisualSize(8, 8), new VisualSize(8, 8), 2, VisualTensorLayout.Nchw,
                ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), batchFrames: frames);
            var values = new float[2 * 1 * 5];
            values[0] = 0; values[1] = 0; values[2] = 1; values[3] = 1; values[4] = .9f;
            values[5] = 0; values[6] = 0; values[7] = 1; values[8] = 1; values[9] = .8f;
            var outputs = InferenceOutputs.Create("output", new Tensor<float>(new TensorShape(2, 1, 5), values, TensorBufferOwnership.Transfer));

            var batch = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as DetectionBatchResult;
            Assert.IsNotNull(batch);
            Assert.AreEqual(2, batch!.Count);
            Assert.AreEqual(1, batch[0].Detections.Count);
            Assert.AreEqual(1, batch[1].Detections.Count);
            Assert.AreEqual(8f, batch[0].Detections[0].Box.Width, .001f);
            Assert.AreEqual(16f, batch[1].Detections[0].Box.Width, .001f);
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
            SetWinner(structure, classes, 3, 48);
            SetWinner(structure, classes, 4, 6);
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
                Assert.AreEqual("<thead><tr><td></td></tr>", result.Markup);
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
        public void SlanetDecoderPreservesIndependentBatchRowsAndPageMetadata()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired");
            var schema = new PaddleDocumentTableStructureSchema(
                "structure",
                "locations",
                new[] { "<s>", "<td></td>", "<eos>" },
                startTokenIndex: 0,
                endTokenIndex: 2,
                cellTokens: new[] { "<td></td>" });
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateTableStructure(descriptor, schema, modelSize: new VisualSize(8, 8), maximumBatch: 2);
            var image = new Tensor<float>(new TensorShape(2, 3, 8, 8), new float[2 * 3 * 8 * 8], TensorBufferOwnership.Transfer);
            var structure = new float[2 * 3 * 3];
            for (int row = 0; row < 2; row++)
            {
                SetWinner(structure, 3, row * 3, 0);
                SetWinner(structure, 3, row * 3 + 1, 1);
                SetWinner(structure, 3, row * 3 + 2, 2);
            }
            var locations = new float[2 * 3 * 8];
            int rowOneCell = (1 * 3 + 1) * 8;
            locations[rowOneCell + 0] = .5f;
            locations[rowOneCell + 1] = .5f;
            locations[rowOneCell + 2] = .75f;
            locations[rowOneCell + 3] = .5f;
            locations[rowOneCell + 4] = .75f;
            locations[rowOneCell + 5] = .75f;
            locations[rowOneCell + 6] = .5f;
            locations[rowOneCell + 7] = .75f;
            var frames = new[]
            {
                new VisualInputFrame(new VisualSize(8, 8), new VisualSize(8, 8), ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), new string('a', 64)),
                new VisualInputFrame(new VisualSize(8, 8), new VisualSize(8, 8), ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), new string('b', 64))
            };
            using var input = new PreparedVisualInput("x", image, new VisualSize(8, 8), new VisualSize(8, 8), 2, VisualTensorLayout.Nchw,
                ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), inputId: new string('t', 64), batchFrames: frames);
            var outputs = new InferenceOutputs(new[]
            {
                new NamedTensor("locations", new Tensor<float>(new TensorShape(2, 3, 8), locations, TensorBufferOwnership.Transfer)),
                new NamedTensor("structure", new Tensor<float>(new TensorShape(2, 3, 3), structure, TensorBufferOwnership.Transfer))
            });

            object decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            var batch = decoded as PaddleDocumentTableBatchResult ?? throw new AssertFailedException("The SLANeXt decoder did not preserve the batched result.");
            Assert.AreEqual(2, batch.Count);
            Assert.AreEqual(0, batch[0].Metadata.PageIndex);
            Assert.AreEqual(1, batch[1].Metadata.PageIndex);
            Assert.AreEqual(new string('a', 64), batch[0].Metadata.InputSha256);
            Assert.AreEqual(new string('b', 64), batch[1].Metadata.InputSha256);
            Assert.AreEqual("<td></td>", batch[0].Markup);
            Assert.AreEqual("<td></td>", batch[1].Markup);
            Assert.AreEqual(1, batch[0].Regions.Count);
            Assert.AreEqual(1, batch[1].Regions.Count);
            Assert.AreEqual(4f, batch[1].Regions[0].Bounds.X, .01f);
            Assert.AreEqual(2f, batch[1].Regions[0].Bounds.Width, .01f);
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
        [DataRow("pp-formulanet-plus-s", 384, 384)]
        [DataRow("pp-formulanet-plus-m", 384, 384)]
        [DataRow("pp-formulanet-plus-l", 768, 768)]
        [DataRow("pp-formulanet-s", 384, 384)]
        [DataRow("pp-formulanet-l", 768, 768)]
        [DataRow("unimernet", 672, 192)]
        public void FormulaProfileUsesOfficialDimensionsAndSpecializedInput(string name, int width, int height)
        {
            var profile = PaddleDocumentProfiles.CreateFormula(PaddleDocumentModelCatalog.Get("paddle-formula/" + name), new PaddleDocumentFormulaSchema(new[] { "<s>", "x", "</s>" }, 2, 0)).VisualProfile;
            Assert.AreEqual(new TensorShape(1, 1, height, width), profile.Input.ShapePattern);
            Assert.IsNull(profile.Preprocessing, "Official formula preprocessing must not fall back to a simple resize.");
            using var input = new PreparedVisualInput("x", new Tensor<float>(profile.Input.ShapePattern, new float[width * height]), new VisualSize(width, height), new VisualSize(width, height), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(width, height), new VisualSize(width, height)));
            var output = InferenceOutputs.Create("fetch_name_0", new Tensor<long>(new TensorShape(1, 2), new long[] { 0, 1 }));
            var result = (PaddleDocumentFormulaResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, output, CancellationToken.None));
            CollectionAssert.Contains(result.Warnings.ToArray(), "missing-eos:sequence-may-be-truncated");
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
        public void FormulaDecoderPreservesIndependentBatchRowsAndEosBoundaries()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s");
            var schema = new PaddleDocumentFormulaSchema(new[] { "<s>", "a", "b", "</s>" }, 3, 0);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(descriptor, schema, maximumBatch: 2);
            var image = new Tensor<float>(new TensorShape(2, 1, 384, 384), new float[2 * 384 * 384], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("x", image, new VisualSize(384, 384), new VisualSize(384, 384), 2, VisualTensorLayout.Nchw,
                ImageTransform.Resize(new VisualSize(384, 384), new VisualSize(384, 384)), inputId: new string('q', 64));
            var tokens = new Tensor<long>(new TensorShape(2, 5), new long[]
            {
                0, 1, 2, 3, 1,
                0, 2, 3, 1, 1
            }, TensorBufferOwnership.Transfer);
            using (input)
            {
                object decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile,
                    InferenceOutputs.Create("fetch_name_0", tokens), CancellationToken.None));
                var batch = decoded as PaddleDocumentFormulaBatchResult ?? throw new AssertFailedException("The formula decoder did not preserve the batched result.");
                Assert.AreEqual(2, batch.Count);
                Assert.AreEqual("ab", batch[0].Latex);
                Assert.AreEqual("b", batch[1].Latex);
                CollectionAssert.AreEqual(new[] { 1, 2 }, batch[0].TokenIds.ToArray());
                CollectionAssert.AreEqual(new[] { 2 }, batch[1].TokenIds.ToArray());
                Assert.IsFalse(batch[0].Warnings.Contains("missing-eos:sequence-may-be-truncated"));
                Assert.IsFalse(batch[1].Warnings.Contains("missing-eos:sequence-may-be-truncated"));
            }
        }

        [TestMethod]
        public void ChartParsingContractDecodesIntegerTokensWhileKeepingConversionBlockedExplicit()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-chart/pp-chart2table");
            var tokenizer = new PaddleDocumentFormulaTokenPieceTokenizer(new[] { "<s>", "<pad>", "</s>", "<table>", "</table>" });
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateChartParsing(descriptor, tokenizer, new VisualSize(8, 8), endTokenId: 2, startTokenId: 0, padTokenId: 1);
            var image = new Tensor<float>(new TensorShape(1, 3, 8, 8), new float[3 * 8 * 8], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("x", image, new VisualSize(8, 8), new VisualSize(8, 8), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), inputId: new string('h', 64));
            var tokens = new Tensor<long>(new TensorShape(1, 5), new long[] { 0, 3, 4, 2, 3 }, TensorBufferOwnership.Transfer);
            using (input)
            {
                var result = (PaddleDocumentChartResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, InferenceOutputs.Create("fetch_name_0", tokens), CancellationToken.None));
                Assert.AreEqual("<table></table>", result.StructuredData);
                CollectionAssert.AreEqual(new[] { 3, 4 }, result.TokenIds.ToArray());
                Assert.IsFalse(PaddleDocumentModelCatalog.TryGetReleaseArtifact(descriptor.ModelId, out _));
                Assert.ThrowsExactly<InvalidOperationException>(() => profile.CreateArtifact("chart2table.onnx"));
            }
        }

        [TestMethod]
        public void PaddleDocumentProfileCanTargetAConvertedBackendArtifactFormat()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile onnxProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true);

            PaddleDocumentProfile engineProfile = onnxProfile.ForArtifactFormat("tensorrt-engine");
            Assert.AreEqual("onnx", onnxProfile.VisualProfile.ModelFormat);
            Assert.AreEqual("tensorrt-engine", engineProfile.VisualProfile.ModelFormat);
            Assert.AreEqual(onnxProfile.VisualProfile.ProfileId + ".tensorrt-engine", engineProfile.VisualProfile.ProfileId);
            Assert.AreEqual(onnxProfile.VisualProfile.Input.Name, engineProfile.VisualProfile.Input.Name);
            Assert.AreEqual(onnxProfile.VisualProfile.Outputs.Count, engineProfile.VisualProfile.Outputs.Count);
            Assert.AreSame(onnxProfile.VisualProfile.Decoder, engineProfile.VisualProfile.Decoder);
            var profiles = new VisualProfileRegistry();
            profiles.Register(onnxProfile.VisualProfile);
            profiles.Register(engineProfile.VisualProfile);
            Assert.AreEqual(2, profiles.GetProfiles().Count);

            ModelArtifact artifact = engineProfile.CreateArtifact("pp-doclayout-l.engine", new BackendId("tensorrt"));
            Assert.AreEqual("tensorrt-engine", artifact.Format);
            Assert.AreEqual("pp-doclayout-l.engine", artifact.Location);
            Assert.IsNull(artifact.Sha256, "A backend-specific artifact must not inherit the ONNX SHA.");
            Assert.AreEqual(new ModelId("paddle-doc/pp-doclayout-l"), artifact.ModelId);

            const string engineSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            ModelArtifact hashed = engineProfile.CreateArtifactWithSha256("pp-doclayout-l.engine", engineSha, new BackendId("tensorrt"));
            Assert.AreEqual(engineSha, hashed.Sha256);
        }

#if NET8_0 || NET9_0 || NET10_0
        [TestMethod]
        public void FormulaDecoderUsesOfficialBpeTokenizerWhenProvided()
        {
            var vocabulary = new[]
            {
                new KeyValuePair<string, int>("<s>", 0),
                new KeyValuePair<string, int>("<pad>", 1),
                new KeyValuePair<string, int>("</s>", 2),
                new KeyValuePair<string, int>("<unk>", 3),
                new KeyValuePair<string, int>("a", 4),
                new KeyValuePair<string, int>("b", 5),
                new KeyValuePair<string, int>("ab", 6)
            };
            PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromVocabularyAndMerges(vocabulary, new[] { "a b" }, new Dictionary<string, int> { ["<s>"] = 0, ["<pad>"] = 1, ["</s>"] = 2, ["<unk>"] = 3 });
            PaddleDocumentFormulaSchema schema = new PaddleDocumentFormulaSchema(tokenizer, endTokenId: 2, startTokenId: 0, padTokenId: 1, unknownTokenId: 3);
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(descriptor, schema);
            var image = new Tensor<float>(new TensorShape(1, 1, 384, 384), new float[384 * 384], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("x", image, new VisualSize(384, 384), new VisualSize(384, 384), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(384, 384), new VisualSize(384, 384)), inputId: new string('f', 64));
            var tokens = new Tensor<long>(new TensorShape(1, 4), new long[] { 0, 4, 5, 2 }, TensorBufferOwnership.Transfer);
            using (input)
            {
                var result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, InferenceOutputs.Create("fetch_name_0", tokens), CancellationToken.None));
                Assert.AreEqual("ab", result.Latex);
                CollectionAssert.AreEqual(new[] { 4, 5 }, result.TokenIds.ToArray());
            }
        }

        [TestMethod]
        public void FormulaTokenizerLoadsOfficialPaddleInferenceYamlWhenPresent()
        {
            const string path = @"E:\Model\PaddleDocument\source\unimernet\UniMERNet_infer\inference.yml";
            if (!System.IO.File.Exists(path)) Assert.Inconclusive("The optional official UniMERNet fixture is not installed.");
            PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(path);
            Assert.IsTrue(tokenizer.Tokens.Count > 45000);
            Assert.AreEqual("<s>", tokenizer.Tokens[0]);
            Assert.AreEqual("</s>", tokenizer.Tokens[2]);
            StringAssert.Contains(tokenizer.Decode(new[] { 23 }), "!");
        }
#endif

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
        public void PaddleNmsProfileAcceptsInt32AndInt64CountExports()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                new[] { "text" },
                new VisualSize(640, 640));

            VisualOutputBinding count = profile.VisualProfile.Outputs.Single(binding => binding.Name == "fetch_name_1");
            Assert.AreEqual(TensorElementType.Int32, count.ElementType);
            CollectionAssert.AreEquivalent(
                new[] { TensorElementType.Int32, TensorElementType.Int64 },
                count.AcceptedElementTypes.ToArray());
            Assert.IsTrue(count.AcceptsElementType(TensorElementType.Int32));
            Assert.IsTrue(count.AcceptsElementType(TensorElementType.Int64));
            Assert.IsFalse(count.AcceptsElementType(TensorElementType.Float32));
        }

        [TestMethod]
        public void PaddleNmsDecoderDecodesInt32CountExport()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(descriptor, new[] { "text" }, new VisualSize(8, 8));
            var image = new Tensor<float>(new TensorShape(1, 3, 8, 8), new float[3 * 8 * 8], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("image", image, new VisualSize(8, 8), new VisualSize(8, 8), 1, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), inputId: new string('i', 64));
            var rows = new Tensor<float>(new TensorShape(1, 1, 6), new float[] { 0, .9f, 1, 1, 4, 4 }, TensorBufferOwnership.Transfer);
            var count = new Tensor<int>(new TensorShape(1), new[] { 1 }, TensorBufferOwnership.Transfer);
            using (input)
            {
                var result = (DetectionResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, new InferenceOutputs(new[] { new NamedTensor("fetch_name_0", rows), new NamedTensor("fetch_name_1", count) }), CancellationToken.None));
                Assert.AreEqual(1, result.Detections.Count);
                Assert.AreEqual("text", result.Detections[0].Label.Label);
            }
        }

        [TestMethod]
        public void PaddleNmsDecoderSupportsBatchedRankThreeOutputWithoutCountTensor()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                new[] { "text" },
                new VisualSize(8, 8),
                countOutputName: null,
                maximumBatch: 2);
            Assert.AreEqual(1, profile.VisualProfile.Outputs.Count);
            var image = new Tensor<float>(new TensorShape(2, 3, 8, 8), new float[2 * 3 * 8 * 8], TensorBufferOwnership.Transfer);
            var rows = new Tensor<float>(new TensorShape(2, 1, 6), new float[]
            {
                0, .9f, 1, 1, 4, 4,
                0, .8f, 2, 2, 5, 5
            }, TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("image", image, new VisualSize(8, 8), new VisualSize(8, 8), 2, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), inputId: new string('b', 64));
            using (input)
            {
                object decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, InferenceOutputs.Create("fetch_name_0", rows), CancellationToken.None));
                var batch = decoded as DetectionBatchResult ?? throw new AssertFailedException("The decoder did not preserve the batched result.");
                Assert.AreEqual(2, batch.Count);
                Assert.AreEqual(1, batch[0].Detections.Count);
                Assert.AreEqual(1, batch[1].Detections.Count);
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

        [TestMethod]
        public void SealDecoderPreservesIndependentBatchRowsAndPageMetadata()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-mobile");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(descriptor, new VisualSize(8, 8), maximumBatch: 2, threshold: .5f, minimumArea: 2);
            var image = new Tensor<float>(new TensorShape(2, 3, 8, 8), new float[2 * 3 * 8 * 8], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("x", image, new VisualSize(8, 8), new VisualSize(8, 8), 2, VisualTensorLayout.Nchw,
                ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), inputId: new string('s', 64));
            var mask = new float[2 * 8 * 8];
            mask[(2 * 8) + 2] = .9f; mask[(2 * 8) + 3] = .8f; mask[(3 * 8) + 2] = .7f; mask[(3 * 8) + 3] = .6f;
            int second = 8 * 8;
            mask[second + (5 * 8) + 5] = .95f; mask[second + (5 * 8) + 6] = .85f; mask[second + (6 * 8) + 5] = .75f; mask[second + (6 * 8) + 6] = .65f;
            using (input)
            {
                object decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile,
                    InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(2, 1, 8, 8), mask, TensorBufferOwnership.Transfer)), CancellationToken.None));
                var batch = decoded as PaddleDocumentSealBatchResult ?? throw new AssertFailedException("The seal decoder did not preserve the batched result.");
                Assert.AreEqual(2, batch.Count);
                Assert.AreEqual(0, batch[0].Metadata.PageIndex);
                Assert.AreEqual(1, batch[1].Metadata.PageIndex);
                Assert.AreEqual(1, batch[0].Regions.Count);
                Assert.AreEqual(1, batch[1].Regions.Count);
                Assert.AreEqual(2f, batch[0].Regions[0].Bounds.X, .01f);
                Assert.AreEqual(5f, batch[1].Regions[0].Bounds.X, .01f);
            }
        }

        [TestMethod]
        public void UnwarpingDecoderPreservesIndependentBatchRowsAndPixels()
        {
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateUnwarping(descriptor, new VisualSize(8, 8), maximumBatch: 2);
            var image = new Tensor<float>(new TensorShape(2, 3, 8, 8), new float[2 * 3 * 8 * 8], TensorBufferOwnership.Transfer);
            var input = new PreparedVisualInput("image", image, new VisualSize(8, 8), new VisualSize(8, 8), 2, VisualTensorLayout.Nchw,
                ImageTransform.Resize(new VisualSize(8, 8), new VisualSize(8, 8)), inputId: new string('u', 64));
            var pixels = new float[2 * 3 * 2 * 2];
            pixels[0] = .25f;
            pixels[12] = .75f;
            using (input)
            {
                object decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile,
                    InferenceOutputs.Create("fetch_name_0", new Tensor<float>(new TensorShape(2, 3, 2, 2), pixels, TensorBufferOwnership.Transfer)), CancellationToken.None));
                var batch = decoded as PaddleDocumentUnwarpingBatchResult ?? throw new AssertFailedException("The UVDoc decoder did not preserve the batched result.");
                Assert.AreEqual(2, batch.Count);
                Assert.AreEqual(0, batch[0].Metadata.PageIndex);
                Assert.AreEqual(1, batch[1].Metadata.PageIndex);
                Assert.AreEqual(.25f, batch[0].Pixels[0], .000001f);
                Assert.AreEqual(.75f, batch[1].Pixels[0], .000001f);
                Assert.AreEqual(12, batch[0].Pixels.Count);
                Assert.AreEqual(12, batch[1].Pixels.Count);
            }
        }

        [TestMethod]
        public void PaddleGeometryMatchesModelCanvasAndOfficialDetrNormalization()
        {
            var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"), PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640), includeGeometryInputs: true, maximumBatch: 2);
            var inputs = new InferenceInputs(profile.CreateGeometryInputs(2));
            CollectionAssert.AreEqual(new[] { 640f, 640f, 640f, 640f }, (float[])inputs.GetRequired("im_shape").Buffer);
            CollectionAssert.AreEqual(new[] { 1f, 1f, 1f, 1f }, (float[])inputs.GetRequired("scale_factor").Buffer);
            Assert.AreEqual(23, profile.VisualProfile.Labels.Count);
            Assert.AreEqual(VisualNormalizationMode.Scale, profile.VisualProfile.Preprocessing!.Normalization.Mode);
            Assert.AreEqual(255f, profile.VisualProfile.Preprocessing.Normalization.InputDivisors[0]);
            Assert.AreEqual(VisualInterpolationMode.Cubic, profile.VisualProfile.Preprocessing.Interpolation);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => profile.CreateGeometryInputs(3));
        }

        [TestMethod]
        public void PaddleGeometryFromPreparedInputKeepsModelSpaceForNonSquareSource()
        {
            var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"),
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true);
            using var input = new PreparedVisualInput(
                "image",
                new Tensor<float>(new TensorShape(1, 3, 640, 640), new float[3 * 640 * 640], TensorBufferOwnership.Transfer),
                new VisualSize(810, 1080),
                new VisualSize(640, 640),
                1,
                VisualTensorLayout.Nchw,
                ImageTransform.Resize(new VisualSize(810, 1080), new VisualSize(640, 640)));

            var inputs = new InferenceInputs(profile.CreateGeometryInputs(input));
            CollectionAssert.AreEqual(new[] { 640f, 640f }, (float[])inputs.GetRequired("im_shape").Buffer);
            CollectionAssert.AreEqual(new[] { 1f, 1f }, (float[])inputs.GetRequired("scale_factor").Buffer);
            Assert.AreEqual(640f / 810f, input.Transform.ScaleX, .000001f);
            Assert.AreEqual(640f / 1080f, input.Transform.ScaleY, .000001f);
        }

        [TestMethod]
        public void PaddleNmsRaggedBatchUsesPerImageCountsWithoutEqualRowSplitting()
        {
            var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"), new[] { "text" }, new VisualSize(8, 8), maximumBatch: 2);
            using var input = new PreparedVisualInput("image", new Tensor<float>(new TensorShape(2, 3, 8, 8), new float[384]),
                new VisualSize(16, 16), new VisualSize(8, 8), 2, VisualTensorLayout.Nchw, ImageTransform.Resize(new VisualSize(16, 16), new VisualSize(8, 8)));
            var rows = new Tensor<float>(new TensorShape(3, 6), new[] { 0f, .9f, 1, 1, 3, 3, 0, .8f, 2, 2, 4, 4, 0, .7f, 3, 3, 5, 5 });
            var outputs = new InferenceOutputs(new[] { new NamedTensor("fetch_name_0", rows), new NamedTensor("fetch_name_1", new Tensor<int>(new TensorShape(2), new[] { 1, 2 })) });
            var result = (DetectionBatchResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.AreEqual(1, result[0].Detections.Count);
            Assert.AreEqual(2, result[1].Detections.Count);
            Assert.AreEqual(.8f, result[1].Detections[0].Label.Score);
            Assert.AreEqual(4f, result[1].Detections[0].Box.X);
            var invalid = new InferenceOutputs(new[] { new NamedTensor("fetch_name_0", rows), new NamedTensor("fetch_name_1", new Tensor<int>(new TensorShape(2), new[] { 2, 2 })) });
            Assert.ThrowsExactly<VisualException>(() => profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, invalid, CancellationToken.None)));
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
