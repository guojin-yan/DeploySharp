using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;

internal static class Program
{
    private static int Main(string[] args)
    {
        bool load = args.Any(value => string.Equals(value, "--load", StringComparison.OrdinalIgnoreCase));
        bool execute = args.Any(value => string.Equals(value, "--execute", StringComparison.OrdinalIgnoreCase));
        bool benchmark = args.Any(value => string.Equals(value, "--benchmark", StringComparison.OrdinalIgnoreCase));
        load |= execute;
        load |= benchmark;
        string architecture = Environment.GetEnvironmentVariable("DEPLOYSHARP_CUDA_ARCHITECTURE") ?? "compute_75";
        var definitions = new[]
        {
            TensorRtCudaOcrKernels.NormalizeLetterboxDefinition,
            TensorRtCudaOcrKernels.HomographyDefinition,
            TensorRtCudaOcrKernels.PerspectiveCropDefinition,
            TensorRtCudaOcrKernels.PerspectiveCropFromQuadrilateralDefinition,
            TensorRtCudaOcrKernels.CtcDecodeDefinition,
            TensorRtCudaOcrKernels.CtcTraceDefinition
        };
        var options = new TensorRtCudaRtcCompileOptions(architecture, TensorRtCudaRtcArtifactKind.Ptx, useFastMath: true);
        Console.WriteLine("DEPLOYSHARP_CUDA_OCR_PROBE architecture=" + architecture + ";load=" + load + ";execute=" + execute + ";benchmark=" + benchmark);
        int passed = 0;
        bool executionPassed = !execute;
        bool enginePassed = true;
        var kernels = new Dictionary<string, TensorRtCudaCompiledKernel>(StringComparer.Ordinal);
        foreach (TensorRtCudaRtcKernelDefinition definition in definitions)
        {
            try
            {
                TensorRtCudaRtcArtifact artifact = TensorRtCudaRtcCompiler.Compile(definition, options);
                if (load)
                {
                    kernels[definition.KernelName] = TensorRtCudaCompiledKernel.Load(artifact, 0);
                }
                Console.WriteLine("CUDA_OCR_KERNEL status=pass;name=" + definition.KernelName + ";bytes=" + artifact.Length + ";sha256=" + artifact.ArtifactSha256);
                passed++;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("CUDA_OCR_KERNEL status=fail;name=" + definition.KernelName + ";error=" + exception);
            }
        }
        if (execute && passed == definitions.Length)
        {
            try
            {
                ExecuteSmoke(kernels);
                Console.WriteLine("CUDA_OCR_KERNEL_EXECUTION status=pass;stream=single;stages=normalize,homography,crop,ctc");
                executionPassed = true;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("CUDA_OCR_KERNEL_EXECUTION status=fail;error=" + exception);
                executionPassed = false;
            }
        }
        if (benchmark && passed == definitions.Length)
        {
            try
            {
                RunKernelBenchmark(kernels);
                Console.WriteLine("CUDA_OCR_KERNEL_BENCHMARK status=pass;stream=single;profile=736x736-det,16x48x320-crops,16x40x18385-ctc");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("CUDA_OCR_KERNEL_BENCHMARK status=fail;error=" + exception);
                executionPassed = false;
            }
        }
        string? enginePath = Environment.GetEnvironmentVariable("DEPLOYSHARP_TENSORRT_ENGINE");
        if (!string.IsNullOrWhiteSpace(enginePath))
        {
            try
            {
                RunEngineSmoke(enginePath);
                Console.WriteLine("CUDA_OCR_TENSORRT_EXECUTION status=pass;stream=single;engine=" + enginePath);
                enginePassed = true;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("CUDA_OCR_TENSORRT_EXECUTION status=fail;engine=" + enginePath + ";error=" + exception);
                enginePassed = false;
            }
        }
        foreach (TensorRtCudaCompiledKernel kernel in kernels.Values) kernel.Dispose();
        Console.WriteLine("DEPLOYSHARP_CUDA_OCR_PROBE_RESULT=" + passed + "/" + definitions.Length);
        return passed == definitions.Length && executionPassed && enginePassed ? 0 : 1;
    }

