using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.OpenCvSharp.Core;
using ImageCodecs = JYPPX.OpenCvSharp.ImgCodecs.Cv2;
using Drawing = JYPPX.OpenCvSharp.ImgProc.Cv2;
using HersheyFonts = JYPPX.OpenCvSharp.ImgProc.HersheyFonts;
using Ort = Microsoft.ML.OnnxRuntime;

string Option(string name, string fallback) => Array.IndexOf(args, name) is int i && i >= 0
    ? i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing value: " + name) : fallback;
int Number(string name, int fallback, int maximum) => int.TryParse(Option(name, fallback.ToString()), out int n) && n > 0 && n <= maximum
    ? n : throw new ArgumentException(name + " must be between 1 and " + maximum);
string modelPath = Path.GetFullPath(Option("--model", "models/yolo26n.onnx"));
string videoPath = Path.GetFullPath(Option("--video", "videos/vtest.avi"));
string backend = Option("--backend", "ort-cpu");
string output = Path.GetFullPath(Option("--output", "results/" + backend));
string ffmpeg = Option("--ffmpeg", "ffmpeg"), ffprobe = Option("--ffprobe", "ffprobe");
int frameLimit = Number("--frames", 48, 1000000), window = Number("--window", 12, 64);
int repeats = Number("--repeats", 3, 100), threads = Number("--threads", 2, 128);
int batch = Number("--batch", 4, 16);
if (!File.Exists(modelPath)) throw new FileNotFoundException("Download the ONNX model first.", modelPath);
using var stop = new CancellationTokenSource();
if (args.Contains("--cancel-after")) stop.CancelAfter(Number("--cancel-after", 1000, 600000));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };

try { await Execute(stop.Token); }
catch (Exception e) when (stop.IsCancellationRequested && e is not InvalidOperationException) { Console.WriteLine("Cancelled; active calls drained. " + e.GetType().Name); Environment.ExitCode = 130; }

