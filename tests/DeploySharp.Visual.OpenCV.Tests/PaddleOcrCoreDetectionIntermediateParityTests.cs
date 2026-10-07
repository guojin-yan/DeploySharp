using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>
    /// Compares the raw DB detector output for every local PP-OCR v4/v5/v6
    /// detector on the same three images in ORT CPU and OpenVINO CPU.
    /// This is an intermediate tensor contract, not a detection quality score.
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrCoreDetectionIntermediateParityTests
    {
        private const string ImageRootDefault = @"E:\Data\ocr";
        private const string ModelRootDefault = @"E:\Model\paddleocr";
        private const string DatasetRootDefault = @"F:\OCRBenchmarkTesting";
        private const double MaximumAbsDifferenceDefault = 0.01;
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AllCoreDetectorsMatchAcrossOrtAndOpenVinoOnThreeImages()
        {
            RequireExternal();
            string imageRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_IMAGE_ROOT") ?? ImageRootDefault;
            string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault;
            double maximumAbsDifference = ParseDouble(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_MAX_ABS"), MaximumAbsDifferenceDefault);
            string[] images =
            {
                RequireFile(Path.Combine(imageRoot, "demo_1.jpg")),
                RequireFile(Path.Combine(imageRoot, "demo_2.jpg")),
                RequireFile(Path.Combine(imageRoot, "demo_3.jpg"))
            };

            var records = new List<DetectorRecord>();
            foreach (DetectorCase testCase in Cases)
            {
                string modelPath = RequireFile(Path.Combine(modelRoot, testCase.Folder, testCase.FileName));
                PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.ModelId));
                PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, Artifact(descriptor), maximumBatch: 1);
                BackendRun ort = RunBackend(profile, modelPath, images, openVino: false);
                BackendRun openVino = RunBackend(profile, modelPath, images, openVino: true);
                Assert.AreEqual(ort.Images.Count, openVino.Images.Count, testCase.Name + " image count differs.");

                var imageRecords = new List<ImageRecord>();
                double maximumObservedAbs = 0;
                double maximumObservedMean = 0;
                int inputMismatches = 0;
                int shapeMismatches = 0;
                for (int index = 0; index < ort.Images.Count; index++)
                {
                    DetectorImageRun left = ort.Images[index];
                    DetectorImageRun right = openVino.Images[index];
                    Assert.AreEqual(left.ImagePath, right.ImagePath, testCase.Name + " image ordering differs.");
                    if (!string.Equals(left.InputSha256, right.InputSha256, StringComparison.Ordinal)) inputMismatches++;
                    if (!left.OutputShape.SequenceEqual(right.OutputShape)) shapeMismatches++;
                    Assert.AreEqual(left.OutputValues.Length, right.OutputValues.Length, testCase.Name + " output length differs for " + left.ImagePath + ".");

                    double sum = 0;
                    double maximum = 0;
                    for (int valueIndex = 0; valueIndex < left.OutputValues.Length; valueIndex++)
                    {
                        double delta = Math.Abs(left.OutputValues[valueIndex] - right.OutputValues[valueIndex]);
                        sum += delta;
                        if (delta > maximum) maximum = delta;
                    }
                    double mean = left.OutputValues.Length == 0 ? 0 : sum / left.OutputValues.Length;
                    maximumObservedAbs = Math.Max(maximumObservedAbs, maximum);
                    maximumObservedMean = Math.Max(maximumObservedMean, mean);
                    imageRecords.Add(new ImageRecord(
                        Path.GetFileName(left.ImagePath),
                        left.InputSha256,
                        left.OutputShape,
                        left.OutputSha256,
                        right.OutputSha256,
                        maximum,
                        mean));
                }

                Assert.AreEqual(0, inputMismatches, testCase.Name + " preprocessing input differs across backends.");
                Assert.AreEqual(0, shapeMismatches, testCase.Name + " output shape differs across backends.");
                Assert.IsTrue(maximumObservedAbs <= maximumAbsDifference,
                    testCase.Name + " raw detector drift exceeded " + maximumAbsDifference.ToString("R", CultureInfo.InvariantCulture) + ": " + maximumObservedAbs.ToString("R", CultureInfo.InvariantCulture));
                records.Add(new DetectorRecord(
                    testCase.Name,
                    testCase.ModelId,
                    modelPath,
                    Sha256File(modelPath),
                    maximumAbsDifference,
                    inputMismatches,
                    shapeMismatches,
                    maximumObservedAbs,
                    maximumObservedMean,
                    imageRecords));

                Console.WriteLine("PADDLEOCR_DET_PARITY variant=" + testCase.Name
                    + ";images=" + imageRecords.Count.ToString(CultureInfo.InvariantCulture)
                    + ";inputMismatches=" + inputMismatches.ToString(CultureInfo.InvariantCulture)
                    + ";shapeMismatches=" + shapeMismatches.ToString(CultureInfo.InvariantCulture)
                    + ";maximumAbs=" + maximumObservedAbs.ToString("R", CultureInfo.InvariantCulture)
                    + ";maximumMean=" + maximumObservedMean.ToString("R", CultureInfo.InvariantCulture));
            }

            string? evidencePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(evidencePath)) WriteEvidence(evidencePath!, images, modelRoot, maximumAbsDifference, records);
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void AllCoreDetectorsHaveProbabilityMapParityOnTheHierTextHoldout()
        {
            RequireExternal();
            string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_DATASET_ROOT") ?? DatasetRootDefault);
            string modelRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault);
            string manifestPath = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_MANIFEST")
                ?? Path.Combine(datasetRoot, "data", "annotations", "manifests", "hiertext-validation-sample-003.jsonl"));
            string[] images = LoadHoldoutImages(manifestPath, datasetRoot);
            Assert.AreEqual(24, images.Length, "The pinned sample-003 probability-map holdout must contain 24 unique images.");
            double threshold = new PaddleDbPostprocessOptions().ProbabilityThreshold;
            double maximumAbsDifference = ParseDouble(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_MAX_ABS"), MaximumAbsDifferenceDefault);

            var modelRecords = new List<HoldoutDetectorRecord>(Cases.Length);
            foreach (DetectorCase testCase in Cases)
            {
                string modelPath = RequireFile(Path.Combine(modelRoot, testCase.Folder, testCase.FileName));
                PaddleOcrModelDescriptor descriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.ModelId));
                PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
                string modelSha256 = Sha256File(modelPath);
                Assert.AreEqual(release.Sha256, modelSha256, testCase.Name + " model differs from the catalog-pinned artifact.");
                PaddleOcrProfile profile = PaddleOcrModelCatalog.CreateProfile(descriptor, Artifact(descriptor), maximumBatch: 1);
                HoldoutDetectorRecord modelRecord = CompareHoldoutDetector(testCase, profile, modelPath, modelSha256, images, threshold);
                Assert.IsTrue(modelRecord.MaximumAbsDifference <= maximumAbsDifference,
                    testCase.Name + " holdout probability-map drift exceeded " + maximumAbsDifference.ToString("R", CultureInfo.InvariantCulture)
                    + ": " + modelRecord.MaximumAbsDifference.ToString("R", CultureInfo.InvariantCulture));
                modelRecords.Add(modelRecord);
                Console.WriteLine("PADDLEOCR_DET_HOLDOUT_PARITY variant=" + testCase.Name
                    + ";images=" + modelRecord.Images.Count.ToString(CultureInfo.InvariantCulture)
                    + ";comparedElements=" + modelRecord.ComparedElements.ToString(CultureInfo.InvariantCulture)
                    + ";maxAbsDiff=" + modelRecord.MaximumAbsDifference.ToString("R", CultureInfo.InvariantCulture)
                    + ";meanAbsDiff=" + modelRecord.MeanAbsDifference.ToString("R", CultureInfo.InvariantCulture)
                    + ";thresholdMismatches=" + modelRecord.ThresholdMismatchElements.ToString(CultureInfo.InvariantCulture));
            }

            string outputPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_HOLDOUT_EVIDENCE_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddleocr-core-detector-probability-map-parity-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + ".json");
            WriteHoldoutEvidence(outputPath, manifestPath, modelRoot, images, threshold, maximumAbsDifference, modelRecords);
            TestContext.AddResultFile(Path.GetFullPath(outputPath));
        }

        private static readonly DetectorCase[] Cases =
        {
            new DetectorCase("v4-mobile", "paddleocr/ppocrv4/mobile-det", "PP-OCRv4", "PP-OCRv4_mobile_det.onnx"),
            new DetectorCase("v4-server", "paddleocr/ppocrv4/server-det", "PP-OCRv4", "PP-OCRv4_server_det.onnx"),
            new DetectorCase("v5-mobile", "paddleocr/ppocrv5/mobile-det", "PP-OCRv5", "PP-OCRv5_mobile_det.onnx"),
            new DetectorCase("v5-server", "paddleocr/ppocrv5/server-det", "PP-OCRv5", "PP-OCRv5_server_det.onnx"),
            new DetectorCase("v6-tiny", "paddleocr/ppocrv6/tiny-det", Path.Combine("PP-OCRv6", "tiny"), "PP-OCRv6_tiny_det_inference.onnx"),
            new DetectorCase("v6-small", "paddleocr/ppocrv6/small-det", Path.Combine("PP-OCRv6", "small"), "PP-OCRv6_small_det_inference.onnx"),
            new DetectorCase("v6-medium", "paddleocr/ppocrv6/medium-det", Path.Combine("PP-OCRv6", "medium"), "PP-OCRv6_medium_det_inference.onnx")
        };

        private static HoldoutDetectorRecord CompareHoldoutDetector(
            DetectorCase testCase,
            PaddleOcrProfile profile,
            string modelPath,
            string modelSha256,
            IReadOnlyList<string> images,
            double threshold)
        {
            using var ortBackends = new BackendRegistry();
            ortBackends.UseOnnxRuntime();
            using var openVinoBackends = new BackendRegistry();
            openVinoBackends.UseOpenVino();
            using IInferenceSession ortSession = ortBackends.CreateSession(
                profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId),
                new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"),
                new SessionOptions(1, false));
            using IInferenceSession openVinoSession = openVinoBackends.CreateSession(
                profile.CreateArtifact(modelPath, OpenVinoBackendProvider.BackendId),
                new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"),
                new SessionOptions(1, false));

            var factory = new OpenCvVisualInputFactory();
            var imageRecords = new List<HoldoutImageRecord>(images.Count);
            double totalAbsoluteDifference = 0;
            double maximumAbsoluteDifference = 0;
            long comparedElements = 0;
            long differentElements = 0;
            long thresholdMismatchElements = 0;
            long ortPositiveOpenVinoNegative = 0;
            long ortNegativeOpenVinoPositive = 0;

            foreach (string imagePath in images)
            {
                string imageSha256 = Sha256File(imagePath);
                VisualSize sourceSize;
                using (PreparedVisualInput probe = factory.CreateFromFile(
                    imagePath,
                    "probe",
                    new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
                    sourceSize = probe.SourceSize;

                using PreparedVisualInput input = factory.CreateFromFile(
                    imagePath,
                    profile.VisualProfile.Input.Name,
                    OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
                string inputSha256 = TensorSha(input.Tensor);
                InferenceOutputs ortOutputs = ortSession.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
                Assert.AreEqual(inputSha256, TensorSha(input.Tensor), testCase.Name + " ORT modified the shared input tensor for " + Path.GetFileName(imagePath) + ".");
                ITensor ortOutput = ortOutputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
                InferenceOutputs openVinoOutputs = openVinoSession.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
                Assert.AreEqual(inputSha256, TensorSha(input.Tensor), testCase.Name + " OpenVINO did not receive the unchanged shared input tensor for " + Path.GetFileName(imagePath) + ".");
                ITensor openVinoOutput = openVinoOutputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
                long[] ortShape = ortOutput.Shape.ToArray();
                long[] openVinoShape = openVinoOutput.Shape.ToArray();
                CollectionAssert.AreEqual(ortShape, openVinoShape, testCase.Name + " output shapes differ for " + Path.GetFileName(imagePath) + ".");
                Assert.AreEqual(TensorElementType.Float32, ortOutput.ElementType, testCase.Name + " ORT output must be Float32.");
                Assert.AreEqual(TensorElementType.Float32, openVinoOutput.ElementType, testCase.Name + " OpenVINO output must be Float32.");
                float[] ortValues = ortOutput.Buffer as float[] ?? throw new InvalidOperationException(testCase.Name + " ORT output is not Float32[].");
                float[] openVinoValues = openVinoOutput.Buffer as float[] ?? throw new InvalidOperationException(testCase.Name + " OpenVINO output is not Float32[].");
                Assert.AreEqual(ortValues.Length, openVinoValues.Length, testCase.Name + " output element counts differ.");

                double imageAbsoluteDifferenceSum = 0;
                double imageMaximumAbsoluteDifference = 0;
                long imageDifferentElements = 0;
                long imageThresholdMismatchElements = 0;
                long imageOrtPositiveOpenVinoNegative = 0;
                long imageOrtNegativeOpenVinoPositive = 0;
                for (int index = 0; index < ortValues.Length; index++)
                {
                    float ortValue = ortValues[index];
                    float openVinoValue = openVinoValues[index];
                    Assert.IsTrue(float.IsFinite(ortValue) && float.IsFinite(openVinoValue), testCase.Name + " output contains a non-finite value.");
                    double difference = Math.Abs((double)ortValue - openVinoValue);
                    imageAbsoluteDifferenceSum += difference;
                    imageMaximumAbsoluteDifference = Math.Max(imageMaximumAbsoluteDifference, difference);
                    if (ortValue != openVinoValue) imageDifferentElements++;

                    bool ortPositive = ortValue >= threshold;
                    bool openVinoPositive = openVinoValue >= threshold;
                    if (ortPositive != openVinoPositive)
                    {
                        imageThresholdMismatchElements++;
                        if (ortPositive) imageOrtPositiveOpenVinoNegative++;
                        else imageOrtNegativeOpenVinoPositive++;
                    }
                }

                totalAbsoluteDifference += imageAbsoluteDifferenceSum;
                maximumAbsoluteDifference = Math.Max(maximumAbsoluteDifference, imageMaximumAbsoluteDifference);
                comparedElements += ortValues.Length;
                differentElements += imageDifferentElements;
                thresholdMismatchElements += imageThresholdMismatchElements;
                ortPositiveOpenVinoNegative += imageOrtPositiveOpenVinoNegative;
                ortNegativeOpenVinoPositive += imageOrtNegativeOpenVinoPositive;
                imageRecords.Add(new HoldoutImageRecord(
                    Path.GetFileName(imagePath),
                    imageSha256,
                    inputSha256,
                    ortShape,
                    TensorSha(ortOutput),
                    TensorSha(openVinoOutput),
                    ortValues.Length,
                    imageMaximumAbsoluteDifference,
                    imageAbsoluteDifferenceSum / ortValues.Length,
                    imageDifferentElements,
                    imageThresholdMismatchElements,
                    imageOrtPositiveOpenVinoNegative,
                    imageOrtNegativeOpenVinoPositive));
            }

            return new HoldoutDetectorRecord(
                testCase.Name,
                testCase.ModelId,
                modelPath,
                modelSha256,
                images.Count,
                comparedElements,
                maximumAbsoluteDifference,
                comparedElements == 0 ? 0 : totalAbsoluteDifference / comparedElements,
                differentElements,
                thresholdMismatchElements,
                ortPositiveOpenVinoNegative,
                ortNegativeOpenVinoPositive,
                imageRecords);
        }

        private static string[] LoadHoldoutImages(string manifestPath, string datasetRoot)
        {
            RequireFile(manifestPath);
            var images = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in File.ReadLines(manifestPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement row = document.RootElement;
                string id = row.GetProperty("image_id").GetString() ?? throw new InvalidDataException("The HierText holdout image_id is missing.");
                string relativePath = row.GetProperty("image_relpath").GetString() ?? throw new InvalidDataException("The HierText holdout image_relpath is missing.");
                Assert.IsTrue(ids.Add(id), "The HierText holdout contains duplicate image IDs: " + id);
                images.Add(RequireFile(Path.GetFullPath(Path.Combine(datasetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))));
            }
            return images.ToArray();
        }

        private static void WriteHoldoutEvidence(
            string path,
            string manifestPath,
            string modelRoot,
            IReadOnlyList<string> images,
            double threshold,
            double maximumAbsDifference,
            IReadOnlyList<HoldoutDetectorRecord> records)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var evidence = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceRevision = ResolveSourceRevision(),
                sourceWorkingTreeDirty = true,
                runtime = new
                {
                    onnxRuntime = new { device = "CPU", package = "Microsoft.ML.OnnxRuntime", version = "1.28.0" },
                    openVino = new { device = "CPU", package = "OpenVINO.runtime.win", version = "2026.2.1" },
                    targetFramework = ".NET " + Environment.Version.ToString(),
                    operatingSystem = Environment.OSVersion.VersionString
                },
                dataset = new
                {
                    source = "HierText validation sample-003; local smoke-only selection",
                    manifestPath,
                    manifestSha256 = Sha256File(manifestPath),
                    images = images.Count,
                    imageDigests = images.Select(image => new { fileName = Path.GetFileName(image), sha256 = Sha256File(image) }).ToArray(),
                    redistribution = "Images, labels and predictions are not included in this repository or a Release."
                },
                protocol = new
                {
                    sharedInput = "Each image is decoded and preprocessed once per detector; the same Float32 tensor instance is run sequentially by both Sessions and its SHA-256 is checked after each inference.",
                    output = "Raw DB probability outputs must have identical Float32 shapes and finite values.",
                    probabilityThreshold = threshold,
                    maximumAbsDifferenceTolerance = maximumAbsDifference,
                    metrics = new[] { "maxAbsDiff", "meanAbsDiff", "differentElements", "thresholdMismatchElements", "ortPositiveOpenVinoNegative", "ortNegativeOpenVinoPositive" },
                    interpretation = "Runtime tensor-contract diagnostics only; threshold mismatch pixels are not detection recall, precision or accuracy."
                },
                summary = new
                {
                    detectorCount = records.Count,
                    imageCount = images.Count,
                    detectorImagePairs = records.Sum(record => record.Images.Count),
                    comparedElements = records.Sum(record => record.ComparedElements),
                    maximumAbsDiff = records.Count == 0 ? 0 : records.Max(record => record.MaximumAbsDifference),
                    maximumMeanAbsDiff = records.Count == 0 ? 0 : records.Max(record => record.MeanAbsDifference),
                    differentElements = records.Sum(record => record.DifferentElements),
                    thresholdMismatchElements = records.Sum(record => record.ThresholdMismatchElements),
                    maximumAbsDifferenceTolerance = maximumAbsDifference,
                    passed = records.All(record => record.MaximumAbsDifference <= maximumAbsDifference)
                },
                detectors = records
            };
            File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + Environment.NewLine, new UTF8Encoding(false));
        }

        private sealed record HoldoutImageRecord(
            string Image,
            string ImageSha256,
            string InputTensorSha256,
            IReadOnlyList<long> OutputShape,
            string OrtOutputSha256,
            string OpenVinoOutputSha256,
            int ComparedElements,
            double MaximumAbsDifference,
            double MeanAbsDifference,
            long DifferentElements,
            long ThresholdMismatchElements,
            long OrtPositiveOpenVinoNegative,
            long OrtNegativeOpenVinoPositive);

        private sealed record HoldoutDetectorRecord(
            string Variant,
            string ModelId,
            string ModelPath,
            string ModelSha256,
            int ImageCount,
            long ComparedElements,
            double MaximumAbsDifference,
            double MeanAbsDifference,
            long DifferentElements,
            long ThresholdMismatchElements,
            long OrtPositiveOpenVinoNegative,
            long OrtNegativeOpenVinoPositive,
            IReadOnlyList<HoldoutImageRecord> Images);

        private static BackendRun RunBackend(PaddleOcrProfile profile, string modelPath, IReadOnlyList<string> images, bool openVino)
        {
            BackendId backendId = openVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            string device = openVino ? "CPU" : "cpu";
            using var backends = new BackendRegistry();
            if (openVino) backends.UseOpenVino(); else backends.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
            using IInferenceSession session = backends.CreateSession(profile.CreateArtifact(modelPath, backendId), request, new SessionOptions(1, false));
            var runs = new List<DetectorImageRun>();
            foreach (string imagePath in images)
            {
                VisualSize sourceSize;
                using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr)))
                    sourceSize = probe.SourceSize;
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(
                    imagePath,
                    profile.VisualProfile.Input.Name,
                    OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(profile.VisualProfile.Input.Name, input.Tensor), CancellationToken.None);
                ITensor output = outputs.GetRequired(profile.VisualProfile.Outputs[0].Name);
                float[] values = output.Buffer as float[] ?? throw new InvalidOperationException("The PaddleOCR detector output must be Float32.");
                runs.Add(new DetectorImageRun(imagePath, TensorSha(input.Tensor), output.Shape.ToArray(), values.ToArray(), TensorSha(output)));
            }
            return new BackendRun(runs);
        }

        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor)
        {
            PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
            return new PaddleOcrArtifactContract(
                release.Opset,
                release.Sha256,
                "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
                "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
                "Apache-2.0;external-artifact-redistribution-unverified",
                "ppocr-official-inference-v1",
                "deploysharp-paddleocr-db-ctc-v1");
        }

        private static void WriteEvidence(string path, IReadOnlyList<string> images, string modelRoot, double maximumAbsDifference, IReadOnlyList<DetectorRecord> records)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var evidence = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceRevision = ResolveSourceRevision(),
                runtime = new { left = "onnxruntime-cpu", right = "openvino-cpu" },
                modelRoot,
                protocol = new
                {
                    images = images.Select(Path.GetFileName).ToArray(),
                    combinations = records.Count * images.Count,
                    maximumAbsDifference,
                    input = "OpenCV input factory + PP-OCR official detection preprocessing",
                    comparison = "raw detector output element-wise maximum/mean absolute difference"
                },
                summary = new
                {
                    detectorCount = records.Count,
                    imageCount = images.Count,
                    combinations = records.Count * images.Count,
                    inputMismatches = records.Sum(record => record.InputMismatches),
                    shapeMismatches = records.Sum(record => record.ShapeMismatches),
                    maximumObservedAbs = records.Count == 0 ? 0 : records.Max(record => record.MaximumObservedAbs),
                    maximumObservedMean = records.Count == 0 ? 0 : records.Max(record => record.MaximumObservedMean),
                    passed = records.All(record => record.InputMismatches == 0 && record.ShapeMismatches == 0 && record.MaximumObservedAbs <= maximumAbsDifference)
                },
                inputs = images.Select(image => new { path = image, sha256 = Sha256File(image) }).ToArray(),
                detectors = records
            };
            File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        }

        private static string ResolveSourceRevision()
        {
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = AppContext.BaseDirectory
                    }
                };
                process.Start();
                string value = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return value;
            }
            catch { return "unknown"; }
        }

        private static string TensorSha(ITensor tensor)
        {
            if (!(tensor.Buffer is float[] values)) throw new InvalidOperationException("The PaddleOCR tensor must be Float32.");
            byte[] bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static string Sha256File(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static double ParseDouble(string? value, double fallback)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed > 0 ? parsed : fallback;

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR detector parity file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_DET_PARITY_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_DET_PARITY_RUN_EXTERNAL=1 to run the local detector parity matrix.");
        }

        private sealed record DetectorCase(string Name, string ModelId, string Folder, string FileName);
        private sealed record DetectorImageRun(string ImagePath, string InputSha256, IReadOnlyList<long> OutputShape, float[] OutputValues, string OutputSha256);
        private sealed record BackendRun(IReadOnlyList<DetectorImageRun> Images);
        private sealed record ImageRecord(string Image, string InputSha256, IReadOnlyList<long> OutputShape, string OrtOutputSha256, string OpenVinoOutputSha256, double MaximumAbsDifference, double MeanAbsDifference);
        private sealed record DetectorRecord(string Variant, string ModelId, string ModelPath, string ModelSha256, double MaximumAbsDifferenceThreshold, int InputMismatches, int ShapeMismatches, double MaximumObservedAbs, double MaximumObservedMean, IReadOnlyList<ImageRecord> Images);
    }
}
