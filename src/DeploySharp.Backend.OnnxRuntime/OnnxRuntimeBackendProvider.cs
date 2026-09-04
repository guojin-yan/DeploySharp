using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Backends.OnnxRuntime.Internal;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Extensibility;
using JYPPX.DeploySharp.Models;
using Microsoft.ML.OnnxRuntime;
using CoreSessionOptions = JYPPX.DeploySharp.Models.SessionOptions;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime
{
    /// <summary>Creates Core tensor-inference sessions through ONNX Runtime 1.28 managed APIs. / 通过 ONNX Runtime 1.28 托管 API 创建 Core 张量推理会话。</summary>
    public sealed class OnnxRuntimeBackendProvider : IBackendProvider
    {
        private readonly OnnxRuntimeOptions _options;
        private bool _disposed;

        /// <summary>Gets the stable ONNX Runtime backend identifier. / 获取稳定的 ONNX Runtime 后端标识。</summary>
        public static BackendId BackendId { get; } = new BackendId("onnxruntime");

        /// <summary>Initializes a managed provider. Native runtime selection remains application-owned; CUDA requires a matching official GPU runtime package. / 初始化托管 Provider；原生运行时选择仍由应用负责，CUDA 需要匹配的官方 GPU 运行时包。</summary>
        public OnnxRuntimeBackendProvider(OnnxRuntimeOptions? options = null)
        {
            _options = options ?? OnnxRuntimeOptions.Default;
            Descriptor = new BackendDescriptor(
                BackendId,
                "ONNX Runtime",
                "1.28.0",
                BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes,
                new[] { "onnx" },
                description: "ONNX Runtime managed adapter with explicit CPU or CUDA execution-provider selection.",
                iconKey: "onnxruntime",
                supportedTargetFrameworks: new[] { "netstandard2.0", "net8.0" },
                supportedRuntimeIdentifiers: new[] { "win-x64", "linux-x64", "linux-arm64" },
                supportedDevices: new[] { "cpu", "cuda" },
                providerPackageId: _options.ExecutionProvider == OnnxRuntimeExecutionProvider.Cuda ? "Microsoft.ML.OnnxRuntime.Gpu.Windows" : "Microsoft.ML.OnnxRuntime.Managed",
                providerPackageVersion: "1.28.0",
                preferredExecutionMode: _options.ExecutionProvider == OnnxRuntimeExecutionProvider.Cuda ? BackendExecutionMode.Worker : BackendExecutionMode.InProcessOrWorker,
                runtimeDependencies: new IBackendRuntimeDependency[]
                {
                    new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "Microsoft.ML.OnnxRuntime.Managed", "1.28.0"),
                    new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "Microsoft.ML.OnnxRuntime", "1.28.0", downloadable: true, licenseExpression: "MIT"),
                    new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "Microsoft.ML.OnnxRuntime.Gpu.Windows", "1.28.0", "win-x64", downloadable: true, licenseExpression: "MIT", condition: "executionProvider == cuda")
                },
                nativeProbeId: "onnxruntime-native",
                optionsSchema: new BackendOptionsSchema("onnxruntime.options.v1", new[]
                {
                    new BackendOptionDefinition("executionprovider", BackendOptionValueType.Enum, "cpu", enumValues: new[] { "cpu", "cuda" }),
                    new BackendOptionDefinition("cudadeviceid", BackendOptionValueType.Integer, "0", minimum: 0, visibleWhen: "executionProvider == cuda")
                }),
                healthCheckId: "onnxruntime-native");
        }

        /// <summary>Gets verified format and managed execution capabilities. / 获取已验证的格式与托管执行能力。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether an ONNX session can satisfy the request. / 确定 ONNX 会话是否能够满足请求。</summary>
        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (request.BackendId.HasValue && request.BackendId.Value != BackendId) return false;
            if (!string.Equals(artifact.Format, "onnx", StringComparison.Ordinal)) return false;
            if (_options.ExecutionProvider == OnnxRuntimeExecutionProvider.Cpu)
            {
                if (!IsCpu(request.Device)) return false;
            }
            else if (!IsCuda(request.Device))
            {
                return false;
            }
            return Descriptor.Supports(request.RequiredCapabilities);
        }

        /// <summary>Validates and loads a local ONNX model into a caller-owned session or independent session pool. / 验证并加载本地 ONNX 模型，返回调用方持有的会话或彼此独立的会话池。</summary>
        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, CoreSessionOptions options)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            if (!CanCreate(artifact, request))
            {
                string expectedDevice = _options.ExecutionProvider == OnnxRuntimeExecutionProvider.Cuda ? "cuda" : "cpu";
                if ((_options.ExecutionProvider == OnnxRuntimeExecutionProvider.Cpu && !IsCpu(request.Device)) || (_options.ExecutionProvider == OnnxRuntimeExecutionProvider.Cuda && !IsCuda(request.Device)))
                {
                    throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ConfigurationInvalid, "The configured ONNX Runtime execution provider requires request device '" + expectedDevice + "'.", modelId: artifact.ModelId, operation: "configure", technicalDetails: "device=" + request.Device);
                }
                throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId ?? BackendId);
            }
            string modelPath = OnnxModelArtifactValidator.Validate(artifact);
            OnnxRuntimeNativePreflight.Validate(artifact);
            try
            {
                if (options.MaxConcurrency == 1) return CreateSingleSession(modelPath, artifact, options);
                var sessions = new List<OnnxRuntimeSession>(options.MaxConcurrency);
                try
                {
                    for (int index = 0; index < options.MaxConcurrency; index++) sessions.Add(CreateSingleSession(modelPath, artifact, options));
                    return new OnnxRuntimeSessionPool(sessions);
                }
                catch
                {
                    foreach (OnnxRuntimeSession session in sessions) session.Dispose();
                    throw;
                }
            }
            catch (Exception exception)
            {
                throw OnnxRuntimeExceptionMapper.Map(exception, artifact, "load");
            }
        }

        /// <summary>Disposes this provider without disposing sessions already returned to callers. / 释放当前 Provider，但不释放已返回给调用方的会话。</summary>
        /// <remarks>Provider disposal is idempotent. / Provider 释放是幂等的。</remarks>
        public void Dispose() { _disposed = true; }

        private Microsoft.ML.OnnxRuntime.SessionOptions CreateNativeOptions(CoreSessionOptions coreOptions, ModelArtifact artifact)
        {
            var value = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                IntraOpNumThreads = _options.IntraOpThreads,
                InterOpNumThreads = _options.InterOpThreads,
                GraphOptimizationLevel = Map(_options.GraphOptimization),
                ExecutionMode = _options.ExecutionMode == OnnxRuntimeExecutionMode.Sequential ? ExecutionMode.ORT_SEQUENTIAL : ExecutionMode.ORT_PARALLEL,
                EnableMemoryPattern = _options.EnableMemoryPattern,
                EnableCpuMemArena = _options.EnableCpuMemoryArena,
                LogSeverityLevel = (OrtLoggingLevel)(int)_options.LogSeverity
            };
            if (_options.LogId != null) value.LogId = _options.LogId;
            if (coreOptions.EnableProfiling)
            {
                if (_options.ProfilingOutputPathPrefix == null)
                {
                    value.Dispose();
                    throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ConfigurationInvalid, "Profiling requires a non-empty profiling output path prefix.", modelId: artifact.ModelId, operation: "configure");
                }
                value.ProfileOutputPathPrefix = _options.ProfilingOutputPathPrefix;
                value.EnableProfiling = true;
            }
            if (_options.ExecutionProvider == OnnxRuntimeExecutionProvider.Cuda)
            {
                value.AppendExecutionProvider_CUDA(_options.CudaDeviceId);
            }
            else
            {
                value.AppendExecutionProvider_CPU(_options.EnableCpuMemoryArena ? 1 : 0);
            }
            return value;
        }

        private OnnxRuntimeSession CreateSingleSession(string modelPath, ModelArtifact artifact, CoreSessionOptions options)
        {
            Microsoft.ML.OnnxRuntime.SessionOptions? nativeOptions = null;
            InferenceSession? nativeSession = null;
            try
            {
                nativeOptions = CreateNativeOptions(options, artifact);
                nativeSession = new InferenceSession(modelPath, nativeOptions);
                var session = new OnnxRuntimeSession(artifact, nativeSession, 1, _options.IntraOpThreads != 1);
                nativeSession = null;
                return session;
            }
            finally
            {
                nativeSession?.Dispose();
                nativeOptions?.Dispose();
            }
        }

        private static GraphOptimizationLevel Map(OnnxRuntimeGraphOptimization value)
        {
            switch (value)
            {
                case OnnxRuntimeGraphOptimization.Disabled: return GraphOptimizationLevel.ORT_DISABLE_ALL;
                case OnnxRuntimeGraphOptimization.Basic: return GraphOptimizationLevel.ORT_ENABLE_BASIC;
                case OnnxRuntimeGraphOptimization.Extended: return GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
                case OnnxRuntimeGraphOptimization.All: return GraphOptimizationLevel.ORT_ENABLE_ALL;
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static bool IsCpu(string? device)
        {
            if (string.IsNullOrWhiteSpace(device)) return true;
            return string.Equals(device!.Trim(), "cpu", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCuda(string? device)
        {
            return !string.IsNullOrWhiteSpace(device) && string.Equals(device!.Trim(), "cuda", StringComparison.OrdinalIgnoreCase);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ObjectDisposed, "The ONNX Runtime provider has been disposed.", operation: "provider");
        }
    }
}
