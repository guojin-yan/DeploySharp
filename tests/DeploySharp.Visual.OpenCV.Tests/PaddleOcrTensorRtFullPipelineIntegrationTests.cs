using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Executes one complete PP-OCR chain through freshly built TensorRT engines:
/// detector, crop, orientation classifier, recognizer and merge. The test is
/// opt-in because it requires the local CUDA/TensorRT installation and model
/// assets. ORT CPU is run on the same prepared image as a bounded reference.
/// / 使用新构建的 TensorRT Engine 完整运行 PP-OCR：检测、裁剪、方向分类、识别和合并；ORT CPU 作为同一输入的有界参考。
/// </summary>
[TestClass]
public sealed class PaddleOcrTensorRtFullPipelineIntegrationTests
{
    private const string DefaultImage = @"E:\Data\ocr\demo_1.jpg";
    private const string DefaultRoot = @"E:\Model\paddleocr";
    private const string DetectorId = "paddleocr/ppocrv6/small-det";
    private const string ClassifierId = "paddleocr/ppocrv5/mobile-cls";
    private const string RecognizerId = "paddleocr/ppocrv6/small-rec";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void PpOcrV6SmallRunsCompleteDetClsRecMergeOnTensorRt()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_FULL_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_TRT_FULL_RUN_EXTERNAL=1 to run the authorized TensorRT full-pipeline test.");
        }

        string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_FULL_IMAGE") ?? DefaultImage);
        string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? DefaultRoot;
        string detectorPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_det_inference.onnx"));
        string recognizerPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_rec_inference.onnx"));
        string dictionaryPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_rec_dict.txt"));
        string classifierPath = RequireFile(Path.Combine(root, "PP-OCRv5", "PP-OCRv5_mobile_cls.onnx"));

        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId(DetectorId));
        PaddleOcrModelDescriptor classifierDescriptor = PaddleOcrModelCatalog.Find(new ModelId(ClassifierId));
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId(RecognizerId));
        OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.trt.full.v6-small", "v6-small", true);

        PaddleOcrProfile detectorOnnx = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Contract(detectorDescriptor), maximumBatch: 1);
        PaddleOcrProfile classifierOnnx = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Contract(classifierDescriptor), maximumBatch: 2);
        PaddleOcrProfile recognizerOnnx = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Contract(recognizerDescriptor, characters.Sha256), characters, maximumBatch: 16);
        VisualSize sourceSize = ProbeSourceSize(imagePath);
        using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(
            imagePath,
            detectorOnnx.VisualProfile.Input.Name,
            OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize),
            "trt-full-pipeline");

        string engineRoot = Path.Combine(Path.GetTempPath(), "deploysharp-ppocr-trt-full-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(engineRoot);
        try
        {
            TensorRtOnnxEngineBuildResult detectorEngine = BuildStatic(detectorOnnx, detectorPath, Path.Combine(engineRoot, "det.engine"), input.DetectionInput.Tensor.Shape);
            TensorRtOnnxEngineBuildResult classifierEngine = BuildClassifier(classifierOnnx, classifierPath, Path.Combine(engineRoot, "cls.engine"));
            TensorRtOnnxEngineBuildResult recognizerEngine = BuildRecognizer(recognizerOnnx, recognizerPath, Path.Combine(engineRoot, "rec.engine"));
            PaddleOcrProfile detectorTrt = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Contract(detectorDescriptor, modelFormat: "tensorrt-engine", artifactSha256: detectorEngine.EngineSha256), maximumBatch: 1);
            PaddleOcrProfile classifierTrt = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Contract(classifierDescriptor, modelFormat: "tensorrt-engine", artifactSha256: classifierEngine.EngineSha256), maximumBatch: 2);
            PaddleOcrProfile recognizerTrt = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Contract(recognizerDescriptor, characters.Sha256, "tensorrt-engine", recognizerEngine.EngineSha256), characters, maximumBatch: 16);

            OcrResult ortResult;
            Stopwatch ortWatch = Stopwatch.StartNew();
            using (BackendRegistry ortRegistry = new BackendRegistry().UseOnnxRuntime())
            using (OcrPipeline ortPipeline = CreatePipeline(ortRegistry, OnnxRuntimeBackendProvider.BackendId,
                detectorOnnx, classifierOnnx, recognizerOnnx, detectorPath, classifierPath, recognizerPath,
                false))
            {
                ortResult = ortPipeline.Run(input);
            }
            ortWatch.Stop();

            OcrResult trtResult;
            Stopwatch trtWatch = Stopwatch.StartNew();
            using (BackendRegistry trtRegistry = new BackendRegistry().UseTensorRT(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10, cudaTargetArchitecture: "sm_86")))
            using (OcrPipeline trtPipeline = CreatePipeline(trtRegistry, TensorRtBackendProvider.BackendId,
                detectorTrt, classifierTrt, recognizerTrt, detectorEngine.EnginePath, classifierEngine.EnginePath, recognizerEngine.EnginePath,
                true))
            {
                trtResult = trtPipeline.Run(input);
            }
            trtWatch.Stop();

            Assert.IsTrue(ortResult.Regions.Count > 0, "The ORT reference returned no regions.");
            Assert.IsTrue(trtResult.Regions.Count > 0, "TensorRT returned no regions.");
            Assert.AreEqual(ortResult.Regions.Count, trtResult.Regions.Count, "TensorRT and ORT region counts differ.");
            for (int index = 0; index < ortResult.Regions.Count; index++)
            {
                string expected = ortResult.Regions[index].Recognition?.Text ?? string.Empty;
                string actual = trtResult.Regions[index].Recognition?.Text ?? string.Empty;
                Assert.AreEqual(expected, actual, "TensorRT CTC text differs at region " + index.ToString(CultureInfo.InvariantCulture) + ".");
            }

            var report = new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                environment = new
                {
                    os = RuntimeInformation.OSDescription,
                    framework = RuntimeInformation.FrameworkDescription,
                    processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    tensorRtRoot = Environment.GetEnvironmentVariable("JYPPX_TENSORRT_ROOT"),
                    cudaRoot = Environment.GetEnvironmentVariable("JYPPX_CUDA_ROOT"),
                    cudnnRoot = Environment.GetEnvironmentVariable("JYPPX_CUDNN_ROOT"),
                    gpuTelemetry = ReadGpuTelemetry()
                },
                model = new { detector = DetectorId, classifier = ClassifierId, recognizer = RecognizerId },
                input = new { image = imagePath, imageSha256 = Sha256File(imagePath), detectionShape = input.DetectionInput.Tensor.Shape.ToArray() },
                engines = new
                {
                    detector = new { onnxSha256 = Sha256File(detectorPath), engineSha256 = detectorEngine.EngineSha256, bytes = detectorEngine.EngineBytes },
                    classifier = new { onnxSha256 = Sha256File(classifierPath), engineSha256 = classifierEngine.EngineSha256, bytes = classifierEngine.EngineBytes },
                    recognizer = new { onnxSha256 = Sha256File(recognizerPath), engineSha256 = recognizerEngine.EngineSha256, bytes = recognizerEngine.EngineBytes }
                },
                ortCpu = new { regions = ortResult.Regions.Count, recognized = ortResult.Regions.Count(value => value.Recognition != null), textSha256 = TextSha(ortResult), elapsedMs = ortWatch.Elapsed.TotalMilliseconds },
                tensorRtCuda = new { regions = trtResult.Regions.Count, recognized = trtResult.Regions.Count(value => value.Recognition != null), textSha256 = TextSha(trtResult), elapsedMs = trtWatch.Elapsed.TotalMilliseconds },
                parity = new { regionCountEqual = true, orderedTextEqual = true },
                boundary = "One v6 Small detector/recognizer with the v5 Mobile text-line classifier on one Windows TensorRT host. This proves complete stage execution and bounded ordered-text parity for these exact artifacts; it is not a quality score, 5/50 benchmark, cross-device portability result or all-model TensorRT admission."
            };
            string reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_FULL_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddleocr-v6-small-tensorrt-full-pipeline.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            TestContext.AddResultFile(reportPath);
            Console.WriteLine("PADDLEOCR_TRT_FULL_PIPELINE regions=" + trtResult.Regions.Count.ToString(CultureInfo.InvariantCulture) + ";ortMs=" + ortWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";trtMs=" + trtWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";textSha=" + TextSha(trtResult));
        }
        finally
        {
            try { Directory.Delete(engineRoot, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static OcrPipeline CreatePipeline(BackendRegistry registry, BackendId backendId, PaddleOcrProfile detector, PaddleOcrProfile classifier, PaddleOcrProfile recognizer, string detectorPath, string classifierPath, string recognizerPath, bool tensorRt)
    {
        var profiles = new VisualProfileRegistry();
        profiles.Register(detector.VisualProfile);
        profiles.Register(classifier.VisualProfile);
        profiles.Register(recognizer.VisualProfile);
        profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, tensorRt ? "cuda" : "cpu");
        return new OcrPipeline(
            registry,
            profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection), request,
            profiles.Select(classifier.CreateArtifact(classifierPath, backendId), registry, request, VisualTaskId.TextOrientationClassification), request,
            classifier.CropProfile!,
            profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition), request,
            recognizer.CropProfile!,
            new OcrPipelineOptions(maximumRegions: 64, maximumRecognitionBatch: 16),
            orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
    }

    private static TensorRtOnnxEngineBuildResult BuildStatic(PaddleOcrProfile profile, string path, string enginePath, TensorShape shape)
    {
        var options = new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt10, precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
            workspaceBytes: 268435456UL, optimizationLevel: 0, overwrite: true,
            inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, shape, shape, shape) });
        return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, options);
    }

    private static TensorRtOnnxEngineBuildResult BuildClassifier(PaddleOcrProfile profile, string path, string enginePath)
    {
        var min = new TensorShape(1, 3, profile.CropProfile!.TargetHeight, profile.CropProfile.FixedWidth);
        var opt = new TensorShape(2, 3, profile.CropProfile.TargetHeight, profile.CropProfile.FixedWidth);
        var options = new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt10, precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
            workspaceBytes: 268435456UL, optimizationLevel: 0, overwrite: true,
            inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, min, opt, opt) });
        return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, options);
    }

    private static TensorRtOnnxEngineBuildResult BuildRecognizer(PaddleOcrProfile profile, string path, string enginePath)
    {
        var min = new TensorShape(1, 3, profile.CropProfile!.TargetHeight, profile.CropProfile.MinimumWidth);
        var opt = new TensorShape(8, 3, profile.CropProfile.TargetHeight, 320);
        var max = new TensorShape(16, 3, profile.CropProfile.TargetHeight, profile.CropProfile.MaximumWidth);
        var options = new TensorRtOnnxEngineBuildOptions(apiVersion: TensorRtApiVersion.TensorRt10, precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
            workspaceBytes: 536870912UL, optimizationLevel: 0, overwrite: true,
            inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, min, opt, max) });
        return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, options);
    }

    private static PaddleOcrArtifactContract Contract(PaddleOcrModelDescriptor descriptor, string? dictionarySha = null, string modelFormat = "onnx", string? artifactSha256 = null)
    {
        PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
        return new PaddleOcrArtifactContract(release.Opset, artifactSha256 ?? release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", modelFormat: modelFormat, dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
    }

    private static VisualSize ProbeSourceSize(string path)
    {
        using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(path, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
        return probe.SourceSize;
    }

    private static string TextSha(OcrResult result)
    {
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", result.Regions.Select(value => value.Recognition?.Text ?? string.Empty))))).ToLowerInvariant();
    }

    private static string Sha256File(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string? ReadGpuTelemetry()
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,driver_version,pstate,clocks.gr,clocks.mem --format=csv,noheader",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            })!;
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return "nvidia-smi timeout";
            }
            string output = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception || exception is IOException)
        {
            return "unavailable: " + exception.GetType().Name;
        }
    }

    private static string RequireFile(string path)
    {
        if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR file does not exist: " + path);
        return path;
    }
}
