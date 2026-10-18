using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            var watch = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<OcrResult> results = await pipeline.RunPrefetchedAsync(
                images,
                (image, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    preparationGate.Enter();
                    try
                    {
                        VisualSize sourceSize = ProbeSourceSize(image);
                        return new OpenCvOcrImageInputFactory().CreateFromFile(
                            image,
                            detector.VisualProfile.Input.Name,
                            OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize));
                    }
                    finally { preparationGate.Exit(); }
                },
                prefetch: 2,
                options: new OcrExecutionOptions(disposeInputOnCompletion: true, correlationId: "external-session-pool"),
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
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