async Task Execute(CancellationToken token)
{
    // Metadata inspection creates one short-lived ORT CPU session, outside all measurements.
    bool dynamicBatch;
    using (var metadataOptions = new Ort.SessionOptions { IntraOpNumThreads = 1 })
    using (var metadata = new Ort.InferenceSession(modelPath, metadataOptions))
    {
        foreach (var (name, port) in metadata.InputMetadata) Console.WriteLine($"input {name}: [{string.Join(',', port.Dimensions)}]");
        foreach (var (name, port) in metadata.OutputMetadata) Console.WriteLine($"output {name}: [{string.Join(',', port.Dimensions)}]");
        var input = metadata.InputMetadata.Single();
        var result = metadata.OutputMetadata.Single();
        if (input.Key != "images" || input.Value.Dimensions.Length != 4 || input.Value.Dimensions[1] != 3 ||
            input.Value.Dimensions[2] is not (640 or -1) || input.Value.Dimensions[3] is not (640 or -1) ||
            result.Key != "output0" || result.Value.Dimensions.Length != 3 || result.Value.Dimensions[2] is not (6 or -1) ||
            input.Value.ElementType != typeof(float) || result.Value.ElementType != typeof(float))
            throw new InvalidDataException("This example expects YOLO26 end-to-end FP32 [B,3,640,640] -> [B,N,6].");
        dynamicBatch = input.Value.Dimensions[0] < 0 && result.Value.Dimensions[0] < 0;
        if (!dynamicBatch && input.Value.Dimensions[0] != 1) throw new InvalidDataException("Use batch-one or dynamic-batch ONNX.");
        if (args.Contains("--inspect")) return;
    }
    Directory.CreateDirectory(output);
    using var backends = new BackendRegistry();
    BackendId backendId;
    string device;
    if (backend == "ort-cpu")
    {
        backends.UseOnnxRuntime(new OnnxRuntimeOptions(intraOpThreads: threads, interOpThreads: 1));
        backendId = OnnxRuntimeBackendProvider.BackendId; device = "cpu";
    }
    else if (backend == "openvino")
    {
        backends.UseOpenVino(new OpenVinoOptions(device: "CPU", performanceHint: OpenVinoPerformanceHint.Latency,
            streams: 1, inferenceThreads: threads, requestCount: 1));
        backendId = OpenVinoBackendProvider.BackendId; device = "CPU";
    }
    else throw new ArgumentException("Use --backend ort-cpu or openvino.");

    var id = new ModelId("blog/yolo26-video");
    var size = new VisualSize(640, 640);
    var profile = new VisualModelProfile("blog.yolo26.video", id, VisualTaskId.ObjectDetection, "1", "onnx",
        new VisualInputBinding("images", TensorElementType.Float32, new TensorShape(dynamicBatch ? -1 : 1, 3, 640, 640),
            VisualTensorLayout.Nchw, maximumBatch: dynamicBatch ? batch : 1),
        new[] { new VisualOutputBinding("output0", TensorElementType.Float32, new TensorShape(dynamicBatch ? -1 : 1, -1, 6)) },
        YoloLabelSets.Coco80.Select((s, i) => new VisualLabel(i, s)),
        new YoloDetectionDecoder(new YoloDetectionOutputContract("output0", YoloDetectionOutputKind.EndToEnd, 80),
            new YoloDetectionDecoderOptions(scoreThreshold: .25f)),
        preprocessing: new VisualPreprocessingOptions(size, VisualResizeMode.Letterbox, VisualColorOrder.Rgb,
            VisualNormalizationOptions.Scale(255f), paddingColor: new VisualRgbColor(114, 114, 114)));
    var profiles = new VisualProfileRegistry(); profiles.Register(profile); profiles.Freeze();
    var request = new BackendRequest(BackendCapabilities.TensorInference, backendId, device);
    var selection = profiles.Select(new ModelArtifact(id, "onnx", modelPath), backends, request, VisualTaskId.ObjectDetection);
    var modes = new List<Mode> { new("serial", 1, 0, 1), new("pool-2", 2, 0, 1),
        new("prefetch-1", 1, 2, 1), new("prefetch-2", 2, 2, 1) };
    if (dynamicBatch) modes.Add(new("batch-" + batch, 1, 0, batch));
    else Console.WriteLine("True model Batch skipped: this ONNX has fixed batch=1. Session pool and prefetch remain available.");

    var summaries = new List<object>();
    IReadOnlyList<FrameResult>? reference = null;
    foreach (Mode mode in modes)
    {
        token.ThrowIfCancellationRequested();
        using var pipeline = new VisualPipeline(backends, selection, request, new SessionOptions(maxConcurrency: mode.Sessions));
        var preparer = new FramePreparer();
        // Two complete small windows warm every independent session and the exact-size buffer cache.
        for (int w = 0; w < 2; w++) await RunClip(pipeline, preparer, mode, window, token);
        preparer.ResetPeak();
        var times = new List<double>(); var windowTimes = new List<double>();
        IReadOnlyList<FrameResult> last = Array.Empty<FrameResult>();
        for (int repeat = 0; repeat < repeats; repeat++)
        {
            var run = await RunClip(pipeline, preparer, mode, frameLimit, token);
            times.Add(run.TotalMs); windowTimes.AddRange(run.WindowMs); last = run.Frames;
            if (last.Count != frameLimit) throw new InvalidDataException("The clip is shorter than --frames. Lower the limit.");
            if (preparer.Active != 0) throw new InvalidOperationException("An input buffer is still in use.");
            Console.WriteLine($"{mode.Name} run={repeat + 1} frames={last.Count} total_ms={run.TotalMs:F2} fps={last.Count * 1000 / run.TotalMs:F2}");
        }
        bool exact = reference == null || JsonSerializer.Serialize(reference) == JsonSerializer.Serialize(last);
        var parity = Compare(reference ?? last, last);
        reference ??= last;
        string modeDirectory = Path.Combine(output, mode.Name); Directory.CreateDirectory(modeDirectory);
        File.WriteAllText(Path.Combine(modeDirectory, "frames.json"), JsonSerializer.Serialize(last, JsonOptions()));
        double median = Percentile(times, .5);
        summaries.Add(new { mode = mode.Name, sessions = mode.Sessions, batch = mode.Batch, prefetch = mode.Prefetch,
            totalMs = times, medianTotalMs = median, fpsAtMedianTotal = frameLimit * 1000 / median,
            windowP50Ms = Percentile(windowTimes, .5), windowP95Ms = Percentile(windowTimes, .95),
            frames = last.Count, detections = last.Sum(x => x.Detections.Count), exactWithSerial = exact, parity,
            peakPreparedInputs = preparer.Peak, activeInputsAfterRun = preparer.Active });
        if (mode.Batch == 1 && !exact) throw new InvalidDataException("Session/prefetch results differ from serial. Inspect frames.json before using the timings.");
        if (!parity.Equivalent) throw new InvalidDataException("Batch results exceed the stated parity tolerances. Inspect frames.json.");
    }
    var report = new { createdAt = DateTimeOffset.Now, backend, device, threadsPerSession = threads,
        model = Path.GetFileName(modelPath), dynamicBatch, frameLimit, window, repeats, warmupWindows = 2,
        protocol = "Timed: raw BGR pipe reads (including decoder wait), input preparation, scheduler, inference, decode and result projection. Excluded: ffprobe/process creation, model load, warmup, drawing, JSON writes. FFmpeg uses 1 decoder thread. Window percentiles are completion times, not single-frame latency.",
        environment = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            runtime = RuntimeInformation.FrameworkDescription, logicalProcessors = Environment.ProcessorCount }, modes = summaries };
    File.WriteAllText(Path.Combine(output, "benchmark.json"), JsonSerializer.Serialize(report, JsonOptions()));
    // One separate preview pass, so drawing cannot disturb any measured repetition.
    using (var preview = new FfmpegFrameReader(videoPath, ffmpeg, ffprobe, token))
    {
        for (int i = 0; i < Math.Min(48, reference!.Count); i++)
        {
            using var image = new Mat();
            if (!preview.Read(image, token)) break;
            if (i % 12 == 0)
            {
                var result = new DetectionResult(reference[i].Detections.Select(d => new Detection(
                    new JYPPX.DeploySharp.Geometry.RectangleF(d.X, d.Y, d.Width, d.Height),
                    new JYPPX.DeploySharp.Results.LabelScore(0, d.Label, d.Score))));
                SavePreview(new Frame(i, image), result, Path.Combine(output, $"frame-{i:D4}.jpg"));
            }
        }
    }
    Console.WriteLine("Saved: " + output);
}

