using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenCV;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.OpenCvSharp.Core;
using JYPPX.OpenCvSharp.Dnn;
using DnnCv2 = JYPPX.OpenCvSharp.Dnn.Cv2;

internal static class Program
{
    private const string DefaultRoot = @"E:\Model\paddleocr";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_ROOT") ?? DefaultRoot;
        int warmup = ReadInt("DEPLOYSHARP_PADDLEOCR_WARMUP", 3);
        int iterations = ReadInt("DEPLOYSHARP_PADDLEOCR_ITERATIONS", 15);
        int stageConcurrency = ReadInt("DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY", 1);
        int batchSize = ReadInt("DEPLOYSHARP_PADDLEOCR_BATCH_SIZE", 4);
        string? configuredIntraOpThreads = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_INTRA_OP_THREADS");
        int intraOpThreads = string.IsNullOrWhiteSpace(configuredIntraOpThreads) ? -1 : ReadNonNegativeInt("DEPLOYSHARP_PADDLEOCR_INTRA_OP_THREADS", 0);
        int detectionIntraOpThreads = ReadNonNegativeInt("DEPLOYSHARP_PADDLEOCR_DETECTION_INTRA_OP_THREADS", 0);
        double maximumPaddingRatio = ReadDouble("DEPLOYSHARP_PADDLEOCR_MAX_PADDING_RATIO", 2.0);
        bool reusePreparedInput = ReadBool("DEPLOYSHARP_PADDLEOCR_REUSE_INPUT", false);
        bool autoTune = ReadBool("DEPLOYSHARP_PADDLEOCR_AUTOTUNE", true);
        int tensorRtBatchSize = ReadInt("DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE", 1);
        if (tensorRtBatchSize <= 0) throw new ArgumentOutOfRangeException("DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE", "TensorRT batch size must be greater than zero.");
        TensorRtApiVersion tensorRtApiVersion = ReadTensorRtApiVersion();
        string output = args.Length > 1 ? args[1] : Path.Combine("artifacts", "local-model-benchmarks", "paddleocr-full-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", Invariant) + ".csv");
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine("PADDLEOCR_BENCHMARK_ERROR root-not-found=" + root);
            return 2;
        }
        List<ModelCase> models = Discover(root);
        if (models.Count == 0)
        {
            Console.Error.WriteLine("PADDLEOCR_BENCHMARK_ERROR no-onnx-models-found=" + root);
            return 2;
        }
        return RunFullPipeline(root, models, output, warmup, iterations, tensorRtApiVersion, reusePreparedInput, stageConcurrency, batchSize, tensorRtBatchSize, intraOpThreads, detectionIntraOpThreads, maximumPaddingRatio, autoTune);
    }

    private static int RunFullPipeline(string root, IReadOnlyList<ModelCase> models, string output, int warmup, int iterations, TensorRtApiVersion tensorRtApiVersion, bool reusePreparedInput, int stageConcurrency, int batchSize, int tensorRtBatchSize, int intraOpThreads, int detectionIntraOpThreads, double maximumPaddingRatio, bool autoTune)
    {
        string requestedImage = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_IMAGE") ?? @"E:\Data\ocr\demo\_1.jpg";
        string imagePath = ResolveImagePath(requestedImage);
        HashSet<string> selectedBackends = new HashSet<string>((Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_BACKENDS") ?? "onnxruntime,openvino,opencv-dnn,onnxruntime-cuda,tensorrt").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
        HashSet<string> selectedVersions = new HashSet<string>((Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VERSIONS") ?? "v4,v5,v6").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
        Console.WriteLine("PADDLEOCR_FULL_IMAGE requested=" + requestedImage + ";used=" + imagePath);
        Console.WriteLine("PADDLEOCR_FULL_PARALLEL stageConcurrency=" + stageConcurrency.ToString(Invariant) + ";batchSize=" + batchSize.ToString(Invariant) + ";tensorRtBatchSize=" + tensorRtBatchSize.ToString(Invariant) + ";intraOpThreads=" + intraOpThreads.ToString(Invariant) + ";maximumPaddingRatio=" + maximumPaddingRatio.ToString("F3", Invariant));
        var rows = new List<FullResultRow>();
        foreach (string version in new[] { "v4", "v5", "v6" })
        {
            if (!selectedVersions.Contains(version) && !selectedVersions.Any(value => value.StartsWith(version + "-", StringComparison.OrdinalIgnoreCase))) continue;
            foreach (string variant in models.Where(x => x.Version == version && x.Role == "det").Select(x => x.Variant).Distinct(StringComparer.Ordinal).OrderBy(x => x))
            {
                if (!selectedVersions.Contains(version) && !selectedVersions.Contains(version + "-" + variant)) continue;
                ModelCase? detector = models.FirstOrDefault(x => x.Version == version && x.Variant == variant && x.Role == "det");
                ModelCase? recognizer = models.FirstOrDefault(x => x.Version == version && x.Variant == variant && x.Role == "rec");
                ModelCase? classifier = models.FirstOrDefault(x => x.Version == version && x.Role == "cls");
                if (detector == null || recognizer == null) continue;
                void Add(string backend, string device)
                {
                    rows.Add(autoTune
                        ? RunBestFullBackend(version, backend, device, detector, recognizer, classifier, imagePath, warmup, iterations, tensorRtApiVersion, reusePreparedInput, intraOpThreads, detectionIntraOpThreads, maximumPaddingRatio)
                        : RunFullBackend(version, backend, device, detector, recognizer, classifier, imagePath, warmup, iterations, tensorRtApiVersion, reusePreparedInput, stageConcurrency, batchSize, tensorRtBatchSize, ResolveStageIntraOpThreads(backend, stageConcurrency, intraOpThreads), detectionIntraOpThreads, maximumPaddingRatio));
                }
                if (selectedBackends.Contains("onnxruntime")) Add("onnxruntime", "cpu");
                if (selectedBackends.Contains("openvino")) Add("openvino", "CPU");
                if (selectedBackends.Contains("opencv-dnn")) Add("opencv-dnn", "cpu");
                if (selectedBackends.Contains("onnxruntime-cuda")) Add("onnxruntime-cuda", "cuda");
                if (selectedBackends.Contains("tensorrt")) Add("tensorrt", "cuda");
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        using (var writer = new StreamWriter(output, false, new System.Text.UTF8Encoding(false)))
        {
            writer.WriteLine("version,variant,backend,device,status,selected_batch_size,selected_inference_channels,preprocess_ms,detection_ms,detection_inference_ms,detection_postprocess_ms,crop_ms,orientation_ms,recognition_ms,recognition_prepare_work_ms,recognition_inference_work_ms,recognition_postprocess_work_ms,recognition_batches,merge_ms,total_ms,total_p50_ms,total_p95_ms,preprocess_allocated_bytes,pipeline_process_allocated_bytes,regions,result_text_sha256,result_contract_sha256,image_path,detail");
            foreach (FullResultRow row in rows) writer.WriteLine(row.ToCsv());
        }
        Console.WriteLine("PADDLEOCR_FULL_REPORT=" + Path.GetFullPath(output));
        Console.WriteLine("PADDLEOCR_FULL_ROWS=" + rows.Count.ToString(Invariant));
        return 0;
    }

    private static FullResultRow RunBestFullBackend(string version, string backend, string device, ModelCase detector, ModelCase recognizer, ModelCase? classifier, string imagePath, int warmup, int iterations, TensorRtApiVersion tensorRtApiVersion, bool reusePreparedInput, int configuredIntraOpThreads, int detectionIntraOpThreads, double maximumPaddingRatio)
    {
        int[] concurrencyCandidates = ReadPositiveIntList("DEPLOYSHARP_PADDLEOCR_AUTOTUNE_CONCURRENCY", "1,2,4");
        int[] batchCandidates = ReadPositiveIntList("DEPLOYSHARP_PADDLEOCR_AUTOTUNE_BATCHES", "1,2,4,8,16");
        int tuneWarmup = ReadNonNegativeInt("DEPLOYSHARP_PADDLEOCR_AUTOTUNE_WARMUP", 1);
        int tuneIterations = ReadInt("DEPLOYSHARP_PADDLEOCR_AUTOTUNE_ITERATIONS", 3);
        var trials = new List<(int Concurrency, int Batch, FullResultRow Row)>();
        foreach (int concurrency in concurrencyCandidates)
        {
            foreach (int batch in batchCandidates)
            {
                int stageIntraOpThreads = ResolveStageIntraOpThreads(backend, concurrency, configuredIntraOpThreads);
                FullResultRow trial = RunFullBackend(version, backend, device, detector, recognizer, classifier, imagePath, tuneWarmup, tuneIterations, tensorRtApiVersion, reusePreparedInput: true, concurrency, batch, batch, stageIntraOpThreads, detectionIntraOpThreads, maximumPaddingRatio);
                trials.Add((concurrency, batch, trial));
                Console.WriteLine("PADDLEOCR_AUTOTUNE_TRIAL version=" + version + ";variant=" + detector.Variant + ";backend=" + backend + ";concurrency=" + concurrency.ToString(Invariant) + ";batch=" + batch.ToString(Invariant) + ";status=" + trial.Status + (trial.Timing.HasValue ? ";stageMs=" + TunedStageMilliseconds(trial.Timing.Value).ToString("F3", Invariant) + ";totalMs=" + trial.Timing.Value.Total.ToString("F3", Invariant) + ";actualRecognitionBatches=" + trial.Timing.Value.RecognitionBatches.ToString(Invariant) : ";detail=" + trial.Detail));
            }
        }

        var allPassed = trials.Where(value => value.Row.Status == "pass" && value.Row.Timing.HasValue).ToArray();
        var passed = allPassed
            // Select the configuration that minimizes the complete pipeline
            // latency. Batch size and channel count primarily affect the
            // recognition stages, but ranking only those stages can choose a
            // configuration that is worse for the user's actual end-to-end
            // call once detection and orchestration are included.
            .OrderBy(value => value.Row.Timing!.Value.Total)
            .ThenBy(value => TunedStageMilliseconds(value.Row.Timing!.Value))
            .ToArray();
        if (passed.Length == 0)
        {
            FullResultRow failure = trials.First().Row;
            return failure with { Detail = failure.Detail + "; autotune found no passing batch/channel combination" };
        }

        var best = passed[0];
        string selectedContract = best.Row.Timing!.Value.ResultContractSha256;
        int selectedIntraOpThreads = ResolveStageIntraOpThreads(backend, best.Concurrency, configuredIntraOpThreads);
        FullResultRow result = RunFullBackend(version, backend, device, detector, recognizer, classifier, imagePath, warmup, iterations, tensorRtApiVersion, reusePreparedInput, best.Concurrency, best.Batch, best.Batch, selectedIntraOpThreads, detectionIntraOpThreads, maximumPaddingRatio);
        if (result.Timing.HasValue && !string.Equals(result.Timing.Value.ResultContractSha256, selectedContract, StringComparison.Ordinal))
            return FullResultRow.Fail(version, detector.Variant, backend, device, imagePath, new InvalidOperationException("The formal run result contract differs from the selected autotune candidate."));
        string summary = string.Join("|", trials.Select(value => value.Concurrency.ToString(Invariant) + "x" + value.Batch.ToString(Invariant) + "=" + (value.Row.Timing.HasValue ? "stage:" + TunedStageMilliseconds(value.Row.Timing.Value).ToString("F3", Invariant) + "/total:" + value.Row.Timing.Value.Total.ToString("F3", Invariant) + "/batches:" + value.Row.Timing.Value.RecognitionBatches.ToString(Invariant) : value.Row.Status)));
        int contractVariants = allPassed.Select(value => value.Row.Timing!.Value.ResultContractSha256).Distinct(StringComparer.Ordinal).Count();
        return result with { SelectedInferenceChannels = best.Concurrency, SelectedBatchSize = best.Batch, Detail = result.Detail + "; autotune used prepared input and ranked complete-pipeline latency (orientation+recognition is the tie-breaker); selected concurrency=" + best.Concurrency.ToString(Invariant) + ",batch=" + best.Batch.ToString(Invariant) + ",stageIntraOpThreads=" + selectedIntraOpThreads.ToString(Invariant) + "; deterministicContractVariantsAcrossShapes=" + contractVariants.ToString(Invariant) + "; trials=" + summary };
    }

    private static double TunedStageMilliseconds(FullTiming timing) => timing.Orientation + timing.Recognition;

    private static int ResolveStageIntraOpThreads(string backend, int concurrency, int configured)
    {
        if (configured >= 0) return configured;
        return backend == "onnxruntime" ? Math.Max(1, Environment.ProcessorCount / concurrency) : 0;
    }

    private static int? ResolveOpenCvNumThreads()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_OPENCV_NUM_THREADS");
        if (string.IsNullOrWhiteSpace(value)) return null;
        return ReadInt("DEPLOYSHARP_OPENCV_NUM_THREADS", 1);
    }

    private static int[] ReadPositiveIntList(string name, string fallback)
    {
        string raw = Environment.GetEnvironmentVariable(name) ?? fallback;
        int[] values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(value => int.Parse(value, NumberStyles.Integer, Invariant)).Distinct().ToArray();
        if (values.Length == 0 || values.Any(value => value <= 0)) throw new ArgumentOutOfRangeException(name, "Values must be positive integers.");
        return values;
    }

    private static string ResolveImagePath(string requested)
    {
        if (File.Exists(requested)) return Path.GetFullPath(requested);
        string fallback = requested.Replace("\\demo\\_1.jpg", "\\demo_1.jpg", StringComparison.OrdinalIgnoreCase);
        return File.Exists(fallback) ? Path.GetFullPath(fallback) : Path.GetFullPath(requested);
    }

    private static FullResultRow RunFullBackend(string version, string backend, string device, ModelCase detector, ModelCase recognizer, ModelCase? classifier, string imagePath, int warmup, int iterations, TensorRtApiVersion tensorRtApiVersion, bool reusePreparedInput, int stageConcurrency, int batchSize, int tensorRtBatchSize, int intraOpThreads, int detectionIntraOpThreads, double maximumPaddingRatio)
    {
        if (!File.Exists(imagePath)) return FullResultRow.Unavailable(version, detector.Variant, backend, device, imagePath, "image-not-found");
        if (backend == "tensorrt" && !string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            return FullResultRow.Unavailable(version, detector.Variant, backend, device, imagePath, "set DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1 after configuring the native TensorRT bridge/runtime");
        if (backend == "tensorrt" && (detector.EnginePath == null || recognizer.EnginePath == null || classifier != null && classifier.EnginePath == null))
            return FullResultRow.Unavailable(version, detector.Variant, backend, device, imagePath, "matching TensorRT engine sidecars are required for detector, recognizer, and classifier");
        OpenCvOcrImageInput? reusableInput = null;
        try
        {
            int pipelineTimeoutMs = ReadInt("DEPLOYSHARP_PADDLEOCR_PIPELINE_TIMEOUT_MS", 15000);
            VisualSize sourceSize;
            using (PreparedVisualInput probe = new OpenCvVisualInputFactory().CreateFromFile(imagePath, detector.InputName, new OpenCvPreprocessOptions(new VisualSize(32, 32), OpenCvResizeMode.Resize, VisualColorOrder.Bgr))) sourceSize = probe.SourceSize;
            OpenCvPreprocessOptions detOptions = OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions(sourceSize);
            int effectiveBatchSize = backend == "tensorrt" ? tensorRtBatchSize : batchSize;
            if (backend == "tensorrt")
            {
                // The retained remote sidecars use a static 736x736 detector input.
                detOptions = new OpenCvPreprocessOptions(new VisualSize(736, 736), OpenCvResizeMode.Resize, VisualColorOrder.Bgr,
                    OpenCvAlphaMode.Drop, new[] { .485f, .456f, .406f }, new[] { .229f, .224f, .225f }, inputDivisors: new[] { 255f, 255f, 255f });
            }
            using var detectionRegistry = new BackendRegistry();
            using var stageRegistry = new BackendRegistry();
            using var orientationRegistry = new BackendRegistry();
            BackendRequest request;
            string modelFormat = backend == "tensorrt" ? "tensorrt-engine" : "onnx";
            PaddleOcrProfile det = CreateDetectionProfile(version, detector, modelFormat);
            PaddleOcrProfile rec = CreateRecognitionProfile(version, recognizer, modelFormat);
            PaddleOcrProfile? cls = classifier == null ? null : CreateClassificationProfile(version, classifier, effectiveBatchSize, modelFormat);
            VisualModelProfile detProfile = det.VisualProfile;
            VisualModelProfile recProfile = rec.VisualProfile;
            VisualModelProfile? clsProfile = cls?.VisualProfile;
            TextCropProfile recognitionCrop = rec.CropProfile!;
            if (backend == "tensorrt") recognitionCrop = FixedWidthCrop(recognitionCrop, 320);
            if (backend == "onnxruntime" || backend == "onnxruntime-cuda")
            {
                OnnxRuntimeExecutionProvider executionProvider = backend == "onnxruntime-cuda" ? OnnxRuntimeExecutionProvider.Cuda : OnnxRuntimeExecutionProvider.Cpu;
                var detectionOrtOptions = new OnnxRuntimeOptions(intraOpThreads: detectionIntraOpThreads, interOpThreads: 1, executionProvider: executionProvider, cudaDeviceId: 0);
                var stageOrtOptions = new OnnxRuntimeOptions(intraOpThreads: intraOpThreads, interOpThreads: 1, executionProvider: executionProvider, cudaDeviceId: 0);
                detectionRegistry.UseOnnxRuntime(detectionOrtOptions);
                stageRegistry.UseOnnxRuntime(stageOrtOptions);
                orientationRegistry.UseOnnxRuntime(stageOrtOptions);
                request = new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, executionProvider == OnnxRuntimeExecutionProvider.Cuda ? "cuda" : "cpu");
            }
            else if (backend == "openvino")
            {
                detectionRegistry.UseOpenVino();
                stageRegistry.UseOpenVino();
                orientationRegistry.UseOpenVino();
                request = new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU");
            }
            else if (backend == "opencv-dnn")
            {
                int? openCvNumThreads = ResolveOpenCvNumThreads();
                TensorShape detInput = new TensorShape(1, 3, detOptions.ModelSize.Height, detOptions.ModelSize.Width);
                TensorShape detOutput = new TensorShape(1, 1, detOptions.ModelSize.Height, detOptions.ModelSize.Width);
                TensorDescriptor recOutput = OpenCvOutput(recognizer);
                TensorShape recInput = new TensorShape(effectiveBatchSize, 3, 48, 320);
                TensorShape recOutputShape = WithBatch(recOutput.Shape, effectiveBatchSize);
                detProfile = WithStaticOpenCvContract(detProfile, detInput, detOutput);
                recProfile = WithStaticOpenCvContract(recProfile, recInput, recOutputShape);
                recognitionCrop = FixedWidthCrop(recognitionCrop, 320);
                // Keep OpenCV DNN's production CPU graph optimizations enabled. The
                // static contracts above already constrain the admitted graph; Fusion
                // and Winograd reduce repeated convolution work without changing the
                // tensor contract.
                detectionRegistry.UseOpenCvDnn(new OpenCvDnnOptions(OpenCvContract(detProfile), numThreads: openCvNumThreads));
                stageRegistry.UseOpenCvDnn(new OpenCvDnnOptions(OpenCvContract(recProfile), numThreads: openCvNumThreads));
                if (clsProfile != null)
                {
                    TensorShape clsInput = WithBatch(classifier!.InputShape, effectiveBatchSize);
                    TensorShape clsOutput = WithBatch(OpenCvOutput(classifier).Shape, effectiveBatchSize);
                    clsProfile = WithStaticOpenCvContract(clsProfile, clsInput, clsOutput);
                    orientationRegistry.UseOpenCvDnn(new OpenCvDnnOptions(OpenCvContract(clsProfile), numThreads: openCvNumThreads));
                }
                request = new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu");
            }
            else if (backend == "tensorrt")
            {
                string? cudaTargetArchitecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE");
                var tensorRtOptions = new TensorRtBackendOptions(
                    tensorRtApiVersion,
                    cudaTargetArchitecture: cudaTargetArchitecture,
                    cacheImmutableHostInputsOnDevice: reusePreparedInput);
                detectionRegistry.UseTensorRT(tensorRtOptions);
                stageRegistry.UseTensorRT(tensorRtOptions);
                orientationRegistry.UseTensorRT(tensorRtOptions);
                request = new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda");
            }
            else return FullResultRow.Unsupported(version, detector.Variant, backend, device, imagePath, "unknown-backend");

            var profiles = new VisualProfileRegistry(); profiles.Register(detProfile); if (clsProfile != null) profiles.Register(clsProfile); profiles.Register(recProfile); profiles.Freeze();
            string detectorPath = modelFormat == "tensorrt-engine" ? detector.EnginePath! : detector.OnnxPath;
            string recognizerPath = modelFormat == "tensorrt-engine" ? recognizer.EnginePath! : recognizer.OnnxPath;
            string? classifierPath = classifier == null ? null : modelFormat == "tensorrt-engine" ? classifier.EnginePath! : classifier.OnnxPath;
            using OcrPipeline pipeline = cls == null
                ? new OcrPipeline(detectionRegistry, profiles.Select(det.CreateArtifact(detectorPath, request.BackendId), detectionRegistry, request, VisualTaskId.TextDetection), request,
                    stageRegistry, profiles.Select(rec.CreateArtifact(recognizerPath, request.BackendId), stageRegistry, request, VisualTaskId.TextRecognition), request, recognitionCrop,
                    new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: effectiveBatchSize, maximumRecognitionPaddingRatio: maximumPaddingRatio), new SessionOptions(1), new SessionOptions(stageConcurrency))
                : new OcrPipeline(detectionRegistry, profiles.Select(det.CreateArtifact(detectorPath, request.BackendId), detectionRegistry, request, VisualTaskId.TextDetection), request,
                    orientationRegistry, profiles.Select(cls.CreateArtifact(classifierPath!, request.BackendId), orientationRegistry, request, VisualTaskId.TextOrientationClassification), request, cls.CropProfile!,
                    stageRegistry, profiles.Select(rec.CreateArtifact(recognizerPath, request.BackendId), stageRegistry, request, VisualTaskId.TextRecognition), request, recognitionCrop,
                    new OcrPipelineOptions(maximumRegions: 32, maximumRecognitionBatch: effectiveBatchSize, maximumRecognitionPaddingRatio: maximumPaddingRatio), new SessionOptions(1), new SessionOptions(stageConcurrency), new SessionOptions(stageConcurrency), OcrOrientationRejectionPolicy.UseZeroDegrees);
            if (reusePreparedInput) reusableInput = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, det.VisualProfile.Input.Name, detOptions);
            FullTiming MeasureOne()
            {
                long preprocessAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var prep = Stopwatch.StartNew();
                OpenCvOcrImageInput? ownedInput = reusableInput;
                if (ownedInput == null) ownedInput = new OpenCvOcrImageInputFactory().CreateFromFile(imagePath, det.VisualProfile.Input.Name, detOptions);
                prep.Stop();
                long preprocessAllocated = reusePreparedInput ? 0L : GC.GetAllocatedBytesForCurrentThread() - preprocessAllocatedBefore;
                try
                {
                    // Backend calls may complete on worker threads. Use the process-wide allocation
                    // counter for the pipeline span so the CSV still captures their managed work.
                    long pipelineAllocatedBefore = GC.GetTotalAllocatedBytes(false);
                    OcrResult result = pipeline.Run(ownedInput, new OcrExecutionOptions(TimeSpan.FromMilliseconds(pipelineTimeoutMs)));
                    long pipelineAllocated = GC.GetTotalAllocatedBytes(false) - pipelineAllocatedBefore;
                    double preprocessing = reusePreparedInput ? 0d : prep.Elapsed.TotalMilliseconds;
                    OcrDetailedStageTiming details = result.Timing.Details ?? new OcrDetailedStageTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0);
                    return new FullTiming(
                        preprocessing,
                        result.Timing.Detection.TotalMilliseconds,
                        details.DetectionInference.TotalMilliseconds,
                        details.DetectionPostprocessing.TotalMilliseconds,
                        result.Timing.CropAndBatch.TotalMilliseconds,
                        result.Timing.OrientationClassification.TotalMilliseconds,
                        result.Timing.Recognition.TotalMilliseconds,
                        details.RecognitionPreparationWork.TotalMilliseconds,
                        details.RecognitionInferenceWork.TotalMilliseconds,
                        details.RecognitionPostprocessingWork.TotalMilliseconds,
                        details.RecognitionBatchCount,
                        result.Timing.Orchestration.TotalMilliseconds,
                        preprocessing + result.Timing.Total.TotalMilliseconds,
                        0d,
                        0d,
                        preprocessAllocated,
                        pipelineAllocated,
                        result.Regions.Count,
                        ComputeTextSha256(result),
                        ComputeContractSha256(result));
                }
                finally
                {
                    if (reusableInput == null) ownedInput.Dispose();
                }
            }
            for (int i = 0; i < warmup; i++) _ = MeasureOne();
            var values = new List<FullTiming>(iterations); for (int i = 0; i < iterations; i++) values.Add(MeasureOne());
            string? accelerationDetail = null;
            if (backend == "tensorrt")
            {
                string? architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE");
                accelerationDetail = string.IsNullOrWhiteSpace(architecture)
                    ? "TensorRT CUDA sequence argmax is disabled; recognition output uses the CPU fallback"
                    : "TensorRT CUDA sequence argmax is enabled for " + architecture.Trim();
            }
            return FullResultRow.Pass(version, detector.Variant, backend, device, imagePath, FullTiming.Average(values), reusePreparedInput, stageConcurrency, effectiveBatchSize, accelerationDetail);
        }
        catch (OcrPipelineException ex) when (ex.InnerException is OperationCanceledException || ex.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return FullResultRow.Unavailable(version, detector.Variant, backend, device, imagePath, "pipeline-timeout-or-cancelled after " + ReadInt("DEPLOYSHARP_PADDLEOCR_PIPELINE_TIMEOUT_MS", 15000).ToString(Invariant) + " ms; likely backend/model execution is too slow for the configured bound");
        }
        catch (Exception ex)
        {
            if (version == "v4" && (backend == "onnxruntime" || backend == "openvino"))
                return FullResultRow.Unsupported(version, detector.Variant, backend, device, imagePath, "PP-OCRv4 legacy graph output metadata does not match the current strict visual OCR pipeline profile.");
            if (IsRuntimeUnavailable(ex))
                return FullResultRow.Unavailable(version, detector.Variant, backend, device, imagePath, FullResultRow.ExceptionDetail(ex));
            return FullResultRow.Fail(version, detector.Variant, backend, device, imagePath, ex);
        }
        finally
        {
            reusableInput?.Dispose();
        }
    }

    private static bool IsRuntimeUnavailable(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is DeploySharpException deploySharp &&
                (deploySharp.ErrorCode == OnnxRuntimeErrorCodes.ExecutionProviderUnavailable || deploySharp.ErrorCode == DeploySharpErrorCodes.NativeRuntimeUnavailable)) return true;
        }
        return false;
    }

    private static string ComputeTextSha256(OcrResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (OcrRegionResult region in result.Regions.OrderBy(value => value.Region.SourceIndex))
            {
                writer.Write(region.Region.SourceIndex);
                writer.Write(region.Recognition.Text);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)))).ToLowerInvariant();
    }

    private static string ComputeContractSha256(OcrResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (OcrRegionResult item in result.Regions.OrderBy(value => value.Region.SourceIndex))
            {
                writer.Write(item.Region.SourceIndex);
                writer.Write(item.Region.Score);
                writer.Write(item.Region.Polygon.Vertices.Count);
                foreach (JYPPX.DeploySharp.Geometry.PointF vertex in item.Region.Polygon.Vertices)
                {
                    writer.Write(vertex.X);
                    writer.Write(vertex.Y);
                }
                RecognizedText recognition = item.Recognition;
                writer.Write(recognition.Text);
                writer.Write(recognition.Confidence);
                writer.Write(recognition.CharacterSetId);
                writer.Write(recognition.CharacterSetVersion);
                writer.Write(recognition.CharacterSetSha256);
                writer.Write(recognition.Tokens.Count);
                foreach (OcrToken token in recognition.Tokens)
                {
                    writer.Write(token.Timestep);
                    writer.Write(token.ClassIndex);
                    writer.Write(token.Confidence);
                    writer.Write(token.Text ?? string.Empty);
                    writer.Write(token.IsBlank);
                    writer.Write(token.IsCollapsedRepeat);
                    writer.Write(token.IsUnknown);
                    writer.Write(token.Emitted);
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)))).ToLowerInvariant();
    }

    private static PaddleOcrProfile CreateDetectionProfile(string version, ModelCase model, string modelFormat = "onnx")
        => PaddleOcrProfiles.CreateDetection(new ModelId("external/paddleocr/" + version + "/" + model.Variant + "/det"), Artifact(model, modelFormat), outputName: version == "v4" ? "sigmoid_0.tmp_0" : "fetch_name_0");

    private static PaddleOcrProfile CreateRecognitionProfile(string version, ModelCase model, string modelFormat = "onnx")
    {
        string dictionary = version == "v4" ? Path.Combine(Path.GetDirectoryName(model.OnnxPath)!, "ppocrv4_keys.txt") : version == "v5" ? Path.Combine(Path.GetDirectoryName(model.OnnxPath)!, "ppocrv5_dict.txt") : Path.Combine(Path.GetDirectoryName(model.OnnxPath)!, model.Variant == "tiny" ? "PP-OCRv6_tiny_rec_dict.txt" : "PP-OCRv6_" + model.Variant + "_rec_dict.txt");
        OcrCharacterSet chars = LoadBenchmarkCharacterSet(dictionary, "external." + version + ".dict", version);
        return PaddleOcrProfiles.CreateRecognition(new ModelId("external/paddleocr/" + version + "/" + model.Variant + "/rec"), Artifact(model, modelFormat), chars, outputName: version == "v4" ? "softmax_11.tmp_0" : "fetch_name_0");
    }

    private static VisualModelProfile WithStaticOpenCvContract(VisualModelProfile source, TensorShape inputShape, TensorShape outputShape)
    {
        VisualOutputBinding output = source.Outputs.Single();
        int batch = checked((int)inputShape[0]);
        return new VisualModelProfile(
            source.ProfileId + ".opencv-b" + batch.ToString(Invariant), source.ModelId, source.Task, source.Version, source.ModelFormat,
            new VisualInputBinding(source.Input.Name, source.Input.ElementType, inputShape, source.Input.Layout, batch, batch),
            new[] { new VisualOutputBinding(output.Name, output.ElementType, outputShape) }, source.Labels, source.Decoder,
            source.RequiredCapabilities, source.MinimumBackendVersion, source.AuxiliaryInputs);
    }

    private static OpenCvDnnModelContract OpenCvContract(VisualModelProfile profile)
        => new OpenCvDnnModelContract(
            profile.ModelId,
            new[] { new TensorDescriptor(profile.Input.Name, profile.Input.ElementType, profile.Input.ShapePattern) },
            profile.Outputs.Select(output => new TensorDescriptor(output.Name, output.ElementType, output.ShapePattern)));

    private static TensorShape WithBatch(TensorShape shape, int batch)
    {
        long[] dimensions = shape.ToArray();
        dimensions[0] = batch;
        return new TensorShape(dimensions);
    }

    private static TextCropProfile FixedWidthCrop(TextCropProfile source, int width)
        => new TextCropProfile(
            source.ProfileId + ".fixed-w" + width.ToString(Invariant), source.TargetHeight, OcrRecognitionWidthMode.Fixed, width, width,
            source.WidthAlignment, source.Interpolation, source.ColorOrder, source.Layout, source.Means, source.Scales, source.PaddingColor, source.MaximumCropPixels);

    private static OcrCharacterSet LoadBenchmarkCharacterSet(string path, string id, string version)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string raw in File.ReadAllLines(path))
        {
            string token = raw;
            if (token.Length == 0) continue;
            if (!seen.Add(token))
            {
                int suffix = 2; string candidate;
                do { candidate = token + "\u0001" + suffix++; } while (!seen.Add(candidate));
                token = candidate;
            }
            tokens.Add(token);
        }
        tokens.Add(" ");
        return new OcrCharacterSet(id, version, tokens);
    }

    private static PaddleOcrProfile CreateClassificationProfile(string version, ModelCase model, int maximumBatch, string modelFormat = "onnx")
        => version == "v4" ? PaddleOcrProfiles.CreateLegacyClassification(new ModelId("external/paddleocr/v4/cls"), Artifact(model, modelFormat), outputName: "softmax_0.tmp_0", rejectionThreshold: 0f, maximumBatch: maximumBatch, allowDynamicBatch: true) : PaddleOcrProfiles.CreateTextLineOrientationClassification(new ModelId("external/paddleocr/" + version + "/cls"), Artifact(model, modelFormat), outputName: "fetch_name_0", rejectionThreshold: 0f, maximumBatch: maximumBatch, allowDynamicBatch: true);

    private static PaddleOcrArtifactContract Artifact(ModelCase model, string modelFormat = "onnx")
    {
        using SHA256 sha = SHA256.Create();
        string path = string.Equals(modelFormat, "tensorrt-engine", StringComparison.OrdinalIgnoreCase) ? model.EnginePath ?? throw new FileNotFoundException("TensorRT engine sidecar is missing.", model.OnnxPath) : model.OnnxPath;
        string hash = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path))).ToLowerInvariant();
        return new PaddleOcrArtifactContract(7, hash, "external-local", "local-export", "Apache-2.0", "benchmark-image-preprocess-v1", "benchmark-pipeline-postprocess-v1", modelFormat);
    }

    private static List<ModelCase> Discover(string root)
    {
        var result = new List<ModelCase>();
        foreach (string path in Directory.EnumerateFiles(root, "*.onnx", SearchOption.AllDirectories))
        {
            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            string version = path.Contains("PP-OCRv4", StringComparison.OrdinalIgnoreCase) ? "v4" : path.Contains("PP-OCRv5", StringComparison.OrdinalIgnoreCase) ? "v5" : path.Contains("PP-OCRv6", StringComparison.OrdinalIgnoreCase) ? "v6" : "unknown";
            if (version == "unknown") continue;
            string role = file.Contains("cls") ? "cls" : file.Contains("det") ? "det" : file.Contains("rec") ? "rec" : "unknown";
            if (role == "unknown") continue;
            string variant = version == "v6" ? new DirectoryInfo(Path.GetDirectoryName(path)!).Name : "mobile";
            string? engine = FindEngine(path);
            result.Add(new ModelCase(version, variant, role, path, engine, InputShape(version, role), "x"));
        }
        return result.OrderBy(x => x.Version).ThenBy(x => x.Variant).ThenBy(x => x.Role).ToList();
    }

    private static string? FindEngine(string onnxPath)
    {
        string onnxSuffix = onnxPath + ".engine";
        if (File.Exists(onnxSuffix)) return onnxSuffix;
        string direct = Path.ChangeExtension(onnxPath, ".engine");
        if (File.Exists(direct)) return direct;
        string withoutOnnx = Path.Combine(Path.GetDirectoryName(onnxPath)!, Path.GetFileNameWithoutExtension(onnxPath).Replace("_onnx", "", StringComparison.OrdinalIgnoreCase) + ".engine");
        if (File.Exists(withoutOnnx)) return withoutOnnx;
        string withoutInference = Path.Combine(Path.GetDirectoryName(onnxPath)!, Path.GetFileNameWithoutExtension(onnxPath).Replace("_inference", "", StringComparison.OrdinalIgnoreCase) + ".engine");
        return File.Exists(withoutInference) ? withoutInference : null;
    }

    private static TensorShape InputShape(string version, string role)
    {
        if (role == "cls") return version == "v4" ? new TensorShape(1, 3, 48, 192) : new TensorShape(1, 3, 80, 160);
        if (role == "rec") return new TensorShape(1, 3, 48, 320);
        return new TensorShape(1, 3, 736, 736);
    }

    private static ResultRow RunOnnxRuntime(ModelCase model, OnnxRuntimeExecutionProvider providerKind, int warmup, int iterations)
    {
        string backend = providerKind == OnnxRuntimeExecutionProvider.Cpu ? "onnxruntime" : "onnxruntime-cuda";
        string device = providerKind == OnnxRuntimeExecutionProvider.Cpu ? "cpu" : "cuda";
        try
        {
            using var provider = new OnnxRuntimeBackendProvider(new OnnxRuntimeOptions(executionProvider: providerKind));
            var artifact = new ModelArtifact(new ModelId("paddleocr/" + model.Version + "/" + model.Variant + "/" + model.Role), "onnx", model.OnnxPath);
            using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, device), SessionOptions.Default);
            InferenceInputs inputs = InferenceInputs.Create(model.InputName, new Tensor<float>(model.InputShape, new float[(int)model.InputShape.GetElementCount()]));
            Timing timing = Measure(() => session.Run(inputs, default), warmup, iterations);
            return Pass(model, backend, device, timing, "input=" + model.InputShape);
        }
        catch (Exception ex) { return Fail(model, backend, device, ex); }
    }

    private static ResultRow RunOpenVino(ModelCase model, int warmup, int iterations)
    {
        try
        {
            using var provider = new OpenVinoBackendProvider();
            var artifact = new ModelArtifact(new ModelId("paddleocr/" + model.Version + "/" + model.Variant + "/" + model.Role), "onnx", model.OnnxPath);
            using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, OpenVinoBackendProvider.BackendId, "CPU"), SessionOptions.Default);
            InferenceInputs inputs = InferenceInputs.Create(model.InputName, new Tensor<float>(model.InputShape, new float[(int)model.InputShape.GetElementCount()]));
            Timing timing = Measure(() => session.Run(inputs, default), warmup, iterations);
            return Pass(model, "openvino", "CPU", timing, "input=" + model.InputShape);
        }
        catch (Exception ex) { return Fail(model, "openvino", "CPU", ex); }
    }

    private static ResultRow RunOpenCv(ModelCase model, int warmup, int iterations)
    {
        try
        {
            var modelId = new ModelId("paddleocr/" + model.Version + "/" + model.Variant + "/" + model.Role);
            var contract = new OpenCvDnnModelContract(
                modelId,
                new[] { new TensorDescriptor(model.InputName, TensorElementType.Float32, model.InputShape) },
                new[] { OpenCvOutput(model) });
            using var provider = new OpenCvDnnBackendProvider(new OpenCvDnnOptions(contract, enableFusion: false, enableWinograd: false));
            using IInferenceSession session = provider.CreateSession(
                new ModelArtifact(modelId, "onnx", model.OnnxPath),
                new BackendRequest(BackendCapabilities.TensorInference, OpenCvDnnBackendProvider.BackendId, "cpu"),
                SessionOptions.Default);
            InferenceInputs inputs = InferenceInputs.Create(model.InputName, new Tensor<float>(model.InputShape, new float[(int)model.InputShape.GetElementCount()]));
            Timing timing = Measure(() => session.Run(inputs, default), warmup, iterations);
            return Pass(model, "opencv-dnn", "cpu", timing, "input-specialized=true;output=" + contract.Outputs[0].Shape);
        }
        catch (Exception ex) { return Fail(model, "opencv-dnn", "cpu", ex); }
    }

    private static TensorDescriptor OpenCvOutput(ModelCase model)
    {
        if (model.Role == "det")
        {
            string name = model.Version == "v4" ? "sigmoid_0.tmp_0" : "fetch_name_0";
            return new TensorDescriptor(name, TensorElementType.Float32, new TensorShape(1, 1, 736, 736));
        }
        if (model.Role == "cls")
        {
            string name = model.Version == "v4" ? "softmax_0.tmp_0" : "fetch_name_0";
            return new TensorDescriptor(name, TensorElementType.Float32, new TensorShape(1, 2));
        }

        string outputName = model.Version == "v4" ? "softmax_11.tmp_0" : "fetch_name_0";
        int classes = model.Version == "v4" ? 6625 : model.Version == "v5" ? 18385 : model.Variant == "tiny" ? 6906 : 18710;
        return new TensorDescriptor(outputName, TensorElementType.Float32, new TensorShape(1, 40, classes));
    }

    private static ResultRow RunTensorRt(ModelCase model, TensorRtApiVersion apiVersion, int warmup, int iterations)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_RUN_EXTERNAL"), "1", StringComparison.Ordinal))
            return Skip(model, "tensorrt", "cuda", "set DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1 after configuring the native TensorRT bridge/runtime");
        if (model.EnginePath == null) return Skip(model, "tensorrt", "cuda", "matching .engine sidecar not found");
        try
        {
            using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(
                apiVersion,
                cudaTargetArchitecture: Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE"),
                cacheImmutableHostInputsOnDevice: true));
            var artifact = new ModelArtifact(new ModelId("paddleocr/" + model.Version + "/" + model.Variant + "/" + model.Role), "tensorrt-engine", model.EnginePath);
            using IInferenceSession session = provider.CreateSession(artifact, new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"), SessionOptions.Default);
            TensorDescriptor descriptor = session.Metadata.Inputs[0];
            if (descriptor.Shape.IsDynamic) throw new InvalidOperationException("TensorRT engine input shape is dynamic and no optimization profile was selected: " + descriptor.Shape);
            InferenceInputs inputs = InferenceInputs.Create(descriptor.Name, new Tensor<float>(descriptor.Shape, new float[(int)descriptor.Shape.GetElementCount()]));
            Timing timing = Measure(() => session.Run(inputs, default), warmup, iterations);
            return Pass(model, "tensorrt", "cuda", timing, "input=" + descriptor.Shape);
        }
        catch (Exception ex) { return Fail(model, "tensorrt", "cuda", ex); }
    }

    private static Timing Measure(Action action, int warmup, int iterations)
    {
        for (int i = 0; i < warmup; i++) action();
        var values = new double[iterations];
        for (int i = 0; i < iterations; i++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            values[i] = stopwatch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(values);
        return new Timing(values.Average(), Percentile(values, .5), Percentile(values, .95));
    }

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 1) return values[0];
        double index = (values.Length - 1) * percentile;
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);
        if (lower == upper) return values[lower];
        return values[lower] + (values[upper] - values[lower]) * (index - lower);
    }

    private static ResultRow Pass(ModelCase model, string backend, string device, Timing timing, string detail)
    {
        var row = new ResultRow(model, backend, device, "pass", timing.Mean, timing.P50, timing.P95, detail);
        Console.WriteLine(row.ToLog());
        return row;
    }

    private static ResultRow Skip(ModelCase model, string backend, string device, string detail)
    {
        var row = new ResultRow(model, backend, device, "skip", null, null, null, detail);
        Console.WriteLine(row.ToLog());
        return row;
    }

    private static ResultRow Fail(ModelCase model, string backend, string device, Exception exception)
    {
        string detail = exception.GetType().Name + ": " + exception.Message.Replace((char)13, ' ').Replace((char)10, ' ');
        if (exception is DeploySharpException deploySharp && !string.IsNullOrWhiteSpace(deploySharp.TechnicalDetails))
            detail += " | " + deploySharp.TechnicalDetails!.Replace((char)13, ' ').Replace((char)10, ' ');
        bool tensorRtBridgeUnavailable = backend == "tensorrt" && detail.Contains("BridgeProbeException", StringComparison.OrdinalIgnoreCase);
        string status = backend.IndexOf("cuda", StringComparison.OrdinalIgnoreCase) >= 0 || tensorRtBridgeUnavailable ? "unavailable" : backend == "opencv-dnn" ? "unsupported" : "fail";
        var row = new ResultRow(model, backend, device, status, null, null, null, detail);
        Console.WriteLine(row.ToLog());
        return row;
    }

    private static int ReadInt(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, Invariant, out int value) && value > 0 ? value : fallback;
    }

    private static int ReadNonNegativeInt(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, Invariant, out int value) && value >= 0 ? value : fallback;
    }

    private static double ReadDouble(string name, double fallback)
    {
        return double.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, Invariant, out double value) && value >= 1.0 && !double.IsInfinity(value) ? value : fallback;
    }

    private static bool ReadBool(string name, bool fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value == "1" || bool.TryParse(value, out bool parsed) && parsed;
    }

    private static TensorRtApiVersion ReadTensorRtApiVersion()
    {
        string? value = Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_API_VERSION");
        if (string.IsNullOrWhiteSpace(value)) return TensorRtApiVersion.TensorRt11;
        if (int.TryParse(value, NumberStyles.Integer, Invariant, out int numeric) && Enum.IsDefined(typeof(TensorRtApiVersion), numeric))
            return (TensorRtApiVersion)numeric;
        if (Enum.TryParse(value, true, out TensorRtApiVersion parsed) && Enum.IsDefined(typeof(TensorRtApiVersion), parsed))
            return parsed;
        throw new ArgumentException("DEPLOYSHARP_TENSORRT_API_VERSION must be 8, 10, 11, TensorRt8, TensorRt10, or TensorRt11.");
    }

    private sealed record ModelCase(string Version, string Variant, string Role, string OnnxPath, string? EnginePath, TensorShape InputShape, string InputName);
    private readonly record struct Timing(double Mean, double P50, double P95);
    private sealed record ResultRow(ModelCase Model, string Backend, string Device, string Status, double? Mean, double? P50, double? P95, string Detail)
    {
        public string ToLog() => "PADDLEOCR_BENCHMARK version=" + Model.Version + ";variant=" + Model.Variant + ";role=" + Model.Role + ";backend=" + Backend + ";device=" + Device + ";status=" + Status + (Mean.HasValue ? ";meanMs=" + Mean.Value.ToString("F3", Invariant) + ";p50Ms=" + P50!.Value.ToString("F3", Invariant) + ";p95Ms=" + P95!.Value.ToString("F3", Invariant) : ";detail=" + Detail);
        public string ToCsv() => string.Join(",", new[] { Model.Version, Model.Variant, Model.Role, Backend, Device, Status, Csv(Mean), Csv(P50), Csv(P95), Csv(Model.OnnxPath), Csv(Detail) });
        private static string Csv(double? value) => value.HasValue ? value.Value.ToString("F3", Invariant) : "";
        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private readonly record struct FullTiming(double Preprocess, double Detection, double DetectionInference, double DetectionPostprocess, double Crop, double Orientation, double Recognition, double RecognitionPrepareWork, double RecognitionInferenceWork, double RecognitionPostprocessWork, int RecognitionBatches, double Merge, double Total, double TotalP50, double TotalP95, long PreprocessAllocatedBytes, long PipelineProcessAllocatedBytes, int Regions, string ResultTextSha256, string ResultContractSha256)
    {
        public static FullTiming Average(IReadOnlyList<FullTiming> values)
        {
            string[] hashes = values.Select(value => value.ResultTextSha256).Distinct(StringComparer.Ordinal).ToArray();
            if (hashes.Length != 1) throw new InvalidOperationException("OCR text output changed between timed iterations.");
            string[] contractHashes = values.Select(value => value.ResultContractSha256).Distinct(StringComparer.Ordinal).ToArray();
            if (contractHashes.Length != 1) throw new InvalidOperationException("OCR result contract changed between timed iterations.");
            double[] totals = values.Select(value => value.Total).OrderBy(value => value).ToArray();
            return new FullTiming(
                values.Average(x => x.Preprocess),
                values.Average(x => x.Detection),
                values.Average(x => x.DetectionInference),
                values.Average(x => x.DetectionPostprocess),
                values.Average(x => x.Crop),
                values.Average(x => x.Orientation),
                values.Average(x => x.Recognition),
                values.Average(x => x.RecognitionPrepareWork),
                values.Average(x => x.RecognitionInferenceWork),
                values.Average(x => x.RecognitionPostprocessWork),
                (int)Math.Round(values.Average(x => x.RecognitionBatches)),
                values.Average(x => x.Merge),
                values.Average(x => x.Total),
                Percentile(totals, .5),
                Percentile(totals, .95),
                checked((long)values.Average(x => x.PreprocessAllocatedBytes)),
                checked((long)values.Average(x => x.PipelineProcessAllocatedBytes)),
                (int)Math.Round(values.Average(x => x.Regions)),
                hashes[0],
                contractHashes[0]);
        }
    }

    private sealed record FullResultRow(string Version, string Variant, string Backend, string Device, string Status, FullTiming? Timing, int? Regions, int? SelectedBatchSize, int? SelectedInferenceChannels, string ImagePath, string Detail)
    {
        public static FullResultRow Pass(string version, string variant, string backend, string device, string image, FullTiming timing, bool reusedInput, int inferenceChannels, int batchSize, string? accelerationDetail = null)
        {
            string inputTiming = reusedInput ? "prepared detector input and decoded source are reused; preprocess_ms is zero and total_ms is the warm pipeline latency" : "preprocess includes image decode and detector tensor creation";
            string detail = "end-to-end OCR pipeline; timings exclude model load; " + inputTiming + "; crop/recognition scale with detected region count";
            if (!string.IsNullOrWhiteSpace(accelerationDetail)) detail += "; " + accelerationDetail;
            var row = new FullResultRow(version, variant, backend, device, "pass", timing, timing.Regions, batchSize, inferenceChannels, image, detail);
            Console.WriteLine(row.ToLog()); return row;
        }
        public static FullResultRow Unsupported(string version, string variant, string backend, string device, string image, string detail)
        {
            var row = new FullResultRow(version, variant, backend, device, "unsupported", null, null, null, null, image, detail);
            Console.WriteLine(row.ToLog()); return row;
        }
        public static FullResultRow Unavailable(string version, string variant, string backend, string device, string image, string detail)
        {
            var row = new FullResultRow(version, variant, backend, device, "unavailable", null, null, null, null, image, detail);
            Console.WriteLine(row.ToLog()); return row;
        }
        public static FullResultRow Fail(string version, string variant, string backend, string device, string image, Exception ex)
        {
            string detail = ExceptionDetail(ex);
            var row = new FullResultRow(version, variant, backend, device, "fail", null, null, null, null, image, detail);
            Console.WriteLine(row.ToLog()); return row;
        }
        public static string ExceptionDetail(Exception ex)
        {
            string detail = ex.GetType().Name + ": " + ex.Message.Replace((char)13, ' ').Replace((char)10, ' ');
            var errorCodes = new List<string>();
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is DeploySharpException deploySharp && !errorCodes.Contains(deploySharp.ErrorCode, StringComparer.Ordinal)) errorCodes.Add(deploySharp.ErrorCode);
            }
            if (errorCodes.Count > 0) detail += " | errorCodes=" + string.Join("->", errorCodes);
            string? technical = FindConciseTechnicalDetails(ex);
            if (!string.IsNullOrWhiteSpace(technical)) detail += " | " + technical;
            return detail;
        }
        private static string? FindConciseTechnicalDetails(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is not DeploySharpException deploySharp || string.IsNullOrWhiteSpace(deploySharp.TechnicalDetails)) continue;
                string value = deploySharp.TechnicalDetails!.Replace((char)13, ' ').Replace((char)10, ' ').Trim();
                if (value.IndexOf(" at ", StringComparison.Ordinal) >= 0) continue;
                return value;
            }
            return null;
        }
        public string ToLog() => "PADDLEOCR_FULL version=" + Version + ";variant=" + Variant + ";backend=" + Backend + ";device=" + Device + ";status=" + Status + (Timing.HasValue ? ";selectedBatchSize=" + SelectedBatchSize + ";selectedInferenceChannels=" + SelectedInferenceChannels + ";preprocessMs=" + Timing.Value.Preprocess.ToString("F3", Invariant) + ";detectionMs=" + Timing.Value.Detection.ToString("F3", Invariant) + ";detectionInferenceMs=" + Timing.Value.DetectionInference.ToString("F3", Invariant) + ";detectionPostprocessMs=" + Timing.Value.DetectionPostprocess.ToString("F3", Invariant) + ";cropMs=" + Timing.Value.Crop.ToString("F3", Invariant) + ";orientationMs=" + Timing.Value.Orientation.ToString("F3", Invariant) + ";recognitionMs=" + Timing.Value.Recognition.ToString("F3", Invariant) + ";recognitionPrepareWorkMs=" + Timing.Value.RecognitionPrepareWork.ToString("F3", Invariant) + ";recognitionInferenceWorkMs=" + Timing.Value.RecognitionInferenceWork.ToString("F3", Invariant) + ";recognitionPostprocessWorkMs=" + Timing.Value.RecognitionPostprocessWork.ToString("F3", Invariant) + ";recognitionBatches=" + Timing.Value.RecognitionBatches.ToString(Invariant) + ";mergeMs=" + Timing.Value.Merge.ToString("F3", Invariant) + ";totalMs=" + Timing.Value.Total.ToString("F3", Invariant) + ";totalP50Ms=" + Timing.Value.TotalP50.ToString("F3", Invariant) + ";totalP95Ms=" + Timing.Value.TotalP95.ToString("F3", Invariant) + ";preprocessAllocated=" + Timing.Value.PreprocessAllocatedBytes.ToString(Invariant) + ";pipelineProcessAllocated=" + Timing.Value.PipelineProcessAllocatedBytes.ToString(Invariant) + ";regions=" + Regions + ";resultTextSha256=" + Timing.Value.ResultTextSha256 + ";resultContractSha256=" + Timing.Value.ResultContractSha256 : ";detail=" + Detail);
        public string ToCsv()
        {
            FullTiming t = Timing.GetValueOrDefault();
            string[] timings = Timing.HasValue
                ? new[] { N(t.Preprocess), N(t.Detection), N(t.DetectionInference), N(t.DetectionPostprocess), N(t.Crop), N(t.Orientation), N(t.Recognition), N(t.RecognitionPrepareWork), N(t.RecognitionInferenceWork), N(t.RecognitionPostprocessWork), t.RecognitionBatches.ToString(Invariant), N(t.Merge), N(t.Total), N(t.TotalP50), N(t.TotalP95), t.PreprocessAllocatedBytes.ToString(Invariant), t.PipelineProcessAllocatedBytes.ToString(Invariant) }
                : new[] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" };
            return string.Join(",", new[] { Csv(Version), Csv(Variant), Csv(Backend), Csv(Device), Csv(Status), SelectedBatchSize?.ToString(Invariant) ?? "", SelectedInferenceChannels?.ToString(Invariant) ?? "" }.Concat(timings).Concat(new[] { Regions?.ToString(Invariant) ?? "", Timing.HasValue ? Csv(t.ResultTextSha256) : "", Timing.HasValue ? Csv(t.ResultContractSha256) : "", Csv(ImagePath), Csv(Detail) }));
        }
        private static string N(double value) => value == 0d && double.IsNaN(value) ? "" : value.ToString("F3", Invariant);
        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
