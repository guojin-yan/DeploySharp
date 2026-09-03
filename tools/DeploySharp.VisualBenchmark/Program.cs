using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.Anomalib;
using JYPPX.DeploySharp.Visual.Models.Detr;
using JYPPX.DeploySharp.Visual.Models.Yolo;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.TensorRT;

internal static class Program
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            if (options.Help)
            {
                PrintUsage();
                return 0;
            }

            DeviceSnapshot deviceBefore = DeviceSnapshot.Capture();
            var rows = new List<ResultRow>();
            foreach (string backend in options.Backends)
            {
                foreach (string kind in options.Kinds)
                {
                    try
                    {
                        BenchmarkCase item = BenchmarkCase.Create(kind, options.ModelPathFor(kind, backend), options.ImagePath, backend);
                        foreach (BenchmarkMode mode in options.Modes) rows.Add(Run(item, backend, mode, options));
                    }
                    catch (Exception exception)
                    {
                        foreach (BenchmarkMode mode in options.Modes) rows.Add(ResultRow.Failure(kind, backend, mode, DeviceFor(backend), "fail", DescribeFailure(exception)));
                    }
                }
            }

            string outputPath = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(ResultRow.Header);
                foreach (ResultRow row in rows) writer.WriteLine(row.ToCsv());
            }

            DeviceSnapshot deviceAfter = DeviceSnapshot.Capture();
            string jsonOutputPath = Path.GetFullPath(options.JsonOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(jsonOutputPath)!);
            var report = new BenchmarkReport
            {
                SchemaVersion = 1,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                DeviceBefore = deviceBefore,
                DeviceAfter = deviceAfter,
                Configuration = new BenchmarkConfiguration
                {
                    Kinds = options.Kinds,
                    Backends = options.Backends,
                    ImagePath = options.ImagePath,
                    ModelPaths = options.ModelPaths,
                    Warmup = options.Warmup,
                    Iterations = options.Iterations,
                    Modes = options.Modes.Select(mode => mode.ToString().ToLowerInvariant()).ToArray(),
                    OpenCvFusionEnabled = !string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_ENABLE_FUSION"), "0", StringComparison.Ordinal),
                    OpenCvWinogradEnabled = !string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_ENABLE_WINOGRAD"), "0", StringComparison.Ordinal),
                    CsvOutputPath = outputPath,
                    JsonOutputPath = jsonOutputPath
                },
                Rows = rows
            };
            File.WriteAllText(jsonOutputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            }), new UTF8Encoding(false));

            foreach (ResultRow row in rows) Console.WriteLine(row.ToLog());
            Console.WriteLine("DEPLOYSHARP_VISUAL_BENCHMARK_REPORT=" + outputPath);
            Console.WriteLine("DEPLOYSHARP_VISUAL_BENCHMARK_JSON=" + jsonOutputPath);
            return rows.Any(row => row.Status == "pass") ? 0 : 3;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine("DEPLOYSHARP_VISUAL_BENCHMARK_USAGE_ERROR " + exception.Message);
            PrintUsage();
            return 2;
        }
    }

    private static ResultRow Run(BenchmarkCase item, string backend, BenchmarkMode mode, Options options)
    {
        if (item.Profile.ModelFormat == "openvino-ir" && backend != "openvino")
            return ResultRow.Failure(item, backend, mode, DeviceFor(backend), "unsupported", "The OpenVINO IR artifact is admitted only by the OpenVINO backend.");
        if (!File.Exists(item.ModelPath)) return ResultRow.Unavailable(item, backend, mode, "model-not-found=" + item.ModelPath);
        if (!File.Exists(item.ImagePath)) return ResultRow.Unavailable(item, backend, mode, "image-not-found=" + item.ImagePath);
        // A few known graphs currently terminate the OpenCV DNN importer with a
        // native access violation. Do not let an aggregate matrix run take down
        // the process; isolated probes can opt in explicitly for diagnostics.
        bool allowUnsafeOpenCvProbe = string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_ALLOW_NATIVE_CRASH_PROBES"), "1", StringComparison.Ordinal);
        if (!allowUnsafeOpenCvProbe && backend == "opencv-dnn" && (item.Kind == "deimv2" || item.Kind == "ppyoloe" || item.Kind == "rfdetr" || item.Kind == "rfdetr-seg"))
        {
            string modelName = item.Kind == "deimv2" ? "DEIMv2" : item.Kind == "ppyoloe" ? "PP-YOLOE" : item.Kind.StartsWith("rfdetr", StringComparison.Ordinal) ? "RF-DETR" : item.Kind;
            return ResultRow.Failure(item, backend, mode, DeviceFor(backend), "unsupported", "OpenCV DNN 5.0 cannot safely import the dynamic Transformer shape graph for " + modelName + "; the matrix runner skipped this unsupported graph. The provider also preserves the raw graph and reports a managed DS-OCV-8002 diagnostic. Set DEPLOYSHARP_OPENCV_ALLOW_NATIVE_CRASH_PROBES=1 only for an isolated diagnostic probe.");
        }

        if (backend == "tensorrt-cuda") return RunTensorRtCuda(item, mode, options);

        try
        {
            using var registry = new BackendRegistry();
            VisualModelProfile runtimeProfile = backend == "tensorrt" ? WithModelFormat(item.Profile, "tensorrt-engine") : item.Profile;
            if (backend == "opencv-dnn")
            {
                runtimeProfile = PrepareOpenCvProfile(item);
            }
            BackendRequest request = RegisterBackend(registry, backend, item, runtimeProfile);
            var profiles = new VisualProfileRegistry();
            profiles.Register(runtimeProfile);
            profiles.Freeze();
            string modelFormat = backend == "tensorrt" ? "tensorrt-engine" : runtimeProfile.ModelFormat;
            ModelArtifact artifact = new ModelArtifact(runtimeProfile.ModelId, modelFormat, item.ModelPath, preferredBackend: request.BackendId);
            using var pipeline = new VisualPipeline(registry, profiles.Select(artifact, registry, request, runtimeProfile.Task), request);
            var factory = new OpenCvVisualInputFactory();
            OpenCvImageSource source = OpenCvImageSource.FromFile(item.ImagePath);

            PreparedVisualInput? steadyInput = null;
            Measurement setup = Measurement.Empty;
            try
            {
                if (mode == BenchmarkMode.Steady)
                {
                    long setupAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    Stopwatch setupWatch = Stopwatch.StartNew();
                    steadyInput = item.Prepare(factory, source);
                    setupWatch.Stop();
                    setup = Measurement.Setup(setupWatch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - setupAllocatedBefore);
                }

                for (int index = 0; index < options.Warmup; index++)
                {
                    if (steadyInput == null) RunOne(factory, pipeline, item, source);
                    else RunPreparedOne(pipeline, steadyInput);
                }
                var measurements = new List<Measurement>(options.Iterations);
                for (int index = 0; index < options.Iterations; index++)
                {
                    measurements.Add(steadyInput == null ? RunOne(factory, pipeline, item, source) : RunPreparedOne(pipeline, steadyInput));
                }
                Measurement measured = Measurement.Aggregate(measurements, setup);
                return ResultRow.Pass(item, backend, mode, request.Device ?? DeviceFor(backend), measured);
            }
            finally
            {
                steadyInput?.Dispose();
            }
        }
        catch (Exception exception)
        {
            string detail = DescribeFailure(exception);
            string status = backend == "opencv-dnn" ? "unsupported" : "unavailable";
            return ResultRow.Failure(item, backend, mode, DeviceFor(backend), status, detail);
        }
    }

    private static ResultRow RunTensorRtCuda(BenchmarkCase item, BenchmarkMode mode, Options options)
    {
        if (item.Profile.AuxiliaryInputs.Count > 0)
        {
            return ResultRow.Failure(item, "tensorrt-cuda", mode, "cuda", "unsupported", "The fused CUDA visual pipeline currently accepts one image tensor and cannot bind this model's auxiliary tensors.");
        }
        try
        {
            var backendOptions = new TensorRtBackendOptions(
                ResolveTensorRtApiVersion(),
                cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE"),
                cacheImmutableHostInputsOnDevice: true);
            TensorRtCudaVisualPostprocessingMode postprocessingMode = string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_CUDA_POSTPROCESSING"), "0", StringComparison.Ordinal)
                ? TensorRtCudaVisualPostprocessingMode.Disabled
                : TensorRtCudaVisualPostprocessingMode.WhenSupported;
            using var pipeline = new TensorRtVisualPipeline(item.Profile, item.ModelPath, item.Preprocessing, backendOptions, postprocessingMode);
            var factory = new OpenCvBgrImageFactory();
            if (pipeline.UsesCudaPostprocessing && string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_CUDA_VALIDATE_POSTPROCESSING"), "1", StringComparison.Ordinal))
            {
                ValidateTensorRtCudaPostprocessing(item, backendOptions, pipeline, factory);
            }
            OpenCvBgrImage? steadyImage = null;
            Measurement setup = Measurement.Empty;
            try
            {
                if (mode == BenchmarkMode.Steady)
                {
                    long setupAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    Stopwatch setupWatch = Stopwatch.StartNew();
                    steadyImage = factory.CreateFromFile(item.ImagePath, item.ImagePath);
                    setupWatch.Stop();
                    setup = Measurement.Setup(setupWatch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - setupAllocatedBefore);
                }

                for (int index = 0; index < options.Warmup; index++) RunTensorRtCudaOne(pipeline, factory, item.ImagePath, steadyImage);
                var measurements = new List<Measurement>(options.Iterations);
                for (int index = 0; index < options.Iterations; index++) measurements.Add(RunTensorRtCudaOne(pipeline, factory, item.ImagePath, steadyImage));
                return ResultRow.Pass(item, "tensorrt-cuda", mode, "cuda", Measurement.Aggregate(measurements, setup));
            }
            finally
            {
                // OpenCvBgrImage is an immutable managed byte owner; it has no native handle to release.
                GC.KeepAlive(steadyImage);
            }
        }
        catch (Exception exception)
        {
            return ResultRow.Failure(item, "tensorrt-cuda", mode, "cuda", "unavailable", DescribeFailure(exception));
        }
    }

    private static Measurement RunTensorRtCudaOne(TensorRtVisualPipeline pipeline, OpenCvBgrImageFactory factory, string imagePath, OpenCvBgrImage? steadyImage)
    {
        Stopwatch totalWatch = Stopwatch.StartNew();
        OpenCvBgrImage? image = steadyImage;
        long preprocessAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch decodeWatch = Stopwatch.StartNew();
        if (image == null) image = factory.CreateFromFile(imagePath, imagePath);
        decodeWatch.Stop();
        long preprocessAllocated = steadyImage == null ? GC.GetAllocatedBytesForCurrentThread() - preprocessAllocatedBefore : 0;
        long pipelineAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        VisualInferenceResult result = pipeline.Run(image);
        totalWatch.Stop();
        long pipelineAllocated = GC.GetAllocatedBytesForCurrentThread() - pipelineAllocatedBefore;
        InferenceTiming timing = result.Timing;
        double preprocess = decodeWatch.Elapsed.TotalMilliseconds + timing.Preprocessing.TotalMilliseconds;
        double orchestration = Math.Max(0d, totalWatch.Elapsed.TotalMilliseconds - preprocess - timing.Inference.TotalMilliseconds - timing.Postprocessing.TotalMilliseconds);
        return Measurement.Single(preprocess, timing.Inference.TotalMilliseconds, timing.Postprocessing.TotalMilliseconds, orchestration, totalWatch.Elapsed.TotalMilliseconds, preprocessAllocated, pipelineAllocated, ResultFingerprint(result.Value));
    }

    private static void ValidateTensorRtCudaPostprocessing(BenchmarkCase item, TensorRtBackendOptions backendOptions, TensorRtVisualPipeline accelerated, OpenCvBgrImageFactory factory)
    {
        using var baseline = new TensorRtVisualPipeline(item.Profile, item.ModelPath, item.Preprocessing, backendOptions, TensorRtCudaVisualPostprocessingMode.Disabled);
        OpenCvBgrImage image = factory.CreateFromFile(item.ImagePath, item.ImagePath);
        object expected = baseline.Run(image).Value;
        object actual = accelerated.Run(image).Value;
        if (expected is BackgroundRemovalResult expectedMatting && actual is BackgroundRemovalResult actualMatting)
        {
            ValidateFloatPlanes(item.Kind, expectedMatting.Alpha.ToArray(), actualMatting.Alpha.ToArray(), 0.0001f);
            return;
        }
        if (expected is AnomalyDetectionResult expectedAnomaly && actual is AnomalyDetectionResult actualAnomaly)
        {
            ValidateFloatPlanes(item.Kind, expectedAnomaly.NormalizedMap.ToArray(), actualAnomaly.NormalizedMap.ToArray(), 0.0001f);
            byte[] expectedMask = expectedAnomaly.Mask.ToArray();
            byte[] actualMask = actualAnomaly.Mask.ToArray();
            int differences = 0;
            for (int index = 0; index < expectedMask.Length; index++) if (expectedMask[index] != actualMask[index]) differences++;
            if (differences != 0) throw new InvalidOperationException("CUDA postprocessing changed " + differences.ToString(Invariant) + " anomaly-mask pixels.");
            return;
        }
        if (expected is InstanceSegmentationResult expectedInstances && actual is InstanceSegmentationResult actualInstances)
        {
            if (expectedInstances.Instances.Count != actualInstances.Instances.Count) throw new InvalidOperationException("CUDA postprocessing changed the retained instance count.");
            int differences = 0;
            for (int instanceIndex = 0; instanceIndex < expectedInstances.Instances.Count; instanceIndex++)
            {
                InstanceSegmentationInstance expectedInstance = expectedInstances.Instances[instanceIndex];
                InstanceSegmentationInstance actualInstance = actualInstances.Instances[instanceIndex];
                if (expectedInstance.SourceIndex != actualInstance.SourceIndex || expectedInstance.ClassIndex != actualInstance.ClassIndex
                    || expectedInstance.Score != actualInstance.Score || expectedInstance.BoundingBox != actualInstance.BoundingBox)
                {
                    throw new InvalidOperationException("CUDA postprocessing changed retained instance metadata at index " + instanceIndex.ToString(Invariant) + ".");
                }
                byte[] expectedMask = expectedInstance.Mask.ToArray();
                byte[] actualMask = actualInstance.Mask.ToArray();
                if (expectedMask.Length != actualMask.Length) throw new InvalidOperationException("CUDA postprocessing changed an instance-mask element count.");
                for (int pixel = 0; pixel < expectedMask.Length; pixel++) if (expectedMask[pixel] != actualMask[pixel]) differences++;
            }
            Console.WriteLine("DEPLOYSHARP_TENSORRT_CUDA_POSTPROCESSING_VALIDATION kind=" + item.Kind + " mask_pixel_differences=" + differences.ToString(Invariant));
            if (differences != 0) throw new InvalidOperationException("CUDA postprocessing changed " + differences.ToString(Invariant) + " instance-mask pixels.");
            return;
        }
        throw new InvalidOperationException("CUDA postprocessing validation does not recognize the decoded result type.");
    }

    private static void ValidateFloatPlanes(string kind, float[] expected, float[] actual, float tolerance)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("CUDA postprocessing changed the output element count.");
        float maximum = 0f;
        double total = 0d;
        for (int index = 0; index < expected.Length; index++)
        {
            float difference = Math.Abs(expected[index] - actual[index]);
            if (difference > maximum) maximum = difference;
            total += difference;
        }
        double mean = expected.Length == 0 ? 0d : total / expected.Length;
        Console.WriteLine("DEPLOYSHARP_TENSORRT_CUDA_POSTPROCESSING_VALIDATION kind=" + kind + " max_abs=" + maximum.ToString("0.000000000", Invariant) + " mean_abs=" + mean.ToString("0.000000000", Invariant));
        if (maximum > tolerance) throw new InvalidOperationException("CUDA postprocessing exceeded the semantic tolerance: max_abs=" + maximum.ToString("R", Invariant));
    }

    private static string DescribeFailure(Exception exception)
    {
        var details = new List<string>();
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            string value = current.GetType().Name + ": " + current.Message.Replace('\r', ' ').Replace('\n', ' ');
            if (current is DeploySharpException deploySharp)
            {
                if (!string.IsNullOrWhiteSpace(deploySharp.ErrorCode)) value += " [code=" + deploySharp.ErrorCode + "]";
                if (deploySharp is VisualException visual && !string.IsNullOrWhiteSpace(visual.TensorName)) value += " [tensor=" + visual.TensorName + "]";
                if (!string.IsNullOrWhiteSpace(deploySharp.TechnicalDetails)) value += " [details=" + deploySharp.TechnicalDetails.Replace('\r', ' ').Replace('\n', ' ') + "]";
            }
            details.Add(value);
        }
        if (details.Count == 1 && exception.InnerException == null) return details[0] + " [stack=" + exception.StackTrace?.Replace('\r', ' ').Replace('\n', ' ') + "]";
        return string.Join(" <- ", details);
    }

    private static BackendRequest RegisterBackend(BackendRegistry registry, string backend, BenchmarkCase item, VisualModelProfile? runtimeProfile = null)
    {
        VisualModelProfile profile = runtimeProfile ?? item.Profile;
        if (backend == "onnxruntime" || backend == "onnxruntime-cuda")
        {
            OnnxRuntimeExecutionProvider executionProvider = backend == "onnxruntime-cuda" ? OnnxRuntimeExecutionProvider.Cuda : OnnxRuntimeExecutionProvider.Cpu;
            registry.UseOnnxRuntime(new OnnxRuntimeOptions(executionProvider: executionProvider, cudaDeviceId: 0));
            return new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, executionProvider == OnnxRuntimeExecutionProvider.Cuda ? "cuda" : "cpu");
        }
        if (backend == "openvino")
        {
            registry.UseOpenVino();
            return new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
        }
        if (backend == "opencv-dnn")
        {
            var inputDescriptors = new List<TensorDescriptor>(1 + profile.AuxiliaryInputs.Count)
            {
                new TensorDescriptor(profile.Input.Name, profile.Input.ElementType, profile.Input.ShapePattern)
            };
            foreach (VisualAuxiliaryInputBinding auxiliary in profile.AuxiliaryInputs)
                inputDescriptors.Add(new TensorDescriptor(auxiliary.Name, auxiliary.ElementType, auxiliary.ShapePattern));
            var contract = new OpenCvDnnModelContract(
                profile.ModelId,
                inputDescriptors,
                item.OpenCvOutputs ?? profile.Outputs.Select(output => new TensorDescriptor(output.Name, output.ElementType, output.ShapePattern)),
                imageInputNames: new[] { profile.Input.Name });
            bool enableFusion = !string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_ENABLE_FUSION"), "0", StringComparison.Ordinal);
            bool enableWinograd = !string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_ENABLE_WINOGRAD"), "0", StringComparison.Ordinal);
            // Production defaults remain enabled. The explicit environment switches
            // permit controlled compatibility A/B runs for graphs whose native fused
            // implementation may differ numerically from the unfused ONNX path.
            registry.UseOpenCvDnn(new OpenCvDnnOptions(contract, enableFusion, enableWinograd));
            return new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
        }
        if (backend == "tensorrt")
        {
            registry.UseTensorRT(new TensorRtBackendOptions(
                ResolveTensorRtApiVersion(),
                cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE"),
                cacheImmutableHostInputsOnDevice: true));
            return new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
        }
        throw new ArgumentException("Unknown backend: " + backend);
    }

    private static VisualModelProfile PrepareOpenCvProfile(BenchmarkCase item)
    {
        VisualModelProfile source = item.Profile;
        bool dynamicSpatial = item.Kind == "rmbg20" || item.Kind == "rmbg20-int8";
        long[] inputDimensions = source.Input.ShapePattern.ToArray();
        bool inputChanged = false;
        for (int index = 0; index < inputDimensions.Length; index++)
        {
            if (inputDimensions[index] >= 0) continue;
            long replacement = index == 0 ? 1L : dynamicSpatial && (index == 2 || index == 3) ? 1024L : inputDimensions[index];
            if (replacement > 0)
            {
                inputDimensions[index] = replacement;
                inputChanged = true;
            }
        }

        var outputs = new List<VisualOutputBinding>(source.Outputs.Count);
        bool outputChanged = false;
        foreach (VisualOutputBinding output in source.Outputs)
        {
            long[] dimensions = output.ShapePattern.ToArray();
            for (int index = 0; index < dimensions.Length; index++)
            {
                if (dimensions[index] >= 0) continue;
                long replacement = index == 0 ? 1L : dynamicSpatial && dimensions.Length >= 4 && (index == 2 || index == 3) ? 1024L : dimensions[index];
                if (replacement > 0)
                {
                    dimensions[index] = replacement;
                    outputChanged = true;
                }
            }
            outputs.Add(new VisualOutputBinding(output.Name, output.ElementType, new TensorShape(dimensions)));
        }

        bool auxiliaryChanged = source.AuxiliaryInputs.Any(input => input.ShapePattern.IsDynamic);
        if (!inputChanged && !outputChanged && !auxiliaryChanged) return source;
        return new VisualModelProfile(
            source.ProfileId + ".opencv-static",
            source.ModelId,
            source.Task,
            source.Version,
            source.ModelFormat,
            new VisualInputBinding(source.Input.Name, source.Input.ElementType, new TensorShape(inputDimensions), source.Input.Layout, inputChanged ? 1 : source.Input.MinimumBatch, inputChanged ? 1 : source.Input.MaximumBatch),
            outputs,
            source.Labels,
            source.Decoder,
            source.RequiredCapabilities,
            source.MinimumBackendVersion,
            StaticizeAuxiliaryInputs(source.AuxiliaryInputs));
    }

    private static TensorRtApiVersion ResolveTensorRtApiVersion()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API");
        if (string.IsNullOrWhiteSpace(value)) return TensorRtApiVersion.TensorRt11;
        if (value == "8") return TensorRtApiVersion.TensorRt8;
        if (value == "10") return TensorRtApiVersion.TensorRt10;
        if (value == "11") return TensorRtApiVersion.TensorRt11;
        throw new ArgumentException("DEPLOYSHARP_TENSORRT_API must be 8, 10, or 11.");
    }

    private static VisualModelProfile WithModelFormat(VisualModelProfile source, string modelFormat)
    {
        return new VisualModelProfile(
            source.ProfileId + "." + modelFormat,
            source.ModelId,
            source.Task,
            source.Version,
            modelFormat,
            source.Input,
            source.Outputs,
            source.Labels,
            source.Decoder,
            source.RequiredCapabilities,
            source.MinimumBackendVersion,
            source.AuxiliaryInputs);
    }

    private static IReadOnlyList<VisualAuxiliaryInputBinding> StaticizeAuxiliaryInputs(IReadOnlyList<VisualAuxiliaryInputBinding> inputs)
    {
        if (inputs.Count == 0) return inputs;
        var result = new List<VisualAuxiliaryInputBinding>(inputs.Count);
        foreach (VisualAuxiliaryInputBinding input in inputs)
        {
            long[] dimensions = input.ShapePattern.ToArray();
            bool changed = false;
            for (int index = 0; index < dimensions.Length; index++)
            {
                if (dimensions[index] >= 0) continue;
                dimensions[index] = 1;
                changed = true;
            }
            result.Add(changed ? new VisualAuxiliaryInputBinding(input.Name, input.ElementType, new TensorShape(dimensions)) : input);
        }
        return result;
    }

    private static Measurement RunOne(OpenCvVisualInputFactory factory, VisualPipeline pipeline, BenchmarkCase item, OpenCvImageSource source)
    {
        long preAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch preprocessWatch = Stopwatch.StartNew();
        using PreparedVisualInput input = item.Prepare(factory, source);
        preprocessWatch.Stop();
        long preprocessAllocated = GC.GetAllocatedBytesForCurrentThread() - preAllocatedBefore;

        long pipelineAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch runWatch = Stopwatch.StartNew();
        VisualInferenceResult result = pipeline.Run(input);
        runWatch.Stop();
        long pipelineAllocated = GC.GetAllocatedBytesForCurrentThread() - pipelineAllocatedBefore;
        InferenceTiming timing = result.Timing;
        double orchestration = Math.Max(0d, runWatch.Elapsed.TotalMilliseconds - timing.Inference.TotalMilliseconds - timing.Postprocessing.TotalMilliseconds);
        return Measurement.Single(preprocessWatch.Elapsed.TotalMilliseconds, timing.Inference.TotalMilliseconds, timing.Postprocessing.TotalMilliseconds, orchestration, runWatch.Elapsed.TotalMilliseconds + preprocessWatch.Elapsed.TotalMilliseconds, preprocessAllocated, pipelineAllocated, ResultFingerprint(result.Value));
    }

    private static Measurement RunPreparedOne(VisualPipeline pipeline, PreparedVisualInput input)
    {
        long pipelineAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch runWatch = Stopwatch.StartNew();
        VisualInferenceResult result = pipeline.Run(input);
        runWatch.Stop();
        long pipelineAllocated = GC.GetAllocatedBytesForCurrentThread() - pipelineAllocatedBefore;
        InferenceTiming timing = result.Timing;
        double orchestration = Math.Max(0d, runWatch.Elapsed.TotalMilliseconds - timing.Inference.TotalMilliseconds - timing.Postprocessing.TotalMilliseconds);
        return Measurement.Single(0d, timing.Inference.TotalMilliseconds, timing.Postprocessing.TotalMilliseconds, orchestration, runWatch.Elapsed.TotalMilliseconds, 0, pipelineAllocated, ResultFingerprint(result.Value));
    }

    private static string ResultFingerprint(object value)
    {
        string hash = value switch
        {
            InstanceSegmentationResult segmentation => segmentation.ComputeSha256(),
            PoseEstimationResult pose => pose.ComputeSha256(),
            OrientedDetectionResult oriented => oriented.ComputeSha256(),
            AnomalyDetectionResult anomaly => anomaly.ComputeSha256(),
            BackgroundRemovalResult background => background.Alpha.ComputeSha256(),
            JYPPX.DeploySharp.Results.Vision.DetectionResult detection => DetectionFingerprint(detection),
            JYPPX.DeploySharp.Results.Vision.ClassificationResult classification => ClassificationFingerprint(classification),
            _ => value.GetType().FullName ?? value.GetType().Name
        };
        return hash + ";result_summary=" + ResultSummary(value);
    }

    private static string ResultSummary(object value)
    {
        if (value is JYPPX.DeploySharp.Results.Vision.DetectionResult detection)
        {
            float top = detection.Detections.Count == 0 ? 0f : detection.Detections[0].Label.Score;
            string summary = string.Format(Invariant, "count={0}|top={1:F6}", detection.Detections.Count, top);
            if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_RESULT_DETAILS"), "1", StringComparison.Ordinal)) return summary;
            return summary + "|detections=" + string.Join(";", detection.Detections.Select(item => string.Format(
                Invariant,
                "{0}:{1:F6}:{2:F3},{3:F3},{4:F3},{5:F3}",
                item.Label.Index,
                item.Label.Score,
                item.Box.X,
                item.Box.Y,
                item.Box.Width,
                item.Box.Height)));
        }
        if (value is JYPPX.DeploySharp.Results.Vision.ClassificationResult classification)
        {
            float top = classification.Predictions.Count == 0 ? 0f : classification.Predictions[0].Score;
            return string.Format(Invariant, "count={0}|top={1:F6}", classification.Predictions.Count, top);
        }
        if (value is InstanceSegmentationResult segmentation)
        {
            float top = segmentation.Instances.Count == 0 ? 0f : segmentation.Instances[0].Score;
            return string.Format(Invariant, "count={0}|top={1:F6}", segmentation.Instances.Count, top);
        }
        if (value is PoseEstimationResult pose)
        {
            float top = pose.Instances.Count == 0 ? 0f : pose.Instances[0].Score;
            return string.Format(Invariant, "count={0}|top={1:F6}", pose.Instances.Count, top);
        }
        if (value is OrientedDetectionResult oriented)
        {
            float top = oriented.Detections.Count == 0 ? 0f : oriented.Detections[0].Score;
            return string.Format(Invariant, "count={0}|top={1:F6}", oriented.Detections.Count, top);
        }
        if (value is AnomalyDetectionResult anomaly) return string.Format(Invariant, "score={0:F6}|ratio={1:F6}", anomaly.ImageScore, anomaly.AnomalousPixelRatio);
        if (value is BackgroundRemovalResult background)
        {
            float[] alpha = background.Alpha.ToArray();
            return string.Format(Invariant, "pixels={0}|mean={1:F6}", alpha.Length, alpha.Average(item => (double)item));
        }
        return value.GetType().Name;
    }

    private static string DetectionFingerprint(JYPPX.DeploySharp.Results.Vision.DetectionResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(result.Detections.Count);
            foreach (JYPPX.DeploySharp.Results.Vision.Detection detection in result.Detections)
            {
                writer.Write(detection.Label.Index);
                writer.Write(detection.Label.Label);
                writer.Write(detection.Label.Score);
                writer.Write(detection.Box.X);
                writer.Write(detection.Box.Y);
                writer.Write(detection.Box.Width);
                writer.Write(detection.Box.Height);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)))).ToLowerInvariant();
    }

    private static string ClassificationFingerprint(JYPPX.DeploySharp.Results.Vision.ClassificationResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(result.Predictions.Count);
            foreach (JYPPX.DeploySharp.Results.LabelScore prediction in result.Predictions)
            {
                writer.Write(prediction.Index);
                writer.Write(prediction.Label);
                writer.Write(prediction.Score);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)))).ToLowerInvariant();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project tools/DeploySharp.VisualBenchmark/DeploySharp.VisualBenchmark.csproj -c Release -- --kind <catalog-kind|all> --image <path> [options]");
        Console.WriteLine("  --model <path>                         Model path when exactly one kind is selected");
        Console.WriteLine("  --model-<kind> <path>                  Model path for a case when --kind all is used");
        Console.WriteLine("  --backend <all|onnxruntime|onnxruntime-cuda|openvino|opencv-dnn|tensorrt|tensorrt-cuda|comma-list>  Backend(s), default all");
        Console.WriteLine("  --warmup <count> --iterations <count>  Defaults: 3 and 10");
        Console.WriteLine("  --mode <cold|steady|both>              Cold includes decode/preprocess per call; steady reuses one prepared input; default cold");
        Console.WriteLine("  --output <path>                        CSV report path");
        Console.WriteLine("  --json-output <path>                   JSON report with device information and rows");
    }

    private static string DeviceFor(string backend) => backend == "openvino" ? "CPU" : backend == "onnxruntime-cuda" || backend == "tensorrt" || backend == "tensorrt-cuda" ? "cuda" : "cpu";

    private sealed class BenchmarkCase
    {
        private readonly Func<OpenCvVisualInputFactory, OpenCvImageSource, PreparedVisualInput>? _prepare;

        private BenchmarkCase(string kind, string modelPath, string imagePath, VisualModelProfile profile, OpenCvPreprocessOptions preprocessing, IReadOnlyList<TensorDescriptor>? openCvOutputs = null, Func<OpenCvVisualInputFactory, OpenCvImageSource, PreparedVisualInput>? prepare = null)
        {
            Kind = kind; ModelPath = modelPath; ImagePath = imagePath; Profile = profile; Preprocessing = preprocessing; OpenCvOutputs = openCvOutputs; _prepare = prepare;
        }

        public string Kind { get; }
        public string ModelPath { get; }
        public string ImagePath { get; }
        public VisualModelProfile Profile { get; }
        public OpenCvPreprocessOptions Preprocessing { get; }
        public IReadOnlyList<TensorDescriptor>? OpenCvOutputs { get; }

        public PreparedVisualInput Prepare(OpenCvVisualInputFactory factory, OpenCvImageSource source)
            => _prepare == null ? factory.Create(source, Profile.Input.Name, Preprocessing) : _prepare(factory, source);

        public static BenchmarkCase Create(string kind, string modelPath, string imagePath, string backend)
        {
            DetectionDefinition? detection = DetectionDefinitions.FirstOrDefault(item => item.Kind == kind);
            if (detection != null)
            {
                bool openCvYoloV7Raw = backend == "opencv-dnn" && detection.Family == YoloDetectionFamily.YoloV7;
                var options = openCvYoloV7Raw
                    ? new YoloDetectionProfileOptions(
                        detection.Opset,
                        outputName: "onnx_node!/model/model.105/Concat_3",
                        postprocessingVersion: "deploysharp-yolov7-opencv-raw-v1",
                        outputKind: YoloDetectionOutputKind.RawCandidateMajor)
                    : new YoloDetectionProfileOptions(detection.Opset);
                YoloDetectionProfile profile = YoloDetectionProfiles.Create(detection.Family, new ModelId(detection.ModelId), detection.Sha256, YoloLabelSets.Coco80, detection.UpstreamCommit, detection.ExporterVersion, options);
                return new BenchmarkCase(kind, modelPath, imagePath, profile.VisualProfile, OpenCvYoloPreprocessing.CreateOptions(profile));
            }

            MultiTaskDefinition? multiTask = MultiTaskDefinitions.FirstOrDefault(item => item.Kind == kind);
            if (multiTask != null)
            {
                YoloMultiTaskProfile profile = CreateMultiTask(multiTask);
                return new BenchmarkCase(kind, modelPath, imagePath, profile.VisualProfile, OpenCvYoloPreprocessing.CreateOptions(profile));
            }

            PortableDefinition? portable = PortableDefinitions.FirstOrDefault(item => item.Kind == kind);
            if (portable != null)
            {
                PortableDetectorProfile profile = CreatePortable(portable, backend);
                return new BenchmarkCase(
                    kind,
                    modelPath,
                    imagePath,
                    profile.VisualProfile,
                    OpenCvPortableDetectorPreprocessing.CreateOptions(profile),
                    prepare: (factory, source) => OpenCvPortableDetectorPreprocessing.Create(factory, source, profile));
            }

            if (kind == "padim")
            {
                AnomalibProfile profile = AnomalibProfiles.CreatePadim(new ModelId("benchmark/padim"), new AnomalibArtifactContract(14, Hash("bde19ca3086d3fa52bb3cbc2b9ea2d554ce1f10b4c8a8b38d7393bd54247ffff"), "ffde4cce", "torch-2.7.1"));
                return new BenchmarkCase(kind, modelPath, imagePath, profile.VisualProfile, OpenCvStage19Preprocessing.CreateAnomalibOptions(profile), new[]
                {
                    new TensorDescriptor("pred_score", TensorElementType.Float32, new TensorShape(1, 1)),
                    new TensorDescriptor("pred_label", TensorElementType.Boolean, new TensorShape(1, 1)),
                    new TensorDescriptor("anomaly_map", TensorElementType.Float32, new TensorShape(1, 1, 256, 256)),
                    new TensorDescriptor("pred_mask", TensorElementType.Boolean, new TensorShape(1, 1, 256, 256))
                });
            }
            if (kind == "rmbg14")
            {
                BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg14(new ModelId("benchmark/rmbg14"), new BriaRmbgProfileOptions(11, new VisualSize(1024, 1024), "input", "output", Hash("8cafcf770b06757c4eaced21b1a88e57fd2b66de01b8045f35f01535ba742e0f"), "2ceba5a5", "torch-2.1.0", "LicenseRef-BRIA-RMBG-1.4"));
                return new BenchmarkCase(kind, modelPath, imagePath, profile.VisualProfile, OpenCvStage19Preprocessing.CreateBriaRmbgOptions(profile));
            }
            if (kind == "rmbg20" || kind == "rmbg20-int8")
            {
                string sha256 = kind == "rmbg20" ? "5b486f08200f513f460da46dd701db5fbb47d79b4be4b708a19444bcd4e79958" : "fcea23951a378f92634834888896cc1eec54655366ae6e949282646ce17c5420";
                string exporter = kind == "rmbg20" ? "local-exporter-unverified" : "onnx.quantize";
                BriaRmbgProfile profile = BriaRmbgProfiles.CreateRmbg20(new ModelId("benchmark/" + kind), new BriaRmbgProfileOptions(14, new VisualSize(1024, 1024), "pixel_values", "alphas", sha256, "5df4c9c7", exporter, "LicenseRef-BRIA-RMBG-2.0"));
                return new BenchmarkCase(kind, modelPath, imagePath, profile.VisualProfile, OpenCvStage19Preprocessing.CreateBriaRmbgOptions(profile));
            }
            throw new ArgumentException("Unsupported kind: " + kind);
        }

        private static YoloMultiTaskProfile CreateMultiTask(MultiTaskDefinition item)
        {
            var id = new ModelId(item.ModelId);
            if (item.Task == "classification")
            {
                return YoloMultiTaskProfiles.CreateClassification(id, item.Sha256, Enumerable.Range(0, 1000).Select(index => "class" + index.ToString(Invariant)), item.UpstreamCommit, item.ExporterVersion, new YoloClassificationProfileOptions(item.Opset, item.ModelSize, topK: 5));
            }

            var decoder = new YoloPackedDecoderOptions(scoreThreshold: .25f, iouThreshold: .45f, maximumCandidates: item.CandidateCount, maximumDetections: 100, maximumWorkspaceBytes: 512L * 1024 * 1024);
            var options = new YoloPackedProfileOptions(item.Opset, item.CandidateCount, item.ModelSize, decoderOptions: decoder, profileId: "benchmark." + item.Kind);
            if (item.Task == "segment") return YoloMultiTaskProfiles.CreateInstanceSegmentation(item.Family, id, item.Sha256, YoloLabelSets.Coco80, item.UpstreamCommit, item.ExporterVersion, options);
            if (item.Task == "pose") return YoloMultiTaskProfiles.CreatePose(item.Family, id, item.Sha256, item.UpstreamCommit, item.ExporterVersion, options);
            return YoloMultiTaskProfiles.CreateObb(item.Family, id, item.Sha256, YoloLabelSets.Dota15, item.UpstreamCommit, item.ExporterVersion, options);
        }

        private static PortableDetectorProfile CreatePortable(PortableDefinition item, string backend)
        {
            bool openVino = backend == "openvino";
            string inputName = openVino && (item.Family == PortableDetectorFamily.RFDETRDet || item.Family == PortableDetectorFamily.RFDETRSeg) ? "/backbone/backbone.0/encoder/encoder/embeddings/Cast_output_0" : item.InputName;
            string? masksName = openVino && item.Family == PortableDetectorFamily.RFDETRSeg ? "/segmentation_head/Einsum_output_0" : item.MasksName;
            string modelFormat = item.ModelFormat;
            var options = new PortableDetectorProfileOptions(
                item.Opset,
                item.ModelSize,
                item.Labels,
                modelFormat: modelFormat,
                inputName: inputName,
                artifactSha256: item.Sha256,
                upstreamRepository: "catalog-benchmark",
                upstreamCommit: "catalog-artifact",
                exporterVersion: "catalog-artifact",
                license: "External",
                scoreThreshold: item.ScoreThreshold,
                maximumCandidates: 3000,
                maximumResults: 300,
                topK: 300,
                boxesOutputName: item.BoxesName,
                labelsOutputName: item.LabelsName,
                masksOutputName: masksName,
                countOutputName: item.CountName,
                rfDetrQueryCount: item.QueryCount,
                rfDetrIncludesNoObjectClass: item.IncludesNoObject,
                hasDynamicBatchAxis: item.DynamicBatch,
                paddleCountShape: item.CountShape);
            var id = new ModelId(item.ModelId);
            if (item.Family == PortableDetectorFamily.DEIMv2Det) return PortableDetectorProfiles.CreateDEIMv2(id, options);
            if (item.Family == PortableDetectorFamily.RFDETRDet) return PortableDetectorProfiles.CreateRFDETR(id, options);
            if (item.Family == PortableDetectorFamily.RFDETRSeg) return PortableDetectorProfiles.CreateRFDETRSeg(id, options);
            if (item.RawQuery) return PortableDetectorProfiles.CreateRTDETRRaw(id, options);
            if (item.Family == PortableDetectorFamily.RTDETRDet) return PortableDetectorProfiles.CreateRTDETR(id, options);
            return PortableDetectorProfiles.CreatePPYOLOE(id, options);
        }

        private static readonly DetectionDefinition[] DetectionDefinitions =
        {
            new("yolov5n", "yolo/v5/detect/n", YoloDetectionFamily.YoloV5, "1cad0ece41bc351e2e1a3bd9b244dc4219f1b7b4d322928f13b6e7d19a00ef9d", "20d1d78a08277e365d57bfa3a2cce752772d9e59", "local-onnx-export", 12),
            new("yolov6s", "yolo/v6/detect/s", YoloDetectionFamily.YoloV6, "f6fddae83fb23ff02578d5b5e9f4eb9d68b5d8e7f469bb80edf4041681c757f6", "e86a483f3f6bded25d45970b56831345a99744a4", "local-onnx-export", 12),
            new("yolov7", "yolo/v7/detect/base", YoloDetectionFamily.YoloV7, "8ee07ed4aa95070ae1c9e7a37c2407c2aa065e989f887cb1193bcb117603c641", "a207844b1ce82d204ab36d87d496728d3d2348e7", "local-onnx-export", 12),
            new("yolov8n", "yolo/v8/detect/n", YoloDetectionFamily.YoloV8, "50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4", "1367566337fb8056223a1aeb469360747f1b1bcd", "8.3.78", 19),
            new("yolov9s", "yolo/v9/detect/s", YoloDetectionFamily.YoloV9, "e985aab9f5031b5e34e1846b1ed9535de23e77b792c70680010979eb5d98f6c7", "5b1ea9a8b3f0ffe4fe0e203ec6232d788bb3fcff", "8.3.78", 19),
            new("yolov10n", "yolo/v10/detect/n", YoloDetectionFamily.YoloV10, "908f513fda6e38eeb4230d53d1fcea1d7e068b8cec4b7bbd4e818f704320ca81", "453c6e38a51e9d1d5a2aa5fb7f1014a711913397", "8.3.78", 19),
            new("yolo11n", "yolo/v11/detect/n", YoloDetectionFamily.YoloV11, "7060132736a0e5856a8b91d68fd7558ac6daf8c5fb7cec46dbc9cb034f8409c3", "1367566337fb8056223a1aeb469360747f1b1bcd", "8.3.78", 19),
            new("yolo12n", "yolo/v12/detect/n", YoloDetectionFamily.YoloV12, "9a99a764c60423ffaef870bf22687c66da284c6b2ad7f249605ced9c8a2a3e80", "01a22c0603e0eaa6d9bd62120a391e744d92cea2", "8.3.78", 19),
            new("yolo13n", "yolo/v13/detect/n", YoloDetectionFamily.YoloV13, "a589a4e351e9f9be6712ba4d6831cfbcc16b7ac58d6498c02a8386eca828cf80", "73289949533efac82bb5f72ec19b746618656bd2", "8.3.63", 17),
            new("yolo26n", "yolo/v26/detect/n", YoloDetectionFamily.YoloV26, "bd169d41c0c04abe18bc1ea6220ff295cf77a38c165071b1acc76ee6ef0c10c4", "1367566337fb8056223a1aeb469360747f1b1bcd", "8.4.0", 19)
        };

        private static readonly MultiTaskDefinition[] MultiTaskDefinitions =
        {
            new("yolov8s-cls", "yolo/v8/classify/s", "classification", YoloDetectionFamily.YoloV8, "6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee", "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6", 17, new VisualSize(224, 224), 1000),
            new("yolov5s-seg", "yolo/v5/segment/s", "segment", YoloDetectionFamily.YoloV5, "ab44adf19119521f4764966a48f76fbac9125d22f5db776589bf049b49267576", "20d1d78a08277e365d57bfa3a2cce752772d9e59", "local-pytorch2.1.2-export", 17, new VisualSize(640, 640), 25200),
            new("yolov8n-seg", "yolo/v8/segment/n", "segment", YoloDetectionFamily.YoloV8, "986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2", "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.0.119", 12, new VisualSize(640, 640), 8400),
            new("yolov9c-seg", "yolo/v9/segment/c", "segment", YoloDetectionFamily.YoloV9, "2cc4ea632009115d72f30841d7295d5ca064cc9697a2fb4efbea3ce41ac0a2a0", "5b1ea9a8b3f0ffe4fe0e203ec6232d788bb3fcff", "local-pytorch2.2.1-export", 12, new VisualSize(640, 640), 8400),
            new("yolo11s-seg", "yolo/v11/segment/s", "segment", YoloDetectionFamily.YoloV11, "0707f946915fcdfdbc5438d1f45ca446e70d388805e422ac849996240880fe48", "636685ace98527cd0113656fd024a82291fa3122", "8.3.24", 19, new VisualSize(640, 640), 8400),
            new("yolo26s-seg", "yolo/v26/segment/s", "segment", YoloDetectionFamily.YoloV26, "79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f", "6f6158be448c73471c000cf41db5cd9169300ed9", "8.4.0-end2end", 19, new VisualSize(640, 640), 300),
            new("yolov8s-pose", "yolo/v8/pose/s", "pose", YoloDetectionFamily.YoloV8, "253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b", "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6", 17, new VisualSize(640, 640), 8400),
            new("yolo11s-pose", "yolo/v11/pose/s", "pose", YoloDetectionFamily.YoloV11, "5b8d5bce3dff5ac176ea922faf14705fa46fa3b0d3a4b7974b765c355806bae5", "636685ace98527cd0113656fd024a82291fa3122", "8.3.24", 19, new VisualSize(640, 640), 8400),
            new("yolo26s-pose", "yolo/v26/pose/s", "pose", YoloDetectionFamily.YoloV26, "55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc", "6f6158be448c73471c000cf41db5cd9169300ed9", "8.4.0-end2end", 19, new VisualSize(640, 640), 300),
            new("yolov8s-obb", "yolo/v8/obb/s", "obb", YoloDetectionFamily.YoloV8, "2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981", "ef141af4b837e0a1c34ff187ac40ef36af56c135", "8.1.6", 17, new VisualSize(1024, 1024), 21504),
            new("yolo11s-obb", "yolo/v11/obb/s", "obb", YoloDetectionFamily.YoloV11, "50ae0e11b742007fcd297408382be94a25c884093d63dce00ead62f37ea2cad0", "636685ace98527cd0113656fd024a82291fa3122", "8.3.24", 19, new VisualSize(1024, 1024), 21504),
            new("yolo26s-obb", "yolo/v26/obb/s", "obb", YoloDetectionFamily.YoloV26, "bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df", "6f6158be448c73471c000cf41db5cd9169300ed9", "8.4.0-end2end", 19, new VisualSize(1024, 1024), 300)
        };

        private static readonly PortableDefinition[] PortableDefinitions =
        {
            new("deimv2", "deim/v2/detect", PortableDetectorFamily.DEIMv2Det, "08a6a9052c83ccd356e91f8839dfe7b2e686639b577feb7f0b7b204f7f2969cc", 16, new VisualSize(640, 640), YoloLabelSets.Coco80, "images"),
            new("ppyoloe", "pp-yoloe/plus-crn-l", PortableDetectorFamily.PPYOLOEDet, "68866d9841e41f6637d4a1c13db6c70a42c9f0367c79870b0a8a9e9df32b8504", 11, new VisualSize(640, 640), YoloLabelSets.Coco80, "image"),
            new("rfdetr", "rf-detr/detect", PortableDetectorFamily.RFDETRDet, "b464822e768f5795f249a6bd08cf1c5299787806c740204ed8e46d3a369ab769", 17, new VisualSize(512, 512), Labels(5), "input", QueryCount: 300, IncludesNoObject: true),
            new("rfdetr-seg", "rf-detr/segment", PortableDetectorFamily.RFDETRSeg, "6156aaff01ea0da0a007b29157fa34bf512d99d9e6a872cad70ae28cd08d6a35", 17, new VisualSize(432, 432), Labels(90), "input", MasksName: "4245", QueryCount: 200, IncludesNoObject: true),
            new("rtdetr-decoded-ir", "rt-detr/r50vd-decoded-vector-ir", PortableDetectorFamily.RTDETRDet, "9d49703964c07567de7f00bda85bae1760da322e2b0655bfae110f2c222c778d", 16, new VisualSize(640, 640), YoloLabelSets.Coco80, "image", ModelFormat: "openvino-ir", BoxesName: "save_infer_model/scale_0.tmp_0", CountName: "save_infer_model/scale_1.tmp_0", DynamicBatch: false, CountShape: PortableDetectorCountShape.BatchVector, ScoreThreshold: .4f),
            new("rtdetr-decoded-onnx", "rt-detr/r50vd-decoded-vector-onnx", PortableDetectorFamily.RTDETRDet, "a0477cb6cb33f431eae72438cd9a38fa80c46bca9b8d397a4ece49a9ee4353db", 16, new VisualSize(640, 640), YoloLabelSets.Coco80, "image", BoxesName: "save_infer_model/scale_0.tmp_0", CountName: "save_infer_model/scale_1.tmp_0", DynamicBatch: true, CountShape: PortableDetectorCountShape.BatchVector, ScoreThreshold: .4f),
            new("rtdetr-raw", "rt-detr/r50vd-raw-query", PortableDetectorFamily.RTDETRDet, "544133360bc01a473125f5e6c607a09d9a969744b05e2125f1ccd1dd3f1273ad", 16, new VisualSize(640, 640), YoloLabelSets.Coco80, "image", BoxesName: "stack_7.tmp_0_slice_0", LabelsName: "stack_8.tmp_0_slice_0", QueryCount: 300, DynamicBatch: true, RawQuery: true)
        };

        private static IReadOnlyList<string> Labels(int count) => Enumerable.Range(0, count).Select(index => "class" + index.ToString(Invariant)).ToArray();
        private static string Hash(string value) => value;

        private sealed record DetectionDefinition(string Kind, string ModelId, YoloDetectionFamily Family, string Sha256, string UpstreamCommit, string ExporterVersion, int Opset);
        private sealed record MultiTaskDefinition(string Kind, string ModelId, string Task, YoloDetectionFamily Family, string Sha256, string UpstreamCommit, string ExporterVersion, int Opset, VisualSize ModelSize, int CandidateCount);
        private sealed record PortableDefinition(string Kind, string ModelId, PortableDetectorFamily Family, string Sha256, int Opset, VisualSize ModelSize, IReadOnlyList<string> Labels, string InputName, string ModelFormat = "onnx", string? BoxesName = null, string? LabelsName = null, string? MasksName = null, string? CountName = null, int QueryCount = -1, bool IncludesNoObject = false, bool DynamicBatch = false, bool RawQuery = false, PortableDetectorCountShape CountShape = PortableDetectorCountShape.BatchVector, float ScoreThreshold = .01f);
    }

    private enum BenchmarkMode
    {
        Cold,
        Steady
    }

    private sealed class BenchmarkReport
    {
        public int SchemaVersion { get; init; }
        public DateTimeOffset GeneratedAtUtc { get; init; }
        public DeviceSnapshot DeviceBefore { get; init; } = null!;
        public DeviceSnapshot DeviceAfter { get; init; } = null!;
        public BenchmarkConfiguration Configuration { get; init; } = null!;
        public IReadOnlyList<ResultRow> Rows { get; init; } = Array.Empty<ResultRow>();
    }

    private sealed class BenchmarkConfiguration
    {
        public IReadOnlyList<string> Kinds { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Backends { get; init; } = Array.Empty<string>();
        public string ImagePath { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, string> ModelPaths { get; init; } = new Dictionary<string, string>();
        public int Warmup { get; init; }
        public int Iterations { get; init; }
        public IReadOnlyList<string> Modes { get; init; } = Array.Empty<string>();
        public bool OpenCvFusionEnabled { get; init; }
        public bool OpenCvWinogradEnabled { get; init; }
        public string CsvOutputPath { get; init; } = string.Empty;
        public string JsonOutputPath { get; init; } = string.Empty;
    }

    private sealed class DeviceSnapshot
    {
        public DateTimeOffset CapturedAtUtc { get; init; }
        public string MachineName { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string OsDescription { get; init; } = string.Empty;
        public string OsVersion { get; init; } = string.Empty;
        public string ProcessArchitecture { get; init; } = string.Empty;
        public string RuntimeDescription { get; init; } = string.Empty;
        public int ProcessorCount { get; init; }
        public long TotalAvailableMemoryBytes { get; init; }
        public string? CudaArchitecture { get; init; }
        public bool TensorRtExternalEnabled { get; init; }
        public string? TensorRtApiVersion { get; init; }
        public string? NativeBridgePath { get; init; }
        public string? TensorRtRoot { get; init; }
        public string? CudaRoot { get; init; }
        public string? NvidiaSmi { get; init; }
        public IReadOnlyList<string> BundledNativeRuntimeFiles { get; init; } = Array.Empty<string>();

        public static DeviceSnapshot Capture()
        {
            string nativeRoot = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
            string[] nativeFiles = Directory.Exists(nativeRoot)
                ? Directory.EnumerateFiles(nativeRoot, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Where(name => name != null).Cast<string>().OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();
            return new DeviceSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                OsDescription = RuntimeInformation.OSDescription,
                OsVersion = Environment.OSVersion.VersionString,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeDescription = RuntimeInformation.FrameworkDescription,
                ProcessorCount = Environment.ProcessorCount,
                TotalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                CudaArchitecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE"),
                TensorRtExternalEnabled = string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_RUN_EXTERNAL"), "true", StringComparison.OrdinalIgnoreCase),
                TensorRtApiVersion = Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API_VERSION"),
                NativeBridgePath = Environment.GetEnvironmentVariable("JYPPX_NATIVE_BRIDGE_PATH"),
                TensorRtRoot = Environment.GetEnvironmentVariable("JYPPX_TENSORRT_ROOT"),
                CudaRoot = Environment.GetEnvironmentVariable("JYPPX_CUDA_ROOT"),
                NvidiaSmi = CaptureNvidiaSmi(),
                BundledNativeRuntimeFiles = nativeFiles
            };
        }

        private static string? CaptureNvidiaSmi()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                process.StartInfo.ArgumentList.Add("--query-gpu=name,driver_version,memory.total,pstate,clocks.current.graphics,clocks.current.memory,temperature.gpu,utilization.gpu");
                process.StartInfo.ArgumentList.Add("--format=csv,noheader,nounits");
                if (!process.Start()) return "nvidia-smi-start-failed";
                if (!process.WaitForExit(5000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return "nvidia-smi-timeout";
                }
                string output = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd().Trim();
                if (process.ExitCode != 0) return "exit=" + process.ExitCode.ToString(Invariant) + (string.IsNullOrEmpty(error) ? string.Empty : ";error=" + error);
                return string.IsNullOrEmpty(output) ? "nvidia-smi-empty" : output;
            }
            catch (Exception exception)
            {
                return "unavailable=" + exception.GetType().Name + ":" + exception.Message;
            }
        }
    }

    private sealed class Options
    {
        private static readonly string[] AllKinds =
        {
            "yolov5n", "yolov6s", "yolov7", "yolov8n", "yolov9s", "yolov10n", "yolo11n", "yolo12n", "yolo13n", "yolo26n",
            "yolov8s-cls", "yolov5s-seg", "yolov8n-seg", "yolov9c-seg", "yolo11s-seg", "yolo26s-seg",
            "yolov8s-pose", "yolo11s-pose", "yolo26s-pose", "yolov8s-obb", "yolo11s-obb", "yolo26s-obb",
            "deimv2", "ppyoloe", "rfdetr", "rfdetr-seg", "rtdetr-decoded-ir", "rtdetr-decoded-onnx", "rtdetr-raw",
            "padim", "rmbg14", "rmbg20", "rmbg20-int8"
        };
        private static readonly string[] AllBackends = { "onnxruntime", "onnxruntime-cuda", "openvino", "opencv-dnn", "tensorrt", "tensorrt-cuda" };

        private Options(IReadOnlyList<string> kinds, IReadOnlyList<string> backends, string imagePath, string? modelPath, IReadOnlyDictionary<string, string> modelPaths, int warmup, int iterations, string outputPath, string jsonOutputPath, IReadOnlyList<BenchmarkMode> modes, bool help)
        { Kinds = kinds; Backends = backends; ImagePath = imagePath; ModelPath = modelPath; ModelPaths = modelPaths; Warmup = warmup; Iterations = iterations; OutputPath = outputPath; JsonOutputPath = jsonOutputPath; Modes = modes; Help = help; }

        public IReadOnlyList<string> Kinds { get; }
        public IReadOnlyList<string> Backends { get; }
        public string ImagePath { get; }
        public string? ModelPath { get; }
        public IReadOnlyDictionary<string, string> ModelPaths { get; }
        public int Warmup { get; }
        public int Iterations { get; }
        public string OutputPath { get; }
        public string JsonOutputPath { get; }
        public IReadOnlyList<BenchmarkMode> Modes { get; }
        public bool Help { get; }

        public string ModelPathFor(string kind, string backend)
        {
            string? path = null;
            if (ModelPaths.TryGetValue(kind, out string? value)) path = value;
            else if (Kinds.Count == 1 && !string.IsNullOrWhiteSpace(ModelPath)) path = ModelPath;
            if (path != null && kind != "rtdetr-decoded-ir" && (backend == "tensorrt" || backend == "tensorrt-cuda") && !path.EndsWith(".engine", StringComparison.OrdinalIgnoreCase)) return path + ".engine";
            if (path != null) return path;
            throw new ArgumentException("A --model-" + kind + " path is required.");
        }

        public static Options Parse(string[] args)
        {
            string kindsValue = "all"; string backendsValue = "all"; string modeValue = "cold"; string? image = null; string? model = null; int warmup = 3; int iterations = 10; bool help = false; string output = Path.Combine("artifacts", "local-model-benchmarks", "visual-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", Invariant) + ".csv"); string? jsonOutput = null;
            var models = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (argument == "--help" || argument == "-h") { help = true; continue; }
                if (argument == "--kind") { kindsValue = Next(args, ref index, argument); continue; }
                if (argument == "--backend") { backendsValue = Next(args, ref index, argument); continue; }
                if (argument == "--mode") { modeValue = Next(args, ref index, argument); continue; }
                if (argument == "--image") { image = Next(args, ref index, argument); continue; }
                if (argument == "--model") { model = Next(args, ref index, argument); continue; }
                if (argument == "--warmup") { warmup = Positive(Next(args, ref index, argument), argument); continue; }
                if (argument == "--iterations") { iterations = Positive(Next(args, ref index, argument), argument); continue; }
                if (argument == "--output") { output = Next(args, ref index, argument); continue; }
                if (argument == "--json-output") { jsonOutput = Next(args, ref index, argument); continue; }
                if (argument.StartsWith("--model-", StringComparison.Ordinal))
                {
                    string kind = argument.Substring(8);
                    if (!AllKinds.Contains(kind, StringComparer.Ordinal)) throw new ArgumentException("Unknown model kind: " + kind);
                    models[kind] = Next(args, ref index, argument);
                    continue;
                }
                throw new ArgumentException("Unknown option: " + argument);
            }
            if (help) return new Options(Array.Empty<string>(), Array.Empty<string>(), string.Empty, null, models, warmup, iterations, output, jsonOutput ?? Path.ChangeExtension(output, ".json"), Array.Empty<BenchmarkMode>(), true);
            if (string.IsNullOrWhiteSpace(image)) throw new ArgumentException("--image is required.");
            return new Options(Select(kindsValue, AllKinds, "kind"), Select(backendsValue, AllBackends, "backend"), Path.GetFullPath(image), model == null ? null : Path.GetFullPath(model), models.ToDictionary(pair => pair.Key, pair => Path.GetFullPath(pair.Value), StringComparer.OrdinalIgnoreCase), warmup, iterations, output, jsonOutput ?? Path.ChangeExtension(output, ".json"), SelectModes(modeValue), false);
        }

        private static IReadOnlyList<BenchmarkMode> SelectModes(string value)
        {
            if (value.Equals("cold", StringComparison.OrdinalIgnoreCase)) return new[] { BenchmarkMode.Cold };
            if (value.Equals("steady", StringComparison.OrdinalIgnoreCase)) return new[] { BenchmarkMode.Steady };
            if (value.Equals("both", StringComparison.OrdinalIgnoreCase)) return new[] { BenchmarkMode.Cold, BenchmarkMode.Steady };
            throw new ArgumentException("Unsupported mode: " + value + " (expected cold, steady, or both).");
        }

        private static IReadOnlyList<string> Select(string value, IReadOnlyList<string> allowed, string name)
        {
            var selected = new List<string>();
            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Equals("all", StringComparison.OrdinalIgnoreCase)) return allowed.ToArray();
                if (!allowed.Contains(part, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Unsupported " + name + ": " + part);
                if (!selected.Contains(part, StringComparer.OrdinalIgnoreCase)) selected.Add(part);
            }
            if (selected.Count == 0) throw new ArgumentException("At least one " + name + " is required.");
            return selected;
        }

        private static string Next(string[] args, ref int index, string option)
        {
            if (++index >= args.Length) throw new ArgumentException("Missing value for " + option);
            return args[index];
        }

        private static int Positive(string value, string option)
        {
            if (!int.TryParse(value, NumberStyles.Integer, Invariant, out int result) || result <= 0) throw new ArgumentException(option + " must be a positive integer.");
            return result;
        }
    }

    private readonly record struct LatencyStatistics(double MeanMilliseconds, double P50Milliseconds, double P95Milliseconds)
    {
        public static LatencyStatistics Single(double value) => new(value, value, value);

        public static LatencyStatistics From(IEnumerable<double> values)
        {
            double[] sorted = values.ToArray();
            if (sorted.Length == 0) return Single(0d);
            Array.Sort(sorted);
            return new(sorted.Average(), Percentile(sorted, 0.50d), Percentile(sorted, 0.95d));
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 1) return sorted[0];
            double position = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
        }
    }

    private readonly record struct Measurement(LatencyStatistics Preprocess, LatencyStatistics Inference, LatencyStatistics Postprocess, LatencyStatistics Orchestration, LatencyStatistics Total, long PreprocessAllocatedBytes, long PipelineAllocatedBytes, double PreparationSetupMilliseconds, long PreparationSetupAllocatedBytes, string ResultFingerprint)
    {
        public double PreprocessMilliseconds => Preprocess.MeanMilliseconds;
        public double InferenceMilliseconds => Inference.MeanMilliseconds;
        public double PostprocessMilliseconds => Postprocess.MeanMilliseconds;
        public double OrchestrationMilliseconds => Orchestration.MeanMilliseconds;
        public double TotalMilliseconds => Total.MeanMilliseconds;
        public static Measurement Empty => new(LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), 0, 0, 0d, 0, string.Empty);
        public static Measurement Single(double preprocess, double inference, double postprocess, double orchestration, double total, long preprocessAllocated, long pipelineAllocated, string resultFingerprint)
            => new(LatencyStatistics.Single(preprocess), LatencyStatistics.Single(inference), LatencyStatistics.Single(postprocess), LatencyStatistics.Single(orchestration), LatencyStatistics.Single(total), preprocessAllocated, pipelineAllocated, 0d, 0, resultFingerprint);
        public static Measurement Setup(double milliseconds, long allocatedBytes)
            => new(LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), LatencyStatistics.Single(0d), 0, 0, milliseconds, allocatedBytes, string.Empty);
        public static Measurement Aggregate(IReadOnlyList<Measurement> values, Measurement setup)
        {
            string fingerprint = values[0].ResultFingerprint;
            if (values.Any(value => !string.Equals(value.ResultFingerprint, fingerprint, StringComparison.Ordinal))) throw new InvalidOperationException("Timed iterations produced different canonical results.");
            return new(
                LatencyStatistics.From(values.Select(value => value.PreprocessMilliseconds)),
                LatencyStatistics.From(values.Select(value => value.InferenceMilliseconds)),
                LatencyStatistics.From(values.Select(value => value.PostprocessMilliseconds)),
                LatencyStatistics.From(values.Select(value => value.OrchestrationMilliseconds)),
                LatencyStatistics.From(values.Select(value => value.TotalMilliseconds)),
                checked((long)values.Average(value => value.PreprocessAllocatedBytes)),
                checked((long)values.Average(value => value.PipelineAllocatedBytes)),
                setup.PreparationSetupMilliseconds,
                setup.PreparationSetupAllocatedBytes,
                fingerprint);
        }
    }

    private sealed class ResultRow
    {
        private ResultRow(string kind, string backend, BenchmarkMode mode, string device, string status, Measurement? measurement, string detail)
        { Kind = kind; Backend = backend; Mode = mode; Device = device; Status = status; Measurement = measurement; Detail = detail; }
        public const string Header = "kind,backend,mode,device,status,preprocess_ms,preprocess_p50_ms,preprocess_p95_ms,inference_ms,inference_p50_ms,inference_p95_ms,postprocess_ms,postprocess_p50_ms,postprocess_p95_ms,orchestration_ms,orchestration_p50_ms,orchestration_p95_ms,total_ms,total_p50_ms,total_p95_ms,preprocess_allocated_bytes,pipeline_allocated_bytes,preparation_setup_ms,preparation_setup_allocated_bytes,detail";
        public string Kind { get; } public string Backend { get; } public BenchmarkMode Mode { get; } public string Device { get; } public string Status { get; } public Measurement? Measurement { get; } public string Detail { get; }
        public static ResultRow Pass(BenchmarkCase item, string backend, BenchmarkMode mode, string device, Measurement measurement) => new(item.Kind, backend, mode, device, "pass", measurement, "result_sha256=" + measurement.ResultFingerprint);
        public static ResultRow Unavailable(BenchmarkCase item, string backend, BenchmarkMode mode, string detail) => new(item.Kind, backend, mode, DeviceFor(backend), "unavailable", null, detail);
        public static ResultRow Failure(BenchmarkCase item, string backend, BenchmarkMode mode, string device, string status, string detail) => new(item.Kind, backend, mode, device, status, null, detail);
        public static ResultRow Failure(string kind, string backend, BenchmarkMode mode, string device, string status, string detail) => new(kind, backend, mode, device, status, null, detail);
        public string ToLog() => Measurement == null ? "DEPLOYSHARP_VISUAL_BENCHMARK kind=" + Kind + " backend=" + Backend + " mode=" + Mode.ToString().ToLowerInvariant() + " status=" + Status + " detail=" + Detail : string.Format(Invariant, "DEPLOYSHARP_VISUAL_BENCHMARK kind={0} backend={1} mode={2} status=pass pre_ms={3:F3} p50={4:F3} p95={5:F3} inference_ms={6:F3} p50={7:F3} p95={8:F3} post_ms={9:F3} p50={10:F3} p95={11:F3} total_ms={12:F3} p50={13:F3} p95={14:F3} setup_ms={15:F3} pre_alloc={16} pipeline_alloc={17}", Kind, Backend, Mode.ToString().ToLowerInvariant(), Measurement.Value.Preprocess.MeanMilliseconds, Measurement.Value.Preprocess.P50Milliseconds, Measurement.Value.Preprocess.P95Milliseconds, Measurement.Value.Inference.MeanMilliseconds, Measurement.Value.Inference.P50Milliseconds, Measurement.Value.Inference.P95Milliseconds, Measurement.Value.Postprocess.MeanMilliseconds, Measurement.Value.Postprocess.P50Milliseconds, Measurement.Value.Postprocess.P95Milliseconds, Measurement.Value.Total.MeanMilliseconds, Measurement.Value.Total.P50Milliseconds, Measurement.Value.Total.P95Milliseconds, Measurement.Value.PreparationSetupMilliseconds, Measurement.Value.PreprocessAllocatedBytes, Measurement.Value.PipelineAllocatedBytes);
        public string ToCsv() => string.Join(",", new[] { Csv(Kind), Csv(Backend), Csv(Mode.ToString().ToLowerInvariant()), Csv(Device), Csv(Status), Number(Measurement?.Preprocess.MeanMilliseconds), Number(Measurement?.Preprocess.P50Milliseconds), Number(Measurement?.Preprocess.P95Milliseconds), Number(Measurement?.Inference.MeanMilliseconds), Number(Measurement?.Inference.P50Milliseconds), Number(Measurement?.Inference.P95Milliseconds), Number(Measurement?.Postprocess.MeanMilliseconds), Number(Measurement?.Postprocess.P50Milliseconds), Number(Measurement?.Postprocess.P95Milliseconds), Number(Measurement?.Orchestration.MeanMilliseconds), Number(Measurement?.Orchestration.P50Milliseconds), Number(Measurement?.Orchestration.P95Milliseconds), Number(Measurement?.Total.MeanMilliseconds), Number(Measurement?.Total.P50Milliseconds), Number(Measurement?.Total.P95Milliseconds), Measurement?.PreprocessAllocatedBytes.ToString(Invariant) ?? string.Empty, Measurement?.PipelineAllocatedBytes.ToString(Invariant) ?? string.Empty, Number(Measurement?.PreparationSetupMilliseconds), Measurement?.PreparationSetupAllocatedBytes.ToString(Invariant) ?? string.Empty, Csv(Detail) });
        private static string Number(double? value) => value.HasValue ? value.Value.ToString("F3", Invariant) : string.Empty;
        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
