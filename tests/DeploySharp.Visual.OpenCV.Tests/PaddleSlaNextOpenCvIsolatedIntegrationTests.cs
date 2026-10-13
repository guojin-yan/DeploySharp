using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs one exact SLANeXt artifact per process through OpenCV DNN and the table decoder. / 每个进程使用 OpenCV DNN 和表格 Decoder 运行一个精确 SLANeXt 工件。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleSlaNextOpenCvIsolatedIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SelectedSlaNextArtifactRunsOnOpenCvDnnAndDecodesTable()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_SLANEXT_OPENCV_PROBE") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_SLANEXT_OPENCV_PROBE=1.");

        string selection = Environment.GetEnvironmentVariable("DEPLOYSHARP_SLANEXT_OPENCV_ARTIFACT") ?? string.Empty;
        ArtifactCase item = Resolve(selection);
        string modelPath = item.ResolvePath();
        string imagePath = Required("DEPLOYSHARP_SLANEXT_OPENCV_IMAGE");
        if (!File.Exists(modelPath)) Assert.Inconclusive("Missing SLANeXt artifact: " + modelPath);
        if (!File.Exists(imagePath)) Assert.Inconclusive("Missing SLANeXt input image: " + imagePath);
        string modelSha256 = FileSha256(modelPath);
        string inputSha256 = FileSha256(imagePath);

        PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Get(item.CatalogModelId);
        PaddleDocumentProfile profile = PaddleDocumentProfiles.CreateTableStructure(descriptor, modelSize: new VisualSize(512, 512));
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile, inputId: item.MatrixModelId);
        var contract = new OpenCvDnnModelContract(
            profile.VisualProfile.ModelId,
            new[] { new TensorDescriptor(profile.VisualProfile.Input.Name, profile.VisualProfile.Input.ElementType, profile.VisualProfile.Input.ShapePattern) },
            profile.VisualProfile.Outputs.Select(output => new TensorDescriptor(output.Name, output.ElementType, new TensorShape(1, -1, output.ShapePattern[2]))),
            new[] { profile.VisualProfile.Input.Name });

        using var registry = new BackendRegistry().UseOpenCvDnn(new OpenCvDnnOptions(
            contract,
            enableFusion: true,
            enableWinograd: true,
            specializeDynamicInputShapes: true));
        var request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
        using IInferenceSession session = registry.CreateSession(
            profile.CreateArtifactWithSha256(modelPath, modelSha256, OpenCvDnnBackendProvider.BackendId),
            request,
            new SessionOptions(1, false));
        InferenceOutputs outputs = session.Run(InferenceInputs.Create(input.InputName, input.Tensor), CancellationToken.None);
        var decoded = profile.VisualProfile.Decoder.Decode(new VisualDecodeContext(input, profile.VisualProfile, outputs, CancellationToken.None)) as PaddleDocumentTableResult
            ?? throw new AssertFailedException(item.MatrixModelId + " did not return a PaddleDocumentTableResult.");

        Assert.IsTrue(decoded.Tokens.Count > 10, item.MatrixModelId + " returned an empty table structure.");
        Assert.IsTrue(decoded.Regions.Count > 0, item.MatrixModelId + " returned no table cells.");
        Console.WriteLine(
            "PADDLE_DOCUMENT_SLANEXT_OPENCV artifact=" + item.MatrixModelId +
            ";modelSha256=" + modelSha256 +
            ";inputSha256=" + inputSha256 +
            ";inputShape=" + input.Tensor.Shape +
            ";outputs=" + string.Join(",", outputs.Select(output => output.Name + output.Tensor.Shape)) +
            ";tokens=" + decoded.Tokens.Count +
            ";cells=" + decoded.Regions.Count +
            ";markupSha256=" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(decoded.Markup))).ToLowerInvariant());
    }

    private static ArtifactCase Resolve(string selection)
    {
        string originalRoot = Required("DEPLOYSHARP_SLANEXT_OPENCV_ORIGINAL_ROOT");
        string compatibilityRoot = Required("DEPLOYSHARP_SLANEXT_OPENCV_COMPAT_ROOT");
        return selection switch
        {
            "wired" => new ArtifactCase("paddle-table/slanext-wired", "paddle-table/slanext-wired", originalRoot, "slanext-wired.onnx"),
            "wireless" => new ArtifactCase("paddle-table/slanext-wireless", "paddle-table/slanext-wireless", originalRoot, "slanext-wireless.onnx"),
            "wired-openvino-compat" => new ArtifactCase("paddle-table/slanext-wired-openvino-compat", "paddle-table/slanext-wired", compatibilityRoot, "slanext-wired-openvino-compat.onnx"),
            "wireless-openvino-compat" => new ArtifactCase("paddle-table/slanext-wireless-openvino-compat", "paddle-table/slanext-wireless", compatibilityRoot, "slanext-wireless-openvino-compat.onnx"),
            _ => throw new AssertFailedException("Set DEPLOYSHARP_SLANEXT_OPENCV_ARTIFACT to wired, wireless, wired-openvino-compat or wireless-openvino-compat.")
        };
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Required external integration variable is missing: " + name);
        return value!;
    }

    private static string FileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record ArtifactCase(string MatrixModelId, string CatalogModelId, string Root, string FileName)
    {
        public string ResolvePath() => Path.Combine(Root, FileName);
    }
}
