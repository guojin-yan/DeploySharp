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

    private static int Find(IReadOnlyList<string> values, string value) { for (int i=0;i<values.Count;i++) if (values[i] == value) return i; return -1; }
    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
