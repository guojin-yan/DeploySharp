using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OrtEnv = Microsoft.ML.OnnxRuntime.OrtEnv;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Measures whether the opt-in PP-OCR crop-enhancement consensus policy changes
/// source-labelled SROIE receipt words for the better or worse. This is a
/// natural-source crop diagnostic, not a low-quality dataset or release gate.
/// / 评估可选 PP-OCR 裁剪增强共识策略对 SROIE 原始收据词区的影响；这是自然图像裁剪诊断，不是低质量数据集或发布门禁。
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleOcrSroieNaturalEnhancementIntegrationTests
{
    private const string DefaultDatasetRoot = @"F:\OCRBenchmarkTesting";
    private const string DefaultModelRoot = @"E:\Model\paddleocr";
    private const string DatasetRevision = "bffe40c26759f3376ec2b3ae9031dbba54cd587c";
    private const string DictionarySha = "769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6";
    private const string DetectorModelId = "paddleocr/ppocrv6/small-det";
    private const string RecognizerModelId = "paddleocr/ppocrv6/small-rec";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task NaturalSroieWordsMeasureEnhancementFalseCorrectionsOnOrtAndOpenVino()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ENHANCEMENT_RUN"), "1", StringComparison.Ordinal))
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLEOCR_SROIE_ENHANCEMENT_RUN=1 to run the source-labelled SROIE enhancement diagnostic.");

        string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ENHANCEMENT_DATASET_ROOT") ?? DefaultDatasetRoot);
        string modelRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ENHANCEMENT_MODEL_ROOT") ?? DefaultModelRoot);
        string[] manifestPaths =
        {
            Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072455614249Z.jsonl"),
            Path.Combine(datasetRoot, "data", "annotations", "manifests", "sroie-train-20260923T072550009854Z.jsonl")
        };
        IReadOnlyList<SroiePage> pages = LoadPages(datasetRoot, manifestPaths);
        Assert.AreEqual(10, pages.Count, "The pinned diagnostic selection must contain ten SROIE receipt pages.");

        string modelDirectory = Path.Combine(modelRoot, "PP-OCRv6", "small");
        string detectorPath = RequireFile(Path.Combine(modelDirectory, "PP-OCRv6_small_det_inference.onnx"));
        string recognizerPath = RequireFile(Path.Combine(modelDirectory, "PP-OCRv6_small_rec_inference.onnx"));
        string dictionaryPath = RequireFile(Path.Combine(modelDirectory, "PP-OCRv6_small_rec_dict.txt"));
        string actualDictionarySha = Sha256File(dictionaryPath);
        Assert.AreEqual(DictionarySha, actualDictionarySha, "The character dictionary must match the pinned PP-OCRv6 Small revision.");

        var recipes = new OcrCropEnhancementOptions[]
        {
            new OcrCropEnhancementOptions(OcrCropEnhancementMode.ContrastNormalize,
                lowContrastThreshold: 128, minimumContrast: 1, targetStandardDeviation: 128, maximumGain: 3),
            new OcrCropEnhancementOptions(OcrCropEnhancementMode.GrayClahe,
                lowContrastThreshold: 128, minimumContrast: 1, targetStandardDeviation: 128,
                claheClipLimit: 2, claheGridSize: 8),
            new OcrCropEnhancementOptions(OcrCropEnhancementMode.UnsharpMask,
                sharpnessThreshold: 1000000, sharpenKernelSize: 3, sharpenAmount: .5, sharpenSigma: 1)
        };

        var backendReports = new List<BackendReport>();
        foreach (bool useOpenVino in new[] { false, true })
        {
            backendReports.Add(await RunBackendAsync(pages, detectorPath, recognizerPath, dictionaryPath, recipes, useOpenVino).ConfigureAwait(false));
        }

        BackendReport ort = backendReports.Single(item => item.Backend == "onnxruntime");
        BackendReport openVino = backendReports.Single(item => item.Backend == "openvino");
        Assert.AreEqual(ort.Regions.Count, openVino.Regions.Count, "Both CPU backends must evaluate the same labelled word regions.");
        Assert.IsTrue(ort.Regions.Select(item => item.RegionId).SequenceEqual(openVino.Regions.Select(item => item.RegionId)),
            "Backend runs must preserve the pinned page/region order.");

        int textMatched = 0;
        int finalTextMatched = 0;
        int decisionMatched = 0;
        int candidateTextsMatched = 0;
        for (int index = 0; index < ort.Regions.Count; index++)
        {
            RegionReport left = ort.Regions[index];
            RegionReport right = openVino.Regions[index];
            if (left.OriginalTextSha256 == right.OriginalTextSha256) textMatched++;
            if (left.FinalTextSha256 == right.FinalTextSha256) finalTextMatched++;
            if (left.Decision == right.Decision && left.SelectedCandidateIndex == right.SelectedCandidateIndex) decisionMatched++;
            if (left.Candidates.Count == right.Candidates.Count && left.Candidates.Select(item => item.TextSha256).SequenceEqual(right.Candidates.Select(item => item.TextSha256))) candidateTextsMatched++;
        }

        string outputDirectory = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_SROIE_ENHANCEMENT_OUTPUT") ?? TestContext.TestResultsDirectory!);
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "paddleocr-sroie-natural-source-enhancement-20261023.json");
        var evidence = new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_SOURCE_REVISION") ?? "unknown",
            model = new
            {
                detectorId = DetectorModelId,
                detectorSha256 = PaddleOcrModelCatalog.GetReleaseArtifact(new ModelId(DetectorModelId)).Sha256,
                recognizerId = RecognizerModelId,
                recognizerSha256 = PaddleOcrModelCatalog.GetReleaseArtifact(new ModelId(RecognizerModelId)).Sha256,
                dictionarySha256 = actualDictionarySha
            },
            dataset = new
            {
                name = "jsdnrs/ICDAR2019-SROIE",
                revision = DatasetRevision,
                declaredLicense = "CC-BY-4.0 (dataset distributor metadata; benchmark smoke selection only)",
                manifestSha256 = manifestPaths.Select(path => new { path = Path.GetFileName(path), sha256 = Sha256File(path) }).ToArray(),
                pageCount = pages.Count,
                labeledWordRegionCount = pages.Sum(page => page.Regions.Count),
                sourceImageSha256 = pages.Select(page => new { page.ImageId, page.Sha256 }).ToArray(),
                imagingQualityLabels = "unknown in source; this is not a natural low-quality split"
            },
            environment = new
            {
                machine = Environment.MachineName,
                os = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                logicalProcessorCount = Environment.ProcessorCount,
                cpuModel = (Environment.GetEnvironmentVariable("DEPLOYSHARP_CPU_MODEL") ?? "unspecified").Trim()
            },
            protocol = new
            {
                execution = "recognizer-only over all valid source-labelled word quadrilaterals; detector bypassed",
                backends = new[] { "onnxruntime-cpu", "openvino-cpu" },
                batchSize = 16,
                sessionConcurrency = 1,
                selection = "ConsensusConfidenceGain(minimumGain=0,minimumMatchingCandidates=2)",
                recipes = recipes.Select(item => item.Mode.ToString()).ToArray(),
                pageTiming = "one measured invocation per page; diagnostic only, not a formal 5+50 performance benchmark"
            },
            backends = backendReports.Select(item => new
            {
                backend = item.Backend,
                runtime = item.Runtime,
                pageCount = item.Pages.Count,
                regionCount = item.Regions.Count,
                baseline = item.Baseline,
                final = item.Final,
                selectedRegions = item.SelectedRegions,
                selectedBetterCerRegions = item.SelectedBetterCerRegions,
                selectedWorseCerRegions = item.SelectedWorseCerRegions,
                selectedEqualCerRegions = item.SelectedEqualCerRegions,
                selectedBetterWerRegions = item.SelectedBetterWerRegions,
                selectedWorseWerRegions = item.SelectedWorseWerRegions,
                selectedEqualWerRegions = item.SelectedEqualWerRegions,
                candidateRecipes = SummarizeCandidates(item.Regions),
                pageLatencyMs = new { p50 = Percentile(item.Pages.Select(page => page.ElapsedMs), .50), p95 = Percentile(item.Pages.Select(page => page.ElapsedMs), .95), samples = item.Pages.Select(page => new { page.ImageId, page.ElapsedMs }).ToArray() },
                pages = item.Pages,
                regions = item.Regions
            }).ToArray(),
            crossBackend = new
            {
                regionsCompared = ort.Regions.Count,
                originalTextMatched = textMatched,
                enhancedTextMatched = finalTextMatched,
                allCandidateTextMatched = candidateTextsMatched,
                decisionAndCandidateIndexMatched = decisionMatched
            },
            boundary = "Natural-source receipts and source word labels; imaging quality is unlabelled. This is a bounded crop-level diagnostic on ten pages, not an independent natural-low-quality split, official SROIE metric, page OCR score, business-field error budget, default enhancement approval, or formal latency result. Enhancement remains opt-in; B1b/B2/B3 stay open."
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        TestContext.AddResultFile(outputPath);

        Console.WriteLine("PADDLEOCR_SROIE_ENHANCEMENT pages=" + pages.Count.ToString(CultureInfo.InvariantCulture)
            + ";regions=" + ort.Regions.Count.ToString(CultureInfo.InvariantCulture)
            + ";ortSelected=" + ort.SelectedRegions.ToString(CultureInfo.InvariantCulture)
            + ";ortWorseCer=" + ort.SelectedWorseCerRegions.ToString(CultureInfo.InvariantCulture)
            + ";openvinoSelected=" + openVino.SelectedRegions.ToString(CultureInfo.InvariantCulture)
            + ";openvinoWorseCer=" + openVino.SelectedWorseCerRegions.ToString(CultureInfo.InvariantCulture)
            + ";crossBackendText=" + textMatched.ToString(CultureInfo.InvariantCulture) + "/" + ort.Regions.Count.ToString(CultureInfo.InvariantCulture)
            + ";crossBackendDecision=" + decisionMatched.ToString(CultureInfo.InvariantCulture) + "/" + ort.Regions.Count.ToString(CultureInfo.InvariantCulture)
            + ";output=" + outputPath);
    }

    private async Task<BackendReport> RunBackendAsync(IReadOnlyList<SroiePage> pages,
        string detectorPath, string recognizerPath, string dictionaryPath, IReadOnlyList<OcrCropEnhancementOptions> recipes, bool useOpenVino)
    {
        string backend = useOpenVino ? "openvino" : "onnxruntime";
        string runtime = useOpenVino
            ? "OpenVINO CPU " + GetOpenVinoRuntimeVersion()
            : "ONNX Runtime CPU " + OrtEnv.Instance().GetVersionString();
        BackendId backendId;
        using var registry = new BackendRegistry();
        if (useOpenVino)
        {
            registry.UseOpenVino();
            backendId = OpenVinoBackendProvider.BackendId;
        }
        else
        {
            registry.UseOnnxRuntime();
            backendId = OnnxRuntimeBackendProvider.BackendId;
        }

        PaddleOcrModelDescriptor detectorDescriptor = PaddleOcrModelCatalog.Find(new ModelId(DetectorModelId));
        PaddleOcrModelDescriptor recognizerDescriptor = PaddleOcrModelCatalog.Find(new ModelId(RecognizerModelId));
        PaddleOcrReleaseArtifact detectorRelease = PaddleOcrModelCatalog.GetReleaseArtifact(new ModelId(DetectorModelId));
        PaddleOcrReleaseArtifact recognizerRelease = PaddleOcrModelCatalog.GetReleaseArtifact(new ModelId(RecognizerModelId));
        OcrCharacterSet characterSet = PaddleOcrProfiles.LoadCharacterSet(dictionaryPath, "sroie-natural-enhancement-v6-small", "v6-small", true, DictionarySha);
        PaddleOcrProfile detector = PaddleOcrModelCatalog.CreateProfile(detectorDescriptor, Artifact(detectorRelease));
        PaddleOcrProfile recognizer = PaddleOcrModelCatalog.CreateProfile(recognizerDescriptor, Artifact(recognizerRelease, DictionarySha), characterSet, maximumBatch: 16);

        var profiles = new VisualProfileRegistry();
        profiles.Register(detector.VisualProfile);
        profiles.Register(recognizer.VisualProfile);
        profiles.Freeze();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, backendId, useOpenVino ? "CPU" : "cpu");
        VisualProfileSelection detectorSelection = profiles.Select(detector.CreateArtifact(detectorPath, backendId), registry, request, VisualTaskId.TextDetection);
        VisualProfileSelection recognizerSelection = profiles.Select(recognizer.CreateArtifact(recognizerPath, backendId), registry, request, VisualTaskId.TextRecognition);
        OcrEnhancementRetryOptions retry = OcrEnhancementRetryOptions.CreateMany(recipes, confidenceThreshold: 1,
            selectionPolicy: OcrEnhancementSelectionPolicy.ConsensusConfidenceGain, minimumConfidenceGain: 0,
            maximumRegionsPerImage: 512, maximumCropsPerImage: 2048);
        TextCropProfile cropProfile = recognizer.CropProfile!.WithEnhancementRetry(retry);
        using var pipeline = new OcrPipeline(registry, detectorSelection, request, recognizerSelection, request, cropProfile,
            new OcrPipelineOptions(maximumRegions: 512, maximumRecognitionBatch: 16, maximumSourcePixels: 128L * 1024L * 1024L),
            new SessionOptions(1), new SessionOptions(1));

        var pageReports = new List<PageReport>(pages.Count);
        var regionReports = new List<RegionReport>(pages.Sum(page => page.Regions.Count));
        foreach (SroiePage page in pages)
        {
            using PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(page.ImagePath, "probe",
                new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr));
            using OpenCvOcrImageInput input = new OpenCvOcrImageInputFactory().CreateFromFile(page.ImagePath,
                detector.VisualProfile.Input.Name,
                OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(probe.SourceSize));

            Stopwatch timer = Stopwatch.StartNew();
            IReadOnlyList<OcrRegionResult> outputs = await pipeline.RecognizeOnlyAsync(input, page.Regions.Select(item => item.Region).ToArray()).ConfigureAwait(false);
            timer.Stop();
            Assert.AreEqual(page.Regions.Count, outputs.Count, "Recognizer-only run must preserve every labelled word region.");
            Dictionary<int, OcrRegionResult> outputBySourceIndex = outputs.ToDictionary(item => item.Region.SourceIndex);

            var pageRows = new List<RegionReport>(page.Regions.Count);
            for (int index = 0; index < page.Regions.Count; index++)
            {
                LabeledRegion source = page.Regions[index];
                OcrRegionResult output = outputBySourceIndex[source.Region.SourceIndex];
                OcrEnhancementRetryResult? trace = output.EnhancementRetry;
                RecognizedText original = trace?.Original.Recognition ?? output.Recognition;
                OcrEnhancementAttempt? primaryCandidate = trace?.Candidate;
                OcrTextAccuracyMetrics originalMetrics = Compare(source.Text, original.Text);
                OcrTextAccuracyMetrics finalMetrics = Compare(source.Text, output.Recognition.Text);
                OcrTextAccuracyMetrics? candidateMetrics = primaryCandidate == null ? null : Compare(source.Text, primaryCandidate.Recognition.Text);
                var candidates = new List<CandidateReport>();
                if (trace != null)
                {
                    for (int candidateIndex = 0; candidateIndex < trace.Candidates.Count; candidateIndex++)
                    {
                        OcrEnhancementAttempt candidate = trace.Candidates[candidateIndex];
                        int recipeIndex = trace.CandidateRecipeIndices[candidateIndex];
                        OcrTextAccuracyMetrics metrics = Compare(source.Text, candidate.Recognition.Text);
                        candidates.Add(new CandidateReport(recipes[recipeIndex].Mode.ToString(), candidate.Recognition.Confidence,
                            metrics.CharacterErrorRate, metrics.WordErrorRate, Sha256(candidate.Recognition.Text),
                            metrics.CharacterErrorRate < originalMetrics.CharacterErrorRate,
                            metrics.CharacterErrorRate > originalMetrics.CharacterErrorRate,
                            metrics.WordErrorRate < originalMetrics.WordErrorRate,
                            metrics.WordErrorRate > originalMetrics.WordErrorRate));
                    }
                }

                bool selected = trace?.Decision == OcrEnhancementRetryDecision.CandidateSelected;
                var report = new RegionReport(page.ImageId + "/" + source.InstanceId, page.ImageId, source.InstanceId,
                    original.Confidence, primaryCandidate?.Recognition.Confidence, output.Recognition.Confidence,
                    originalMetrics.CharacterErrorRate, originalMetrics.WordErrorRate,
                    candidateMetrics?.CharacterErrorRate, candidateMetrics?.WordErrorRate,
                    finalMetrics.CharacterErrorRate, finalMetrics.WordErrorRate,
                    selected, trace?.Decision.ToString() ?? "no-retry-trace", trace?.SelectedCandidateIndex,
                    Sha256(original.Text), Sha256(output.Recognition.Text), candidates,
                    originalMetrics.ReferenceCharacters, originalMetrics.CharacterEditDistance, originalMetrics.ReferenceWords,
                    originalMetrics.WordEditDistance, finalMetrics.CharacterEditDistance, finalMetrics.WordEditDistance);
                pageRows.Add(report);
                regionReports.Add(report);
            }

            pageReports.Add(new PageReport(page.ImageId, page.Sha256, pageRows.Count, timer.Elapsed.TotalMilliseconds,
                Aggregate(pageRows, final: false), Aggregate(pageRows, final: true),
                pageRows.Count(item => item.CandidateSelected),
                pageRows.Count(item => item.CandidateSelected && item.CandidateCaseFoldedCer > item.OriginalCaseFoldedCer),
                pageRows.Count(item => item.CandidateSelected && item.CandidateCaseFoldedWer > item.OriginalCaseFoldedWer)));
        }

        return new BackendReport(backend, runtime, pageReports, regionReports,
            regionReports.Count(item => item.CandidateSelected),
            regionReports.Count(item => item.CandidateSelected && item.CandidateCaseFoldedCer < item.OriginalCaseFoldedCer),
            regionReports.Count(item => item.CandidateSelected && item.CandidateCaseFoldedCer > item.OriginalCaseFoldedCer),
            regionReports.Count(item => item.CandidateSelected && item.CandidateCaseFoldedCer == item.OriginalCaseFoldedCer),
            regionReports.Count(item => item.CandidateSelected && item.CandidateCaseFoldedWer < item.OriginalCaseFoldedWer),
            regionReports.Count(item => item.CandidateSelected && item.CandidateCaseFoldedWer > item.OriginalCaseFoldedWer),
            regionReports.Count(item => item.CandidateSelected && item.CandidateCaseFoldedWer == item.OriginalCaseFoldedWer),
            Aggregate(regionReports, final: false), Aggregate(regionReports, final: true));
    }

    private static string GetOpenVinoRuntimeVersion()
    {
        string? nativeRuntimePath = Directory
            .EnumerateFiles(AppContext.BaseDirectory, "openvino_c.dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (nativeRuntimePath is null) return "unavailable";

        FileVersionInfo version = FileVersionInfo.GetVersionInfo(nativeRuntimePath);
        return !string.IsNullOrWhiteSpace(version.ProductVersion)
            ? version.ProductVersion.Trim()
            : !string.IsNullOrWhiteSpace(version.FileVersion)
                ? version.FileVersion.Trim()
                : "unknown";
    }

    private static IReadOnlyList<SroiePage> LoadPages(string datasetRoot, IEnumerable<string> manifestPaths)
    {
        var pages = new List<SroiePage>();
        foreach (string manifestPath in manifestPaths)
        {
            foreach (string line in File.ReadLines(RequireFile(manifestPath)))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("record_type", out JsonElement recordType) && recordType.GetString() != "image") continue;
                string imageId = root.GetProperty("image_id").GetString() ?? throw new InvalidDataException("SROIE manifest image_id is missing.");
                string relativePath = root.GetProperty("image_relpath").GetString() ?? throw new InvalidDataException("SROIE image_relpath is missing.");
                string imagePath = RequireFile(Path.Combine(datasetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                string imageSha256 = Sha256File(imagePath);
                if (root.TryGetProperty("source_provenance", out JsonElement provenance)
                    && provenance.TryGetProperty("image_sha256", out JsonElement expectedImageSha))
                {
                    Assert.AreEqual(expectedImageSha.GetString(), imageSha256, "SROIE image SHA-256 differs from the pinned manifest: " + imageId);
                }

                var regions = new List<LabeledRegion>();
                foreach (JsonElement instance in root.GetProperty("instances").EnumerateArray())
                {
                    if (instance.TryGetProperty("ignore", out JsonElement ignored) && ignored.GetBoolean()) continue;
                    string text = instance.GetProperty("text").GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    JsonElement polygon = instance.GetProperty("polygon");
                    if (polygon.GetArrayLength() != 4) throw new InvalidDataException("SROIE crop quality requires four-point word polygons.");
                    var points = new PointF[4];
                    for (int index = 0; index < points.Length; index++)
                    {
                        JsonElement point = polygon[index];
                        points[index] = new PointF(point[0].GetSingle(), point[1].GetSingle());
                    }
                    var quad = new TextQuadrilateral(points[0], points[1], points[2], points[3], TextCornerOrder.TopLeftClockwise);
                    string instanceId = instance.GetProperty("instance_id").GetString() ?? "word-" + regions.Count.ToString(CultureInfo.InvariantCulture);
                    var region = new TextRegion(regions.Count, 1f, quad.Polygon, quad, externalId: instanceId);
                    regions.Add(new LabeledRegion(instanceId, text, region));
                }
                if (regions.Count == 0) throw new InvalidDataException("SROIE page contains no usable labelled words: " + imageId);
                pages.Add(new SroiePage(imageId, imagePath, imageSha256, regions));
            }
        }
        return pages;
    }

    private static AggregateMetrics Aggregate(IReadOnlyList<RegionReport> rows, bool final)
    {
        int referenceCharacters = 0, characterEdits = 0, referenceWords = 0, wordEdits = 0;
        foreach (RegionReport row in rows)
        {
            characterEdits += final ? row.FinalCharacterEditDistance : row.OriginalCharacterEditDistance;
            referenceCharacters += row.ReferenceCharacters;
            wordEdits += final ? row.FinalWordEditDistance : row.OriginalWordEditDistance;
            referenceWords += row.ReferenceWords;
        }
        return new AggregateMetrics(rows.Count, referenceCharacters, characterEdits,
            referenceCharacters == 0 ? 0d : (double)characterEdits / referenceCharacters,
            referenceWords, wordEdits, referenceWords == 0 ? 0d : (double)wordEdits / referenceWords);
    }

    private static object[] SummarizeCandidates(IReadOnlyList<RegionReport> rows)
        => rows.SelectMany(row => row.Candidates)
            .GroupBy(candidate => candidate.Recipe, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (object)new
            {
                recipe = group.Key,
                attempts = group.Count(),
                betterCer = group.Count(item => item.CerBetterThanOriginal),
                worseCer = group.Count(item => item.CerWorseThanOriginal),
                equalCer = group.Count(item => !item.CerBetterThanOriginal && !item.CerWorseThanOriginal),
                betterWer = group.Count(item => item.WerBetterThanOriginal),
                worseWer = group.Count(item => item.WerWorseThanOriginal),
                equalWer = group.Count(item => !item.WerBetterThanOriginal && !item.WerWorseThanOriginal)
            }).ToArray();

    private static OcrTextAccuracyMetrics Compare(string reference, string hypothesis)
        => OcrTextAccuracy.Compare(reference.ToLowerInvariant(), hypothesis.ToLowerInvariant());

    private static double Percentile(IEnumerable<double> values, double fraction)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0) return 0d;
        if (ordered.Length == 1) return ordered[0];
        double position = (ordered.Length - 1) * fraction;
        int lower = (int)Math.Floor(position);
        int upper = Math.Min(lower + 1, ordered.Length - 1);
        return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower);
    }

    private static PaddleOcrArtifactContract Artifact(PaddleOcrReleaseArtifact release, string? dictionarySha = null)
        => new PaddleOcrArtifactContract(release.Opset, release.Sha256, "2661c7c0ef5c613e8f93c6e93b2e052399f0f854",
            "paddle2onnx-2.0.2rc3+paddlepaddle-3.0.0.dev20250613-byte-identical",
            "Apache-2.0;external-artifact-redistribution-unverified", "official-inference-v1", "deploysharp-ocr-ctc-v2",
            dictionarySha256: dictionarySha, dictionaryLicense: dictionarySha == null ? string.Empty : "official-repository-file-separate-review-required");

    private static string RequireFile(string path)
    {
        if (!File.Exists(path)) Assert.Inconclusive("Required SROIE/model input does not exist: " + path);
        return path;
    }

    private static string Sha256File(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record LabeledRegion(string InstanceId, string Text, TextRegion Region);
    private sealed record SroiePage(string ImageId, string ImagePath, string Sha256, IReadOnlyList<LabeledRegion> Regions);
    private sealed record AggregateMetrics(int Regions, int ReferenceCharacters, int CharacterEdits, double CharacterErrorRate, int ReferenceWords, int WordEdits, double WordErrorRate);
    private sealed record CandidateReport(string Recipe, float Confidence, double CaseFoldedCer, double CaseFoldedWer, string TextSha256,
        bool CerBetterThanOriginal, bool CerWorseThanOriginal, bool WerBetterThanOriginal, bool WerWorseThanOriginal);
    private sealed record RegionReport(string RegionId, string ImageId, string InstanceId,
        float OriginalConfidence, float? CandidateConfidence, float FinalConfidence,
        double OriginalCaseFoldedCer, double OriginalCaseFoldedWer, double? CandidateCaseFoldedCer, double? CandidateCaseFoldedWer,
        double FinalCaseFoldedCer, double FinalCaseFoldedWer, bool CandidateSelected, string Decision, int? SelectedCandidateIndex,
        string OriginalTextSha256, string FinalTextSha256, IReadOnlyList<CandidateReport> Candidates,
        int ReferenceCharacters, int OriginalCharacterEditDistance, int ReferenceWords, int OriginalWordEditDistance,
        int FinalCharacterEditDistance, int FinalWordEditDistance);
    private sealed record PageReport(string ImageId, string SourceImageSha256, int Regions, double ElapsedMs,
        AggregateMetrics Baseline, AggregateMetrics Final, int SelectedRegions, int SelectedWorseCerRegions, int SelectedWorseWerRegions);
    private sealed record BackendReport(string Backend, string Runtime, IReadOnlyList<PageReport> Pages, IReadOnlyList<RegionReport> Regions,
        int SelectedRegions, int SelectedBetterCerRegions, int SelectedWorseCerRegions, int SelectedEqualCerRegions,
        int SelectedBetterWerRegions, int SelectedWorseWerRegions, int SelectedEqualWerRegions,
        AggregateMetrics Baseline, AggregateMetrics Final);
}
