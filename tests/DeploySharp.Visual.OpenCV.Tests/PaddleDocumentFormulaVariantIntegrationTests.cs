using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentFormulaVariantIntegrationTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string SourceRoot = @"E:\Model\PaddleDocument\source";
    private const string DefaultRoot = @"artifacts\formula-variants-20260929";
    private const string Expected = @"\zeta_{0}(\nu)=-\frac{\nu\varrho^{-2\nu}}{\pi}\int_{\mu}^{\infty}d\omega\int_{C_{+}}dz\frac{2z^{2}}{(z^{2}+\omega^{2})^{\nu+1}}\breve{\Psi}(\omega;z)e^{i\epsilon z}\quad,";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void FormulaNetPlusSRecordsMultiVariantQualityEvidence()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_VARIANTS") != "1") Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_FORMULA_VARIANTS=1 to run formula variants.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_VARIANT_ROOT") ?? DefaultRoot);
        string manifest = Path.Combine(root, "data", "annotations", "manifests", "formula-variants.jsonl");
        string model = Path.Combine(ModelRoot, "pp-formulanet-plus-s.onnx");
        string yaml = Path.Combine(SourceRoot, "pp-formulanet-plus-s", "PP-FormulaNet_plus-S_infer", "inference.yml");
        if (!File.Exists(manifest) || !File.Exists(model) || !File.Exists(yaml)) Assert.Inconclusive("Formula variant assets are incomplete.");
        PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yaml);
        int end = Find(tokenizer.Tokens, "</s>"), start = Find(tokenizer.Tokens, "<s>"), pad = Find(tokenizer.Tokens, "<pad>"), unk = Find(tokenizer.Tokens, "<unk>");
        PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s");
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(descriptor, new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unk), modelSize: new VisualSize(384, 384), maximumSequenceLength: 4096);
        using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(model, OnnxRuntimeBackendProvider.BackendId), request);
        var factory = new OpenCvVisualInputFactory();
        var rows = new List<object>();
        foreach (string line in File.ReadLines(manifest))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using JsonDocument item = JsonDocument.Parse(line);
            string image = Path.Combine(root, item.RootElement.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            using PreparedVisualInput input = factory.CreateFromFile(image, profile.VisualProfile, inputId: item.RootElement.GetProperty("variant").GetString());
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
            PaddleDocumentFormulaResult result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            string normalizedExpected = string.Concat(Expected.Where(value => !char.IsWhiteSpace(value)));
            string normalizedActual = string.Concat(result.Latex.Where(value => !char.IsWhiteSpace(value)));
            rows.Add(new { variant = item.RootElement.GetProperty("variant").GetString(), imageSha256 = item.RootElement.GetProperty("image_sha256").GetString(), tokenCount = result.TokenIds.Count, latexLength = result.Latex.Length, eos = result.TokenIds.Contains(end), exactNormalizedLatex = string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal), latex = result.Latex, warnings = result.Warnings.ToArray(), modelSha256 = FileSha(model) });
            Assert.IsTrue(result.TokenIds.Count > 0);
        }
        string report = Path.Combine(TestContext.TestResultsDirectory!, "formula-plus-s-variants.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { schemaVersion = 1, backend = "onnxruntime-cpu", model = "paddle-formula/pp-formulanet-plus-s", results = rows, boundary = "Controlled variants of one official formula image; not a formula dataset accuracy score." }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(5, rows.Count);
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SixFormulaModelsRecordMultiVariantQualityEvidence()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_VARIANTS") != "1") Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_FORMULA_VARIANTS=1 to run formula variants.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_VARIANT_ROOT") ?? DefaultRoot);
        string manifest = Path.Combine(root, "data", "annotations", "manifests", "formula-variants.jsonl");
        if (!File.Exists(manifest)) Assert.Inconclusive("Formula variant manifest is missing: " + manifest);
        var cases = new[]
        {
            (Model: "pp-formulanet-plus-s", Source: "PP-FormulaNet_plus-S_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-plus-m", Source: "PP-FormulaNet_plus-M_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-plus-l", Source: "PP-FormulaNet_plus-L_infer", Size: new VisualSize(768, 768)),
            (Model: "pp-formulanet-s", Source: "PP-FormulaNet-S_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-l", Source: "PP-FormulaNet-L_infer", Size: new VisualSize(768, 768)),
            (Model: "unimernet", Source: "UniMERNet_infer", Size: new VisualSize(672, 192))
        };
        string expectedNormalized = string.Concat(Expected.Where(value => !char.IsWhiteSpace(value)));
        var rows = new List<object>(cases.Length * 5);
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var factory = new OpenCvVisualInputFactory();
        foreach (var formulaCase in cases)
        {
            string modelPath = Path.Combine(ModelRoot, formulaCase.Model + ".onnx");
            string yaml = Path.Combine(SourceRoot, formulaCase.Model, formulaCase.Source, "inference.yml");
            if (!File.Exists(modelPath) || !File.Exists(yaml))
            {
                Assert.Fail("Formula variant assets are incomplete for " + formulaCase.Model + ".");
            }

            PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yaml);
            int end = Find(tokenizer.Tokens, "</s>");
            int start = Find(tokenizer.Tokens, "<s>");
            int pad = Find(tokenizer.Tokens, "<pad>");
            int unk = Find(tokenizer.Tokens, "<unk>");
            Assert.IsTrue(end >= 0, "The official tokenizer must expose </s>: " + yaml);
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/" + formulaCase.Model);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(
                descriptor,
                new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unk),
                modelSize: formulaCase.Size,
                maximumSequenceLength: 4096);
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), request);

            foreach (string line in File.ReadLines(manifest))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using JsonDocument item = JsonDocument.Parse(line);
                string variant = item.RootElement.GetProperty("variant").GetString()!;
                string image = Path.Combine(root, item.RootElement.GetProperty("image_relpath").GetString()!.Replace('/', Path.DirectorySeparatorChar));
                string manifestImageSha = item.RootElement.GetProperty("image_sha256").GetString()!;
                Assert.IsTrue(File.Exists(image), "Formula variant image is missing: " + image);
                string imageSha = FileSha(image);
                Assert.AreEqual(manifestImageSha, imageSha, "Formula variant image SHA drifted: " + variant);
                using PreparedVisualInput input = factory.CreateFromFile(image, profile.VisualProfile, inputId: variant);
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                PaddleDocumentFormulaResult result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
                string normalizedActual = string.Concat(result.Latex.Where(value => !char.IsWhiteSpace(value)));
                int normalizedEditDistance = EditDistance(expectedNormalized, normalizedActual);
                // The formula decoder removes EOS from the public token sequence;
                // the warning is the stable contract used by the six-model audit.
                bool reachedEos = !result.Warnings.Contains("missing-eos:sequence-may-be-truncated", StringComparer.Ordinal);
                rows.Add(new
                {
                    model = formulaCase.Model,
                    variant,
                    imageSha256 = imageSha,
                    modelSha256 = FileSha(modelPath),
                    tokenizerSha256 = FileSha(yaml),
                    tokenCount = result.TokenIds.Count,
                    tokenIdsSha256 = FileShaText(string.Join(",", result.TokenIds)),
                    latexLength = result.Latex.Length,
                    latexSha256 = FileShaText(result.Latex),
                    normalizedReferenceLength = expectedNormalized.Length,
                    normalizedCharEditDistance = normalizedEditDistance,
                    normalizedCharErrorRate = (double)normalizedEditDistance / Math.Max(1, expectedNormalized.Length),
                    reachedEndOfSequence = reachedEos,
                    explicitEosTokenRetained = result.TokenIds.Contains(end),
                    normalizedReferenceMatch = string.Equals(expectedNormalized, normalizedActual, StringComparison.Ordinal),
                    warnings = result.Warnings.ToArray(),
                    latex = result.Latex
                });
                Assert.IsTrue(result.TokenIds.Count > 0, formulaCase.Model + "/" + variant + " returned no formula tokens.");
                Assert.IsTrue(reachedEos, formulaCase.Model + "/" + variant + " reported a truncated sequence.");
            }
        }

        string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_VARIANT_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "formula-six-models-variants.json");
        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedAtUtc = DateTimeOffset.UtcNow,
            backend = "onnxruntime-cpu",
            sourceImage = "general_formula_rec_001.png",
            modelCount = cases.Length,
            variantCount = 5,
            results = rows,
            boundary = "Six formula models across five controlled variants of one official formula image; the normalized reference match is a controlled regression, not a formula dataset accuracy score or natural-image robustness claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(cases.Length * 5, rows.Count, "The six-model variant report must retain one row per model and variant.");
    }

    private static int Find(IReadOnlyList<string> values, string value) { for (int i=0;i<values.Count;i++) if (values[i] == value) return i; return -1; }
    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string FileShaText(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static int EditDistance(string expected, string actual)
    {
        int[] previous = new int[actual.Length + 1];
        int[] current = new int[actual.Length + 1];
        for (int column = 0; column <= actual.Length; column++) previous[column] = column;
        for (int row = 1; row <= expected.Length; row++)
        {
            current[0] = row;
            for (int column = 1; column <= actual.Length; column++)
            {
                int substitution = previous[column - 1] + (expected[row - 1] == actual[column - 1] ? 0 : 1);
                current[column] = Math.Min(Math.Min(previous[column] + 1, current[column - 1] + 1), substitution);
            }
            (previous, current) = (current, previous);
        }
        return previous[actual.Length];
    }
}
