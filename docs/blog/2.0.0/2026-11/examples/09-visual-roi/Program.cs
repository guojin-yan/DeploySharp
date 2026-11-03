using System.Runtime.InteropServices;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Configuration.Json;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;

string Option(string key, string fallback)
{
    int index = Array.IndexOf(args, key);
    return index < 0 ? fallback : index + 1 < args.Length
        ? args[index + 1] : throw new ArgumentException("Missing value: " + key);
}
string model = Path.GetFullPath(Option("--model", "models/yolo26n.onnx"));
string backend = Option("--backend", "ort-cpu");
string output = Path.GetFullPath(Option("--output", "results/" + backend));
string images = Path.GetFullPath(Option("--images", "demo-inputs"));
string configs = Path.GetFullPath(Option("--configs", "configs"));
int threads = int.Parse(Option("--threads", "2"));
int sessions = int.Parse(Option("--sessions", "1"));
int trtApi = int.Parse(Option("--trt-api", OperatingSystem.IsWindows() ? "10" : "11"));
if (threads is < 1 or > 128 || sessions is < 1 or > 8 || trtApi is not (10 or 11))
    throw new ArgumentException("Use threads 1..128, sessions 1..8, trt-api 10 or 11.");
if (!File.Exists(model)) throw new FileNotFoundException("Download the ONNX model first.", model);
string[] files = Directory.GetFiles(images, "*.jpg").Order(StringComparer.Ordinal).ToArray();
if (files.Length == 0) throw new InvalidDataException("The image folder has no JPG files.");
Directory.CreateDirectory(output);
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var token = stop.Token;

var id = new ModelId("blog/yolo26-roi");
using var backends = new BackendRegistry();
BackendId backendId;
string device, format = "onnx", artifact = model;
if (backend == "ort-cpu")
{
    backends.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: threads, interOpThreads: 1));
    backendId = OnnxRuntimeBackendProvider.BackendId; device = "cpu";
}
else if (backend == "ort-cuda")
{
    backends.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: threads, interOpThreads: 1,
        executionProvider: OnnxRuntimeExecutionProvider.Cuda,
        cudaProviderOptions: new Dictionary<string, string> { ["use_tf32"] = "0" }));
    backendId = OnnxRuntimeBackendProvider.BackendId; device = "cuda";
}
else if (backend == "openvino")
{
    backends.UseOpenVino(new OpenVinoOptions(device: "CPU", performanceHint: OpenVinoPerformanceHint.Latency,
        streams: 1, inferenceThreads: threads, requestCount: 1));
    backendId = OpenVinoBackendProvider.BackendId; device = "CPU";
}
else if (backend == "opencv")
{
    backends.UseOpenCvDnn(new OpenCvDnnOptions(new OpenCvDnnModelContract(id,
        new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 640, 640)) },
        new[] { new TensorDescriptor("output0", TensorElementType.Float32, new TensorShape(1, 300, 6)) }), numThreads: threads));
    backendId = OpenCvDnnBackendProvider.BackendId; device = "cpu";
}
else if (backend == "tensorrt")
{
    string engine = Path.GetFullPath(Option("--engine", Path.ChangeExtension(model,
        $".{(OperatingSystem.IsWindows() ? "win" : "linux")}-trt{trtApi}-fp32-b1.engine")));
    var api = (TensorRtApiVersion)trtApi;
    if (!File.Exists(engine))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(engine)!);
        Console.WriteLine("Building local FP32 engine: " + engine);
        new TensorRtOnnxEngineBuilder().Build(new ModelArtifact(id, "onnx", model), engine,
            new TensorRtOnnxEngineBuildOptions(apiVersion: api,
                precision: trtApi == 10 ? TensorRtOnnxEnginePrecision.Float32 : TensorRtOnnxEnginePrecision.RuntimeDefault,
                disableTf32: true, optimizationLevel: 3));
    }
    backends.UseTensorRT(new TensorRtBackendOptions(api));
    backendId = TensorRtBackendProvider.BackendId; device = "cuda";
    format = "tensorrt-engine"; artifact = engine;
}
else throw new ArgumentException("Use --backend ort-cpu, ort-cuda, openvino, opencv or tensorrt.");

