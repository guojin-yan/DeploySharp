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
/// Runs every locally available PP-OCR v4/v5/v6 core DET+CLS+REC combination
/// through ORT CPU and freshly built TensorRT engines. Each case is reported
/// independently so an importer/runtime blocker cannot be mistaken for a pass
/// of the remaining models.
/// </summary>
[TestClass]
public sealed class PaddleOcrAllCorePipelineTensorRtIntegrationTests
{
    private const string DefaultImage = @"E:\Data\ocr\demo_1.jpg";
    private const string DefaultRoot = @"E:\Model\paddleocr";
    private const string V4DictionarySha = "8e9dc37300253c08a5db6d75f8ae7dbf9ab8dc8c8f88827400bfe974269d3953";
    private const string V5DictionarySha = "d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void EveryCorePpOcrVariantRunsCompleteDetClsRecMergeOnTensorRt()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_ALL_FULL_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_TRT_ALL_FULL_RUN_EXTERNAL=1 to run the authorized TensorRT core-pipeline matrix.");
        }

        string imagePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_FULL_IMAGE") ?? DefaultImage);
        string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? DefaultRoot;
        VisualSize sourceSize = ProbeSourceSize(imagePath);
        string engineRoot = Path.Combine(Path.GetTempPath(), "deploysharp-ppocr-trt-all-full-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(engineRoot);
        var rows = new List<object>();
        int failures = 0;
        try
        {
            foreach (CoreCase testCase in Cases)
            {
                try
                {
                    rows.Add(RunCase(testCase, root, imagePath, sourceSize, engineRoot));
                }
                catch (AssertInconclusiveException) { throw; }
                catch (Exception exception)
                {
                    failures++;
                    rows.Add(new
                    {
                        name = testCase.Name,
                        detector = testCase.DetectorModelId,
                        classifier = testCase.ClassifierModelId,
                        recognizer = testCase.RecognizerModelId,
                        status = "blocked",
                        error = exception.GetType().FullName + ": " + exception.Message
                    });
                    Console.WriteLine("PADDLEOCR_TRT_FULL_PIPELINE_BLOCKED variant=" + testCase.Name + ";error=" + exception.Message);
                }
            }

            string reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_TRT_ALL_FULL_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), "paddleocr-core-tensorrt-full-pipeline-20261013.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
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
                input = new { image = imagePath, imageSha256 = Sha256File(imagePath) },
                cases = rows,
                summary = new { total = Cases.Length, passed = Cases.Length - failures, blocked = failures },
                boundary = "Core PP-OCR v4/v5/v6 full det-crop-cls-rec-merge execution on one Windows host. Each row is scoped to its exact ONNX artifacts and runtime; blocked rows are not generalized to other TensorRT versions or models. This is not a quality score, CER/WER, formal P50/P95 benchmark or cross-device portability result."
            };
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            TestContext.AddResultFile(reportPath);
            Assert.AreEqual(0, failures, "One or more core PP-OCR TensorRT full-pipeline cases were blocked; see the machine-readable report.");
        }
        finally
        {
            try { Directory.Delete(engineRoot, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static object RunCase(CoreCase testCase, string root, string imagePath, VisualSize sourceSize, string engineRoot)
    {
        string folder = Path.Combine(root, testCase.Folder);
        string detectorPath = RequireFile(Path.Combine(folder, testCase.DetectorFile));
        string classifierFolder = testCase.ClassifierModelId.StartsWith("paddleocr/ppocrv4/", StringComparison.OrdinalIgnoreCase) ? Path.Combine(root, "PP-OCRv4") : Path.Combine(root, "PP-OCRv5");
        string classifierPath = RequireFile(Path.Combine(classifierFolder, testCase.ClassifierFile));
        string recognizerPath = RequireFile(Path.Combine(folder, testCase.RecognizerFile));
        string dictionaryPath = RequireFile(Path.Combine(folder, testCase.DictionaryFile));
        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.DetectorModelId));
        PaddleOcrModelDescriptor classifierDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.ClassifierModelId));
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId(testCase.RecognizerModelId));
        OcrCharacterSet characterSet = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.trt.all." + testCase.Name, testCase.Name, true, testCase.DictionarySha);
        PaddleOcrProfile detectorOnnx = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Contract(detectorDescriptor), maximumBatch: 1);
        PaddleOcrProfile classifierOnnx = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Contract(classifierDescriptor), maximumBatch: 2);
        PaddleOcrProfile recognizerOnnx = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Contract(recognizerDescriptor, characterSet.Sha256), characterSet, maximumBatch: 16);
        using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(
            imagePath,
            detectorOnnx.VisualProfile.Input.Name,
            OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize),
            "trt-all-full-" + testCase.Name);

        string caseRoot = Path.Combine(engineRoot, testCase.Name);
        Directory.CreateDirectory(caseRoot);
        TensorRtOnnxEngineBuildResult detectorEngine = BuildStatic(detectorOnnx, detectorPath, Path.Combine(caseRoot, "det.engine"), input.DetectionInput.Tensor.Shape);
        TensorRtOnnxEngineBuildResult classifierEngine = BuildClassifier(classifierOnnx, classifierPath, Path.Combine(caseRoot, "cls.engine"));
        TensorRtOnnxEngineBuildResult recognizerEngine = BuildRecognizer(recognizerOnnx, recognizerPath, Path.Combine(caseRoot, "rec.engine"));
        PaddleOcrProfile detectorTrt = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Contract(detectorDescriptor, modelFormat: "tensorrt-engine", artifactSha256: detectorEngine.EngineSha256), maximumBatch: 1);
        PaddleOcrProfile classifierTrt = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Contract(classifierDescriptor, modelFormat: "tensorrt-engine", artifactSha256: classifierEngine.EngineSha256), maximumBatch: 2);
        PaddleOcrProfile recognizerTrt = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Contract(recognizerDescriptor, characterSet.Sha256, "tensorrt-engine", recognizerEngine.EngineSha256), characterSet, maximumBatch: 16);

        OcrResult ortResult;
        Stopwatch ortWatch = Stopwatch.StartNew();
        using (BackendRegistry registry = new BackendRegistry().UseOnnxRuntime())
        using (OcrPipeline pipeline = CreatePipeline(registry, OnnxRuntimeBackendProvider.BackendId, detectorOnnx, classifierOnnx, recognizerOnnx, detectorPath, classifierPath, recognizerPath, false))
        {
            ortResult = pipeline.Run(input);
        }
        ortWatch.Stop();

        OcrResult trtResult;
        Stopwatch trtWatch = Stopwatch.StartNew();
        using (BackendRegistry registry = new BackendRegistry().UseTensorRT(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10, cudaTargetArchitecture: "sm_86")))
        using (OcrPipeline pipeline = CreatePipeline(registry, TensorRtBackendProvider.BackendId, detectorTrt, classifierTrt, recognizerTrt, detectorEngine.EnginePath, classifierEngine.EnginePath, recognizerEngine.EnginePath, true))
        {
            trtResult = pipeline.Run(input);
        }
        trtWatch.Stop();
        Assert.IsTrue(ortResult.Regions.Count > 0, testCase.Name + " ORT returned no text regions.");
        Assert.IsTrue(trtResult.Regions.Count > 0, testCase.Name + " TensorRT returned no text regions.");
        Assert.AreEqual(ortResult.Regions.Count, trtResult.Regions.Count, testCase.Name + " region counts differ.");
        string ortTextSha = TextSha(ortResult);
        string trtTextSha = TextSha(trtResult);
        Assert.AreEqual(ortTextSha, trtTextSha, testCase.Name + " ordered text differs between ORT and TensorRT.");
        Console.WriteLine("PADDLEOCR_TRT_FULL_PIPELINE variant=" + testCase.Name + ";regions=" + trtResult.Regions.Count.ToString(CultureInfo.InvariantCulture) + ";ortMs=" + ortWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";trtMs=" + trtWatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        return new
        {
            name = testCase.Name,
            detector = testCase.DetectorModelId,
            classifier = testCase.ClassifierModelId,
            recognizer = testCase.RecognizerModelId,
            status = "passed",
            regions = new { ort = ortResult.Regions.Count, tensorRt = trtResult.Regions.Count },
            recognized = new { ort = ortResult.Regions.Count(value => value.Recognition != null), tensorRt = trtResult.Regions.Count(value => value.Recognition != null) },
            orderedTextSha256 = new { ort = ortTextSha, tensorRt = trtTextSha },
            elapsedMs = new { ort = ortWatch.Elapsed.TotalMilliseconds, tensorRt = trtWatch.Elapsed.TotalMilliseconds },
            engines = new
            {
                detector = new { onnxSha256 = Sha256File(detectorPath), engineSha256 = detectorEngine.EngineSha256 },
                classifier = new { onnxSha256 = Sha256File(classifierPath), engineSha256 = classifierEngine.EngineSha256 },
                recognizer = new { onnxSha256 = Sha256File(recognizerPath), engineSha256 = recognizerEngine.EngineSha256 }
            }
        };
    }

    private static OcrPipeline CreatePipeline(BackendRegistry registry, BackendId backendId, PaddleOcrProfile detector, PaddleOcrProfile classifier, PaddleOcrProfile recognizer, string detectorPath, string classifierPath, string recognizerPath, bool tensorRt)
    {
        var profiles = new VisualProfileRegistry();
        profiles.Register(detector.VisualProfile); profiles.Register(classifier.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, tensorRt ? "cuda" : "cpu");
        return new OcrPipeline(registry,
            profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection), request,
            profiles.Select(classifier.CreateArtifact(classifierPath, backendId), registry, request, VisualTaskId.TextOrientationClassification), request,
            classifier.CropProfile!,
            profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition), request,
            recognizer.CropProfile!, new OcrPipelineOptions(maximumRegions: 64, maximumRecognitionBatch: 16, maximumConcurrency: 1),
            orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);
    }

    private static TensorRtOnnxEngineBuildResult BuildStatic(PaddleOcrProfile profile, string path, string enginePath, TensorShape shape)
        => new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, new TensorRtOnnxEngineBuildOptions(TensorRtApiVersion.TensorRt10, TensorRtOnnxEnginePrecision.RuntimeDefault, workspaceBytes: 536870912UL, optimizationLevel: 0, overwrite: true, inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, shape, shape, shape) }));

    private static TensorRtOnnxEngineBuildResult BuildClassifier(PaddleOcrProfile profile, string path, string enginePath)
    {
        var min = new TensorShape(1, 3, profile.CropProfile!.TargetHeight, profile.CropProfile.FixedWidth);
        var opt = new TensorShape(2, 3, profile.CropProfile.TargetHeight, profile.CropProfile.FixedWidth);
        return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, new TensorRtOnnxEngineBuildOptions(TensorRtApiVersion.TensorRt10, TensorRtOnnxEnginePrecision.RuntimeDefault, workspaceBytes: 268435456UL, optimizationLevel: 0, overwrite: true, inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, min, opt, opt) }));
    }

    private static TensorRtOnnxEngineBuildResult BuildRecognizer(PaddleOcrProfile profile, string path, string enginePath)
    {
        var min = new TensorShape(1, 3, profile.CropProfile!.TargetHeight, profile.CropProfile.MinimumWidth);
        var opt = new TensorShape(8, 3, profile.CropProfile.TargetHeight, 320);
        var max = new TensorShape(16, 3, profile.CropProfile.TargetHeight, profile.CropProfile.MaximumWidth);
        return new TensorRtOnnxEngineBuilder().Build(profile.CreateArtifact(path), enginePath, new TensorRtOnnxEngineBuildOptions(TensorRtApiVersion.TensorRt10, TensorRtOnnxEnginePrecision.RuntimeDefault, workspaceBytes: 536870912UL, optimizationLevel: 0, overwrite: true, inputProfiles: new[] { new TensorRtOnnxInputProfile(profile.VisualProfile.Input.Name, min, opt, max) }));
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
            using Process process = Process.Start(new ProcessStartInfo { FileName = "nvidia-smi", Arguments = "--query-gpu=name,driver_version,pstate,clocks.gr,clocks.mem --format=csv,noheader", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 })!;
            if (!process.WaitForExit(5000)) { try { process.Kill(); } catch { } return "nvidia-smi timeout"; }
            string output = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception || exception is IOException) { return "unavailable: " + exception.GetType().Name; }
    }

    private static string RequireFile(string path)
    {
        if (!File.Exists(path)) Assert.Inconclusive("The configured PaddleOCR file does not exist: " + path);
        return path;
    }

    private static CoreCase[] Cases { get; } =
    {
        new("v4-mobile", "paddleocr/ppocrv4/mobile-det", "paddleocr/ppocrv4/legacy-cls", "paddleocr/ppocrv4/mobile-rec", "PP-OCRv4", "PP-OCRv4_mobile_det.onnx", "PP-OCRv4_mobile_cls.onnx", "PP-OCRv4_mobile_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
        new("v4-server", "paddleocr/ppocrv4/server-det", "paddleocr/ppocrv4/legacy-cls", "paddleocr/ppocrv4/server-rec", "PP-OCRv4", "PP-OCRv4_server_det.onnx", "PP-OCRv4_mobile_cls.onnx", "PP-OCRv4_server_rec.onnx", "ppocrv4_keys.txt", V4DictionarySha),
        new("v5-mobile", "paddleocr/ppocrv5/mobile-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv5/mobile-rec", "PP-OCRv5", "PP-OCRv5_mobile_det.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv5_mobile_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
        new("v5-server", "paddleocr/ppocrv5/server-det", "paddleocr/ppocrv5/server-cls", "paddleocr/ppocrv5/server-rec", "PP-OCRv5", "PP-OCRv5_server_det.onnx", "PP-OCRv5_server_cls.onnx", "PP-OCRv5_server_rec.onnx", "ppocrv5_dict.txt", V5DictionarySha),
        new("v6-tiny", "paddleocr/ppocrv6/tiny-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/tiny-rec", Path.Combine("PP-OCRv6", "tiny"), "PP-OCRv6_tiny_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_tiny_rec_inference.onnx", "PP-OCRv6_tiny_rec_dict.txt", null),
        new("v6-small", "paddleocr/ppocrv6/small-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/small-rec", Path.Combine("PP-OCRv6", "small"), "PP-OCRv6_small_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_small_rec_inference.onnx", "PP-OCRv6_small_rec_dict.txt", null),
        new("v6-medium", "paddleocr/ppocrv6/medium-det", "paddleocr/ppocrv5/mobile-cls", "paddleocr/ppocrv6/medium-rec", Path.Combine("PP-OCRv6", "medium"), "PP-OCRv6_medium_det_inference.onnx", "PP-OCRv5_mobile_cls.onnx", "PP-OCRv6_medium_rec_inference.onnx", "PP-OCRv6_medium_rec_dict.txt", null)
    };

    private sealed record CoreCase(string Name, string DetectorModelId, string ClassifierModelId, string RecognizerModelId, string Folder, string DetectorFile, string ClassifierFile, string RecognizerFile, string DictionaryFile, string? DictionarySha);
}
