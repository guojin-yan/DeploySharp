using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>Runs real dynamic-batch PP-Structure exports through supported CPU backends. / 使用支持的 CPU 后端运行真实动态 Batch PP-Structure 导出。</summary>
    [TestClass]
    public sealed class PaddleDocumentDynamicBatchIntegrationTests
    {
        private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
        private const string FormulaSourceRoot = @"E:\Model\PaddleDocument\source";
        private const string FormulaImagePath = @"E:\Model\PaddleDocument\validation\general_formula_rec_001.png";
        private const string ImagePath = @"E:\Data\image\bus.jpg";
        private const string TableImagePath = @"E:\Model\PaddleDocument\validation\table_recognition.jpg";
        private const string SlanextOpenVinoRoot = @"E:\Model\PaddleDocument\onnx-normalized-rerun-20260929";
        private const float ClassifierCrossBackendMaxAbsoluteTolerance = 0.0001f;

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchClassifiersRunOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(ImagePath, "dynamic-batch image");
            var rows = new List<object>();
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId)
            })
            {
                foreach (ClassifierCase item in Cases)
                {
                    rows.Add(RunCase(backend, item));
                }
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                input = new { file = ImagePath, sha256 = FileSha256(ImagePath) },
                batch = 2,
                scope = "Official PP-LCNet document-orientation and table-classification ONNX exports with two identical source rows.",
                results = rows,
                boundary = "True model batch execution and decoder result-order evidence on one Windows host; not a quality score, throughput benchmark or cross-device claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(4, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchClassifiersPreserveDistinctRowsAcrossCpuBackends()
        {
            RequireExternal();
            RequireFile(TableImagePath, "dynamic-batch classifier image");
            var rows = new List<object>();
            var ortRawOutputRowsByModel = new Dictionary<string, float[][]>(StringComparer.Ordinal);
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId),
                new BackendCase("opencv-dnn-cpu", OpenCvDnnBackendProvider.BackendId)
            })
            {
                foreach (ClassifierCase item in Cases)
                    rows.Add(RunCase(backend, item, useDistinctHorizontalBands: true, ortRawOutputRowsByModel: ortRawOutputRowsByModel));
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_CLASSIFIER_DISTINCT_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                execution = new
                {
                    os = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    framework = RuntimeInformation.FrameworkDescription,
                    targetFramework = "net10.0",
                    configuration = "Release"
                },
                input = new { file = TableImagePath, sha256 = FileSha256(TableImagePath), regions = "top and bottom non-overlapping horizontal bands" },
                batch = 2,
                models = Cases.Select(item => new { model = item.ModelId, file = item.FileName }).ToArray(),
                backends = new[] { "onnxruntime-cpu", "openvino-cpu", "opencv-dnn-cpu" },
                scope = "Official PP-LCNet document-orientation and table-classification ONNX exports; each batch row is a distinct top/bottom region from the same table-recognition image. Raw outputs are compared per row against ONNX Runtime CPU with an explicit maximum-absolute-error tolerance.",
                results = rows,
                numericParity = new { referenceBackend = "onnxruntime-cpu", comparison = "raw output values per batch row", maximumAbsoluteTolerance = ClassifierCrossBackendMaxAbsoluteTolerance },
                boundary = "True batch binding, distinct raw input/output rows, decoder result mapping and bounded raw-output numerical parity for these exact artifacts/backends on one Windows host. Regions are execution probes, not classification ground truth; no accuracy, throughput or cross-device claim is made."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(6, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialFormulaPlusSDynamicBatchMatchesIndependentRunsOnOrtCpu()
        {
            RequireExternal();
            RequireFile(FormulaImagePath, "official formula example image");
            string modelPath = Path.Combine(ModelRoot, "pp-formulanet-plus-s.onnx");
            string tokenizerPath = Path.Combine(FormulaSourceRoot, "pp-formulanet-plus-s", "PP-FormulaNet_plus-S_infer", "inference.yml");
            RequireFile(modelPath, "PP-FormulaNet Plus-S dynamic-batch ONNX export");
            RequireFile(tokenizerPath, "PP-FormulaNet Plus-S official tokenizer");

            PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(tokenizerPath);
            int endTokenId = FindTokenId(tokenizer.Tokens, "</s>");
            int startTokenId = FindTokenId(tokenizer.Tokens, "<s>");
            int padTokenId = FindTokenId(tokenizer.Tokens, "<pad>");
            int unknownTokenId = FindTokenId(tokenizer.Tokens, "<unk>");
            Assert.IsTrue(endTokenId >= 0, "The official formula tokenizer must expose its EOS token.");

            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(
                descriptor,
                new PaddleDocumentFormulaSchema(tokenizer, endTokenId, startTokenId, padTokenId, unknownTokenId),
                modelSize: new VisualSize(384, 384),
                maximumSequenceLength: 4096,
                maximumBatch: 2);
            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0], "FormulaNet input must expose a dynamic batch axis.");
            Assert.AreEqual(-1L, profile.VisualProfile.Outputs[0].ShapePattern[0], "FormulaNet output must expose a dynamic batch axis.");

            var formulaImageFactory = new OpenCvBgrImageFactory();
            OpenCvBgrImage formulaSource = formulaImageFactory.Create(OpenCvImageSource.FromFile(FormulaImagePath), "formula-source");
            int firstBandHeight = formulaSource.Height / 2;
            OpenCvBgrImage firstBand = CropHorizontalBand(formulaSource, 0, firstBandHeight, "formula-top-band");
            OpenCvBgrImage secondBand = CropHorizontalBand(formulaSource, firstBandHeight, formulaSource.Height - firstBandHeight, "formula-bottom-band");
            var factory = new PaddleDocumentFormulaInputFactory();
            using PreparedVisualInput row0 = factory.Create(firstBand, profile.VisualProfile);
            using PreparedVisualInput row1 = factory.Create(secondBand, profile.VisualProfile);
            Assert.AreEqual(1, row0.BatchSize);
            Assert.AreEqual(1, row1.BatchSize);
            Assert.IsTrue(row0.Tensor.Buffer is float[] && row1.Tensor.Buffer is float[], "Formula preprocessing must produce Float32 tensors.");
            float[] row0Values = (float[])row0.Tensor.Buffer;
            float[] row1Values = (float[])row1.Tensor.Buffer;
            Assert.AreEqual(row0Values.Length, row1Values.Length);
            Assert.AreNotEqual(TensorSha256(row0.Tensor), TensorSha256(row1.Tensor), "Distinct formula bands must produce distinct prepared batch rows.");
            var batchedValues = new float[checked(row0Values.Length + row1Values.Length)];
            Array.Copy(row0Values, 0, batchedValues, 0, row0Values.Length);
            Array.Copy(row1Values, 0, batchedValues, row0Values.Length, row1Values.Length);
            var batchedTensor = new Tensor<float>(
                new TensorShape(2, 1, row0.Tensor.Shape[2], row0.Tensor.Shape[3]),
                batchedValues,
                TensorBufferOwnership.Transfer);
            using var batchInput = new PreparedVisualInput(
                row0.InputName,
                batchedTensor,
                row0.SourceSize,
                row0.ModelSize,
                2,
                row0.Layout,
                row0.Transform,
                row0.Preprocessing,
                "formula-distinct-session-runs-batch",
                batchFrames: new[] { row0.BatchFrames[0], row1.BatchFrames[0] });
            Assert.AreEqual(2L, batchInput.Tensor.Shape[0]);
            Assert.AreEqual(TensorRowSha256(batchInput.Tensor, 0), TensorRowSha256(row0.Tensor, 0));
            Assert.AreEqual(TensorRowSha256(batchInput.Tensor, 1), TensorRowSha256(row1.Tensor, 0));

            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), request);
            PaddleDocumentFormulaResult single0 = RunFormulaRow(session, profile, row0);
            PaddleDocumentFormulaResult single1 = RunFormulaRow(session, profile, row1);
            InferenceOutputs batchOutputs = session.Run(InferenceInputs.Create(batchInput.InputName, batchInput.Tensor), CancellationToken.None);
            ITensor tokenOutput = batchOutputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
            Assert.AreEqual(TensorElementType.Int64, tokenOutput.ElementType);
            Assert.AreEqual(2L, tokenOutput.Shape[0]);
            var batchResult = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(batchInput, profile.VisualProfile, batchOutputs, CancellationToken.None)) as PaddleDocumentFormulaBatchResult
                ?? throw new AssertFailedException("The formula decoder did not return a batch result for batch=2.");
            Assert.AreEqual(2, batchResult.Count);
            CollectionAssert.AreEqual(single0.TokenIds.ToArray(), batchResult[0].TokenIds.ToArray(), "Formula batch row 0 diverged from its independent run.");
            CollectionAssert.AreEqual(single1.TokenIds.ToArray(), batchResult[1].TokenIds.ToArray(), "Formula batch row 1 diverged from its independent run.");
            CollectionAssert.AreNotEqual(single0.TokenIds.ToArray(), single1.TokenIds.ToArray(), "Distinct formula bands must produce distinct decoded rows for row-isolation evidence.");
            Assert.AreEqual(single0.Latex, batchResult[0].Latex, "Formula batch row 0 changed the decoded LaTeX.");
            Assert.AreEqual(single1.Latex, batchResult[1].Latex, "Formula batch row 1 changed the decoded LaTeX.");
            Assert.IsFalse(batchResult[0].Warnings.Contains("missing-eos:sequence-may-be-truncated", StringComparer.Ordinal));
            Assert.IsFalse(batchResult[1].Warnings.Contains("missing-eos:sequence-may-be-truncated", StringComparer.Ordinal));

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-formula-plus-s-dynamic-batch-ort-20261007.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                execution = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), framework = RuntimeInformation.FrameworkDescription, targetFramework = "net10.0", configuration = "Release" },
                backend = "onnxruntime-cpu",
                model = new { id = descriptor.ModelId, file = Path.GetFileName(modelPath), sha256 = FileSha256(modelPath) },
                tokenizer = new { file = "inference.yml", sha256 = FileSha256(tokenizerPath), tokenCount = tokenizer.Tokens.Count, endTokenId },
                input = new { file = FormulaImagePath, sha256 = FileSha256(FormulaImagePath), regions = new[] { new { row = 0, y = 0, height = firstBandHeight }, new { row = 1, y = firstBandHeight, height = formulaSource.Height - firstBandHeight } }, rowInputsAreDistinct = true },
                batch = new { inputShape = batchInput.Tensor.Shape.ToString(), outputShape = tokenOutput.Shape.ToString(), rowInputSha256 = new[] { TensorRowSha256(batchInput.Tensor, 0), TensorRowSha256(batchInput.Tensor, 1) } },
                rows = new[]
                {
                    new { row = 0, inputId = batchInput.BatchFrames[0].InputId, batchTokenCount = batchResult[0].TokenIds.Count, batchTokenSha256 = Sha256Text(string.Join(",", batchResult[0].TokenIds)), singleTokenSha256 = Sha256Text(string.Join(",", single0.TokenIds)), reachedEos = !batchResult[0].Warnings.Contains("missing-eos:sequence-may-be-truncated", StringComparer.Ordinal), latexSha256 = Sha256Text(batchResult[0].Latex), exactSingleRunMatch = true },
                    new { row = 1, inputId = batchInput.BatchFrames[1].InputId, batchTokenCount = batchResult[1].TokenIds.Count, batchTokenSha256 = Sha256Text(string.Join(",", batchResult[1].TokenIds)), singleTokenSha256 = Sha256Text(string.Join(",", single1.TokenIds)), reachedEos = !batchResult[1].Warnings.Contains("missing-eos:sequence-may-be-truncated", StringComparer.Ordinal), latexSha256 = Sha256Text(batchResult[1].Latex), exactSingleRunMatch = true }
                },
                boundary = "True dynamic batch=2 execution with two distinct horizontal regions and exact token/LaTeX parity against independent single-region ORT CPU runs for this exact FormulaNet Plus-S artifact on one Windows host. The regions come from one official formula image and are execution probes, not separately labeled formulas; this proves row isolation, not multi-formula accuracy, cross-backend support or performance."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(2, batchResult.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchLayoutRunsOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(ImagePath, "dynamic-batch layout image");
            var rows = new List<object>();
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId)
            })
            {
                rows.Add(RunLayoutCase(backend));
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_LAYOUT_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-layout-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                input = new { file = ImagePath, sha256 = FileSha256(ImagePath) },
                batch = 2,
                model = "paddle-doc/pp-doclayout-l",
                modelFile = "pp-doclayout-l.onnx",
                scope = "Official PP-DocLayout-L dynamic Paddle NMS export with two identical source rows.",
                results = rows,
                boundary = "True model batch execution, auxiliary geometry binding and flattened bbox_num row partitioning on one Windows host; not a quality score, throughput benchmark or cross-device claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(2, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchRtDetrLayoutRunsOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(ImagePath, "dynamic-batch RT-DETR layout image");
            var rows = new List<object>();
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId)
            })
            {
                rows.Add(RunNmsCase(backend, "paddle-doc/rt-detr-h-layout-3cls", "rt-detr-h-layout-3cls.onnx", PaddleDocumentProfiles.Layout3Labels));
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_RTDETR_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-rtdetr-layout-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                input = new { file = ImagePath, sha256 = FileSha256(ImagePath) },
                batch = 2,
                model = "paddle-doc/rt-detr-h-layout-3cls",
                modelFile = "rt-detr-h-layout-3cls.onnx",
                scope = "Official RT-DETR-H three-class layout dynamic Paddle NMS export with two identical source rows.",
                results = rows,
                boundary = "True model batch execution, auxiliary geometry binding and flattened bbox_num row partitioning on one Windows host; not a quality score, throughput benchmark or cross-device claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(2, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchAdditionalLayoutsRunOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(ImagePath, "dynamic-batch additional layout image");
            var models = new[]
            {
                (ModelId: "paddle-doc/pp-doclayout-plus-l", FileName: "pp-doclayout-plus-l.onnx", Labels: PaddleDocumentProfiles.LayoutPlusLabels, Size: new VisualSize(800, 800)),
                (ModelId: "paddle-doc/pp-docblocklayout", FileName: "pp-docblocklayout.onnx", Labels: PaddleDocumentProfiles.RegionLabels, Size: new VisualSize(640, 640))
            };
            var rows = new List<object>();
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId)
            })
            {
                foreach (var model in models)
                {
                    rows.Add(RunNmsCase(
                        backend,
                        model.ModelId,
                        model.FileName,
                        model.Labels,
                        ImagePath,
                        useDistinctHorizontalBands: true,
                        modelSize: model.Size));
                }
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ADDITIONAL_LAYOUT_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-additional-layout-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                input = new { file = ImagePath, sha256 = FileSha256(ImagePath) },
                batch = 2,
                models = models.Select(value => new { model = value.ModelId, file = value.FileName, modelSize = new { width = value.Size.Width, height = value.Size.Height } }).ToArray(),
                scope = "Official PP-DocLayout_plus-L and PP-DocBlockLayout dynamic Paddle NMS exports. Each batch row is a distinct non-overlapping horizontal crop of bus.jpg; ORT CPU and OpenVINO CPU execute the full NMS decoder.",
                results = rows,
                boundary = "True model batch binding and row-isolated decoded results for these two exact artifacts/backends on one Windows host. Crops are execution probes, not representative document-quality samples; not an accuracy score, throughput benchmark, TensorRT/OpenCV batch claim or cross-device claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(4, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchSealDetectorsRunOnOrtOpenVinoAndOpenCvDnn()
        {
            RequireExternal();
            string[] images = { @"E:\Data\ocr\demo_4.jpg", @"E:\Data\ocr\demo_5.jpg" };
            foreach (string image in images) RequireFile(image, "dynamic-batch seal image");

            var models = new[]
            {
                (ModelId: "paddle-seal/ppocrv4-mobile", FileName: "ppocrv4-mobile-seal-det.onnx"),
                (ModelId: "paddle-seal/ppocrv4-server", FileName: "ppocrv4-server-seal-det.onnx")
            };
            var rows = new List<object>();
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId),
                new BackendCase("opencv-dnn-cpu", OpenCvDnnBackendProvider.BackendId)
            })
            {
                foreach (var model in models)
                    rows.Add(RunSealBatchCase(backend, model.ModelId, model.FileName, images));
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_SEAL_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                execution = new
                {
                    os = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    framework = RuntimeInformation.FrameworkDescription,
                    targetFramework = "net10.0",
                    configuration = "Release"
                },
                images = images.Select(path => new { file = path, sha256 = FileSha256(path) }).ToArray(),
                batch = 2,
                models = models.Select(value => new { model = value.ModelId, file = value.FileName }).ToArray(),
                scope = "Official PP-OCRv4 mobile/server seal probability-map exports; two distinct full source images are stacked into one Float32 tensor and decoded as a batch on ORT CPU, OpenVINO CPU and OpenCV DNN CPU.",
                results = rows,
                boundary = "True batch binding, raw output row separation and per-row source/page metadata mapping for these exact artifacts on one Windows host. The images have no seal ground truth; no detection quality, accuracy, throughput, TensorRT batch or cross-device claim is made."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(6, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchTableCellDetectorsRunOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(TableImagePath, "dynamic-batch table-cell image");
            var models = new[]
            {
                (ModelId: "paddle-table/rt-detr-l-wired-cell-det", FileName: "rt-detr-l-wired-cell-det.onnx"),
                (ModelId: "paddle-table/rt-detr-l-wireless-cell-det", FileName: "rt-detr-l-wireless-cell-det.onnx")
            };
            var rows = new List<object>();
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId)
            })
            {
                foreach (var model in models)
                {
                    rows.Add(RunNmsCase(
                        backend,
                        model.ModelId,
                        model.FileName,
                        new[] { "table-cell" },
                        TableImagePath,
                        useDistinctHorizontalBands: true));
                }
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_TABLE_CELL_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-table-cell-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                input = new { file = TableImagePath, sha256 = FileSha256(TableImagePath) },
                batch = 2,
                models = models.Select(value => new { model = value.ModelId, file = value.FileName }).ToArray(),
                scope = "Official wired and wireless RT-DETR-L table-cell dynamic NMS exports with two distinct, non-overlapping horizontal bands from one source image on ONNX Runtime CPU and OpenVINO CPU.",
                results = rows,
                boundary = "True model batch binding, distinct input and result row digests, per-row auxiliary geometry and decoder row isolation on one Windows host. The source is one table image split into two regions, not two independent pages. This is not a cell-detection accuracy score, cross-backend numerical parity test, throughput benchmark or cross-device claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(4, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchSlaNextRunsOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(TableImagePath, "dynamic-batch table image");
            var cases = new[]
            {
                new TableBackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId, "paddle-table/slanext-wired", Path.Combine(ModelRoot, "slanext-wired.onnx")),
                new TableBackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId, "paddle-table/slanext-wired", Path.Combine(SlanextOpenVinoRoot, "slanext-wired-openvino-compat.onnx"))
            };
            var rows = new List<object>();
            foreach (TableBackendCase backend in cases) rows.Add(RunSlaNextCase(backend));

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_SLANEXT_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-slanext-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                input = new { file = TableImagePath, sha256 = FileSha256(TableImagePath) },
                batch = 2,
                model = "paddle-table/slanext-wired",
                scope = "Official SLANeXt dynamic table-structure export on ORT and the separately hashed OpenVINO-compatible graph.",
                results = rows,
                boundary = "True model batch execution and table decoder row isolation on one Windows host; OpenVINO uses the compatibility graph because the original graph remains Loop-importer blocked; not a table accuracy score, throughput benchmark or cross-device claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(2, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchWirelessSlaNextRunsOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(TableImagePath, "dynamic-batch wireless table image");
            var cases = new[]
            {
                new TableBackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId, "paddle-table/slanext-wireless", Path.Combine(ModelRoot, "slanext-wireless.onnx")),
                new TableBackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId, "paddle-table/slanext-wireless", Path.Combine(SlanextOpenVinoRoot, "slanext-wireless-openvino-compat.onnx"))
            };
            var rows = new List<object>();
            foreach (TableBackendCase backend in cases) rows.Add(RunSlaNextCase(backend));

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_SLANEXT_WIRELESS_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-slanext-wireless-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                input = new { file = TableImagePath, sha256 = FileSha256(TableImagePath) },
                batch = 2,
                model = "paddle-table/slanext-wireless",
                scope = "Official wireless SLANeXt dynamic table-structure export on ORT and the separately hashed OpenVINO-compatible graph.",
                results = rows,
                boundary = "True model batch execution and table decoder row isolation on one Windows host; OpenVINO uses the compatibility graph because the original Loop graph remains importer-blocked; not a table accuracy score, throughput benchmark or cross-device claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(2, rows.Count);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialDynamicBatchUnwarpingRunsOnOrtAndOpenVino()
        {
            RequireExternal();
            RequireFile(ImagePath, "dynamic-batch UVDoc image");
            string modelPath = Path.Combine(ModelRoot, "uvdoc.onnx");
            RequireFile(modelPath, "dynamic-batch UVDoc model");

            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateUnwarping(
                descriptor,
                new VisualSize(640, 640),
                maximumBatch: 2);
            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0]);
            Assert.AreEqual(-1L, profile.VisualProfile.Outputs[0].ShapePattern[0]);

            var factory = new OpenCvVisualInputFactory();
            using PreparedVisualInput probe = factory.CreateFromFile(ImagePath, profile.VisualProfile, inputId: "uvdoc-dynamic-batch-probe");
            float leftWidth = probe.SourceSize.Width / 2f;
            var leftRegion = new RectangleRoiGeometry(new RectangleF(0, 0, leftWidth, probe.SourceSize.Height));
            var rightRegion = new RectangleRoiGeometry(new RectangleF(leftWidth, 0, probe.SourceSize.Width - leftWidth, probe.SourceSize.Height));
            using PreparedVisualInput input = factory.CreateRoiBatch(
                OpenCvImageSource.FromFile(ImagePath),
                new IVisualRoiGeometry[] { leftRegion, rightRegion },
                profile.VisualProfile.Input.Name,
                profile.VisualProfile.Preprocessing!.ToOpenCvOptions(),
                inputId: "uvdoc-dynamic-batch-rows",
                cancellationToken: CancellationToken.None);
            Assert.AreEqual(2, input.BatchSize);
            Assert.AreEqual(2L, input.Tensor.Shape[0]);
            string preparedInputSha256 = TensorSha256(input.Tensor);

            var reportRows = new List<object>();
            var outputsByBackend = new Dictionary<string, float[]>(StringComparer.Ordinal);
            foreach (BackendCase backend in new[]
            {
                new BackendCase("onnxruntime-cpu", OnnxRuntimeBackendProvider.BackendId),
                new BackendCase("openvino-cpu", OpenVinoBackendProvider.BackendId)
            })
            {
                using var registry = new BackendRegistry();
                BackendRequest request;
                if (backend.Id == OnnxRuntimeBackendProvider.BackendId)
                {
                    registry.UseOnnxRuntime();
                    request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
                }
                else
                {
                    registry.UseOpenVino();
                    request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
                }

                using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, backend.Id), request);
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                ITensor output = outputs.GetRequired("fetch_name_0");
                Assert.AreEqual(TensorElementType.Float32, output.ElementType);
                Assert.AreEqual(2L, output.Shape[0]);
                Assert.AreEqual(3L, output.Shape[1]);
                Assert.AreEqual(640L, output.Shape[2]);
                Assert.AreEqual(640L, output.Shape[3]);
                Assert.IsTrue(output.Buffer is float[], backend.Name + " UVDoc output must expose a Float32 buffer.");
                float[] outputValues = (float[])output.Buffer;
                Assert.IsTrue(outputValues.All(float.IsFinite), backend.Name + " returned a non-finite UVDoc pixel.");

                var decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as PaddleDocumentUnwarpingBatchResult
                    ?? throw new AssertFailedException(backend.Name + " did not return a PaddleDocumentUnwarpingBatchResult.");
                Assert.AreEqual(2, decoded.Count);
                Assert.AreEqual(0, decoded[0].Metadata.PageIndex);
                Assert.AreEqual(1, decoded[1].Metadata.PageIndex);
                Assert.AreEqual(decoded[0].Width, decoded[1].Width);
                Assert.AreEqual(decoded[0].Height, decoded[1].Height);
                Assert.AreEqual(decoded[0].Channels, decoded[1].Channels);
                Assert.AreEqual(640, decoded[0].Width);
                Assert.AreEqual(640, decoded[0].Height);

                int rowLength = outputValues.Length / 2;
                double rowMeanAbs = MeanAbsoluteDifference(outputValues, 0, outputValues, rowLength, rowLength);
                Assert.IsTrue(rowMeanAbs > 0.001, backend.Name + " did not preserve distinct UVDoc results for the left/right source regions: " + rowMeanAbs.ToString("R"));
                outputsByBackend.Add(backend.Name, (float[])outputValues.Clone());
                reportRows.Add(new
                {
                    backend = backend.Name,
                    modelSha256 = FileSha256(modelPath),
                    inputShape = input.Tensor.Shape.ToString(),
                    outputShape = output.Shape.ToString(),
                    resultCount = decoded.Count,
                    resultSizes = decoded.Items.Select(value => new { value.Width, value.Height, value.Channels, value.Pixels.Count }).ToArray(),
                    pageIndexes = decoded.Items.Select(value => value.Metadata.PageIndex).ToArray(),
                    roiRegions = new object[]
                    {
                        new { x = 0, y = 0, width = leftWidth, height = probe.SourceSize.Height },
                        new { x = leftWidth, y = 0, width = probe.SourceSize.Width - leftWidth, height = probe.SourceSize.Height }
                    },
                    rowMeanAbsoluteDifference = rowMeanAbs,
                    status = "passed"
                });
            }

            float[] ortValues = outputsByBackend["onnxruntime-cpu"];
            float[] openVinoValues = outputsByBackend["openvino-cpu"];
            Assert.AreEqual(ortValues.Length, openVinoValues.Length);
            int outputRowLength = ortValues.Length / 2;
            double[] backendMeanAbsByRow = new double[2];
            for (int row = 0; row < backendMeanAbsByRow.Length; row++)
            {
                backendMeanAbsByRow[row] = MeanAbsoluteDifference(ortValues, row * outputRowLength, openVinoValues, row * outputRowLength, outputRowLength);
                Assert.IsTrue(backendMeanAbsByRow[row] < 0.01, "UVDoc ORT/OpenVINO mean pixel drift exceeded the existing diagnostic threshold for row " + row + ": " + backendMeanAbsByRow[row].ToString("R"));
            }
            double backendMeanAbs = backendMeanAbsByRow.Average();
            double backendMaxAbs = MaxAbsoluteDifference(ortValues, openVinoValues);

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_UVDOC_DYNAMIC_BATCH_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-uvdoc-dynamic-batch-ort-openvino.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                model = descriptor.ModelId,
                modelSha256 = FileSha256(modelPath),
                input = new { file = ImagePath, sha256 = FileSha256(ImagePath) },
                preparedInputSha256,
                batch = 2,
                inputShape = input.Tensor.Shape.ToString(),
                outputShape = new[] { 2, 3, 640, 640 },
                scope = "Official UVDoc dynamic batch with two distinct, non-overlapping full-height ROIs from one real source image, decoded independently by ORT CPU and OpenVINO CPU.",
                results = reportRows,
                backendParity = new { meanAbsoluteDifference = backendMeanAbs, meanAbsoluteDifferenceByRow = backendMeanAbsByRow, maxAbsoluteDifference = backendMaxAbs, meanDiagnosticThreshold = 0.01 },
                boundary = "True dynamic batch execution on two distinct source regions, output decoder row isolation and bounded numerical parity for this exact Windows-host model/input pair; not a visual-quality score, throughput benchmark, pixel-equivalence claim or TensorRT/OpenCV support claim."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Console.WriteLine("PADDLE_DOCUMENT_UVDOC_DYNAMIC_BATCH input=" + input.Tensor.Shape + ";output=" + string.Join(";", reportRows.Select(value => JsonSerializer.Serialize(value))) + ";meanAbs=" + backendMeanAbs.ToString("R") + ";maxAbs=" + backendMaxAbs.ToString("R"));
        }

        private static readonly ClassifierCase[] Cases =
        {
            new ClassifierCase(
                "paddle-doc/pp-lcnet-x1-0-doc-ori",
                "pp-lcnet-x1-0-doc-ori.onnx",
                VisualTaskId.DocumentOrientation,
                PaddleDocumentProfiles.DocumentOrientationLabels),
            new ClassifierCase(
                "paddle-table/pp-lcnet-x1-0-table-cls",
                "pp-lcnet-x1-0-table-cls.onnx",
                VisualTaskId.TableClassification,
                new[] { "wired", "wireless" })
        };

        private static object RunLayoutCase(BackendCase backend)
        {
            return RunNmsCase(backend, "paddle-doc/pp-doclayout-l", "pp-doclayout-l.onnx", PaddleDocumentProfiles.Layout23Labels);
        }

        private static object RunNmsCase(BackendCase backend, string modelId, string fileName, IReadOnlyList<string> labels, string imagePath = ImagePath, bool useDistinctHorizontalBands = false, VisualSize? modelSize = null)
        {
            string path = Path.Combine(ModelRoot, fileName);
            RequireFile(path, modelId + " dynamic model");
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(modelId);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                labels,
                modelSize ?? new VisualSize(640, 640),
                includeGeometryInputs: true,
                maximumBatch: 2,
                scoreThreshold: 0);
            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0]);
            Assert.AreEqual(-1L, profile.VisualProfile.AuxiliaryInputs[0].ShapePattern[0]);

            var factory = new OpenCvVisualInputFactory();
            using PreparedVisualInput probe = factory.CreateFromFile(imagePath, profile.VisualProfile, inputId: "nms-dynamic-batch-probe");
            var fullSource = new RectangleRoiGeometry(new RectangleF(0, 0, probe.SourceSize.Width, probe.SourceSize.Height));
            IVisualRoiGeometry[] regions;
            if (useDistinctHorizontalBands)
            {
                float topHeight = probe.SourceSize.Height / 2f;
                regions = new IVisualRoiGeometry[]
                {
                    new RectangleRoiGeometry(new RectangleF(0, 0, probe.SourceSize.Width, topHeight)),
                    new RectangleRoiGeometry(new RectangleF(0, topHeight, probe.SourceSize.Width, probe.SourceSize.Height - topHeight))
                };
            }
            else
            {
                regions = new IVisualRoiGeometry[] { fullSource, fullSource };
            }
            using PreparedVisualInput raw = factory.CreateRoiBatch(
                OpenCvImageSource.FromFile(imagePath),
                regions,
                profile.VisualProfile.Input.Name,
                profile.VisualProfile.Preprocessing!.ToOpenCvOptions(),
                inputId: "nms-dynamic-batch-rows",
                cancellationToken: CancellationToken.None);
            // Rebind the exact per-row geometry after the ROI batch knows source/model sizes.
            using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.RebindWithGeometryInputs(raw, profile);
            Assert.AreEqual(2, input.BatchSize);
            Assert.AreEqual(2L, input.Tensor.Shape[0]);
            Assert.AreEqual(2, input.AuxiliaryInputs.Count);
            foreach (NamedTensor auxiliary in input.AuxiliaryInputs) Assert.AreEqual(2L, auxiliary.Tensor.Shape[0], auxiliary.Name);
            string[] inputRowSha256 = Enumerable.Range(0, input.BatchSize).Select(row => TensorRowSha256(input.Tensor, row)).ToArray();
            if (useDistinctHorizontalBands)
                Assert.AreNotEqual(inputRowSha256[0], inputRowSha256[1], backend.Name + " did not preserve distinct prepared inputs for the two source regions.");
            else
                Assert.AreEqual(inputRowSha256[0], inputRowSha256[1], backend.Name + " changed identical prepared input rows.");

            using var registry = new BackendRegistry();
            BackendRequest request;
            if (backend.Id == OnnxRuntimeBackendProvider.BackendId)
            {
                registry.UseOnnxRuntime();
                request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            }
            else
            {
                registry.UseOpenVino();
                request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            }

            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, backend.Id), request);
            var inputs = new List<NamedTensor>(1 + input.AuxiliaryInputs.Count) { new NamedTensor(input.InputName, input.Tensor) };
            inputs.AddRange(input.AuxiliaryInputs);
            InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
            var decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as DetectionBatchResult
                ?? throw new AssertFailedException(modelId + " did not return a DetectionBatchResult.");
            Assert.AreEqual(2, decoded.Count);
            Assert.IsTrue(decoded[0].Detections.Count > 0, backend.Name + " returned no detections.");
            Assert.IsTrue(decoded[1].Detections.Count > 0, backend.Name + " returned no detections for the second batch row.");
            if (useDistinctHorizontalBands)
            {
                bool rowsProducedDistinctResults = decoded[0].Detections.Count != decoded[1].Detections.Count;
                int commonCount = Math.Min(decoded[0].Detections.Count, decoded[1].Detections.Count);
                for (int index = 0; index < commonCount && !rowsProducedDistinctResults; index++)
                {
                    Detection first = decoded[0].Detections[index];
                    Detection second = decoded[1].Detections[index];
                    rowsProducedDistinctResults = first.Label.Label != second.Label.Label ||
                        Math.Abs(first.Label.Score - second.Label.Score) > .00001f ||
                        Math.Abs(first.Box.X - second.Box.X) > .00001f ||
                        Math.Abs(first.Box.Y - second.Box.Y) > .00001f ||
                        Math.Abs(first.Box.Width - second.Box.Width) > .00001f ||
                        Math.Abs(first.Box.Height - second.Box.Height) > .00001f;
                }
                Assert.IsTrue(rowsProducedDistinctResults, backend.Name + " returned indistinguishable detections for distinct batch input rows.");
            }
            else
            {
                Assert.AreEqual(decoded[0].Detections.Count, decoded[1].Detections.Count, backend.Name + " changed detection count between identical batch rows.");
                for (int index = 0; index < decoded[0].Detections.Count; index++)
                {
                    Detection first = decoded[0].Detections[index];
                    Detection second = decoded[1].Detections[index];
                    Assert.AreEqual(first.Label.Label, second.Label.Label, backend.Name + " changed labels between identical batch rows.");
                    Assert.AreEqual(first.Label.Score, second.Label.Score, .00001f, backend.Name + " changed scores between identical batch rows.");
                    Assert.AreEqual(first.Box.X, second.Box.X, .00001f, backend.Name + " changed x geometry between identical batch rows.");
                    Assert.AreEqual(first.Box.Y, second.Box.Y, .00001f, backend.Name + " changed y geometry between identical batch rows.");
                    Assert.AreEqual(first.Box.Width, second.Box.Width, .00001f, backend.Name + " changed width geometry between identical batch rows.");
                    Assert.AreEqual(first.Box.Height, second.Box.Height, .00001f, backend.Name + " changed height geometry between identical batch rows.");
                }
            }

            Console.WriteLine("PADDLE_DOCUMENT_NMS_DYNAMIC_BATCH backend=" + backend.Name + ";model=" + modelId + ";batch=" + input.BatchSize + ";detections=" + decoded[0].Detections.Count);
            string[] detectionRowSha256 = decoded.Results.Select(DetectionRowSha256).ToArray();
            if (useDistinctHorizontalBands)
                Assert.AreNotEqual(detectionRowSha256[0], detectionRowSha256[1], backend.Name + " produced the same decoded-result digest for distinct batch input rows.");
            return new
            {
                backend = backend.Name,
                model = descriptor.ModelId,
                modelSha256 = FileSha256(path),
                inputShape = input.Tensor.Shape.ToString(),
                inputBatch = input.BatchSize,
                inputRowSha256,
                detectionRowSha256,
                sourceRegions = regions.Select(value => new { x = value.Bounds.X, y = value.Bounds.Y, width = value.Bounds.Width, height = value.Bounds.Height }).ToArray(),
                auxiliaryShapes = input.AuxiliaryInputs.Select(value => new { name = value.Name, shape = value.Tensor.Shape.ToString() }).ToArray(),
                outputNames = outputs.Select(value => value.Name).ToArray(),
                resultCount = decoded.Count,
                detectionCounts = decoded.Results.Select(value => value.Detections.Count).ToArray(),
                distinctFirstRowLabels = decoded[0].Detections.Select(value => value.Label.Label).Distinct(StringComparer.Ordinal).ToArray(),
                status = "passed"
            };
        }

        private static object RunSealBatchCase(BackendCase backend, string modelId, string fileName, IReadOnlyList<string> imagePaths)
        {
            string modelPath = Path.Combine(ModelRoot, fileName);
            RequireFile(modelPath, modelId + " dynamic seal model");
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(modelId);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(descriptor, new VisualSize(224, 224), maximumBatch: 2);
            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0]);
            Assert.AreEqual(-1L, profile.VisualProfile.Outputs[0].ShapePattern[0]);

            var factory = new OpenCvVisualInputFactory();
            string[] imageHashes = imagePaths.Select(FileSha256).ToArray();
            using PreparedVisualInput first = factory.CreateFromFile(imagePaths[0], profile.VisualProfile, imageHashes[0]);
            using PreparedVisualInput second = factory.CreateFromFile(imagePaths[1], profile.VisualProfile, imageHashes[1]);
            Assert.AreEqual(first.Tensor.Shape, second.Tensor.Shape);
            Assert.AreEqual(TensorElementType.Float32, first.Tensor.ElementType);
            Assert.AreEqual(TensorElementType.Float32, second.Tensor.ElementType);
            float[] firstValues = first.Tensor.Buffer as float[] ?? throw new AssertFailedException("The first seal image must produce Float32 input.");
            float[] secondValues = second.Tensor.Buffer as float[] ?? throw new AssertFailedException("The second seal image must produce Float32 input.");
            Assert.AreEqual(firstValues.Length, secondValues.Length);

            var batchValues = new float[checked(firstValues.Length + secondValues.Length)];
            Array.Copy(firstValues, 0, batchValues, 0, firstValues.Length);
            Array.Copy(secondValues, 0, batchValues, firstValues.Length, secondValues.Length);
            long[] batchShape = first.Tensor.Shape.ToArray();
            batchShape[0] = 2;
            var frames = new[] { first.BatchFrames[0], second.BatchFrames[0] };
            using var input = new PreparedVisualInput(
                first.InputName,
                new Tensor<float>(new TensorShape(batchShape), batchValues, TensorBufferOwnership.Transfer),
                first.SourceSize,
                first.ModelSize,
                2,
                first.Layout,
                first.Transform,
                first.Preprocessing,
                inputId: "paddle-seal-dynamic-batch",
                batchFrames: frames);
            Assert.AreEqual(2L, input.Tensor.Shape[0]);
            string[] inputRowSha256 = Enumerable.Range(0, 2).Select(row => TensorRowSha256(input.Tensor, row)).ToArray();
            Assert.AreNotEqual(inputRowSha256[0], inputRowSha256[1], backend.Name + " prepared duplicate seal image rows.");

            using var registry = new BackendRegistry();
            BackendRequest request;
            if (backend.Id == OnnxRuntimeBackendProvider.BackendId)
            {
                registry.UseOnnxRuntime();
                request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            }
            else
            {
                if (backend.Id == OpenVinoBackendProvider.BackendId)
                {
                    registry.UseOpenVino();
                    request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
                }
                else
                {
                    var contract = new OpenCvDnnModelContract(
                        profile.VisualProfile.ModelId,
                        new[] { new TensorDescriptor("x", TensorElementType.Float32, new TensorShape(-1, 3, 224, 224)) },
                        new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(-1, 1, 224, 224)) },
                        imageInputNames: new[] { "x" });
                    registry.UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
                    request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
                }
            }

            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, backend.Id), request);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
            ITensor mask = outputs.GetRequired("fetch_name_0");
            Assert.AreEqual(4, mask.Shape.Rank);
            Assert.AreEqual(2L, mask.Shape[0]);
            string[] outputRowSha256 = Enumerable.Range(0, 2).Select(row => TensorRowSha256(mask, row)).ToArray();
            Assert.AreNotEqual(outputRowSha256[0], outputRowSha256[1], backend.Name + " returned duplicate raw seal-mask rows for distinct images.");

            var decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as PaddleDocumentSealBatchResult
                ?? throw new AssertFailedException(modelId + " did not return a PaddleDocumentSealBatchResult for batch=2.");
            Assert.AreEqual(2, decoded.Count);
            var resultRows = new List<object>(decoded.Count);
            for (int row = 0; row < decoded.Count; row++)
            {
                PaddleDocumentSealResult result = decoded[row];
                Assert.AreEqual(row, result.Metadata.PageIndex, backend.Name + " changed seal batch row order.");
                Assert.AreEqual(imageHashes[row], result.Metadata.InputSha256, backend.Name + " mapped seal output to the wrong source image.");
                Assert.IsTrue(result.MaskWidth > 0 && result.MaskHeight > 0);
                resultRows.Add(new
                {
                    row,
                    input = imagePaths[row],
                    inputSha256 = result.Metadata.InputSha256,
                    pageIndex = result.Metadata.PageIndex,
                    mask = new { width = result.MaskWidth, height = result.MaskHeight },
                    regionCount = result.Regions.Count,
                    regionSummarySha256 = SealRegionsSha256(result)
                });
            }

            return new
            {
                backend = backend.Name,
                model = modelId,
                modelSha256 = FileSha256(modelPath),
                inputShape = input.Tensor.Shape.ToString(),
                inputRowSha256,
                outputShape = mask.Shape.ToString(),
                outputRowSha256,
                resultRows,
                status = "passed"
            };
        }

        private static string SealRegionsSha256(PaddleDocumentSealResult result)
        {
            var canonical = new StringBuilder(result.Regions.Count * 96);
            foreach (PaddleDocumentRegion region in result.Regions)
                canonical.Append(region.Category).Append('|')
                    .Append(region.Score.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(region.Bounds.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(region.Bounds.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(region.Bounds.Width.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(region.Bounds.Height.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
        }

        private static object RunCase(
            BackendCase backend,
            ClassifierCase item,
            bool useDistinctHorizontalBands = false,
            Dictionary<string, float[][]>? ortRawOutputRowsByModel = null)
        {
            string path = Path.Combine(ModelRoot, item.FileName);
            RequireFile(path, item.ModelId);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateClassification(
                PaddleDocumentModelCatalog.Get(item.ModelId),
                item.Labels,
                item.Task,
                modelSize: new VisualSize(224, 224),
                maximumBatch: 2,
                allowDynamicBatch: true);
            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0], item.ModelId + " must expose a dynamic input batch axis.");
            Assert.AreEqual(-1L, profile.VisualProfile.Outputs[0].ShapePattern[0], item.ModelId + " must expose a dynamic output batch axis.");

            string imagePath = useDistinctHorizontalBands ? TableImagePath : ImagePath;
            var factory = new OpenCvVisualInputFactory();
            using OpenCvDecodedRoiImage source = factory.DecodeForRois(OpenCvImageSource.FromFile(imagePath));
            IVisualRoiGeometry[] regions;
            if (useDistinctHorizontalBands)
            {
                float halfHeight = source.SourceSize.Height / 2f;
                regions = new IVisualRoiGeometry[]
                {
                    new RectangleRoiGeometry(new RectangleF(0, 0, source.SourceSize.Width, halfHeight)),
                    new RectangleRoiGeometry(new RectangleF(0, halfHeight, source.SourceSize.Width, source.SourceSize.Height - halfHeight))
                };
            }
            else
            {
                var fullSource = new RectangleRoiGeometry(new RectangleF(0, 0, source.SourceSize.Width, source.SourceSize.Height));
                regions = new IVisualRoiGeometry[] { fullSource, fullSource };
            }
            using PreparedVisualInput input = factory.CreateRoiBatch(
                OpenCvImageSource.FromFile(imagePath),
                regions,
                profile.VisualProfile.Input.Name,
                profile.VisualProfile.Preprocessing!.ToOpenCvOptions(),
                inputId: "dynamic-batch-rows",
                cancellationToken: CancellationToken.None);
            Assert.AreEqual(2, input.BatchSize);
            Assert.AreEqual(2L, input.Tensor.Shape[0]);
            Assert.AreEqual(2, input.BatchFrames.Count);
            string[] inputRowSha256 = Enumerable.Range(0, input.BatchSize).Select(row => TensorRowSha256(input.Tensor, row)).ToArray();
            if (useDistinctHorizontalBands)
                Assert.AreNotEqual(inputRowSha256[0], inputRowSha256[1], backend.Name + " received duplicate classifier batch rows.");
            else
                Assert.AreEqual(inputRowSha256[0], inputRowSha256[1], backend.Name + " changed identical classifier rows.");

            using var registry = new BackendRegistry();
            BackendRequest request;
            if (backend.Id == OnnxRuntimeBackendProvider.BackendId)
            {
                registry.UseOnnxRuntime();
                request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            }
            else if (backend.Id == OpenVinoBackendProvider.BackendId)
            {
                registry.UseOpenVino();
                request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            }
            else
            {
                var contract = new OpenCvDnnModelContract(
                    profile.VisualProfile.ModelId,
                    new[] { new TensorDescriptor(profile.VisualProfile.Input.Name, profile.VisualProfile.Input.ElementType, profile.VisualProfile.Input.ShapePattern) },
                    profile.VisualProfile.Outputs.Select(output => new TensorDescriptor(output.Name, output.ElementType, output.ShapePattern)),
                    imageInputNames: new[] { profile.VisualProfile.Input.Name });
                registry.UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
                request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
            }

            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, backend.Id), request);
            var inputs = new List<NamedTensor>(1 + input.AuxiliaryInputs.Count) { new NamedTensor(input.InputName, input.Tensor) };
            inputs.AddRange(input.AuxiliaryInputs);
            InferenceOutputs outputs = session.Run(new InferenceInputs(inputs), CancellationToken.None);
            ITensor logits = outputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
            Assert.AreEqual(2L, logits.Shape[0], item.ModelId + " returned the wrong raw output batch size.");
            string[] outputRowSha256 = Enumerable.Range(0, 2).Select(row => TensorRowSha256(logits, row)).ToArray();
            float[][] rawOutputRows = TensorRows(logits, 2);
            if (useDistinctHorizontalBands)
                Assert.AreNotEqual(outputRowSha256[0], outputRowSha256[1], backend.Name + " returned duplicate raw classifier rows for distinct regions.");
            else
                Assert.AreEqual(outputRowSha256[0], outputRowSha256[1], backend.Name + " changed raw outputs for identical classifier rows.");
            object? numericParity = null;
            if (ortRawOutputRowsByModel is not null)
            {
                if (backend.Id == OnnxRuntimeBackendProvider.BackendId)
                {
                    Assert.IsFalse(ortRawOutputRowsByModel.ContainsKey(item.ModelId), "The ONNX Runtime reference output was recorded twice for " + item.ModelId + ".");
                    ortRawOutputRowsByModel.Add(item.ModelId, rawOutputRows);
                    numericParity = new
                    {
                        referenceBackend = backend.Name,
                        maximumAbsoluteTolerance = ClassifierCrossBackendMaxAbsoluteTolerance,
                        maximumAbsoluteDifferencePerRow = new[] { 0d, 0d },
                        status = "reference"
                    };
                }
                else
                {
                    Assert.IsTrue(ortRawOutputRowsByModel.TryGetValue(item.ModelId, out float[][]? referenceRows), "ONNX Runtime reference output is missing for " + item.ModelId + ".");
                    Assert.AreEqual(referenceRows!.Length, rawOutputRows.Length, backend.Name + " returned a different classifier batch row count.");
                    var maximumAbsoluteDifferencePerRow = new double[rawOutputRows.Length];
                    for (int row = 0; row < rawOutputRows.Length; row++)
                    {
                        maximumAbsoluteDifferencePerRow[row] = MaxAbsoluteDifference(referenceRows[row], rawOutputRows[row]);
                        Assert.IsTrue(
                            maximumAbsoluteDifferencePerRow[row] <= ClassifierCrossBackendMaxAbsoluteTolerance,
                            backend.Name + " raw classifier row " + row + " differs from ONNX Runtime by " + maximumAbsoluteDifferencePerRow[row].ToString("R", CultureInfo.InvariantCulture) +
                            ", exceeding absolute tolerance " + ClassifierCrossBackendMaxAbsoluteTolerance.ToString("R", CultureInfo.InvariantCulture) + ".");
                    }

                    numericParity = new
                    {
                        referenceBackend = "onnxruntime-cpu",
                        maximumAbsoluteTolerance = ClassifierCrossBackendMaxAbsoluteTolerance,
                        maximumAbsoluteDifferencePerRow,
                        status = "passed"
                    };
                }
            }
            var decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as ClassificationBatchResult
                ?? throw new AssertFailedException(item.ModelId + " did not return a ClassificationBatchResult.");
            Assert.AreEqual(2, decoded.Count, item.ModelId + " returned an unexpected batch row count.");
            LabelScore first = decoded[0].TopPrediction ?? throw new AssertFailedException(item.ModelId + " returned no first prediction.");
            LabelScore second = decoded[1].TopPrediction ?? throw new AssertFailedException(item.ModelId + " returned no second prediction.");
            if (!useDistinctHorizontalBands)
            {
                Assert.AreEqual(first.Label, second.Label, item.ModelId + " changed labels between identical batch rows.");
                Assert.AreEqual(first.Score, second.Score, .00001f, item.ModelId + " changed scores between identical batch rows.");
            }

            Console.WriteLine("PADDLE_DOCUMENT_DYNAMIC_BATCH backend=" + backend.Name + ";model=" + item.ModelId + ";batch=" + input.BatchSize + ";label=" + first.Label + ";score=" + first.Score);
            return new
            {
                backend = backend.Name,
                model = item.ModelId,
                modelSha256 = FileSha256(path),
                inputShape = input.Tensor.Shape.ToString(),
                inputBatch = input.BatchSize,
                inputRowSha256,
                outputNames = outputs.Select(value => value.Name).ToArray(),
                rawOutputShape = logits.Shape.ToString(),
                rawOutputRowSha256 = outputRowSha256,
                rawOutputRows,
                numericParity,
                resultCount = decoded.Count,
                labels = decoded.Results.Select(value => value.TopPrediction!.Label).ToArray(),
                scores = decoded.Results.Select(value => value.TopPrediction!.Score).ToArray(),
                status = "passed"
            };
        }

        private sealed class BackendCase
        {
            public BackendCase(string name, BackendId id) { Name = name; Id = id; }
            public string Name { get; }
            public BackendId Id { get; }
        }

        private sealed class ClassifierCase
        {
            public ClassifierCase(string modelId, string fileName, VisualTaskId task, IReadOnlyList<string> labels)
            {
                ModelId = modelId; FileName = fileName; Task = task; Labels = labels;
            }
            public string ModelId { get; }
            public string FileName { get; }
            public VisualTaskId Task { get; }
            public IReadOnlyList<string> Labels { get; }
        }

        private sealed class TableBackendCase
        {
            public TableBackendCase(string name, BackendId id, string modelId, string modelPath) { Name = name; Id = id; ModelId = modelId; ModelPath = modelPath; }
            public string Name { get; }
            public BackendId Id { get; }
            public string ModelId { get; }
            public string ModelPath { get; }
        }

        private static object RunSlaNextCase(TableBackendCase backend)
        {
            RequireFile(backend.ModelPath, backend.Name + " SLANeXt model");
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(backend.ModelId);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateTableStructure(descriptor, modelSize: new VisualSize(512, 512), maximumBatch: 2);
            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0]);
            Assert.AreEqual(-1L, profile.VisualProfile.Outputs[0].ShapePattern[0]);
            Assert.AreEqual(-1L, profile.VisualProfile.Outputs[1].ShapePattern[0]);

            var factory = new OpenCvVisualInputFactory();
            using PreparedVisualInput probe = factory.CreateFromFile(TableImagePath, profile.VisualProfile, inputId: "slanext-dynamic-batch-probe");
            var fullSource = new RectangleRoiGeometry(new RectangleF(0, 0, probe.SourceSize.Width, probe.SourceSize.Height));
            using PreparedVisualInput input = factory.CreateRoiBatch(
                OpenCvImageSource.FromFile(TableImagePath),
                new IVisualRoiGeometry[] { fullSource, fullSource },
                profile.VisualProfile.Input.Name,
                profile.VisualProfile.Preprocessing!.ToOpenCvOptions(),
                inputId: "slanext-dynamic-batch-rows",
                cancellationToken: CancellationToken.None);
            Assert.AreEqual(2, input.BatchSize);
            Assert.AreEqual(2L, input.Tensor.Shape[0]);

            using var registry = new BackendRegistry();
            BackendRequest request;
            if (backend.Id == OnnxRuntimeBackendProvider.BackendId)
            {
                registry.UseOnnxRuntime();
                request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            }
            else
            {
                registry.UseOpenVino();
                request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            }

            ModelArtifact artifact = backend.Id == OpenVinoBackendProvider.BackendId
                ? profile.CreateArtifactWithSha256(backend.ModelPath, FileSha256(backend.ModelPath), backend.Id)
                : profile.CreateArtifact(backend.ModelPath, backend.Id);
            using IInferenceSession session = registry.CreateSession(artifact, request);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
            var decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as PaddleDocumentTableBatchResult
                ?? throw new AssertFailedException(backend.Name + " did not return a PaddleDocumentTableBatchResult.");
            Assert.AreEqual(2, decoded.Count);
            Assert.IsTrue(decoded[0].Tokens.Count > 10, backend.Name + " returned an empty first table structure.");
            Assert.AreEqual(decoded[0].Tokens.Count, decoded[1].Tokens.Count, backend.Name + " changed token count between identical batch rows.");
            Assert.AreEqual(decoded[0].Markup, decoded[1].Markup, backend.Name + " changed HTML between identical batch rows.");
            CollectionAssert.AreEqual(decoded[0].Tokens.Select(value => value.Index).ToArray(), decoded[1].Tokens.Select(value => value.Index).ToArray());

            Console.WriteLine("PADDLE_DOCUMENT_SLANEXT_DYNAMIC_BATCH model=" + descriptor.ModelId + ";backend=" + backend.Name + ";batch=" + input.BatchSize + ";tokens=" + decoded[0].Tokens.Count + ";cells=" + decoded[0].Regions.Count);
            return new
            {
                backend = backend.Name,
                model = descriptor.ModelId,
                modelPath = backend.ModelPath,
                modelSha256 = FileSha256(backend.ModelPath),
                inputShape = input.Tensor.Shape.ToString(),
                inputBatch = input.BatchSize,
                outputNames = outputs.Select(value => value.Name).ToArray(),
                resultCount = decoded.Count,
                tokenCounts = decoded.Items.Select(value => value.Tokens.Count).ToArray(),
                cellCounts = decoded.Items.Select(value => value.Regions.Count).ToArray(),
                markupSha256 = decoded.Items.Select(value => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Markup))).ToLowerInvariant()).ToArray(),
                status = "passed"
            };
        }

        private static void RequireFile(string path, string description)
        {
            if (!File.Exists(path)) Assert.Inconclusive("Missing " + description + ": " + path);
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL=1 to run the authorized local dynamic-batch evidence.");
        }

        private static PaddleDocumentFormulaResult RunFormulaRow(IInferenceSession session, PaddleDocumentProfile profile, PreparedVisualInput input)
        {
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
            return profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as PaddleDocumentFormulaResult
                ?? throw new AssertFailedException("The formula decoder did not return a single-row result.");
        }

        private static OpenCvBgrImage CropHorizontalBand(OpenCvBgrImage image, int top, int height, string inputId)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (top < 0 || height <= 0 || top > image.Height - height) throw new ArgumentOutOfRangeException(nameof(top));
            int rowBytes = checked(image.Width * 3);
            var pixels = new byte[checked(rowBytes * height)];
            Buffer.BlockCopy(image.GetReadOnlyInteropBuffer(), checked(top * rowBytes), pixels, 0, pixels.Length);
            return new OpenCvBgrImage(image.Width, height, pixels, TensorBufferOwnership.Transfer, inputId);
        }

        private static int FindTokenId(IReadOnlyList<string> tokens, string value)
        {
            for (int index = 0; index < tokens.Count; index++)
                if (string.Equals(tokens[index], value, StringComparison.Ordinal)) return index;
            return -1;
        }

        private static string Sha256Text(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        private static string FileSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static string TensorSha256(ITensor tensor)
        {
            if (!(tensor.Buffer is float[] values)) throw new AssertFailedException("The prepared dynamic batch must be Float32.");
            return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(values.AsSpan()))).ToLowerInvariant();
        }

        private static string TensorRowSha256(ITensor tensor, int row)
        {
            if (!(tensor.Buffer is float[] values)) throw new AssertFailedException("The prepared dynamic batch must be Float32.");
            int batchSize = checked((int)tensor.Shape[0]);
            if (row < 0 || row >= batchSize || values.Length % batchSize != 0) throw new ArgumentOutOfRangeException(nameof(row));
            int rowLength = values.Length / batchSize;
            return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(values.AsSpan(row * rowLength, rowLength)))).ToLowerInvariant();
        }

        private static float[][] TensorRows(ITensor tensor, int batchSize)
        {
            if (!(tensor.Buffer is float[] values)) throw new AssertFailedException("The classifier output must be Float32.");
            if (tensor.Shape.Rank == 0 || tensor.Shape[0] != batchSize || values.Length % batchSize != 0)
                throw new AssertFailedException("The classifier output does not contain the expected batch rows.");
            int rowLength = values.Length / batchSize;
            return Enumerable.Range(0, batchSize)
                .Select(row => values.AsSpan(row * rowLength, rowLength).ToArray())
                .ToArray();
        }

        private static string DetectionRowSha256(DetectionResult result)
        {
            var canonical = new StringBuilder(result.Detections.Count * 96);
            foreach (Detection detection in result.Detections)
            {
                canonical.Append(detection.Label.Label).Append('|')
                    .Append(detection.Label.Score.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.Width.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(detection.Box.Height.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
        }

        private static double MeanAbsoluteDifference(float[] left, int leftOffset, float[] right, int rightOffset, int length)
        {
            double sum = 0;
            for (int index = 0; index < length; index++) sum += Math.Abs((double)left[leftOffset + index] - right[rightOffset + index]);
            return sum / length;
        }

        private static double MaxAbsoluteDifference(float[] left, float[] right)
        {
            double max = 0;
            for (int index = 0; index < left.Length; index++) max = Math.Max(max, Math.Abs((double)left[index] - right[index]));
            return max;
        }
    }
}
