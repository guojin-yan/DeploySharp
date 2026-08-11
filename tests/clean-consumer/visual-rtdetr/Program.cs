using System;
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
        string? modelPath = ExternalFile("DEPLOYSHARP_RTDETR_ONNX");
        string? imagePath = ExternalFile("DEPLOYSHARP_RTDETR_IMAGE");
        if (modelPath == null || imagePath == null)
        {
            Console.WriteLine("DEPLOYSHARP_VISUAL_RTDETR_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        PortableDetectorProfile profile = CreateProfile();
        ModelArtifact artifact = profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId);
        using var backends = new BackendRegistry();
        backends.UseOnnxRuntime();
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile.VisualProfile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu");
        using var pipeline = new VisualPipeline(backends, profiles.Select(artifact, backends, request, profile.VisualProfile.Task), request);
        using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), imagePath, profile);
        VisualInferenceResult inference = pipeline.Run(input);
        if (inference.Value is not DetectionResult result) throw new InvalidOperationException("The RT-DETR pipeline returned an unexpected result type.");

        Console.WriteLine("rtdetr:backend=" + inference.BackendId + ";detections=" + result.Detections.Count.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("DEPLOYSHARP_VISUAL_RTDETR_CONSUMER_OK");
        return 0;
    }

    private static PortableDetectorProfile CreateProfile()
    {
        var options = new PortableDetectorProfileOptions(
            16,
            new VisualSize(640, 640),
            Enumerable.Range(0, 80).Select(index => "coco-" + index.ToString(CultureInfo.InvariantCulture)),
            inputName: "image",
            artifactSha256: "a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db",
            upstreamRepository: "https://github.com/PaddlePaddle/PaddleDetection",
            upstreamCommit: "b25522a0f4bde8c80603f3ba5e3472059972e3b5",
            exporterVersion: "PaddleDetection-export_model+paddle2onnx-local-artifact-unverified",
            license: "External; upstream code Apache-2.0; artifact chain unverified",
            scoreThreshold: .4f,
            maximumCandidates: 300,
            maximumResults: 300,
            topK: 300,
            preprocessingVersion: "paddledetection-rgb-resize-div255-aux-v2",
            postprocessingVersion: "paddledetection-decoded-rows-vector-count-v2",
            boxesOutputName: "save_infer_model/scale_0.tmp_0",
            countOutputName: "save_infer_model/scale_1.tmp_0",
            hasDynamicBatchAxis: true,
            paddleCountShape: PortableDetectorCountShape.BatchVector);
        return PortableDetectorProfiles.CreateRTDETR(new ModelId("external/rt-detr-r50vd-decoded-vector"), options);
    }

    private static string? ExternalFile(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) || !File.Exists(value) ? null : Path.GetFullPath(value);
    }
}
