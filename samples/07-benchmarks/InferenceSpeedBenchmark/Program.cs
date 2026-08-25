using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

internal static class Program
{
    private const int DefaultWarmup = 10;
    private const int DefaultIterations = 100;
    private static readonly string[] BackendNames = { "onnxruntime", "opencv-dnn", "openvino" };

    private static int Main(string[] args)
    {
        try
        {
            BenchmarkOptions options = BenchmarkOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            string onnxPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "classification.onnx");
            string irPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "ir", "classification.xml");
            InferenceInputs inputs = CreateInputs();
            var results = new List<BenchmarkResult>();
            foreach (string backend in options.Backends)
            {
                results.Add(RunBackend(backend, onnxPath, irPath, inputs, options));
            }

            foreach (BenchmarkResult result in results)
            {
                if (result.Status == "ok")
                {
                    Console.WriteLine(
                        "DEPLOYSHARP_BENCHMARK backend={0} status=ok p50_ms={1:F3} p95_ms={2:F3} throughput_per_second={3:F2} allocated_bytes_per_iteration={4:F0}",
                        result.Backend, result.P50Milliseconds, result.P95Milliseconds, result.ThroughputPerSecond, result.AllocatedBytesPerIteration);
                }
                else
                {
                    Console.WriteLine("DEPLOYSHARP_BENCHMARK backend={0} status={1} message={2}", result.Backend, result.Status, result.Message);
                }
            }

            if (options.OutputPath != null)
            {
                string fullOutputPath = Path.GetFullPath(options.OutputPath);
                string? directory = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(fullOutputPath, JsonSerializer.Serialize(new BenchmarkReport(options, results), new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("DEPLOYSHARP_BENCHMARK_REPORT path=" + fullOutputPath);
            }

            return results.Any(result => result.Status == "ok") ? 0 : 3;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine("DEPLOYSHARP_BENCHMARK_USAGE_ERROR " + exception.Message);
            PrintUsage();
            return 2;
        }
    }

    private static BenchmarkResult RunBackend(string backend, string onnxPath, string irPath, InferenceInputs inputs, BenchmarkOptions options)
    {
        try
        {
            if (backend == "onnxruntime") return Measure(backend, "cpu", OpenOnnxRuntime(onnxPath), inputs, options);
            if (backend == "opencv-dnn") return Measure(backend, "cpu", OpenOpenCv(onnxPath), inputs, options);
            if (backend == "openvino") return Measure(backend, "CPU", OpenOpenVino(irPath), inputs, options);
            throw new ArgumentException("Unknown backend: " + backend);
        }
        catch (Exception exception)
        {
            return BenchmarkResult.Unavailable(backend, options, exception);
        }
    }

    private static IInferenceSession OpenOnnxRuntime(string path)
    {
        var provider = new OnnxRuntimeBackendProvider();
        try
        {
            return provider.CreateSession(
                new ModelArtifact(new ModelId("benchmark/classification"), "onnx", path),
                new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"),
                new SessionOptions());
        }
        finally { provider.Dispose(); }
    }

    private static IInferenceSession OpenOpenCv(string path)
    {
        var modelId = new ModelId("benchmark/classification");
        var contract = new OpenCvDnnModelContract(
            modelId,
            new[] { new TensorDescriptor("images", TensorElementType.Float32, new TensorShape(1, 3, 2, 2)) },
            new[] { new TensorDescriptor("scores", TensorElementType.Float32, new TensorShape(1, 3)) });
        var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
        try
        {
            return provider.CreateSession(
                new ModelArtifact(modelId, "onnx", path),
                new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                new SessionOptions());
        }
        finally { provider.Dispose(); }
    }

    private static IInferenceSession OpenOpenVino(string path)
    {
        var provider = new OpenVinoBackendProvider(new OpenVinoOptions(performanceHint: OpenVinoPerformanceHint.Latency));
        try
        {
            return provider.CreateSession(
                new ModelArtifact(new ModelId("benchmark/classification"), "openvino-ir", path),
                new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"),
                new SessionOptions());
        }
        finally { provider.Dispose(); }
    }

    private static BenchmarkResult Measure(string backend, string device, IInferenceSession session, InferenceInputs inputs, BenchmarkOptions options)
    {
        using (session)
        {
            for (int index = 0; index < options.Warmup; index++) session.Run(inputs, CancellationToken.None);

            var timings = new List<double>(options.Iterations);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < options.Iterations; index++)
            {
                long start = Stopwatch.GetTimestamp();
                session.Run(inputs, CancellationToken.None);
                timings.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            timings.Sort();
            double average = timings.Average();
            return BenchmarkResult.Success(backend, device, options, timings[0], Percentile(timings, 0.50), Percentile(timings, 0.95), timings[timings.Count - 1], average, average <= 0 ? 0 : 1000d / average, allocatedBytes / (double)options.Iterations);
        }
    }

