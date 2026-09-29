using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.OpenCV.Tests;

// Official examples are a small regression set, not a dataset-level accuracy claim.
[TestClass]
[DoNotParallelize]
public sealed class PaddleDocumentOfficialExampleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("ort", false)]
    [DataRow("ort", true)]
    [DataRow("openvino", false)]
    [DataRow("openvino", true)]
    [DataRow("opencv", false)]
    [DataRow("opencv", true)]
    [TestCategory("ExternalModels")]
    public void ClassificationMatchesOfficialExampleAndPreservesProbabilities(string backend, bool table)
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ACCURACY") != "1")
            Assert.Inconclusive("Set DEPLOYSHARP_PADDLE_DOCUMENT_ACCURACY=1 and acquire the official examples using Acquire-PaddleDocumentValidation.ps1.");
        string root = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLE_DOCUMENT_ROOT") ?? @"E:\Model\PaddleDocument";
        string imageName = table ? "table_recognition.jpg" : "img_rot180_demo.jpg";
        string imagePath = Path.Combine(root, "validation", imageName);
        string id = table ? "paddle-table/pp-lcnet-x1-0-table-cls" : "paddle-doc/pp-lcnet-x1-0-doc-ori";
        var descriptor = PaddleDocumentModelCatalog.Get(id);
        string modelPath = Path.Combine(root, "onnx", id.Substring(id.IndexOf('/') + 1) + ".onnx");
        Assert.IsTrue(File.Exists(imagePath), imagePath);
        Assert.IsTrue(File.Exists(modelPath), modelPath);
        string imageHash = Sha256(imagePath);
        Assert.AreEqual(table ? "acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c" : "c5a77e031470e13878ff4f28a06ca843fd455d95c20b1b49b486681e346209ed", imageHash);
        var document = PaddleDocumentProfiles.CreateClassification(descriptor,
            table ? new[] { "wired_table", "wireless_table" } : PaddleDocumentProfiles.DocumentOrientationLabels,
            table ? VisualTaskId.TableClassification : VisualTaskId.DocumentOrientation);
        var profile = document.VisualProfile;
        using var registry = new BackendRegistry();
        BackendId backendId;
        switch (backend)
        {
            case "ort": registry.UseOnnxRuntime(); backendId = OnnxRuntimeBackendProvider.BackendId; break;
            case "openvino": registry.UseOpenVino(); backendId = OpenVinoBackendProvider.BackendId; break;
            case "opencv":
                var contract = new OpenCvDnnModelContract(profile.ModelId,
                    new[] { new TensorDescriptor("x", TensorElementType.Float32, new TensorShape(1, 3, 224, 224)) },
                    new[] { new TensorDescriptor("fetch_name_0", TensorElementType.Float32, new TensorShape(1, table ? 2 : 4)) }, imageInputNames: new[] { "x" });
                registry.UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion: true, enableWinograd: true, specializeDynamicInputShapes: true));
                backendId = OpenCvDnnBackendProvider.BackendId; break;
            default: throw new ArgumentException(backend);
        }
        using var session = registry.CreateSession(document.CreateArtifact(modelPath, backendId), new BackendRequest(BackendCapabilities.TensorInference, backendId, "cpu"));
        var factory = new OpenCvVisualInputFactory();
        using var decoded = factory.DecodeForRois(OpenCvImageSource.FromFile(imagePath));
        var rectangle = new RectangleRoiGeometry(new RectangleF(0, 0, decoded.SourceSize.Width, decoded.SourceSize.Height));
        var options = profile.Preprocessing!.ToOpenCvOptions();
        const int warmup = 5, measured = 50;
        var samples = new List<object>();
        var total = new List<double>();
        var pre = new List<double>();
        var infer = new List<double>();
        var post = new List<double>();
        float score = 0;
        for (int iteration = -warmup; iteration < measured; iteration++)
        {
            var watch = Stopwatch.StartNew();
            using var input = decoded.PrepareRectangle(rectangle, "x", options);
            double preMs = watch.Elapsed.TotalMilliseconds;
            var outputs = session.Run(new InferenceInputs(new[] { new NamedTensor("x", input.Tensor) }), CancellationToken.None);
            double inferEnd = watch.Elapsed.TotalMilliseconds;
            var result = (ClassificationResult)profile.Decoder.Decode(new VisualDecodeContext(input, profile, outputs, CancellationToken.None));
            double end = watch.Elapsed.TotalMilliseconds;
            int expectedClass = table ? 0 : 2;
            Assert.AreEqual(expectedClass, result.TopPrediction!.Index, "Official example label mismatch.");
            score = result.TopPrediction.Score;
            var probabilities = (float[])outputs[0].Tensor.Buffer;
            Assert.AreEqual(probabilities[expectedClass], score, 1e-6f, "Decoder must preserve Softmax probabilities.");
            Assert.AreEqual(table ? .84421f : .88164f, score, .025f, "Regression against the official example score (export/runtime tolerances). ");
            if (iteration >= 0)
            {
                pre.Add(preMs); infer.Add(inferEnd - preMs); post.Add(end - inferEnd); total.Add(end);
                samples.Add(new { preprocessingMs = preMs, inferenceMs = inferEnd - preMs, postprocessingMs = end - inferEnd, totalMs = end });
            }
        }
        string reportDirectory = Path.Combine(TestContext.TestResultsDirectory!, "paddle-document");
        Directory.CreateDirectory(reportDirectory);
        string report = Path.Combine(reportDirectory, backend + "-" + (table ? "table-cls" : "doc-ori") + ".json");
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            schema = 1, model = id, backend, device = "cpu", machine = Environment.MachineName,
            os = Environment.OSVersion.ToString(), runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            processors = Environment.ProcessorCount, timestampUtc = DateTimeOffset.UtcNow,
            tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime-default",
#if DEBUG
            buildConfiguration = "Debug",
#else
            buildConfiguration = "Release",
#endif
            sourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_SOURCE_REVISION") ?? "unrecorded",
            modelSha256 = Sha256(modelPath), image = imageName, imageSha256 = imageHash, expectedClass = table ? 0 : 2, score,
            warmup, iterations = measured, batch = 1, sessions = 1,
            protocol = "Decoded image reused; CPU preprocessing + synchronous session.Run including transfers + decoder; excludes decode, load, hashing and assertions. Nearest-rank percentiles. Official example regression only.",
            preprocessing = Percentiles(pre), inference = Percentiles(infer), postprocessing = Percentiles(post), total = Percentiles(total), samples
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddResultFile(report);
        Console.WriteLine(File.ReadAllText(report).Split("\"samples\"")[0]);
    }

    private static object Percentiles(List<double> values)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        return new { p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1] };
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