async Task<ClipResult> RunClip(VisualPipeline pipeline, FramePreparer preparer, Mode mode, int limit, CancellationToken token)
{
    using var capture = new FfmpegFrameReader(videoPath, ffmpeg, ffprobe, token);
    var results = new List<FrameResult>(); var windowTimes = new List<double>();
    double totalMs = 0;
    while (results.Count < limit)
    {
        var watch = Stopwatch.StartNew();
        var frames = new List<Frame>();
        try
        {
            for (int i = 0; i < Math.Min(window, limit - results.Count); i++)
            {
                token.ThrowIfCancellationRequested();
                var image = new Mat();
                try
                {
                    if (!capture.Read(image, token) || image.Empty) { image.Dispose(); break; }
                    frames.Add(new Frame(results.Count + i, image));
                }
                catch { image.Dispose(); throw; }
            }
            if (frames.Count == 0) break;
            IReadOnlyList<VisualInferenceResult> predicted;
            if (mode.Prefetch > 0)
                predicted = await pipeline.RunPrefetchedAsync(frames,
                    (frame, ct) => preparer.Prepare(new[] { frame }, ct), prefetch: mode.Prefetch, cancellationToken: token);
            else
            {
                var prepared = new List<PreparedVisualInput>();
                try
                {
                    // Serial baseline prepares immediately before every inference.
                    if (mode.Name == "serial")
                    {
                        var items = new List<VisualInferenceResult>();
                        foreach (Frame frame in frames)
                        {
                            using var input = preparer.Prepare(new[] { frame }, token);
                            items.Add(pipeline.Run(input, cancellationToken: token));
                        }
                        predicted = items;
                    }
                    else
                    {
                        foreach (Frame[] group in frames.Chunk(mode.Batch)) prepared.Add(preparer.Prepare(group, token));
                        predicted = await pipeline.RunManyAsync(prepared, cancellationToken: token);
                    }
                }
                finally { foreach (var input in prepared) input.Dispose(); }
            }
            var decoded = new List<DetectionResult>();
            foreach (var value in predicted)
            {
                if (mode.Batch > 1 && value.GetValue<object>() is DetectionBatchResult batched) decoded.AddRange(batched.Results);
                else decoded.Add(value.GetValue<DetectionResult>());
            }
            if (decoded.Count != frames.Count) throw new InvalidDataException("Decoded frame count mismatch.");
            for (int i = 0; i < frames.Count; i++) results.Add(FrameResult.From(frames[i].Index, decoded[i]));
            watch.Stop(); windowTimes.Add(watch.Elapsed.TotalMilliseconds); totalMs += watch.Elapsed.TotalMilliseconds;
        }
        finally
        {
            foreach (Frame frame in frames) frame.Dispose();
            if (preparer.Active != 0) throw new InvalidOperationException("An input buffer was not returned after draining the window.");
        }
    }
    return new ClipResult(results, totalMs, windowTimes);
}