var size = new VisualSize(640, 640);
var preprocessing = new VisualPreprocessingOptions(size, VisualResizeMode.Letterbox, VisualColorOrder.Rgb,
    VisualNormalizationOptions.Scale(255f), paddingColor: new VisualRgbColor(114, 114, 114));
var profile = new VisualModelProfile("blog.yolo26.roi", id, VisualTaskId.ObjectDetection, "1", format,
    new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(1, 3, 640, 640), VisualTensorLayout.Nchw),
    new[] { new VisualOutputBinding("output0", TensorElementType.Float32, new TensorShape(1, 300, 6)) },
    YoloLabelSets.Coco80.Select((s, i) => new VisualLabel(i, s)),
    new YoloDetectionDecoder(new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.EndToEnd, 80),
        new YoloDetectionDecoderOptions(scoreThreshold: .25f)), preprocessing: preprocessing);
var profiles = new VisualProfileRegistry(); profiles.Register(profile); profiles.Freeze();
var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
var selection = profiles.Select(new ModelArtifact(id, format, artifact), backends, request, VisualTaskId.ObjectDetection);
using var pipeline = new VisualPipeline(backends, selection, request, new SessionOptions(maxConcurrency: sessions));
var factory = new OpenCvVisualInputFactory();
var runner = new VisualRoiRunner(pipeline);
var store = new RoiConfigurationStore(args.Contains("--rebind-normalized"));
var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

