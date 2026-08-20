using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.CudaSharp;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Identifies how a CUDA kernel accesses one caller-owned device buffer. / 定义或说明 CUDA合同。</summary>
    public enum TensorRtCudaBufferAccess
    {
        /// <summary>The kernel only reads the buffer range. / 表示 CUDA状态或选项。</summary>
        Read = 1,
        /// <summary>The kernel only writes the buffer range. / 表示 CUDA状态或选项。</summary>
        Write = 2,
        /// <summary>The kernel may read and write the buffer range. / 表示 CUDA状态或选项。</summary>
        ReadWrite = 3
    }

    /// <summary>Describes one exact typed/shape-bound device-buffer range without owning memory. / 定义或说明张量或缓冲区合同。</summary>
    public sealed class TensorRtCudaBufferDescriptor
    {
        /// <summary>Initializes an immutable buffer-range contract. / 初始化张量或缓冲区对象。</summary>
        public TensorRtCudaBufferDescriptor(
            string name,
            TensorElementType elementType,
            TensorShape shape,
            TensorRtCudaBufferAccess access,
            int byteOffset = 0,
            int? byteLength = null)
        {
            Name = TensorRtContractHash.ValidateText(name, nameof(name), allowEmpty: false);
            if (!Enum.IsDefined(typeof(TensorRtCudaBufferAccess), access)) throw new ArgumentOutOfRangeException(nameof(access));
            int elementSize = GetElementSize(elementType);
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            if (shape.IsDynamic) throw new ArgumentException("A CUDA device-buffer shape must be fully static.", nameof(shape));
            long elementCount = shape.GetElementCount();
            if (elementCount <= 0) throw new ArgumentException("A CUDA device-buffer shape must contain at least one element.", nameof(shape));
            long requiredBytes = checked(elementCount * elementSize);
            if (requiredBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(shape), "The CUDA device-buffer byte length exceeds Int32.MaxValue.");
            if (byteOffset < 0) throw new ArgumentOutOfRangeException(nameof(byteOffset));
            int exactLength = byteLength ?? checked((int)requiredBytes);
            if (exactLength != requiredBytes) throw new ArgumentException("The CUDA device-buffer byte length must exactly match its element type and shape.", nameof(byteLength));

            ElementType = elementType;
            Access = access;
            ByteOffset = byteOffset;
            ByteLength = exactLength;
            IdentitySha256 = TensorRtContractHash.Sequence(new[]
            {
                "deploysharp-tensorrt-cuda-buffer-v1",
                Name,
                ((int)ElementType).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Shape.ToString(),
                ((int)Access).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ByteOffset.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }

        /// <summary>Gets the logical buffer name. / 获取张量或缓冲区信息。</summary>
        public string Name { get; }
        /// <summary>Gets the exact element type. / 获取相关信息。</summary>
        public TensorElementType ElementType { get; }
        /// <summary>Gets the fully static logical shape. / 获取形状或执行配置信息。</summary>
        public TensorShape Shape { get; }
        /// <summary>Gets the declared kernel access mode. / 获取 CUDA信息。</summary>
        public TensorRtCudaBufferAccess Access { get; }
        /// <summary>Gets the byte offset in the caller-owned allocation. / 获取路径信息。</summary>
        public int ByteOffset { get; }
        /// <summary>Gets the exact byte length covered by the contract. / 获取相关信息。</summary>
        public int ByteLength { get; }
        /// <summary>Gets the buffer-contract identity SHA256. / 获取张量或缓冲区信息。</summary>
        public string IdentitySha256 { get; }

        private static int GetElementSize(TensorElementType elementType)
        {
            return elementType switch
            {
                TensorElementType.Boolean or TensorElementType.Int8 or TensorElementType.UInt8 => 1,
                TensorElementType.Int16 or TensorElementType.UInt16 or TensorElementType.Float16 or TensorElementType.BFloat16 => 2,
                TensorElementType.Int32 or TensorElementType.UInt32 or TensorElementType.Float32 => 4,
                TensorElementType.Int64 or TensorElementType.UInt64 or TensorElementType.Float64 => 8,
                _ => throw new ArgumentException("The CUDA device-buffer element type must have a fixed unmanaged width.", nameof(elementType))
            };
        }
    }

    /// <summary>Binds a buffer contract to caller-owned CUDA device memory. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaDeviceBuffer
    {
        /// <summary>Initializes a borrowed device-buffer binding. / 初始化张量或缓冲区对象。</summary>
        public TensorRtCudaDeviceBuffer(TensorRtCudaBufferDescriptor descriptor, CudaMemory memory)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            if (descriptor.ByteOffset > memory.SizeInBytes || descriptor.ByteLength > memory.SizeInBytes - descriptor.ByteOffset)
            {
                throw new ArgumentException("The CUDA device-buffer contract exceeds the caller-owned allocation.", nameof(memory));
            }
            CudaPointerAttributes attributes = memory.GetPointerAttributes();
            if (attributes.MemoryType != CudaMemoryPointerType.Device)
            {
                throw new ArgumentException("The CUDA kernel buffer must be device memory.", nameof(memory));
            }
            DeviceOrdinal = attributes.DeviceOrdinal;
        }

        /// <summary>Gets the immutable typed/shape-bound range contract. / 获取形状或执行配置信息。</summary>
        public TensorRtCudaBufferDescriptor Descriptor { get; }
        /// <summary>Gets the borrowed caller-owned CUDA allocation. / 获取 CUDA信息。</summary>
        public CudaMemory Memory { get; }
        /// <summary>Gets the CUDA device ordinal reported for the allocation. / 获取 CUDA信息。</summary>
        public int DeviceOrdinal { get; }
    }

    /// <summary>Identifies a copied scalar or borrowed device-buffer kernel argument. / 定义或说明 CUDA合同。</summary>
    public enum TensorRtCudaKernelArgumentKind
    {
        /// <summary>A copied scalar value. / 表示参数状态或选项。</summary>
        Scalar = 1,
        /// <summary>A borrowed caller-owned device buffer. / 表示张量或缓冲区状态或选项。</summary>
        DeviceBuffer = 2
    }

    /// <summary>Represents one ordered typed CUDA kernel argument. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaKernelArgument
    {
        private readonly CudaKernelArgument _nativeArgument;

        private TensorRtCudaKernelArgument(CudaKernelArgument nativeArgument, CudaKernelScalarType scalarType, byte[] scalarBytes)
        {
            _nativeArgument = nativeArgument;
            Kind = TensorRtCudaKernelArgumentKind.Scalar;
            ScalarType = scalarType;
            ScalarSizeInBytes = scalarBytes.Length;
            ScalarValueSha256 = TensorRtContractHash.Bytes(scalarBytes);
        }

        private TensorRtCudaKernelArgument(TensorRtCudaDeviceBuffer buffer)
        {
            Buffer = buffer;
            _nativeArgument = CudaKernelArgument.FromDeviceMemory(buffer.Memory, buffer.Descriptor.ByteOffset);
            Kind = TensorRtCudaKernelArgumentKind.DeviceBuffer;
            ScalarType = CudaKernelScalarType.None;
        }

        /// <summary>Gets the argument ownership/layout kind. / 获取参数信息。</summary>
        public TensorRtCudaKernelArgumentKind Kind { get; }
        /// <summary>Gets the copied scalar type, or None for a device buffer. / 获取张量或缓冲区信息。</summary>
        public CudaKernelScalarType ScalarType { get; }
        /// <summary>Gets the copied scalar byte length. / 获取参数信息。</summary>
        public int ScalarSizeInBytes { get; }
        /// <summary>Gets the copied scalar-value SHA256, or null for a device buffer. / 获取张量或缓冲区信息。</summary>
        public string? ScalarValueSha256 { get; }
        /// <summary>Gets the borrowed buffer binding, or null for a scalar. / 获取张量或缓冲区信息。</summary>
        public TensorRtCudaDeviceBuffer? Buffer { get; }

        /// <summary>Creates a borrowed device-buffer argument. / 创建张量或缓冲区对象。</summary>
        public static TensorRtCudaKernelArgument FromDeviceBuffer(TensorRtCudaDeviceBuffer buffer) => new TensorRtCudaKernelArgument(buffer ?? throw new ArgumentNullException(nameof(buffer)));
        /// <summary>Copies a Boolean scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromBoolean(bool value) => Scalar(CudaKernelArgument.FromBoolean(value), CudaKernelScalarType.Boolean, BitConverter.GetBytes(value));
        /// <summary>Copies an unsigned 8-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromByte(byte value) => Scalar(CudaKernelArgument.FromByte(value), CudaKernelScalarType.Byte, new[] { value });
        /// <summary>Copies a signed 8-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromSByte(sbyte value) => Scalar(CudaKernelArgument.FromSByte(value), CudaKernelScalarType.SByte, new[] { unchecked((byte)value) });
        /// <summary>Copies a signed 16-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromInt16(short value) => Scalar(CudaKernelArgument.FromInt16(value), CudaKernelScalarType.Int16, BitConverter.GetBytes(value));
        /// <summary>Copies an unsigned 16-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromUInt16(ushort value) => Scalar(CudaKernelArgument.FromUInt16(value), CudaKernelScalarType.UInt16, BitConverter.GetBytes(value));
        /// <summary>Copies a signed 32-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromInt32(int value) => Scalar(CudaKernelArgument.FromInt32(value), CudaKernelScalarType.Int32, BitConverter.GetBytes(value));
        /// <summary>Copies an unsigned 32-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromUInt32(uint value) => Scalar(CudaKernelArgument.FromUInt32(value), CudaKernelScalarType.UInt32, BitConverter.GetBytes(value));
        /// <summary>Copies a signed 64-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromInt64(long value) => Scalar(CudaKernelArgument.FromInt64(value), CudaKernelScalarType.Int64, BitConverter.GetBytes(value));
        /// <summary>Copies an unsigned 64-bit scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromUInt64(ulong value) => Scalar(CudaKernelArgument.FromUInt64(value), CudaKernelScalarType.UInt64, BitConverter.GetBytes(value));
        /// <summary>Copies an IEEE 754 single-precision scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromSingle(float value) => Scalar(CudaKernelArgument.FromSingle(value), CudaKernelScalarType.Single, BitConverter.GetBytes(value));
        /// <summary>Copies an IEEE 754 double-precision scalar argument. / 复制参数数据。</summary>
        public static TensorRtCudaKernelArgument FromDouble(double value) => Scalar(CudaKernelArgument.FromDouble(value), CudaKernelScalarType.Double, BitConverter.GetBytes(value));

        internal CudaKernelArgument NativeArgument => _nativeArgument;

        private static TensorRtCudaKernelArgument Scalar(CudaKernelArgument nativeArgument, CudaKernelScalarType scalarType, byte[] bytes)
        {
            return new TensorRtCudaKernelArgument(nativeArgument, scalarType, bytes);
        }
    }

    /// <summary>Defines the explicit synchronization boundary after a CUDA Driver launch. / 定义或说明 CUDA合同。</summary>
    public enum TensorRtCudaSynchronizationMode
    {
        /// <summary>Return immediately; the returned launch owner must later be synchronized or disposed. / 表示相关状态或选项。</summary>
        CallerManaged = 0,
        /// <summary>Wait for this kernel's completion event before returning. / 表示 CUDA状态或选项。</summary>
        KernelCompletion = 1,
        /// <summary>Synchronize all work queued on the supplied caller-owned stream before returning. / 表示数据流状态或选项。</summary>
        StreamCompletion = 2
    }

    /// <summary>Contains an exact CUDA grid/block/shared-memory and synchronization contract. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaKernelLaunchOptions
    {
        /// <summary>Initializes explicit launch settings. / 初始化配置对象。</summary>
        public TensorRtCudaKernelLaunchOptions(
            uint gridX,
            uint blockX,
            TensorRtCudaSynchronizationMode synchronizationMode,
            uint gridY = 1,
            uint gridZ = 1,
            uint blockY = 1,
            uint blockZ = 1,
            int dynamicSharedMemoryBytes = 0)
        {
            if (!Enum.IsDefined(typeof(TensorRtCudaSynchronizationMode), synchronizationMode)) throw new ArgumentOutOfRangeException(nameof(synchronizationMode));
            NativeConfiguration = new CudaKernelLaunchConfiguration(
                new CudaDim3(gridX, gridY, gridZ),
                new CudaDim3(blockX, blockY, blockZ),
                dynamicSharedMemoryBytes);
            SynchronizationMode = synchronizationMode;
        }

        /// <summary>Gets the grid X extent. / 获取形状或执行配置信息。</summary>
        public uint GridX => NativeConfiguration.GridDimensions.X;
        /// <summary>Gets the grid Y extent. / 获取形状或执行配置信息。</summary>
        public uint GridY => NativeConfiguration.GridDimensions.Y;
        /// <summary>Gets the grid Z extent. / 获取形状或执行配置信息。</summary>
        public uint GridZ => NativeConfiguration.GridDimensions.Z;
        /// <summary>Gets the block X extent. / 获取形状或执行配置信息。</summary>
        public uint BlockX => NativeConfiguration.BlockDimensions.X;
        /// <summary>Gets the block Y extent. / 获取形状或执行配置信息。</summary>
        public uint BlockY => NativeConfiguration.BlockDimensions.Y;
        /// <summary>Gets the block Z extent. / 获取形状或执行配置信息。</summary>
        public uint BlockZ => NativeConfiguration.BlockDimensions.Z;
        /// <summary>Gets dynamic shared-memory bytes per block. / 获取形状或执行配置信息。</summary>
        public int DynamicSharedMemoryBytes => NativeConfiguration.DynamicSharedMemoryBytes;
        /// <summary>Gets the explicit post-launch synchronization policy. / 获取配置信息。</summary>
        public TensorRtCudaSynchronizationMode SynchronizationMode { get; }

        internal CudaKernelLaunchConfiguration NativeConfiguration { get; }
    }

    /// <summary>Captures the complete immutable managed identity of one kernel launch. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaKernelLaunchIdentity
    {
        internal TensorRtCudaKernelLaunchIdentity(
            TensorRtCudaRtcArtifact artifact,
            TensorRtCudaKernelLaunchOptions options,
            IReadOnlyList<TensorRtCudaKernelArgument> arguments)
        {
            var fields = new List<string>
            {
                "deploysharp-tensorrt-cuda-launch-v1",
                artifact.ArtifactSha256,
                artifact.KernelName,
                options.GridX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                options.GridY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                options.GridZ.ToString(System.Globalization.CultureInfo.InvariantCulture),
                options.BlockX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                options.BlockY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                options.BlockZ.ToString(System.Globalization.CultureInfo.InvariantCulture),
                options.DynamicSharedMemoryBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ((int)options.SynchronizationMode).ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            var bufferDescriptors = new List<TensorRtCudaBufferDescriptor>();
            foreach (TensorRtCudaKernelArgument argument in arguments)
            {
                fields.Add(((int)argument.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture));
                fields.Add(((int)argument.ScalarType).ToString(System.Globalization.CultureInfo.InvariantCulture));
                fields.Add(argument.ScalarSizeInBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
                fields.Add(argument.ScalarValueSha256 ?? string.Empty);
                if (argument.Buffer != null)
                {
                    fields.Add(argument.Buffer.Descriptor.IdentitySha256);
                    fields.Add(argument.Buffer.DeviceOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    bufferDescriptors.Add(argument.Buffer.Descriptor);
                }
                else
                {
                    fields.Add(string.Empty);
                    fields.Add(string.Empty);
                }
            }
            BufferDescriptors = new ReadOnlyCollection<TensorRtCudaBufferDescriptor>(bufferDescriptors);
            LaunchInputsSha256 = TensorRtContractHash.Sequence(fields);
        }

        /// <summary>Gets device-buffer contracts in kernel argument order, excluding scalar arguments. / 获取 CUDA信息。</summary>
        public IReadOnlyList<TensorRtCudaBufferDescriptor> BufferDescriptors { get; }
        /// <summary>Gets the exact managed launch-input SHA256. / 获取哈希标识信息。</summary>
        public string LaunchInputsSha256 { get; }
    }
}