static void SavePreview(Frame frame, DetectionResult result, string path)
{
    using Mat image = frame.Image.Clone();
    foreach (Detection d in result.Detections)
    {
        var b = d.Box;
        Drawing.Rectangle(image, new Point((int)b.X, (int)b.Y), new Point((int)b.Right, (int)b.Bottom), new Scalar(70, 215, 95), 2);
        Drawing.PutText(image, $"{d.Label.Label} {d.Label.Score:F2}", new Point((int)b.X, Math.Max(18, (int)b.Y - 4)), HersheyFonts.HersheySimplex, .5, new Scalar(10, 20, 30), 3);
        Drawing.PutText(image, $"{d.Label.Label} {d.Label.Score:F2}", new Point((int)b.X, Math.Max(18, (int)b.Y - 4)), HersheyFonts.HersheySimplex, .5, new Scalar(250, 250, 250), 1);
    }
    Drawing.PutText(image, $"frame {frame.Index:D4} | {result.Detections.Count} detections", new Point(15, 28), HersheyFonts.HersheySimplex, .7, new Scalar(250, 250, 250), 2);
    if (!ImageCodecs.ImWrite(path, image)) throw new IOException("Cannot save " + path);
}
static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
static double Percentile(IEnumerable<double> values, double p) { double[] sorted = values.Order().ToArray(); return sorted[(int)Math.Ceiling(p * sorted.Length) - 1]; }
static Parity Compare(IReadOnlyList<FrameResult> baseline, IReadOnlyList<FrameResult> actual)
{
    int unmatched = 0, matched = 0; float maximumScoreDelta = 0, minimumIou = 1;
    if (baseline.Count != actual.Count) return new Parity(false, 0, baseline.Count + actual.Count, 0, 1);
    for (int i = 0; i < baseline.Count; i++)
    {
        if (baseline[i].Index != actual[i].Index) throw new InvalidDataException("Frame order changed.");
        var available = actual[i].Detections.ToList();
        foreach (var a in baseline[i].Detections)
        {
            int index = -1; float bestIou = 0;
            for (int j = 0; j < available.Count; j++)
            {
                var b = available[j]; if (a.Label != b.Label) continue;
                float intersection = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X)) *
                    Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
                float union = a.Width * a.Height + b.Width * b.Height - intersection;
                float iou = union > 0 ? intersection / union : 0;
                if (iou > bestIou) { index = j; bestIou = iou; }
            }
            if (index < 0) { unmatched++; continue; }
            minimumIou = Math.Min(minimumIou, bestIou);
            maximumScoreDelta = Math.Max(maximumScoreDelta, Math.Abs(a.Score - available[index].Score));
            matched++; available.RemoveAt(index);
        }
        unmatched += available.Count;
    }
    return new Parity(unmatched == 0 && minimumIou >= .99f && maximumScoreDelta <= .001f,
        matched, unmatched, minimumIou, maximumScoreDelta);
}
internal sealed record Parity(bool Equivalent, int Matched, int Unmatched, float MinimumIou, float MaximumScoreDelta);
internal sealed record Mode(string Name, int Sessions, int Prefetch, int Batch);
internal sealed record ClipResult(IReadOnlyList<FrameResult> Frames, double TotalMs, IReadOnlyList<double> WindowMs);
internal sealed record BoxResult(string Label, float Score, float X, float Y, float Width, float Height);
internal sealed record FrameResult(int Index, IReadOnlyList<BoxResult> Detections)
{
    public static FrameResult From(int index, DetectionResult result) => new(index,
        result.Detections.Select(d => new BoxResult(d.Label.Label, d.Label.Score, d.Box.X, d.Box.Y, d.Box.Width, d.Box.Height)).ToArray());
}
