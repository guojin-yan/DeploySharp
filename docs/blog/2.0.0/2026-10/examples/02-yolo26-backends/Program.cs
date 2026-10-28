using System.Diagnostics;
using System.Text.Json;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.ImgProc;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;

string modelPath = Path.GetFullPath(
    args.Length > 1 ? args[1] : Path.Combine("models", "yolo26n.onnx"));
string imagePath = Path.GetFullPath(
    args.Length > 0 ? args[0] : Path.Combine("images", "bus.jpg"));
IReadOnlyList<string> classNames = args.Length > 2
    ? File.ReadAllLines(args[2])
    : YoloLabelSets.Coco80;
string backend = Environment.GetEnvironmentVariable("DEPLOYSHARP_DEMO_BACKEND") ?? "ort-cpu";
string format = backend == "tensorrt" ? "tensorrt-engine" : "onnx";
string loadPath = backend == "tensorrt" ? Path.ChangeExtension(modelPath, ".win-trt10.engine") : modelPath;
string outputDirectory = Path.GetFullPath(Path.Combine("results", backend));
Directory.CreateDirectory(outputDirectory);
var modelSize = new VisualSize(640, 640);
const string inputName = "images";
const string outputName = "output0";

if (!File.Exists(modelPath))
    throw new FileNotFoundException("找不到 ONNX 模型。", modelPath);
if (!File.Exists(imagePath))
    throw new FileNotFoundException("找不到输入图片。", imagePath);

// 声明 YOLO26 端到端检测模型的输入输出，复用内置解码器。
var modelId = new ModelId("local/yolo26-detect");
var profile = new VisualModelProfile(
    "yolo26.detect.local", modelId, VisualTaskId.ObjectDetection, "1", format,
    new VisualInputBinding(inputName, TensorElementType.Float32,
        new TensorShape(1, 3, modelSize.Height, modelSize.Width), VisualTensorLayout.Nchw),
    new[] { new VisualOutputBinding(outputName, TensorElementType.Float32,
        new TensorShape(1, -1, 6)) },
    classNames.Select((name, index) => new VisualLabel(index, name)),
    new YoloDetectionDecoder(
        new YoloDetectionOutputContract(outputName,
            YoloDetectionOutputKind.EndToEnd, classNames.Count),
        new YoloDetectionDecoderOptions(scoreThreshold: 0.25f)),
    preprocessing: new VisualPreprocessingOptions(
        modelSize, VisualResizeMode.Letterbox, VisualColorOrder.Rgb,
        VisualNormalizationOptions.Scale(255f),
        paddingColor: new VisualRgbColor(114, 114, 114)));

// 选择推理后端，并注册视觉任务配置。
using var backends = new BackendRegistry();
BackendId backendId;
string device;
switch (backend)
{
    case "ort-cpu":
        backends.UseOnnxRuntime();
        backendId = OnnxRuntimeBackendProvider.BackendId; device = "cpu"; break;
    case "ort-cuda":
        backends.UseOnnxRuntime(new OnnxRuntimeOptions(
            executionProvider: OnnxRuntimeExecutionProvider.Cuda,
            cudaProviderOptions: new Dictionary<string, string> { ["use_tf32"] = "0" }));
        backendId = OnnxRuntimeBackendProvider.BackendId; device = "cuda"; break;
    case "openvino":
        backends.UseOpenVino(new OpenVinoOptions(performanceHint: OpenVinoPerformanceHint.Latency));
        backendId = OpenVinoBackendProvider.BackendId; device = "CPU"; break;
    case "opencv-dnn":
        backends.UseOpenCvDnn(new OpenCvDnnOptions(new OpenCvDnnModelContract(modelId,
            new[] { new TensorDescriptor(inputName, TensorElementType.Float32, new TensorShape(1, 3, 640, 640)) },
            new[] { new TensorDescriptor(outputName, TensorElementType.Float32, new TensorShape(1, -1, 6)) })));
        backendId = OpenCvDnnBackendProvider.BackendId; device = "cpu"; break;
    case "tensorrt":
        if (!File.Exists(loadPath))
            new TensorRtOnnxEngineBuilder().Build(new ModelArtifact(modelId, "onnx", modelPath), loadPath,
                new TensorRtOnnxEngineBuildOptions(precision: TensorRtOnnxEnginePrecision.Float32,
                    optimizationLevel: 3));
        backends.UseTensorRT();
        backendId = TensorRtBackendProvider.BackendId; device = "cuda"; break;
    default: throw new ArgumentException("Unknown backend: " + backend);
}
var profiles = new VisualProfileRegistry();
profiles.Register(profile);
profiles.Freeze();
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    backendId, device);
var artifact = new ModelArtifact(modelId, format, loadPath);

