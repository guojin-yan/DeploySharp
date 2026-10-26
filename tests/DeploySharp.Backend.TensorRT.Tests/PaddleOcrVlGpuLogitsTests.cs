using System;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Backend.TensorRT.Tests;

[TestClass]
public sealed class PaddleOcrVlGpuLogitsTests
{
    private const int VocabularySize = 103424;

    [TestMethod]
    [TestCategory("ExternalIntegration")]
    public void FullVocabularyArgMaxPreservesCpuTieAndNonFiniteRules()
    {
        if (Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VL_GPU_ARGMAX_TEST") != "1")
            Assert.Inconclusive("Enable this test on a CUDA/NVRTC device with DEPLOYSHARP_PADDLEOCR_VL_GPU_ARGMAX_TEST=1.");

        TensorRtNativePreflight.Validate(new ModelId("paddleocr-vl/1.6/gpu-argmax-test"));
        int deviceOrdinal = CudaDevice.Current;
        CudaDeviceInfo device = CudaDevice.GetInfo(deviceOrdinal);
        string architecture = "compute_" + device.Major + device.Minor;
        var options = new TensorRtCudaRtcCompileOptions(architecture, TensorRtCudaRtcArtifactKind.Ptx, useFastMath: false);
        using TensorRtCudaCompiledKernel kernel = TensorRtCudaCompiledKernel.Load(
            TensorRtCudaRtcCompiler.Compile(TensorRtCudaOcrKernels.CtcTraceDefinition, options), deviceOrdinal);
        using var stream = new CudaStream();
        using var logitsMemory = new CudaMemory(VocabularySize * sizeof(float));
        using var classMemory = new CudaMemory(sizeof(int));
        using var confidenceMemory = new CudaMemory(sizeof(float));
        using var invalidMemory = new CudaMemory(sizeof(int));
        var logits = Buffer("logits", TensorElementType.Float32, new TensorShape(1, 1, VocabularySize),
            TensorRtCudaBufferAccess.Read, logitsMemory);
        var classes = Buffer("classes", TensorElementType.Int32, new TensorShape(1, 1),
            TensorRtCudaBufferAccess.Write, classMemory);
        var confidences = Buffer("confidences", TensorElementType.Float32, new TensorShape(1, 1),
            TensorRtCudaBufferAccess.Write, confidenceMemory);
        var invalidOffsets = Buffer("invalid", TensorElementType.Int32, new TensorShape(1),
            TensorRtCudaBufferAccess.Write, invalidMemory);

        (int selected, int invalid) Reduce(float[] values)
        {
            logitsMemory.CopyFrom(values);
            using (TensorRtCudaKernelLaunch launch = TensorRtCudaOcrKernels.LaunchCtcTrace(
                kernel, stream, logits, classes, confidences, invalidOffsets,
                1, 1, VocabularySize, false, false, false))
                launch.Synchronize();
            return (BitConverter.ToInt32(classMemory.ToArray(sizeof(int)), 0),
                BitConverter.ToInt32(invalidMemory.ToArray(sizeof(int)), 0));
        }

        var values = new float[VocabularySize];
        for (int index = 0; index < values.Length; index++) values[index] = (index % 97) * 0.001f;
        values[13] = 9;
        values[VocabularySize - 1] = 9;
        Assert.AreEqual((13, -1), Reduce(values), "Equal maxima must select the lowest token ID.");

        values[13] = 0;
        values[VocabularySize - 1] = 10;
        Assert.AreEqual((VocabularySize - 1, -1), Reduce(values), "The last vocabulary entry must be scanned.");

        values[31] = float.NaN;
        Assert.AreEqual(31, Reduce(values).invalid, "A non-finite logit must be rejected even when it is not the maximum.");
    }

    private static TensorRtCudaDeviceBuffer Buffer(string name, TensorElementType type, TensorShape shape,
        TensorRtCudaBufferAccess access, CudaMemory memory) =>
        new(new TensorRtCudaBufferDescriptor(name, type, shape, access), memory);
}
