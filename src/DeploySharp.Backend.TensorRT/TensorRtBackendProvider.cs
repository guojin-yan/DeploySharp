using System;
using System.Collections.Generic;
using System.IO;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Extensibility;
using JYPPX.DeploySharp.Models;
using JYPPX.TensorRtSharp;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Creates managed sessions over caller-owned, serialized TensorRT engines. / 创建 TensorRT 引擎对象。</summary>
    public sealed class TensorRtBackendProvider : IBackendProvider
    {
        private readonly TensorRtBackendOptions _options;
        private bool _disposed;

        /// <summary>Gets the stable TensorRT backend identifier. / 获取相关信息。</summary>
        public static BackendId BackendId { get; } = new BackendId("tensorrt");

        /// <summary>Initializes a provider over caller-owned TensorRT runtime resources. / 初始化原生运行时对象。</summary>
        public TensorRtBackendProvider(TensorRtBackendOptions? options = null)
        {
            _options = options ?? TensorRtBackendOptions.Default;
            Descriptor = new BackendDescriptor(
                BackendId,
                "TensorRT",
                "4.0.0",
                BackendCapabilities.TensorInference | BackendCapabilities.DynamicShapes,
                new[] { "tensorrt-engine" },
                description: "TensorRT managed adapter; CUDA, cuDNN, NVIDIA driver and the matching native bridge remain consumer-owned.",
                iconKey: "tensorrt",
                supportedTargetFrameworks: new[] { "net8.0" },
                supportedRuntimeIdentifiers: new[] { "win-x64", "linux-x64" },
                supportedDevices: new[] { "cuda" },
                providerPackageId: "JYPPX.TensorRT.CSharp.API",
                providerPackageVersion: "4.0.0",
                preferredExecutionMode: BackendExecutionMode.Worker,
                runtimeDependencies: new IBackendRuntimeDependency[]
                {
                    new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "JYPPX.TensorRT.CSharp.API", "4.0.0"),
                    new BackendRuntimeDependency(BackendRuntimeDependencyKind.Environment, environmentVariables: new[] { "JYPPX_CUDA_ROOT", "JYPPX_CUDNN_ROOT", "JYPPX_TENSORRT_ROOT", "JYPPX_NATIVE_BRIDGE_PATH", "DEPLOYSHARP_TENSORRT_API_VERSION" }, requiresUserSelectedRoot: true),
                    new NativeRuntimeRequirement(NativeRuntimeKind.CUDA, apiLine: "12", runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_CUDA_ROOT", "CUDA_PATH" }),
                    new NativeRuntimeRequirement(NativeRuntimeKind.CuDNN, runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_CUDNN_ROOT" }),
                    new NativeRuntimeRequirement(NativeRuntimeKind.TensorRT, apiLine: ((int)_options.ApiVersion).ToString(), runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_TENSORRT_ROOT" }),
                    new NativeRuntimeRequirement(NativeRuntimeKind.NVRTC, runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true),
                    new NativeRuntimeRequirement(NativeRuntimeKind.Driver, runtimeIdentifiers: new[] { "win-x64", "linux-x64" }),
                    new NativeRuntimeRequirement(NativeRuntimeKind.Unknown, apiLine: "bridge", runtimeIdentifiers: new[] { "win-x64", "linux-x64" }, requiresUserSelectedRoot: true, environmentVariables: new[] { "JYPPX_NATIVE_BRIDGE_PATH" })
                },
                nativeProbeId: "tensorrt-native",
                optionsSchema: new BackendOptionsSchema("tensorrt.options.v1", new[]
                {
                    new BackendOptionDefinition("apiversion", BackendOptionValueType.Enum, ((int)_options.ApiVersion).ToString(), enumValues: new[] { "8", "10", "11" }),
                    new BackendOptionDefinition("optimizationprofile", BackendOptionValueType.Integer, _options.OptimizationProfile.ToString(), minimum: 0),
                    new BackendOptionDefinition("cudatargetarchitecture", BackendOptionValueType.String, _options.CudaTargetArchitecture)
                }),
                healthCheckId: "tensorrt-native");
        }

        /// <summary>Gets the managed adapter descriptor. / 获取相关信息。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether a CUDA TensorRT engine request is compatible. / 说明 TensorRT 引擎公共 API。</summary>
        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (request.BackendId.HasValue && request.BackendId.Value != BackendId) return false;
            if (!string.Equals(artifact.Format, "tensorrt-engine", StringComparison.Ordinal)) return false;
            if (!IsCuda(request.Device)) return false;
            return Descriptor.Supports(request.RequiredCapabilities);
        }

        /// <summary>Loads an external serialized engine into a caller-owned managed session or independent session pool. / 加载 TensorRT 引擎资源，返回调用方持有的会话或彼此独立的会话池。</summary>
        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            if (!CanCreate(artifact, request))
            {
                if (!IsCuda(request.Device))
                {
                    throw new TensorRtBackendException(
                        TensorRtErrorCodes.ConfigurationInvalid,
                        "TensorRT requires the consumer-owned CUDA device; request device 'cuda'.",
                        modelId: artifact.ModelId,
                        operation: "configure",
                        technicalDetails: "device=" + request.Device);
                }

                throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId ?? BackendId);
            }

            if (options.EnableProfiling)
            {
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.ConfigurationInvalid,
                    "Core profiling is not enabled by this adapter; use the consumer-owned TensorRT profiler contract.",
                    modelId: artifact.ModelId,
                    operation: "configure");
            }

            byte[] serializedEngine = TensorRtModelArtifactValidator.ReadValidatedBytes(artifact, _options.MaximumEngineBytes);
            if (options.MaxConcurrency == 1) return CreateSingleSession(artifact, serializedEngine);

            var sessions = new List<TensorRtSession>(options.MaxConcurrency);
            try
            {
                for (int index = 0; index < options.MaxConcurrency; index++) sessions.Add(CreateSingleSession(artifact, serializedEngine));
                return new TensorRtSessionPool(sessions);
            }
            catch
            {
                foreach (TensorRtSession session in sessions) session.Dispose();
                throw;
            }
        }

        /// <summary>Disposes the provider; already-created sessions remain caller-owned. / 释放推理会话资源。</summary>
        public void Dispose() => _disposed = true;

        private static void DisposeAfterFailedLoad(params IDisposable?[] resources)
        {
            foreach (IDisposable? resource in resources)
            {
                try { resource?.Dispose(); }
                catch { }
            }
        }

        private TensorRtSession CreateSingleSession(ModelArtifact artifact, byte[] serializedEngine)
        {
            TensorRtLogger? logger = null;
            TensorRtRuntime? runtime = null;
            TensorRtEngine? engine = null;
            TensorRtExecutionContext? context = null;
            TensorRtInferenceBindings? bindings = null;
            try
            {
                var line = TensorRtApiLineMapper.Map(_options.ApiVersion);
                logger = new TensorRtLogger(line);
                runtime = new TensorRtRuntime(logger);
                engine = runtime.Deserialize(serializedEngine);
                context = engine.CreateExecutionContext();
                bindings = new TensorRtInferenceBindings(engine, context, _options.OptimizationProfile);
                return new TensorRtSession(
                    artifact,
                    logger,
                    runtime,
                    engine,
                    context,
                    bindings,
                    1,
                    _options.CudaTargetArchitecture,
                    _options.CacheImmutableHostInputsOnDevice);
            }
            catch (TensorRtBackendException)
            {
                DisposeAfterFailedLoad(bindings, context, engine, runtime, logger);
                throw;
            }
            catch (Exception exception)
            {
                DisposeAfterFailedLoad(bindings, context, engine, runtime, logger);
                throw new TensorRtBackendException(
                    TensorRtErrorCodes.NativeRuntimeUnavailable,
                    "TensorRT engine loading requires the consumer-owned native bridge, CUDA runtime, driver, and a compatible GPU.",
                    exception,
                    artifact.ModelId,
                    operation: "load",
                    technicalDetails: exception.GetType().FullName + ": " + exception.Message);
            }
        }

        private static bool IsCuda(string? device)
        {
            return string.IsNullOrWhiteSpace(device) || string.Equals(device.Trim(), "cuda", StringComparison.OrdinalIgnoreCase);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new TensorRtBackendException(TensorRtErrorCodes.ObjectDisposed, "The TensorRT provider has been disposed.", operation: "provider");
            }
        }
    }
}
