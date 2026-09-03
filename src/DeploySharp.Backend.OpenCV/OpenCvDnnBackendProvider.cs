using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.OpenCvSharp;
using JYPPX.OpenCvSharp.Dnn;
using CoreCv2 = JYPPX.OpenCvSharp.Core.Cv2;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Creates CPU OpenCV DNN sessions for exact static or runtime-dynamic contracts with guarded dynamic outputs and independent session pooling. / 为精确静态或运行时动态合同创建 CPU OpenCV DNN 会话，并支持受保护动态输出和独立 Session 池。</summary>
    public sealed class OpenCvDnnBackendProvider : IBackendProvider
    {
        private readonly OpenCvDnnOptions _options;
        private bool _disposed;

        /// <summary>Gets the stable OpenCV DNN backend identifier. / 获取稳定的 OpenCV DNN 后端标识符。</summary>
        public static BackendId BackendId { get; } = new BackendId("opencv-dnn");

        /// <summary>Initializes a provider bound to one exact model contract. / 初始化绑定到一个精确模型合同的 Provider。</summary>
        public OpenCvDnnBackendProvider(OpenCvDnnOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            Descriptor = new BackendDescriptor(BackendId, "OpenCV DNN CPU", OpenCvSharpBuildInfo.PackageVersion, BackendCapabilities.TensorInference, new[] { "onnx" });
        }

        /// <summary>Gets backend identity and the intentionally synchronous CPU capability set. / 获取后端身份和有意保持同步的 CPU 能力集。</summary>
        public BackendDescriptor Descriptor { get; }

        /// <summary>Determines whether the artifact and request match this provider's exact contract. / 判断工件和请求是否匹配此 Provider 的精确合同。</summary>
        public bool CanCreate(ModelArtifact artifact, BackendRequest request)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();
            return artifact.ModelId == _options.Contract.ModelId
                && string.Equals(artifact.Format, "onnx", StringComparison.Ordinal)
                && (!request.BackendId.HasValue || request.BackendId.Value == BackendId)
                && (request.RequiredCapabilities & ~Descriptor.Capabilities) == 0
                && IsCpu(request.Device);
        }

        /// <summary>Loads the ONNX graph, selects OpenCV CPU, and returns one or more independently-loaded sessions according to the requested concurrency. / 加载 ONNX 图、选择 OpenCV CPU，并根据请求并发数返回一个或多个独立加载的 Session。</summary>
        public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (options == null) throw new ArgumentNullException(nameof(options));
            ThrowIfDisposed();
            if (!CanCreate(artifact, request))
            {
                if (!IsCpu(request.Device)) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ConfigurationInvalid, "OpenCV DNN v1 is verified only for the CPU target.", modelId: artifact.ModelId, operation: "configure", technicalDetails: "device=" + request.Device);
                throw new BackendNotCompatibleException(artifact.ModelId, request.BackendId ?? BackendId);
            }
            string path = OpenCvDnnModelArtifactValidator.Validate(artifact);
            try
            {
                // OpenCV exposes one process-global thread pool. Configure it before
                // importing/running any graph; callers using multiple pooled sessions
                // can lower this value to avoid oversubscription.
                if (_options.NumThreads.HasValue) CoreCv2.SetNumThreads(_options.NumThreads.Value);
                string nativeVersion = OpenCvSharpBuildInfo.GetNativeOpenCvVersion();
                if (!OpenCvSharpBuildInfo.IsNativeOpenCvVersionCompatible()) throw new OpenCvDnnBackendException(DeploySharpErrorCodes.NativeRuntimeUnavailable, "The OpenCV native runtime version is incompatible with the managed API.", modelId: artifact.ModelId, operation: "preflight", technicalDetails: "managed=" + OpenCvSharpBuildInfo.OpenCvVersion + ";native=" + nativeVersion);
                if (options.MaxConcurrency == 1) return CreateSingleSession(path, artifact);
                var sessions = new List<OpenCvDnnSession>(options.MaxConcurrency);
                try
                {
                    for (int index = 0; index < options.MaxConcurrency; index++) sessions.Add(CreateSingleSession(path, artifact));
                    return new OpenCvDnnSessionPool(sessions);
                }
                catch
                {
                    foreach (OpenCvDnnSession session in sessions) session.Dispose();
                    throw;
                }
            }
            catch (OpenCvDnnBackendException) { throw; }
            catch (DllNotFoundException exception) { throw NativeUnavailable(artifact, exception); }
            catch (BadImageFormatException exception) { throw NativeUnavailable(artifact, exception); }
            catch (EntryPointNotFoundException exception) { throw NativeUnavailable(artifact, exception); }
            catch (Exception exception) { throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ModelLoadFailed, "OpenCV DNN could not load the ONNX model.", exception, artifact.ModelId, operation: "load", technicalDetails: exception.Message); }
        }

        /// <summary>Disposes the provider without disposing sessions already returned to callers. / 释放 Provider，但不释放已返回给调用方的 Session。</summary>
        public void Dispose() => _disposed = true;

        private Net LoadNetwork(string path, ModelArtifact artifact, IReadOnlyList<TensorDescriptor> inputDescriptors)
        {
            try
            {
                byte[] model = File.ReadAllBytes(path);
                byte[] normalized = model;
                bool normalizedChanged = false;
#if NET8_0_OR_GREATER
                // OpenCV 5.0's importer is not re-entrant-safe for the dynamic
                // Transformer graphs used by DEIMv2 and RF-DETR. Their raw
                // graphs produce a managed importer diagnostic, while the
                // generic compatibility rewrites can turn the same failure
                // into a native access violation. Keep those families raw and
                // let the provider report the unsupported graph safely.
                if (!OpenCvDnnOnnxCompatibilityPasses.IsNativeImporterHazard(model))
                    normalized = OpenCvDnnOnnxCompatibilityPasses.Normalize(model, out normalizedChanged);
#endif
                if (!_options.SpecializeDynamicInputShapes)
                {
                    return normalizedChanged ? Net.ReadNetFromOnnx(normalized, DnnEngine.Classic) : Net.ReadNetFromOnnx(path, DnnEngine.Classic);
                }
                byte[] specialized = OpenCvDnnOnnxInputSpecializer.Specialize(normalized, inputDescriptors, out bool specializedChanged);
                return normalizedChanged || specializedChanged ? Net.ReadNetFromOnnx(specialized, DnnEngine.Classic) : Net.ReadNetFromOnnx(path, DnnEngine.Classic);
            }
            catch (OpenCvDnnBackendException) { throw; }
            catch (Exception exception)
            {
                throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ModelLoadFailed, "OpenCV DNN could not specialize the ONNX input contract.", exception, artifact.ModelId, operation: "specialize-input", technicalDetails: exception.Message);
            }
        }

        private OpenCvDnnSession CreateSingleSession(string path, ModelArtifact artifact)
        {
            if (_options.Contract.Inputs.Any(value => value.Shape.IsDynamic))
            {
                if (!_options.SpecializeDynamicInputShapes) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ConfigurationInvalid, "Runtime-dynamic OpenCV DNN inputs require input-shape specialization to be enabled.", modelId: artifact.ModelId, operation: "configure");
                return new OpenCvDnnSession(artifact, _options.Contract, descriptors => CreateConfiguredNetwork(path, artifact, descriptors));
            }

            Net? network = null;
            try
            {
                network = CreateConfiguredNetwork(path, artifact, _options.Contract.Inputs);
                var session = new OpenCvDnnSession(artifact, network, _options.Contract);
                network = null;
                return session;
            }
            finally { network?.Dispose(); }
        }

        private Net CreateConfiguredNetwork(string path, ModelArtifact artifact, IReadOnlyList<TensorDescriptor> descriptors)
        {
            Net? network = null;
            try
            {
                network = LoadNetwork(path, artifact, descriptors);
                if (network.Empty) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ModelLoadFailed, "OpenCV DNN loaded an empty network.", modelId: artifact.ModelId, operation: "load");
                network.SetPreferableBackend(DnnBackend.OpenCV).SetPreferableTarget(DnnTarget.Cpu).EnableFusion(_options.EnableFusion).EnableWinograd(_options.EnableWinograd);
                Net result = network;
                network = null;
                return result;
            }
            finally { network?.Dispose(); }
        }

        private static bool IsCpu(string? device) => string.IsNullOrWhiteSpace(device) || string.Equals(device!.Trim(), "cpu", StringComparison.OrdinalIgnoreCase);
        private static OpenCvDnnBackendException NativeUnavailable(ModelArtifact artifact, Exception exception) => new OpenCvDnnBackendException(DeploySharpErrorCodes.NativeRuntimeUnavailable, "The OpenCV native runtime is unavailable or ABI-incompatible. Install a matching JYPPX.OpenCV.runtime package for the current RID.", exception, artifact.ModelId, operation: "preflight", technicalDetails: exception.Message);
        private void ThrowIfDisposed() { if (_disposed) throw new OpenCvDnnBackendException(OpenCvDnnErrorCodes.ObjectDisposed, "The OpenCV DNN provider has been disposed.", operation: "provider"); }
    }
}
