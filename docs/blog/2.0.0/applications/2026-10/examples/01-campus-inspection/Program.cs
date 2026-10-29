using System.Globalization;
using System.Runtime.InteropServices;
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
            using var metadataOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR };
            using var session = new InferenceSession(Path.Combine(options.Models, name), metadataOptions);
            Console.WriteLine(name);
            foreach (var port in session.InputMetadata)
                Console.WriteLine($"  input: {port.Key}; {port.Value.ElementType.Name}; [{string.Join(',', port.Value.Dimensions)}]");
            foreach (var port in session.OutputMetadata)
                Console.WriteLine($"  output: {port.Key}; {port.Value.ElementType.Name}; [{string.Join(',', port.Value.Dimensions)}]");
        }
        return 0;
    }
    InspectionManifest manifest = InspectionConfig.Load(options.Manifest);
    PromptGroup[] groups = InspectionConfig.LoadPrompts(options.Prompts);
    Prompt[] prompts = groups.SelectMany(g => g.Candidates).ToArray();
    using var backends = new BackendRegistry();
    if (options.Backend == "ort-cpu") backends.UseOnnxRuntime(); else backends.UseOpenVino();
    var request = new BackendRequest(BackendCapabilities.TensorInference,
        options.Backend == "ort-cpu" ? OnnxRuntimeBackendProvider.BackendId : OpenVinoBackendProvider.BackendId,
        options.Backend == "ort-cpu" ? "cpu" : "CPU");
    var detector = NanoDetModel.Create(options.Threshold, options.Nms);
    var imageEncoder = ClipModels.CreateImageEncoder();
    using var detectionRunner = detector.CreateRunner(backends,
        new ModelArtifact(detector.Profile.ModelId, "onnx", Path.Combine(options.Models, "object_detection_nanodet_2022nov.onnx")), request);
    float[][] textVectors = await ClipModels.EncodePromptsAsync(backends, request, options.Models, prompts, cancellation.Token);
    var textByKey = prompts.Select((p, i) => (p.Key, Value: textVectors[i])).ToDictionary(x => x.Key, x => x.Value);
    using var imageRunner = imageEncoder.CreateRunner(backends,
        new ModelArtifact(imageEncoder.Profile.ModelId, "onnx", Path.Combine(options.Models, "clip-vit-b-32.image-encoder.onnx")), request);
    var reports = new List<SceneAnalysisResult>();
    string root = Path.Combine(options.Output, options.Backend);
    Directory.CreateDirectory(root);
    foreach (InspectionSnapshot snapshot in manifest.Snapshots)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        InspectionCamera camera = manifest.Cameras.Single(c => c.Id == snapshot.CameraId);
        string imagePath = Path.GetFullPath(snapshot.Image, Path.GetDirectoryName(options.Manifest)!);
        string directory = Path.Combine(root, snapshot.Id);
        Directory.CreateDirectory(directory);
        using var decoded = new OpenCvVisualInputFactory().DecodeForRois(OpenCvImageSource.FromFile(imagePath), cancellation.Token);
        ObjectDetections detected = await detectionRunner.RunAsync(decoded, cancellationToken: cancellation.Token);
        ZoneObservation[] zones = InspectionRules.Evaluate(camera, detected, decoded.SourceSize);
        var zoneIds = detected.Objects.Select((_, index) => zones.Where(z => z.ObjectIds.Contains(index)).Select(z => z.Id).ToArray()).ToArray();
        // 语义阶段只处理区域内有候选组的目标，几何事件不依赖 CLIP 猜测。
        var selected = detected.Objects.Select((d, index) => new { Detection = d, Index = index,
            Group = groups.SingleOrDefault(g => g.ClassIds.Contains(d.ClassId)) })
            .Where(x => zoneIds[x.Index].Length > 0 && x.Group != null).Take(options.MaxRois).ToArray();
        var analyzed = new Dictionary<int, AnalyzedObject>();
        var batches = new List<int>();
        for (int start = 0; start < selected.Length; start += options.Batch)
        {
            var batch = selected.Skip(start).Take(options.Batch).ToArray();
            PixelBox[] crops = batch.Select(x => InspectionRules.Expand(x.Detection.Box, decoded.SourceSize, options.Padding)).ToArray();
            IVisualRoiGeometry[] regions = crops.Select(b => (IVisualRoiGeometry)new RectangleRoiGeometry(new RectangleF(b.X, b.Y, b.Width, b.Height))).ToArray();
            ImageEmbeddings embeddings = await imageRunner.RunAsync(new RoiBatch(decoded, regions), cancellationToken: cancellation.Token);
            batches.Add(batch.Length);
            for (int row = 0; row < batch.Length; row++)
            {
                var item = batch[row];
                SemanticMatch[] matches = ClipModels.Match(embeddings.Values[row], item.Group!, textByKey);
                double margin = matches[0].CandidateScore - matches[1].CandidateScore;
                analyzed.Add(item.Index, new(item.Index, crops[row], item.Group!.Name,
                    margin < options.ReviewMargin, margin, matches.Take(3).ToArray()));
            }
        }
        var objects = detected.Objects.Select((d, i) => new SceneObject(i, d, zoneIds[i], analyzed.GetValueOrDefault(i),
            analyzed.ContainsKey(i) ? null : zoneIds[i].Length == 0 ? "outside-zones" :
            groups.Any(g => g.ClassIds.Contains(d.ClassId)) ? "max-rois-limit" : "no-prompt-group")).ToArray();
        InspectionEvent[] events = zones.Where(z => z.Triggered).Select(z => new InspectionEvent(
            snapshot.Id + "-" + z.Id, z.Id, z.Reason, z.ObjectIds, "pending-human-review", "scene.jpg")).ToArray();
        var report = new SceneAnalysisResult("2.0.0", options.Backend, snapshot, camera, decoded.SourceSize.Width, decoded.SourceSize.Height,
            new { os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription },
            new { detection = "NanoDet-Plus m 1.5x 416 FP32", clip = "CLIP ViT-B/32 split ONNX",
                options.Threshold, options.Nms, options.Batch, options.MaxRois, options.Padding, options.ReviewMargin },
            groups, batches.ToArray(), zones, objects, events);
        cancellation.Token.ThrowIfCancellationRequested();
        PreviewWriter.SaveResult(imagePath, directory, report);
        InspectionOutput.WriteJson(Path.Combine(directory, "scene.json"), report);
        reports.Add(report);
        Console.WriteLine($"{options.Backend}/{snapshot.Id}: detections={objects.Length}; zone-hits={objects.Count(o => o.ZoneIds.Length > 0)}; semantic-rois={analyzed.Count}; events={events.Length}; batches=[{string.Join(',', batches)}]");
    }
    InspectionOutput.SaveSummary(root, manifest, reports);
    Console.WriteLine($"Completed: snapshots={reports.Count}; observations={reports.Sum(r => r.Objects.Length)}; events={reports.Sum(r => r.Events.Length)}");
    Console.WriteLine("Open: " + Path.Combine(root, "index.html"));
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("已取消。"); return 2; }
catch (Exception error) when (cancellation.IsCancellationRequested)
{ Console.Error.WriteLine("已取消：" + error.Message); return 2; }
catch (Exception error) { Console.Error.WriteLine(error); return 1; }

