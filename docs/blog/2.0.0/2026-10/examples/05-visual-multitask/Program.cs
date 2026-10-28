using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using OrientedDetectionResult = JYPPX.DeploySharp.Visual.OrientedDetectionResult;

string task = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
if (task == "all" && args.Length > 1)
    throw new ArgumentException("自定义模型和图片路径请使用单个任务，不要使用 all。");
string[] tasks = task == "all" ? ["classify", "segment", "pose", "obb"] : [task];
using var backends = new BackendRegistry();
backends.UseOnnxRuntime();
var request = new BackendRequest(BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");

foreach (string current in tasks)
{
    var defaults = current switch
    {
        "classify" => (Model: "yolov8s-cls.onnx", Image: "demo_7.jpg"),
        "segment" => (Model: "yolo26s-seg.onnx", Image: "bus.jpg"),
        "pose" => (Model: "yolo26s-pose.onnx", Image: "demo_9.jpg"),
        "obb" => (Model: "yolo26s-obb.onnx", Image: "plane.png"),
        _ => throw new ArgumentException("任务应为 all / classify / segment / pose / obb。")
    };
    string modelPath = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine("models", defaults.Model));
    string imagePath = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine("images", defaults.Image));
    if (!File.Exists(modelPath)) throw new FileNotFoundException("找不到本地模型。", modelPath);
    if (!File.Exists(imagePath)) throw new FileNotFoundException("找不到输入图片。", imagePath);

    VisualModelProfile profile = Profiles.Create(current, args.Length > 3 ? args[3] : null);
    var profiles = new VisualProfileRegistry();
    profiles.Register(profile);
    profiles.Freeze();
    var artifact = new ModelArtifact(profile.ModelId, "onnx", modelPath);
    using var pipeline = new VisualPipeline(backends,
        profiles.Select(artifact, backends, request, profile.Task), request);
    using PreparedVisualInput input = new OpenCvVisualInputFactory().CreateFromFile(imagePath, profile);
    VisualInferenceResult result = pipeline.Run(input);

    string directory = Path.GetFullPath(Path.Combine("results", current));
    Directory.CreateDirectory(directory);
    object details = current switch
    {
        "classify" => ResultWriter.Classification(imagePath, directory, result.GetValue<ClassificationResult>()),
        "segment" => ResultWriter.Segmentation(imagePath, directory, result.GetValue<InstanceSegmentationResult>()),
        "pose" => ResultWriter.Pose(imagePath, directory, result.GetValue<PoseEstimationResult>()),
        "obb" => ResultWriter.Obb(imagePath, directory, result.GetValue<OrientedDetectionResult>()),
        _ => throw new InvalidOperationException()
    };
    File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new
    {
        task = current, backend = "onnxruntime", device = "cpu", packageVersion = "2.0.0",
        model = Path.GetFileName(modelPath), image = Path.GetFileName(imagePath),
        environment = new
        {
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        },
        preprocessing = new { size = profile.Input.ShapePattern.ToString(),
            resize = profile.Preprocessing!.ResizeMode.ToString(), color = "RGB", scaleDivisor = 255 },
        outputs = profile.Outputs.Select(o => new { o.Name, shape = o.ShapePattern.ToString() }),
        result = details
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{current}: saved={directory}");
}
