using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
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
    /// <summary>Validates representative PP-Structure decoder contracts on the OpenVINO CPU provider. / 在 OpenVINO CPU Provider 上验证代表性 PP-Structure Decoder 合同。</summary>
    [TestClass]
    public sealed class PaddleDocumentOpenVinoSemanticIntegrationTests
    {
        private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
        private const string ImagePath = @"E:\Data\image\bus.jpg";

        [TestMethod]
        [DataRow("wired")]
        [DataRow("wireless")]
        [TestCategory("ExternalModels")]
        public void AlphaRenamedTableGraphMatchesOriginalOrtOutputs(string variant)
        {
            RequireExternal();
            string? normalizedRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_NORMALIZED_ROOT");
            if (string.IsNullOrWhiteSpace(normalizedRoot)) Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_NORMALIZED_ROOT to alpha-renamed ONNX artifacts.");
            string fileName = "slanext-" + variant;
            string normalizedPath = Path.Combine(normalizedRoot!, fileName + "-local-parameters.onnx");
            if (!File.Exists(normalizedPath)) Assert.Inconclusive("Missing normalized graph: " + normalizedPath);
            var profile = PaddleDocumentProfiles.CreateTableStructure(PaddleDocumentModelCatalog.Get("paddle-table/" + fileName), modelSize: new VisualSize(512, 512));
            using var input = new OpenCvVisualInputFactory().CreateFromFile(@"E:\Model\PaddleDocument\validation\table_recognition.jpg", profile.VisualProfile);
            using var ort = new BackendRegistry().UseOnnxRuntime();
            using var ov = new BackendRegistry().UseOpenVino();
            using var original = ort.CreateSession(profile.CreateArtifact(Path.Combine(ModelRoot, fileName + ".onnx"), OnnxRuntimeBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
            string derivedHash;
            using (var stream = File.OpenRead(normalizedPath))
            using (var hash = System.Security.Cryptography.SHA256.Create()) derivedHash = Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
            using var normalized = ov.CreateSession(new ModelArtifact(profile.VisualProfile.ModelId, "onnx", normalizedPath, derivedHash, OpenVinoBackendProvider.BackendId), new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"));
            var inputs = InferenceInputs.Create(input.InputName, input.Tensor);
            InferenceOutputs expected = original.Run(inputs, CancellationToken.None);
            InferenceOutputs actual = normalized.Run(inputs, CancellationToken.None);
            foreach (NamedTensor output in expected)
            {
                ITensor candidate = actual.GetRequired(output.Name);
                Assert.AreEqual(output.Tensor.Shape, candidate.Shape, output.Name);
                float[] values = (float[])output.Tensor.Buffer;
                float[] other = (float[])candidate.Buffer;
                for (int index = 0; index < values.Length; index++)
                {
                    Assert.IsTrue(float.IsFinite(values[index]) && float.IsFinite(other[index]), "Non-finite table output.");
                    Assert.AreEqual(values[index], other[index], .001f, output.Name + "[" + index + "]");
                }
            }
            var decoded = (PaddleDocumentTableResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, actual, CancellationToken.None));
            var reference = (PaddleDocumentTableResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, expected, CancellationToken.None));
            Assert.IsTrue(decoded.Tokens.Count > 10, "A real table must produce more than an empty structure.");
            Assert.AreEqual(reference.Markup, decoded.Markup);
            CollectionAssert.AreEqual(reference.Tokens.Select(token => token.Index).ToArray(), decoded.Tokens.Select(token => token.Index).ToArray());
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_NORMALIZED model=" + fileName + ";sha256=" + derivedHash + ";floatTolerance=0.001;decoder=passed;image=table_recognition.jpg;tokens=" + decoded.Tokens.Count + ";cells=" + decoded.Regions.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RepresentativeDocumentOrientationLayoutAndUnwarpingRunOnRealOpenVinoCpu()
        {
            RequireExternal();
            var orientation = PaddleDocumentProfiles.CreateClassification(
                PaddleDocumentModelCatalog.Get("paddle-doc/pp-lcnet-x1-0-doc-ori"),
                PaddleDocumentProfiles.DocumentOrientationLabels,
                VisualTaskId.DocumentOrientation,
                modelSize: new VisualSize(224, 224));
            var orientationResult = Run(orientation, Path.Combine(ModelRoot, "pp-lcnet-x1-0-doc-ori.onnx"), Array.Empty<NamedTensor>()) as ClassificationResult;
            Assert.IsNotNull(orientationResult?.TopPrediction);
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_SEMANTIC module=document-orientation;label=" + orientationResult!.TopPrediction!.Label + ";score=" + orientationResult.TopPrediction.Score);

            var layout = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"),
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                scoreThreshold: 0);
            var layoutResult = Run(layout, Path.Combine(ModelRoot, "pp-doclayout-l.onnx"), layout.CreateGeometryInputs()) as DetectionResult;
            Assert.IsNotNull(layoutResult);
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_SEMANTIC module=layout;detections=" + layoutResult!.Detections.Count);

            var uvdoc = PaddleDocumentProfiles.CreateUnwarping(PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc"), new VisualSize(640, 640));
            var uvdocResult = Run(uvdoc, Path.Combine(ModelRoot, "uvdoc.onnx"), Array.Empty<NamedTensor>()) as PaddleDocumentUnwarpingResult;
            Assert.IsNotNull(uvdocResult);
            Assert.IsTrue(uvdocResult!.Pixels.All(value => !float.IsNaN(value) && !float.IsInfinity(value)));
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_SEMANTIC module=unwarping;shape=" + uvdocResult.Width + "x" + uvdocResult.Height + "x" + uvdocResult.Channels);

        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void TableStructureOpenVinoLoopImporterBlockerIsExplicit()
        {
            RequireExternal();
            var table = PaddleDocumentProfiles.CreateTableStructure(PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired"), modelSize: new VisualSize(512, 512));
            try
            {
                Run(table, Path.Combine(ModelRoot, "slanext-wired.onnx"), Array.Empty<NamedTensor>());
                Assert.Fail("The current OpenVINO importer unexpectedly accepted the SLANeXt Loop graph; update the support matrix and this test with the new evidence.");
            }
            catch (OpenVinoBackendException exception)
            {
                StringAssert.Contains(exception.ToString(), "Loop", "The known SLANeXt OpenVINO blocker changed and must be re-audited.");
                Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_UNSUPPORTED module=table-structure;model=paddle-table/slanext-wired;reason=onnx-loop-importer");
                Console.WriteLine(exception.ToString());
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void WirelessTableStructureOpenVinoLoopImporterBlockerIsExplicit()
        {
            RequireExternal();
            var table = PaddleDocumentProfiles.CreateTableStructure(PaddleDocumentModelCatalog.Get("paddle-table/slanext-wireless"), modelSize: new VisualSize(512, 512));
            try
            {
                Run(table, Path.Combine(ModelRoot, "slanext-wireless.onnx"), Array.Empty<NamedTensor>());
                Assert.Fail("The current OpenVINO importer unexpectedly accepted the wireless SLANeXt Loop graph; update the support matrix and this test with the new evidence.");
            }
            catch (OpenVinoBackendException exception)
            {
                StringAssert.Contains(exception.ToString(), "Loop", "The known wireless SLANeXt OpenVINO blocker changed and must be re-audited.");
                Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_UNSUPPORTED module=table-structure;model=paddle-table/slanext-wireless;reason=onnx-loop-importer");
                Console.WriteLine(exception.ToString());
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void SealDecoderRunsOnRealOpenVinoCpu()
        {
            RequireExternal();
            var seal = PaddleDocumentProfiles.CreateSealDetection(PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-mobile"), new VisualSize(224, 224));
            var sealResult = Run(seal, Path.Combine(ModelRoot, "ppocrv4-mobile-seal-det.onnx"), Array.Empty<NamedTensor>()) as PaddleDocumentSealResult;
            Assert.IsNotNull(sealResult);
            Assert.IsTrue(sealResult!.MaskWidth > 0 && sealResult.MaskHeight > 0);
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_SEMANTIC module=seal;regions=" + sealResult.Regions.Count + ";mask=" + sealResult.MaskWidth + "x" + sealResult.MaskHeight);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void ServerSealDecoderRunsOnRealOpenVinoCpu()
        {
            RequireExternal();
            var seal = PaddleDocumentProfiles.CreateSealDetection(PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-server"), new VisualSize(224, 224));
            var sealResult = Run(seal, Path.Combine(ModelRoot, "ppocrv4-server-seal-det.onnx"), Array.Empty<NamedTensor>()) as PaddleDocumentSealResult;
            Assert.IsNotNull(sealResult);
            Assert.IsTrue(sealResult!.MaskWidth > 0 && sealResult.MaskHeight > 0);
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_SEMANTIC module=seal;model=paddle-seal/ppocrv4-server;regions=" + sealResult.Regions.Count + ";mask=" + sealResult.MaskWidth + "x" + sealResult.MaskHeight);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void TableClassificationAndCellDecodersRunOnRealOpenVinoCpu()
        {
            RequireExternal();
            var classification = PaddleDocumentProfiles.CreateClassification(
                PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls"),
                new[] { "wired", "wireless" },
                VisualTaskId.TableClassification,
                modelSize: new VisualSize(224, 224));
            var classificationResult = Run(classification, Path.Combine(ModelRoot, "pp-lcnet-x1-0-table-cls.onnx"), Array.Empty<NamedTensor>()) as ClassificationResult;
            Assert.IsNotNull(classificationResult?.TopPrediction);
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_SEMANTIC module=table-classification;label=" + classificationResult!.TopPrediction!.Label + ";score=" + classificationResult.TopPrediction.Score);

            var cells = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                PaddleDocumentModelCatalog.Get("paddle-table/rt-detr-l-wired-cell-det"),
                new[] { "table-cell" },
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                scoreThreshold: 0);
            var cellResult = Run(cells, Path.Combine(ModelRoot, "rt-detr-l-wired-cell-det.onnx"), cells.CreateGeometryInputs()) as DetectionResult;
            Assert.IsNotNull(cellResult);
            Console.WriteLine("PADDLE_DOCUMENT_OPENVINO_SEMANTIC module=table-cell;detections=" + cellResult!.Detections.Count);
        }

        private static object Run(PaddleDocumentProfile profile, string path, IReadOnlyList<NamedTensor> auxiliary)
        {
            if (!File.Exists(path)) Assert.Inconclusive("Missing local PP-Structure model: " + path);
            using var registry = new BackendRegistry();
            registry.UseOpenVino();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            var inputFactory = new OpenCvVisualInputFactory();
            using PreparedVisualInput input = profile.VisualProfile.AuxiliaryInputs.Count == 0
                ? inputFactory.CreateFromFile(RequireImage(), profile.VisualProfile, inputId: "paddle-document-openvino-semantic-smoke")
                : OpenCvPaddleDocumentPreprocessing.CreateFromFile(inputFactory, RequireImage(), profile, inputId: "paddle-document-openvino-semantic-smoke");
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, OpenVinoBackendProvider.BackendId), request);
            var inputs = new List<NamedTensor>(1 + input.AuxiliaryInputs.Count) { new NamedTensor(input.InputName, input.Tensor) };
            inputs.AddRange(input.AuxiliaryInputs);
            InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
            return profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
        }

        private static NamedTensor FloatInput(string name, params float[] values)
            => new NamedTensor(name, new Tensor<float>(new TensorShape(1, 2), values, TensorBufferOwnership.Transfer));

        private static string RequireImage()
        {
            if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing semantic smoke input image: " + ImagePath);
            return ImagePath;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_RUN_EXTERNAL=1 to run the authorized local PP-Structure OpenVINO semantic smoke.");
        }
    }
}
