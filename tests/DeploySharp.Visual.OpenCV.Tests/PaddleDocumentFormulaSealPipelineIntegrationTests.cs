using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentFormulaSealPipelineIntegrationTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string SourceRoot = @"E:\Model\PaddleDocument\source";

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task FormulaAndSealResultsComposeIntoPagePipelineOnOrtCpu()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1 to run formula/seal page stages.");
        string formulaImage = @"E:\Model\PaddleDocument\validation\general_formula_rec_001.png";
        string sealImage = @"E:\Data\ocr\demo_1.jpg";
        string formulaModel = Path.Combine(ModelRoot, "pp-formulanet-plus-s.onnx");
        string formulaYaml = Path.Combine(SourceRoot, "pp-formulanet-plus-s", "PP-FormulaNet_plus-S_infer", "inference.yml");
        string sealModel = Path.Combine(ModelRoot, "ppocrv4-mobile-seal-det.onnx");
        if (!File.Exists(formulaImage) || !File.Exists(formulaModel) || !File.Exists(formulaYaml)) Assert.Inconclusive("Formula validation assets are incomplete.");
        if (!File.Exists(sealImage) || !File.Exists(sealModel)) Assert.Inconclusive("Seal validation assets are incomplete.");

        PaddleDocumentFormulaTokenizer tokenizer = PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(formulaYaml);
        int end = Find(tokenizer.Tokens, "</s>"), start = Find(tokenizer.Tokens, "<s>"), pad = Find(tokenizer.Tokens, "<pad>"), unk = Find(tokenizer.Tokens, "<unk>");
        var formulaDescriptor = PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s");
        PaddleDocumentProfile formulaProfile = PaddleDocumentProfiles.CreateFormula(formulaDescriptor, new PaddleDocumentFormulaSchema(tokenizer, end, start, pad, unk), modelSize: new VisualSize(384, 384));
        var sealDescriptor = PaddleDocumentModelCatalog.Get("paddle-seal/ppocrv4-mobile");
        PaddleDocumentProfile sealProfile = PaddleDocumentProfiles.CreateSealDetection(sealDescriptor, new VisualSize(224, 224));
        using var formulaRegistry = new BackendRegistry(); formulaRegistry.UseOnnxRuntime();
        using var sealRegistry = new BackendRegistry(); sealRegistry.UseOnnxRuntime();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using VisualPipeline formulaPipeline = CreatePipeline(formulaRegistry, formulaProfile, formulaModel, request);
        using VisualPipeline sealPipeline = CreatePipeline(sealRegistry, sealProfile, sealModel, request);
        string formulaSha = Sha256(formulaImage), sealSha = Sha256(sealImage);

        var formulaStage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
            PaddleDocumentModule.FormulaRecognition, formulaPipeline,
            (context, token) => new OpenCvVisualInputFactory().CreateFromFile(formulaImage, formulaProfile.VisualProfile, formulaSha, token),
            (context, inference) =>
            {
                PaddleDocumentFormulaResult value = inference.GetValue<PaddleDocumentFormulaResult>();
                return new PaddleDocumentFormulaResult(new PaddleDocumentResultMetadata(formulaDescriptor, inference.BackendId.Value, inference.Timing.Total, formulaSha, context.Page.PageIndex), value.Latex, value.Warnings, value.TokenIds);
            });
        var sealStage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
            PaddleDocumentModule.SealTextDetection, sealPipeline,
            (context, token) => new OpenCvVisualInputFactory().CreateFromFile(sealImage, sealProfile.VisualProfile, sealSha, token),
            (context, inference) =>
            {
                PaddleDocumentSealResult value = inference.GetValue<PaddleDocumentSealResult>();
                return new PaddleDocumentSealResult(new PaddleDocumentResultMetadata(sealDescriptor, inference.BackendId.Value, inference.Timing.Total, sealSha, context.Page.PageIndex), value.MaskWidth, value.MaskHeight, value.Regions, value.Warnings);
            });

        PaddleDocumentPipelineResult formulaPage = await new PaddleDocumentPipeline(new[] { formulaStage }).RunAsync(new PaddleDocumentPage(formulaImage, new VisualSize(512, 512), 2), CancellationToken.None).ConfigureAwait(false);
        PaddleDocumentPipelineResult sealPage = await new PaddleDocumentPipeline(new[] { sealStage }).RunAsync(new PaddleDocumentPage(sealImage, new VisualSize(500, 500), 3), CancellationToken.None).ConfigureAwait(false);
        PaddleDocumentFormulaResult formula = formulaPage.GetRequired<PaddleDocumentFormulaResult>(PaddleDocumentModule.FormulaRecognition);
        PaddleDocumentSealResult seal = sealPage.GetRequired<PaddleDocumentSealResult>(PaddleDocumentModule.SealTextDetection);
        Assert.AreEqual(2, formula.Metadata.PageIndex); Assert.AreEqual(formulaSha, formula.Metadata.InputSha256);
        Assert.IsTrue(formula.TokenIds.Count > 0 && formula.Latex.Contains("\\frac", StringComparison.Ordinal));
        Assert.AreEqual(3, seal.Metadata.PageIndex); Assert.AreEqual(sealSha, seal.Metadata.InputSha256);
        Assert.IsTrue(seal.MaskWidth > 0 && seal.MaskHeight > 0);
    }

    private static VisualPipeline CreatePipeline(BackendRegistry registry, PaddleDocumentProfile profile, string path, BackendRequest request)
    {
        var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
        return new VisualPipeline(registry, profiles.Select(profile.CreateArtifact(path, OnnxRuntimeBackendProvider.BackendId), registry, request, profile.VisualProfile.Task), request);
    }

    private static int Find(System.Collections.Generic.IReadOnlyList<string> tokens, string value)
    {
        for (int i = 0; i < tokens.Count; i++) if (tokens[i] == value) return i;
        return -1;
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
