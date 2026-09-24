using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Runs table classification -> cell detection -> SLANeXt HTML as one real ORT page task. / 运行真实 ORT 表格分类→单元格检测→SLANeXt HTML 页面任务链。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentTablePipelineIntegrationTests
{
    private const string ModelRoot = @"E:\Model\PaddleDocument\onnx";
    private const string ImagePath = @"E:\Model\PaddleDocument\validation\table_recognition.jpg";

    [TestMethod]
    [TestCategory("ExternalModels")]
    public async Task TableClassificationCellDetectionAndSlaNextComposeToHtmlOnOrtCpu()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL=1 to run the local table pipeline.");
        string tableClsPath = RequireModel("pp-lcnet-x1-0-table-cls.onnx");
        string cellPath = RequireModel("rt-detr-l-wired-cell-det.onnx");
        string structurePath = RequireModel("slanext-wired.onnx");
        if (!File.Exists(ImagePath)) Assert.Inconclusive("Missing official table image: " + ImagePath);
        string sourceSha = Sha256(ImagePath);
        var page = new PaddleDocumentPage(ImagePath, new VisualSize(1654, 1300), 0);
        using var clsRegistry = new BackendRegistry();
        using var cellRegistry = new BackendRegistry();
        using var structureRegistry = new BackendRegistry();
        clsRegistry.UseOnnxRuntime(); cellRegistry.UseOnnxRuntime(); structureRegistry.UseOnnxRuntime();
        BackendRequest request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");

        PaddleDocumentModelDescriptor clsDescriptor = PaddleDocumentModelCatalog.Get("paddle-table/pp-lcnet-x1-0-table-cls");
        PaddleDocumentProfile clsProfile = PaddleDocumentProfiles.CreateClassification(clsDescriptor, new[] { "wired_table", "wireless_table" }, VisualTaskId.TableClassification, modelSize: new VisualSize(224, 224));
        PaddleDocumentModelDescriptor cellDescriptor = PaddleDocumentModelCatalog.Get("paddle-table/rt-detr-l-wired-cell-det");
        PaddleDocumentProfile cellProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(cellDescriptor, new[] { "table-cell" }, new VisualSize(640, 640), includeGeometryInputs: true, scoreThreshold: 0);
        PaddleDocumentModelDescriptor structureDescriptor = PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired");
        PaddleDocumentProfile structureProfile = PaddleDocumentProfiles.CreateTableStructure(structureDescriptor, modelSize: new VisualSize(512, 512));

        using VisualPipeline clsPipeline = CreatePipeline(clsRegistry, clsProfile, tableClsPath, request);
        using VisualPipeline cellPipeline = CreatePipeline(cellRegistry, cellProfile, cellPath, request);
        using VisualPipeline structurePipeline = CreatePipeline(structureRegistry, structureProfile, structurePath, request);
        var clsStage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
            PaddleDocumentModule.TableClassification,
            clsPipeline,
            (context, token) => new OpenCvVisualInputFactory().CreateFromFile(ImagePath, clsProfile.VisualProfile),
            (context, inference) =>
            {
                ClassificationResult value = inference.GetValue<ClassificationResult>();
                LabelScore top = value.TopPrediction ?? throw new AssertFailedException("Table classifier returned no prediction.");
                return new PaddleDocumentClassificationResult(PaddleDocumentModule.TableClassification,
                    new PaddleDocumentResultMetadata(clsDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex),
                    top.Label, top.Index, top.Score);
            });
        var cellStage = new PaddleDocumentDependentStage(PaddleDocumentModule.TableCellDetection, new[] { PaddleDocumentModule.TableClassification }, async (context, token) =>
        {
            using PreparedVisualInput input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), ImagePath, cellProfile, sourceSha);
            VisualInferenceResult inference = await cellPipeline.RunAsync(input, cancellationToken: token).ConfigureAwait(false);
            DetectionResult value = inference.GetValue<DetectionResult>();
            var regions = value.Detections.Select(item => new PaddleDocumentRegion(item.Label.Label, item.Label.Score, item.Box)).ToArray();
            return new PaddleDocumentRegionResult(PaddleDocumentModule.TableCellDetection,
                new PaddleDocumentResultMetadata(cellDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex), regions);
        });
        var structureStage = new PaddleDocumentDependentStage(PaddleDocumentModule.TableStructureRecognition, new[] { PaddleDocumentModule.TableCellDetection }, async (context, token) =>
        {
            using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(ImagePath, structureProfile.VisualProfile, sourceSha);
            VisualInferenceResult inference = await structurePipeline.RunAsync(input, cancellationToken: token).ConfigureAwait(false);
            PaddleDocumentTableResult value = inference.GetValue<PaddleDocumentTableResult>();
            return new PaddleDocumentTableResult(PaddleDocumentModule.TableStructureRecognition,
                new PaddleDocumentResultMetadata(structureDescriptor, inference.BackendId.Value, inference.Timing.Total, sourceSha, context.Page.PageIndex),
                value.Markup, value.Regions, value.TableType, value.Warnings, value.Tokens, value.AverageScore);
        });

        PaddleDocumentPipelineResult result = await new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[] { structureStage, cellStage, clsStage }).RunAsync(page, CancellationToken.None).ConfigureAwait(false);
        PaddleDocumentClassificationResult classification = result.GetRequired<PaddleDocumentClassificationResult>(PaddleDocumentModule.TableClassification);
        PaddleDocumentRegionResult cells = result.GetRequired<PaddleDocumentRegionResult>(PaddleDocumentModule.TableCellDetection);
        PaddleDocumentTableResult table = result.GetRequired<PaddleDocumentTableResult>(PaddleDocumentModule.TableStructureRecognition);
        Assert.AreEqual("wired_table", classification.Label);
        Assert.IsTrue(cells.Regions.Count > 0);
        Assert.IsTrue(table.Tokens.Count > 10);
        Assert.IsTrue(table.Markup.Contains("<td", StringComparison.OrdinalIgnoreCase) || table.Markup.Contains("<table", StringComparison.OrdinalIgnoreCase), "SLANeXt markup did not contain an HTML table/cell tag: " + table.Markup);
        Assert.IsTrue(table.Regions.Count >= 10);
        Console.WriteLine("PADDLE_DOCUMENT_TABLE_PIPELINE backend=onnxruntime-cpu;classification=" + classification.Label + ";cells=" + cells.Regions.Count + ";tokens=" + table.Tokens.Count + ";htmlLength=" + table.Markup.Length + ";totalMs=" + result.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";inputSha256=" + sourceSha);
    }

    private static VisualPipeline CreatePipeline(BackendRegistry registry, PaddleDocumentProfile profile, string modelPath, BackendRequest request)
    {
        var profiles = new VisualProfileRegistry(); profiles.Register(profile.VisualProfile); profiles.Freeze();
        VisualProfileSelection selection = profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId), registry, request, profile.VisualProfile.Task);
        return new VisualPipeline(registry, selection, request);
    }

    private static string RequireModel(string name)
    {
        string path = Path.Combine(ModelRoot, name);
        if (!File.Exists(path)) Assert.Inconclusive("Missing local table pipeline model: " + path);
        return path;
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