using var pipeline = new VisualPipeline(
    backends,
    profiles.Select(artifact, backends, request, VisualTaskId.ObjectDetection),
    request);
var factory = new OpenCvVisualInputFactory();
DetectionResult Once(out double prepareMs, out double runMs)
{
    var stopwatch = Stopwatch.StartNew();
    using PreparedVisualInput input = factory.CreateFromFile(imagePath, profile);
    prepareMs = stopwatch.Elapsed.TotalMilliseconds;
    stopwatch.Restart();
    DetectionResult value = pipeline.Run(input).GetValue<DetectionResult>();
    runMs = stopwatch.Elapsed.TotalMilliseconds;
    return value;
}
const int warmup = 3, repeats = 20;
for (int i = 0; i < warmup; i++) Once(out _, out _);
var prepare = new List<double>();
var runs = new List<double>();
var totals = new List<double>();
DetectionResult result = null!;
for (int i = 0; i < repeats; i++)
{
    var total = Stopwatch.StartNew();
    result = Once(out double p, out double t);
    total.Stop();
    prepare.Add(p); runs.Add(t); totals.Add(total.Elapsed.TotalMilliseconds);
}
double Percentile(List<double> values, double q) => values.Order().ElementAt((int)Math.Ceiling(q * values.Count) - 1);
var report = new
{
    backend, device, warmup, repeats, format, modelPath,
    input = new { imagePath, width = 640, height = 640, resize = "letterbox", threshold = .25 },
    environment = new { os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription },
    prepareP50 = Percentile(prepare, .5), runP50 = Percentile(runs, .5),
    totalP50 = Percentile(totals, .5), totalP95 = Percentile(totals, .95),
    samples = totals,
    detections = result.Detections.Select(d => new { label = d.Label.Label, score = d.Label.Score,
        x = d.Box.X, y = d.Box.Y, width = d.Box.Width, height = d.Box.Height })
};
File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(FormattableString.Invariant($"warmup={warmup};repeats={repeats};total_p50_ms={report.totalP50:F3};total_p95_ms={report.totalP95:F3}"));

// 结果坐标已经还原到原图，直接在原图上绘制。
using Mat canvas = ImageCodecs.ImRead(imagePath);
Console.WriteLine($"backend={backend};device={device}");
Console.WriteLine($"detections={result.Detections.Count}");
foreach (Detection item in result.Detections)
{
    var box = item.Box;
    string label = FormattableString.Invariant(
        $"{item.Label.Label} {item.Label.Score:F3}");
    Console.WriteLine(FormattableString.Invariant(
        $"{label} box=({box.X:F1},{box.Y:F1},{box.Width:F1},{box.Height:F1})"));

    var topLeft = new Point((int)box.X, (int)box.Y);
    var bottomRight = new Point((int)box.Right, (int)box.Bottom);
    Size textSize = Drawing.GetTextSize(
        label, HersheyFonts.HersheySimplex, 0.8, 2, out _);
    var caption = new Point(
        Math.Clamp(topLeft.X, 3, Math.Max(3, canvas.Cols - textSize.Width - 16)),
        Math.Max(textSize.Height + 4, topLeft.Y - 8));

    Drawing.Rectangle(canvas, topLeft, bottomRight, new Scalar(80, 210, 30), 3);
    Drawing.PutText(canvas, label, caption, HersheyFonts.HersheySimplex,
        0.8, new Scalar(20, 20, 20), 5);
    Drawing.PutText(canvas, label, caption, HersheyFonts.HersheySimplex,
        0.8, new Scalar(255, 255, 255), 2);
}
if (!ImageCodecs.ImWrite(Path.Combine(outputDirectory, "result.jpg"), canvas))
    throw new IOException("无法保存 result.jpg。");
Console.WriteLine($"saved={outputDirectory}");
