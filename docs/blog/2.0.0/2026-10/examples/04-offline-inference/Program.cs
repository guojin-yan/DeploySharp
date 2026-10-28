using System.Runtime.InteropServices;
using System.Text.Json;
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

// 相对路径统一从程序目录解析，从其他目录启动也能找到模型、配置和图片。
string baseDirectory = AppContext.BaseDirectory;
string Resolve(string value) => Path.GetFullPath(value, baseDirectory);
var config = JsonSerializer.Deserialize<DeploymentConfig>(
    File.ReadAllText(Resolve("deploysharp.json")))
    ?? throw new InvalidDataException("deploysharp.json 不能为空。");
string imagePath = Resolve(args.Length > 0 ? args[0] : config.ImagePath);
string modelPath = Resolve(args.Length > 1 ? args[1] : config.ModelPath);
string outputDirectory = Resolve(args.Length > 2 ? args[2] : config.OutputDirectory);
foreach (string path in new[] { imagePath, modelPath })
    if (!File.Exists(path)) throw new FileNotFoundException("找不到输入文件。", path);
IReadOnlyList<string> labels = string.IsNullOrWhiteSpace(config.ClassNamesPath)
    ? YoloLabelSets.Coco80
    : File.ReadAllLines(Resolve(config.ClassNamesPath));
if (labels.Count == 0 || labels.Any(string.IsNullOrWhiteSpace))
    throw new InvalidDataException("类别文件需要每行一个名称，按训练索引排列，不含空行。");
Directory.CreateDirectory(outputDirectory);

var size = new VisualSize(config.ModelWidth, config.ModelHeight);
var modelId = new ModelId("local/yolo26-offline");
var profile = new VisualModelProfile(
    "yolo26.detect.offline", modelId, VisualTaskId.ObjectDetection, "1", "onnx",
    new VisualInputBinding(config.InputName, TensorElementType.Float32,
        new TensorShape(1, 3, size.Height, size.Width), VisualTensorLayout.Nchw),
    new[] { new VisualOutputBinding(config.OutputName, TensorElementType.Float32,
        new TensorShape(1, -1, 6)) },
    labels.Select((name, index) => new VisualLabel(index, name)),
    new YoloDetectionDecoder(
        new YoloDetectionOutputContract(config.OutputName,
            YoloDetectionOutputKind.EndToEnd, labels.Count),
        new YoloDetectionDecoderOptions(scoreThreshold: config.ScoreThreshold)),
    preprocessing: new VisualPreprocessingOptions(
        size, VisualResizeMode.Letterbox, VisualColorOrder.Rgb,
        VisualNormalizationOptions.Scale(255f),
        paddingColor: new VisualRgbColor(114, 114, 114)));

using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var profiles = new VisualProfileRegistry();
profiles.Register(profile);
profiles.Freeze();
var request = new BackendRequest(BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");
var artifact = new ModelArtifact(modelId, "onnx", modelPath);
using var pipeline = new VisualPipeline(backends,
    profiles.Select(artifact, backends, request, VisualTaskId.ObjectDetection), request);
using PreparedVisualInput input = new OpenCvVisualInputFactory()
    .CreateFromFile(imagePath, profile);
DetectionResult result = pipeline.Run(input).GetValue<DetectionResult>();

using Mat canvas = ImageCodecs.ImRead(imagePath);
foreach (Detection item in result.Detections)
{
    var box = item.Box;
    string caption = FormattableString.Invariant($"{item.Label.Label} {item.Label.Score:F3}");
    Size textSize = Drawing.GetTextSize(caption, HersheyFonts.HersheySimplex, 0.75, 2, out _);
    var captionPoint = new Point(
        Math.Clamp((int)box.X, 3, Math.Max(3, canvas.Cols - textSize.Width - 12)),
        Math.Max(textSize.Height + 4, (int)box.Y - 8));
    Drawing.Rectangle(canvas, new Point((int)box.X, (int)box.Y),
        new Point((int)box.Right, (int)box.Bottom), new Scalar(80, 210, 30), 3);
    Drawing.PutText(canvas, caption, captionPoint,
        HersheyFonts.HersheySimplex, 0.75, new Scalar(20, 20, 20), 5);
    Drawing.PutText(canvas, caption, captionPoint,
        HersheyFonts.HersheySimplex, 0.75, new Scalar(255, 255, 255), 2);
}
if (!ImageCodecs.ImWrite(Path.Combine(outputDirectory, "result.jpg"), canvas))
    throw new IOException("无法写入结果图片，请使用有写权限的输出目录。");

var report = new
{
    backend = "onnxruntime", device = "cpu", deploySharp = "2.0.0",
    os = RuntimeInformation.OSDescription,
    framework = RuntimeInformation.FrameworkDescription,
    runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(),
    baseDirectory, modelPath, imagePath,
    detections = result.Detections.Select(item => new
    {
        label = item.Label.Label, score = item.Label.Score,
        x = item.Box.X, y = item.Box.Y, width = item.Box.Width, height = item.Box.Height
    })
};
File.WriteAllText(Path.Combine(outputDirectory, "result.json"),
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"backend=onnxruntime;device=cpu;detections={result.Detections.Count}");
Console.WriteLine("labels=" + string.Join(",", result.Detections.Select(item => item.Label.Label)));
Console.WriteLine("framework=" + report.framework);
Console.WriteLine("runtime_directory=" + report.runtimeDirectory);
Console.WriteLine("saved=" + outputDirectory);

internal sealed class DeploymentConfig
{
    public string ModelPath { get; set; } = "models/yolo26n.onnx";
    public string ImagePath { get; set; } = "images/bus.jpg";
    public string? ClassNamesPath { get; set; }
    public string OutputDirectory { get; set; } = "results";
    public string InputName { get; set; } = "images";
    public string OutputName { get; set; } = "output0";
    public int ModelWidth { get; set; } = 640;
    public int ModelHeight { get; set; } = 640;
    public float ScoreThreshold { get; set; } = 0.25f;
}