    private static void ExecuteSmoke(IReadOnlyDictionary<string, TensorRtCudaCompiledKernel> kernels)
    {
        using var stream = new CudaStream();
        const int batch = 2;
        using var source = new CudaMemory(4 * 4 * 3);
        using var normalized = new CudaMemory(3 * 4 * 4 * sizeof(float));
        using var quadrilateral = new CudaMemory(batch * 8 * sizeof(float));
        using var homography = new CudaMemory(batch * 9 * sizeof(float));
        using var crops = new CudaMemory(batch * 3 * 4 * 4 * sizeof(float));
        using var logits = new CudaMemory(batch * 4 * 3 * sizeof(float));
        using var tokenIds = new CudaMemory(batch * 8 * sizeof(int));
        using var lengths = new CudaMemory(batch * sizeof(int));
        using var confidences = new CudaMemory(batch * sizeof(float));
        using var traceClasses = new CudaMemory(batch * 4 * sizeof(int));
        using var traceConfidences = new CudaMemory(batch * 4 * sizeof(float));
        using var invalidOffsets = new CudaMemory(batch * sizeof(int));

        source.CopyFrom(new byte[4 * 4 * 3]);
        quadrilateral.CopyFrom(new[] { 0f, 0f, 3f, 0f, 3f, 3f, 0f, 3f, 0f, 0f, 3f, 0f, 3f, 3f, 0f, 3f });
        logits.CopyFrom(new[] { 0f, 1f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 1f, 0f });
        var sourceBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("source", TensorElementType.UInt8, new TensorShape(4, 4, 3), TensorRtCudaBufferAccess.Read), source);
        var normalizedBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("normalized", TensorElementType.Float32, new TensorShape(1, 3, 4, 4), TensorRtCudaBufferAccess.Write), normalized);
        var quadrilateralBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("quadrilateral", TensorElementType.Float32, new TensorShape(batch, 8), TensorRtCudaBufferAccess.Read), quadrilateral);
        var homographyBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("homography", TensorElementType.Float32, new TensorShape(batch, 9), TensorRtCudaBufferAccess.Write), homography);
        var cropsBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("crops", TensorElementType.Float32, new TensorShape(batch, 3, 4, 4), TensorRtCudaBufferAccess.Write), crops);
        var logitsBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("logits", TensorElementType.Float32, new TensorShape(batch, 4, 3), TensorRtCudaBufferAccess.Read), logits);
        var tokenIdsBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("tokenIds", TensorElementType.Int32, new TensorShape(batch, 8), TensorRtCudaBufferAccess.Write), tokenIds);
        var lengthsBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("lengths", TensorElementType.Int32, new TensorShape(batch), TensorRtCudaBufferAccess.Write), lengths);
        var confidencesBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("confidences", TensorElementType.Float32, new TensorShape(batch), TensorRtCudaBufferAccess.Write), confidences);
        var traceClassesBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("traceClasses", TensorElementType.Int32, new TensorShape(batch, 4), TensorRtCudaBufferAccess.Write), traceClasses);
        var traceConfidencesBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("traceConfidences", TensorElementType.Float32, new TensorShape(batch, 4), TensorRtCudaBufferAccess.Write), traceConfidences);
        var invalidOffsetsBuffer = new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor("invalidOffsets", TensorElementType.Int32, new TensorShape(batch), TensorRtCudaBufferAccess.Write), invalidOffsets);

        using TensorRtCudaKernelLaunch normalize = TensorRtCudaOcrKernels.LaunchNormalizeLetterbox(kernels["deploysharp_normalize_letterbox"], stream, sourceBuffer, normalizedBuffer, 4, 4, 4, 4, 0, 0, 0, 0, 1f);
        using TensorRtCudaKernelLaunch h = TensorRtCudaOcrKernels.LaunchHomography(kernels["deploysharp_quad_to_homography"], stream, quadrilateralBuffer, homographyBuffer, batch);
        using TensorRtCudaKernelLaunch crop = TensorRtCudaOcrKernels.LaunchPerspectiveCrop(kernels["deploysharp_perspective_crop"], stream, sourceBuffer, homographyBuffer, cropsBuffer, 4, 4, 4, 4, batch, 0, 0, 0, 1f);
        // The fused path consumes detector quadrilaterals directly and avoids
        // the intermediate homography write/read. Keep the split launch above
        // in the smoke graph as a compatibility check, then overwrite the same
        // output on the same stream with the optimized path.
        using TensorRtCudaKernelLaunch fusedCrop = TensorRtCudaOcrKernels.LaunchPerspectiveCropFromQuadrilaterals(kernels["deploysharp_perspective_crop_quad"], stream, sourceBuffer, quadrilateralBuffer, cropsBuffer, 4, 4, 4, 4, batch, 0, 0, 0, 1f);
        using TensorRtCudaKernelLaunch ctc = TensorRtCudaOcrKernels.LaunchCtcDecode(kernels["deploysharp_ctc_decode"], stream, logitsBuffer, tokenIdsBuffer, lengthsBuffer, confidencesBuffer, batch, 4, 3, 2, 8);
        using TensorRtCudaKernelLaunch trace = TensorRtCudaOcrKernels.LaunchCtcTrace(kernels["deploysharp_ctc_trace"], stream, logitsBuffer, traceClassesBuffer, traceConfidencesBuffer, invalidOffsetsBuffer, batch, 4, 3, timeBatchClasses: false, applySoftmax: false, requireUnitInterval: true);
        stream.Synchronize();
        int[] decodedLengths = ReadInt32(lengths, batch);
        int[] decodedTokens = ReadInt32(tokenIds, batch * 8);
        float[] decodedConfidences = ReadSingle(confidences, batch);
        if (decodedLengths.Any(value => value != 2) || decodedTokens[0] != 1 || decodedTokens[1] != 1 || decodedTokens[8] != 1 || decodedTokens[9] != 1 || decodedConfidences.Any(value => value <= 0 || value > 1))
        {
            throw new InvalidOperationException("The GPU CTC smoke output did not match the expected greedy collapse.");
        }
        int[] tracedClasses = ReadInt32(traceClasses, batch * 4);
        float[] tracedConfidences = ReadSingle(traceConfidences, batch * 4);
        int[] tracedInvalidOffsets = ReadInt32(invalidOffsets, batch);
        if (!tracedClasses.SequenceEqual(new[] { 1, 1, 2, 1, 1, 1, 2, 1 }) || tracedConfidences.Any(value => value != 1f) || tracedInvalidOffsets.Any(value => value != -1))
        {
            throw new InvalidOperationException("The GPU CTC trace output did not preserve the expected per-timestep contract.");
        }
    }

    private static void RunKernelBenchmark(IReadOnlyDictionary<string, TensorRtCudaCompiledKernel> kernels)
    {
        const int sourceWidth = 1920;
        const int sourceHeight = 1080;
        const int detectorWidth = 736;
        const int detectorHeight = 736;
        const int regionCount = 16;
        const int cropWidth = 320;
        const int cropHeight = 48;
        const int ctcTime = 40;
        const int ctcClasses = 18385;
        const int maximumTokens = 40;
        int warmup = ReadPositiveInt("DEPLOYSHARP_CUDA_OCR_BENCH_WARMUP", 5);
        int iterations = ReadPositiveInt("DEPLOYSHARP_CUDA_OCR_BENCH_ITERATIONS", 30);

        using var stream = new CudaStream();
        using var source = new CudaMemory(checked(sourceWidth * sourceHeight * 3));
        using var normalized = new CudaMemory(checked(3 * detectorWidth * detectorHeight * sizeof(float)));
        using var quadrilateral = new CudaMemory(checked(regionCount * 8 * sizeof(float)));
        using var homography = new CudaMemory(checked(regionCount * 9 * sizeof(float)));
        using var crops = new CudaMemory(checked(regionCount * 3 * cropHeight * cropWidth * sizeof(float)));
        using var logits = new CudaMemory(checked(regionCount * ctcTime * ctcClasses * sizeof(float)));
        using var tokenIds = new CudaMemory(checked(regionCount * maximumTokens * sizeof(int)));
        using var lengths = new CudaMemory(checked(regionCount * sizeof(int)));
        using var confidences = new CudaMemory(checked(regionCount * sizeof(float)));

        source.Fill(0);
        quadrilateral.CopyFrom(CreateBenchmarkQuadrilaterals(regionCount, sourceWidth - 1, sourceHeight - 1));
        logits.Fill(0);

        TensorRtCudaDeviceBuffer sourceBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("source", TensorElementType.UInt8, new TensorShape(sourceHeight, sourceWidth, 3), TensorRtCudaBufferAccess.Read), source);
        TensorRtCudaDeviceBuffer normalizedBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("normalized", TensorElementType.Float32, new TensorShape(1, 3, detectorHeight, detectorWidth), TensorRtCudaBufferAccess.Write), normalized);
        TensorRtCudaDeviceBuffer quadrilateralBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("quadrilateral", TensorElementType.Float32, new TensorShape(regionCount, 8), TensorRtCudaBufferAccess.Read), quadrilateral);
        TensorRtCudaDeviceBuffer homographyBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("homography", TensorElementType.Float32, new TensorShape(regionCount, 9), TensorRtCudaBufferAccess.Write), homography);
        TensorRtCudaDeviceBuffer cropsBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("crops", TensorElementType.Float32, new TensorShape(regionCount, 3, cropHeight, cropWidth), TensorRtCudaBufferAccess.Write), crops);
        TensorRtCudaDeviceBuffer logitsBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("logits", TensorElementType.Float32, new TensorShape(regionCount, ctcTime, ctcClasses), TensorRtCudaBufferAccess.Read), logits);
        TensorRtCudaDeviceBuffer tokenIdsBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("tokenIds", TensorElementType.Int32, new TensorShape(regionCount, maximumTokens), TensorRtCudaBufferAccess.Write), tokenIds);
        TensorRtCudaDeviceBuffer lengthsBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("lengths", TensorElementType.Int32, new TensorShape(regionCount), TensorRtCudaBufferAccess.Write), lengths);
        TensorRtCudaDeviceBuffer confidencesBuffer = new TensorRtCudaDeviceBuffer(
            new TensorRtCudaBufferDescriptor("confidences", TensorElementType.Float32, new TensorShape(regionCount), TensorRtCudaBufferAccess.Write), confidences);

        MeasureKernel("normalize_letterbox", warmup, iterations, stream, () => new[]
        {
            TensorRtCudaOcrKernels.LaunchNormalizeLetterbox(kernels["deploysharp_normalize_letterbox"], stream, sourceBuffer, normalizedBuffer, sourceWidth, sourceHeight, detectorWidth, detectorHeight, 0, .485f, .456f, .406f, 1f / 255f)
        });
        MeasureKernel("crop_split", warmup, iterations, stream, () => new[]
        {
            TensorRtCudaOcrKernels.LaunchHomography(kernels["deploysharp_quad_to_homography"], stream, quadrilateralBuffer, homographyBuffer, regionCount),
            TensorRtCudaOcrKernels.LaunchPerspectiveCrop(kernels["deploysharp_perspective_crop"], stream, sourceBuffer, homographyBuffer, cropsBuffer, sourceWidth, sourceHeight, cropWidth, cropHeight, regionCount, 0, 0, 0, 1f)
        });
        MeasureKernel("crop_fused", warmup, iterations, stream, () => new[]
        {
            TensorRtCudaOcrKernels.LaunchPerspectiveCropFromQuadrilaterals(kernels["deploysharp_perspective_crop_quad"], stream, sourceBuffer, quadrilateralBuffer, cropsBuffer, sourceWidth, sourceHeight, cropWidth, cropHeight, regionCount, 0, 0, 0, 1f)
        });
        MeasureKernel("ctc_decode", warmup, iterations, stream, () => new[]
        {
            TensorRtCudaOcrKernels.LaunchCtcDecode(kernels["deploysharp_ctc_decode"], stream, logitsBuffer, tokenIdsBuffer, lengthsBuffer, confidencesBuffer, regionCount, ctcTime, ctcClasses, ctcClasses - 1, maximumTokens, applySoftmax: false)
        });
        MeasureKernel("prepost_fused", warmup, iterations, stream, () => new[]
        {
            TensorRtCudaOcrKernels.LaunchNormalizeLetterbox(kernels["deploysharp_normalize_letterbox"], stream, sourceBuffer, normalizedBuffer, sourceWidth, sourceHeight, detectorWidth, detectorHeight, 0, .485f, .456f, .406f, 1f / 255f),
            TensorRtCudaOcrKernels.LaunchPerspectiveCropFromQuadrilaterals(kernels["deploysharp_perspective_crop_quad"], stream, sourceBuffer, quadrilateralBuffer, cropsBuffer, sourceWidth, sourceHeight, cropWidth, cropHeight, regionCount, 0, 0, 0, 1f),
            TensorRtCudaOcrKernels.LaunchCtcDecode(kernels["deploysharp_ctc_decode"], stream, logitsBuffer, tokenIdsBuffer, lengthsBuffer, confidencesBuffer, regionCount, ctcTime, ctcClasses, ctcClasses - 1, maximumTokens, applySoftmax: false)
        });
    }

    private static void MeasureKernel(string stage, int warmup, int iterations, CudaStream stream, Func<TensorRtCudaKernelLaunch[]> enqueue)
    {
        for (int i = 0; i < warmup; i++)
        {
            TensorRtCudaKernelLaunch[] launches = enqueue();
            stream.Synchronize();
            DisposeLaunches(launches);
        }

        var values = new double[iterations];
        for (int i = 0; i < iterations; i++)
        {
            TensorRtCudaKernelLaunch[] launches = Array.Empty<TensorRtCudaKernelLaunch>();
            try
            {
                // CUDA events measure device execution only. The stop event
                // also establishes the one synchronization boundary for this
                // iteration, so host launch overhead is not reported as GPU time.
                values[i] = stream.MeasureElapsedTime(_ => launches = enqueue());
            }
            finally
            {
                DisposeLaunches(launches);
            }
        }
        Array.Sort(values);
        Console.WriteLine("CUDA_OCR_KERNEL_TIMING stage=" + stage + ";warmup=" + warmup + ";iterations=" + iterations + ";mean_ms=" + values.Average().ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";p50_ms=" + Percentile(values, .5).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";p95_ms=" + Percentile(values, .95).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void DisposeLaunches(IReadOnlyList<TensorRtCudaKernelLaunch> launches)
    {
        for (int i = launches.Count - 1; i >= 0; i--) launches[i].Dispose();
    }

    private static float[] CreateBenchmarkQuadrilaterals(int count, int right, int bottom)
    {
        var values = new float[checked(count * 8)];
        for (int i = 0; i < count; i++)
        {
            int offset = i * 8;
            values[offset] = 0;
            values[offset + 1] = 0;
            values[offset + 2] = right;
            values[offset + 3] = 0;
            values[offset + 4] = right;
            values[offset + 5] = bottom;
            values[offset + 6] = 0;
            values[offset + 7] = bottom;
        }
        return values;
    }

    private static int ReadPositiveInt(string name, int fallback)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out int value) && value > 0 ? value : fallback;
    }

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0) return 0d;
        if (values.Length == 1) return values[0];
        double index = (values.Length - 1) * percentile;
        int lower = (int)Math.Floor(index);
        int upper = (int)Math.Ceiling(index);
        return lower == upper ? values[lower] : values[lower] + (values[upper] - values[lower]) * (index - lower);
    }

    private static int[] ReadInt32(CudaMemory memory, int count)
    {
        byte[] bytes = memory.ToArray(checked(count * sizeof(int)));
        var values = new int[count];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }

    private static float[] ReadSingle(CudaMemory memory, int count)
    {
        return memory.ToSingleArray(count);
    }

    private static void RunEngineSmoke(string enginePath)
    {
        using var provider = new TensorRtBackendProvider(new TensorRtBackendOptions(TensorRtApiVersion.TensorRt11));
        using IInferenceSession session = provider.CreateSession(
            new ModelArtifact(new ModelId("cuda-ocr-probe"), "tensorrt-engine", enginePath),
            new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"),
            new SessionOptions(maxConcurrency: 1));
        if (session is not ITensorRtDeviceInferenceSession deviceSession)
        {
            throw new InvalidOperationException("The TensorRT provider did not expose its device execution surface.");
        }
        if (session.Metadata.Inputs.Any(descriptor => descriptor.Shape.IsDynamic) || session.Metadata.Outputs.Any(descriptor => descriptor.Shape.IsDynamic))
        {
            throw new InvalidOperationException("The probe requires an engine with static input and output shapes.");
        }
        var inputMemory = new List<CudaMemory>();
        var outputMemory = new List<CudaMemory>();
        try
        {
            var inputs = new List<TensorRtDeviceTensor>(session.Metadata.Inputs.Count);
            foreach (TensorDescriptor descriptor in session.Metadata.Inputs)
            {
                CudaMemory memory = new CudaMemory(checked((int)(descriptor.Shape.GetElementCount() * ElementSize(descriptor.ElementType))));
                inputMemory.Add(memory);
                memory.Fill(0);
                inputs.Add(new TensorRtDeviceTensor(descriptor.Name, descriptor.ElementType, descriptor.Shape, memory));
            }
            var outputs = new List<TensorRtDeviceTensor>(session.Metadata.Outputs.Count);
            foreach (TensorDescriptor descriptor in session.Metadata.Outputs)
            {
                CudaMemory memory = new CudaMemory(checked((int)(descriptor.Shape.GetElementCount() * ElementSize(descriptor.ElementType))));
                outputMemory.Add(memory);
                outputs.Add(new TensorRtDeviceTensor(descriptor.Name, descriptor.ElementType, descriptor.Shape, memory));
            }
            using var stream = new CudaStream();
            TensorRtDeviceInferenceExecution first = deviceSession.RunDevice(inputs, outputs, stream);
            first.ReleaseAfterEnqueue();
            TensorRtDeviceInferenceExecution second = deviceSession.RunDevice(inputs, outputs, stream);
            second.ReleaseAfterEnqueue();
            stream.Synchronize();
        }
        finally
        {
            foreach (CudaMemory memory in inputMemory) memory.Dispose();
            foreach (CudaMemory memory in outputMemory) memory.Dispose();
        }
    }

    private static int ElementSize(TensorElementType elementType)
    {
        return elementType switch
        {
            TensorElementType.Boolean or TensorElementType.Int8 or TensorElementType.UInt8 => 1,
            TensorElementType.Int16 or TensorElementType.UInt16 or TensorElementType.Float16 or TensorElementType.BFloat16 => 2,
            TensorElementType.Int32 or TensorElementType.UInt32 or TensorElementType.Float32 => 4,
            TensorElementType.Int64 or TensorElementType.UInt64 or TensorElementType.Float64 => 8,
            _ => throw new NotSupportedException("Unsupported static TensorRT probe element type: " + elementType)
        };
    }
}
