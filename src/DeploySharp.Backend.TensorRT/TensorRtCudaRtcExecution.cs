using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.CudaSharp;
using JYPPX.TensorRtSharp.Shared.Interop;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Compiles explicit CUDA C++ source into copied in-memory PTX/CUBIN without writing a cache. / 说明缓存公共 API。</summary>
    public static class TensorRtCudaRtcCompiler
    {
        /// <summary>Compiles one kernel definition using the consumer-owned NVRTC/native bridge installation. / 说明 CUDA公共 API。</summary>
        public static TensorRtCudaRtcArtifact Compile(TensorRtCudaRtcKernelDefinition definition, TensorRtCudaRtcCompileOptions options)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (options == null) throw new ArgumentNullException(nameof(options));
            try
            {
                var headers = definition.Headers.Select(header => new CudaRtcHeader(header.IncludeName, header.Source));
                IEnumerable<string>? nameExpressions = definition.KernelNameExpression == null
                    ? null
                    : new[] { definition.KernelNameExpression };
                var source = new CudaRtcProgramSource(definition.Source, definition.ProgramName, headers, nameExpressions);
                CudaRtcCompilationResult result = CudaRtcCompiler.Compile(source, options.NativeOptions);
                if (!result.Success)
                {
                    throw new TensorRtBackendException(
                        TensorRtErrorCodes.CudaCompilationFailed,
                        "NVRTC rejected the caller-supplied CUDA kernel source.",
                        operation: "cuda-rtc-compile",
                        technicalDetails: "result=" + result.ResultCode + ";compiler=" + result.CompilerVersion + ";log=" + result.Log);
                }

                CudaRtcArtifactKind nativeKind = options.ArtifactKind == TensorRtCudaRtcArtifactKind.Ptx
                    ? CudaRtcArtifactKind.Ptx
                    : CudaRtcArtifactKind.Cubin;
                CudaRtcArtifact? nativeArtifact = result.FindArtifact(nativeKind);
                if (nativeArtifact == null)
                {
                    throw new TensorRtBackendException(
                        TensorRtErrorCodes.CudaCompilationFailed,
                        "NVRTC did not emit the requested Driver-loadable artifact.",
                        operation: "cuda-rtc-artifact",
                        technicalDetails: "kind=" + options.ArtifactKind + ";compiler=" + result.CompilerVersion);
                }

                string resolvedKernelName = definition.KernelName;
                if (definition.KernelNameExpression != null)
                {
                    CudaRtcLoweredName? loweredName = result.LoweredNames.SingleOrDefault(value =>
                        string.Equals(value.Expression, definition.KernelNameExpression, StringComparison.Ordinal));
                    if (loweredName == null || string.IsNullOrWhiteSpace(loweredName.LoweredName))
                    {
                        throw new TensorRtBackendException(
                            TensorRtErrorCodes.CudaCompilationFailed,
                            "NVRTC did not resolve the requested CUDA kernel name expression.",
                            operation: "cuda-rtc-lowered-name");
                    }
                    resolvedKernelName = loweredName.LoweredName;
                }

                return new TensorRtCudaRtcArtifact(
                    nativeArtifact.ToArray(),
                    options.ArtifactKind,
                    definition.Role,
                    nativeArtifact.SourceSha256,
                    nativeArtifact.HeadersSha256,
                    nativeArtifact.OptionsSha256,
                    nativeArtifact.CompilerVersion,
                    nativeArtifact.TargetArchitecture,
                    definition.ProgramName,
                    resolvedKernelName,
                    definition.KernelNameExpression,
                    nativeArtifact.Sha256);
            }
            catch (TensorRtBackendException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.CudaCompilationFailed,
                    "CUDA runtime compilation failed in the consumer-owned native runtime.",
                    exception,
                    operation: "cuda-rtc-compile",
                    technicalDetails: exception.GetType().FullName);
            }
        }
    }

    /// <summary>Owns one in-memory CUDA Driver module while borrowing all launch streams and buffers. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaCompiledKernel : IDisposable
    {
        private readonly object _lifetimeGate = new object();
        private readonly CudaDriverModule? _module;
        private int _activeLaunches;
        private bool _disposed;

        private TensorRtCudaCompiledKernel(TensorRtCudaRtcArtifact artifact, int deviceOrdinal, CudaDriverModule? module)
        {
            Artifact = artifact;
            DeviceOrdinal = deviceOrdinal;
            _module = module;
        }

        /// <summary>Gets the copied source/artifact/kernel identity. / 获取 CUDA信息。</summary>
        public TensorRtCudaRtcArtifact Artifact { get; }
        /// <summary>Gets the CUDA device whose primary context owns the module. / 获取 CUDA信息。</summary>
        public int DeviceOrdinal { get; }

        /// <summary>Loads copied PTX/CUBIN bytes into the selected consumer-owned CUDA Driver primary context. / 加载 CUDA资源。</summary>
        public static TensorRtCudaCompiledKernel Load(TensorRtCudaRtcArtifact artifact, int deviceOrdinal)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (deviceOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(deviceOrdinal));
            try
            {
                return new TensorRtCudaCompiledKernel(artifact, deviceOrdinal, CudaDriverModule.Load(artifact.ToArray(), deviceOrdinal));
            }
            catch (Exception exception)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.CudaCompilationFailed,
                    "The consumer-owned CUDA Driver could not load the in-memory PTX/CUBIN artifact.",
                    exception,
                    operation: "cuda-module-load",
                    technicalDetails: "device=" + deviceOrdinal + ";artifact=" + artifact.ArtifactSha256 + ";exception=" + exception.GetType().FullName);
            }
        }

        internal static TensorRtCudaCompiledKernel CreateManagedTestDouble(TensorRtCudaRtcArtifact artifact, int deviceOrdinal)
        {
            return new TensorRtCudaCompiledKernel(artifact, deviceOrdinal, module: null);
        }

        /// <summary>Launches the kernel on an explicit caller-owned stream with ordered scalar/buffer arguments. / 执行 CUDA操作。</summary>
        public TensorRtCudaKernelLaunch Launch(
            CudaStream stream,
            TensorRtCudaKernelLaunchOptions options,
            IEnumerable<TensorRtCudaKernelArgument> arguments)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            TensorRtCudaKernelArgument[] copiedArguments = arguments.ToArray();
            if (copiedArguments.Any(argument => argument == null)) throw new ArgumentException("CUDA kernel arguments cannot contain null entries.", nameof(arguments));
            int streamDeviceOrdinal = ResolveStreamDeviceOrdinal(stream);
            if (streamDeviceOrdinal != DeviceOrdinal)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.CudaContractInvalid,
                    "The caller-owned CUDA stream and loaded module must use the same device.",
                    operation: "cuda-kernel-device",
                    technicalDetails: "moduleDevice=" + DeviceOrdinal + ";streamDevice=" + streamDeviceOrdinal);
            }
            TensorRtCudaDeviceBuffer? mismatchedBuffer = copiedArguments
                .Where(argument => argument.Buffer != null)
                .Select(argument => argument.Buffer)
                .FirstOrDefault(buffer => buffer!.DeviceOrdinal != DeviceOrdinal);
            if (mismatchedBuffer != null)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.CudaContractInvalid,
                    "Every caller-owned CUDA device buffer must use the module and stream device.",
                    tensorName: mismatchedBuffer.Descriptor.Name,
                    operation: "cuda-kernel-device",
                    technicalDetails: "moduleDevice=" + DeviceOrdinal + ";bufferDevice=" + mismatchedBuffer.DeviceOrdinal);
            }
            var identity = new TensorRtCudaKernelLaunchIdentity(Artifact, options, copiedArguments);
            lock (_lifetimeGate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(TensorRtCudaCompiledKernel));
                _activeLaunches++;
            }

            TensorRtCudaKernelLaunch? launch = null;
            try
            {
                CudaDriverModule module = _module ?? throw new InvalidOperationException("A managed CUDA kernel test double cannot launch native work.");
                CudaDriverKernelLaunch nativeLaunch = module.Launch(
                    Artifact.KernelName,
                    options.NativeConfiguration,
                    stream,
                    copiedArguments.Select(argument => argument.NativeArgument).ToArray());
                launch = new TensorRtCudaKernelLaunch(this, nativeLaunch, identity, options.SynchronizationMode);
                if (options.SynchronizationMode == TensorRtCudaSynchronizationMode.KernelCompletion) launch.Synchronize();
                else if (options.SynchronizationMode == TensorRtCudaSynchronizationMode.StreamCompletion) stream.Synchronize();
                return launch;
            }
            catch (Exception exception)
            {
                if (launch != null)
                {
                    try { launch.Dispose(); }
                    catch { }
                }
                else ReleaseLaunch();
                if (exception is TensorRtBackendException) throw;
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.CudaLaunchFailed,
                    "The consumer-owned CUDA Driver kernel launch failed.",
                    exception,
                    operation: "cuda-kernel-launch",
                    technicalDetails: "kernel=" + Artifact.KernelName + ";artifact=" + Artifact.ArtifactSha256 + ";exception=" + exception.GetType().FullName);
            }
        }

        /// <summary>Unloads the owned module after all returned launch owners have been disposed. / 说明相关公共 API。</summary>
        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                if (_activeLaunches != 0) throw new InvalidOperationException("Dispose every CUDA kernel launch owner before disposing its compiled module.");
                _disposed = true;
            }
            _module?.Dispose();
            GC.SuppressFinalize(this);
        }

        internal void ReleaseLaunch()
        {
            lock (_lifetimeGate)
            {
                if (_activeLaunches > 0) _activeLaunches--;
            }
        }

        internal static bool IsStreamDeviceQueryUnavailable(CudaException exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return exception.StatusCode == BridgeStatusCode.NotSupported &&
                   exception.ErrorCategory == BridgeErrorCategory.Cuda;
        }

        private static int ResolveStreamDeviceOrdinal(CudaStream stream)
        {
            try
            {
                return stream.DeviceOrdinal;
            }
            catch (CudaException exception) when (IsStreamDeviceQueryUnavailable(exception))
            {
                return CudaDevice.Current;
            }
        }
    }

    /// <summary>Owns one asynchronous CUDA Driver launch while borrowing its module, stream, and device buffers. / 定义或说明 CUDA合同。</summary>
    public sealed class TensorRtCudaKernelLaunch : IDisposable
    {
        private readonly object _lifetimeGate = new object();
        private readonly TensorRtCudaCompiledKernel _owner;
        private readonly CudaDriverKernelLaunch _nativeLaunch;
        private bool _disposed;

        internal TensorRtCudaKernelLaunch(
            TensorRtCudaCompiledKernel owner,
            CudaDriverKernelLaunch nativeLaunch,
            TensorRtCudaKernelLaunchIdentity identity,
            TensorRtCudaSynchronizationMode synchronizationMode)
        {
            _owner = owner;
            _nativeLaunch = nativeLaunch;
            Identity = identity;
            SynchronizationMode = synchronizationMode;
        }

        /// <summary>Gets the complete managed launch identity. / 获取相关信息。</summary>
        public TensorRtCudaKernelLaunchIdentity Identity { get; }
        /// <summary>Gets the explicit synchronization policy selected for the launch. / 获取配置信息。</summary>
        public TensorRtCudaSynchronizationMode SynchronizationMode { get; }
        /// <summary>Gets whether the kernel completion event has finished. / 获取 CUDA信息。</summary>
        public bool IsCompleted
        {
            get
            {
                lock (_lifetimeGate)
                {
                    ThrowIfDisposed();
                    return _nativeLaunch.IsCompleted;
                }
            }
        }

        /// <summary>Waits for this kernel's completion event and surfaces asynchronous errors. / 说明 CUDA公共 API。</summary>
        public void Synchronize()
        {
            lock (_lifetimeGate)
            {
                ThrowIfDisposed();
                _nativeLaunch.Synchronize();
            }
        }

        /// <summary>Synchronizes pending work, releases the native launch, and releases all borrowed SafeHandle leases. / 表示原生运行时状态或选项。</summary>
        public void Dispose()
        {
            DisposeCore(suppressNativeErrors: false);
            GC.SuppressFinalize(this);
        }

        /// <summary>Synchronizes and releases a forgotten launch owner. / 表示相关状态或选项。</summary>
        ~TensorRtCudaKernelLaunch()
        {
            DisposeCore(suppressNativeErrors: true);
        }

        private void DisposeCore(bool suppressNativeErrors)
        {
            lock (_lifetimeGate)
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    if (suppressNativeErrors)
                    {
                        try { _nativeLaunch.Dispose(); }
                        catch { }
                    }
                    else
                    {
                        _nativeLaunch.Dispose();
                    }
                }
                finally
                {
                    _owner.ReleaseLaunch();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TensorRtCudaKernelLaunch));
        }
    }
}
