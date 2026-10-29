using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using Microsoft.ML.OnnxRuntime;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var options = DemoOptions.Parse(args);
    if (options.Inspect)
    {
        foreach (string name in new[] { "object_detection_nanodet_2022nov.onnx", "clip-vit-b-32.image-encoder.onnx", "clip-vit-b-32.text-encoder.onnx" })
        {
            Console.WriteLine(name);
            using var metadataOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR };
            using var session = new InferenceSession(Path.Combine(options.Models, name), metadataOptions);
            foreach (var port in session.InputMetadata)
                Console.WriteLine($"  input: {port.Key}; {port.Value.ElementType.Name}; [{string.Join(',', port.Value.Dimensions)}]");
            foreach (var port in session.OutputMetadata)
                Console.WriteLine($"  output: {port.Key}; {port.Value.ElementType.Name}; [{string.Join(',', port.Value.Dimensions)}]");
        }
        return 0;
    }

    PromptGroup[] groups = JsonSerializer.Deserialize<PromptGroup[]>(File.ReadAllText(options.Prompts))
        ?? throw new InvalidDataException("候选描述文件为空。");
    if (groups.Length == 0 || groups.Any(g => string.IsNullOrWhiteSpace(g.Name) || g.ClassIds is null ||
        g.ClassIds.Length == 0 || g.ClassIds.Any(c => c < 0 || c >= 80) ||
        g.Candidates is null || g.Candidates.Length < 2 ||
        g.Candidates.Any(p => p is null || string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Text))))
        throw new InvalidDataException("每组需要名称、有效 COCO 类别和至少两条有 Key/Text 的描述。");
    if (groups.SelectMany(g => g.ClassIds).Distinct().Count() != groups.Sum(g => g.ClassIds.Length))
        throw new InvalidDataException("同一个检测类别不能分属多组。");
    Prompt[] prompts = groups.SelectMany(g => g.Candidates).ToArray();
    if (prompts.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != prompts.Length)
        throw new InvalidDataException("候选 Key 必须全局唯一。");

    using var backends = new BackendRegistry();
    if (options.Backend == "ort-cpu") backends.UseOnnxRuntime();
    else backends.UseOpenVino();
    var request = new BackendRequest(BackendCapabilities.TensorInference,
        options.Backend == "ort-cpu" ? OnnxRuntimeBackendProvider.BackendId : OpenVinoBackendProvider.BackendId,
        options.Backend == "ort-cpu" ? "cpu" : "CPU");
    var detector = NanoDetModel.Create(options.Threshold, options.Nms);
    var imageEncoder = ClipModels.CreateImageEncoder();
    using var detectionRunner = detector.CreateRunner(backends,
        new ModelArtifact(detector.Profile.ModelId, "onnx", Path.Combine(options.Models, "object_detection_nanodet_2022nov.onnx")), request);
    // 先编码候选并释放文本 Session，再加载大图像 Encoder，降低同时驻留的权重内存。
    float[][] textVectors = await ClipModels.EncodePromptsAsync(backends, request, options.Models, prompts, cancellation.Token);
    var textByKey = prompts.Select((p, i) => (p.Key, Value: textVectors[i])).ToDictionary(x => x.Key, x => x.Value);
    using var imageRunner = imageEncoder.CreateRunner(backends,
        new ModelArtifact(imageEncoder.Profile.ModelId, "onnx", Path.Combine(options.Models, "clip-vit-b-32.image-encoder.onnx")), request);
    string directory = Path.Combine(options.Output, options.Backend);
    Directory.CreateDirectory(directory);
    using var decoded = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromFile(options.Image), cancellation.Token);
    ObjectDetections detected = await detectionRunner.RunAsync(decoded, cancellationToken: cancellation.Token);
    var selected = detected.Objects.Select((d, index) => new { Detection = d, Index = index,
        Group = groups.SingleOrDefault(g => g.ClassIds.Contains(d.ClassId)) })
        .Where(x => x.Group != null).Take(options.MaxRois).ToArray();
    var analyzed = new List<AnalyzedObject>();
    for (int start = 0; start < selected.Length; start += options.Batch)
    {
        var batch = selected.Skip(start).Take(options.Batch).ToArray();
        PixelBox[] crops = batch.Select(x => Expand(x.Detection.Box, decoded.SourceSize, options.Padding)).ToArray();
        IVisualRoiGeometry[] regions = crops.Select(b => (IVisualRoiGeometry)new RectangleRoiGeometry(
            new RectangleF(b.X, b.Y, b.Width, b.Height))).ToArray();
        ImageEmbeddings embeddings = await imageRunner.RunAsync(new RoiBatch(decoded, regions), cancellationToken: cancellation.Token);
        for (int row = 0; row < batch.Length; row++)
        {
            var item = batch[row];
            SemanticMatch[] matches = ClipModels.Match(embeddings.Values[row], item.Group!, textByKey);
            double margin = matches[0].CandidateScore - matches[1].CandidateScore;
            analyzed.Add(new(item.Index, item.Detection, crops[row], item.Group!.Name,
                margin < options.ReviewMargin, margin, matches.Take(3).ToArray()));
        }
    }
    var objects = detected.Objects.Select((d, i) => new SceneObject(i, d,
        analyzed.SingleOrDefault(x => x.Id == i),
        analyzed.Any(x => x.Id == i) ? null : groups.Any(g => g.ClassIds.Contains(d.ClassId))
            ? "max-rois-limit" : "no-prompt-group")).ToArray();
    var report = new SceneAnalysisResult("2.0.0", options.Backend, Path.GetFileName(options.Image),
        decoded.SourceSize.Width, decoded.SourceSize.Height,
        new { os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription },
        new { detection = "NanoDet-Plus m 1.5x 416 FP32", clip = "CLIP ViT-B/32 split ONNX",
            options.Threshold, options.Nms, options.Batch, options.MaxRois, options.Padding, options.ReviewMargin },
        groups, objects);
    cancellation.Token.ThrowIfCancellationRequested();
    File.WriteAllText(Path.Combine(directory, "scene.json"), JsonSerializer.Serialize(report,
        new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    PreviewWriter.SaveResult(options.Image, directory, report);
    Console.WriteLine($"{options.Backend}: detections={objects.Length}; semantic-rois={analyzed.Count}; prompts={prompts.Length}; clip-batch={options.Batch}");
    foreach (var obj in analyzed)
        Console.WriteLine(FormattableString.Invariant($"#{obj.Id} {obj.Detection.Label} det={obj.Detection.Score:F4} => {obj.Matches[0].Key}; candidate-score={obj.Matches[0].CandidateScore:F4}; review={obj.RequiresReview}"));
    Console.WriteLine("Saved: " + directory);
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("已取消。"); return 2; }
catch (Exception error) when (cancellation.IsCancellationRequested)
{ Console.Error.WriteLine("已取消：" + error.Message); return 2; }
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

static PixelBox Expand(PixelBox box, VisualSize size, float padding)
{
    int left = Math.Max(0, (int)Math.Floor(box.X - box.Width * padding));
    int top = Math.Max(0, (int)Math.Floor(box.Y - box.Height * padding));
    int right = Math.Min(size.Width, (int)Math.Ceiling(box.X + box.Width * (1 + padding)));
    int bottom = Math.Min(size.Height, (int)Math.Ceiling(box.Y + box.Height * (1 + padding)));
    return new(left, top, right - left, bottom - top);
}

public sealed record AnalyzedObject(int Id, DetectedObject Detection, PixelBox Crop,
    string PromptGroup, bool RequiresReview, double Margin, SemanticMatch[] Matches);
public sealed record SceneObject(int Id, DetectedObject Detection, AnalyzedObject? Semantic, string? SkippedReason);
public sealed record SceneAnalysisResult(string PackageVersion, string Backend, string Image, int Width, int Height,
    object Environment, object Configuration, PromptGroup[] PromptGroups, SceneObject[] Objects);

internal sealed record DemoOptions(string Backend, string Models, string Image, string Prompts, string Output,
    float Threshold, float Nms, int Batch, int MaxRois, float Padding, double ReviewMargin, bool Inspect)
{
    public static DemoOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool inspect = false;
        string[] allowed = ["--backend", "--models", "--image", "--prompts", "--out", "--threshold", "--nms", "--batch", "--max-rois", "--padding", "--review-margin"];
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--inspect") { inspect = true; continue; }
            string key = args[i];
            if (!allowed.Contains(key) || i + 1 >= args.Length || !values.TryAdd(key, args[++i]))
                throw new ArgumentException("支持 --backend ort-cpu|openvino、--models、--image、--prompts、--out、--threshold、--nms、--batch、--max-rois、--padding、--review-margin 和 --inspect。");
        }
        string Value(string key, string fallback) => values.GetValueOrDefault(key, fallback);
        float Number(string key, string fallback) => float.Parse(Value(key, fallback), CultureInfo.InvariantCulture);
        string backend = Value("--backend", "ort-cpu");
        if (backend is not ("ort-cpu" or "openvino")) throw new ArgumentException("本例只配置了 ORT CPU 和 OpenVINO CPU。");
        float threshold = Number("--threshold", "0.35"), nms = Number("--nms", "0.6"), padding = Number("--padding", "0.06");
        double review = double.Parse(Value("--review-margin", "0.1"), CultureInfo.InvariantCulture);
        int batch = int.Parse(Value("--batch", "4"), CultureInfo.InvariantCulture);
        int maxRois = int.Parse(Value("--max-rois", "16"), CultureInfo.InvariantCulture);
        if (!float.IsFinite(threshold) || threshold is <= 0 or > 1 || !float.IsFinite(nms) || nms is <= 0 or > 1 ||
            !float.IsFinite(padding) || padding is < 0 or > .5f || !double.IsFinite(review) || review is < 0 or > 1 ||
            batch is < 1 or > 16 || maxRois is < 1 or > 100)
            throw new ArgumentException("阈值需在 (0,1]，复核差值在 [0,1]，padding 在 [0,0.5]，batch 在 1—16，max-rois 在 1—100。");
        return new(backend, Path.GetFullPath(Value("--models", "models")),
            Path.GetFullPath(Value("--image", "images/bus.jpg")), Path.GetFullPath(Value("--prompts", "prompts.json")),
            Path.GetFullPath(Value("--out", "results")), threshold, nms, batch, maxRois, padding, review, inspect);
    }
}