    private static InferenceInputs CreateInputs()
    {
        return InferenceInputs.Create("images", new Tensor<float>(new TensorShape(1, 3, 2, 2), new[]
        {
            1f, 1f, 1f, 1f,
            2f, 2f, 2f, 2f,
            3f, 3f, 3f, 3f
        }));
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 1) return sorted[0];
        double position = (sorted.Count - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj -c Release -- [options]");
        Console.WriteLine("  --backend <all|onnxruntime|opencv-dnn|openvino|comma-list>  Backend(s) to measure (default: all)");
        Console.WriteLine("  --warmup <count>                                           Warmup iterations (default: 10)");
        Console.WriteLine("  --iterations <count>                                       Timed iterations (default: 100)");
        Console.WriteLine("  --output <path>                                            Write a JSON report");
        Console.WriteLine("  --help                                                     Show this help");
    }

    private sealed class BenchmarkOptions
    {
        private BenchmarkOptions(IReadOnlyList<string> backends, int warmup, int iterations, string? outputPath, bool showHelp)
        {
            Backends = backends;
            Warmup = warmup;
            Iterations = iterations;
            OutputPath = outputPath;
            ShowHelp = showHelp;
        }

        public IReadOnlyList<string> Backends { get; }
        public int Warmup { get; }
        public int Iterations { get; }
        public string? OutputPath { get; }
        public bool ShowHelp { get; }

        public static BenchmarkOptions Parse(string[] args)
        {
            string backendValue = "all";
            int warmup = DefaultWarmup;
            int iterations = DefaultIterations;
            string? output = null;
            bool help = false;
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (argument == "--help" || argument == "-h") { help = true; continue; }
                if (argument == "--backend") { backendValue = NextValue(args, ref index, argument); continue; }
                if (argument == "--warmup") { warmup = ParsePositive(NextValue(args, ref index, argument), argument); continue; }
                if (argument == "--iterations") { iterations = ParsePositive(NextValue(args, ref index, argument), argument); continue; }
                if (argument == "--output") { output = NextValue(args, ref index, argument); continue; }
                throw new ArgumentException("Unknown option: " + argument);
            }

            var selected = new List<string>();
            foreach (string value in backendValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalized = value.Trim().ToLowerInvariant();
                if (normalized == "all") { selected.Clear(); selected.AddRange(BackendNames); break; }
                if (!BackendNames.Contains(normalized, StringComparer.Ordinal)) throw new ArgumentException("Unsupported backend: " + value);
                if (!selected.Contains(normalized, StringComparer.Ordinal)) selected.Add(normalized);
            }
            if (selected.Count == 0) throw new ArgumentException("At least one backend is required.");
            return new BenchmarkOptions(selected, warmup, iterations, output, help);
        }

        private static string NextValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length) throw new ArgumentException("Missing value for " + option);
            index++;
            return args[index];
        }

        private static int ParsePositive(string value, string option)
        {
            if (!int.TryParse(value, out int result) || result <= 0) throw new ArgumentException(option + " must be a positive integer.");
            return result;
        }
    }

    private sealed class BenchmarkReport
    {
        public BenchmarkReport(BenchmarkOptions options, IReadOnlyList<BenchmarkResult> results)
        {
            TimestampUtc = DateTimeOffset.UtcNow;
            OperatingSystem = RuntimeInformation.OSDescription;
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString();
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
            Framework = RuntimeInformation.FrameworkDescription;
            Model = "classification.onnx / classification.xml";
            InputShape = "1x3x2x2";
            ProcessorCount = Environment.ProcessorCount;
            Warmup = options.Warmup;
            Iterations = options.Iterations;
            Results = results;
        }

        public DateTimeOffset TimestampUtc { get; }
        public string OperatingSystem { get; }
        public string OSArchitecture { get; }
        public string ProcessArchitecture { get; }
        public string Framework { get; }
        public string Model { get; }
        public string InputShape { get; }
        public int ProcessorCount { get; }
        public int Warmup { get; }
        public int Iterations { get; }
        public IReadOnlyList<BenchmarkResult> Results { get; }
    }

    private sealed class BenchmarkResult
    {
        private BenchmarkResult() { }

        public string Backend { get; private set; } = string.Empty;
        public string Device { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public int Warmup { get; private set; }
        public int Iterations { get; private set; }
        public double MinMilliseconds { get; private set; }
        public double P50Milliseconds { get; private set; }
        public double P95Milliseconds { get; private set; }
        public double MaxMilliseconds { get; private set; }
        public double AverageMilliseconds { get; private set; }
        public double ThroughputPerSecond { get; private set; }
        public double AllocatedBytesPerIteration { get; private set; }
        public string? Message { get; private set; }

        public static BenchmarkResult Success(string backend, string device, BenchmarkOptions options, double min, double p50, double p95, double max, double average, double throughput, double allocated)
        {
            return new BenchmarkResult { Backend = backend, Device = device, Status = "ok", Warmup = options.Warmup, Iterations = options.Iterations, MinMilliseconds = min, P50Milliseconds = p50, P95Milliseconds = p95, MaxMilliseconds = max, AverageMilliseconds = average, ThroughputPerSecond = throughput, AllocatedBytesPerIteration = allocated };
        }

        public static BenchmarkResult Unavailable(string backend, BenchmarkOptions options, Exception exception)
        {
            return new BenchmarkResult { Backend = backend, Device = backend == "openvino" ? "CPU" : "cpu", Status = "unavailable", Warmup = options.Warmup, Iterations = options.Iterations, Message = exception.GetType().Name + ": " + exception.Message };
        }
    }
}
