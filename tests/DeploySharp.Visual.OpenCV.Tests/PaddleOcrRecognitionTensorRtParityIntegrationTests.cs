using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.TensorRT;
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
    /// Runs two real detector-derived text crops through ORT CPU and a freshly
    /// built TensorRT engine, then compares decoded CTC rows and token traces.
    /// This is intentionally opt-in because it loads local image/model/native
    /// assets and exercises the TensorRT CUDA bridge.
    /// / 使用真实检测裁剪在 ORT CPU 与新建 TensorRT Engine 上运行两行，比较 CTC 解码文本和 token 轨迹。
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrRecognitionTensorRtParityIntegrationTests
    {
        private const string ImageDefault = @"E:\Data\ocr\demo_1.jpg";
        private const string ModelRootDefault = @"E:\Model\paddleocr";
        private const string DetectorSha = "d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e";
        private const string RecognizerSha = "5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void RealDetectorCropsDecodeTheSameOnOrtAndTensorRtDynamicBatchTwo()
        {
            RequireExternal();
            string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_PARITY_IMAGE") ?? ImageDefault);
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? ModelRootDefault;
            string detectorPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_det_inference.onnx"));
            string recognizerPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_rec_inference.onnx"));
            string dictionaryPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_rec_dict.txt"));

            var detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/small-det"));
            var recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/small-rec"));
            PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorDescriptor), maximumBatch: 1);
            OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "trt-parity-v6-small", "v6-small", true);
            PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerDescriptor, characters.Sha256), characters, maximumBatch: 2);
            VisualSize sourceSize = ProbeSourceSize(imagePath);

            using var input = new OpenCvOcrImageInputFactory().CreateFromFile(
                imagePath,
                detector.VisualProfile.Input.Name,
                OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize),
                "trt-rec-parity");

            TextDetectionResult detection = RunDetector(detector, detectorPath, input.DetectionInput);
            Assert.IsTrue(detection.Regions.Count >= 2, "The real detector must provide at least two regions for batch row isolation.");
            IReadOnlyList<TextRegion> regions = detection.Regions.Take(2).ToArray();
            TextCropRequest[] requests = regions
                .Select(region => new TextCropRequest(region, recognizer.CropProfile!).WithTargetWidth(recognizer.CropProfile!.MaximumWidth))
                .ToArray();
            using PreparedVisualInput cropBatch = input.PrepareRecognitionBatch(recognizer.VisualProfile.Input.Name, requests, default);
            Assert.AreEqual(2, cropBatch.BatchSize);
            Assert.AreNotEqual(RowSha256(cropBatch, 0), RowSha256(cropBatch, 1), "The two detector-derived crop rows must not be identical.");

            RecognitionRun ort = RunRecognitionOnOrt(recognizer, recognizerPath, cropBatch);
            string engineRoot = Path.Combine(Path.GetTempPath(), "deploysharp-ppocr-trt-rec-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(engineRoot);
            try
            {
                string enginePath = Path.Combine(engineRoot, "ppocrv6-small-rec.engine");
                TensorRtOnnxEngineBuildResult build = BuildEngine(recognizer, recognizerPath, enginePath);
                RecognitionRun trt = RunRecognitionOnTensorRt(recognizer, build, cropBatch);
                Assert.AreEqual(2, trt.Result.Items.Count, "TensorRT must return one decoded result per crop row.");
                Assert.AreEqual(2, ort.Result.Items.Count, "ORT must return one decoded result per crop row.");
                var rows = new List<object>(2);
                for (int index = 0; index < 2; index++)
                {
                    RecognizedText ortRow = ort.Result.Items[index];
                    RecognizedText trtRow = trt.Result.Items[index];
                    Assert.AreEqual(ortRow.Text, trtRow.Text, "Decoded CTC text differs at batch row " + index + ".");
                    Assert.AreEqual(ortRow.Tokens.Count, trtRow.Tokens.Count, "CTC token trace length differs at batch row " + index + ".");
                    var emittedOrt = ortRow.Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray();
                    var emittedTrt = trtRow.Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex).ToArray();
                    CollectionAssert.AreEqual(emittedOrt, emittedTrt, "CTC emitted token sequence differs at batch row " + index + ".");
                    int classMismatches = 0;
                    int emittedFlagMismatches = 0;
                    int emittedClassMismatches = 0;
                    float maximumConfidenceDifference = 0;
                    for (int token = 0; token < ortRow.Tokens.Count; token++)
                    {
                        OcrToken ortToken = ortRow.Tokens[token];
                        OcrToken trtToken = trtRow.Tokens[token];
                        if (ortToken.ClassIndex != trtToken.ClassIndex) classMismatches++;
                        if (ortToken.Emitted != trtToken.Emitted) emittedFlagMismatches++;
                        if (ortToken.Emitted && trtToken.Emitted && ortToken.ClassIndex != trtToken.ClassIndex) emittedClassMismatches++;
                        maximumConfidenceDifference = Math.Max(maximumConfidenceDifference, Math.Abs(ortToken.Confidence - trtToken.Confidence));
                    }
                    Console.WriteLine("PADDLEOCR_TRT_REC_PARITY_ROW row=" + index + ";ortText=" + ortRow.Text + ";trtText=" + trtRow.Text + ";ortTokens=" + ortRow.Tokens.Count + ";trtTokens=" + trtRow.Tokens.Count + ";classMismatches=" + classMismatches + ";nonEmittedClassMismatches=" + (classMismatches - emittedClassMismatches) + ";maxConfidenceDiff=" + maximumConfidenceDifference.ToString("R", CultureInfo.InvariantCulture));
                    rows.Add(new
                    {
                        row = index,
                        ortText = ortRow.Text,
                        trtText = trtRow.Text,
                        ortTextSha256 = TextSha(ortRow.Text),
                        trtTextSha256 = TextSha(trtRow.Text),
                        tokenCount = ortRow.Tokens.Count,
                        emittedTokenCount = emittedOrt.Length,
                        classMismatches,
                        nonEmittedClassMismatches = classMismatches - emittedClassMismatches,
                        emittedFlagMismatches,
                        emittedClassMismatches,
                        maximumConfidenceDifference
                    });
                }

                Console.WriteLine(
                    "PADDLEOCR_TRT_REC_PARITY model=paddleocr/ppocrv6/small-rec;batch=2;rows=2" +
                    ";inputSha=" + Sha256Tensor(cropBatch) +
                    ";onnxSha=" + RecognizerSha +
                    ";engineSha=" + build.EngineSha256 +
                    ";engineBytes=" + build.EngineBytes.ToString(CultureInfo.InvariantCulture) +
                    ";ortTextSha=" + TextSha(ort.Result) +
                    ";trtTextSha=" + TextSha(trt.Result) +
                    ";ortOutputSha=" + ort.OutputSha256 +
                    ";trtOutputSha=" + trt.OutputSha256 +
                    ";row0=" + ort.Result.Items[0].Text +
                    ";row1=" + ort.Result.Items[1].Text);
                WriteReport(imagePath, cropBatch, build, ort, trt, rows);
            }
            finally
            {
                try { Directory.Delete(engineRoot, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static TextDetectionResult RunDetector(PaddleOcrProfile profile, string path, PreparedVisualInput input)
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry();
            profiles.Register(profile.VisualProfile);
            profiles.Freeze();
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using var pipeline = new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), registry, request, VisualTaskId.TextDetection), request);
            VisualInferenceResult result = pipeline.Run(input);
            return (TextDetectionResult)result.Value;
        }

        private static RecognitionRun RunRecognitionOnOrt(PaddleOcrProfile profile, string path, PreparedVisualInput input)
        {
            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), request, SessionOptions.Default);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
            ITensor raw = outputs.GetRequired("fetch_name_0");
            var context = new VisualDecodeContext(input, profile.VisualProfile, outputs, default);
            return new RecognitionRun((TextRecognitionBatchResult)profile.VisualProfile.Decoder.Decode(context), raw.Shape.ToArray(), Sha256Tensor(raw), MaximumAbs(raw), MeanAbs(raw));
        }

        private static TensorRtOnnxEngineBuildResult BuildEngine(PaddleOcrProfile profile, string path, string enginePath)
        {
            var input = new TensorShape(2, 3, profile.CropProfile!.TargetHeight, profile.CropProfile.MaximumWidth);
            var minimum = new TensorShape(1, 3, profile.CropProfile.TargetHeight, profile.CropProfile.MaximumWidth);
            var options = new TensorRtOnnxEngineBuildOptions(
                apiVersion: TensorRtApiVersion.TensorRt11,
                precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
                workspaceBytes: 268435456UL,
                optimizationLevel: 0,
                overwrite: true,
                inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, minimum, input, input) });
            return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, options);
        }

        private static RecognitionRun RunRecognitionOnTensorRt(PaddleOcrProfile profile, TensorRtOnnxEngineBuildResult build, PreparedVisualInput input)
        {
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11, cudaTargetArchitecture: "sm_86"));
            using IInferenceSession session = provider.CreateSession(
                new ModelArtifact(profile.VisualProfile.ModelId, "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId),
                request,
                SessionOptions.Default);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), default);
            ITensor raw = outputs.GetRequired("fetch_name_0");
            var context = new VisualDecodeContext(input, profile.VisualProfile, outputs, default);
            return new RecognitionRun((TextRecognitionBatchResult)profile.VisualProfile.Decoder.Decode(context), raw.Shape.ToArray(), Sha256Tensor(raw), MaximumAbs(raw), MeanAbs(raw));
        }

        private void WriteReport(string imagePath, PreparedVisualInput input, TensorRtOnnxEngineBuildResult build, RecognitionRun ort, RecognitionRun trt, IReadOnlyList<object> rows)
        {
            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_REC_PARITY_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddleocr-v6-small-rec-tensorrt-dynamic-batch-parity.json");
            string? directory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                model = "paddleocr/ppocrv6/small-rec",
                input = new { image = imagePath, imageSha256 = Sha256File(imagePath), tensorSha256 = Sha256Tensor(input), shape = input.Tensor.Shape.ToArray(), batch = input.BatchSize },
                detector = new { modelSha256 = DetectorSha },
                recognizer = new { onnxSha256 = RecognizerSha, engineSha256 = build.EngineSha256, engineBytes = build.EngineBytes },
                backends = new
                {
                    ortCpu = new { outputShape = ort.OutputShape, outputSha256 = ort.OutputSha256, maximumAbs = ort.MaximumAbs, meanAbs = ort.MeanAbs },
                    tensorRtCuda = new { outputShape = trt.OutputShape, outputSha256 = trt.OutputSha256, maximumAbs = trt.MaximumAbs, meanAbs = trt.MeanAbs }
                },
                rows,
                contract = "Text, batch order and emitted CTC class sequence must match. Non-emitted timestep class drift is reported but does not fail parity because CTC collapse removes it.",
                boundary = "Two detector-derived crops on one Windows host; not a CER/WER score, throughput benchmark or all-model TensorRT claim."
            }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            TestContext.AddResultFile(report);
        }

        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor, string? dictionarySha = null)
        {
            PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
            return new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
        }

        private static VisualSize ProbeSourceSize(string path)
        {
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(path, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            return probe.SourceSize;
        }

        private static string RowSha256(PreparedVisualInput input, int row)
        {
            var tensor = (Tensor<float>)input.Tensor;
            int rowLength = checked((int)(tensor.Length / input.BatchSize));
            float[] values = tensor.ToArray();
            using SHA256 sha = SHA256.Create();
            byte[] bytes = new byte[rowLength * sizeof(float)];
            Buffer.BlockCopy(values, row * rowLength, bytes, 0, bytes.Length);
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static string Sha256Tensor(PreparedVisualInput input)
        {
            var tensor = (Tensor<float>)input.Tensor;
            byte[] bytes = new byte[checked((int)tensor.Length * sizeof(float))];
            Buffer.BlockCopy(tensor.ToArray(), 0, bytes, 0, bytes.Length);
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static string Sha256Tensor(ITensor tensor)
        {
            byte[] bytes;
            if (tensor is Tensor<float> floatTensor)
            {
                float[] values = floatTensor.ToArray();
                bytes = new byte[checked(values.Length * sizeof(float))];
                Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            }
            else if (tensor is Tensor<double> doubleTensor)
            {
                double[] values = doubleTensor.ToArray();
                bytes = new byte[checked(values.Length * sizeof(double))];
                Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            }
            else
            {
                throw new AssertFailedException("The parity test only supports Float32/Float64 raw outputs; actual=" + tensor.ElementType);
            }

            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }

        private static double MaximumAbs(ITensor tensor)
        {
            double maximum = 0;
            ForEachValue(tensor, value => maximum = Math.Max(maximum, Math.Abs(value)));
            return maximum;
        }

        private static double MeanAbs(ITensor tensor)
        {
            double sum = 0;
            long count = 0;
            ForEachValue(tensor, value => { sum += Math.Abs(value); count++; });
            return count == 0 ? 0 : sum / count;
        }

        private static void ForEachValue(ITensor tensor, Action<double> visitor)
        {
            if (tensor is Tensor<float> floatTensor)
            {
                foreach (float value in floatTensor.ToArray()) visitor(value);
                return;
            }
            if (tensor is Tensor<double> doubleTensor)
            {
                foreach (double value in doubleTensor.ToArray()) visitor(value);
                return;
            }
            throw new AssertFailedException("The parity test only supports Float32/Float64 raw outputs; actual=" + tensor.ElementType);
        }

        private static string Sha256File(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static string TextSha(string text)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }

        private static string TextSha(TextRecognitionBatchResult result)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", result.Items.Select(value => value.Text))))).ToLowerInvariant();
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR file does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_PARITY_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_TRT_PARITY_RUN_EXTERNAL=1 to run the authorized local TensorRT OCR parity test.");
        }

        private sealed record RecognitionRun(
            TextRecognitionBatchResult Result,
            long[] OutputShape,
            string OutputSha256,
            double MaximumAbs,
            double MeanAbs);
    }
}
