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

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SixFormulaModelsEvaluateRealFormulaDatasetOnOrtCpu()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_REAL_FORMULA=1 to run the realFormula quality evaluation.");

        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT")
            ?? @"F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public");
        string manifest = Path.Combine(root, "realFormula-manifest.jsonl");
        string csv = Path.Combine(root, "test-corrected_normalized.csv");
        if (!File.Exists(manifest) || !File.Exists(csv))
            Assert.Inconclusive("realFormula manifest/CSV is missing; run Prepare-RealFormulaDataset.ps1 first: " + root);

        List<RealFormulaRow> samples = File.ReadLines(manifest)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<RealFormulaRow>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .ToList();
        Assert.AreEqual(121, samples.Count, "The pinned realFormula v1 corpus should contain 121 annotated formula images.");
        string csvSha256 = FileSha(csv);
        string manifestSha256 = FileSha(manifest);
        string archivePath = Path.GetFullPath(Path.Combine(root, "..", "..", "realFormula.zip"));
        string? archiveSha256 = File.Exists(archivePath) ? FileSha(archivePath) : null;
        int sampleLimit = 0;
        _ = int.TryParse(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_LIMIT"), out sampleLimit);
        if (sampleLimit > 0) samples = samples.Take(sampleLimit).ToList();

        var cases = new[]
        {
            (Model: "pp-formulanet-plus-s", Source: "PP-FormulaNet_plus-S_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-plus-m", Source: "PP-FormulaNet_plus-M_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-plus-l", Source: "PP-FormulaNet_plus-L_infer", Size: new VisualSize(768, 768)),
            (Model: "pp-formulanet-s", Source: "PP-FormulaNet-S_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-l", Source: "PP-FormulaNet-L_infer", Size: new VisualSize(768, 768)),
            (Model: "unimernet", Source: "UniMERNet_infer", Size: new VisualSize(672, 192))
        };
        string modelFilter = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_MODELS") ?? string.Empty;
        HashSet<string> selectedModels = modelFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedCases = cases.Where(item => selectedModels.Count == 0 || selectedModels.Contains(item.Model)).ToArray();
        Assert.IsTrue(selectedCases.Length > 0, "The model filter did not select a known formula model.");

        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var factory = new OpenCvVisualInputFactory();
        var modelReports = new List<object>();
        foreach (var formulaCase in selectedCases)
        {
            string modelPath = Path.Combine(ModelRoot, formulaCase.Model + ".onnx");
            string yaml = Path.Combine(SourceRoot, formulaCase.Model, formulaCase.Source, "inference.yml");
            Assert.IsTrue(File.Exists(modelPath) && File.Exists(yaml), "Formula model assets are incomplete: " + formulaCase.Model);
            PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yaml);
            int end = Find(tokenizer.Tokens, "</s>"), start = Find(tokenizer.Tokens, "<s>"), pad = Find(tokenizer.Tokens, "<pad>"), unk = Find(tokenizer.Tokens, "<unk>");
            Assert.IsTrue(end >= 0, "Official formula tokenizer is missing EOS: " + yaml);
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/" + formulaCase.Model);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(descriptor,
                new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unk), modelSize: formulaCase.Size, maximumSequenceLength: 4096);
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), request);

            var resultRows = new List<object>(samples.Count);
            long totalEditDistance = 0;
            long totalReferenceCharacters = 0;
            double totalSampleCer = 0;
            long totalTokenEditDistance = 0;
            long totalReferenceTokens = 0;
            int exactMatches = 0, reachedEosCount = 0, emptyCount = 0;
            int tokenExactMatches = 0;
            foreach (RealFormulaRow sample in samples)
            {
                string imagePath = Path.Combine(root, sample.ImageRelPath.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(imagePath), "realFormula image is missing: " + imagePath);
                Assert.AreEqual(sample.ImageSha256, FileSha(imagePath), "realFormula image SHA changed: " + sample.Image);
                using PreparedVisualInput input = factory.CreateFromFile(imagePath, profile.VisualProfile, inputId: sample.Image);
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                PaddleDocumentFormulaResult result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(
                    new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));

                // The realFormula labels are canonical, whitespace-tokenized LaTeX. Removing all whitespace
                // compares the same canonical sequence as the model decoder without claiming TeX semantics.
                string normalizedReference = string.Concat(sample.ReferenceLatex.Where(character => !char.IsWhiteSpace(character)));
                string normalizedPrediction = string.Concat(result.Latex.Where(character => !char.IsWhiteSpace(character)));
                int editDistance = EditDistance(normalizedReference, normalizedPrediction);
                string[] referenceTokens = SplitTokens(sample.ReferenceLatex);
                string[] predictionTokens = SplitTokens(result.Latex);
                int tokenEditDistance = EditDistance(referenceTokens, predictionTokens);
                bool reachedEos = !result.Warnings.Contains("missing-eos:sequence-may-be-truncated", StringComparer.Ordinal);
                totalEditDistance += editDistance;
                totalReferenceCharacters += normalizedReference.Length;
                totalSampleCer += (double)editDistance / Math.Max(1, normalizedReference.Length);
                totalTokenEditDistance += tokenEditDistance;
                totalReferenceTokens += referenceTokens.Length;
                if (string.Equals(normalizedReference, normalizedPrediction, StringComparison.Ordinal)) exactMatches++;
                if (referenceTokens.SequenceEqual(predictionTokens, StringComparer.Ordinal)) tokenExactMatches++;
                if (reachedEos) reachedEosCount++;
                if (normalizedPrediction.Length == 0) emptyCount++;
                resultRows.Add(new
                {
                    image = sample.Image,
                    imageSha256 = sample.ImageSha256,
                    referenceSha256 = FileShaText(sample.ReferenceLatex),
                    referenceLength = normalizedReference.Length,
                    predictionLength = normalizedPrediction.Length,
                    referenceTokenCount = referenceTokens.Length,
                    predictionTokenCount = predictionTokens.Length,
                    normalizedCharEditDistance = editDistance,
                    normalizedCharErrorRate = (double)editDistance / Math.Max(1, normalizedReference.Length),
                    normalizedExactMatch = string.Equals(normalizedReference, normalizedPrediction, StringComparison.Ordinal),
                    whitespaceTokenEditDistance = tokenEditDistance,
                    whitespaceTokenErrorRate = (double)tokenEditDistance / Math.Max(1, referenceTokens.Length),
                    whitespaceTokenExactMatch = referenceTokens.SequenceEqual(predictionTokens, StringComparer.Ordinal),
                    reachedEndOfSequence = reachedEos,
                    emptyPrediction = normalizedPrediction.Length == 0,
                    warnings = result.Warnings.ToArray(),
                    prediction = result.Latex
                });
            }
            modelReports.Add(new
            {
                model = "paddle-formula/" + formulaCase.Model,
                modelSha256 = FileSha(modelPath),
                tokenizerYamlSha256 = FileSha(yaml),
                backend = "onnxruntime-cpu",
                sampleCount = samples.Count,
                exactMatches,
                exactMatchRate = (double)exactMatches / Math.Max(1, samples.Count),
                whitespaceTokenExactMatches = tokenExactMatches,
                whitespaceTokenExactMatchRate = (double)tokenExactMatches / Math.Max(1, samples.Count),
                corpusNormalizedCharErrorRate = (double)totalEditDistance / Math.Max(1, totalReferenceCharacters),
                meanNormalizedCharErrorRate = samples.Count == 0 ? 0 : totalSampleCer / samples.Count,
                corpusWhitespaceTokenErrorRate = (double)totalTokenEditDistance / Math.Max(1, totalReferenceTokens),
                reachedEndOfSequenceCount = reachedEosCount,
                emptyPredictionCount = emptyCount,
                results = resultRows
            });
        }

        string report = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "realFormula-six-models-ort.json");
        string? reportDirectory = Path.GetDirectoryName(Path.GetFullPath(report));
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = "deploysharp-realformula-six-model-quality-v1",
            generatedAtUtc = DateTimeOffset.UtcNow,
            dataset = new
            {
                name = "MathNet realFormula",
                version = "Zenodo 11296815 v1",
                doi = "10.5281/zenodo.11296815",
                recordUrl = "https://zenodo.org/records/11296815",
                license = "CC-BY-4.0",
                attribution = "Schmitt-Koopmann, Felix; Huang, Elaine; Hutter, Hans-Peter; Stadelmann, Thilo; Darvishy, Alireza. MER dataset realFormula. Zenodo, v1, 2024. https://doi.org/10.5281/zenodo.11296815",
                archiveSha256,
                csvSha256,
                manifestSha256,
                annotatedSampleCount = 121,
                evaluatedSampleCount = samples.Count,
                evaluationLimit = sampleLimit,
                annotation = "121 manually annotated mathematical expressions from arXiv papers; image-to-label mapping is validated by exact image name and SHA-256."
            },
            normalization = "Remove Unicode whitespace from the dataset's canonical tokenized LaTeX and decoded LaTeX; character Levenshtein CER is diagnostic and does not measure mathematical/TeX semantic equivalence.",
            modelCount = selectedCases.Length,
            modelResults = modelReports,
            boundary = "This is an external realFormula v1 corpus evaluation on local ONNX Runtime CPU assets. Training-corpus overlap for the tested Paddle checkpoints is unknown; this report is not a universal accuracy claim, and it does not establish OpenVINO/OpenCV/TensorRT accuracy or performance."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.AreEqual(selectedCases.Length, modelReports.Count);
    }

    private sealed record RealFormulaRow(string Image, string ImageRelPath, string ImageSha256, string ReferenceLatex);

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

    private static string[] SplitTokens(string value) => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static int EditDistance(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        int[] previous = new int[actual.Count + 1];
        int[] current = new int[actual.Count + 1];
        for (int column = 0; column <= actual.Count; column++) previous[column] = column;
        for (int row = 1; row <= expected.Count; row++)
        {
            current[0] = row;
            for (int column = 1; column <= actual.Count; column++)
            {
                int substitution = previous[column - 1] + (string.Equals(expected[row - 1], actual[column - 1], StringComparison.Ordinal) ? 0 : 1);
                current[column] = Math.Min(Math.Min(previous[column] + 1, current[column - 1] + 1), substitution);
            }
            (previous, current) = (current, previous);
        }
        return previous[actual.Count];
    }
}