internal sealed record DemoOptions(string Backend, string Models, string Manifest, string Prompts, string Output,
    float Threshold, float Nms, int Batch, int MaxRois, float Padding, double ReviewMargin, bool Inspect)
{
    public static DemoOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool inspect = false;
        string[] allowed = ["--backend", "--models", "--manifest", "--prompts", "--out", "--threshold", "--nms", "--batch", "--max-rois", "--padding", "--review-margin"];
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--inspect") { inspect = true; continue; }
            string key = args[i];
            if (!allowed.Contains(key) || i + 1 >= args.Length || !values.TryAdd(key, args[++i]))
                throw new ArgumentException("支持 --backend ort-cpu|openvino、--manifest、--models、--prompts、--out、--threshold、--nms、--batch、--max-rois、--padding、--review-margin 和 --inspect。");
        }
        string Value(string key, string fallback) => values.GetValueOrDefault(key, fallback);
        float Number(string key, string fallback) => float.Parse(Value(key, fallback), CultureInfo.InvariantCulture);
        string backend = Value("--backend", "ort-cpu");
        if (backend is not ("ort-cpu" or "openvino")) throw new ArgumentException("本例只配置了 ORT CPU 和 OpenVINO CPU。");
        float threshold = Number("--threshold", "0.35"), nms = Number("--nms", "0.6"), padding = Number("--padding", "0.06");
        double review = double.Parse(Value("--review-margin", "0.1"), CultureInfo.InvariantCulture);
        int batch = int.Parse(Value("--batch", "4"), CultureInfo.InvariantCulture), maxRois = int.Parse(Value("--max-rois", "16"), CultureInfo.InvariantCulture);
        if (!float.IsFinite(threshold) || threshold is <= 0 or > 1 || !float.IsFinite(nms) || nms is <= 0 or > 1 ||
            !float.IsFinite(padding) || padding is < 0 or > .5f || !double.IsFinite(review) || review is < 0 or > 1 ||
            batch is < 1 or > 16 || maxRois is < 1 or > 100)
            throw new ArgumentException("阈值需在 (0,1]，复核差值在 [0,1]，padding 在 [0,0.5]，batch 在 1—16，max-rois 在 1—100。");
        return new(backend, Path.GetFullPath(Value("--models", "models")), Path.GetFullPath(Value("--manifest", "inspection.json")),
            Path.GetFullPath(Value("--prompts", "prompts.json")), Path.GetFullPath(Value("--out", "results")),
            threshold, nms, batch, maxRois, padding, review, inspect);
    }
}
