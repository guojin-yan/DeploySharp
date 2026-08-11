using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static int Main()
    {
        string? imagePath = RequiredFile("DEPLOYSHARP_DETR_IMAGE");
        string? detectionPath = RequiredFile("DEPLOYSHARP_DETR_RF_DET_MODEL");
        string? segmentationPath = RequiredFile("DEPLOYSHARP_DETR_RF_SEG_MODEL");
        if (imagePath == null || detectionPath == null || segmentationPath == null) return 2;

        string detection = Run(CreateDetection(), detectionPath, imagePath);
        string segmentation = Run(CreateSegmentation(), segmentationPath, imagePath);
        Console.WriteLine(detection);
        Console.WriteLine(segmentation);
        Console.WriteLine("DEPLOYSHARP_VISUAL_DETR_CONSUMER_OK");
        return 0;
    }

    private static string Run(PortableDetectorProfile profile, string modelPath, string imagePath)
    {
        ModelArtifact artifact = profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId);
        using var backends = new BackendRegistry();
        backends.UseOnnxRuntime();
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, profile.VisualProfile.Task), request);
        using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), imagePath, profile);
        VisualInferenceResult result = pipeline.Run(input);
        if (result.Value is DetectionResult detection)
        {
            return "detection:backend=" + result.BackendId + ";detections=" + detection.Detections.Count.ToString(CultureInfo.InvariantCulture);
        }
        if (result.Value is InstanceSegmentationResult segmentation)
        {
            return "segmentation:backend=" + result.BackendId + ";instances=" + segmentation.Instances.Count.ToString(CultureInfo.InvariantCulture);
        }
        throw new InvalidOperationException("The portable detector pipeline returned an unexpected result type.");
    }

    private static PortableDetectorProfile CreateDetection()
    {
        var options = Options(
            17,
            new VisualSize(512, 512),
            Labels(5),
            "b464822e768f5795f249a6bd08cf1c5299787806c740204ed8e46d3a369ab769",
            300,
            null);
        return PortableDetectorProfiles.CreateRFDETR(new ModelId("external/rf-detr-detect"), options);
    }

    private static PortableDetectorProfile CreateSegmentation()
    {
        var options = Options(
            17,
            new VisualSize(432, 432),
            Labels(90),
            "6156aaff01ea0da0a007b29157fa34bf512d99d9e6a872cad70ae28cd08d6a35",
            200,
            "4245");
        return PortableDetectorProfiles.CreateRFDETRSeg(new ModelId("external/rf-detr-segment"), options);
    }

    private static PortableDetectorProfileOptions Options(int opset, VisualSize size, IEnumerable<string> labels, string sha256, int queryCount, string? masksOutputName)
    {
        return new PortableDetectorProfileOptions(
            opset,
            size,
            labels,
            inputName: "input",
            artifactSha256: sha256,
            upstreamRepository: "https://github.com/roboflow/rf-detr",
            upstreamCommit: "cc538cea510c24d6d7bc64332f0bf29875a5b2d6",
            exporterVersion: "opset17-local-artifact",
            license: "External",
            scoreThreshold: .4f,
            maximumCandidates: 3000,
            maximumResults: 100,
            topK: 300,
            masksOutputName: masksOutputName,
            rfDetrQueryCount: queryCount,
            rfDetrIncludesNoObjectClass: true);
    }

    private static IEnumerable<string> Labels(int count)
    {
        return Enumerable.Range(0, count).Select(index => "class" + index.ToString(CultureInfo.InvariantCulture));
    }

    private static string? RequiredFile(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value)) return Path.GetFullPath(value);
        Console.Error.WriteLine(variable + " must point to an audited external file.");
        return null;
    }
}
