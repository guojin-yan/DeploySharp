using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using Microsoft.ML.OnnxRuntime;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;

Console.OutputEncoding = Encoding.UTF8;
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancel;
try
{
    var options = DemoOptions.Parse(args);
    if (options.Inspect)
    {
        using var settings = new Microsoft.ML.OnnxRuntime.SessionOptions
            { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR };
        using var session = new InferenceSession(options.Model, settings);
        foreach (var port in session.InputMetadata)
            Console.WriteLine($"input {port.Key}: {port.Value.ElementType.Name} [{string.Join(',', port.Value.Dimensions)}]");
        foreach (var port in session.OutputMetadata)
            Console.WriteLine($"output {port.Key}: {port.Value.ElementType.Name} [{string.Join(',', port.Value.Dimensions)}]");
        return 0;
    }

    using var backends = new BackendRegistry();
    if (options.Backend == "ort-cpu") backends.UseOnnxRuntime(); else backends.UseOpenVino();
    var request = new BackendRequest(BackendCapabilities.TensorInference,
        options.Backend == "ort-cpu" ? OnnxRuntimeBackendProvider.BackendId : OpenVinoBackendProvider.BackendId,
        options.Backend == "ort-cpu" ? "cpu" : "CPU");
    var definition = NanoDetModel.Create(options.Threshold, options.Nms);
    using var runner = definition.CreateRunner(backends,
        new ModelArtifact(definition.Profile.ModelId, "onnx", options.Model), request);
    using var image = new OpenCvVisualInputFactory().DecodeForRois(
        OpenCvImageSource.FromFile(options.Image), cancellation.Token);
    ObjectDetections result = await runner.RunAsync(image, cancellationToken: cancellation.Token);
    cancellation.Token.ThrowIfCancellationRequested();

    string directory = Path.Combine(options.Output, options.Backend, Path.GetFileNameWithoutExtension(options.Image));
    Directory.CreateDirectory(directory);
    using var canvas = ImageCodecs.ImRead(options.Image);
    foreach (DetectedObject item in result.Objects)
    {
        PixelBox b = item.Box;
        Drawing.Rectangle(canvas, new Point((int)b.X, (int)b.Y),
            new Point((int)Math.Ceiling(b.X + b.Width), (int)Math.Ceiling(b.Y + b.Height)), new Scalar(130, 195, 25), 2);
        Drawing.PutText(canvas, $"{item.Label} {item.Score:F2}", new Point((int)b.X, Math.Max(16, (int)b.Y - 5)),
            JYPPX.OpenCvSharp.ImgProc.HersheyFonts.HersheySimplex, .55, new Scalar(30, 80, 235), 2);
        Console.WriteLine($"{item.Label}: {item.Score:F4}; box=({b.X:F1},{b.Y:F1},{b.Width:F1},{b.Height:F1})");
    }
    if (!ImageCodecs.ImWrite(Path.Combine(directory, "result.jpg"), canvas)) throw new IOException("Cannot save result.jpg");
    File.WriteAllText(Path.Combine(directory, "detections.json"), JsonSerializer.Serialize(new
    {
        deploySharp = "2.0.0", options.Backend, image = Path.GetFileName(options.Image),
        sourceSize = image.SourceSize, model = Path.GetFileName(options.Model), options.Threshold, options.Nms,
        environment = new { os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription },
        objects = result.Objects
    }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    Console.WriteLine($"Completed: backend={options.Backend}; detections={result.Objects.Count}; output={directory}");
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("已取消。"); return 2; }
catch (Exception error) when (cancellation.IsCancellationRequested) { Console.Error.WriteLine("已取消：" + error.Message); return 2; }
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
finally { Console.CancelKeyPress -= cancel; }

internal sealed record DemoOptions(string Backend, string Image, string Model, string Output, float Threshold, float Nms, bool Inspect)
{
    public static DemoOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool inspect = false;
        string[] allowed = ["--backend", "--image", "--model", "--out", "--threshold", "--nms"];
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--inspect") { inspect = true; continue; }
            string key = args[i];
            if (!allowed.Contains(key) || i + 1 >= args.Length || !values.TryAdd(key, args[++i]))
                throw new ArgumentException("Usage: --backend ort-cpu|openvino --image <file> --model <onnx> --out <directory> --threshold 0.35 --nms 0.6 [--inspect]");
        }
        string Value(string key, string fallback) => values.GetValueOrDefault(key, fallback);
        string backend = Value("--backend", "ort-cpu");
        float threshold = float.Parse(Value("--threshold", "0.35"), CultureInfo.InvariantCulture);
        float nms = float.Parse(Value("--nms", "0.6"), CultureInfo.InvariantCulture);
        if (backend is not ("ort-cpu" or "openvino") || !float.IsFinite(threshold) || threshold is <= 0 or > 1 ||
            !float.IsFinite(nms) || nms is <= 0 or > 1) throw new ArgumentException("Unsupported backend or threshold outside (0,1].");
        return new(backend, Path.GetFullPath(Value("--image", "demo-inputs/road-00.jpg")),
            Path.GetFullPath(Value("--model", "models/object_detection_nanodet_2022nov.onnx")),
            Path.GetFullPath(Value("--out", "results")), threshold, nms, inspect);
    }
}
