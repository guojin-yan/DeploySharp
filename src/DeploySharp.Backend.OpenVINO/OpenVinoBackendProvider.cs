using System;
using System.Collections.Generic;
using System.IO;
using JYPPX.DeploySharp.Backends.OpenVINO.Internal;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Extensibility;
using JYPPX.DeploySharp.Models;
using OpenVinoSharp;
using CoreSessionOptions = JYPPX.DeploySharp.Models.SessionOptions;

namespace JYPPX.DeploySharp.Backends.OpenVINO
{
    /// <summary>Creates Core tensor-inference sessions through OpenVINO C# API 3.3.0. / 通过 OpenVINO C# API 3.3.0 创建 Core 张量推理会话。</summary>
    public sealed class OpenVinoBackendProvider : IBackendProvider
    {
        private readonly OpenVinoOptions _options;
        private bool _disposed;

        /// <summary>Gets the stable OpenVINO backend identifier. / 获取稳定的 OpenVINO 后端标识。</summary>
        public static BackendId BackendId { get; } = new BackendId("openvino");

        /// <summary>Initializes a CPU-only managed provider; native runtime selection remains application-owned. / 初始化仅支持 CPU 的托管 provider；原生运行时选择仍由应用负责。</summary>
        public OpenVinoBackendProvider(OpenVinoOptions? options = null)
        {
            _options = options ?? OpenVinoOptions.Default;
            Descriptor = new BackendDescriptor(
                BackendId,
                "OpenVINO",
                "3.3.0",
                BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes,
                new[] { "onnx", "openvino-ir" },
                description: "OpenVINO C# API adapter using an application-owned runtime and device plug-ins.",
                iconKey: "openvino",
                supportedTargetFrameworks: new[] { "net48", "net8.0", "net10.0" },
                supportedRuntimeIdentifiers: new[] { "win-x64", "linux-x64" },
                supportedDevices: new[] { "CPU" },
                providerPackageId: "JYPPX.OpenVINO.CSharp.API",
                providerPackageVersion: "3.3.0",
                preferredExecutionMode: BackendExecutionMode.InProcessOrWorker,
                runtimeDependencies: new IBackendRuntimeDependency[]
                {
                    new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "JYPPX.OpenVINO.CSharp.API", "3.3.0"),
                    new BackendRuntimeDependency(BackendRuntimeDependencyKind.ManagedPackage, "OpenVINO.runtime.win", "2026.2.1", "win-x64", downloadable: true, licenseExpression: "Apache-2.0")
                },
                nativeProbeId: "openvino-native",
                optionsSchema: new BackendOptionsSchema("openvino.options.v1", new[]
                {
                    new BackendOptionDefinition("device", BackendOptionValueType.Enum, "CPU", enumValues: new[] { "CPU" }),
                    new BackendOptionDefinition("allowdynamicshapes", BackendOptionValueType.Boolean, "true")
                }),
                healthCheckId: "openvino-native");
        }

        /// <summary>Gets verified format and managed execution capabilities. / 获取已经验证的格式与托管执行能力。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether a CPU ONNX or IR session can satisfy the request. / 确定 CPU ONNX 或 IR 会话是否可满足请求。</summary>
        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            if (request.BackendId.HasValue && request.BackendId.Value != BackendId) return false;
            if (!string.Equals(artifact.Format, "onnx", StringComparison.Ordinal) && !string.Equals(artifact.Format, "openvino-ir", StringComparison.Ordinal)) return false;
            if (!IsCpu(request.Device)) return false;
            return Descriptor.Supports(request.RequiredCapabilities);
        }

        /// <summary>Validates, reads, and compiles a local ONNX or IR model into a caller-owned session or independent session pool. / 验证、读取并编译本地 ONNX 或 IR 模型，返回调用方持有的会话或彼此独立的会话池。</summary>
        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, CoreSessionOptions options)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            if (!CanCreate(artifact, request))
            {
                if (!IsCpu(request.Device)) throw new OpenVinoBackendException(OpenVinoErrorCodes.ConfigurationInvalid, "This release verifies only the CPU device; request device 'CPU'.", modelId: artifact.ModelId, operation: "configure", device: request.Device, technicalDetails: "device=" + request.Device);
                throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId ?? BackendId);
            }
            string modelPath = OpenVinoModelArtifactValidator.Validate(artifact);
            OpenVinoNativePreflight.Validate(artifact);
            try
            {
                if (options.MaxConcurrency == 1) return CreateSingleSession(modelPath, artifact, options);
                var sessions = new List<OpenVinoSession>(options.MaxConcurrency);
                try
                {
                    for (int index = 0; index < options.MaxConcurrency; index++) sessions.Add(CreateSingleSession(modelPath, artifact, options));
                    return new OpenVinoSessionPool(sessions);
                }
                catch
                {
                    foreach (OpenVinoSession session in sessions) session.Dispose();
                    throw;
                }
            }
            catch (Exception exception)
            {
                throw OpenVinoExceptionMapper.Map(exception, artifact, "load-compile", _options.Device);
            }
        }

        /// <summary>Disposes this provider without disposing sessions already returned to callers. / 释放当前 provider，但不释放已经返回给调用方的会话。</summary>
        /// <remarks>Provider disposal is idempotent. / Provider 释放是幂等的。</remarks>
        public void Dispose() { _disposed = true; }

        private Dictionary<string, string> CreateCompileProperties(CoreSessionOptions sessionOptions)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> property in _options.CompileProperties) values.Add(property.Key, property.Value);
            if (sessionOptions.EnableProfiling && !values.ContainsKey("PERF_COUNT")) values.Add("PERF_COUNT", "YES");
            if (_options.CacheDirectory != null) Directory.CreateDirectory(_options.CacheDirectory);
            return values;
        }

        private OpenVinoSession CreateSingleSession(string modelPath, ModelArtifact artifact, CoreSessionOptions options)
        {
            Core? core = null;
            Model? model = null;
            CompiledModel? compiled = null;
            try
            {
                core = new Core();
                IReadOnlyList<string> devices = core.GetAvailableDevices();
                bool cpuAvailable = false;
                for (int index = 0; index < devices.Count; index++) if (string.Equals(devices[index], "CPU", StringComparison.OrdinalIgnoreCase)) cpuAvailable = true;
                if (!cpuAvailable) throw new OpenVinoBackendException(OpenVinoErrorCodes.DeviceUnavailable, "The OpenVINO CPU plug-in is not available.", modelId: artifact.ModelId, operation: "device-discovery", device: _options.Device, technicalDetails: string.Join(",", devices));

                string weights = string.Equals(artifact.Format, "openvino-ir", StringComparison.Ordinal) ? Path.ChangeExtension(modelPath, ".bin") : string.Empty;
                model = core.ReadModel(modelPath, weights);
                Dictionary<string, string> properties = CreateCompileProperties(options);
                compiled = core.CompileModel(model, _options.Device, properties);
                var session = new OpenVinoSession(artifact, core, model, compiled, 1, _options.AllowDynamicShapes, _options.Device);
                core = null;
                model = null;
                compiled = null;
                return session;
            }
            finally
            {
                compiled?.Dispose();
                model?.Dispose();
                core?.Dispose();
            }
        }

        private static bool IsCpu(string? device) => string.IsNullOrWhiteSpace(device) || string.Equals(device!.Trim(), "CPU", StringComparison.OrdinalIgnoreCase);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new OpenVinoBackendException(OpenVinoErrorCodes.ObjectDisposed, "The OpenVINO provider has been disposed.", operation: "provider");
        }
    }
}
