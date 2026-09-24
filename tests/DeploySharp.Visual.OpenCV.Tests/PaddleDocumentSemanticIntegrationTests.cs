using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>Runs representative PP-Structure exports through an actual DeploySharp decoder, not only a raw ORT graph smoke. / 使用真实 DeploySharp Decoder 运行代表性 PP-Structure 导出，而不是只做原始 ORT 图级 smoke。</summary>
    [TestClass]
    public sealed class PaddleDocumentSemanticIntegrationTests
    {
        private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
        private const string PaddleDocumentSourceRoot = @"E:\Model\PaddleDocument\source";
        private const string ImagePath = @"E:\Data\image\bus.jpg";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void DocumentOrientationDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-lcnet-x1-0-doc-ori");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateClassification(
                descriptor,
                PaddleDocumentProfiles.DocumentOrientationLabels,
                VisualTaskId.DocumentOrientation,
                modelSize: new VisualSize(224, 224));
            ClassificationResult result = Run(profile, Path.Combine(ModelRoot, "pp-lcnet-x1-0-doc-ori.onnx"), Array.Empty<NamedTensor>()) as ClassificationResult
                ?? throw new AssertFailedException("The document orientation decoder returned an unexpected result type.");
            Assert.IsNotNull(result.TopPrediction);
            Assert.IsTrue(result.TopPrediction!.Score >= 0 && result.TopPrediction.Score <= 1);
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=document-orientation;model=" + descriptor.ModelId + ";label=" + result.TopPrediction.Label + ";score=" + result.TopPrediction.Score);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LayoutNmsDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                scoreThreshold: 0);
            var auxiliary = profile.CreateGeometryInputs();
            DetectionResult result = Run(profile, Path.Combine(ModelRoot, "pp-doclayout-l.onnx"), auxiliary) as DetectionResult
                ?? throw new AssertFailedException("The layout decoder returned an unexpected result type.");
            Assert.IsNotNull(result.Detections);
            foreach (Detection detection in result.Detections)
            {
                Assert.IsTrue(detection.Label.Score >= 0 && detection.Label.Score <= 1);
                Assert.IsTrue(detection.Box.X >= 0 && detection.Box.Y >= 0);
            }
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=layout;model=" + descriptor.ModelId + ";detections=" + result.Detections.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void TableClassificationDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateClassification(
                descriptor,
                new[] { "wired", "wireless" },
                VisualTaskId.TableClassification,
                modelSize: new VisualSize(224, 224));
            ClassificationResult result = Run(profile, Path.Combine(ModelRoot, "pp-lcnet-x1-0-table-cls.onnx"), Array.Empty<NamedTensor>()) as ClassificationResult
                ?? throw new AssertFailedException("The table-classification decoder returned an unexpected result type.");
            Assert.IsNotNull(result.TopPrediction);
            Assert.IsTrue(result.TopPrediction!.Score >= 0 && result.TopPrediction.Score <= 1);
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=table-classification;model=" + descriptor.ModelId + ";label=" + result.TopPrediction.Label + ";score=" + result.TopPrediction.Score);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void TableCellNmsDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-table/rt-detr-l-wired-cell-det");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                new[] { "table-cell" },
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                scoreThreshold: 0);
            DetectionResult result = Run(profile, Path.Combine(ModelRoot, "rt-detr-l-wired-cell-det.onnx"), profile.CreateGeometryInputs()) as DetectionResult
                ?? throw new AssertFailedException("The table-cell decoder returned an unexpected result type.");
            Assert.IsNotNull(result.Detections);
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=table-cell;model=" + descriptor.ModelId + ";detections=" + result.Detections.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void UnwarpingDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateUnwarping(descriptor, new VisualSize(640, 640));
            PaddleDocumentUnwarpingResult result = Run(profile, Path.Combine(ModelRoot, "uvdoc.onnx"), Array.Empty<NamedTensor>()) as PaddleDocumentUnwarpingResult
                ?? throw new AssertFailedException("The UVDoc decoder returned an unexpected result type.");
            Assert.AreEqual(3, result.Channels);
            Assert.AreEqual(result.Width * result.Height * result.Channels, result.Pixels.Count);
            Assert.IsTrue(result.Pixels.All(value => !float.IsNaN(value) && !float.IsInfinity(value)));
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=unwarping;model=" + descriptor.ModelId + ";shape=" + result.Width + "x" + result.Height + "x" + result.Channels);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void TableStructureDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateTableStructure(descriptor, modelSize: new VisualSize(512, 512));
            PaddleDocumentTableResult result = Run(profile, Path.Combine(ModelRoot, "slanext-wired.onnx"), Array.Empty<NamedTensor>(), @"E:\Model\PaddleDocument\validation\table_recognition.jpg") as PaddleDocumentTableResult
                ?? throw new AssertFailedException("The SLANeXt decoder returned an unexpected result type.");
            Assert.IsNotNull(result.Markup);
            Assert.IsTrue(result.Tokens.Count > 10, "A real table must produce more than an empty structure.");
            Assert.AreEqual(13, result.Regions.Count, "Official example has one colspan header and twelve ordinary cells.");
            Assert.IsTrue(result.AverageScore >= 0 && result.AverageScore <= 1);
            foreach (PaddleDocumentRegion cell in result.Regions)
            {
                Assert.IsTrue(cell.Bounds.X >= 0 && cell.Bounds.Y >= 0);
                Assert.IsTrue(cell.Bounds.Width >= 0 && cell.Bounds.Height >= 0);
            }
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=table-structure;model=" + descriptor.ModelId + ";tokens=" + result.Tokens.Count + ";cells=" + result.Regions.Count + ";markup=" + result.Markup);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void WirelessTableStructureDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-table/slanext-wireless");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateTableStructure(descriptor, modelSize: new VisualSize(512, 512));
            PaddleDocumentTableResult result = Run(profile, Path.Combine(ModelRoot, "slanext-wireless.onnx"), Array.Empty<NamedTensor>(), @"E:\Model\PaddleDocument\validation\table_recognition.jpg") as PaddleDocumentTableResult
                ?? throw new AssertFailedException("The wireless SLANeXt decoder returned an unexpected result type.");
            Assert.IsNotNull(result.Markup);
            Assert.IsTrue(result.AverageScore >= 0 && result.AverageScore <= 1);
            Assert.IsTrue(result.Tokens.Count > 0);
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=table-structure;model=" + descriptor.ModelId + ";tokens=" + result.Tokens.Count + ";cells=" + result.Regions.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void SealDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-mobile");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(descriptor, new VisualSize(224, 224));
            PaddleDocumentSealResult result = Run(profile, Path.Combine(ModelRoot, "ppocrv4-mobile-seal-det.onnx"), Array.Empty<NamedTensor>()) as PaddleDocumentSealResult
                ?? throw new AssertFailedException("The seal decoder returned an unexpected result type.");
            Assert.IsTrue(result.MaskWidth > 0 && result.MaskHeight > 0);
            foreach (PaddleDocumentRegion region in result.Regions)
            {
                Assert.IsTrue(region.Score >= 0 && region.Score <= 1);
                Assert.IsTrue(region.Bounds.Width >= 0 && region.Bounds.Height >= 0);
            }
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=seal;model=" + descriptor.ModelId + ";regions=" + result.Regions.Count + ";mask=" + result.MaskWidth + "x" + result.MaskHeight);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void ServerSealDecoderRunsOnRealOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-server");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(descriptor, new VisualSize(224, 224));
            PaddleDocumentSealResult result = Run(profile, Path.Combine(ModelRoot, "ppocrv4-server-seal-det.onnx"), Array.Empty<NamedTensor>()) as PaddleDocumentSealResult
                ?? throw new AssertFailedException("The server seal decoder returned an unexpected result type.");
            Assert.IsTrue(result.MaskWidth > 0 && result.MaskHeight > 0);
            foreach (PaddleDocumentRegion region in result.Regions)
            {
                Assert.IsTrue(region.Score >= 0 && region.Score <= 1);
                Assert.IsTrue(region.Bounds.Width >= 0 && region.Bounds.Height >= 0);
            }
            Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=seal;model=" + descriptor.ModelId + ";regions=" + result.Regions.Count + ";mask=" + result.MaskWidth + "x" + result.MaskHeight);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void SealDecoderRunsAcrossThreeLocalImagesOnOrtCpu()
        {
            RequireExternal();
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-mobile");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(descriptor, new VisualSize(224, 224));
            foreach (string image in new[] { @"E:\Data\ocr\demo_1.jpg", @"E:\Data\ocr\demo_2.jpg", @"E:\Data\ocr\demo_3.jpg" })
            {
                if (!File.Exists(image)) Assert.Inconclusive("Missing seal multi-image sample: " + image);
                PaddleDocumentSealResult result = Run(profile, Path.Combine(ModelRoot, "ppocrv4-mobile-seal-det.onnx"), Array.Empty<NamedTensor>(), image) as PaddleDocumentSealResult
                    ?? throw new AssertFailedException("The seal decoder returned an unexpected result type.");
                Assert.IsTrue(result.MaskWidth > 0 && result.MaskHeight > 0);
                Assert.IsTrue(result.Regions.All(region => region.Score >= 0 && region.Score <= 1));
                Console.WriteLine("PADDLE_DOCUMENT_SEAL_MULTI_IMAGE image=" + image + ";regions=" + result.Regions.Count + ";mask=" + result.MaskWidth + "x" + result.MaskHeight);
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AllLocalLayoutAndWirelessCellDecodersRunOnRealOrtCpu()
        {
            RequireExternal();
            var cases = new[]
            {
                new LayoutCase("paddle-doc/pp-doclayout-plus-l", "pp-doclayout-plus-l.onnx", new VisualSize(800, 800), PaddleDocumentProfiles.LayoutPlusLabels, true),
                new LayoutCase("paddle-doc/pp-doclayout-m", "pp-doclayout-m.onnx", new VisualSize(640, 640), PaddleDocumentProfiles.Layout23Labels, false),
                new LayoutCase("paddle-doc/pp-doclayout-s", "pp-doclayout-s.onnx", new VisualSize(480, 480), PaddleDocumentProfiles.Layout23Labels, false),
                new LayoutCase("paddle-doc/pp-docblocklayout", "pp-docblocklayout.onnx", new VisualSize(640, 640), PaddleDocumentProfiles.RegionLabels, true),
                new LayoutCase("paddle-doc/picodet-layout-1x", "picodet-layout-1x.onnx", new VisualSize(608, 800), PaddleDocumentProfiles.Layout5Labels, false),
                new LayoutCase("paddle-doc/picodet-layout-1x-table", "picodet-layout-1x-table.onnx", new VisualSize(608, 800), PaddleDocumentProfiles.TableOnlyLabels, false),
                new LayoutCase("paddle-doc/picodet-s-layout-3cls", "picodet-s-layout-3cls.onnx", new VisualSize(480, 480), PaddleDocumentProfiles.Layout3Labels, false),
                new LayoutCase("paddle-doc/picodet-l-layout-3cls", "picodet-l-layout-3cls.onnx", new VisualSize(640, 640), PaddleDocumentProfiles.Layout3Labels, false),
                new LayoutCase("paddle-doc/rt-detr-h-layout-3cls", "rt-detr-h-layout-3cls.onnx", new VisualSize(640, 640), PaddleDocumentProfiles.Layout3Labels, true),
                new LayoutCase("paddle-doc/picodet-s-layout-17cls", "picodet-s-layout-17cls.onnx", new VisualSize(480, 480), PaddleDocumentProfiles.Layout17Labels, false),
                new LayoutCase("paddle-doc/picodet-l-layout-17cls", "picodet-l-layout-17cls.onnx", new VisualSize(640, 640), PaddleDocumentProfiles.Layout17Labels, false),
                new LayoutCase("paddle-doc/rt-detr-h-layout-17cls", "rt-detr-h-layout-17cls.onnx", new VisualSize(640, 640), PaddleDocumentProfiles.Layout17Labels, true),
                new LayoutCase("paddle-table/rt-detr-l-wireless-cell-det", "rt-detr-l-wireless-cell-det.onnx", new VisualSize(640, 640), new[] { "table-cell" }, true)
            };

            foreach (LayoutCase item in cases)
            {
                PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(item.ModelId);
                PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                    descriptor,
                    item.Labels,
                    item.ModelSize,
                    includeImageShapeInput: item.IncludeImageShape,
                    includeScaleFactorInput: true,
                    scoreThreshold: 0);
                var auxiliary = profile.CreateGeometryInputs();
                DetectionResult result = Run(profile, Path.Combine(ModelRoot, item.FileName), auxiliary) as DetectionResult
                    ?? throw new AssertFailedException("The layout decoder returned an unexpected result type for " + item.ModelId + ".");
                Assert.IsNotNull(result.Detections);
                foreach (Detection detection in result.Detections)
                {
                    Assert.IsTrue(detection.Label.Score >= 0 && detection.Label.Score <= 1);
                    Assert.IsFalse(float.IsNaN(detection.Box.X) || float.IsNaN(detection.Box.Y));
                }
                Console.WriteLine("PADDLE_DOCUMENT_SEMANTIC module=" + descriptor.Module + ";model=" + item.ModelId + ";detections=" + result.Detections.Count + ";im_shape=" + item.IncludeImageShape);
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void FormulaExportsExposeTheirActualOutputContract()
        {
            RequireExternal();
            foreach (string model in new[] { "pp-formulanet-plus-s", "unimernet" })
            {
                string fileName = model + ".onnx";
                string path = Path.Combine(ModelRoot, fileName);
                if (!File.Exists(path)) { Console.WriteLine("PADDLE_DOCUMENT_FORMULA_AUDIT model=" + model + ";status=missing"); continue; }
                PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/" + model);
                VisualSize size = string.Equals(model, "unimernet", StringComparison.Ordinal) ? new VisualSize(672, 192) : new VisualSize(384, 384);
                PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(
                    descriptor,
                    new PaddleDocumentFormulaSchema(new[] { "<s>", "x", "</s>" }, endTokenId: 2, startTokenId: 0),
                    modelSize: size,
                    maximumSequenceLength: 4096);
                InferenceOutputs outputs = RunRaw(profile, path, Array.Empty<NamedTensor>());
                ITensor tensor = outputs.GetRequired("fetch_name_0");
                string values = tensor.Buffer switch
                {
                    float[] floats => string.Join(",", floats.Length > 8 ? new ArraySegment<float>(floats, 0, 8) : floats),
                    long[] longs => string.Join(",", longs.Length > 8 ? new ArraySegment<long>(longs, 0, 8) : longs),
                    int[] ints => string.Join(",", ints.Length > 8 ? new ArraySegment<int>(ints, 0, 8) : ints),
                    _ => tensor.Buffer.GetType().Name
                };
                Console.WriteLine("PADDLE_DOCUMENT_FORMULA_AUDIT model=" + model + ";elementType=" + tensor.ElementType + ";shape=" + tensor.Shape + ";values=" + values);
                Assert.IsTrue(tensor.Shape.Rank == 2 && tensor.Shape[0] == 1, "Formula export output must remain auditable as a batch sequence-like tensor.");
                Assert.AreEqual(TensorElementType.Int64, tensor.ElementType, "The converted FormulaNet/UniMERNet exports must expose integer token IDs before they can be routed through PaddleDocumentFormulaDecoder.");
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void FormulaExportsDecodeWithOfficialTokenizerOnRealOrtCpu()
        {
            RequireExternal();
            var cases = new[]
            {
                new FormulaCase("pp-formulanet-plus-s", "PP-FormulaNet_plus-S_infer", new VisualSize(384, 384)),
                new FormulaCase("pp-formulanet-plus-m", "PP-FormulaNet_plus-M_infer", new VisualSize(384, 384)),
                new FormulaCase("pp-formulanet-plus-l", "PP-FormulaNet_plus-L_infer", new VisualSize(768, 768)),
                new FormulaCase("pp-formulanet-s", "PP-FormulaNet-S_infer", new VisualSize(384, 384)),
                new FormulaCase("pp-formulanet-l", "PP-FormulaNet-L_infer", new VisualSize(768, 768)),
                new FormulaCase("unimernet", "UniMERNet_infer", new VisualSize(672, 192))
            };
            foreach (FormulaCase formulaCase in cases)
            {
                string model = formulaCase.ModelId;
                string path = Path.Combine(ModelRoot, model + ".onnx");
                if (!File.Exists(path)) { Console.WriteLine("PADDLE_DOCUMENT_FORMULA_SEMANTIC model=" + model + ";status=missing-model"); continue; }
                string yaml = Path.Combine(PaddleDocumentSourceRoot, model, formulaCase.SourceDirectory, "inference.yml");
                if (!File.Exists(yaml)) { Console.WriteLine("PADDLE_DOCUMENT_FORMULA_SEMANTIC model=" + model + ";status=missing-tokenizer"); continue; }

                PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yaml);
                int endTokenId = FindTokenId(tokenizer.Tokens, "</s>");
                int startTokenId = FindTokenId(tokenizer.Tokens, "<s>");
                int padTokenId = FindTokenId(tokenizer.Tokens, "<pad>");
                int unknownTokenId = FindTokenId(tokenizer.Tokens, "<unk>");
                if (endTokenId < 0) Assert.Fail("The official formula tokenizer does not expose </s>: " + yaml);
                PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/" + model);
                PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(
                    descriptor,
                    new PaddleDocumentFormulaSchema(tokenizer, endTokenId, startTokenId, padTokenId, unknownTokenId),
                    modelSize: formulaCase.ModelSize,
                    maximumSequenceLength: 4096);
                string formulaImage = Path.Combine(Path.GetDirectoryName(ModelRoot)!, "validation", "general_formula_rec_001.png");
                Assert.IsTrue(File.Exists(formulaImage), "Acquire the official formula example before running semantic validation.");
                PaddleDocumentFormulaResult result = Run(profile, path, Array.Empty<NamedTensor>(), formulaImage) as PaddleDocumentFormulaResult
                    ?? throw new AssertFailedException("The formula decoder returned an unexpected result type for " + model + ".");
                Assert.IsTrue(result.TokenIds.Count > 0, "The formula model returned no semantic token IDs: " + model);
                Assert.IsNotNull(result.Latex);
                Assert.IsTrue(result.TokenIds.Count < 1000, "The official example must reach EOS before the export's maximum generation length.");
                Assert.IsTrue(result.Latex.Contains("\\frac"), "The official fraction formula was not recovered.");
                if (model.StartsWith("pp-formulanet-plus-", StringComparison.Ordinal))
                {
                    const string expected = @"\zeta_{0}(\nu)=-\frac{\nu\varrho^{-2\nu}}{\pi}\int_{\mu}^{\infty}d\omega\int_{C_{+}}dz\frac{2z^{2}}{(z^{2}+\omega^{2})^{\nu+1}}\breve{\Psi}(\omega;z)e^{i\epsilon z}\quad,";
                    Assert.AreEqual(string.Concat(expected.Where(value => !char.IsWhiteSpace(value))), string.Concat(result.Latex.Where(value => !char.IsWhiteSpace(value))), "Official formula mismatch after removing formatting whitespace.");
                }
                Console.WriteLine("PADDLE_DOCUMENT_FORMULA_SEMANTIC model=" + model + ";tokens=" + result.TokenIds.Count + ";latexLength=" + result.Latex.Length + ";warnings=" + result.Warnings.Count + ";image=general_formula_rec_001.png;latex=" + result.Latex);
            }
        }

        private sealed class LayoutCase
        {
            public LayoutCase(string modelId, string fileName, VisualSize modelSize, IReadOnlyList<string> labels, bool includeImageShape)
            {
                ModelId = modelId; FileName = fileName; ModelSize = modelSize; Labels = labels; IncludeImageShape = includeImageShape;
            }
            public string ModelId { get; }
            public string FileName { get; }
            public VisualSize ModelSize { get; }
            public IReadOnlyList<string> Labels { get; }
            public bool IncludeImageShape { get; }
        }

        private sealed class FormulaCase
        {
            public FormulaCase(string modelId, string sourceDirectory, VisualSize modelSize)
            {
                ModelId = modelId; SourceDirectory = sourceDirectory; ModelSize = modelSize;
            }
            public string ModelId { get; }
            public string SourceDirectory { get; }
            public VisualSize ModelSize { get; }
        }

        private static object Run(PaddleDocumentProfile profile, string path, IReadOnlyList<NamedTensor> auxiliary, string? imagePath = null)
        {
            if (!File.Exists(path)) Assert.Inconclusive("Missing local PP-Structure model: " + path);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            var inputFactory = new OpenCvVisualInputFactory();
            using PreparedVisualInput input = profile.VisualProfile.AuxiliaryInputs.Count == 0
                ? inputFactory.CreateFromFile(imagePath ?? RequireImage(), profile.VisualProfile, inputId: "paddle-document-semantic-smoke")
                : OpenCvPaddleDocumentPreprocessing.CreateFromFile(inputFactory, imagePath ?? RequireImage(), profile, inputId: "paddle-document-semantic-smoke");
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), request);
            var inputs = new List<NamedTensor>(1 + input.AuxiliaryInputs.Count) { new NamedTensor(input.InputName, input.Tensor) };
            inputs.AddRange(input.AuxiliaryInputs);
            InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
            return profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
        }

        private static InferenceOutputs RunRaw(PaddleDocumentProfile profile, string path, IReadOnlyList<NamedTensor> auxiliary)
        {
            if (!File.Exists(path)) Assert.Inconclusive("Missing local PP-Structure model: " + path);
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                RequireImage(), profile.VisualProfile, inputId: "paddle-document-formula-audit");
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), request);
            var inputs = new List<NamedTensor>(1 + input.AuxiliaryInputs.Count) { new NamedTensor(input.InputName, input.Tensor) };
            inputs.AddRange(input.AuxiliaryInputs);
            return session.Run(new InferenceInputs(inputs), CancellationToken.None);
        }

        private static NamedTensor FloatInput(string name, params float[] values)
            => new NamedTensor(name, new Tensor<float>(new TensorShape(1, 2), values, TensorBufferOwnership.Transfer));

        private static int FindTokenId(IReadOnlyList<string> tokens, string token)
        {
            for (int index = 0; index < tokens.Count; index++) if (string.Equals(tokens[index], token, StringComparison.Ordinal)) return index;
            return -1;
        }

        private static string RequireImage()
        {
            if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing semantic smoke input image: " + ImagePath);
            return ImagePath;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1 to run the authorized local PP-Structure semantic smoke.");
        }
    }
}
