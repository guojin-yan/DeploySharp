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

/// <summary>Collects multi-model formula and multi-image seal execution evidence without claiming accuracy. / 收集多模型公式和多图印章执行证据，不宣称准确率。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentFormulaSealEvidenceTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string SourceRoot = @"E:\Model\PaddleDocument\source";
    private const string FormulaImage = @"E:\Model\PaddleDocument\validation\general_formula_rec_001.png";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void FormulaModelsAndSealModelsProduceMachineReadableOrtEvidence()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL") != "1") Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1 to collect formula/seal evidence.");
        if (!File.Exists(FormulaImage)) Assert.Inconclusive("Missing formula reference image: " + FormulaImage);
        var formulaRows = new List<object>();
        foreach (FormulaCase item in FormulaCases())
        {
            string modelPath = Path.Combine(ModelRoot, item.ModelId + ".onnx");
            string yaml = Path.Combine(SourceRoot, item.ModelId, item.SourceDirectory, "inference.yml");
            if (!File.Exists(modelPath) || !File.Exists(yaml)) { formulaRows.Add(new { model = item.ModelId, status = "missing-asset" }); continue; }
            PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yaml);
            int end = Find(tokenizer.Tokens, "</s>"), start = Find(tokenizer.Tokens, "<s>"), pad = Find(tokenizer.Tokens, "<pad>"), unknown = Find(tokenizer.Tokens, "<unk>");
            PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get("paddle-formula/" + item.ModelId);
            PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(descriptor, new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unknown), modelSize: item.Size, maximumSequenceLength: 4096);
            using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
            BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(FormulaImage, profile.VisualProfile, Sha256(FormulaImage));
            using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), request);
            InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
            PaddleDocumentFormulaResult result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
            Assert.IsTrue(result.TokenIds.Count > 0);
            formulaRows.Add(new { model = item.ModelId, status = "pass", modelSha256 = Sha256(modelPath), imageSha256 = Sha256(FormulaImage), tokenCount = result.TokenIds.Count, latexLength = result.Latex.Length, warnings = result.Warnings.ToArray(), latex = result.Latex });
        }

        var sealRows = new List<object>();
        foreach (string image in new[] { @"E:\Data\ocr\demo_1.jpg", @"E:\Data\ocr\demo_2.jpg", @"E:\Data\ocr\demo_3.jpg" })
        {
            foreach (string model in new[] { "ppocrv4-mobile-seal-det", "ppocrv4-server-seal-det" })
            {
                string modelPath = Path.Combine(ModelRoot, model + ".onnx");
                if (!File.Exists(modelPath) || !File.Exists(image)) { sealRows.Add(new { model, image, status = "missing-asset" }); continue; }
                string modelId = model.StartsWith("ppocrv4-mobile", StringComparison.Ordinal) ? "paddle-seal/ppocrv4-mobile" : "paddle-seal/ppocrv4-server";
                PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(modelId);
                PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateSealDetection(descriptor, new VisualSize(224, 224));
                using var registry = new BackendRegistry(); registry.UseOnnxRuntime();
                BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
                using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(image, profile.VisualProfile, Sha256(image));
                using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), request);
                InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
                PaddleDocumentSealResult result = (PaddleDocumentSealResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
                sealRows.Add(new { model = modelId, status = "pass", modelSha256 = Sha256(modelPath), image, imageSha256 = Sha256(image), maskWidth = result.MaskWidth, maskHeight = result.MaskHeight, regions = result.Regions.Count, scores = result.Regions.Select(region => region.Score).ToArray() });
            }
        }
        string directory = Path.Combine(TestContext.TestResultsDirectory!, "paddle-document"); Directory.CreateDirectory(directory);
        string report = Path.Combine(directory, "formula-seal-ort-evidence.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, backend = "onnxruntime-cpu", formula = formulaRows, seals = sealRows, boundary = "Execution evidence only; no ground-truth formula or seal labels are included." }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Assert.IsTrue(formulaRows.Any(row => row.ToString()!.Contains("pass", StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<FormulaCase> FormulaCases()
    {
        yield return new FormulaCase("pp-formulanet-plus-s", "PP-FormulaNet_plus-S_infer", new VisualSize(384, 384));
        yield return new FormulaCase("pp-formulanet-plus-m", "PP-FormulaNet_plus-M_infer", new VisualSize(384, 384));
        yield return new FormulaCase("pp-formulanet-plus-l", "PP-FormulaNet_plus-L_infer", new VisualSize(768, 768));
        yield return new FormulaCase("pp-formulanet-s", "PP-FormulaNet-S_infer", new VisualSize(384, 384));
        yield return new FormulaCase("pp-formulanet-l", "PP-FormulaNet-L_infer", new VisualSize(768, 768));
        yield return new FormulaCase("unimernet", "UniMERNet_infer", new VisualSize(672, 192));
    }

    private static int Find(IReadOnlyList<string> tokens, string value) { for (int i = 0; i < tokens.Count; i++) if (tokens[i] == value) return i; return -1; }
    private static string Sha256(string path) { using FileStream stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private sealed record FormulaCase(string ModelId, string SourceDirectory, VisualSize Size);
}
