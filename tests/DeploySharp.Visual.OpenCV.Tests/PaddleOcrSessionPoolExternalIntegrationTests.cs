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
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests
{
    /// <summary>
    /// Exercises the real PP-OCR pipeline with independent ORT sessions, bounded
    /// end-to-end concurrency and asynchronous input preparation. This is an
    /// opt-in resource/lifetime contract, not a throughput ranking.
    /// / 使用真实 PP-OCR 流水线验证独立 ORT Session、端到端并发和异步输入准备。
    /// 这是资源与生命周期合同，不是吞吐排名。
    /// </summary>
    [TestClass]
    public sealed class PaddleOcrSessionPoolExternalIntegrationTests
    {
        private const string DefaultRoot = @"E:\Model\paddleocr";
        private const string DefaultImage1 = @"E:\Data\ocr\demo_1.jpg";
        private const string DefaultImage2 = @"E:\Data\ocr\demo_2.jpg";
        private const string DefaultImage3 = @"E:\Data\ocr\demo_3.jpg";

        [TestMethod]
        [TestCategory("ExternalModels")]
        public async Task PpOcrV6SmallUsesIndependentSessionsAndPrefetchedConcurrentFrames()
        {
            RequireExternal();
            string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? DefaultRoot;
            string[] images =
            {
                Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SESSION_POOL_IMAGE_1") ?? DefaultImage1,
                Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SESSION_POOL_IMAGE_2") ?? DefaultImage2,
                Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SESSION_POOL_IMAGE_3") ?? DefaultImage3
            };
            foreach (string image in images) RequireFile(image);

            string detectorPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_det_inference.onnx"));
            string recognizerPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_rec_inference.onnx"));
            string classifierPath = RequireFile(Path.Combine(root, "PP-OCRv5", "PP-OCRv5_mobile_cls.onnx"));
            string dictionaryPath = RequireFile(Path.Combine(root, "PP-OCRv6", "small", "PP-OCRv6_small_rec_dict.txt"));

            PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/small-det"));
            PaddleOcrModelDescriptor classifierDescriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv5/mobile-cls"));
            PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId("paddleocr/ppocrv6/small-rec"));
            OcrCharacterSet characterSet = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "external.session-pool.v6-small", "v6-small", true, null);
            PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorDescriptor), maximumBatch: 1);
            PaddleOcrProfile classifier = PaddleOcrModelCatalog.CreateProfile(classifierDescriptor, Artifact(classifierDescriptor), maximumBatch: 2);
            PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerDescriptor, characterSet.Sha256), characterSet, maximumBatch: 16);

            using var registry = new BackendRegistry();
            registry.UseOnnxRuntime();
            var profiles = new VisualProfileRegistry();
            profiles.Register(detector.VisualProfile);
            profiles.Register(classifier.VisualProfile);
            profiles.Register(recognizer.VisualProfile);
            profiles.Freeze();
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");

            using var pipeline = new OcrPipeline(
                registry,
                profiles.Select(detector.CreateArtifact(detectorPath, OnnxRuntimeBackendProvider.BackendId), registry, request, VisualTaskId.TextDetection), request,
                profiles.Select(classifier.CreateArtifact(classifierPath, OnnxRuntimeBackendProvider.BackendId), registry, request, VisualTaskId.TextOrientationClassification), request,
                classifier.CropProfile!,
                profiles.Select(recognizer.CreateArtifact(recognizerPath, OnnxRuntimeBackendProvider.BackendId), registry, request, VisualTaskId.TextRecognition), request,
                recognizer.CropProfile!,
                new OcrPipelineOptions(maximumConcurrency: 2, maximumRegions: 128, maximumRecognitionBatch: 16),
                detectionSessionOptions: new SessionOptions(2),
                orientationSessionOptions: new SessionOptions(2),
                recognitionSessionOptions: new SessionOptions(2),
                orientationRejectionPolicy: OcrOrientationRejectionPolicy.UseZeroDegrees);

            var preparationGate = new PreparationTracker();
            VisualSize[] expectedSizes = images.Select(ProbeSourceSize).ToArray();
            var sourceSizes = new Dictionary<string, VisualSize>(StringComparer.Ordinal);
            for (int index = 0; index < images.Length; index++) sourceSizes.Add(images[index], expectedSizes[index]);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<OcrResult> results = await RunPrefetchedAsync(pipeline, images, sourceSizes, detector.VisualProfile.Input.Name, preparationGate).ConfigureAwait(false);
            watch.Stop();

            Assert.AreEqual(images.Length, results.Count, "Prefetched results must preserve one result per input.");
            Assert.IsTrue(preparationGate.MaximumActive >= 2, "The prefetch path did not overlap at least two input preparations.");
            for (int index = 0; index < results.Count; index++)
            {
                Assert.AreEqual(expectedSizes[index], results[index].SourceSize, "Result order/source size changed at index " + index.ToString(CultureInfo.InvariantCulture));
                Assert.IsTrue(results[index].Regions.Count > 0, "Input " + index.ToString(CultureInfo.InvariantCulture) + " returned no text regions.");
                Assert.IsTrue(results[index].Regions.Any(region => region.Recognition != null), "Input " + index.ToString(CultureInfo.InvariantCulture) + " returned no recognized text.");
            }

            string[] resultHashes = results.Select(result => result.ComputeSha256()).ToArray();
            Assert.AreEqual(resultHashes.Length, resultHashes.Distinct(StringComparer.Ordinal).Count(), "Distinct source images should not collapse to one result contract.");
            Console.WriteLine("PADDLEOCR_SESSION_POOL_CONTRACT backend=onnxruntime-cpu;model=ppocrv6-small;inputs=" + images.Length.ToString(CultureInfo.InvariantCulture) + ";pipelineConcurrency=2;sessionConcurrency=2;prefetch=2;maxPreparationConcurrency=" + preparationGate.MaximumActive.ToString(CultureInfo.InvariantCulture) + ";elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + ";resultSha=" + Sha256(string.Join("\n", resultHashes)));

            double[]? benchmarkSamplesMs = null;
            if (string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SESSION_POOL_BENCHMARK"), "1", StringComparison.Ordinal))
            {
                for (int warmup = 0; warmup < 5; warmup++)
                {
                    IReadOnlyList<OcrResult> warmupResults = await RunPrefetchedAsync(pipeline, images, sourceSizes, detector.VisualProfile.Input.Name, null).ConfigureAwait(false);
                    AssertResultHashes(resultHashes, warmupResults, "warmup " + warmup.ToString(CultureInfo.InvariantCulture));
                }

                benchmarkSamplesMs = new double[50];
                for (int iteration = 0; iteration < benchmarkSamplesMs.Length; iteration++)
                {
                    var sampleWatch = System.Diagnostics.Stopwatch.StartNew();
                    IReadOnlyList<OcrResult> measuredResults = await RunPrefetchedAsync(pipeline, images, sourceSizes, detector.VisualProfile.Input.Name, null).ConfigureAwait(false);
                    sampleWatch.Stop();
                    AssertResultHashes(resultHashes, measuredResults, "measurement " + iteration.ToString(CultureInfo.InvariantCulture));
                    benchmarkSamplesMs[iteration] = sampleWatch.Elapsed.TotalMilliseconds;
                }

                Console.WriteLine("PADDLEOCR_SESSION_POOL_BENCHMARK backend=onnxruntime-cpu;model=ppocrv6-small;warmup=5;iterations=50;unit=three-image-prefetched-pipeline;p50Ms=" + Percentile(benchmarkSamplesMs, 0.50).ToString("F3", CultureInfo.InvariantCulture) + ";p95Ms=" + Percentile(benchmarkSamplesMs, 0.95).ToString("F3", CultureInfo.InvariantCulture));
            }

            WriteExternalReportIfRequested(images, detectorPath, classifierPath, recognizerPath, dictionaryPath, preparationGate.MaximumActive, watch.Elapsed.TotalMilliseconds, resultHashes, benchmarkSamplesMs);
        }

        private static async Task<IReadOnlyList<OcrResult>> RunPrefetchedAsync(
            OcrPipeline pipeline,
            string[] images,
            IReadOnlyDictionary<string, VisualSize> sourceSizes,
            string inputName,
            PreparationTracker? preparationTracker)
        {
            var factory = new OpenCvOcrImageInputFactory();
            return await pipeline.RunPrefetchedAsync(
                images,
                (image, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    preparationTracker?.Enter();
                    try
                    {
                        return factory.CreateFromFile(
                            image,
                            inputName,
                            OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSizes[image]));
                    }
                    finally { preparationTracker?.Exit(); }
                },
                prefetch: 2,
                options: new OcrExecutionOptions(disposeInputOnCompletion: true, correlationId: "external-session-pool"),
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }

        private static void AssertResultHashes(string[] expectedHashes, IReadOnlyList<OcrResult> results, string phase)
        {
            Assert.AreEqual(expectedHashes.Length, results.Count, "Input count changed during " + phase + ".");
            for (int index = 0; index < expectedHashes.Length; index++)
                Assert.AreEqual(expectedHashes[index], results[index].ComputeSha256(), "Result contract changed during " + phase + " at input " + index.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static double Percentile(double[] samples, double percentile)
        {
            double[] sorted = (double[])samples.Clone();
            Array.Sort(sorted);
            double position = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }

        private static void WriteExternalReportIfRequested(
            string[] images,
            string detectorPath,
            string classifierPath,
            string recognizerPath,
            string dictionaryPath,
            int maximumPreparationConcurrency,
            double elapsedMilliseconds,
            string[] resultHashes,
            double[]? benchmarkSamplesMs)
        {
            string? reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SESSION_POOL_REPORT");
            if (string.IsNullOrWhiteSpace(reportPath)) return;

            var report = new
            {
                schemaVersion = 2,
                generatedUtc = DateTimeOffset.UtcNow,
                scope = "Real PP-OCRv6 Small ORT CPU session-pool and asynchronous prefetch contract",
                sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unknown",
                sourceWorkingTreeDirty = ParseNullableBoolean(Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_WORKTREE_DIRTY")),
                testSourceSha256 = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_SOURCE_SHA256"),
                test = nameof(PpOcrV6SmallUsesIndependentSessionsAndPrefetchedConcurrentFrames),
                host = new
                {
                    machine = Environment.MachineName,
                    operatingSystem = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.OSArchitecture.ToString(),
                    runtime = RuntimeInformation.FrameworkDescription,
                    cpuModel = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_HOST_CPU"),
                    cpuGovernor = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_HOST_CPU_GOVERNOR"),
                    cpuMinMHz = ParseNullableDouble(Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_HOST_CPU_MIN_MHZ")),
                    cpuMaxMHz = ParseNullableDouble(Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_HOST_CPU_MAX_MHZ")),
                    gpu = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_HOST_GPU"),
                    gpuDriver = Environment.GetEnvironmentVariable("DEPLOYSHARP_TEST_HOST_GPU_DRIVER"),
                    backend = "onnxruntime-cpu",
                    onnxRuntimePackage = "Microsoft.ML.OnnxRuntime 1.28.0",
                    onnxRuntimeAssembly = typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly.GetName().Version?.ToString() ?? "unknown"
                },
                models = new
                {
                    detector = ModelFile(detectorPath, "paddleocr/ppocrv6/small-det"),
                    orientation = ModelFile(classifierPath, "paddleocr/ppocrv5/mobile-cls"),
                    recognizer = ModelFile(recognizerPath, "paddleocr/ppocrv6/small-rec"),
                    dictionary = ModelFile(dictionaryPath, null)
                },
                protocol = new
                {
                    inputs = images.Length,
                    pipelineMaximumConcurrency = 2,
                    detectorSessionMaximumConcurrency = 2,
                    orientationSessionMaximumConcurrency = 2,
                    recognizerSessionMaximumConcurrency = 2,
                    prefetch = 2,
                    disposeInputOnCompletion = true,
                    resultOrder = "input-order",
                    preparationOverlap = "at-least-two-active-preparations"
                },
                inputs = images.Select(ImageFile).ToArray(),
                observed = new
                {
                    testStatus = "passed",
                    maxPreparationConcurrency = maximumPreparationConcurrency,
                    elapsedMs = Math.Round(elapsedMilliseconds, 3),
                    resultContractSha256 = Sha256(string.Join("\n", resultHashes)),
                    allInputsReturned = true,
                    sourceOrderPreserved = true,
                    allInputsProducedRegions = true,
                    allInputsProducedRecognition = true,
                    distinctResultContracts = resultHashes.Distinct(StringComparer.Ordinal).Count() == resultHashes.Length,
                    inputsDisposedByPipeline = true
                },
                timing = benchmarkSamplesMs == null ? null : new
                {
                    protocol = "5 warm-up + 50 measured",
                    unit = "three-image RunPrefetchedAsync pipeline invocation",
                    p50Ms = Math.Round(Percentile(benchmarkSamplesMs, 0.50), 3),
                    p95Ms = Math.Round(Percentile(benchmarkSamplesMs, 0.95), 3),
                    samplesMs = benchmarkSamplesMs,
                    includes = "image file decode, preprocessing, asynchronous input preparation, DET/CLS/REC execution, and result decoding/merge",
                    excludes = "model loading, session construction, initial size probing, and the five warm-up invocations"
                },
                boundary = benchmarkSamplesMs == null
                    ? "This is a three-image execution, ordering, overlap and ownership contract on one host. It is not a formal performance benchmark, throughput ranking, long-running soak, accuracy score, GPU result or evidence for other models/backends. Session-pool size must still be tuned for the target backend and device."
                    : "This is a one-host, three-image, 5+50 prefetched pipeline latency observation for the exact ORT CPU model/runtime and session-pool configuration. It is not single-image latency, GPU performance, accuracy, a long-running soak, a cross-device ranking or evidence for other models/backends. No CPU frequency lock was configured or independently measured; consult the recorded CPU governor/frequency limits."
            };

            string? directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, Encoding.UTF8);
            Console.WriteLine("PADDLEOCR_SESSION_POOL_REPORT=" + Path.GetFullPath(reportPath));
        }

        private static object ModelFile(string path, string? modelId)
        {
            var info = new FileInfo(path);
            return new
            {
                modelId,
                path = Path.GetFullPath(path),
                sha256 = Sha256File(path),
                bytes = info.Length
            };
        }

        private static object ImageFile(string path)
        {
            var info = new FileInfo(path);
            VisualSize size = ProbeSourceSize(path);
            return new
            {
                path = Path.GetFullPath(path),
                sha256 = Sha256File(path),
                bytes = info.Length,
                sourceSize = size.Width.ToString(CultureInfo.InvariantCulture) + "x" + size.Height.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static string Sha256File(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static bool? ParseNullableBoolean(string? value)
        {
            return bool.TryParse(value, out bool parsed) ? parsed : null;
        }

        private static double? ParseNullableDouble(string? value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : null;
        }

        private static PaddleOcrArtifactContract Artifact(PaddleOcrModelDescriptor descriptor, string? dictionarySha = null)
        {
            PaddleOcrReleaseArtifact release = PaddleOcrModelCatalog.GetReleaseArtifact(descriptor.ModelId);
            return new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854", "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical", "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2", dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");
        }

        private static VisualSize ProbeSourceSize(string imagePath)
        {
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, "probe", new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            return probe.SourceSize;
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path)) Assert.Inconclusive("Required external PaddleOCR input does not exist: " + path);
            return path;
        }

        private static void RequireExternal()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SESSION_POOL_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SESSION_POOL_RUN_EXTERNAL=1 to run the real PP-OCR session-pool contract.");
        }

        private static string Sha256(string value)
        {
            using SHA256 sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        private sealed class PreparationTracker
        {
            private int _active;
            private int _maximum;

            public int MaximumActive => Volatile.Read(ref _maximum);

            public void Enter()
            {
                int active = Interlocked.Increment(ref _active);
                while (true)
                {
                    int observed = Volatile.Read(ref _maximum);
                    if (active <= observed || Interlocked.CompareExchange(ref _maximum, active, observed) == observed) break;
                }
            }

            public void Exit() => Interlocked.Decrement(ref _active);
        }
    }
}