do
{
    var reports = new List<object>();
    foreach (string file in files)
    {
        token.ThrowIfCancellationRequested();
        string name = Path.GetFileNameWithoutExtension(file);
        string camera = name.Split('-')[0];
        string directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
        using var decoded = factory.DecodeForRois(OpenCvImageSource.FromFile(file), token);
        // Capture exactly one immutable configuration for all work on this image.
        VisualRoiSnapshot snapshot = store.Capture(Path.Combine(configs, camera + ".json"), decoded.SourceSize);
        var filterSnapshot = new VisualRoiSnapshot(snapshot.SourceSize,
            snapshot.Rois.Where(r => r.ExecutionMode == RoiExecutionMode.FilterResults), snapshot.Version);
        var cropSnapshot = new VisualRoiSnapshot(snapshot.SourceSize,
            snapshot.Rois.Where(r => r.ExecutionMode == RoiExecutionMode.CropAndInfer), snapshot.Version);
        if (cropSnapshot.Rois.Count == 0) throw new InvalidDataException("Configure at least one CropAndInfer ROI.");

        var whole = new RectangleRoiGeometry(new RectangleF(0, 0, decoded.SourceSize.Width, decoded.SourceSize.Height));
        DetectionResult full;
        using (var input = decoded.PrepareRectangle(whole, "images", preprocessing.ToOpenCvOptions(), token))
            full = (DetectionResult)pipeline.Run(input).Value;
        RoiDetectionResult filtered = VisualRoiDetectionFilter.Filter(full, filterSnapshot, VisualTaskId.ObjectDetection);

        RoiInferenceBatchResult batch = await runner.RunCropAsync(cropSnapshot,
            (_, geometry, ct) => Task.FromResult(decoded.Prepare(geometry, "images", preprocessing.ToOpenCvOptions(), ct)),
            new RoiExecutionOptions(maximumRois: 8, prefetch: 2, maximumPreparedPixels: 16_000_000,
                mergeMode: RoiResultMergeMode.ClassAwareNms), cancellationToken: token);
        RoiMergedDetectionResult merged = new RoiDetectionResultMerger().Merge(batch, iouThreshold: .45f);
        RoiDetectionResult cropFiltered = VisualRoiDetectionFilter.Filter(merged.Detections, filterSnapshot, VisualTaskId.ObjectDetection);

        if (batch.Items.Any(i => !i.Succeeded)) throw new InvalidDataException("One or more ROI calls failed.");
        foreach (var detection in full.Detections.Concat(merged.Detections.Detections))
            ValidateBox(detection, decoded.SourceSize);
        var fullRows = full.Detections.Select(d => Row(d)).ToArray();
        var filteredRows = filtered.Detections.Select(d => Row(d.Detection, d.RoiIds)).ToArray();
        var rawCropRows = batch.Items.SelectMany(item => ((DetectionResult)item.Result!.Value).Detections
            .Select(d => Row(d, new[] { item.Roi.Id }))).ToArray();
        var mergedRows = merged.Items.Select(d => Row(d.Detection, d.ContributingRoiIds)).ToArray();
        var finalRows = cropFiltered.Detections.Select(d => Row(d.Detection,
            merged.Items.Single(m => ReferenceEquals(m.Detection, d.Detection)).ContributingRoiIds)).ToArray();
        var item = new { image = Path.GetFileName(file), source = new { width = decoded.SourceSize.Width, height = decoded.SourceSize.Height },
            snapshotVersion = snapshot.Version, full = fullRows, filtered = filteredRows,
            crops = batch.Items.Select(i => new { roiId = i.Roi.Id, succeeded = i.Succeeded,
                detections = ((DetectionResult)i.Result!.Value).Detections.Count }),
            rawCrop = rawCropRows, merged = mergedRows, cropFiltered = finalRows,
            counts = new { full = fullRows.Length, filtered = filteredRows.Length, cropCalls = batch.Items.Count,
                cropRaw = rawCropRows.Length, cropMerged = mergedRows.Length, cropFiltered = finalRows.Length } };
        File.WriteAllText(Path.Combine(directory, "detections.json"), JsonSerializer.Serialize(item, jsonOptions));
        File.WriteAllText(Path.Combine(directory, "roi.snapshot.json"), VisualRoiJsonSerializer.Serialize(snapshot));
        RoiDrawing.Save(file, Path.Combine(directory, "01-regions.jpg"), snapshot, Array.Empty<DetectionRow>());
        RoiDrawing.Save(file, Path.Combine(directory, "02-full.jpg"), filterSnapshot, fullRows);
        RoiDrawing.Save(file, Path.Combine(directory, "03-filtered.jpg"), filterSnapshot, filteredRows);
        RoiDrawing.Save(file, Path.Combine(directory, "04-crop-raw.jpg"), cropSnapshot, rawCropRows);
        RoiDrawing.Save(file, Path.Combine(directory, "04-crop-merged.jpg"), cropSnapshot, mergedRows);
        RoiDrawing.Save(file, Path.Combine(directory, "05-crop-filtered.jpg"), filterSnapshot, finalRows);
        reports.Add(item);
        Console.WriteLine($"{name}: full={fullRows.Length}, filtered={filteredRows.Length}, " +
            $"crop={rawCropRows.Length}->{mergedRows.Length}, crop_filtered={finalRows.Length}, snapshot={snapshot.Version}");
    }
    File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new {
        createdAt = DateTimeOffset.Now, backend, device, model = Path.GetFileName(model), sessions, threads,
        preprocessing = "640x640 Letterbox / RGB / divide 255 / padding 114", scoreThreshold = .25,
        merge = "ClassAwareNms; IoU > 0.45", modelBatch = 1, prefetch = 2,
        environment = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            runtime = RuntimeInformation.FrameworkDescription, logicalProcessors = Environment.ProcessorCount }, images = reports }, jsonOptions));
    Console.WriteLine("Saved: " + output);
    if (!args.Contains("--watch") || token.IsCancellationRequested) break;
    Console.WriteLine("Edit a config, then press Enter to run again; q to quit.");
    string? line = Console.ReadLine();
    if (line == null || line.Equals("q", StringComparison.OrdinalIgnoreCase)) break;
} while (!token.IsCancellationRequested);

static DetectionRow Row(Detection d, IEnumerable<string>? roiIds = null) => new(d.Label.Index, d.Label.Label,
    d.Label.Score, d.Box.X, d.Box.Y, d.Box.Width, d.Box.Height, (roiIds ?? Array.Empty<string>()).ToArray());

static void ValidateBox(Detection detection, VisualSize source)
{
    RectangleF b = detection.Box;
    if (!float.IsFinite(b.X + b.Y + b.Width + b.Height) || b.Width < 0 || b.Height < 0 ||
        b.X < -.01f || b.Y < -.01f || b.Right > source.Width + .01f || b.Bottom > source.Height + .01f)
        throw new InvalidDataException("A decoded box is outside source coordinates.");
}

internal sealed record DetectionRow(int ClassId, string Label, float Score, float X, float Y, float Width, float Height, string[] RoiIds);
