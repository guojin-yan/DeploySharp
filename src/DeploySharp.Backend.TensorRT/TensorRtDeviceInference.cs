using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Tensors;
using JYPPX.TensorRtSharp;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Exposes the optional caller-owned CUDA execution surface of a TensorRT session. / 暴露 TensorRT 会话的可选 CUDA 设备执行接口。</summary>
    public interface ITensorRtDeviceInferenceSession : IInferenceSession
    {
        /// <summary>Gets the CUDA device ordinal used by this TensorRT execution context. / 获取 TensorRT 执行上下文使用的 CUDA 设备序号。</summary>
        public int DeviceOrdinal { get; }

        /// <summary>
        /// Enqueues inference using caller-owned device buffers and a caller-owned CUDA stream.
        /// The returned execution keeps the session occupied until it is disposed, so multiple stages can be chained on one stream safely.
        /// 使用调用方拥有的设备缓冲区和 CUDA stream 将推理入队；返回的执行租约在释放前保持会话占用，因此可以安全地在同一 stream 上串联多个阶段。
        /// </summary>
        public TensorRtDeviceInferenceExecution RunDevice(
            IReadOnlyList<TensorRtDeviceTensor> inputs,
            IReadOnlyList<TensorRtDeviceTensor> outputs,
            CudaStream stream,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Describes one caller-owned CUDA tensor allocation and its runtime shape. / 描述一个调用方拥有的 CUDA tensor 分配及其运行时形状。</summary>
    public sealed class TensorRtDeviceTensor
    {
        /// <summary>Initializes a device tensor descriptor without exposing a native pointer. / 初始化设备 tensor 描述，不暴露原生指针。</summary>
        public TensorRtDeviceTensor(string name, TensorElementType elementType, TensorShape shape, CudaMemory memory)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A TensorRT tensor name is required.", nameof(name));
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (shape.IsDynamic) throw new ArgumentException("A device tensor shape must be fully static.", nameof(shape));
            long elementCount = shape.GetElementCount();
            if (elementCount <= 0) throw new ArgumentException("A device tensor shape must contain at least one element.", nameof(shape));
            int elementSize = GetElementSize(elementType);
            long byteLength = checked(elementCount * elementSize);
            if (byteLength > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(shape), "The device tensor exceeds Int32.MaxValue bytes.");
            if (memory.SizeInBytes < byteLength) throw new ArgumentException("The caller-owned CUDA allocation is smaller than the tensor shape.", nameof(memory));
            CudaPointerAttributes attributes = memory.GetPointerAttributes();
            if (attributes.MemoryType != CudaMemoryPointerType.Device) throw new ArgumentException("The TensorRT device tensor must use CUDA device memory.", nameof(memory));

            Name = name.Trim();
            ElementType = elementType;
            Shape = shape;
            Memory = memory;
            ByteLength = (int)byteLength;
            DeviceOrdinal = attributes.DeviceOrdinal;
        }

        /// <summary>Gets the TensorRT tensor name. / 获取 TensorRT tensor 名称。</summary>
        public string Name { get; }
        /// <summary>Gets the managed element type. / 获取托管元素类型。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the fully static runtime shape. / 获取完整静态运行时形状。</summary>
        public TensorShape Shape { get; }
        /// <summary>Gets the borrowed CUDA allocation. / 获取借用的 CUDA 设备内存。</summary>
        public CudaMemory Memory { get; }
        /// <summary>Gets the number of bytes covered by the shape. The allocation may be larger for a maximum output shape. / 获取形状覆盖的字节数；显存分配可以为了最大输出形状而更大。</summary>
        public int ByteLength { get; }
        /// <summary>Gets the CUDA device ordinal reported when the allocation was bound. / 获取绑定时显存所属的 CUDA 设备序号。</summary>
        public int DeviceOrdinal { get; }

        private static int GetElementSize(TensorElementType elementType)
        {
            return elementType switch
            {
                TensorElementType.Boolean or TensorElementType.Int8 or TensorElementType.UInt8 => 1,
                TensorElementType.Int16 or TensorElementType.UInt16 or TensorElementType.Float16 or TensorElementType.BFloat16 => 2,
                TensorElementType.Int32 or TensorElementType.UInt32 or TensorElementType.Float32 => 4,
                TensorElementType.Int64 or TensorElementType.UInt64 or TensorElementType.Float64 => 8,
                _ => throw new ArgumentException("The device tensor element type must have a fixed unmanaged width.", nameof(elementType))
            };
        }
    }

    /// <summary>Owns one asynchronous TensorRT enqueue while borrowing all caller-owned buffers and the CUDA stream. / 表示一次异步 TensorRT 入队，借用调用方的缓冲区和 CUDA stream。</summary>
    public sealed class TensorRtDeviceInferenceExecution : IDisposable
    {
        private readonly Action _release;
        private int _disposed;

        internal TensorRtDeviceInferenceExecution(CudaStream stream, IReadOnlyList<TensorRtDeviceTensor> outputs, Action release)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Outputs = new ReadOnlyCollection<TensorRtDeviceTensor>(outputs?.ToList() ?? throw new ArgumentNullException(nameof(outputs)));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        /// <summary>Gets the caller-owned stream carrying this execution. / 获取承载本次执行的调用方 CUDA stream。</summary>
        public CudaStream Stream { get; }
        /// <summary>Gets output tensors that remain in device memory until the caller releases them. / 获取仍保留在设备内存中的输出 tensor。</summary>
        public IReadOnlyList<TensorRtDeviceTensor> Outputs { get; }

        /// <summary>Synchronizes the execution stream and surfaces asynchronous CUDA/TensorRT failures. / 同步执行 stream 并报告异步 CUDA/TensorRT 错误。</summary>
        public void Synchronize()
        {
            ThrowIfDisposed();
            Stream.Synchronize();
        }

        /// <summary>
        /// Releases the session lease without synchronizing. Use only after all dependent work has been enqueued on the same CUDA stream.
        /// 不同步地释放会话租约；仅应在同一 CUDA stream 上的所有依赖工作都已入队后调用。
        /// </summary>
        public void ReleaseAfterEnqueue()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _release();
            GC.SuppressFinalize(this);
        }

        /// <summary>Synchronizes the stream and releases the session lease. / 同步 stream 并释放会话租约。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { Stream.Synchronize(); }
            finally { _release(); }
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(TensorRtDeviceInferenceExecution));
        }
    }
}
