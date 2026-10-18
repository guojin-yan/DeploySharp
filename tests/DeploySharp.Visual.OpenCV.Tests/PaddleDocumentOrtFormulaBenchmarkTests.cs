using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>
/// Records formal ONNX Runtime CPU latency and output-contract stability for the six official
/// Paddle formula exports. This is execution/performance evidence, not a formula accuracy score.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentOrtFormulaBenchmarkTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string SourceRoot = @"E:\Model\PaddleDocument\source";
    private const string ImagePath = @"E:\Model\PaddleDocument\validation\general_formula_rec_001.png";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    [TestCategory("Performance")]
    public void SixFormulaModels_OnnxRuntimeCpu_RecordFormalLatencyAndStableOutputs()
    {
        RequireEnabled();
        RequireFile(ImagePath, "formula benchmark input");

        const int warmupCount = 5;
        const int measurementCount = 50;
        var rows = new List<object>(FormulaCases.Length);

        using var registry = new BackendRegistry().UseOnnxRuntime();
        BackendRequest request = new(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var factory = new OpenCvVisualInputFactory();

        foreach (FormulaCase item in FormulaCases)
        {
            string modelPath = Path.Combine(ModelRoot, item.ModelFile);
            string yamlPath = Path.Combine(SourceRoot, item.SourceDirectory, "inference.yml");
            RequireFile(modelPath, item.ModelId + " ONNX model");
            RequireFile(yamlPath, item.ModelId + " tokenizer");

            PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yamlPath);
            int end = FindToken(tokenizer.Tokens, "</s>");
            int start = FindToken(tokenizer.Tokens, "<s>");
            int pad = FindToken(tokenizer.Tokens, "<pad>");
            int unknown = FindToken(tokenizer.Tokens, "<unk>");
            Assert.IsTrue(end >= 0, item.ModelId + " tokenizer has no </s> token.");

            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/" + item.ModelId);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(
                descriptor,
                new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unknown),
                modelSize: item.Size,
                maximumSequenceLength: 4096);

            using PreparedVisualInput input = factory.CreateFromFile(
                ImagePath,
                profile.VisualProfile,
                inputId: "paddle-document-ort-formula-benchmark/" + item.ModelId);
            using IInferenceSession session = registry.CreateSession(
                profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId),
                request);

            FormulaOutputContract baseline = RunAndCapture(session, profile, input, end);
            for (int index = 1; index < warmupCount; index++)
                AssertContract(item.ModelId, baseline, RunAndCapture(session, profile, input, end), "warm-up");

            var samples = new double[measurementCount];
            for (int index = 0; index < measurementCount; index++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                FormulaOutputContract actual = RunAndCapture(session, profile, input, end);
                stopwatch.Stop();
                AssertContract(item.ModelId, baseline, actual, "measured run");
                samples[index] = stopwatch.Elapsed.TotalMilliseconds;
            }

            rows.Add(new
            {
                model = item.ModelId,
                modelFile = item.ModelFile,
                modelSha256 = Sha256(modelPath),
                tokenizerSha256 = Sha256(yamlPath),
                input = new { file = Path.GetFileName(ImagePath), sha256 = Sha256(ImagePath), inputShape = $"[1,3,{item.Size.Height},{item.Size.Width}]" },
                output = new
                {
                    tokenCount = baseline.TokenCount,
                    latexLength = baseline.LatexLength,
                    reachedEndOfSequence = baseline.ReachedEndOfSequence,
                    tokenIdsSha256 = baseline.TokenIdsSha256,
                    latexSha256 = baseline.LatexSha256,
                    warnings = baseline.Warnings
                },
                timing = Percentiles(samples),
                samplesMs = samples
            });
        }

        string reportPath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_ORT_BENCHMARK_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "paddle-document-ort-formula-benchmark-20261018.json");
        WriteJson(reportPath, new
        {
            schemaVersion = "deploysharp-paddle-document-ort-formula-benchmark-v1",
            generatedUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            device = Device(),
            backend = new { id = "onnxruntime-cpu", runtimePackage = "Microsoft.ML.OnnxRuntime 1.28.0" },
            protocol = new
            {
                warmupCount,
                measurementCount,
                batch = 1,
                sessions = 1,
                inputReuse = "One PreparedVisualInput and one Session reused per model.",
                timing = "Synchronous VisualPipeline-equivalent session.Run plus PaddleDocumentFormulaDecoder; excludes image decode, preprocessing, model/session creation and report serialization.",
                percentile = "Nearest-rank"
            },
            models = rows,
            boundary = "Six exact official formula ONNX artifacts on one Windows host. Every run preserved token/EOS/LaTeX fingerprints. This is execution and latency evidence, not formula accuracy, semantic equivalence, cross-backend parity or cross-device performance."
        });

        Assert.AreEqual(FormulaCases.Length, rows.Count);
    }

    private static FormulaOutputContract RunAndCapture(IInferenceSession session, PaddleDocumentProfile profile, PreparedVisualInput input, int endToken)
    {
        InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
        PaddleDocumentFormulaResult result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(
            new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
        Assert.IsTrue(result.TokenIds.Count > 0, "Formula decoder returned no tokens.");
        bool reachedEnd = !result.Warnings.Contains("missing-eos:sequence-may-be-truncated", StringComparer.Ordinal);
        Assert.IsTrue(reachedEnd, "Formula decoder reported a missing EOS token.");
        Assert.IsTrue(result.TokenIds.Contains(endToken) || reachedEnd, "Formula output has no EOS evidence.");
        return new FormulaOutputContract(
            result.TokenIds.Count,
            result.Latex.Length,
            reachedEnd,
            Sha256Text(string.Join(",", result.TokenIds)),
            Sha256Text(result.Latex),
            result.Warnings.ToArray());
    }

    private static void AssertContract(string modelId, FormulaOutputContract expected, FormulaOutputContract actual, string phase)
    {
        Assert.AreEqual(expected.TokenCount, actual.TokenCount, modelId + " changed token count during " + phase + ".");
        Assert.AreEqual(expected.LatexLength, actual.LatexLength, modelId + " changed LaTeX length during " + phase + ".");
        Assert.AreEqual(expected.ReachedEndOfSequence, actual.ReachedEndOfSequence, modelId + " changed EOS state during " + phase + ".");
        Assert.AreEqual(expected.TokenIdsSha256, actual.TokenIdsSha256, modelId + " changed token IDs during " + phase + ".");
        Assert.AreEqual(expected.LatexSha256, actual.LatexSha256, modelId + " changed decoded LaTeX during " + phase + ".");
    }

    private static object Percentiles(double[] samples)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        return new
        {
            p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .50) - 1],
            p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1],
            minMs = sorted[0],
            maxMs = sorted[^1],
            meanMs = sorted.Average()
        };
    }

    private static object Device() => new
    {
        machine = Environment.MachineName,
        os = RuntimeInformation.OSDescription,
        runtime = RuntimeInformation.FrameworkDescription,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        logicalProcessorCount = Environment.ProcessorCount,
        processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "not-recorded"
    };

    private void WriteJson(string path, object value)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(path);
        Console.WriteLine("PADDLE_DOCUMENT_ORT_FORMULA_BENCHMARK=" + path);
    }

    private static int FindToken(IReadOnlyList<string> tokens, string value)
    {
        for (int index = 0; index < tokens.Count; index++)
            if (tokens[index] == value) return index;
        return -1;
    }

    private static void RequireEnabled() => Assert.IsTrue(
        Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_ORT_BENCHMARK") == "1",
        "Set DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_ORT_BENCHMARK=1 to run the opt-in formula benchmark.");

    private static void RequireFile(string path, string label) => Assert.IsTrue(File.Exists(path), "Missing " + label + ": " + path);

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Sha256Text(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record FormulaOutputContract(int TokenCount, int LatexLength, bool ReachedEndOfSequence, string TokenIdsSha256, string LatexSha256, string[] Warnings);
    private sealed record FormulaCase(string ModelId, string ModelFile, string SourceDirectory, VisualSize Size);

    private static readonly FormulaCase[] FormulaCases =
    {
        new("pp-formulanet-plus-s", "pp-formulanet-plus-s.onnx", "pp-formulanet-plus-s/PP-FormulaNet_plus-S_infer", new VisualSize(384, 384)),
        new("pp-formulanet-plus-m", "pp-formulanet-plus-m.onnx", "pp-formulanet-plus-m/PP-FormulaNet_plus-M_infer", new VisualSize(384, 384)),
        new("pp-formulanet-plus-l", "pp-formulanet-plus-l.onnx", "pp-formulanet-plus-l/PP-FormulaNet_plus-L_infer", new VisualSize(768, 768)),
        new("pp-formulanet-s", "pp-formulanet-s.onnx", "pp-formulanet-s/PP-FormulaNet-S_infer", new VisualSize(384, 384)),
        new("pp-formulanet-l", "pp-formulanet-l.onnx", "pp-formulanet-l/PP-FormulaNet-L_infer", new VisualSize(768, 768)),
        new("unimernet", "unimernet.onnx", "unimernet/UniMERNet_infer", new VisualSize(672, 192))
    };
}
