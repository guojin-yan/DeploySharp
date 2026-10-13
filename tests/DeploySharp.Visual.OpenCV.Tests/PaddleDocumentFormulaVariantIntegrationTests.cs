using System;
using System.Collections.Generic;
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
        string imageFilter = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_IMAGES") ?? string.Empty;
        HashSet<string> selectedImages = imageFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedImages.Count > 0)
        {
            string[] missingImages = selectedImages.Where(image => samples.All(sample => !string.Equals(sample.Image, image, StringComparison.OrdinalIgnoreCase))).ToArray();
            Assert.AreEqual(0, missingImages.Length, "The realFormula image filter contains unknown samples: " + string.Join(", ", missingImages));
            samples = samples.Where(sample => selectedImages.Contains(sample.Image)).ToList();
        }
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
        string? inputTensorOverridePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_INPUT_TENSOR");
        if (!string.IsNullOrWhiteSpace(inputTensorOverridePath))
        {
            Assert.AreEqual(1, samples.Count, "An input tensor override is only valid for a single selected formula sample.");
            Assert.AreEqual(1, selectedCases.Length, "An input tensor override is only valid for a single selected formula model.");
        }
        bool captureTokenTrace = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_TRACE_TOKENS") == "1";
        string? inputTensorDumpDirectory = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_REAL_FORMULA_INPUT_DUMP_DIR");
        if (!string.IsNullOrWhiteSpace(inputTensorDumpDirectory)) Directory.CreateDirectory(inputTensorDumpDirectory);

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

            string sourceModelDirectory = Path.GetDirectoryName(yaml)!;
            string sourceProgramPath = Path.Combine(sourceModelDirectory, "inference.json");
            string sourceWeightsPath = Path.Combine(sourceModelDirectory, "inference.pdiparams");
            var maxSequenceMatch = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(yaml), @"(?m)^\s*max_seq_len:\s*(\d+)");
            int? yamlMaximumSequenceLength = maxSequenceMatch.Success ? int.Parse(maxSequenceMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;

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
                string? inputTensorOverrideSha256 = null;
                if (!string.IsNullOrWhiteSpace(inputTensorOverridePath))
                    inputTensorOverrideSha256 = OverrideFloat32Tensor(input.Tensor, inputTensorOverridePath);
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                PaddleDocumentFormulaDecoder decoder = (PaddleDocumentFormulaDecoder)profile.VisualProfile.Decoder;
                ITensor rawOutput = outputs.GetRequired(decoder.OutputName);
                long[] rawTokenIds = ReadIntegerSequence(rawOutput);
                string? inputTensorDumpFile = null;
                if (!string.IsNullOrWhiteSpace(inputTensorDumpDirectory))
                {
                    inputTensorDumpFile = Path.GetFileNameWithoutExtension(sample.Image) + ".deploysharp.f32";
                    File.WriteAllBytes(Path.Combine(inputTensorDumpDirectory, inputTensorDumpFile), ReadFloat32Bytes(input.Tensor));
                }
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
                    inputTensorSource = inputTensorOverrideSha256 == null ? "deploysharp-preprocessing" : "external-float32-override",
                    inputShape = input.Tensor.Shape.ToArray(),
                    inputTensorSha256 = TensorSha256(input.Tensor),
                    inputTensorOverrideSha256,
                    inputTensorDumpFile,
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
                    rawOutputShape = rawOutput.Shape.ToArray(),
                    rawEosIndex = Array.IndexOf(rawTokenIds, end),
                    rawOutputTokenIds = captureTokenTrace ? rawTokenIds : null,
                    decodedTokenIds = captureTokenTrace ? result.TokenIds.ToArray() : null,
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
                sourceInferenceProgramSha256 = File.Exists(sourceProgramPath) ? FileSha(sourceProgramPath) : null,
                sourceInferenceWeightsSha256 = File.Exists(sourceWeightsPath) ? FileSha(sourceWeightsPath) : null,
                startTokenId = start,
                endTokenId = end,
                padTokenId = pad,
                yamlMaximumSequenceLength,
                decoderSafetyLimitSequenceLength = ((PaddleDocumentFormulaDecoder)profile.VisualProfile.Decoder).MaximumSequenceLength,
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

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SixFormulaModelsRunOnOrtCudaAndMatchCpuBounded()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA=1 and restore with DeploySharpFormulaOrtCuda=true to run the CUDA evidence probe.");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("The current opt-in formula CUDA evidence package is Windows-only.");

        string modelRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_MODEL_ROOT") ?? ModelRoot);
        string sourceRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_SOURCE_ROOT") ?? SourceRoot);
        string datasetRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ROOT")
            ?? @"F:\OCRBenchmarkTesting\datasets\realFormula-zenodo-11296815\extracted\realFormula-public");
        string manifest = Path.Combine(datasetRoot, "realFormula-manifest.jsonl");
        if (!File.Exists(manifest)) Assert.Inconclusive("realFormula manifest is missing: " + manifest);

        var samples = File.ReadLines(manifest)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<RealFormulaRow>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .ToList();
        int limit = 5;
        if (int.TryParse(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIMIT"), out int configuredLimit) && configuredLimit > 0)
            limit = configuredLimit;
        int offset = 0;
        if (int.TryParse(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_OFFSET"), out int configuredOffset) && configuredOffset > 0)
            offset = configuredOffset;
        samples = samples.Skip(offset).Take(limit).ToList();
        Assert.IsTrue(samples.Count > 0, "The CUDA probe requires at least one realFormula sample.");

        var cases = new[]
        {
            (Model: "pp-formulanet-plus-s", Source: "PP-FormulaNet_plus-S_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-plus-m", Source: "PP-FormulaNet_plus-M_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-plus-l", Source: "PP-FormulaNet_plus-L_infer", Size: new VisualSize(768, 768)),
            (Model: "pp-formulanet-s", Source: "PP-FormulaNet-S_infer", Size: new VisualSize(384, 384)),
            (Model: "pp-formulanet-l", Source: "PP-FormulaNet-L_infer", Size: new VisualSize(768, 768)),
            (Model: "unimernet", Source: "UniMERNet_infer", Size: new VisualSize(672, 192))
        };
        string modelFilter = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_MODELS") ?? string.Empty;
        HashSet<string> selectedModels = modelFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedModels.Count > 0)
            cases = cases.Where(item => selectedModels.Contains(item.Model)).ToArray();
        Assert.IsTrue(cases.Length > 0, "The CUDA probe model filter did not select a known formula model.");

        IReadOnlyDictionary<string, string> cudaProviderOptions = ParseOptionEntries(
            Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_OPTIONS"),
            "DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_OPTIONS");
        IReadOnlyDictionary<string, string> sessionConfigEntries = ParseOptionEntries(
            Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_SESSION_CONFIG"),
            "DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_SESSION_CONFIG");
        bool preferPreallocatedCpuOutputs = string.Equals(
            Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_PREALLOCATE_CPU_OUTPUTS"),
            "1",
            StringComparison.Ordinal);

        string report = Path.GetFullPath(Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_REPORT_PATH")
            ?? Path.Combine(TestContext.TestResultsDirectory!, "formula-ort-cuda-bounded.json"));
        string? reportDirectory = Path.GetDirectoryName(report);
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);

        var rows = new List<object>();
        bool cudaUnavailable = false;
        var cpuRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        var cudaRequest = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cuda");
        var factory = new OpenCvVisualInputFactory();

        foreach (var formulaCase in cases)
        {
            string modelPath = Path.Combine(modelRoot, formulaCase.Model + ".onnx");
            string yaml = Path.Combine(sourceRoot, formulaCase.Model, formulaCase.Source, "inference.yml");
            if (!File.Exists(modelPath) || !File.Exists(yaml))
            {
                rows.Add(new { model = formulaCase.Model, status = "missing-asset", modelPath, tokenizerPath = yaml });
                continue;
            }

            object? lastRawOutputSummary = null;
            try
            {
                PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yaml);
                int end = Find(tokenizer.Tokens, "</s>"), start = Find(tokenizer.Tokens, "<s>"), pad = Find(tokenizer.Tokens, "<pad>"), unk = Find(tokenizer.Tokens, "<unk>");
                PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/" + formulaCase.Model);
                PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(descriptor,
                    new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unk), modelSize: formulaCase.Size, maximumSequenceLength: 4096);
                var sampleRows = new List<object>();
                int tokenMatches = 0;
                int latexMatches = 0;
                double? maximumRawDifference = null;
                foreach (RealFormulaRow sample in samples)
                {
                    string imagePath = Path.Combine(datasetRoot, sample.ImageRelPath.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(imagePath)) throw new FileNotFoundException("realFormula image is missing", imagePath);
                    // Formula exports contain autoregressive Loop state. Recreate the
                    // registries and sessions for every sample so a provider-side
                    // state/cache issue cannot contaminate the next comparison row.
                    using var cpuRegistry = new BackendRegistry().UseOnnxRuntime(new OnnxRuntimeOptions(executionProvider: OnnxRuntimeExecutionProvider.Cpu));
                    using var cudaRegistry = new BackendRegistry().UseOnnxRuntime(new OnnxRuntimeOptions(
                        executionProvider: OnnxRuntimeExecutionProvider.Cuda,
                        cudaProviderOptions: cudaProviderOptions,
                        sessionConfigEntries: sessionConfigEntries,
                        preferPreallocatedCpuOutputs: preferPreallocatedCpuOutputs));
                    using IInferenceSession cpuSession = cpuRegistry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), cpuRequest);
                    using IInferenceSession cudaSession = cudaRegistry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), cudaRequest);
                    using PreparedVisualInput input = factory.CreateFromFile(imagePath, profile.VisualProfile, inputId: sample.Image);
                    InferenceOutputs cpuOutputs = cpuSession.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                    InferenceOutputs cudaOutputs = cudaSession.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                    ITensor cudaRaw = cudaOutputs.GetRequired(((PaddleDocumentFormulaDecoder)profile.VisualProfile.Decoder).OutputName);
                    lastRawOutputSummary = SummarizeIntegerTensor(cudaRaw);
                    PaddleDocumentFormulaResult cpuResult = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, cpuOutputs, CancellationToken.None));
                    PaddleDocumentFormulaResult cudaResult = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, cudaOutputs, CancellationToken.None));
                    ITensor cpuRaw = cpuOutputs.GetRequired(((PaddleDocumentFormulaDecoder)profile.VisualProfile.Decoder).OutputName);
                    double? rawDifference = TensorMaximumAbsDifference(cpuRaw, cudaRaw);
                    if (rawDifference.HasValue)
                        maximumRawDifference = Math.Max(maximumRawDifference ?? 0, rawDifference.Value);
                    bool sameTokens = cpuResult.TokenIds.SequenceEqual(cudaResult.TokenIds);
                    bool sameLatex = string.Equals(cpuResult.Latex, cudaResult.Latex, StringComparison.Ordinal);
                    if (sameTokens) tokenMatches++;
                    if (sameLatex) latexMatches++;
                    sampleRows.Add(new
                    {
                        image = sample.Image,
                        imageSha256 = sample.ImageSha256,
                        inputShape = input.Tensor.Shape.ToArray(),
                        cpuTokenCount = cpuResult.TokenIds.Count,
                        cudaTokenCount = cudaResult.TokenIds.Count,
                        tokenIdsMatch = sameTokens,
                        latexMatch = sameLatex,
                        cpuLatexSha256 = FileShaText(cpuResult.Latex),
                        cudaLatexSha256 = FileShaText(cudaResult.Latex),
                        rawOutputShape = cudaRaw.Shape.ToArray(),
                        rawOutputsComparable = rawDifference.HasValue,
                        rawMaximumAbsoluteDifference = rawDifference,
                        cpuWarnings = cpuResult.Warnings.ToArray(),
                        cudaWarnings = cudaResult.Warnings.ToArray()
                    });
                }
                rows.Add(new
                {
                    model = formulaCase.Model,
                    status = tokenMatches == samples.Count && latexMatches == samples.Count && maximumRawDifference.HasValue ? "pass" : "parity-failed",
                    modelSha256 = FileSha(modelPath),
                    tokenizerSha256 = FileSha(yaml),
                    sampleCount = samples.Count,
                    tokenMatches,
                    latexMatches,
                    maximumRawAbsoluteDifference = maximumRawDifference,
                    results = sampleRows
                });
            }
            catch (Exception exception)
            {
                cudaUnavailable |= exception.Message.Contains("CUDA", StringComparison.OrdinalIgnoreCase)
                    || exception.Message.Contains("execution provider", StringComparison.OrdinalIgnoreCase)
                    || exception.Message.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase);
                rows.Add(new { model = formulaCase.Model, status = "unsupported", error = exception.GetType().FullName, detail = exception.Message, exception = exception.ToString(), rawOutput = lastRawOutputSummary });
            }
        }

        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedAtUtc = DateTimeOffset.UtcNow,
            backend = "onnxruntime-cuda",
            managedAssembly = typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly.GetName().Version?.ToString(),
            runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            modelCount = cases.Length,
            sampleCountPerModel = samples.Count,
            cudaProviderOptions,
            sessionConfigEntries,
            preferPreallocatedCpuOutputs,
            models = rows,
            boundary = "Bounded ORT CUDA execution and CPU token/LaTeX parity on the selected realFormula samples. This is not a 121-sample CUDA quality score, a mathematical semantic-equivalence result, or evidence for OpenVINO/OpenCV/TensorRT."
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        if (cudaUnavailable && rows.All(row => !row.ToString()!.Contains("status = pass", StringComparison.OrdinalIgnoreCase)))
            Assert.Inconclusive("The CUDA execution provider was unavailable; see the machine-readable report: " + report);
        Assert.IsTrue(rows.Any(row => row.ToString()!.Contains("status = pass", StringComparison.OrdinalIgnoreCase)), "No formula model produced a CUDA evidence row. See " + report);
    }

    private sealed record RealFormulaRow(string Image, string ImageRelPath, string ImageSha256, string ReferenceLatex);

    private static IReadOnlyDictionary<string, string> ParseOptionEntries(string? value, string variableName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value)) return result;
        foreach (string item in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = item.IndexOf('=');
            if (separator <= 0 || separator == item.Length - 1)
                throw new AssertFailedException(variableName + " entries must use key=value;key=value syntax.");
            string key = item.Substring(0, separator).Trim();
            string entryValue = item.Substring(separator + 1).Trim();
            if (key.Length == 0 || entryValue.Length == 0)
                throw new AssertFailedException(variableName + " contains an empty key or value.");
            result[key] = entryValue;
        }
        return result;
    }

    private static int Find(IReadOnlyList<string> values, string value) { for (int i=0;i<values.Count;i++) if (values[i] == value) return i; return -1; }
    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string FileShaText(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static double? TensorMaximumAbsDifference(ITensor left, ITensor right)
    {
        if (left.ElementType != right.ElementType || left.Shape.Rank != right.Shape.Rank) return null;
        for (int dimension = 0; dimension < left.Shape.Rank; dimension++)
            if (left.Shape[dimension] != right.Shape[dimension]) return null;
        if (left.ElementType == TensorElementType.Float32 && left.Buffer is float[] leftValues && right.Buffer is float[] rightValues)
        {
            double maximum = 0;
            for (int index = 0; index < Math.Min(leftValues.Length, rightValues.Length); index++)
                maximum = Math.Max(maximum, Math.Abs((double)leftValues[index] - rightValues[index]));
            return maximum;
        }
        if (left.ElementType == TensorElementType.Int64 && left.Buffer is long[] leftInt64 && right.Buffer is long[] rightInt64)
        {
            decimal maximum = 0;
            for (int index = 0; index < Math.Min(leftInt64.Length, rightInt64.Length); index++)
                maximum = Math.Max(maximum, Math.Abs((decimal)leftInt64[index] - rightInt64[index]));
            return (double)maximum;
        }
        if (left.ElementType == TensorElementType.Int32 && left.Buffer is int[] leftInt32 && right.Buffer is int[] rightInt32)
        {
            long maximum = 0;
            for (int index = 0; index < Math.Min(leftInt32.Length, rightInt32.Length); index++)
                maximum = Math.Max(maximum, Math.Abs((long)leftInt32[index] - rightInt32[index]));
            return maximum;
        }
        return null;
    }
    private static string TensorSha256(ITensor tensor)
    {
        if (tensor.ElementType == TensorElementType.Float32 && tensor.Buffer is float[] values)
        {
            byte[] bytes = ReadFloat32Bytes(tensor);
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
        throw new InvalidOperationException("Formula image input is not a Float32 tensor.");
    }

    private static byte[] ReadFloat32Bytes(ITensor tensor)
    {
        if (tensor.ElementType != TensorElementType.Float32 || tensor.Buffer is not float[] values)
            throw new InvalidOperationException("Formula image input is not a Float32 tensor.");
        byte[] bytes = new byte[checked(values.Length * sizeof(float))];
        System.Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static string OverrideFloat32Tensor(ITensor tensor, string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Formula input tensor override was not found.", path);
        if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Formula input tensor overrides are currently defined as little-endian Float32.");
        if (tensor.ElementType != TensorElementType.Float32 || tensor.Buffer is not float[] values)
            throw new InvalidOperationException("Formula input tensor override requires a Float32 tensor.");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != checked(values.Length * sizeof(float)))
            throw new InvalidDataException($"Formula input tensor override contains {bytes.Length} bytes; expected {values.Length * sizeof(float)} for shape [{string.Join(",", tensor.Shape)}].");
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static long[] ReadIntegerSequence(ITensor tensor)
    {
        if (tensor.ElementType == TensorElementType.Int64 && tensor.Buffer is long[] values64) return values64;
        if (tensor.ElementType == TensorElementType.Int32 && tensor.Buffer is int[] values32) return values32.Select(value => (long)value).ToArray();
        throw new InvalidOperationException("Formula model output is not a supported integer token tensor: " + tensor.ElementType);
    }

    private static object SummarizeIntegerTensor(ITensor tensor)
    {
        if (tensor.ElementType == TensorElementType.Int64 && tensor.Buffer is long[] values64 && values64.Length > 0)
            return new { elementType = "int64", shape = tensor.Shape.ToArray(), length = values64.Length, minimum = values64.Min(), maximum = values64.Max(), first = values64.Take(Math.Min(12, values64.Length)).ToArray() };
        if (tensor.ElementType == TensorElementType.Int32 && tensor.Buffer is int[] values32 && values32.Length > 0)
            return new { elementType = "int32", shape = tensor.Shape.ToArray(), length = values32.Length, minimum = values32.Min(), maximum = values32.Max(), first = values32.Take(Math.Min(12, values32.Length)).ToArray() };
        return new { elementType = tensor.ElementType.ToString(), shape = tensor.Shape.ToArray(), length = tensor.Length };
    }

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
