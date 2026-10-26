using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Compares Clamp and SlidingWindow on original annotated quadrilaterals whose
/// recognition width exceeds 3200 pixels, without detector association bias.
/// / 使用归一化识别宽度超过 3200 像素的原始标注四边形对照压缩与滑窗，跳过检测关联偏差。
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrNaturalWideLineExternalIntegrationTests
{
    private const string Revision = "70b6620b2b112597d8219e11eee9773a1403827c";
    private const string AnnotationSha = "ce7085d8bf24c2f13df852715c34494e6748cafcabb4bfc7a6e2bb320f58d23a";
    private const string TestAnnotationSha = "8a8f6cb6dd706b457b822634e9f19b7f51cc9813729d57401a3bb6ea45abd438";
    private const string TrainAnnotationSha = "bf7a271c7a016e66ad87556078f0371f8fb12beaa2e2e60fc33458ad57c3e4fd";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task NaturalWideGroundTruthLinesCompareClampAndSlidingWindowAcrossConfiguredBackends()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_RUN") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_RUN=1 to run source-linked natural wide-line quality evidence.");

        string datasetRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_ROOT") ?? @"F:\OCRBenchmarkTesting";
        string modelRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_MODEL_ROOT") ?? @"E:\Model\paddleocr";
        string split = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_SPLIT") ?? "validation";
        if (split is not ("validation" or "test" or "train")) throw new ArgumentException("Choose the validation, test or train HierText split.");
        string candidatePath = RequireFile(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_CANDIDATES")
            ?? (split == "validation" ? Path.Combine(FindRepoRoot(), "eng/models/paddle-ocr/verification/hiertext-natural-wide-coverage-a2-20261017.json")
                : throw new ArgumentException("Set DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_CANDIDATES for the test or train split.")));
        string annotationPath = RequireFile(Path.Combine(datasetRoot, "artifacts/cache/hiertext/" + split + "-" + Revision + ".jsonl.gz"));
        string expectedAnnotationSha = split switch { "test" => TestAnnotationSha, "train" => TrainAnnotationSha, _ => AnnotationSha };
        Assert.AreEqual(expectedAnnotationSha, ShaFile(annotationPath));
        List<WideLine> lines = LoadLines(candidatePath, annotationPath, datasetRoot, split, expectedAnnotationSha);
        Assert.AreEqual(split switch { "test" => 17, "train" => 12, _ => 13 }, lines.Count);
        if (split == "train") Assert.IsTrue(lines.All(line => line.QualityStratum == "legible-height-at-least-2px"));
        if (split == "validation")
        {
            Assert.AreEqual(11, lines.Count(line => line.QualityStratum == "legible-height-at-least-2px"));
            Assert.AreEqual(1, lines.Count(line => !line.Legible));
            Assert.AreEqual(1, lines.Count(line => line.Height < 2));
        }
        string? selectedKeys = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_KEYS");
        if (!string.IsNullOrWhiteSpace(selectedKeys))
        {
            string[] keys = selectedKeys.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (keys.Length == 0 || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length
                || keys.Any(key => !lines.Any(line => line.Key == key)))
                throw new ArgumentException("Choose unique natural-wide keys present in the pinned candidate report.");
            lines = lines.Where(line => keys.Contains(line.Key, StringComparer.Ordinal)).ToList();
        }

        var rows = new List<LineEvidence>();
        var localTraces = new List<object>();
        bool collectTrace = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRACE") == "1";
        var models = new List<object>();
        var runtimes = new List<object>();
        var engines = new List<object>();
        string outputRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_OUTPUT") ?? TestContext.TestResultsDirectory!;
        Directory.CreateDirectory(outputRoot);
        string progressPath = Path.Combine(outputRoot, "natural-wide-progress-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json");
        void Checkpoint(string backend, string model, string phase, string? lineKey = null, string? mode = null)
            => File.WriteAllText(progressPath, JsonSerializer.Serialize(new { generatedUtc = DateTimeOffset.UtcNow, backend, model, phase, lineKey, mode,
                completedCalls = rows.Count, engines, boundary = "Progress only; an interrupted run has no completed quality/parity result." }, new JsonSerializerOptions { WriteIndented = true }));
        string[] backends = (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_BACKENDS") ?? "onnxruntime,openvino")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (backends.Length == 0 || backends.Distinct(StringComparer.Ordinal).Count() != backends.Length
            || backends.Any(backend => backend is not ("onnxruntime" or "openvino" or "opencv-dnn" or "tensorrt")))
            throw new ArgumentException("Choose unique natural-wide backends: onnxruntime,openvino,opencv-dnn,tensorrt.");
        string engineRoot = Path.Combine(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_ENGINE_ROOT")
            ?? Path.Combine(FindRepoRoot(), "artifacts/paddleocr-natural-wide-engines"), Guid.NewGuid().ToString("N"));
        string? gpuBefore = backends.Contains("tensorrt") ? ReadGpuTelemetry() : null;
        foreach (string backend in backends)
        {
            using var detectorRegistry = new BackendRegistry();
            bool useOpenVino = backend == "openvino";
            // OcrPipeline owns an unused detector Session even in RecognizeOnlyAsync.
            // New REC-only backends use ORT for that dormant Session; no DET is executed.
            if (useOpenVino) detectorRegistry.UseOpenVino(); else detectorRegistry.UseOnnxRuntime();
            BackendId detectorBackendId = useOpenVino ? OpenVinoBackendProvider.BackendId : OnnxRuntimeBackendProvider.BackendId;
            var detectorRequest = new BackendRequest(BackendCapabilities.TensorInference, detectorBackendId, useOpenVino ? "CPU" : "cpu");

            foreach (string size in new[] { "small", "medium" })
            {
                string folder = Path.Combine(modelRoot, "PP-OCRv6", size);
                string detectorPath = RequireFile(Path.Combine(folder, "PP-OCRv6_" + size + "_det_inference.onnx"));
                string recognizerPath = RequireFile(Path.Combine(folder, "PP-OCRv6_" + size + "_rec_inference.onnx"));
                string dictionaryPath = RequireFile(Path.Combine(folder, "PP-OCRv6_" + size + "_rec_dict.txt"));
                var detectorId = new ModelId("paddleocr/ppocrv6/" + size + "-det");
                var recognizerId = new ModelId("paddleocr/ppocrv6/" + size + "-rec");
                PaddleOcrReleaseArtifact detRelease = PaddleOcrModelCatalog.GetReleaseArtifact(detectorId);
                PaddleOcrReleaseArtifact recRelease = PaddleOcrModelCatalog.GetReleaseArtifact(recognizerId);
                Assert.AreEqual(detRelease.Sha256, ShaFile(detectorPath));
                Assert.AreEqual(recRelease.Sha256, ShaFile(recognizerPath));
                Assert.AreEqual(DictionarySha, ShaFile(dictionaryPath));
                OcrCharacterSet characters = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "natural-wide-v6-" + size, size, true, DictionarySha);
                PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorId, Artifact(detRelease), maximumBatch: 1);
                PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerId, Artifact(recRelease, DictionarySha), characters, maximumBatch: 1);
                Assert.AreEqual(3200, recognizer.CropProfile!.MaximumWidth);
                Assert.AreEqual(48, recognizer.CropProfile.TargetHeight);
                if (backend == backends[0]) models.Add(new { model = size, detectorId = detectorId.Value, recognizerId = recognizerId.Value,
                    detectorSha256 = detRelease.Sha256, recognizerSha256 = recRelease.Sha256, dictionarySha256 = DictionarySha });

                using var recognizerRegistry = new BackendRegistry();
                BackendId backendId;
                string selectedRecognizerPath = recognizerPath;
                switch (backend)
                {
                    case "onnxruntime": recognizerRegistry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; break;
                    case "openvino": recognizerRegistry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; break;
                    case "opencv-dnn":
                        var contract = new OpenCvDnnModelContract(recognizerId,
                            new[] { new TensorDescriptor(recognizer.VisualProfile.Input.Name, TensorElementType.Float32, new TensorShape(1, 3, 48, -1)) },
                            new[] { new TensorDescriptor(recognizer.VisualProfile.Outputs[0].Name, TensorElementType.Float32,
                                new TensorShape(1, -1, ((GreedyCtcDecoder)recognizer.VisualProfile.Decoder).ExpectedClassCount)) });
                        recognizerRegistry.UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
                        backendId = OpenCvDnnBackendProvider.BackendId;
                        break;
                    case "tensorrt":
                        Checkpoint(backend, size, "building-engine");
                        TensorRtApiVersion api = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRT_API") switch
                        {
                            null or "11" => TensorRtApiVersion.TensorRt11,
                            "10" => TensorRtApiVersion.TensorRt10,
                            _ => throw new ArgumentException("Natural-wide TRT API must be 10 or 11.")
                        };
                        var min = new TensorShape(1, 3, 48, recognizer.CropProfile.MinimumWidth);
                        var opt = new TensorShape(1, 3, 48, 320);
                        var max = new TensorShape(1, 3, 48, 3200);
                        string? prebuiltRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_PREBUILT_ENGINE_ROOT");
                        string engineSha;
                        if (string.IsNullOrWhiteSpace(prebuiltRoot))
                        {
                            Directory.CreateDirectory(engineRoot);
                            TensorRtOnnxEngineBuildResult engine = new TensorRtOnnxEngineBuilder().Build(recognizer.CreateArtifact(recognizerPath),
                                Path.Combine(engineRoot, "v6-" + size + "-rec.engine"), new TensorRtOnnxEngineBuildOptions(apiVersion: api,
                                    precision: TensorRtOnnxEnginePrecision.RuntimeDefault, workspaceBytes: 268435456UL, optimizationLevel: 0,
                                    inputProfiles: new[] { new TensorRtOnnxInputProfile(recognizer.VisualProfile.Input.Name, min, opt, max) }));
                            Assert.IsTrue(engine.EngineBytes > 0);
                            engines.Add(new { model = size, engine.EngineSha256, engine.EngineBytes, engine.BuildInputsSha256, engine.OnnxSha256,
                                api = (int)api, precision = engine.Precision.ToString(), optimizationLevel = 0, workspaceBytes = 268435456,
                                minimumShape = min.ToArray(), optimumShape = opt.ToArray(), maximumShape = max.ToArray() });
                            selectedRecognizerPath = engine.EnginePath;
                            engineSha = engine.EngineSha256;
                        }
                        else
                        {
                            selectedRecognizerPath = RequireFile(Path.Combine(prebuiltRoot, "v6-" + size + "-rec.engine"));
                            string? expectedSha = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_PREBUILT_SHA_" + size.ToUpperInvariant());
                            if (expectedSha == null || expectedSha.Length != 64 || !expectedSha.All(Uri.IsHexDigit))
                                throw new ArgumentException("A pinned 64-hex prebuilt Engine SHA is required for each recognizer.");
                            engineSha = ShaFile(selectedRecognizerPath);
                            Assert.AreEqual(expectedSha.ToLowerInvariant(), engineSha);
                            long engineBytes = new FileInfo(selectedRecognizerPath).Length;
                            Assert.IsTrue(engineBytes > 0);
                            engines.Add(new { model = size, EngineSha256 = engineSha, EngineBytes = engineBytes, OnnxSha256 = recRelease.Sha256,
                                api = (int)api, source = "prebuilt-exact-sha", profileSource = "caller-pinned Engine; profile not re-inspected" });
                        }
                        Checkpoint(backend, size, "engine-built");
                        recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerId, Artifact(recRelease, DictionarySha, engineSha), characters, maximumBatch: 1);
                        recognizerRegistry.UseTensorRT(new TensorRtBackendOptions(api));
                        backendId = TensorRtBackendProvider.BackendId;
                        break;
                    default: throw new InvalidOperationException("Unexpected backend.");
                }
                var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, backend == "tensorrt" ? "cuda" : useOpenVino ? "CPU" : "cpu");
                var profiles = new VisualProfileRegistry();
                profiles.Register(detector.VisualProfile); profiles.Register(recognizer.VisualProfile); profiles.Freeze();
                VisualProfileSelection detSelection = profiles.Select(detector.CreateArtifact(detectorPath, detectorBackendId), detectorRegistry, detectorRequest, VisualTaskId.TextDetection);
                VisualProfileSelection recSelection = profiles.Select(recognizer.CreateArtifact(selectedRecognizerPath, backendId), recognizerRegistry, request, VisualTaskId.TextRecognition);
                TextCropProfile recognizerCrop = recognizer.CropProfile ?? throw new InvalidOperationException("Recognition profile requires crop options.");
                foreach (bool sliding in new[] { false, true })
                {
                    Checkpoint(backend, size, "creating-pipeline", mode: sliding ? "SlidingWindow" : "Clamp");
                    TextCropProfile crop = sliding ? recognizerCrop.WithRecognitionWindows(new OcrRecognitionWindowOptions(.2, 32)) : recognizerCrop;
                    using var pipeline = new OcrPipeline(detectorRegistry, detSelection, detectorRequest, recognizerRegistry, recSelection, request, crop,
                        new OcrPipelineOptions(maximumRegions: 1, maximumRecognitionBatch: 1, maximumSourcePixels: 128L * 1024L * 1024L),
                        new SessionOptions(1), new SessionOptions(1));
                    foreach (WideLine line in lines)
                    {
                        Checkpoint(backend, size, "recognizing", line.Key, sliding ? "SlidingWindow" : "Clamp");
                        var region = new TextRegion(line.Index, 1, line.Quadrilateral.Polygon, line.Quadrilateral);
                        long naturalWidth = crop.DescribeWidth(line.Quadrilateral, TextOrientation.Degrees0).NaturalWidth;
                        Assert.IsTrue(naturalWidth > 3200, line.Key);
                        IReadOnlyList<OcrRecognitionWindow> planned = OcrRecognitionWindowPlanner.Plan(region, crop);
                        // DET and CLS are deliberately bypassed. The 32px prepared detection
                        // tensor is unused; crop sampling still uses the original source image.
                        using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(line.ImagePath,
                            detector.VisualProfile.Input.Name, new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
                        IReadOnlyList<OcrRegionResult> output = await pipeline.RecognizeOnlyAsync(input, new[] { region }).ConfigureAwait(false);
                        Assert.AreEqual(1, output.Count);
                        OcrRegionResult result = output[0];
                        Assert.AreEqual(line.Index, result.Region.SourceIndex);
                        Assert.AreEqual(line.Index, result.Recognition.SourceRegionIndex);
                        CollectionAssert.AreEqual(line.Quadrilateral.Polygon.Vertices.ToArray(), result.Region.Polygon.Vertices.ToArray());
                        Assert.AreEqual(naturalWidth, result.RecognitionWidth!.Value.NaturalWidth);
                        ValidateTrace(result.Recognition);
                        if (sliding)
                        {
                            Assert.IsTrue(result.RecognitionWindows.Count >= 2);
                            Assert.AreEqual(planned.Count, result.RecognitionWindows.Count);
                            Assert.AreEqual(planned.Count, result.RecognitionWidth.Value.WindowCount);
                            Assert.IsFalse(result.RecognitionWidth.Value.WidthClamped);
                            Assert.AreEqual(0d, result.RecognitionWindows[0].Start);
                            Assert.AreEqual(1d, result.RecognitionWindows[^1].End);
                            var reconstructed = new StringBuilder();
                            for (int index = 0; index < result.RecognitionWindows.Count; index++)
                            {
                                OcrRecognitionWindowResult window = result.RecognitionWindows[index];
                                Assert.AreEqual(index, window.Index);
                                Assert.AreEqual(planned[index].Start, window.Start);
                                Assert.AreEqual(planned[index].End, window.End);
                                Assert.IsTrue(window.Width.TargetWidth <= 3200 && window.Width.TensorWidth <= 3200);
                                Assert.IsFalse(window.Width.WidthClamped);
                                Assert.AreEqual(line.Index, window.Recognition.SourceRegionIndex);
                                if (index > 0) Assert.IsTrue(window.Start < result.RecognitionWindows[index - 1].End);
                                ValidateTrace(window.Recognition);
                                foreach (OcrToken token in window.Recognition.Tokens.Where(token => token.Emitted).Skip(window.RemovedPrefixTokens)) reconstructed.Append(token.Text);
                            }
                            Assert.AreEqual(result.Recognition.Text, reconstructed.ToString());
                        }
                        else
                        {
                            Assert.AreEqual(0, result.RecognitionWindows.Count);
                            Assert.AreEqual(1, result.RecognitionWidth.Value.WindowCount);
                            Assert.IsTrue(result.RecognitionWidth.Value.WidthClamped);
                        }
                        OcrTextAccuracyMetrics raw = OcrTextAccuracy.Compare(line.Text, result.Recognition.Text);
                        OcrTextAccuracyMetrics lower = OcrTextAccuracy.Compare(line.Text.ToLowerInvariant(), result.Recognition.Text.ToLowerInvariant());
                        object[] windows = result.RecognitionWindows.Select(window => (object)new
                        {
                            window.Index, window.Start, window.End, naturalWidth = window.Width.NaturalWidth, targetWidth = window.Width.TargetWidth,
                            tensorWidth = window.Width.TensorWidth, window.RemovedPrefixTokens, window.SeamUncertain, window.OverlapEditDistance,
                            textSha256 = Sha(window.Recognition.Text), emittedClassSha256 = ClassSha(window.Recognition), emittedTokens = window.Recognition.Tokens.Count(token => token.Emitted)
                        }).ToArray();
                        rows.Add(new LineEvidence(backend, size, sliding ? "SlidingWindow" : "Clamp", line.Key, line.Index, line.QualityStratum,
                            naturalWidth, Sha(line.Text), Sha(result.Recognition.Text), ClassSha(result.Recognition), raw, lower, windows,
                            result.RecognitionWindows.Sum(window => window.RemovedPrefixTokens), result.RecognitionWindows.Count(window => window.SeamUncertain)));
                        if (collectTrace) localTraces.Add(new { backend, model = size, mode = crop.OverflowMode.ToString(), line.Key,
                            expected = line.Text, recognized = result.Recognition.Text, result.RecognitionWidth,
                            windows = result.RecognitionWindows.Select(window => new { window.Index, window.Start, window.End,
                                window.Width, window.RemovedPrefixTokens, window.SeamUncertain, window.OverlapEditDistance,
                                window.Recognition.Text, window.Recognition.Tokens }).ToArray() });
                    }
                    TestContext.WriteLine(backend + "/" + size + "/" + crop.OverflowMode + ": " + lines.Count + " original quadrilaterals completed.");
                }
            }
            Checkpoint(backend, "all", "backend-completed");
            string nativeVersion = backend switch
            {
                "onnxruntime" => Microsoft.ML.OnnxRuntime.OrtEnv.Instance().GetVersionString(),
                "openvino" => OpenVinoVersion(),
                "opencv-dnn" => OpenCvRuntimePreflight.Check().NativeVersion,
                "tensorrt" => LoadedTensorRtVersion(),
                _ => throw new InvalidOperationException()
            };
            runtimes.Add(new { backend, nativeVersion });
        }

        var parity = rows.GroupBy(row => row.Model + "/" + row.Mode + "/" + row.Key).Select(group => new
        {
            key = group.Key, rows = group.Count(), textEqual = group.Select(row => row.TextSha256).Distinct().Count() == 1,
            emittedClassEqual = group.Select(row => row.EmittedClassSha256).Distinct().Count() == 1,
            windowsEqual = group.Select(row => JsonSerializer.Serialize(row.Windows)).Distinct().Count() == 1
        }).ToArray();
        var quality = rows.GroupBy(row => row.Backend + "/" + row.Model + "/" + row.Mode + "/" + row.QualityStratum)
            .Select(group => new { key = group.Key, count = group.Count(), raw = Aggregate(group.Select(row => row.Raw)), lowercase = Aggregate(group.Select(row => row.Lowercase)),
                windows = group.Sum(row => row.Windows.Length), removedPrefixTokens = group.Sum(row => row.RemovedPrefixTokens), uncertainSeams = group.Sum(row => row.UncertainSeams) }).ToArray();
        var paired = rows.GroupBy(row => row.Backend + "/" + row.Model + "/" + row.Key).Select(group =>
        {
            LineEvidence clamp = group.Single(row => row.Mode == "Clamp"), sliding = group.Single(row => row.Mode == "SlidingWindow");
            return new { key = group.Key, stratum = clamp.QualityStratum, rawCerEditsDelta = sliding.Raw.CharacterEditDistance - clamp.Raw.CharacterEditDistance,
                rawWerEditsDelta = sliding.Raw.WordEditDistance - clamp.Raw.WordEditDistance,
                lowercaseCerEditsDelta = sliding.Lowercase.CharacterEditDistance - clamp.Lowercase.CharacterEditDistance,
                lowercaseWerEditsDelta = sliding.Lowercase.WordEditDistance - clamp.Lowercase.WordEditDistance };
        }).ToArray();
        string report = Path.GetFullPath(Path.Combine(outputRoot, "paddleocr-hiertext-natural-wide-gt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json"));
        object? localTraceEvidence = null;
        if (collectTrace)
        {
            string tracePath = Path.ChangeExtension(report, ".local-traces.json");
            File.WriteAllText(tracePath, JsonSerializer.Serialize(localTraces, new JsonSerializerOptions { WriteIndented = true }));
            localTraceEvidence = new { fileName = Path.GetFileName(tracePath), sha256 = ShaFile(tracePath),
                scope = "Local diagnostic artifact with annotation text and raw window tokens; excluded from repository summaries." };
        }
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 2, generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_SOURCE_REVISION") ?? "unknown",
            testAssemblySha256 = ShaFile(GetType().Assembly.Location), visualAssemblySha256 = ShaFile(typeof(OcrPipeline).Assembly.Location),
            environment = new { os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription,
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessors = Environment.ProcessorCount },
            dataset = new { name = "HierText", revision = Revision, split, annotationSha256 = expectedAnnotationSha,
                candidateReportSha256 = ShaFile(candidatePath), lines = lines.Select(line => new { line.Key, line.ImageId, line.ParagraphIndex, line.LineIndex,
                    line.ImageSha256, line.Legible, line.Handwritten, line.Vertical, sourceHeight = line.Height, qualityStratum = line.QualityStratum }).ToArray() },
            protocol = new { path = "GT quadrilateral -> RecognizeOnlyAsync; DET/CLS bypassed", maximumWidthPixels = 3200, targetHeight = 48,
                overlapRatio = .2, batchSize = 1, sessions = 1, order = "backend -> model -> Clamp then SlidingWindow -> pinned candidate order",
                selection = selectedKeys == null ? "complete pinned candidate cohort; not necessarily every eligible line in the split" : "explicit diagnostic key subset; not the complete quality cohort",
                metrics = "Unicode-scalar CER and whitespace-token WER; raw ordinal and explicit invariant-lowercase views; no punctuation/space removal",
                qualityStrata = "Main quality: legible=true and source bbox height>=2px; official illegible and sub-2px geometry retained separately. Counts come from dataset.lines.",
                measurements = "One untimed execution per line/mode/backend; no speed or statistical significance claim",
                detectorSession = "Required by OcrPipeline construction but never executed; OpenCV/TensorRT recognizer-only paths keep this dormant Session on ORT CPU. All crop/window/CTC/merge code is shared.",
                crossBackend = backends.Length > 1 ? "All configured backends compared by the same model/mode/line keys; differences recorded, not assumed absent" : "Not compared within a single-backend run; use the source-linked comparison against a separate reference report" },
            models, runtimes, engines, quality, paired, parity, rows, localTraceEvidence,
            gpuObservation = new { before = gpuBefore, after = backends.Contains("tensorrt") ? ReadGpuTelemetry() : null,
                scope = "Snapshots only, no clock control or during-run utilization/power sampling; quality execution, not a performance benchmark" },
            boundary = "A2 is a >3200 normalized-pixel-width gate, not a 3200-character gate. This bounded geometry-selected slice is recognizer-only, not detector/page accuracy, official HierText scoring, seam ground truth or release acceptance. An explicit key subset is diagnostic only and cannot replace the complete cohort. Repeated-number/single-character seam sets, further backends and wider independent validation remain open."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        TestContext.WriteLine("NATURAL_WIDE_GT_REPORT=" + report);
        Assert.AreEqual(lines.Count * 4 * backends.Length, rows.Count);
        Assert.AreEqual(lines.Count * 4, parity.Length);
        Assert.IsTrue(parity.All(pair => pair.rows == backends.Length));
    }

    private static object Aggregate(IEnumerable<OcrTextAccuracyMetrics> metrics)
    {
        OcrTextAccuracyMetrics[] values = metrics.ToArray();
        int characters = values.Sum(item => item.ReferenceCharacters), words = values.Sum(item => item.ReferenceWords);
        int characterEdits = values.Sum(item => item.CharacterEditDistance), wordEdits = values.Sum(item => item.WordEditDistance);
        return new { referenceCharacters = characters, characterEdits, cer = characters == 0 ? 0 : (double)characterEdits / characters,
            referenceWords = words, wordEdits, wer = words == 0 ? 0 : (double)wordEdits / words };
    }

    private static void ValidateTrace(RecognizedText text)
    {
        Assert.AreEqual(text.Text, string.Concat(text.Tokens.Where(token => token.Emitted).Select(token => token.Text)));
        for (int index = 0; index < text.Tokens.Count; index++) Assert.AreEqual(index, text.Tokens[index].Timestep);
    }

    private static List<WideLine> LoadLines(string candidatePath, string annotationPath, string datasetRoot,
        string split, string expectedAnnotationSha)
    {
        using JsonDocument candidates = JsonDocument.Parse(File.ReadAllText(candidatePath));
        Assert.AreEqual("deploysharp-paddleocr-hiertext-natural-wide-a2-v1", candidates.RootElement.GetProperty("schemaVersion").GetString());
        Assert.AreEqual(expectedAnnotationSha, candidates.RootElement.GetProperty("source").GetProperty("annotationSha256").GetString());
        if (candidates.RootElement.GetProperty("source").TryGetProperty("split", out JsonElement auditSplit))
            Assert.AreEqual(split, auditSplit.GetString());
        else Assert.AreEqual("validation", split);
        using FileStream file = File.OpenRead(annotationPath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using JsonDocument annotations = JsonDocument.Parse(gzip);
        Dictionary<string, JsonElement> pages = annotations.RootElement.GetProperty("annotations").EnumerateArray()
            .ToDictionary(page => page.GetProperty("image_id").GetString()!, StringComparer.Ordinal);
        var result = new List<WideLine>();
        foreach (JsonElement candidate in candidates.RootElement.GetProperty("candidates").EnumerateArray())
        {
            string imageId = candidate.GetProperty("imageId").GetString()!;
            int p = candidate.GetProperty("paragraphIndex").GetInt32(), l = candidate.GetProperty("lineIndex").GetInt32();
            JsonElement line = pages[imageId].GetProperty("paragraphs")[p].GetProperty("lines")[l];
            string text = line.GetProperty("text").GetString()!;
            Assert.IsFalse(string.IsNullOrWhiteSpace(text));
            PointF[] points = line.GetProperty("vertices").EnumerateArray().Select(point => new PointF(point[0].GetSingle(), point[1].GetSingle())).ToArray();
            Assert.AreEqual(4, points.Length);
            double[] candidatePoints = candidate.GetProperty("vertices").EnumerateArray().Select(value => value.GetDouble()).ToArray();
            for (int index = 0; index < 4; index++) { Assert.AreEqual(candidatePoints[index * 2], points[index].X); Assert.AreEqual(candidatePoints[index * 2 + 1], points[index].Y); }
            double height = points.Max(point => point.Y) - points.Min(point => point.Y);
            bool legible = line.GetProperty("legible").GetBoolean();
            string stratum = !legible ? "official-illegible" : height < 2 ? "one-pixel-height-diagnostic" : "legible-height-at-least-2px";
            string imagePath = RequireFile(Path.Combine(datasetRoot, "data/images/hiertext", split, imageId + ".jpg"));
            string imageSha = ShaFile(imagePath);
            Assert.AreEqual(candidate.GetProperty("imageSha256").GetString(), imageSha);
            result.Add(new WideLine(result.Count, imageId + "/" + p + "/" + l, imageId, p, l, text, imagePath, imageSha,
                new TextQuadrilateral(points[0], points[1], points[2], points[3], TextCornerOrder.TopLeftClockwise), height, legible,
                line.GetProperty("handwritten").GetBoolean(), line.GetProperty("vertical").GetBoolean(), stratum));
        }
        return result;
    }

    private static string OpenVinoVersion()
    {
        string? path = Directory.EnumerateFiles(AppContext.BaseDirectory, "openvino_c.dll", SearchOption.AllDirectories).FirstOrDefault();
        return path == null ? "unavailable" : FileVersionInfo.GetVersionInfo(path).ProductVersion ?? "unknown";
    }

    private static string FindRepoRoot()
    {
        for (DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "eng/models/paddle-ocr/verification/hiertext-natural-wide-coverage-a2-20261017.json"))) return dir.FullName;
        throw new DirectoryNotFoundException("Set DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_CANDIDATES to the pinned candidate JSON.");
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null, string? engineSha = null)
        => new PaddleOcrArtifactContract(release.Opset, engineSha ?? release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            modelFormat: engineSha == null ? "onnx" : "tensorrt-engine",
            dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string LoadedTensorRtVersion()
    {
        using Process process = Process.GetCurrentProcess();
        ProcessModule module = process.Modules.Cast<ProcessModule>().Single(module =>
            System.Text.RegularExpressions.Regex.IsMatch(module.ModuleName, @"^nvinfer_(8|10|11)\.dll$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        // Vendor DLLs may have no FileVersionInfo resource. Query the loaded core
        // library; NvInferRuntimeBase.h defines (MAJOR * 100 + MINOR) * 100 + PATCH.
        IntPtr address = NativeLibrary.GetExport(module.BaseAddress, "getInferLibVersion");
        int version = Marshal.GetDelegateForFunctionPointer<InferLibraryVersion>(address)();
        if (version <= 0) throw new InvalidOperationException("TensorRT returned an invalid native library version.");
        return module.ModuleName + ":" + (version / 10000) + "." + (version / 100 % 100) + "." + (version % 100)
            + " (getInferLibVersion=" + version + ")";
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InferLibraryVersion();

    private static string ReadGpuTelemetry()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("nvidia-smi", "--query-gpu=name,driver_version,pstate,clocks.gr,clocks.mem,memory.used,memory.total --format=csv,noheader")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process == null) return "unavailable";
            if (!process.WaitForExit(5000)) { process.Kill(); return "timeout"; }
            return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd().Trim() : "nvidia-smi exit=" + process.ExitCode;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) { return exception.GetType().Name; }
    }

    private static string RequireFile(string path) => File.Exists(path) ? path : throw new FileNotFoundException("Required external wide-line asset is missing.", path);
    private static string ShaFile(string path) { using FileStream file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(); }
    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string ClassSha(RecognizedText text) => Sha(string.Join(",", text.Tokens.Where(token => token.Emitted).Select(token => token.ClassIndex)));
    private sealed record WideLine(int Index, string Key, string ImageId, int ParagraphIndex, int LineIndex, string Text, string ImagePath,
        string ImageSha256, TextQuadrilateral Quadrilateral, double Height, bool Legible, bool Handwritten, bool Vertical, string QualityStratum);
    private sealed record LineEvidence(string Backend, string Model, string Mode, string Key, int SourceIndex, string QualityStratum, long NaturalWidth,
        string ExpectedTextSha256, string TextSha256, string EmittedClassSha256, OcrTextAccuracyMetrics Raw, OcrTextAccuracyMetrics Lowercase,
        object[] Windows, int RemovedPrefixTokens, int UncertainSeams);
}
