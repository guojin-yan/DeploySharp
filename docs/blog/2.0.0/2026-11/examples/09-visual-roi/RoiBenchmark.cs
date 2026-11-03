using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Configuration.Json;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class RoiBenchmark
{
    public static async Task RunAsync(VisualPipeline pipeline, string[] files, string configs, string output,
        OpenCvPreprocessOptions options, int warmup, int iterations, object configuration, CancellationToken token)
    {
        if (warmup < 5 || iterations < 50 || iterations > 10000)
            throw new ArgumentException("Use at least 5 warm-ups and 50 measured calls; maximum 10000 iterations.");
        var factory = new OpenCvVisualInputFactory();
        var runner = new VisualRoiRunner(pipeline);
        var merger = new RoiDetectionResultMerger();
        var cases = new List<object>();
        string[] gpuBefore = GpuSnapshot();
        var process = Process.GetCurrentProcess();
        foreach (string file in files)
        {
            // Encoded bytes/configuration are loaded once; JPEG decode remains inside each timed frame.
            byte[] bytes = File.ReadAllBytes(file);
            var source = OpenCvImageSource.FromBytes(bytes);
            using var inspected = factory.DecodeForRois(source, token);
            string configPath = Path.Combine(configs, Path.GetFileName(file).Split('-')[0] + ".json");
            string configText = File.ReadAllText(configPath);
            VisualRoiSnapshot snapshot = VisualRoiJsonSerializer.Deserialize(configText);
            if (snapshot.SourceSize != inspected.SourceSize) throw new InvalidDataException("Benchmark config/source size mismatch.");
            var filter = new VisualRoiSnapshot(snapshot.SourceSize, snapshot.Rois.Where(r => r.ExecutionMode == RoiExecutionMode.FilterResults));
            var crops = new VisualRoiSnapshot(snapshot.SourceSize, snapshot.Rois.Where(r => r.ExecutionMode == RoiExecutionMode.CropAndInfer));
            var whole = new VisualRoiSnapshot(snapshot.SourceSize, new[] { new VisualRoi("whole",
                new RectangleRoiGeometry(new RectangleF(0, 0, snapshot.SourceSize.Width, snapshot.SourceSize.Height)),
                executionMode: RoiExecutionMode.CropAndInfer) });
            var execution = new RoiExecutionOptions(maximumRois: 8, prefetch: 2, maximumPreparedPixels: 16_000_000);
            var fullSamples = new List<FullSample>(iterations);
            var singleSamples = new List<CropSample>(iterations);
            var cropSamples = new List<CropSample>(iterations);
            DetectionResult? baseline = null;
            for (int round = -warmup; round < iterations; round++)
            {
                // Rotate order; avoid always measuring one route immediately after process startup.
                for (int offset = 0; offset < 3; offset++)
                {
                    int mode = ((round + warmup) + offset) % 3;
                    token.ThrowIfCancellationRequested();
                    if (mode == 0)
                    {
                        long allocated = GC.GetTotalAllocatedBytes(true);
                        long start = Stopwatch.GetTimestamp();
                        var input = factory.Create(source, "images", options, cancellationToken: token);
                        long prepared = Stopwatch.GetTimestamp();
                        DetectionResult detected;
                        try { detected = (DetectionResult)pipeline.Run(input).Value; }
                        finally { input.Dispose(); }
                        long baseDone = Stopwatch.GetTimestamp();
                        var retained = VisualRoiDetectionFilter.Filter(detected, filter, VisualTaskId.ObjectDetection);
                        long done = Stopwatch.GetTimestamp();
                        long allocation = GC.GetTotalAllocatedBytes(true) - allocated;
                        baseline ??= detected;
                        if (round >= 0) fullSamples.Add(new FullSample(Ms(start, baseDone), Ms(start, done),
                            Ms(start, prepared), Ms(prepared, baseDone), Ms(baseDone, done), allocation,
                            detected.Detections.Count, retained.Detections.Count));
                    }
                    else
                    {
                        bool single = mode == 1;
                        long allocated = GC.GetTotalAllocatedBytes(true);
                        long start = Stopwatch.GetTimestamp();
                        var decoded = factory.DecodeForRois(source, token);
                        long decodedAt = Stopwatch.GetTimestamp();
                        RoiInferenceBatchResult batch;
                        RoiMergedDetectionResult merged;
                        int retained;
                        long ran, mergedAt, filteredAt;
                        try
                        {
                            batch = await runner.RunCropAsync(single ? whole : crops,
                                (_, g, ct) => Task.FromResult(decoded.Prepare(g, "images", options, ct)), execution, token);
                            ran = Stopwatch.GetTimestamp();
                            merged = merger.Merge(batch, iouThreshold: .45f,
                                mode: single ? RoiResultMergeMode.KeepAll : RoiResultMergeMode.ClassAwareNms);
                            mergedAt = Stopwatch.GetTimestamp();
                            retained = single ? merged.Detections.Detections.Count :
                                VisualRoiDetectionFilter.Filter(merged.Detections, filter, VisualTaskId.ObjectDetection).Detections.Count;
                            filteredAt = Stopwatch.GetTimestamp();
                        }
                        finally { decoded.Dispose(); }
                        long done = Stopwatch.GetTimestamp();
                        long allocation = GC.GetTotalAllocatedBytes(true) - allocated;
                        if (single && baseline != null) EnsureEquivalent(baseline, merged.Detections);
                        if (round >= 0) (single ? singleSamples : cropSamples).Add(new CropSample(Ms(start, done),
                            Ms(start, decodedAt), Ms(decodedAt, ran), Ms(ran, mergedAt), Ms(mergedAt, filteredAt),
                            Ms(filteredAt, done), allocation, batch.Items.Count,
                            batch.Succeeded.Sum(i => ((DetectionResult)i.Result!.Value).Detections.Count),
                            merged.Detections.Detections.Count, retained));
                    }
                }
            }
            // Fixed decoded result: measures the filter independently of variable model/device latency.
            var filters = new List<double>(1000);
            for (int i = 0; i < 20; i++) VisualRoiDetectionFilter.Filter(baseline!, filter, VisualTaskId.ObjectDetection);
            long filterAllocation = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                long start = Stopwatch.GetTimestamp();
                VisualRoiDetectionFilter.Filter(baseline!, filter, VisualTaskId.ObjectDetection);
                filters.Add(Ms(start, Stopwatch.GetTimestamp()));
            }
            filterAllocation = GC.GetAllocatedBytesForCurrentThread() - filterAllocation;
            process.Refresh();
            cases.Add(new {
                image = Path.GetFileName(file), inputSha256 = Hash(bytes), configSha256 = Hash(System.Text.Encoding.UTF8.GetBytes(configText)),
                source = new { width = snapshot.SourceSize.Width, height = snapshot.SourceSize.Height },
                filterRois = filter.Rois.Count, cropRois = crops.Rois.Count,
                summary = new {
                    noRoi = Stats(fullSamples.Select(s => s.NoRoiMs)),
                    fullWithFilter = Stats(fullSamples.Select(s => s.WithFilterMs)),
                    filterIncrement = Stats(fullSamples.Select(s => s.FilterMs)),
                    filterShareOfMeanBaselinePercent = fullSamples.Average(s => s.FilterMs) / fullSamples.Average(s => s.NoRoiMs) * 100,
                    preparation = Stats(fullSamples.Select(s => s.PreparationMs)),
                    inferenceDecodeAndInputRelease = Stats(fullSamples.Select(s => s.InferenceAndReleaseMs)),
                    singleFullRoi = Stats(singleSamples.Select(s => s.TotalMs)),
                    twoLocalRois = Stats(cropSamples.Select(s => s.TotalMs)),
                    cropMerge = Stats(cropSamples.Select(s => s.MergeMs)),
                    cropFinalFilter = Stats(cropSamples.Select(s => s.FilterMs)),
                    filterOnly = Stats(filters), filterOnlyMeanAllocatedBytes = filterAllocation / 1000d,
                    fullWithFilterMeanManagedAllocatedBytes = fullSamples.Average(s => s.ManagedAllocatedBytes),
                    singleFullRoiMeanManagedAllocatedBytes = singleSamples.Average(s => s.ManagedAllocatedBytes),
                    twoLocalRoisMeanManagedAllocatedBytes = cropSamples.Average(s => s.ManagedAllocatedBytes),
                    workingSetBytes = process.WorkingSet64, peakWorkingSetBytes = process.PeakWorkingSet64 },
                counts = new { full = fullSamples[0].FullCount, filtered = fullSamples[0].FilteredCount,
                    oneFullRoiInputs = singleSamples[0].Inputs, twoLocalRoiInputs = cropSamples[0].Inputs,
                    cropRaw = cropSamples[0].RawCount, cropMerged = cropSamples[0].MergedCount, cropFiltered = cropSamples[0].FilteredCount },
                fullSamples, singleSamples, cropSamples, filterOnlySamplesMs = filters });
            Console.WriteLine($"benchmark {Path.GetFileName(file)}: no_roi_p50={Stats(fullSamples.Select(s=>s.NoRoiMs)).P50:F3} ms, " +
                $"filter_p50={Stats(fullSamples.Select(s=>s.FilterMs)).P50:F5} ms, " +
                $"one_roi_p50={Stats(singleSamples.Select(s=>s.TotalMs)).P50:F3} ms, two_rois_p50={Stats(cropSamples.Select(s=>s.TotalMs)).P50:F3} ms");
        }
        string serialized = JsonSerializer.Serialize(configuration);
        using JsonDocument config = JsonDocument.Parse(serialized);
        string model = config.RootElement.GetProperty("model").GetString()!;
        string artifact = config.RootElement.GetProperty("artifact").GetString()!;
        var report = new { createdAt = DateTimeOffset.Now, configuration,
            modelSha256 = Hash(File.ReadAllBytes(model)), artifactSha256 = Hash(File.ReadAllBytes(artifact)),
            environment = new { os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalProcessors = Environment.ProcessorCount },
            warmupPerRoute = warmup, iterationsPerRoute = iterations, filterOnlyWarmup = 20, filterOnlyIterations = 1000,
            precision = "FP32, TF32 disabled on CUDA/TensorRT", shape = "1x3x640x640", prefetch = 2,
            protocol = "Encoded JPG bytes and parsed snapshots reused; each frame decodes JPEG, prepares input, performs synchronous-completed inference/decoder, and releases input. No-ROI and with-filter share exactly the same inference prefix; filter increment is measured immediately after it. Single-full-ROI uses identical full-image pixels through the ROI scheduler, KeepAll merge. Two-local-ROIs use configured crops, class-aware NMS, final business filter. No disk I/O, JSON parse, drawing, model loading or Engine build is timed. CPU/GPU copy and decoder are inside pipeline time. ROI wall stage includes concurrent preparation and inference; it is not a sum of stage percentiles. Managed allocations include async workers; process peak is cumulative; GPU telemetry is sampled outside measurements.",
            gpuBefore, gpuAfter = GpuSnapshot(), cases };
        File.WriteAllText(Path.Combine(output, "benchmark.json"), JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static double Ms(long start, long end) => Stopwatch.GetElapsedTime(start, end).TotalMilliseconds;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static Distribution Stats(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        return new Distribution(sorted.Average(), sorted[(int)Math.Ceiling(sorted.Length * .5) - 1],
            sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], sorted[0], sorted[^1]);
    }
    private static void EnsureEquivalent(DetectionResult reference, DetectionResult actual)
    {
        if (reference.Detections.Count != actual.Detections.Count) throw new InvalidDataException("Whole-ROI result count differs.");
        var available = actual.Detections.ToList();
        foreach (var a in reference.Detections)
        {
            int i = available.FindIndex(b => b.Label.Index == a.Label.Index && Math.Abs(a.Label.Score - b.Label.Score) < .005f &&
                Math.Abs(a.Box.X-b.Box.X) < 1 && Math.Abs(a.Box.Y-b.Box.Y) < 1 &&
                Math.Abs(a.Box.Width-b.Box.Width) < 1 && Math.Abs(a.Box.Height-b.Box.Height) < 1);
            if (i < 0) throw new InvalidDataException("Whole-ROI coordinates differ from the direct full-image path.");
            available.RemoveAt(i);
        }
    }
    private static string[] GpuSnapshot()
    {
        try
        {
            var start = new ProcessStartInfo("nvidia-smi") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add("--query-gpu=name,driver_version,pstate,clocks.current.graphics,clocks.current.memory,utilization.gpu,memory.used,power.draw");
            start.ArgumentList.Add("--format=csv,noheader");
            using var p = Process.Start(start)!;
            Task<string> stdout = p.StandardOutput.ReadToEndAsync(), stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(3000)) { p.Kill(true); p.WaitForExit(); return new[] { "nvidia-smi timed out" }; }
            return (p.ExitCode == 0 ? stdout.GetAwaiter().GetResult() : stderr.GetAwaiter().GetResult()).Trim().Split('\n');
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        { return new[] { e.GetType().Name + ": " + e.Message }; }
    }
    private sealed record Distribution(double Mean, double P50, double P95, double Minimum, double Maximum);
    private sealed record FullSample(double NoRoiMs, double WithFilterMs, double PreparationMs, double InferenceAndReleaseMs,
        double FilterMs, long ManagedAllocatedBytes, int FullCount, int FilteredCount);
    private sealed record CropSample(double TotalMs, double DecodeMs, double PrepareInferDecodeMs, double MergeMs,
        double FilterMs, double ReleaseMs, long ManagedAllocatedBytes, int Inputs, int RawCount, int MergedCount, int FilteredCount);
}
