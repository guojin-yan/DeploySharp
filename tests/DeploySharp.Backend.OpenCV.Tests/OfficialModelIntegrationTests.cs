using System;
using System.IO;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.OpenCV.Tests;

[TestClass]
public sealed class OfficialModelIntegrationTests
{
    [TestMethod]
    [TestCategory("ExternalModels")]
    public void YoloV8DetectionRunsThroughOpenCvDnnCpu()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_OPENCV_RUN_EXTERNAL=1 to run the exact local YOLOv8n OpenCV DNN probe.");
        }

        string path = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_MODEL") ?? @"E:\Model\yolo\yolov8\yolov8n.onnx";
        if (!File.Exists(path)) Assert.Inconclusive("The configured YOLOv8n model does not exist: " + path);

        var modelId = new ModelId("yolo/v8/detect/n");
        const string sha256 = "50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4";
        var contract = new OpenCvDnnModelContract(
            modelId,
            new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 640, 640)) },
            new[] { new TensorDescriptor("output0", TensorElementType.Float32, new TensorShape(1, 84, 8400)) });

        using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
        using IInferenceSession session = provider.CreateSession(
            new ModelArtifact(modelId, "onnx", path, sha256),
            new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
            SessionOptions.Default);
        var input = new Tensor<float>(new TensorShape(1, 3, 640, 640), new float[3 * 640 * 640]);
        InferenceOutputs outputs = session.Run(new InferenceInputs(new[] { new NamedTensor("images", input) }), CancellationToken.None);
        Assert.AreEqual(1, outputs.Count);
        Assert.AreEqual(84 * 8400, outputs[0].Tensor.Length);
        Console.WriteLine("OPENCV_EXTERNAL model=yolo/v8/detect/n;output=" + outputs[0].Tensor.Length);
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void YoloV8ClassificationRunsThroughOpenCvDnnCpu()
    {
        RequireExternal();
        string path = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_CLS_MODEL") ?? @"E:\Model\yolo\yolov8\yolov8s-cls.onnx";
        if (!File.Exists(path)) Assert.Inconclusive("The configured YOLOv8s classification model does not exist: " + path);

        var modelId = new ModelId("yolo/v8/classify/s");
        const string sha256 = "6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee";
        var contract = new OpenCvDnnModelContract(
            modelId,
            new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 224, 224)) },
            new[] { new TensorDescriptor("output0", TensorElementType.Float32, new TensorShape(1, 1000)) });
        using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
        using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, sha256), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
        var input = new Tensor<float>(new TensorShape(1, 3, 224, 224), new float[3 * 224 * 224]);
        InferenceOutputs outputs = session.Run(new InferenceInputs(new[] { new NamedTensor("images", input) }), CancellationToken.None);
        Assert.AreEqual(1, outputs.Count);
        Assert.AreEqual(1000, outputs[0].Tensor.Length);
        Console.WriteLine("OPENCV_EXTERNAL model=yolo/v8/classify/s;output=" + outputs[0].Tensor.Length);
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void YoloV8SegmentationRunsThroughOpenCvDnnCpu()
    {
        RequireExternal();
        string path = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_SEG_MODEL") ?? @"E:\Model\yolo\yolov8\yolov8n-seg.onnx";
        if (!File.Exists(path)) Assert.Inconclusive("The configured YOLOv8n segmentation model does not exist: " + path);
        RunStaticModel(
            new ModelId("yolo/v8/segment/n"),
            "986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2",
            path,
            new TensorShape(1, 3, 640, 640),
            new[] { new TensorDescriptor("output0", TensorElementType.Float32, new TensorShape(1, 116, 8400)), new TensorDescriptor("output1", TensorElementType.Float32, new TensorShape(1, 32, 160, 160)) });
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void YoloV8PoseRunsThroughOpenCvDnnCpu()
    {
        RequireExternal();
        string path = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_POSE_MODEL") ?? @"E:\Model\yolo\yolov8\yolov8s-pose.onnx";
        if (!File.Exists(path)) Assert.Inconclusive("The configured YOLOv8s pose model does not exist: " + path);
        RunStaticModel(
            new ModelId("yolo/v8/pose/s"),
            "253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b",
            path,
            new TensorShape(1, 3, 640, 640),
            new[] { new TensorDescriptor("output0", TensorElementType.Float32, new TensorShape(1, 56, 8400)) });
    }

    [TestMethod]
    [TestCategory("ExternalModels")]
    public void YoloV8ObbRunsThroughOpenCvDnnCpu()
    {
        RequireExternal();
        string path = Environment.GetEnvironmentVariable("DEPLOYSHARP_YOLO_OBB_MODEL") ?? @"E:\Model\yolo\yolov8\yolov8s-obb.onnx";
        if (!File.Exists(path)) Assert.Inconclusive("The configured YOLOv8s OBB model does not exist: " + path);
        RunStaticModel(
            new ModelId("yolo/v8/obb/s"),
            "2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981",
            path,
            new TensorShape(1, 3, 1024, 1024),
            new[] { new TensorDescriptor("output0", TensorElementType.Float32, new TensorShape(1, 20, 21504)) });
    }

    private static void RunStaticModel(ModelId modelId, string sha256, string path, TensorShape inputShape, TensorDescriptor[] outputs)
    {
        var contract = new OpenCvDnnModelContract(modelId, new[] { new TensorDescriptor("images", TensorElementType.Float32, inputShape) }, outputs);
        using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
        using IInferenceSession session = provider.CreateSession(new ModelArtifact(modelId, "onnx", path, sha256), new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"), SessionOptions.Default);
        int inputLength = checked((int)inputShape.GetElementCount());
        var input = new Tensor<float>(inputShape, new float[inputLength]);
        InferenceOutputs result = session.Run(new InferenceInputs(new[] { new NamedTensor("images", input) }), CancellationToken.None);
        Assert.AreEqual(outputs.Length, result.Count);
        for (int index = 0; index < outputs.Length; index++) Assert.AreEqual(outputs[index].Shape.GetElementCount(), result[index].Tensor.Length, modelId.Value + " output contract mismatch at index " + index);
        Console.WriteLine("OPENCV_EXTERNAL model=" + modelId.Value + ";outputs=" + result.Count);
    }

    private static void RequireExternal()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set DEPLOYSHARP_OPENCV_RUN_EXTERNAL=1 to run exact local OpenCV DNN probes.");
        }
    }
}
