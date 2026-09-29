using System;
using System.IO;
using System.Linq;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs exactly one formula model in an isolated test process so a native importer crash cannot hide other model statuses. / 在隔离测试进程中运行单个公式模型，避免 native importer 崩溃掩盖其它模型状态。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentFormulaOpenVinoIsolatedIntegrationTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string SourceRoot = @"E:\Model\PaddleDocument\source";
    private const string FormulaImage = @"E:\Model\PaddleDocument\validation\general_formula_rec_001.png";

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SelectedFormulaExportRunsOnOpenVinoCpu()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_OPENVINO") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_OPENVINO=1.");
        string modelId = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_MODEL") ?? string.Empty;
        FormulaCase item = Cases().SingleOrDefault(value => string.Equals(value.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
            ?? throw new AssertFailedException("Set DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_MODEL to one of the catalogued formula model IDs.");
        string modelPath = Path.Combine(ModelRoot, item.ModelId + ".onnx");
        string yamlPath = Path.Combine(SourceRoot, item.ModelId, item.SourceDirectory, "inference.yml");
        if (!File.Exists(modelPath) || !File.Exists(yamlPath) || !File.Exists(FormulaImage))
            Assert.Inconclusive("Formula model, tokenizer or image is missing for " + item.ModelId + ".");

        PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(yamlPath);
        int end = Find(tokenizer.Tokens, "</s>");
        int start = Find(tokenizer.Tokens, "<s>");
        int pad = Find(tokenizer.Tokens, "<pad>");
        int unknown = Find(tokenizer.Tokens, "<unk>");
        if (end < 0) throw new AssertFailedException("The tokenizer does not expose </s>.");
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateFormula(
            PaddleDocumentModelCatalog.Get("paddle-formula/" + item.ModelId),
            new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unknown),
            modelSize: item.ModelSize,
            maximumSequenceLength: 4096);
        using var registry = new BackendRegistry();
        registry.UseOpenVino();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(FormulaImage, profile.VisualProfile, inputId: "formula-openvino-isolated");
        using IInferenceSession session = registry.CreateSession(profile.CreateArtifact(modelPath, OpenVinoBackendProvider.BackendId), request);
        InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
        PaddleDocumentFormulaResult result = (PaddleDocumentFormulaResult)profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None));
        Assert.IsTrue(result.TokenIds.Count > 0, "The formula model returned no tokens.");
        Console.WriteLine("PADDLE_DOCUMENT_FORMULA_OPENVINO_ISOLATED model=" + item.ModelId + ";status=pass;tokens=" + result.TokenIds.Count + ";latexLength=" + result.Latex.Length + ";warnings=" + result.Warnings.Count);
    }

    private static int Find(System.Collections.Generic.IReadOnlyList<string> tokens, string value)
    {
        for (int index = 0; index < tokens.Count; index++) if (tokens[index] == value) return index;
        return -1;
    }

    private static FormulaCase[] Cases() => new[]
    {
        new FormulaCase("pp-formulanet-plus-s", "PP-FormulaNet_plus-S_infer", new VisualSize(384, 384)),
        new FormulaCase("pp-formulanet-plus-m", "PP-FormulaNet_plus-M_infer", new VisualSize(384, 384)),
        new FormulaCase("pp-formulanet-plus-l", "PP-FormulaNet_plus-L_infer", new VisualSize(768, 768)),
        new FormulaCase("pp-formulanet-s", "PP-FormulaNet-S_infer", new VisualSize(384, 384)),
        new FormulaCase("pp-formulanet-l", "PP-FormulaNet-L_infer", new VisualSize(768, 768)),
        new FormulaCase("unimernet", "UniMERNet_infer", new VisualSize(672, 192))
    };

    private sealed record FormulaCase(string ModelId, string SourceDirectory, VisualSize ModelSize);
}
