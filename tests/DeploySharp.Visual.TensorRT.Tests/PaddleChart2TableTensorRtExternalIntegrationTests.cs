using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.TensorRT;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.TensorRT.Tests
{
    [TestClass]
    public sealed class PaddleChart2TableTensorRtExternalIntegrationTests
    {
        private const string VisionId = "paddle-chart/pp-chart2table/vision-projector";
        private const string EmbeddingId = "paddle-chart/pp-chart2table/token-embedding";
        private const string PrefillId = "paddle-chart/pp-chart2table/text-prefill";
        private const string DecodeId = "paddle-chart/pp-chart2table/text-decode-with-past";
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void LibraryBuilderSerializesDynamicChartKvDecodeGraph()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL=1 to build the local PP-Chart2Table dynamic-KV graph with TensorRtOnnxEngineBuilder.");

            string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT"), "text-onnx-verify-20260923");
            string outputRoot = Required("DEPLOYSHARP_CHART2TABLE_BUILDER_OUTPUT_ROOT");
            Directory.CreateDirectory(outputRoot);
            string onnxPath = GraphPath(textRoot, "chart-text-decoder-dynamic-past-full");
            string enginePath = Path.Combine(outputRoot, "text-decode-fp32.plan");
            if (File.Exists(enginePath)) Assert.Fail("Refusing to overwrite an existing builder regression artifact: " + enginePath);

            var options = new TensorRtOnnxEngineBuildOptions(
                apiVersion: TensorRtApiVersion.TensorRt10,
                precision: TensorRtOnnxEnginePrecision.Float32,
                maximumOnnxBytes: int.MaxValue,
                maximumEngineBytes: int.MaxValue,
                workspaceBytes: 1073741824UL,
                optimizationLevel: 0,
                overwrite: false,
                inputProfiles: DecodeProfiles(),
                disableTf32: true);
            var artifact = new ModelArtifact(new ModelId(DecodeId), "onnx", onnxPath);

            try
            {
                TensorRtOnnxEngineBuildResult result = new TensorRtOnnxEngineBuilder().Build(artifact, enginePath, options);
                TestContext.WriteLine(JsonSerializer.Serialize(new { result.OnnxBytes, result.EngineBytes, result.EngineSha256, result.BuildInputsSha256, result.OptimizationProfileCount }));
                Assert.IsTrue(result.EngineBytes >= 8);
                Assert.IsTrue(File.Exists(result.EnginePath));
            }
            catch (TensorRtBackendException exception)
            {
                string diagnostic = exception.ErrorCode + ";technicalDetails=" + exception.TechnicalDetails + ";inner=" + exception.InnerException;
                TestContext.WriteLine("CHART2TABLE_DYNAMIC_KV_BUILDER_FAILURE " + diagnostic);
                Assert.Fail(diagnostic);
            }
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void ChartQaHumanTestChartsGenerateCompleteTablesThroughTensorRt()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL=1 to run the local ChartQA samples through TensorRT.");

            string sampleRoot = Required("DEPLOYSHARP_CHARTQA_SAMPLE_ROOT");
            string modelRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
            string engineRoot = Required("DEPLOYSHARP_CHART2TABLE_TRT_ENGINE_ROOT");
            string[] engineFiles = { "vision-fp16.plan", "token-embedding-fp16.plan", "text-prefill-fp32.plan", "text-decode-fp32.plan" };
            string[] modelIds = { VisionId, EmbeddingId, PrefillId, DecodeId };
            var engineArtifacts = new List<ModelArtifact>(engineFiles.Length);
            for (int index = 0; index < engineFiles.Length; index++)
            {
                string path = Path.Combine(engineRoot, engineFiles[index]);
                if (!File.Exists(path)) Assert.Inconclusive("A library-built Chart2Table engine is missing: " + path);
                engineArtifacts.Add(new ModelArtifact(new ModelId(modelIds[index]), "tensorrt-engine", path, ComputeSha256(path), TensorRtBackendProvider.BackendId));
            }

            var samples = new[]
            {
                new { File = "png_41699051005347.png", Questions = new[] { "How many food item is shown in the bar graph? → 14", "What is the difference in value between Lamb and Corn? → 0.57" }, ExpectedText = "Country | Long-term price index in food commodities, 1850-2015, World, 1934\nLamb | 103.7\nCorn | 103.13\nBarley | 102.46\nRye | 87.37\nBeef | 85.27\nWheat | 83.73\nCoffee | 82.2\nTea | 68.48\nPeanuts | 64.71\nPalm oil | 57.6\nPork | 55.36\nRice | 42.48\nSugar | 25.56\nCocoa | 18.81" },
                new { File = "png_41810321001157.png", Questions = new[] { "How many bars are shown in the chart? → 3", "Is the sum value of Madagascar more then Fiji? → No" }, ExpectedText = "Characteristic | Value\nMauritania | 0.48%\nFiji | 0.38%\nMadagascar | 0.21%" },
                new { File = "png_8127.png", Questions = new[] { "What's the value of the lowest bar? → 23", "What is the difference between the highest and the lowest green bar? → 6" }, ExpectedText = "Entity | Limit its military role | Play a more active military role\n2015 | 68 | 23\n2016 | 62 | 29" },
                new { File = "png_166.png", Questions = new[] { "What percent think of President Donald Trump as Dangerous? → 62", "Is Charismatic plus Well-qualified more than A strong leader? → Yes" }, ExpectedText = "Entity | Values\nCaring about ordinary people | 23.0\nWell-qualified president to be | 26.0\nCharismatic | 39.0\na strong leader | 55.0\nDangerous | 62.0\nIntolerant | 65.0\nArrogant | 75.0" }
            };

            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10, cudaTargetArchitecture: "sm_86"));
            var tokenizer = new PaddleChart2TableTokenizer(modelRoot);
            var bundle = new PaddleChart2TableOnnxBundle(engineArtifacts[0], engineArtifacts[1], engineArtifacts[2], engineArtifacts[3]);
            using var session = new PaddleChart2TableTensorRtDeviceSession(provider, bundle, new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"));
            var inputFactory = new OpenCvPaddleChart2TableInputFactory();
            var evidence = new List<object>(samples.Length);

            foreach (var sample in samples)
            {
                string imagePath = Path.Combine(sampleRoot, sample.File);
                if (!File.Exists(imagePath)) Assert.Fail("A downloaded ChartQA image is missing: " + imagePath);
                using PreparedVisualInput input = inputFactory.CreateFromFile(imagePath);
                var watch = Stopwatch.StartNew();
                PaddleChart2TableGenerationResult result = session.Generate(input, tokenizer, maximumNewTokens: 1024);
                watch.Stop();
                PaddleChart2TableQualityComparison quality = PaddleChart2TableQualityEvaluator.Compare(sample.ExpectedText, result.Text);

                TestContext.WriteLine(JsonSerializer.Serialize(new
                {
                    dataset = "ChartQA test_human",
                    sample.File,
                    questionsAndAnswers = sample.Questions,
                    result.FinishReason,
                    tokenCount = result.TokenIds.Count,
                    totalMs = watch.Elapsed.TotalMilliseconds,
                    result.Text
                }));
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text), sample.File + " produced an empty table.");
                Assert.AreEqual(JYPPX.DeploySharp.Results.Language.GenerationFinishReason.EndOfSequence, result.FinishReason, sample.File + " did not complete EOS generation.");
                Assert.AreEqual(sample.ExpectedText, result.Text, sample.File + " did not reproduce the validated ChartQA table.");
                Assert.IsTrue(quality.IsExactStructureAndContent, sample.File + " structural table comparison failed.");
                evidence.Add(new
                {
                    sample = sample.File,
                    imageSha256 = ComputeSha256(imagePath),
                    expectedTextSha256 = Sha256Text(sample.ExpectedText),
                    generatedTextSha256 = Sha256Text(result.Text),
                    tokenCountIncludingEos = result.TokenIds.Count,
                    finishReason = result.FinishReason.ToString(),
                    totalMs = watch.Elapsed.TotalMilliseconds,
                    visionMs = result.VisionTime.TotalMilliseconds,
                    embeddingMs = result.EmbeddingTime.TotalMilliseconds,
                    prefillMs = result.PrefillTime.TotalMilliseconds,
                    decodeStepCount = result.DecodeSteps.Count,
                    decodeP50Ms = Percentile(result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray(), .50),
                    decodeP95Ms = Percentile(result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray(), .95),
                    structure = new
                    {
                        expectedRows = quality.Expected.RowCount,
                        actualRows = quality.Actual.RowCount,
                        expectedDataRows = quality.Expected.DataRowCount,
                        actualDataRows = quality.Actual.DataRowCount,
                        expectedColumns = quality.Expected.ColumnCount,
                        actualColumns = quality.Actual.ColumnCount,
                        expectedCells = quality.Expected.CellCount,
                        actualCells = quality.Actual.CellCount,
                        rowExactMatchCount = quality.RowExactMatchCount,
                        cellExactMatchCount = quality.CellExactMatchCount,
                        rowAccuracy = quality.RowAccuracy,
                        cellAccuracy = quality.CellAccuracy,
                        exactTextMatch = quality.ExactTextMatch,
                        structureMatches = quality.StructureMatches
                    }
                });
            }
            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "chart2table-tensorrt-multi-image-evidence.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                backend = "tensorrt-cuda",
                modelId = "paddle-chart/pp-chart2table",
                runtime = DescribeRuntime(),
                engineRoot,
                engineSha256 = engineArtifacts.Select(value => new { modelId = value.ModelId.Value, sha256 = value.Sha256 }).ToArray(),
                maximumNewTokens = 1024,
                results = evidence,
                boundary = "Four curated ChartQA human samples; exact table and structure reproduction is a multi-image qualitative regression, not a dataset accuracy score or controlled performance benchmark."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(samples.Length, evidence.Count, "The TensorRT evidence report must retain one row per sample.");
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialChartQaExtendedSelectionRecordsTensorRtQualityAndTiming()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
                Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_RUN_EXTERNAL=1 to run the bounded ChartQA TensorRT quality selection.");

            string sampleRoot = Required("DEPLOYSHARP_CHARTQA_EXTENDED_ROOT");
            string modelRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
            string engineRoot = Required("DEPLOYSHARP_CHART2TABLE_TRT_ENGINE_ROOT");
            string manifestPath = Path.Combine(sampleRoot, "manifest.json");
            if (!File.Exists(manifestPath)) Assert.Inconclusive("Missing ChartQA extended manifest: " + manifestPath);
            ExtendedManifest manifest = JsonSerializer.Deserialize<ExtendedManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Invalid ChartQA extended manifest.");
            if (manifest.Samples.Count == 0) Assert.Fail("ChartQA extended manifest contains no samples.");

            string[] engineFiles = { "vision-fp16.plan", "token-embedding-fp16.plan", "text-prefill-fp32.plan", "text-decode-fp32.plan" };
            string[] modelIds = { VisionId, EmbeddingId, PrefillId, DecodeId };
            var engineArtifacts = new List<ModelArtifact>(engineFiles.Length);
            for (int index = 0; index < engineFiles.Length; index++)
            {
                string path = Path.Combine(engineRoot, engineFiles[index]);
                if (!File.Exists(path)) Assert.Inconclusive("A library-built Chart2Table engine is missing: " + path);
                engineArtifacts.Add(new ModelArtifact(
                    new ModelId(modelIds[index]),
                    "tensorrt-engine",
                    path,
                    ComputeSha256(path),
                    TensorRtBackendProvider.BackendId));
            }

            int maximumNewTokens = ParseExtendedMaximumNewTokens();
            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10, cudaTargetArchitecture: "sm_86"));
            var tokenizer = new PaddleChart2TableTokenizer(modelRoot);
            var bundle = new PaddleChart2TableOnnxBundle(engineArtifacts[0], engineArtifacts[1], engineArtifacts[2], engineArtifacts[3]);
            using var session = new PaddleChart2TableTensorRtDeviceSession(
                provider,
                bundle,
                new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"));
            var inputFactory = new OpenCvPaddleChart2TableInputFactory();
            var evidence = new List<object>(manifest.Samples.Count);

            foreach (ExtendedSample sample in manifest.Samples)
            {
                string imagePath = Path.Combine(sampleRoot, "images", sample.File);
                if (!File.Exists(imagePath)) Assert.Fail("Missing ChartQA image: " + imagePath);
                string tableName = string.IsNullOrWhiteSpace(sample.TableFile)
                    ? Path.GetFileNameWithoutExtension(sample.File) + ".csv"
                    : sample.TableFile!;
                string tablePath = Path.Combine(sampleRoot, "tables", tableName);
                if (!File.Exists(tablePath)) Assert.Fail("Missing ChartQA table: " + tablePath);
                string imageSha = ComputeSha256(imagePath);
                if (!string.IsNullOrWhiteSpace(sample.ImageSha256) && !string.Equals(imageSha, sample.ImageSha256, StringComparison.OrdinalIgnoreCase))
                    Assert.Fail("ChartQA image SHA mismatch: " + sample.File);
                string tableSha = ComputeSha256(tablePath);
                if (!string.IsNullOrWhiteSpace(sample.TableSha256) && !string.Equals(tableSha, sample.TableSha256, StringComparison.OrdinalIgnoreCase))
                    Assert.Fail("ChartQA table SHA mismatch: " + tableName);

                using PreparedVisualInput input = inputFactory.CreateFromFile(imagePath);
                Stopwatch watch = Stopwatch.StartNew();
                PaddleChart2TableGenerationResult result = session.Generate(input, tokenizer, maximumNewTokens);
                watch.Stop();
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text), sample.File + " produced an empty table.");
                Assert.AreEqual(
                    JYPPX.DeploySharp.Results.Language.GenerationFinishReason.EndOfSequence,
                    result.FinishReason,
                    sample.File + " did not complete EOS generation.");
                PaddleChart2TableQualityComparison quality = PaddleChart2TableQualityEvaluator.Compare(sample.ExpectedText ?? string.Empty, result.Text);
                double[] decodeMs = result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray();
                evidence.Add(new
                {
                    sample = sample.File,
                    imageSha256 = imageSha,
                    sourceTableSha256 = tableSha,
                    expectedTextSha256 = Sha256Text(sample.ExpectedText ?? string.Empty),
                    generatedTextSha256 = Sha256Text(result.Text),
                    tokenCountIncludingEos = result.TokenIds.Count,
                    finishReason = result.FinishReason.ToString(),
                    totalMs = watch.Elapsed.TotalMilliseconds,
                    visionMs = result.VisionTime.TotalMilliseconds,
                    embeddingMs = result.EmbeddingTime.TotalMilliseconds,
                    prefillMs = result.PrefillTime.TotalMilliseconds,
                    decodeStepCount = decodeMs.Length,
                    decodeP50Ms = Percentile(decodeMs, .50),
                    decodeP95Ms = Percentile(decodeMs, .95),
                    structure = new
                    {
                        expectedRows = quality.Expected.RowCount,
                        actualRows = quality.Actual.RowCount,
                        expectedColumns = quality.Expected.ColumnCount,
                        actualColumns = quality.Actual.ColumnCount,
                        expectedCells = quality.Expected.CellCount,
                        actualCells = quality.Actual.CellCount,
                        rowExactMatchCount = quality.RowExactMatchCount,
                        cellExactMatchCount = quality.CellExactMatchCount,
                        rowAccuracy = quality.RowAccuracy,
                        cellAccuracy = quality.CellAccuracy,
                        exactTextMatch = quality.ExactTextMatch,
                        structureMatches = quality.StructureMatches,
                        actualStructurallyValid = quality.Actual.IsStructurallyValid
                    }
                });
                TestContext.WriteLine(JsonSerializer.Serialize(new
                {
                    sample = sample.File,
                    result.FinishReason,
                    tokenCount = result.TokenIds.Count,
                    totalMs = watch.Elapsed.TotalMilliseconds,
                    decodeP50Ms = Percentile(decodeMs, .50),
                    decodeP95Ms = Percentile(decodeMs, .95),
                    quality.StructureMatches,
                    quality.CellExactMatchCount,
                    quality.Expected.CellCount
                }));
            }

            string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_REPORT_PATH")
                ?? Path.Combine(TestContext.TestResultsDirectory!, "chart2table-tensorrt-extended-quality.json");
            string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
            if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                backend = "tensorrt-cuda",
                modelId = "paddle-chart/pp-chart2table",
                runtime = DescribeRuntime(),
                engineRoot,
                engineSha256 = engineArtifacts.Select(value => new { modelId = value.ModelId.Value, sha256 = value.Sha256 }).ToArray(),
                sourceRepository = manifest.SourceRepository,
                sourceRevision = manifest.SourceRevision,
                split = manifest.Split,
                maximumNewTokens,
                sampleCount = evidence.Count,
                results = evidence,
                boundary = "Bounded official ChartQA image/table selection; EOS, structure and timing evidence only, not split-level ChartQA accuracy or a controlled performance benchmark."
            }, new JsonSerializerOptions { WriteIndented = true }));
            TestContext.AddResultFile(report);
            Assert.AreEqual(manifest.Samples.Count, evidence.Count, "The TensorRT extended evidence report must retain one row per sample.");
        }

        [TestMethod]
        [TestCategory("ExternalModels")]
        public void OfficialChartImageRunsThroughFourTensorRtEnginesAndDynamicKvDecode()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal)) Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_TRT_RUN_EXTERNAL=1 to build and run the local four-graph PP-Chart2Table bundle on TensorRT.");
            string sourceRoot = Required("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
            string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
            string image = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_IMAGE") ?? @"E:\Model\PaddleDocument\validation\chart_parsing_02.png";
            string outputRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_ENGINE_ROOT") ?? @"E:\Model\PaddleDocument\chart2table-tensorrt-verify-20260923";
            Directory.CreateDirectory(outputRoot);
            string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
            var sources = new[]
            {
                (VisionId, Path.Combine(exportRoot, "chart-vision.onnx"), "vision-fp16.plan", Array.Empty<TensorRtOnnxInputProfile>()),
                (EmbeddingId, GraphPath(textRoot, "chart-token-embedding-dynamic"), "token-embedding-fp16.plan", EmbeddingProfiles()),
                (PrefillId, GraphPath(textRoot, "chart-text-prefill-full"), "text-prefill-fp32.plan", Array.Empty<TensorRtOnnxInputProfile>()),
                (DecodeId, GraphPath(textRoot, "chart-text-decoder-dynamic-past-full"), "text-decode-fp32.plan", DecodeProfiles())
            };
            var engineArtifacts = new List<ModelArtifact>(sources.Length);
            bool reusePlans = string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_USE_EXISTING_PLANS"), "1", StringComparison.Ordinal);
            if (reusePlans)
            {
                foreach (var source in sources)
                {
                    string? configuredPath = source.Item1 == PrefillId
                        ? Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_PREFILL_PLAN")
                        : source.Item1 == DecodeId
                            ? Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_DECODE_PLAN")
                            : null;
                    string enginePath = string.IsNullOrWhiteSpace(configuredPath) ? Path.Combine(outputRoot, source.Item3) : Path.GetFullPath(configuredPath);
                    if (!File.Exists(enginePath)) Assert.Inconclusive("A prebuilt TensorRT plan is missing: " + enginePath);
                    engineArtifacts.Add(new ModelArtifact(new ModelId(source.Item1), "tensorrt-engine", enginePath, ComputeSha256(enginePath), TensorRtBackendProvider.BackendId));
                }
            }
            else
            {
                var builder = new TensorRtOnnxEngineBuilder();
                foreach (var source in sources)
                {
                    var onnxArtifact = new ModelArtifact(new ModelId(source.Item1), "onnx", source.Item2);
                    string enginePath = Path.Combine(outputRoot, source.Item3);
                    TensorRtOnnxEnginePrecision precision = source.Item1 == PrefillId || source.Item1 == DecodeId
                        ? TensorRtOnnxEnginePrecision.Float32
                        : TensorRtOnnxEnginePrecision.Float16;
                    var options = new TensorRtOnnxEngineBuildOptions(
                        apiVersion: TensorRtApiVersion.TensorRt10,
                        precision: precision,
                        maximumOnnxBytes: int.MaxValue,
                        maximumEngineBytes: int.MaxValue,
                        workspaceBytes: 1073741824UL,
                        optimizationLevel: 0,
                        overwrite: true,
                        inputProfiles: source.Item4,
                        disableTf32: precision == TensorRtOnnxEnginePrecision.Float32);
                    try
                    {
                        TensorRtOnnxEngineBuildResult build = builder.Build(onnxArtifact, enginePath, options);
                        TestContext.WriteLine(JsonSerializer.Serialize(new { role = source.Item3, build.OnnxBytes, build.EngineBytes, build.EngineSha256, buildInputsSha256 = build.BuildInputsSha256, apiVersion = build.ApiVersion.ToString() }));
                        engineArtifacts.Add(new ModelArtifact(new ModelId(source.Item1), "tensorrt-engine", build.EnginePath, build.EngineSha256, TensorRtBackendProvider.BackendId));
                    }
                    catch (TensorRtBackendException exception) when (exception.ErrorCode == TensorRtErrorCodes.NativeRuntimeUnavailable)
                    {
                        string details = "TensorRT engine build blocked at " + source.Item3 + ": " + exception.ToString() + " " + DescribeRuntime();
                        Console.WriteLine("CHART2TABLE_TENSORRT_BLOCKED role=" + source.Item3 + ";details=" + details);
                        TestContext.WriteLine(details);
                        string diagnosticPath = Path.Combine(outputRoot, "blocked-" + Path.GetFileNameWithoutExtension(source.Item3) + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json");
                        File.WriteAllText(diagnosticPath, JsonSerializer.Serialize(new { role = source.Item3, errorCode = exception.ErrorCode, exception = exception.ToString(), details }, new JsonSerializerOptions { WriteIndented = true }));
                        Assert.Inconclusive(details);
                    }
                    catch (TensorRtBackendException exception)
                    {
                        TestContext.WriteLine("CHART2TABLE_TENSORRT_BUILD_ERROR code=" + exception.ErrorCode + ";details=" + exception.TechnicalDetails);
                        throw;
                    }
                }
            }

            var bundle = new PaddleChart2TableOnnxBundle(engineArtifacts[0], engineArtifacts[1], engineArtifacts[2], engineArtifacts[3]);
            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10, cudaTargetArchitecture: "sm_86"));
            var tokenizer = new PaddleChart2TableTokenizer(sourceRoot);
            using PreparedVisualInput input = new OpenCvPaddleChart2TableInputFactory().CreateFromFile(image);
            using var session = new PaddleChart2TableTensorRtDeviceSession(provider, bundle, new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"));
            int maximumNewTokens = ParseMaximumNewTokens();
            var totalWatch = Stopwatch.StartNew();
            PaddleChart2TableGenerationResult result = session.Generate(input, tokenizer, maximumNewTokens);
            totalWatch.Stop();
            TestContext.WriteLine(JsonSerializer.Serialize(new { result.Text, tokenIds = result.TokenIds, finish = result.FinishReason.ToString(), requestedNewTokens = maximumNewTokens, totalMs = totalWatch.Elapsed.TotalMilliseconds, visionMs = result.VisionTime.TotalMilliseconds, embeddingMs = result.EmbeddingTime.TotalMilliseconds, prefillMs = result.PrefillTime.TotalMilliseconds, decodeMs = result.DecodeSteps.Select(value => value.TotalMilliseconds).ToArray() }));
            CollectionAssert.AreEqual(new[] { 7948, 69442, 760 }, result.TokenIds.Take(3).ToArray());
            Assert.AreEqual(tokenizer.IsTerminalToken(result.TokenIds.Last()) ? JYPPX.DeploySharp.Results.Language.GenerationFinishReason.EndOfSequence : JYPPX.DeploySharp.Results.Language.GenerationFinishReason.MaxTokens, result.FinishReason);
            if (result.FinishReason == JYPPX.DeploySharp.Results.Language.GenerationFinishReason.EndOfSequence)
            {
                const string expectedTable = "年份 | 单家五星级旅游饭店年平均营收 (百万元) | 单家五星级旅游饭店年平均利润 (百万元)\n2018 | 104.22 | 9.87\n2019 | 99.11 | 7.47\n2020 | 57.87 | -3.87\n2021 | 68.99 | -2.90\n2022 | 56.29 | -9.48\n2023 | 87.99 | 5.96";
                Assert.AreEqual(expectedTable, result.Text, "A complete backend run must reproduce the verified official sample table exactly.");
            }
        }

        private static int ParseMaximumNewTokens()
        {
            string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_MAX_NEW_TOKENS");
            if (string.IsNullOrWhiteSpace(value)) return 3;
            if (!int.TryParse(value, out int parsed) || parsed < 3 || parsed > 2048) Assert.Fail("DEPLOYSHARP_CHART2TABLE_TRT_MAX_NEW_TOKENS must be an integer between 3 and 2048.");
            return parsed;
        }

        private static int ParseExtendedMaximumNewTokens()
        {
            string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_MAX_NEW_TOKENS");
            if (string.IsNullOrWhiteSpace(value)) return 1024;
            if (!int.TryParse(value, out int parsed) || parsed < 3 || parsed > 4096)
                Assert.Fail("DEPLOYSHARP_CHART2TABLE_TRT_EXTENDED_MAX_NEW_TOKENS must be an integer between 3 and 4096.");
            return parsed;
        }

        private static TensorRtOnnxInputProfile[] EmbeddingProfiles() => new[]
        {
            new TensorRtOnnxInputProfile("input_ids", new TensorShape(1, 1), new TensorShape(1, 286), new TensorShape(1, 2333))
        };

        private static TensorRtOnnxInputProfile[] DecodeProfiles()
        {
            var values = new List<TensorRtOnnxInputProfile>
            {
                // attention_mask is one token longer than the past KV. The
                // optimum KV length is 512, so the matching mask length is 513.
                new TensorRtOnnxInputProfile("attention_mask", new TensorShape(1, 287), new TensorShape(1, 513), new TensorShape(1, 2333))
            };
            for (int layer = 0; layer < 24; layer++)
            {
                values.Add(new TensorRtOnnxInputProfile("past_key_" + layer, new TensorShape(1, 286, 16, 64), new TensorShape(1, 512, 16, 64), new TensorShape(1, 2332, 16, 64)));
                values.Add(new TensorRtOnnxInputProfile("past_value_" + layer, new TensorShape(1, 286, 16, 64), new TensorShape(1, 512, 16, 64), new TensorShape(1, 2332, 16, 64)));
            }
            return values.ToArray();
        }

        private static string Required(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Required external integration variable is missing: " + name);
            return value!;
        }

        private static string DescribeRuntime() => string.Join(";", new[] { "JYPPX_NATIVE_BRIDGE_PATH", "JYPPX_TENSORRT_ROOT", "JYPPX_CUDA_ROOT", "JYPPX_CUDNN_ROOT" }.Select(name => name + "=" + (Environment.GetEnvironmentVariable(name) ?? "<unset>")));

        private static string GraphPath(string root, string stem)
        {
            string epsilon = Path.Combine(root, stem + "-epsilon.onnx");
            if (File.Exists(epsilon)) return epsilon;
            string standard = Path.Combine(root, stem + ".onnx");
            if (File.Exists(standard)) return standard;
            return standard;
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        private static string Sha256Text(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        private static double Percentile(IReadOnlyList<double> values, double percentile)
        {
            if (values.Count == 0) return 0;
            double[] sorted = values.OrderBy(value => value).ToArray();
            double position = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        private sealed class ExtendedManifest
        {
            public string? Dataset { get; set; }
            public string? Split { get; set; }
            public string? SourceRepository { get; set; }
            public string? SourceRevision { get; set; }
            public List<ExtendedSample> Samples { get; set; } = new();
        }

        private sealed class ExtendedSample
        {
            public string File { get; set; } = string.Empty;
            public string? TableFile { get; set; }
            public string? ExpectedText { get; set; }
            public string? ImageSha256 { get; set; }
            public string? TableSha256 { get; set; }
        }
    }
}
