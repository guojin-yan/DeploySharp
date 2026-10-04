using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
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
    /// <summary>Runs real dynamic-batch PP-Structure exports through ORT and OpenVINO. / 使用真实动态 Batch PP-Structure 导出在 ORT 和 OpenVINO 上运行。</summary>
    [TestClass]
    public sealed class PaddleDocumentDynamicBatchIntegrationTests
    {
        private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
        private const string ImagePath = @"E:\Data\image\bus.jpg";

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
            string path = Path.Combine(ModelRoot, "pp-doclayout-l.onnx");
            RequireFile(path, "PP-DocLayout-L dynamic model");
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
                descriptor,
                PaddleDocumentProfiles.Layout23Labels,
                new VisualSize(640, 640),
                includeGeometryInputs: true,
                maximumBatch: 2,
                scoreThreshold: 0);
            Assert.AreEqual(-1L, profile.VisualProfile.Input.ShapePattern[0]);
            Assert.AreEqual(-1L, profile.VisualProfile.AuxiliaryInputs[0].ShapePattern[0]);

            var factory = new OpenCvVisualInputFactory();
            using PreparedVisualInput probe = factory.CreateFromFile(ImagePath, profile.VisualProfile, inputId: "layout-dynamic-batch-probe");
            var fullSource = new RectangleRoiGeometry(new RectangleF(0, 0, probe.SourceSize.Width, probe.SourceSize.Height));
            using PreparedVisualInput raw = factory.CreateRoiBatch(
                OpenCvImageSource.FromFile(ImagePath),
                new IVisualRoiGeometry[] { fullSource, fullSource },
                profile.VisualProfile.Input.Name,
                profile.VisualProfile.Preprocessing!.ToOpenCvOptions(),
                inputId: "layout-dynamic-batch-rows",
                cancellationToken: CancellationToken.None);
            // Rebind the exact per-row geometry after the ROI batch knows source/model sizes.
            using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.RebindWithGeometryInputs(raw, profile);
            Assert.AreEqual(2, input.BatchSize);
            Assert.AreEqual(2L, input.Tensor.Shape[0]);
            Assert.AreEqual(2, input.AuxiliaryInputs.Count);
            foreach (NamedTensor auxiliary in input.AuxiliaryInputs) Assert.AreEqual(2L, auxiliary.Tensor.Shape[0], auxiliary.Name);

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
                ?? throw new AssertFailedException("PP-DocLayout-L did not return a DetectionBatchResult.");
            Assert.AreEqual(2, decoded.Count);
            Assert.IsTrue(decoded[0].Detections.Count > 0, backend.Name + " returned no layout detections.");
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

            Console.WriteLine("PADDLE_DOCUMENT_LAYOUT_DYNAMIC_BATCH backend=" + backend.Name + ";batch=" + input.BatchSize + ";detections=" + decoded[0].Detections.Count);
            return new
            {
                backend = backend.Name,
                model = descriptor.ModelId,
                modelSha256 = FileSha256(path),
                inputShape = input.Tensor.Shape.ToString(),
                inputBatch = input.BatchSize,
                auxiliaryShapes = input.AuxiliaryInputs.Select(value => new { name = value.Name, shape = value.Tensor.Shape.ToString() }).ToArray(),
                outputNames = outputs.Select(value => value.Name).ToArray(),
                resultCount = decoded.Count,
                detectionCounts = decoded.Results.Select(value => value.Detections.Count).ToArray(),
                firstRowLabels = decoded[0].Detections.Select(value => value.Label.Label).ToArray(),
                status = "passed"
            };
        }

        private static object RunCase(BackendCase backend, ClassifierCase item)
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

            var factory = new OpenCvVisualInputFactory();
            using PreparedVisualInput probe = factory.CreateFromFile(ImagePath, profile.VisualProfile, inputId: "dynamic-batch-probe");
            var fullSource = new RectangleRoiGeometry(new RectangleF(0, 0, probe.SourceSize.Width, probe.SourceSize.Height));
            using PreparedVisualInput input = factory.CreateRoiBatch(
                OpenCvImageSource.FromFile(ImagePath),
                new IVisualRoiGeometry[] { fullSource, fullSource },
                profile.VisualProfile.Input.Name,
                profile.VisualProfile.Preprocessing!.ToOpenCvOptions(),
                inputId: "dynamic-batch-rows",
                cancellationToken: CancellationToken.None);
            Assert.AreEqual(2, input.BatchSize);
            Assert.AreEqual(2L, input.Tensor.Shape[0]);
            Assert.AreEqual(2, input.BatchFrames.Count);

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
            var decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as ClassificationBatchResult
                ?? throw new AssertFailedException(item.ModelId + " did not return a ClassificationBatchResult.");
            Assert.AreEqual(2, decoded.Count, item.ModelId + " returned an unexpected batch row count.");
            LabelScore first = decoded[0].TopPrediction ?? throw new AssertFailedException(item.ModelId + " returned no first prediction.");
            LabelScore second = decoded[1].TopPrediction ?? throw new AssertFailedException(item.ModelId + " returned no second prediction.");
            Assert.AreEqual(first.Label, second.Label, item.ModelId + " changed labels between identical batch rows.");
            Assert.AreEqual(first.Score, second.Score, .00001f, item.ModelId + " changed scores between identical batch rows.");

            Console.WriteLine("PADDLE_DOCUMENT_DYNAMIC_BATCH backend=" + backend.Name + ";model=" + item.ModelId + ";batch=" + input.BatchSize + ";label=" + first.Label + ";score=" + first.Score);
            return new
            {
                backend = backend.Name,
                model = item.ModelId,
                modelSha256 = FileSha256(path),
                inputShape = input.Tensor.Shape.ToString(),
                inputBatch = input.BatchSize,
                outputNames = outputs.Select(value => value.Name).ToArray(),
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

        private static void RequireFile(string path, string description)
        {
            if (!File.Exists(path)) Assert.Inconclusive("Missing " + description + ": " + path);
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL=1 to run the authorized local dynamic-batch evidence.");
        }

        private static string FileSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
