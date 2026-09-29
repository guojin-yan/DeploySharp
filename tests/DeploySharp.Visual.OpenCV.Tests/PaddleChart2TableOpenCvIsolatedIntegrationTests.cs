using System;
using System.Collections.Generic;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

/// <summary>Loads one Chart2Table graph per isolated process to classify OpenCV DNN importer support safely. / 每个隔离进程加载一张 Chart2Table 图，安全判定 OpenCV DNN importer 支持情况。</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaddleChart2TableOpenCvIsolatedIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public void SelectedChart2TableGraphLoadsOnOpenCvDnn()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_OPENCV_PROBE") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_OPENCV_PROBE=1.");
        string graph = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_OPENCV_GRAPH") ?? string.Empty;
        GraphCase item = Cases().Find(value => string.Equals(value.Name, graph, StringComparison.OrdinalIgnoreCase))
            ?? throw new AssertFailedException("Set DEPLOYSHARP_CHART2TABLE_OPENCV_GRAPH to vision, embedding, prefill or decode.");
        string exportRoot = Required("DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT");
        string textRoot = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_TEXT_ROOT") ?? Path.Combine(exportRoot, "text-onnx-verify-20260923");
        string path = item.ResolvePath(exportRoot, textRoot);
        if (!File.Exists(path)) Assert.Inconclusive("Missing Chart2Table graph: " + path);

        var contract = item.CreateContract();
        using var registry = new BackendRegistry();
        registry.Register(new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true)));
        var request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
        using IInferenceSession session = registry.CreateSession(new ModelArtifact(contract.ModelId, "onnx", path, preferredBackend: OpenCvDnnBackendProvider.BackendId), request, new SessionOptions(1, false));
        Console.WriteLine("PADDLE_CHART2TABLE_OPENCV graph=" + item.Name + ";status=pass;path=" + path);
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive("Required external integration variable is missing: " + name);
        return value!;
    }

    private static List<GraphCase> Cases() => new()
    {
        new GraphCase("vision", "paddle-chart/pp-chart2table/vision-projector", "chart-vision.onnx", new[] { new TensorDescriptor("pixel_values", TensorElementType.Float32, new TensorShape(1, 3, 1024, 1024)) }, new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, 256, 1024)) }, new[] { "pixel_values" }),
        new GraphCase("embedding", "paddle-chart/pp-chart2table/token-embedding", "chart-token-embedding-dynamic.onnx", new[] { new TensorDescriptor("input_ids", TensorElementType.Int64, new TensorShape(1, -1)) }, new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, -1, 1024)) }, Array.Empty<string>()),
        new GraphCase("prefill", "paddle-chart/pp-chart2table/text-prefill", "chart-text-prefill-full.onnx", new[] { new TensorDescriptor("inputs_embeds", TensorElementType.Float32, new TensorShape(1, 286, 1024)), new TensorDescriptor("attention_mask", TensorElementType.Boolean, new TensorShape(1, 286)), new TensorDescriptor("position_ids", TensorElementType.Int64, new TensorShape(1, 286)) }, BuildOutputs(286), Array.Empty<string>()),
        new GraphCase("decode", "paddle-chart/pp-chart2table/text-decode-with-past", "chart-text-decoder-dynamic-past-full.onnx", BuildDecodeInputs(), BuildOutputs(-1), Array.Empty<string>())
    };

    private static TensorDescriptor[] BuildOutputs(int sequence)
    {
        var values = new List<TensorDescriptor> { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, 1, 151860)) };
        for (int index = 1; index < 49; index++) values.Add(new TensorDescriptor("fetch_name_" + index, TensorElementType.Float32, new TensorShape(1, sequence, 16, 64)));
        return values.ToArray();
    }

    private static TensorDescriptor[] BuildDecodeInputs()
    {
        var values = new List<TensorDescriptor>
        {
            new TensorDescriptor("inputs_embeds", TensorElementType.Float32, new TensorShape(1, 1, 1024)),
            new TensorDescriptor("attention_mask", TensorElementType.Boolean, new TensorShape(1, -1)),
            new TensorDescriptor("position_ids", TensorElementType.Int64, new TensorShape(1, 1))
        };
        for (int index = 0; index < 24; index++)
        {
            values.Add(new TensorDescriptor("past_key_" + index, TensorElementType.Float32, new TensorShape(1, -1, 16, 64)));
            values.Add(new TensorDescriptor("past_value_" + index, TensorElementType.Float32, new TensorShape(1, -1, 16, 64)));
        }
        return values.ToArray();
    }

    private sealed class GraphCase
    {
        public GraphCase(string name, string modelId, string fileName, TensorDescriptor[] inputs, TensorDescriptor[] outputs, string[] imageInputs)
        { Name = name; ModelId = new ModelId(modelId); FileName = fileName; Inputs = inputs; Outputs = outputs; ImageInputs = imageInputs; }
        public string Name { get; }
        public ModelId ModelId { get; }
        public string FileName { get; }
        public TensorDescriptor[] Inputs { get; }
        public TensorDescriptor[] Outputs { get; }
        public string[] ImageInputs { get; }
        public OpenCvDnnModelContract CreateContract() => new OpenCvDnnModelContract(ModelId, Inputs, Outputs, ImageInputs);
        public string ResolvePath(string exportRoot, string textRoot) => Name == "vision" ? Path.Combine(exportRoot, FileName) : Path.Combine(textRoot, FileName);
    }
}
