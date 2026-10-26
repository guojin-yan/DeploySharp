#if NET8_0
using System;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Backends.TensorRT;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.TensorRT
{
    internal sealed class PaddleOcrVlGpuLogitsReducer : IDisposable
    {
        private readonly TensorRtCudaCompiledKernel _kernel;
        private readonly TensorRtCudaDeviceBuffer _logits;
        private readonly TensorRtCudaDeviceBuffer _classIndex;
        private readonly TensorRtCudaDeviceBuffer _confidence;
        private readonly TensorRtCudaDeviceBuffer _invalidOffset;
        private readonly CudaMemory _classIndexMemory;
        private readonly CudaMemory _confidenceMemory;
        private readonly CudaMemory _invalidOffsetMemory;
        private readonly byte[] _classIndexBytes = new byte[sizeof(int)];
        private readonly byte[] _invalidOffsetBytes = new byte[sizeof(int)];

        private PaddleOcrVlGpuLogitsReducer(TensorRtCudaCompiledKernel kernel, CudaMemory logitsMemory, int vocabularySize)
        {
            _kernel = kernel;
            CudaMemory? classIndexMemory = null, confidenceMemory = null, invalidOffsetMemory = null;
            try
            {
                classIndexMemory = new CudaMemory(sizeof(int));
                confidenceMemory = new CudaMemory(sizeof(float));
                invalidOffsetMemory = new CudaMemory(sizeof(int));
                _logits = Buffer("logits", TensorElementType.Float32, new TensorShape(1, 1, vocabularySize),
                    TensorRtCudaBufferAccess.Read, logitsMemory);
                _classIndex = Buffer("vl.class-index", TensorElementType.Int32, new TensorShape(1, 1),
                    TensorRtCudaBufferAccess.Write, classIndexMemory);
                _confidence = Buffer("vl.confidence", TensorElementType.Float32, new TensorShape(1, 1),
                    TensorRtCudaBufferAccess.Write, confidenceMemory);
                _invalidOffset = Buffer("vl.invalid-offset", TensorElementType.Int32, new TensorShape(1),
                    TensorRtCudaBufferAccess.Write, invalidOffsetMemory);
                _classIndexMemory = classIndexMemory;
                _confidenceMemory = confidenceMemory;
                _invalidOffsetMemory = invalidOffsetMemory;
            }
            catch
            {
                invalidOffsetMemory?.Dispose();
                confidenceMemory?.Dispose();
                classIndexMemory?.Dispose();
                throw;
            }
        }

        internal static PaddleOcrVlGpuLogitsReducer? TryCreate(CudaMemory logitsMemory, int vocabularySize, int deviceOrdinal)
        {
            try
            {
                CudaDeviceInfo device = CudaDevice.GetInfo(deviceOrdinal);
                string architecture = "compute_" + device.Major + device.Minor;
                var options = new TensorRtCudaRtcCompileOptions(architecture, TensorRtCudaRtcArtifactKind.Ptx, useFastMath: false);
                TensorRtCudaRtcArtifact artifact = TensorRtCudaRtcCompiler.Compile(TensorRtCudaOcrKernels.CtcTraceDefinition, options);
                TensorRtCudaCompiledKernel kernel = TensorRtCudaCompiledKernel.Load(artifact, deviceOrdinal);
                try { return new PaddleOcrVlGpuLogitsReducer(kernel, logitsMemory, vocabularySize); }
                catch { kernel.Dispose(); throw; }
            }
            catch (TensorRtBackendException) { return null; }
            catch (CudaException) { return null; }
        }

        internal int Select(CudaStream stream, int vocabularySize)
        {
            using (TensorRtCudaKernelLaunch launch = TensorRtCudaOcrKernels.LaunchCtcTrace(
                _kernel, stream, _logits, _classIndex, _confidence, _invalidOffset,
                batch: 1, time: 1, classes: vocabularySize,
                timeBatchClasses: false, applySoftmax: false, requireUnitInterval: false))
            {
                launch.Synchronize();
            }
            _invalidOffsetMemory.CopyTo(_invalidOffsetBytes);
            if (BitConverter.ToInt32(_invalidOffsetBytes, 0) >= 0)
                throw new InvalidOperationException("PaddleOCR-VL logits contain non-finite values.");
            _classIndexMemory.CopyTo(_classIndexBytes);
            int selected = BitConverter.ToInt32(_classIndexBytes, 0);
            if (selected < 0 || selected >= vocabularySize)
                throw new InvalidOperationException("PaddleOCR-VL GPU argmax returned an invalid token ID.");
            return selected;
        }

        public void Dispose()
        {
            _kernel.Dispose();
            _invalidOffsetMemory.Dispose();
            _confidenceMemory.Dispose();
            _classIndexMemory.Dispose();
        }

        private static TensorRtCudaDeviceBuffer Buffer(string name, TensorElementType type, TensorShape shape,
            TensorRtCudaBufferAccess access, CudaMemory memory) =>
            new TensorRtCudaDeviceBuffer(new TensorRtCudaBufferDescriptor(name, type, shape, access), memory);
    }
}
#endif
