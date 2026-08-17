using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.ModelFactory;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private static async Task<int> Main()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_DETECTOR_RELEASE_SMOKE"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("DEPLOYSHARP_DETECTOR_RELEASE_SMOKE_SKIP disabled");
            return 0;
        }

        string scope = (Environment.GetEnvironmentVariable("DEPLOYSHARP_DETECTOR_RELEASE_SMOKE_SCOPE") ?? "all").Trim();
        if (!string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && !string.Equals(scope, "detr", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DEPLOYSHARP_DETECTOR_RELEASE_SMOKE_SCOPE must be 'all' or 'detr'.");
        }

        string? configuredCache = Environment.GetEnvironmentVariable("DEPLOYSHARP_MODEL_CACHE");
        bool ownsCache = string.IsNullOrWhiteSpace(configuredCache);
        string cacheRoot = ownsCache
            ? Path.Combine(Path.GetTempPath(), "deploysharp-detector-release-smoke-" + Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(configuredCache!);
        string imagePath = Path.Combine(AppContext.BaseDirectory, "assets", "rgb.png");
        if (!File.Exists(imagePath)) throw new InvalidOperationException("The deterministic RGB smoke-test image was not copied to the output directory.");

        try
        {
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            using var factory = new ModelFactoryClient(catalog, new ModelFactoryOptions(cacheRoot, requestTimeout: TimeSpan.FromMinutes(15), maximumConcurrentDownloads: 2));
            var summaries = new List<string>();
            if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
            {
                MaterializedModel yoloDetect = await MaterializeAsync(factory, "yolo/v8/detect/n", "onnxruntime", "onnx").ConfigureAwait(false);
                MaterializedModel yoloClassification = await MaterializeAsync(factory, "yolo/v8/classify/s", "onnxruntime", "onnx").ConfigureAwait(false);
                MaterializedModel yoloSegmentation = await MaterializeAsync(factory, "yolo/v8/segment/n", "onnxruntime", "onnx").ConfigureAwait(false);
                MaterializedModel yoloPose = await MaterializeAsync(factory, "yolo/v8/pose/s", "onnxruntime", "onnx").ConfigureAwait(false);
                MaterializedModel yoloObb = await MaterializeAsync(factory, "yolo/v8/obb/s", "onnxruntime", "onnx").ConfigureAwait(false);
                summaries.Add(RunYoloDetection(yoloDetect, imagePath));
                summaries.Add(RunYoloMultiTask(yoloClassification, imagePath, CreateYoloClassification()));
                summaries.Add(RunYoloMultiTask(yoloSegmentation, imagePath, CreateYoloSegmentation()));
                summaries.Add(RunYoloMultiTask(yoloPose, imagePath, CreateYoloPose()));
                summaries.Add(RunYoloMultiTask(yoloObb, imagePath, CreateYoloObb()));
            }

            MaterializedModel detrDecoded = await MaterializeAsync(factory, "rt-detr/r50vd-decoded-vector-onnx", "onnxruntime", "onnx").ConfigureAwait(false);
            MaterializedModel detrRaw = await MaterializeAsync(factory, "rt-detr/r50vd-raw-query", "onnxruntime", "onnx").ConfigureAwait(false);
            MaterializedModel detrIr = await MaterializeAsync(factory, "rt-detr/r50vd-decoded-vector-ir", "openvino", "openvino-ir").ConfigureAwait(false);
            summaries.Add(RunPortableDetector(detrDecoded, imagePath, CreateRtDetrDecoded(false), OnnxRuntimeBackendProvider.BackendId, "cpu"));
            summaries.Add(RunPortableDetector(detrRaw, imagePath, CreateRtDetrRaw(), OnnxRuntimeBackendProvider.BackendId, "cpu"));
            summaries.Add(RunPortableDetector(detrIr, imagePath, CreateRtDetrDecoded(true), OpenVinoBackendProvider.BackendId, "CPU"));

            Console.WriteLine(string.Join(Environment.NewLine, summaries));
            Console.WriteLine("DEPLOYSHARP_DETECTOR_RELEASE_SMOKE_OK scope=" + scope.ToLowerInvariant() + ";cases=" + summaries.Count.ToString(CultureInfo.InvariantCulture));
            return 0;
        }
        finally
        {
            if (ownsCache && Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        }
    }

    private static async Task<MaterializedModel> MaterializeAsync(ModelFactoryClient factory, string modelId, string backend, string format)
    {
        ModelSelection selection = factory.Select(new ModelQuery(modelId: modelId, backend: backend, format: format, includePreview: true));
        MaterializedModel model = await factory.GetModelAsync(selection).ConfigureAwait(false);
        if (!await factory.VerifyModelCacheAsync(selection).ConfigureAwait(false)) throw new InvalidOperationException("ModelFactory cache verification failed for " + modelId + ".");
        return model;
    }

    private static string RunYoloDetection(MaterializedModel materialized, string imagePath)
    {
        YoloDetectionProfile profile = YoloDetectionProfiles.Create(
            YoloDetectionFamily.YoloV8,
            new ModelId("yolo/v8/detect/n"),
            "50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4",
            YoloLabelSets.Coco80,
            "1367566337fb8056223a1aeb469360747f1b1bcd",
            "8.3.78",
            new YoloDetectionProfileOptions(19));
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(profile));
        return RunVisual("yolo/v8/detect/n", materialized, profile.CreateArtifact(ModelPath(materialized), OnnxRuntimeBackendProvider.BackendId), profile.VisualProfile, input, OnnxRuntimeBackendProvider.BackendId, "cpu");
    }

    private static string RunYoloMultiTask(MaterializedModel materialized, string imagePath, YoloMultiTaskProfile profile)
    {
        using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile.VisualProfile.Input.Name, OpenCvYoloPreprocessing.CreateOptions(profile));
        return RunVisual(materialized.Selection.Entry.ModelId!, materialized, profile.CreateArtifact(ModelPath(materialized), OnnxRuntimeBackendProvider.BackendId), profile.VisualProfile, input, OnnxRuntimeBackendProvider.BackendId, "cpu");
    }

    private static string RunPortableDetector(MaterializedModel materialized, string imagePath, PortableDetectorProfile profile, BackendId backendId, string device)
    {
        using PreparedVisualInput input = OpenCvPortableDetectorPreprocessing.CreateFromFile(new OpenCvVisualInputFactory(), imagePath, profile);
        return RunVisual(materialized.Selection.Entry.ModelId!, materialized, profile.CreateArtifact(ModelPath(materialized), backendId), profile.VisualProfile, input, backendId, device);
    }

    private static string RunVisual(string modelId, MaterializedModel materialized, ModelArtifact artifact, VisualModelProfile profile, PreparedVisualInput input, BackendId backendId, string device)
    {
        using var registry = new BackendRegistry();
        if (backendId == OnnxRuntimeBackendProvider.BackendId) registry.UseOnnxRuntime(); else registry.UseOpenVino();
        var profiles = new VisualProfileRegistry();
        profiles.Register(profile);
        profiles.Freeze();
        var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
        using (IInferenceSession inspected = registry.CreateSession(artifact, request))
        {
            Console.WriteLine(modelId + ":metadata-inputs=" + DescribeMetadata(inspected.Metadata.Inputs) + ";outputs=" + DescribeMetadata(inspected.Metadata.Outputs));
        }
        using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, profile.Task), request);
        VisualInferenceResult result = pipeline.Run(input);
        if (result.Value == null || result.BackendId != backendId || result.Task != profile.Task) throw new InvalidOperationException("The release smoke inference did not produce the expected pipeline result for " + modelId + ".");
        return modelId + ":backend=" + result.BackendId + ";format=" + materialized.Selection.Artifact.Format + ";result=" + result.Value.GetType().Name;
    }

    private static string DescribeMetadata(IEnumerable<TensorDescriptor> values) => string.Join(",", values.Select(value => value.Name + ":" + value.ElementType + ":" + value.Shape));

    private static string ModelPath(MaterializedModel materialized)
    {
        IEnumerable<ModelCatalogAsset> models = materialized.Selection.Artifact.Assets.Where(asset => asset.Kind == ModelCatalogAssetKind.Model);
        ModelCatalogAsset model = string.Equals(materialized.Selection.Artifact.Format, "openvino-ir", StringComparison.OrdinalIgnoreCase)
            ? models.Single(asset => asset.RelativePath!.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            : models.Single();
        return Path.Combine(materialized.PackageRoot, model.RelativePath!.Replace('/', Path.DirectorySeparatorChar));
    }

    private static YoloMultiTaskProfile CreateYoloClassification() => YoloMultiTaskProfiles.CreateClassification(new ModelId("yolo/v8/classify/s"), "6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee", Enumerable.Range(0, 1000).Select(index => "class" + index.ToString(CultureInfo.InvariantCulture)), "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6", new YoloClassificationProfileOptions(17, new VisualSize(224, 224), topK: 5));

    private static YoloMultiTaskProfile CreateYoloSegmentation() => YoloMultiTaskProfiles.CreateInstanceSegmentation(YoloDetectionFamily.YoloV8, new ModelId("yolo/v8/segment/n"), "986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2", YoloLabelSets.Coco80, "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.0.119", new YoloPackedProfileOptions(12, 8400, new VisualSize(640, 640), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 8400)));

    private static YoloMultiTaskProfile CreateYoloPose() => YoloMultiTaskProfiles.CreatePose(YoloDetectionFamily.YoloV8, new ModelId("yolo/v8/pose/s"), "253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b", "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6", new YoloPackedProfileOptions(17, 8400, new VisualSize(640, 640), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 8400)));

    private static YoloMultiTaskProfile CreateYoloObb() => YoloMultiTaskProfiles.CreateObb(YoloDetectionFamily.YoloV8, new ModelId("yolo/v8/obb/s"), "2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981", YoloLabelSets.Dota15, "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6", new YoloPackedProfileOptions(17, 21504, new VisualSize(1024, 1024), decoderOptions: new YoloPackedDecoderOptions(maximumCandidates: 21504)));

    private static PortableDetectorProfile CreateRtDetrDecoded(bool openVino)
    {
        var options = new PortableDetectorProfileOptions(16, new VisualSize(640, 640), YoloLabelSets.Coco80, modelFormat: openVino ? "openvino-ir" : "onnx", inputName: "image", artifactSha256: openVino ? "9d49703964c07567de7f00bda85bae1760da322e2b0655bfae110f2c222c778d" : "a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db", upstreamRepository: "https://github.com/PaddlePaddle/PaddleDetection", upstreamCommit: "b25522a0f4bde8c80603f3ba5e3472059972e3b5", exporterVersion: openVino ? "OpenVINO IR" : "PaddleDetection-export_model+paddle2onnx", license: "Apache-2.0", scoreThreshold: .4f, boxesOutputName: "save_infer_model/scale_0.tmp_0", countOutputName: "save_infer_model/scale_1.tmp_0", hasDynamicBatchAxis: !openVino, paddleCountShape: PortableDetectorCountShape.BatchVector);
        return PortableDetectorProfiles.CreateRTDETR(new ModelId(openVino ? "rt-detr/r50vd-decoded-vector-ir" : "rt-detr/r50vd-decoded-vector-onnx"), options);
    }

    private static PortableDetectorProfile CreateRtDetrRaw()
    {
        var options = new PortableDetectorProfileOptions(16, new VisualSize(640, 640), YoloLabelSets.Coco80, inputName: "image", artifactSha256: "544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad", upstreamRepository: "https://github.com/PaddlePaddle/PaddleDetection", upstreamCommit: "b25522a0f4bde8c80603f3ba5e3472059972e3b5", exporterVersion: "PaddleDetection-export_model+paddle2onnx", license: "Apache-2.0", scoreThreshold: .4f, maximumCandidates: 300, maximumResults: 100, topK: 300, boxesOutputName: "stack_7.tmp_0_slice_0", labelsOutputName: "stack_8.tmp_0_slice_0", rfDetrQueryCount: 300, hasDynamicBatchAxis: true);
        return PortableDetectorProfiles.CreateRTDETRRaw(new ModelId("rt-detr/r50vd-raw-query"), options);
    }
}
