using System;
using JYPPX.DeploySharp.Backends.OnnxRuntime.Internal;
using JYPPX.DeploySharp.Errors;
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

        /// <summary>Initializes a CPU-only managed provider. Native runtime selection remains application-owned. / 初始化仅 CPU 的托管 Provider；原生运行时选择仍由应用负责。</summary>
        public OnnxRuntimeBackendProvider(OnnxRuntimeOptions? options = null)
        {
            _options = options ?? OnnxRuntimeOptions.Default;
            Descriptor = new BackendDescriptor(BackendId, "ONNX Runtime", "1.28.0", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { "onnx" });
        }

        /// <summary>Gets verified format and managed execution capabilities. / 获取已验证的格式与托管执行能力。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether a CPU ONNX session can satisfy the request. / 确定 CPU ONNX 会话是否能够满足请求。</summary>
        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (request.BackendId.HasValue && request.BackendId.Value != BackendId) return false;
            if (!string.Equals(artifact.Format, "onnx", StringComparison.Ordinal)) return false;
            if (!IsCpu(request.Device)) return false;
            return Descriptor.Supports(request.RequiredCapabilities);
        }

        /// <summary>Validates and loads a local ONNX model into a caller-owned session. / 验证并加载本地 ONNX 模型，返回调用方持有的会话。</summary>
        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, CoreSessionOptions options)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            if (!CanCreate(artifact, request))
            {
                if (!IsCpu(request.Device)) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ConfigurationInvalid, "This package has verified only the CPU execution provider; request device 'cpu'.", modelId: artifact.ModelId, operation: "configure", technicalDetails: "device=" + request.Device);
                throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId ?? BackendId);
            }
            string modelPath = OnnxModelArtifactValidator.Validate(artifact);
            OnnxRuntimeNativePreflight.Validate(artifact);
            Microsoft.ML.OnnxRuntime.SessionOptions? nativeOptions = null;
            InferenceSession? nativeSession = null;
            try
            {
                nativeOptions = CreateNativeOptions(options, artifact);
                nativeSession = new InferenceSession(modelPath, nativeOptions);
                var session = new OnnxRuntimeSession(artifact, nativeSession, options.MaxConcurrency, _options.IntraOpThreads != 1);
                nativeSession = null;
                return session;
            }
            catch (Exception exception)
            {
                nativeSession?.Dispose();
                throw OnnxRuntimeExceptionMapper.Map(exception, artifact, "load");
            }
            finally { nativeOptions?.Dispose(); }
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
            value.AppendExecutionProvider_CPU(_options.EnableCpuMemoryArena ? 1 : 0);
            return value;
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

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ObjectDisposed, "The ONNX Runtime provider has been disposed.", operation: "provider");
        }
    }
}
